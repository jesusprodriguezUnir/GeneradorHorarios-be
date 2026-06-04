using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record SlotDto(int Index, string StartTime, string EndTime, bool IsBreak);
public record CycleBreakDto(int AfterSlot, int Minutes);
public record CycleScheduleDto(int Cycle, string MorningStart, string EndTime, string? AfternoonStart, IReadOnlyList<SlotDto> ComputedSlots, IReadOnlyList<CycleBreakDto> Breaks);
public record SchoolDto(
    Guid Id, string Name, string Slug,
    string? CenterCode, string? Locality, string Community,
    string Stage, int MinCourseLevel, int MaxCourseLevel, string AcademicYear,
    string ScheduleType, string MorningStart, string? AfternoonStart,
    int SlotMinutes, int BreakAfterSlot, int BreakMinutes,
    int SlotsPerDay, int AfternoonSlots, int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SlotDto> ComputedSlots,
    IReadOnlyList<CycleScheduleDto> Cycles);
public record NormativeCheckDto(
    bool IsCompliant, int ErrorCount, int WarningCount,
    IReadOnlyList<NormativeIssueDto> Issues);
public record NormativeIssueDto(string Severity, string Description, IReadOnlyList<string> Suggestions, Guid? GroupId);

public record GetSchoolQuery : IRequest<SchoolDto?>;
public record UpdateSchoolCommand(
    string? Name, string? CenterCode, string? Locality, string? Community,
    string? Stage, int? MinCourseLevel, int? MaxCourseLevel, string? AcademicYear,
    string? ScheduleType, string? MorningStart, int? SlotMinutes,
    int? BreakAfterSlot, int? BreakMinutes, int? SlotsPerDay, int? AfternoonSlots,
    string? AfternoonStart, IReadOnlyList<int>? WorkingDays) : IRequest<SchoolDto>;
public record NormativeCheckQuery : IRequest<NormativeCheckDto>;

public record GetCycleScheduleQuery(int Cycle) : IRequest<CycleScheduleDto?>;
public record UpdateCycleScheduleCommand(int Cycle, string MorningStart, string EndTime, string? AfternoonStart, IReadOnlyList<CycleBreakDto>? Breaks = null) : IRequest<CycleScheduleDto>;

public sealed class GetSchoolHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetSchoolQuery, SchoolDto?>
{
    public async Task<SchoolDto?> Handle(GetSchoolQuery request, CancellationToken ct)
    {
        var s = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == user.SchoolId, ct);
        if (s is null) return null;
        var defaultPeriod = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == user.SchoolId && p.IsDefault, ct);
        var cycles = defaultPeriod is not null
            ? await db.CycleSchedules.AsNoTracking()
                .Where(c => c.PeriodId == defaultPeriod.Id).ToListAsync(ct)
            : [];
        return Map(s, cycles);
    }

    internal static SchoolDto Map(School s, List<CycleSchedule> cycles)
    {
        var slots = SlotCalculator.Compute(s, s.SlotsPerDay);
        var workingDays = SlotCalculator.ParseWorkingDays(s.WorkingDays);
        var cycleDtos = cycles.OrderBy(c => c.Cycle).Select(c =>
        {
            var cycleSlots = SlotCalculator.Compute(c, s);
            return new CycleScheduleDto(
                c.Cycle,
                c.MorningStart.ToString("HH:mm"),
                c.EndTime.ToString("HH:mm"),
                c.AfternoonStart?.ToString("HH:mm"),
                cycleSlots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
                c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
        }).ToList();
        return new SchoolDto(
            s.Id, s.Name, s.Slug,
            s.CenterCode, s.Locality, s.Community,
            s.Stage, s.MinCourseLevel, s.MaxCourseLevel, s.AcademicYear,
            s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"),
            s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek,
            workingDays,
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            cycleDtos);
    }
}

