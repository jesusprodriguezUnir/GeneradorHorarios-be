using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

/// <summary>
/// Persistencia del resultado de una generación de horario: tanto el fallo de
/// viabilidad (sin entries) como el resultado del motor con sus conflictos.
/// </summary>
public sealed class ScheduleResultPersister(IAppDbContext db, IScheduleRepository scheduleRepository)
{
    public async Task<Guid> PersistViabilityFailureAsync(
        Guid schoolId,
        Guid stageId,
        Guid periodId,
        string academicYear,
        Guid createdBy,
        IReadOnlyList<ConflictExplanation> viabilityErrors,
        CancellationToken ct)
    {
        var failedSchedule = new ScheduleRecord
        {
            SchoolId = schoolId, StageId = stageId, AcademicYear = academicYear,
            Status = "failed",
            GeneratedAt = DateTime.UtcNow, GenerationSeconds = 0,
            TotalConflicts = viabilityErrors.Count,
            PeriodId = periodId,
            CreatedBy = createdBy,
        };

        var failedConflicts = viabilityErrors.Select(conflict => ToConflictRecord(failedSchedule.Id, conflict)).ToList();

        await scheduleRepository.AddScheduleWithDetailsAsync(failedSchedule, [], failedConflicts, ct);
        return failedSchedule.Id;
    }

    public async Task<Guid> PersistSuccessAsync(
        Guid schoolId,
        Guid stageId,
        Guid periodId,
        string academicYear,
        Guid createdBy,
        ScheduleResult engineResult,
        List<ConflictExplanation> normativeIssues,
        List<ConflictExplanation> coverageConflicts,
        List<ConflictExplanation> teacherHoursConflicts,
        List<Classroom> classrooms,
        CancellationToken ct)
    {
        var homeClassroomMap = await db.CourseGroups.AsNoTracking()
            .Where(g => g.StageId == stageId && g.HomeClassroomId.HasValue)
            .ToDictionaryAsync(g => g.Id, g => g.HomeClassroomId!.Value, ct);
        var defaultClassroomId = classrooms
            .Where(c => c.ClassroomType == "regular")
            .Select(c => c.Id).FirstOrDefault();
        var validClassroomIds = classrooms.Select(c => c.Id).ToHashSet();

        var schedule = new ScheduleRecord
        {
            SchoolId = schoolId, StageId = stageId, AcademicYear = academicYear,
            Status = "generated",
            GeneratedAt = DateTime.UtcNow, GenerationSeconds = engineResult.ElapsedSeconds,
            TotalConflicts = engineResult.Conflicts.Count(c => c.Severity == ConflictSeverity.Error)
                           + normativeIssues.Count(c => c.Severity == ConflictSeverity.Error)
                           + coverageConflicts.Count + teacherHoursConflicts.Count,
            PeriodId = periodId,
            CreatedBy = createdBy,
        };

        var entries = engineResult.AssignedSlots.Select(slot =>
        {
            var classroomId = slot.ClassroomId != Guid.Empty && validClassroomIds.Contains(slot.ClassroomId)
                ? slot.ClassroomId
                : homeClassroomMap.GetValueOrDefault(slot.GroupId, defaultClassroomId);

            return new ScheduleEntry
            {
                ScheduleId = schedule.Id, SchoolId = schoolId,
                GroupId = slot.GroupId, AllocationId = slot.AllocationId,
                TeacherId = slot.TeacherId, ClassroomId = classroomId,
                DayOfWeek = slot.DayOfWeek, SlotIndex = slot.SlotIndex,
            };
        }).ToList();

        var allConflicts = engineResult.Conflicts
            .Concat(normativeIssues).Concat(coverageConflicts).Concat(teacherHoursConflicts).ToList();

        var conflictRecords = allConflicts.Select(conflict => ToConflictRecord(schedule.Id, conflict)).ToList();

        await scheduleRepository.AddScheduleWithDetailsAsync(schedule, entries, conflictRecords, ct);
        return schedule.Id;
    }

    private static ScheduleConflictRecord ToConflictRecord(Guid scheduleId, ConflictExplanation conflict) => new()
    {
        ScheduleId = scheduleId,
        ConflictType = conflict.Type.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture),
        Severity = conflict.Severity.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture),
        Description = conflict.Description,
        Suggestions = JsonSerializer.Serialize(conflict.Suggestions),
        GroupId = conflict.GroupId, TeacherId = conflict.TeacherId,
        DayOfWeek = conflict.DayOfWeek, SlotIndex = conflict.SlotIndex,
    };
}
