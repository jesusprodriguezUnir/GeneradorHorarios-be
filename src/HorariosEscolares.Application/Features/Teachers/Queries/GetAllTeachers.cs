using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Teachers;

public record GetAllTeachersQuery : IRequest<List<TeacherDto>>;

public sealed class GetAllTeachersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllTeachersQuery, List<TeacherDto>>
{
    public async Task<List<TeacherDto>> Handle(GetAllTeachersQuery request, CancellationToken ct)
    {
        var teachers = await db.Teachers.AsNoTracking()
            .Include(t => t.StageAssignments)
            .Include(t => t.SubjectHours)
            .Where(t => t.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var hoursMap = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .GroupBy(a => a.TeacherId)
            .Select(g => new { TeacherId = g.Key, Hours = g.Sum(a => a.WeeklyHours) })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Hours, ct);

        var stageIds = teachers.SelectMany(t => t.StageAssignments).Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return teachers.Select(t => TeacherMapping.ToDto(t, hoursMap.GetValueOrDefault(t.Id), stagesMap)).ToList();
    }
}