public sealed class UpdateSchoolHandler(IAppDbContext db, ISchoolRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateSchoolCommand, SchoolDto>
{
    public async Task<SchoolDto> Handle(UpdateSchoolCommand request, CancellationToken ct)
    {
        var s = await repository.GetByIdAsync(user.SchoolId, ct);
        if (s is null) throw new NotFoundException("School not found");

        if (request.Name is not null) s.Name = request.Name;
        if (request.CenterCode is not null) s.CenterCode = request.CenterCode;
        if (request.Locality is not null) s.Locality = request.Locality;
        if (request.Community is not null) s.Community = request.Community;
        if (request.Stage is not null) s.Stage = request.Stage;
        if (request.MinCourseLevel.HasValue) s.MinCourseLevel = request.MinCourseLevel.Value;
        if (request.MaxCourseLevel.HasValue) s.MaxCourseLevel = request.MaxCourseLevel.Value;
        if (request.AcademicYear is not null) s.AcademicYear = request.AcademicYear;
        if (request.ScheduleType is not null) s.ScheduleType = request.ScheduleType;
        if (request.MorningStart is not null) s.MorningStart = TimeOnly.Parse(request.MorningStart);
        if (request.SlotMinutes.HasValue) s.SlotMinutes = request.SlotMinutes.Value;
        if (request.BreakAfterSlot.HasValue) s.BreakAfterSlot = request.BreakAfterSlot.Value;
        if (request.BreakMinutes.HasValue) s.BreakMinutes = request.BreakMinutes.Value;
        if (request.SlotsPerDay.HasValue) s.SlotsPerDay = request.SlotsPerDay.Value;
        if (request.AfternoonSlots.HasValue) s.AfternoonSlots = request.AfternoonSlots.Value;
        s.AfternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : s.AfternoonStart;

        if (request.MinCourseLevel.HasValue && request.MaxCourseLevel.HasValue && request.MinCourseLevel > request.MaxCourseLevel)
            throw new InvalidOperationException("El rango de cursos no es válido: el nivel mínimo no puede ser mayor que el máximo.");
        if (request.MinCourseLevel.HasValue && !request.MaxCourseLevel.HasValue && request.MinCourseLevel > s.MaxCourseLevel)
            throw new InvalidOperationException("El rango de cursos no es válido: el nivel mínimo no puede ser mayor que el máximo.");
        if (!request.MinCourseLevel.HasValue && request.MaxCourseLevel.HasValue && s.MinCourseLevel > request.MaxCourseLevel)
            throw new InvalidOperationException("El rango de cursos no es válido: el nivel mínimo no puede ser mayor que el máximo.");

        if (request.AfternoonSlots.HasValue && request.AfternoonSlots >= (request.SlotsPerDay ?? s.SlotsPerDay))
            throw new InvalidOperationException("Los slots de tarde deben ser menores que el total de slots por día.");

        if (request.WorkingDays is not null)
        {
            if (request.WorkingDays.Count == 0)
                throw new InvalidOperationException("Debe haber al menos un día lectivo.");
            if (request.WorkingDays.Distinct().Count() != request.WorkingDays.Count)
                throw new InvalidOperationException("Los días lectivos no pueden repetirse.");
            s.WorkingDays = JsonSerializer.Serialize(request.WorkingDays);
            s.DaysPerWeek = request.WorkingDays.Count;
        }

        if (request.AfternoonStart is not null && request.MorningStart is not null)
        {
            var morningSlots = (request.SlotsPerDay ?? s.SlotsPerDay) - (request.AfternoonSlots ?? s.AfternoonSlots);
            var morningEnd = SlotCalculator.ComputeEndTime(
                morningSlots,
                request.SlotMinutes ?? s.SlotMinutes,
                request.BreakAfterSlot ?? s.BreakAfterSlot,
                request.BreakMinutes ?? s.BreakMinutes,
                afternoonSlots: 0,
                TimeOnly.Parse(request.MorningStart),
                afternoonStart: null,
                isPartida: false);
            if (TimeOnly.Parse(request.AfternoonStart) <= morningEnd)
                throw new InvalidOperationException("El inicio de la jornada de tarde debe ser posterior al final de la jornada de mañana.");
        }

        await repository.SaveChangesAsync(ct);

        var cycles = await db.CycleSchedules.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId).ToListAsync(ct);
        return GetSchoolHandler.Map(s, cycles);
    }
}

public sealed class GetCycleScheduleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetCycleScheduleQuery, CycleScheduleDto?>
{
    public async Task<CycleScheduleDto?> Handle(GetCycleScheduleQuery request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolId == user.SchoolId && x.IsDefault, ct);
        if (period is null) return null;
        var c = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == period.Id && x.Cycle == request.Cycle, ct);
        if (c is null) return null;
        var slots = SlotCalculator.Compute(c, period);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}

