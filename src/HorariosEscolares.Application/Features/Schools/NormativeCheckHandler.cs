using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public sealed class NormativeCheckHandler(IAppDbContext db, INormativeValidator validator, ICurrentUser user)
    : IRequestHandler<NormativeCheckQuery, NormativeCheckDto>
{
    public async Task<NormativeCheckDto> Handle(NormativeCheckQuery request, CancellationToken ct)
    {
        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == user.SchoolId, ct);
        if (school is null)
            throw new NotFoundException("School not found");

        var primaryStage = await db.SchoolStages.AsNoTracking()
            .Where(st => st.SchoolId == user.SchoolId)
            .OrderBy(st => st.SortOrder)
            .FirstOrDefaultAsync(ct);

        var cycleSchedules = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .Where(c => c.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var normativeAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId).ToListAsync(ct);
        var normativeData = normativeAssignments
            .Where(a => normativeAllocs.ContainsKey(a.AllocationId))
            .Select(a => (a, normativeAllocs[a.AllocationId]))
            .ToList();

        var workingDays = SlotCalculator.ParseWorkingDays(school.WorkingDays);
        var cycles = cycleSchedules.Select(cs =>
        {
            var slots = SlotCalculator.Compute(cs, school);
            return new CycleGrid(cs.Cycle,
                slots.Select(s => new SlotConfig(s.Index, s.IsBreak, s.StartMinute, s.EndMinute)).ToList());
        }).ToList();

        if (cycles.Count == 0)
        {
            var fallbackSlots = SlotCalculator.Compute(school, school.SlotsPerDay);
            cycles.Add(new CycleGrid(1,
                fallbackSlots.Select(s => new SlotConfig(s.Index, s.IsBreak, s.StartMinute, s.EndMinute)).ToList()));
        }

        var schoolConfig = new SchoolConfig(
            school.SlotsPerDay, school.DaysPerWeek, workingDays, cycles, []);
        var data = new NormativeValidationData
        {
            SchoolConfig = schoolConfig,
            Stage = primaryStage?.StageType ?? "primaria",
            MinCourseLevel = school.MinCourseLevel,
            MaxCourseLevel = school.MaxCourseLevel,
            BreakMinutes = school.BreakMinutes,
            SlotMinutes = school.SlotMinutes,
            Assignments = normativeData.Select(n => new NormativeAssignmentData(
                n.a.GroupId, n.Item2.SubjectKey, n.Item2.SubjectName,
                n.a.WeeklyHours, n.Item2.WeeklyHoursMin,
                n.Item2.WeeklyHoursMax, n.Item2.WeeklyHoursDefault)).ToList(),
        };
        var issues = await validator.ValidateAsync(data, ct);
        var compliant = issues.All(i => i.Severity != ConflictSeverity.Error);

        return new NormativeCheckDto(
            compliant,
            issues.Count(i => i.Severity == ConflictSeverity.Error),
            issues.Count(i => i.Severity == ConflictSeverity.Warning),
            issues.Select(i => new NormativeIssueDto(
                i.Severity.ToString().ToLower(),
                i.Description,
                i.Suggestions,
                i.GroupId)).ToList());
    }
}
