using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Application.Features.Assignments;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Teachers;

public record UpdateTeacherAssignmentsCommand(Guid TeacherId, TeacherAssignmentInput[] Assignments) : IRequest<TeacherDto>;

public sealed class UpdateTeacherAssignmentsHandler(IAppDbContext db, ICurrentUser user, ICycleResolver cycleResolver)
    : IRequestHandler<UpdateTeacherAssignmentsCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherAssignmentsCommand request, CancellationToken ct)
    {
        var teacherExists = await db.Teachers.AnyAsync(
            t => t.Id == request.TeacherId && t.SchoolId == user.SchoolId, ct);
        if (!teacherExists) throw new NotFoundException($"Teacher {request.TeacherId} not found");

        if (request.Assignments.Length > 0)
        {
            var validGroupIds = (await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId)
                .Select(g => g.Id)
                .ToListAsync(ct)).ToHashSet();

            var validAllocationIds = (await db.SubjectAllocations.AsNoTracking()
                .Select(a => a.Id)
                .ToListAsync(ct)).ToHashSet();

            var badGroup = request.Assignments.FirstOrDefault(a => !validGroupIds.Contains(a.GroupId));
            if (badGroup is not null)
                throw new InvalidOperationException($"Grupo no válido: {badGroup.GroupId}");

            var badAlloc = request.Assignments.FirstOrDefault(a => !validAllocationIds.Contains(a.AllocationId));
            if (badAlloc is not null)
                throw new InvalidOperationException($"Asignatura no válida: {badAlloc.AllocationId}");
        }

        var existing = await db.Assignments
            .Where(a => a.TeacherId == request.TeacherId && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var newSet = request.Assignments
            .GroupBy(a => (a.GroupId, a.AllocationId))
            .ToDictionary(
                g => g.Key,
                g => new TeacherAssignmentInput(
                    g.Key.AllocationId,
                    g.Key.GroupId,
                    g.Sum(a => a.WeeklyHours)));

        var toRemove = existing
            .Where(e => !newSet.ContainsKey((e.GroupId, e.AllocationId)))
            .ToList();
        db.Assignments.RemoveRange(toRemove);

        foreach (var (key, input) in newSet)
        {
            var match = existing.FirstOrDefault(
                e => e.GroupId == key.GroupId && e.AllocationId == key.AllocationId);
            if (match is not null)
            {
                match.WeeklyHours = input.WeeklyHours;
            }
            else
            {
                db.Assignments.Add(new Assignment
                {
                    SchoolId = user.SchoolId,
                    TeacherId = request.TeacherId,
                    GroupId = key.GroupId,
                    AllocationId = key.AllocationId,
                    WeeklyHours = input.WeeklyHours,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var distinctGroupIds = newSet.Keys.Select(k => k.GroupId).Distinct();
        foreach (var groupId in distinctGroupIds)
        {
            await TeacherStageAssignmentHelper.EnsureForGroupAsync(db, cycleResolver, request.TeacherId, groupId, ct);
        }
        await db.SaveChangesAsync(ct);

        var teacher = await db.Teachers.AsNoTracking()
            .Include(x => x.StageAssignments)
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.TeacherId, ct);

        var finalAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.TeacherId && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var assignedHours = finalAssignments.Sum(a => a.WeeklyHours);

        var groupIds = finalAssignments.Select(a => a.GroupId).ToHashSet();
        var groupNames = await db.CourseGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);

        var allocationIds = finalAssignments.Select(a => a.AllocationId).ToHashSet();
        var allocationNames = await db.SubjectAllocations.AsNoTracking()
            .Where(s => allocationIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SubjectName, ct);

        var assignmentDtos = finalAssignments.Select(a => new TeacherAssignmentDto(
            a.Id,
            a.GroupId,
            groupNames.GetValueOrDefault(a.GroupId, "?"),
            a.AllocationId,
            allocationNames.GetValueOrDefault(a.AllocationId, "?"),
            a.WeeklyHours
        )).ToArray();

        var stageIds = teacher!.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return TeacherMapping.ToDto(teacher, assignedHours, stagesMap, assignmentDtos);
    }
}
