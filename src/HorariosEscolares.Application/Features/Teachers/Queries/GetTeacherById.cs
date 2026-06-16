using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Teachers;

public record GetTeacherByIdQuery(Guid Id) : IRequest<TeacherDto?>;

public sealed class GetTeacherByIdHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetTeacherByIdQuery, TeacherDto?>
{
    public async Task<TeacherDto?> Handle(GetTeacherByIdQuery request, CancellationToken ct)
    {
        var teacher = await db.Teachers.AsNoTracking()
            .Include(x => x.StageAssignments)
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (teacher is null) return null;

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var assignedHours = assignments.Sum(a => a.WeeklyHours);

        var stageIds = teacher.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        TeacherAssignmentDto[] assignmentDtos = [];
        if (assignments.Count > 0)
        {
            var groupIds = assignments.Select(a => a.GroupId).ToHashSet();
            var groupNames = await db.CourseGroups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);

            var allocationIds = assignments.Select(a => a.AllocationId).ToHashSet();
            var allocationNames = await db.SubjectAllocations.AsNoTracking()
                .Where(s => allocationIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SubjectName, ct);

            assignmentDtos = assignments.Select(a => new TeacherAssignmentDto(
                a.Id,
                a.GroupId,
                groupNames.GetValueOrDefault(a.GroupId, "?"),
                a.AllocationId,
                allocationNames.GetValueOrDefault(a.AllocationId, "?"),
                a.WeeklyHours
            )).ToArray();
        }

        return TeacherMapping.ToDto(teacher, assignedHours, stagesMap, assignmentDtos);
    }
}
