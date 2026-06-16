using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Teachers;

namespace HorariosEscolares.Application.Features.Teachers;

public record CreateTeacherCommand(
    string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, SubjectHourInput[]? SubjectHours, string ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;

public sealed class CreateTeacherHandler(IAppDbContext db, ITeacherRepository repository, ICurrentUser user)
    : IRequestHandler<CreateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(CreateTeacherCommand request, CancellationToken ct)
    {
        var teacher = new Teacher
        {
            SchoolId = user.SchoolId, FullName = request.FullName, Email = request.Email,
            TeacherType = request.TeacherType, MaxWeeklyHours = request.MaxWeeklyHours,
            ColorKey = request.ColorKey,
        };

        if (request.SubjectHours is not null)
        {
            foreach (var sh in request.SubjectHours)
            {
                teacher.SubjectHours.Add(new TeacherSubjectHour
                {
                    TeacherId = teacher.Id,
                    SubjectKey = sh.SubjectKey.ToLowerInvariant().Trim(),
                    WeeklyHours = sh.WeeklyHours,
                });
            }
        }

        if (request.StageAssignments is { Length: > 0 })
        {
            foreach (var sa in request.StageAssignments)
            {
                teacher.StageAssignments.Add(new TeacherStageAssignment
                {
                    TeacherId = teacher.Id,
                    StageId = sa.StageId,
                    Cycle = sa.Cycle,
                });
            }
        }

        await repository.AddAsync(teacher, ct);
        await repository.SaveChangesAsync(ct);

        var stagesMap = new Dictionary<Guid, SchoolStage>();
        if (teacher.StageAssignments.Count > 0)
        {
            var stageIds = teacher.StageAssignments.Select(sa => sa.StageId).Distinct();
            stagesMap = await db.SchoolStages.AsNoTracking()
                .Where(s => stageIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);
        }

        return TeacherMapping.ToDto(teacher, 0, stagesMap);
    }
}
