using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Assignments;

internal static class TeacherStageAssignmentHelper
{
    public static async Task EnsureForGroupAsync(
        IAppDbContext db,
        ICycleResolver cycleResolver,
        Guid teacherId,
        Guid groupId,
        CancellationToken ct)
    {
        var group = await db.CourseGroups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return;

        var stage = await db.SchoolStages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == group.StageId, ct);
        if (stage is null) return;

        var cycle = cycleResolver.ResolveCycle(stage.StageType, group.CourseLevel);

        var exists = await db.TeacherStageAssignments.AnyAsync(
            tsa => tsa.TeacherId == teacherId
                && tsa.StageId == group.StageId
                && tsa.Cycle == cycle, ct);

        if (!exists)
        {
            db.TeacherStageAssignments.Add(new TeacherStageAssignment
            {
                TeacherId = teacherId,
                StageId = group.StageId,
                Cycle = cycle,
            });
        }
    }
}
