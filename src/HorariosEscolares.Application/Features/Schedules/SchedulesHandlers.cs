using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules;

public record ScheduleListDto(
    Guid Id, string AcademicYear, string Status,
    DateTime? GeneratedAt, DateTime? PublishedAt,
    int TotalConflicts, int? GenerationSeconds);

public record ConflictDto(
    string Type, string Severity, string Description, string[] Suggestions,
    Guid? GroupId, Guid? TeacherId, int? DayOfWeek, int? SlotIndex);

public record ScheduleGridDto(
    Guid ScheduleId, string Status, string AcademicYear,
    IReadOnlyList<ScheduleGridEntry> Entries,
    IReadOnlyList<ConflictDto> Conflicts,
    IReadOnlyList<SlotInfoDto> Slots,
    IReadOnlyDictionary<int, IReadOnlyList<SlotInfoDto>> SlotsByCycle);

public record ScheduleGridEntry(
    Guid Id, int DayOfWeek, int SlotIndex,
    Guid GroupId, string GroupDisplay,
    Guid TeacherId, string TeacherName, string TeacherColorKey,
    Guid AllocationId, string SubjectName, string SubjectKey, string SubjectShort,
    Guid ClassroomId, string ClassroomName, bool IsManualOverride);

public record SlotInfoDto(int Index, string StartTime, string EndTime, bool IsBreak);

public record MyScheduleDto(
    string TeacherName, string SchoolName, string AcademicYear,
    IReadOnlyList<MyScheduleEntry> Entries,
    IReadOnlyList<SlotInfoDto> Slots);

public record MyScheduleEntry(
    int DayOfWeek, int SlotIndex, string SlotTime,
    string SubjectName, string SubjectKey, string SubjectShort,
    string GroupLabel, string ClassroomName);

public record UpdateEntryRequest(Guid TeacherId, Guid ClassroomId);

public record GetSchedulesListQuery : IRequest<List<ScheduleListDto>>;
public record GetScheduleGridQuery(Guid ScheduleId) : IRequest<ScheduleGridDto?>;
public record GetMyScheduleQuery : IRequest<MyScheduleDto?>;
public record PublishScheduleCommand(Guid ScheduleId) : IRequest<string>;
public record UpdateScheduleEntryCommand(Guid ScheduleId, Guid EntryId, UpdateEntryRequest Request) : IRequest;

public sealed class GetSchedulesListHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetSchedulesListQuery, List<ScheduleListDto>>
{
    public async Task<List<ScheduleListDto>> Handle(GetSchedulesListQuery request, CancellationToken ct)
    {
        return await db.Schedules.AsNoTracking()
            .Where(s => s.SchoolId == user.SchoolId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ScheduleListDto(s.Id, s.AcademicYear, s.Status,
                s.GeneratedAt, s.PublishedAt, s.TotalConflicts, s.GenerationSeconds))
            .ToListAsync(ct);
    }
}

public sealed class GetScheduleGridHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetScheduleGridQuery, ScheduleGridDto?>
{
    public async Task<ScheduleGridDto?> Handle(GetScheduleGridQuery request, CancellationToken ct)
    {
        var schedule = await db.Schedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.SchoolId == user.SchoolId, ct);
        if (schedule is null) return null;

        if (user.IsTeacher && schedule.Status != "published") return null;

        var entries = await db.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId == schedule.Id).ToListAsync(ct);
        var dbConflicts = await db.ScheduleConflicts.AsNoTracking()
            .Where(c => c.ScheduleId == schedule.Id).ToListAsync(ct);
        var allocations = await db.SubjectAllocations.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schedule.SchoolId).ToDictionaryAsync(t => t.Id, ct);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schedule.SchoolId).ToDictionaryAsync(g => g.Id, ct);
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schedule.SchoolId).ToDictionaryAsync(c => c.Id, ct);
        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schedule.SchoolId, ct);
        var cycles = school is not null
            ? await db.CycleSchedules.AsNoTracking()
                .Where(c => c.SchoolId == schedule.SchoolId).ToListAsync(ct)
            : [];

        var slots = school is not null ? SlotCalculator.Compute(school, school.SlotsPerDay) : [];

        var slotsByCycle = cycles
            .OrderBy(c => c.Cycle)
            .ToDictionary(
                c => c.Cycle,
                c => (IReadOnlyList<SlotInfoDto>)SlotCalculator
                    .Compute(school!, school!.SlotsPerDay, c.MorningStart, c.AfternoonStart)
                    .Select(sl => new SlotInfoDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak))
                    .ToList());

        var gridEntries = entries.Select(e =>
        {
            var alloc = allocations.GetValueOrDefault(e.AllocationId);
            var teacher = teachers.GetValueOrDefault(e.TeacherId);
            var group = groups.GetValueOrDefault(e.GroupId);
            var classroom = classrooms.GetValueOrDefault(e.ClassroomId);
            return new ScheduleGridEntry(
                e.Id, e.DayOfWeek, e.SlotIndex,
                e.GroupId, group?.DisplayName ?? "?",
                e.TeacherId, teacher?.FullName ?? "?", teacher?.ColorKey ?? "mat",
                e.AllocationId, alloc?.SubjectName ?? "?", alloc?.SubjectKey ?? "tut", alloc?.SubjectShort ?? "?",
                e.ClassroomId, classroom?.Name ?? "?", e.IsManualOverride);
        }).ToList();

        var conflictDtos = dbConflicts.Select(c =>
        {
            string[] suggestions;
            try { suggestions = JsonSerializer.Deserialize<string[]>(c.Suggestions) ?? []; }
            catch { suggestions = []; }
            return new ConflictDto(c.ConflictType, c.Severity, c.Description, suggestions,
                c.GroupId, c.TeacherId, c.DayOfWeek, c.SlotIndex);
        }).ToList();

        return new ScheduleGridDto(
            schedule.Id, schedule.Status, schedule.AcademicYear,
            gridEntries, conflictDtos,
            slots.Select(sl => new SlotInfoDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            slotsByCycle);
    }
}