public sealed class UpdateCycleScheduleHandler(IAppDbContext db, ICurrentUser user, ICycleScheduleRepository repository)
    : IRequestHandler<UpdateCycleScheduleCommand, CycleScheduleDto>
{
    public async Task<CycleScheduleDto> Handle(UpdateCycleScheduleCommand request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods
            .FirstOrDefaultAsync(x => x.SchoolId == user.SchoolId && x.IsDefault, ct)
            ?? throw new NotFoundException("No hay un periodo ordinario configurado.");

        var c = await repository.GetByPeriodAndCycleAsync(period.Id, request.Cycle, ct);

        var morningStart = TimeOnly.Parse(request.MorningStart);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;

        if (c is null)
        {
            c = new CycleSchedule
            {
                SchoolId = user.SchoolId, PeriodId = period.Id, Cycle = request.Cycle,
                MorningStart = morningStart,
                EndTime = TimeOnly.Parse(request.EndTime),
                AfternoonStart = afternoonStart,
            };
            db.CycleSchedules.Add(c);
        }
        else
        {
            c.MorningStart = morningStart;
            c.AfternoonStart = afternoonStart;
        }

        if (request.Breaks is not null)
        {
            db.CycleBreaks.RemoveRange(c.Breaks);
            c.Breaks = request.Breaks.Select(b => new CycleBreak
            {
                CycleScheduleId = c.Id,
                AfterSlot = b.AfterSlot,
                Minutes = b.Minutes,
            }).ToList();
        }

        c.EndTime = SlotCalculator.ComputeEndTime(c, period);

        await db.SaveChangesAsync(ct);

        var slots = SlotCalculator.Compute(c, period);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}

// ── Period CRUD ─────────────────────────────────────────────────────────────────

public record SchoolPeriodDto(
    Guid Id, string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    bool IsDefault, int SortOrder, IReadOnlyList<CycleScheduleDto> Cycles);

public record GetPeriodsQuery : IRequest<List<SchoolPeriodDto>>;
public record GetPeriodQuery(Guid PeriodId) : IRequest<SchoolPeriodDto?>;
public record CreatePeriodCommand(
    string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    int SortOrder) : IRequest<SchoolPeriodDto>;
public record UpdatePeriodCommand(
    Guid PeriodId, string? Name, IReadOnlyList<int>? Months,
    string? ScheduleType, int? SlotMinutes, int? SlotsPerDay, int? AfternoonSlots,
    int? SortOrder) : IRequest<SchoolPeriodDto>;
public record DeletePeriodCommand(Guid PeriodId) : IRequest;

public record GetPeriodCycleQuery(Guid PeriodId, int Cycle) : IRequest<CycleScheduleDto?>;
public record UpdatePeriodCycleCommand(
    Guid PeriodId, int Cycle, string MorningStart, string EndTime,
    string? AfternoonStart, IReadOnlyList<CycleBreakDto>? Breaks = null) : IRequest<CycleScheduleDto>;

public sealed class GetPeriodsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodsQuery, List<SchoolPeriodDto>>
{
    public async Task<List<SchoolPeriodDto>> Handle(GetPeriodsQuery request, CancellationToken ct)
    {
        var periods = await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .Where(p => p.SchoolId == user.SchoolId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return periods.Select(MapPeriod).ToList();
    }

    internal static SchoolPeriodDto MapPeriod(SchoolPeriod p)
    {
        var months = JsonSerializer.Deserialize<List<int>>(p.Months) ?? [];
        var cycleDtos = p.Cycles.OrderBy(c => c.Cycle).Select(c =>
        {
            var cycleSlots = SlotCalculator.Compute(c, p);
            return new CycleScheduleDto(
                c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
                c.AfternoonStart?.ToString("HH:mm"),
                cycleSlots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
                c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
        }).ToList();

        return new SchoolPeriodDto(
            p.Id, p.Key, p.Name, months,
            p.ScheduleType, p.SlotMinutes, p.SlotsPerDay, p.AfternoonSlots,
            p.IsDefault, p.SortOrder, cycleDtos);
    }
}

public sealed class GetPeriodHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodQuery, SchoolPeriodDto?>
{
    public async Task<SchoolPeriodDto?> Handle(GetPeriodQuery request, CancellationToken ct)
    {
        var p = await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct);
        return p is null ? null : GetPeriodsHandler.MapPeriod(p);
    }
}

public sealed class CreatePeriodHandler(
    IAppDbContext db, ISchoolPeriodRepository repository, ICurrentUser user)
    : IRequestHandler<CreatePeriodCommand, SchoolPeriodDto>
{
    public async Task<SchoolPeriodDto> Handle(CreatePeriodCommand request, CancellationToken ct)
    {
        ValidateMonths(request.Months);
        if (request.AfternoonSlots >= request.SlotsPerDay)
            throw new InvalidOperationException("Los slots de tarde deben ser menores que el total de slots por día.");

        if (await db.SchoolPeriods.AnyAsync(p => p.SchoolId == user.SchoolId && p.Key == request.Key, ct))
            throw new InvalidOperationException($"Ya existe un periodo con la clave '{request.Key}'.");

        var period = new SchoolPeriod
        {
            SchoolId = user.SchoolId,
            Key = request.Key,
            Name = request.Name,
            Months = JsonSerializer.Serialize(request.Months),
            ScheduleType = request.ScheduleType,
            SlotMinutes = request.SlotMinutes,
            SlotsPerDay = request.SlotsPerDay,
            AfternoonSlots = request.AfternoonSlots,
            IsDefault = !await db.SchoolPeriods.AnyAsync(p => p.SchoolId == user.SchoolId, ct),
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
                PeriodId = period.Id,
                Cycle = c,
                MorningStart = morningStart,
                EndTime = cycleEnd,
                AfternoonStart = isPartida ? new TimeOnly(15, 0) : null,
            });
        }

        await repository.AddAsync(period, ct);
        await repository.SaveChangesAsync(ct);

        return GetPeriodsHandler.MapPeriod(period);
    }

    private static void ValidateMonths(IReadOnlyList<int> months)
    {
        if (months.Count == 0)
            throw new InvalidOperationException("Debe haber al menos un mes.");
        if (months.Any(m => m < 1 || m > 12))
            throw new InvalidOperationException("Los meses deben estar entre 1 y 12.");
        if (months.Distinct().Count() != months.Count)
            throw new InvalidOperationException("Los meses no pueden repetirse.");
    }
}

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
            ValidateMonths(request.Months);
            period.Months = JsonSerializer.Serialize(request.Months);
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
        return GetPeriodsHandler.MapPeriod(reloaded);
    }

    private static void ValidateMonths(IReadOnlyList<int> months)
    {
        if (months.Count == 0)
            throw new InvalidOperationException("Debe haber al menos un mes.");
        if (months.Any(m => m < 1 || m > 12))
            throw new InvalidOperationException("Los meses deben estar entre 1 y 12.");
        if (months.Distinct().Count() != months.Count)
            throw new InvalidOperationException("Los meses no pueden repetirse.");
    }
}

