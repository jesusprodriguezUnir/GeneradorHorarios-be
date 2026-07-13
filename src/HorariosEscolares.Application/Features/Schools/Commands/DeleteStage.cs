using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record DeleteStageCommand(Guid StageId) : IRequest;

public sealed class DeleteStageHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteStageCommand>
{
    public async Task Handle(DeleteStageCommand request, CancellationToken ct)
    {
        var stage = await db.SchoolStages
            .FirstOrDefaultAsync(st => st.Id == request.StageId && st.SchoolId == user.SchoolId, ct)
            ?? throw new NotFoundException("Stage not found");

        var hasGroups = await db.CourseGroups.AnyAsync(cg => cg.StageId == stage.Id, ct);
        var hasTeacherAssignments = await db.TeacherStageAssignments.AnyAsync(tsa => tsa.StageId == stage.Id, ct);
        var hasSchedules = await db.Schedules.AnyAsync(s => s.StageId == stage.Id, ct);

        if (hasGroups || hasTeacherAssignments || hasSchedules)
        {
            var reasons = new List<string>();
            if (hasGroups) reasons.Add("grupos de alumnos (CourseGroups)");
            if (hasTeacherAssignments) reasons.Add("profesores asignados (TeacherStageAssignments)");
            if (hasSchedules) reasons.Add("horarios generados (Schedules)");

            throw new InvalidOperationException($"No se puede eliminar la etapa '{stage.StageType}' porque tiene dependencias activas: {string.Join(", ", reasons)}.");
        }

        var templates = await db.CurriculumTemplates.Where(t => t.StageId == stage.Id).ToListAsync(ct);
        foreach (var template in templates)
        {
            template.StageId = null;
        }

        var stageCycles = await db.CycleSchedules.Include(c => c.Breaks)
            .Where(c => c.StageId == stage.Id).ToListAsync(ct);
        foreach (var cycle in stageCycles)
        {
            db.CycleBreaks.RemoveRange(cycle.Breaks);
        }
        db.CycleSchedules.RemoveRange(stageCycles);

        var periods = await db.SchoolPeriods.Where(p => p.StageId == stage.Id).ToListAsync(ct);
        db.SchoolPeriods.RemoveRange(periods);

        db.SchoolStages.Remove(stage);
        await db.SaveChangesAsync(ct);
    }
}
