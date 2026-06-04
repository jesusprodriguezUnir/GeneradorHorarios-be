using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
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

public sealed class CreateAssignmentHandler(IAppDbContext db, ICurrentUser user)
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
        db.Assignments.Add(a);
        await db.SaveChangesAsync(ct);

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

public sealed class DeleteAssignmentHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteAssignmentCommand>
{
    public async Task Handle(DeleteAssignmentCommand request, CancellationToken ct)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (a is null) throw new NotFoundException($"Assignment {request.Id} not found");
        db.Assignments.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