public sealed class DeletePeriodHandler(
    ISchoolPeriodRepository repository, ICurrentUser user)
    : IRequestHandler<DeletePeriodCommand>
{
    public async Task Handle(DeletePeriodCommand request, CancellationToken ct)
    {
        var period = await repository.GetByIdAsync(request.PeriodId, ct, includeCycles: false)
            ?? throw new NotFoundException("Periodo no encontrado.");
        if (period.SchoolId != user.SchoolId)
            throw new NotFoundException("Periodo no encontrado.");
        if (period.IsDefault)
            throw new InvalidOperationException("No se puede eliminar el periodo ordinario.");

        repository.Delete(period);
        await repository.SaveChangesAsync(ct);
    }
}

public sealed class GetPeriodCycleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodCycleQuery, CycleScheduleDto?>
{
    public async Task<CycleScheduleDto?> Handle(GetPeriodCycleQuery request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct);
        if (period is null) return null;

        var c = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == request.PeriodId && x.Cycle == request.Cycle, ct);
        if (c is null) return null;

        var slots = SlotCalculator.Compute(c, period);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}

public sealed class UpdatePeriodCycleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdatePeriodCycleCommand, CycleScheduleDto>
{
    public async Task<CycleScheduleDto> Handle(UpdatePeriodCycleCommand request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct)
            ?? throw new NotFoundException("Periodo no encontrado.");

        var c = await db.CycleSchedules
            .Include(x => x.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == request.PeriodId && x.Cycle == request.Cycle, ct);

        var morningStart = TimeOnly.Parse(request.MorningStart);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;

        if (c is null)
        {
            c = new CycleSchedule
            {
                SchoolId = user.SchoolId, PeriodId = request.PeriodId, Cycle = request.Cycle,
                MorningStart = morningStart,
                EndTime = TimeOnly.Parse(request.EndTime),
                AfternoonStart = afternoonStart,
            };
            db.CycleSchedules.Add(c);
        }
        else
        {
            c.MorningStart = morningStart;
            c.AfternoonStart = afternoonStart;
        }

        if (request.Breaks is not null)
        {
            db.CycleBreaks.RemoveRange(c.Breaks);
            c.Breaks = request.Breaks.Select(b => new CycleBreak
            {
                CycleScheduleId = c.Id,
                AfterSlot = b.AfterSlot,
                Minutes = b.Minutes,
            }).ToList();
        }

        c.EndTime = SlotCalculator.ComputeEndTime(c, period);
        await db.SaveChangesAsync(ct);

        var slots = SlotCalculator.Compute(c, period);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}
