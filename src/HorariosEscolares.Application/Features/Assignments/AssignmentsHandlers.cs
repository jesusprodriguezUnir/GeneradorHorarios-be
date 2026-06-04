using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Assignments;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Assignments;

public record AssignmentDto(
    Guid Id, Guid TeacherId, string TeacherName,
    Guid GroupId, string GroupDisplay,
    Guid AllocationId, string SubjectName,
    int WeeklyHours);

public record AssignmentSummaryDto(
    string SubjectName, string SubjectKey,
    int RequiredHours, int AssignedHours,
    double CompletionPct,
    IReadOnlyList<AssignmentDto> Assignments);

public record GetAllAssignmentsQuery : IRequest<List<AssignmentSummaryDto>>;
public record CreateAssignmentCommand(Guid TeacherId, Guid GroupId, Guid AllocationId, int WeeklyHours) : IRequest<AssignmentDto>;
public record DeleteAssignmentCommand(Guid Id) : IRequest;

public sealed class GetAllAssignmentsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllAssignmentsQuery, List<AssignmentSummaryDto>>
{
    public async Task<List<AssignmentSummaryDto>> Handle(GetAllAssignmentsQuery request, CancellationToken ct)
    {
        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .ToListAsync(ct);
        var teacherNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == user.SchoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);
        var groupNames = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == user.SchoolId)
            .ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);
        var allocations = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var courseGroups = await db.CourseGroups.AsNoTracking()
            .Include(x => x.SubjectHoursList)
            .Where(g => g.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var summary = assignments
            .GroupBy(a => a.AllocationId)
            .Select(grp =>
            {
                var alloc = allocations.GetValueOrDefault(grp.Key);
                var required = 0;
                if (alloc != null)
                {
                    foreach (var cg in courseGroups)
                    {
                        var groupHours = cg.SubjectHoursList?.ToDictionary(x => x.SubjectKey, x => x.Hours) ?? new();
                        if (groupHours.TryGetValue(alloc.SubjectKey, out var h))
                            required += h;
                        else
                            required += alloc.WeeklyHoursDefault;
                    }
                }
                var assigned = grp.Sum(a => a.WeeklyHours);
                return new AssignmentSummaryDto(
                    alloc?.SubjectName ?? "?", alloc?.SubjectKey ?? "tut",
                    required, assigned,
                    required == 0 ? 100 : Math.Min(100.0, assigned * 100.0 / required),
                    grp.Select(a => new AssignmentDto(
                        a.Id, a.TeacherId, teacherNames.GetValueOrDefault(a.TeacherId, "?"),
                        a.GroupId, groupNames.GetValueOrDefault(a.GroupId, "?"),
                        a.AllocationId, alloc?.SubjectName ?? "?", a.WeeklyHours
                    )).ToList()
                );
            })
            .OrderBy(s => s.SubjectName)
            .ToList();

        return summary;
    }
}

public sealed class CreateAssignmentHandler(IAppDbContext db, IAssignmentRepository repository, ICurrentUser user)
    : IRequestHandler<CreateAssignmentCommand, AssignmentDto>
{
    public async Task<AssignmentDto> Handle(CreateAssignmentCommand request, CancellationToken ct)
    {
        var teacherOk = await db.Teachers.AnyAsync(t => t.Id == request.TeacherId && t.SchoolId == user.SchoolId, ct);
        var groupOk = await db.CourseGroups.AnyAsync(g => g.Id == request.GroupId && g.SchoolId == user.SchoolId, ct);
        if (!teacherOk || !groupOk)
            throw new InvalidOperationException("Profesor o grupo no válido.");

        var a = new Assignment
        {
            SchoolId = user.SchoolId, TeacherId = request.TeacherId,
            GroupId = request.GroupId, AllocationId = request.AllocationId,
            WeeklyHours = request.WeeklyHours,
        };
        await repository.AddAsync(a, ct);
        await repository.SaveChangesAsync(ct);

        var teacherName = await db.Teachers.AsNoTracking()
            .Where(t => t.Id == request.TeacherId).Select(t => t.FullName).FirstOrDefaultAsync(ct) ?? "?";
        var groupDisplay = await db.CourseGroups.AsNoTracking()
            .Where(g => g.Id == request.GroupId).Select(g => g.DisplayName).FirstOrDefaultAsync(ct) ?? "?";
        var subjectName = await db.SubjectAllocations.AsNoTracking()
            .Where(s => s.Id == request.AllocationId).Select(s => s.SubjectName).FirstOrDefaultAsync(ct) ?? "?";
        return new AssignmentDto(a.Id, a.TeacherId, teacherName, a.GroupId, groupDisplay,
            a.AllocationId, subjectName, a.WeeklyHours);
    }
}

