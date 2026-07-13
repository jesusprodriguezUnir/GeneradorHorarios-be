using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Teachers;

public record UpdateTeacherCommand(
    Guid Id, string? FullName, string? Email, string? TeacherType,
    int? MaxWeeklyHours, SubjectHourInput[]? SubjectHours, string? ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;

public sealed class UpdateTeacherHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherCommand request, CancellationToken ct)
    {
        var teacher = await db.Teachers
            .Include(x => x.StageAssignments)
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (teacher is null) throw new NotFoundException($"Teacher {request.Id} not found");

        if (request.FullName is not null) teacher.FullName = request.FullName;
        if (request.Email is not null) teacher.Email = request.Email;
        if (request.TeacherType is not null) teacher.TeacherType = request.TeacherType;
        if (request.MaxWeeklyHours.HasValue) teacher.MaxWeeklyHours = request.MaxWeeklyHours.Value;
        if (request.ColorKey is not null) teacher.ColorKey = request.ColorKey;

        if (request.SubjectHours is not null)
        {
            var newHours = request.SubjectHours.ToDictionary(
                sh => sh.SubjectKey.ToLowerInvariant().Trim(),
                sh => sh.WeeklyHours
            );

            var toRemove = teacher.SubjectHours
                .Where(sh => !newHours.ContainsKey(sh.SubjectKey))
                .ToList();
            foreach (var item in toRemove)
            {
                teacher.SubjectHours.Remove(item);
                db.TeacherSubjectHours.Remove(item);
            }

            foreach (var kvp in newHours)
            {
                var existing = teacher.SubjectHours.FirstOrDefault(sh => sh.SubjectKey == kvp.Key);
                if (existing is not null)
                {
                    existing.WeeklyHours = kvp.Value;
                }
                else
                {
                    var newHour = new TeacherSubjectHour
                    {
                        TeacherId = teacher.Id,
                        SubjectKey = kvp.Key,
                        WeeklyHours = kvp.Value,
                    };
                    teacher.SubjectHours.Add(newHour);
                    db.TeacherSubjectHours.Add(newHour);
                }
            }
        }

        if (request.StageAssignments is not null)
        {
            var newStageIds = request.StageAssignments.Select(sa => sa.StageId).ToHashSet();

            var toRemove = teacher.StageAssignments
                .Where(sa => !newStageIds.Contains(sa.StageId))
                .ToList();
            foreach (var item in toRemove)
            {
                teacher.StageAssignments.Remove(item);
                db.TeacherStageAssignments.Remove(item);
            }

            foreach (var ns in request.StageAssignments)
            {
                var exists = teacher.StageAssignments.Any(sa => sa.StageId == ns.StageId && sa.Cycle == ns.Cycle);
                if (!exists)
                {
                    var newStage = new TeacherStageAssignment
                    {
                        TeacherId = teacher.Id,
                        StageId = ns.StageId,
                        Cycle = ns.Cycle,
                    };
                    teacher.StageAssignments.Add(newStage);
                    db.TeacherStageAssignments.Add(newStage);
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var hours = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id).SumAsync(a => a.WeeklyHours, ct);

        var stageIds = teacher.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return TeacherMapping.ToDto(teacher, hours, stagesMap);
    }
}
