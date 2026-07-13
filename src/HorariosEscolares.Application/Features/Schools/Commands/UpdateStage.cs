using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record UpdateStageCommand(
    Guid StageId, string? Name = null, int? MinLevel = null, int? MaxLevel = null,
    int? SortOrder = null, string? ScheduleType = null, string? MorningStart = null,
    int? SlotMinutes = null, int? BreakAfterSlot = null, int? BreakMinutes = null,
    int? SlotsPerDay = null, int? AfternoonSlots = null, int? DaysPerWeek = null,
    string? AfternoonStart = null, IReadOnlyList<int>? WorkingDays = null) : IRequest<SchoolStageDto>;

public sealed class UpdateStageHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateStageCommand, SchoolStageDto>
{
    public async Task<SchoolStageDto> Handle(UpdateStageCommand request, CancellationToken ct)
    {
        var stage = await db.SchoolStages
            .FirstOrDefaultAsync(st => st.Id == request.StageId && st.SchoolId == user.SchoolId, ct)
            ?? throw new NotFoundException("Stage not found");

        if (request.Name is not null) stage.Name = request.Name;
        if (request.MinLevel.HasValue) stage.MinLevel = request.MinLevel.Value;
        if (request.MaxLevel.HasValue) stage.MaxLevel = request.MaxLevel.Value;
        if (request.SortOrder.HasValue) stage.SortOrder = request.SortOrder.Value;
        if (request.ScheduleType is not null) stage.ScheduleType = request.ScheduleType;
        if (request.MorningStart is not null) stage.MorningStart = TimeOnly.Parse(request.MorningStart);
        if (request.SlotMinutes.HasValue) stage.SlotMinutes = request.SlotMinutes.Value;
        if (request.BreakAfterSlot.HasValue) stage.BreakAfterSlot = request.BreakAfterSlot.Value;
        if (request.BreakMinutes.HasValue) stage.BreakMinutes = request.BreakMinutes.Value;
        if (request.SlotsPerDay.HasValue) stage.SlotsPerDay = request.SlotsPerDay.Value;
        if (request.AfternoonSlots.HasValue) stage.AfternoonSlots = request.AfternoonSlots.Value;
        if (request.DaysPerWeek.HasValue) stage.DaysPerWeek = request.DaysPerWeek.Value;
        stage.AfternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : stage.AfternoonStart;

        if (request.WorkingDays is not null)
        {
            SchoolValidationHelper.ValidateWorkingDays(request.WorkingDays);
            stage.WorkingDays = System.Text.Json.JsonSerializer.Serialize(request.WorkingDays);
        }

        await db.SaveChangesAsync(ct);

        return SchoolMapping.MapStage(stage);
    }
}