public sealed class DeleteAssignmentHandler(IAssignmentRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteAssignmentCommand>
{
    public async Task Handle(DeleteAssignmentCommand request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.Id, ct);
        if (a is null || a.SchoolId != user.SchoolId) throw new NotFoundException($"Assignment {request.Id} not found");
        await repository.DeleteAsync(a, ct);
        await repository.SaveChangesAsync(ct);
    }
}

// ── Period hours overrides ──────────────────────────────────────────────────────

public record PeriodAssignmentHoursDto(Guid AssignmentId, int WeeklyHours);

public record GetPeriodAssignmentHoursQuery(Guid PeriodId) : IRequest<List<PeriodAssignmentHoursDto>>;
public record SetPeriodAssignmentHoursCommand(Guid PeriodId, IReadOnlyList<PeriodAssignmentHoursDto> Hours) : IRequest;

public sealed class GetPeriodAssignmentHoursHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodAssignmentHoursQuery, List<PeriodAssignmentHoursDto>>
{
    public async Task<List<PeriodAssignmentHoursDto>> Handle(GetPeriodAssignmentHoursQuery request, CancellationToken ct)
    {
        var periodOk = await db.SchoolPeriods.AnyAsync(p => p.Id == request.PeriodId && p.SchoolId == user.SchoolId, ct);
        if (!periodOk) throw new NotFoundException("Periodo no encontrado.");

        var overrides = await db.PeriodAssignmentHours.AsNoTracking()
            .Where(h => h.PeriodId == request.PeriodId)
            .ToDictionaryAsync(h => h.AssignmentId, h => h.WeeklyHours, ct);

        var baseAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        return baseAssignments.Select(a => new PeriodAssignmentHoursDto(
            a.Id, overrides.GetValueOrDefault(a.Id, a.WeeklyHours)))
            .OrderBy(x => x.AssignmentId)
            .ToList();
    }
}

public sealed class SetPeriodAssignmentHoursHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<SetPeriodAssignmentHoursCommand>
{
    public async Task Handle(SetPeriodAssignmentHoursCommand request, CancellationToken ct)
    {
        var periodOk = await db.SchoolPeriods.AnyAsync(p => p.Id == request.PeriodId && p.SchoolId == user.SchoolId, ct);
        if (!periodOk) throw new NotFoundException("Periodo no encontrado.");

        var existing = await db.PeriodAssignmentHours
            .Where(h => h.PeriodId == request.PeriodId)
            .ToListAsync(ct);
        db.PeriodAssignmentHours.RemoveRange(existing);

        var baseAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .ToDictionaryAsync(a => a.Id, a => a.WeeklyHours, ct);

        foreach (var dto in request.Hours)
        {
            if (!baseAssignments.ContainsKey(dto.AssignmentId)) continue;
            if (dto.WeeklyHours == baseAssignments[dto.AssignmentId]) continue;

            db.PeriodAssignmentHours.Add(new PeriodAssignmentHours
            {
                PeriodId = request.PeriodId,
                AssignmentId = dto.AssignmentId,
                WeeklyHours = dto.WeeklyHours,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
