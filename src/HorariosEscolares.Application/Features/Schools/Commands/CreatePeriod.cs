using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record CreatePeriodCommand(
    Guid StageId, string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    int SortOrder) : IRequest<SchoolPeriodDto>;

public sealed class CreatePeriodHandler(
    IAppDbContext db, ISchoolPeriodRepository repository, ICurrentUser user)
    : IRequestHandler<CreatePeriodCommand, SchoolPeriodDto>
{
    public async Task<SchoolPeriodDto> Handle(CreatePeriodCommand request, CancellationToken ct)
    {
        SchoolValidationHelper.ValidateMonths(request.Months);
        if (request.AfternoonSlots >= request.SlotsPerDay)
            throw new InvalidOperationException("Los slots de tarde deben ser menores que el total de slots por día.");

        var stage = await db.SchoolStages.AsNoTracking()
            .FirstOrDefaultAsync(st => st.Id == request.StageId && st.SchoolId == user.SchoolId, ct)
            ?? throw new InvalidOperationException("La etapa indicada no existe en este centro.");

        if (await db.SchoolPeriods.AnyAsync(p => p.StageId == request.StageId && p.Key == request.Key, ct))
            throw new InvalidOperationException($"Ya existe un periodo con la clave '{request.Key}' en esta etapa.");

        var period = new SchoolPeriod
        {
            SchoolId = user.SchoolId,
            StageId = request.StageId,
            Key = request.Key,
            Name = request.Name,
            Months = System.Text.Json.JsonSerializer.Serialize(request.Months),
            ScheduleType = request.ScheduleType,
            SlotMinutes = request.SlotMinutes,
            SlotsPerDay = request.SlotsPerDay,
            AfternoonSlots = request.AfternoonSlots,
            IsDefault = !await db.SchoolPeriods.AnyAsync(p => p.StageId == request.StageId, ct),
            SortOrder = request.SortOrder,
        };

        for (int c = 1; c <= 3; c++)
        {
            var morningStart = new TimeOnly(9, 0);
            var isPartida = request.ScheduleType == "partida";
            var cycleEnd = SlotCalculator.ComputeEndTime(
                totalSlots: request.SlotsPerDay,
                slotMinutes: request.SlotMinutes,
                breaks: Array.Empty<(int, int)>(),
                afternoonSlots: request.AfternoonSlots,
                morningStart: morningStart,
                afternoonStart: null,
                isPartida: false);
            period.Cycles.Add(new CycleSchedule
            {
                SchoolId = user.SchoolId,
                StageId = request.StageId,
                PeriodId = period.Id,
                Cycle = c,
                MorningStart = morningStart,
                EndTime = cycleEnd,
                AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            });
        }

        await repository.AddAsync(period, ct);
        await repository.SaveChangesAsync(ct);

        return SchoolMapping.MapPeriod(period);
    }
}
