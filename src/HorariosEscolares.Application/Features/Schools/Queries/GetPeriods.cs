using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record GetPeriodsQuery(Guid? StageId = null) : IRequest<List<SchoolPeriodDto>>;

public sealed class GetPeriodsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodsQuery, List<SchoolPeriodDto>>
{
    public async Task<List<SchoolPeriodDto>> Handle(GetPeriodsQuery request, CancellationToken ct)
    {
        var query = db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .Where(p => p.SchoolId == user.SchoolId);

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

                query = query.Where(p => assignedStageIds.Contains(p.StageId));
            }
            else
            {
                return [];
            }
        }

        if (request.StageId.HasValue)
            query = query.Where(p => p.StageId == request.StageId.Value);

        var periods = await query
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return periods.Select(SchoolMapping.MapPeriod).ToList();
    }
}
