using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record GetStagesQuery : IRequest<List<SchoolStageDto>>;

public sealed class GetStagesHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetStagesQuery, List<SchoolStageDto>>
{
    public async Task<List<SchoolStageDto>> Handle(GetStagesQuery request, CancellationToken ct)
    {
        var query = db.SchoolStages.AsNoTracking()
            .Where(s => s.SchoolId == user.SchoolId);

        if (user.IsTeacher)
        {
            var teacherId = await db.Teachers.AsNoTracking()
                .Where(t => t.UserId == user.UserId)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (teacherId != Guid.Empty)
            {
                var assignedStageIds = await db.TeacherStageAssignments.AsNoTracking()
                    .Where(tsa => tsa.TeacherId == teacherId)
                    .Select(tsa => tsa.StageId)
                    .ToListAsync(ct);

                query = query.Where(s => assignedStageIds.Contains(s.Id));
            }
            else
            {
                return [];
            }
        }

        var stages = await query
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        return stages.Select(SchoolMapping.MapStage).ToList();
    }
}
