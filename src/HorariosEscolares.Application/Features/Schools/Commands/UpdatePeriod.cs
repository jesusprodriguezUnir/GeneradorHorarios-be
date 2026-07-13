using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;

namespace HorariosEscolares.Application.Features.Schools;

public record UpdatePeriodCommand(
    Guid PeriodId, string? Name, IReadOnlyList<int>? Months,
    string? ScheduleType, int? SlotMinutes, int? SlotsPerDay, int? AfternoonSlots,
    int? SortOrder) : IRequest<SchoolPeriodDto>;

public sealed class UpdatePeriodHandler(
    IAppDbContext db, ISchoolPeriodRepository repository, ICurrentUser user)
    : IRequestHandler<UpdatePeriodCommand, SchoolPeriodDto>
{
    public async Task<SchoolPeriodDto> Handle(UpdatePeriodCommand request, CancellationToken ct)
    {
        var period = await repository.GetByIdAsync(request.PeriodId, ct)
            ?? throw new NotFoundException("Periodo no encontrado.");
        if (period.SchoolId != user.SchoolId)
            throw new NotFoundException("Periodo no encontrado.");

        if (request.Name is not null) period.Name = request.Name;
        if (request.Months is not null)
        {
            SchoolValidationHelper.ValidateMonths(request.Months);
            period.Months = System.Text.Json.JsonSerializer.Serialize(request.Months);
        }
        if (request.ScheduleType is not null) period.ScheduleType = request.ScheduleType;
        if (request.SlotMinutes.HasValue) period.SlotMinutes = request.SlotMinutes.Value;
        if (request.SlotsPerDay.HasValue) period.SlotsPerDay = request.SlotsPerDay.Value;
        if (request.AfternoonSlots.HasValue) period.AfternoonSlots = request.AfternoonSlots.Value;
        if (request.SortOrder.HasValue) period.SortOrder = request.SortOrder.Value;

        if (period.AfternoonSlots >= period.SlotsPerDay)
            throw new InvalidOperationException("Los slots de tarde deben ser menores que el total de slots por día.");

        await repository.SaveChangesAsync(ct);

        var reloaded = await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .FirstAsync(p => p.Id == request.PeriodId, ct);
        return SchoolMapping.MapPeriod(reloaded);
    }
}