public sealed class GetMyScheduleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetMyScheduleQuery, MyScheduleDto?>
{
    public async Task<MyScheduleDto?> Handle(GetMyScheduleQuery request, CancellationToken ct)
    {
        var appUser = await db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.UserId, ct);
        if (appUser?.TeacherId is null) return null;
        var teacher = await db.Teachers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == appUser.TeacherId, ct);
        if (teacher is null) return null;

        var schedule = await db.Schedules.AsNoTracking()
            .Where(s => s.SchoolId == user.SchoolId && s.Status == "published")
            .OrderByDescending(s => s.PublishedAt)
            .FirstOrDefaultAsync(ct);
        if (schedule is null) return null;

        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == user.SchoolId, ct);

        var entries = await db.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId == schedule.Id && e.TeacherId == teacher.Id)
            .ToListAsync(ct);

        var allocations = await db.SubjectAllocations.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == user.SchoolId)
            .ToDictionaryAsync(g => g.Id, ct);
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId)
            .ToDictionaryAsync(c => c.Id, ct);

        var slots = school is not null ? SlotCalculator.Compute(school, school.SlotsPerDay) : [];
        var lecSlots = slots.Where(s => !s.IsBreak).ToList();

        var myEntries = entries.Select(e =>
        {
            var alloc = allocations.GetValueOrDefault(e.AllocationId);
            var group = groups.GetValueOrDefault(e.GroupId);
            var classroom = classrooms.GetValueOrDefault(e.ClassroomId);
            var slotTime = lecSlots.Count > e.SlotIndex ? lecSlots[e.SlotIndex].StartTime : "?";
            return new MyScheduleEntry(
                e.DayOfWeek, e.SlotIndex, slotTime,
                alloc?.SubjectName ?? "?", alloc?.SubjectKey ?? "tut", alloc?.SubjectShort ?? "?",
                group?.DisplayName ?? "?", classroom?.Name ?? "?");
        }).ToList();

        return new MyScheduleDto(
            teacher.FullName, school?.Name ?? "?", schedule.AcademicYear,
            myEntries,
            slots.Select(sl => new SlotInfoDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList());
    }
}

public sealed class PublishScheduleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<PublishScheduleCommand, string>
{
    public async Task<string> Handle(PublishScheduleCommand request, CancellationToken ct)
    {
        var schedule = await db.Schedules
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.SchoolId == user.SchoolId, ct);
        if (schedule is null) throw new NotFoundException("Schedule not found");
        if (schedule.Status == "published")
            throw new InvalidOperationException("Este horario ya está publicado.");

        var previous = await db.Schedules
            .Where(s => s.SchoolId == user.SchoolId && s.Status == "published")
            .ToListAsync(ct);
        foreach (var prev in previous)
            prev.Status = "archived";

        schedule.Status = "published";
        schedule.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return "Horario publicado. El anterior ha sido archivado.";
    }
}

public sealed class UpdateScheduleEntryHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateScheduleEntryCommand>
{
    public async Task Handle(UpdateScheduleEntryCommand request, CancellationToken ct)
    {
        var schedule = await db.Schedules
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.SchoolId == user.SchoolId, ct);
        if (schedule is null) throw new NotFoundException("Schedule not found");
        if (schedule.Status == "published")
            throw new InvalidOperationException("No se puede editar un horario publicado. Crea una nueva versión.");

        var entry = await db.ScheduleEntries
            .FirstOrDefaultAsync(e => e.Id == request.EntryId && e.ScheduleId == request.ScheduleId, ct);
        if (entry is null) throw new NotFoundException("Entry not found");

        entry.TeacherId = request.Request.TeacherId;
        entry.ClassroomId = request.Request.ClassroomId;
        entry.IsManualOverride = true;
        await db.SaveChangesAsync(ct);
    }
}
