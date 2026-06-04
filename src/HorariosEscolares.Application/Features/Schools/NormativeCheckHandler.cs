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

        var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var normativeAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId).ToListAsync(ct);
        var normativeData = normativeAssignments
            .Where(a => normativeAllocs.ContainsKey(a.AllocationId))
            .Select(a => (a, normativeAllocs[a.AllocationId]))
            .ToList();

        var schoolConfig = new SchoolConfig(
            school.SlotsPerDay, school.DaysPerWeek,
            SlotCalculator.ParseWorkingDays(school.WorkingDays).ToList(),
            SlotCalculator.Compute(school, school.SlotsPerDay)
                .Select(s => new SlotConfig(s.Index, s.IsBreak)).ToList(),
            []);
        var data = new NormativeValidationData(
            schoolConfig, school.Stage, school.MinCourseLevel,
            school.MaxCourseLevel, school.BreakMinutes, school.SlotMinutes,
            normativeData.Select(n => new NormativeAssignmentData(
                n.a.GroupId, n.Item2.SubjectKey, n.Item2.SubjectName,
                n.a.WeeklyHours, n.Item2.WeeklyHoursMin,
                n.Item2.WeeklyHoursMax, n.Item2.WeeklyHoursDefault)).ToList());
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
