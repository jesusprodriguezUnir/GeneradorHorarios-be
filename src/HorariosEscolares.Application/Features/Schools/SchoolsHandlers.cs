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
public record CycleScheduleDto(
    int Cycle,
    string MorningStart,
    string MorningEnd,
    string? AfternoonStart,
    string? AfternoonEnd,
    /// <summary>Alias de AfternoonEnd ?? MorningEnd; se mantiene por compatibilidad con consumidores existentes.</summary>
    string EndTime,
    IReadOnlyList<SlotDto> ComputedSlots,
    IReadOnlyList<CycleBreakDto> Breaks);
public record SchoolDto(
    Guid Id, string Name, string Slug,
    string? CenterCode, string? Locality, string Community,
    int MinCourseLevel, int MaxCourseLevel, string AcademicYear,
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
    int? MinCourseLevel, int? MaxCourseLevel, string? AcademicYear,
    string? ScheduleType, string? MorningStart, int? SlotMinutes,
    int? BreakAfterSlot, int? BreakMinutes, int? SlotsPerDay, int? AfternoonSlots,
    string? AfternoonStart, IReadOnlyList<int>? WorkingDays) : IRequest<SchoolDto>;
public record NormativeCheckQuery : IRequest<NormativeCheckDto>;

public record SchoolStageDto(
    Guid Id, string StageType, string Name, int MinLevel, int MaxLevel, int SortOrder,
    string ScheduleType, string MorningStart, string? AfternoonStart,
    int SlotMinutes, int BreakAfterSlot, int BreakMinutes,
    int SlotsPerDay, int AfternoonSlots, int DaysPerWeek,
    IReadOnlyList<int> WorkingDays);

public record GetStagesQuery : IRequest<List<SchoolStageDto>>;

public record GetCycleScheduleQuery(int Cycle) : IRequest<CycleScheduleDto?>;
public record UpdateCycleScheduleCommand(
    int Cycle,
    string MorningStart,
    string MorningEnd,
    string? AfternoonStart = null,
    string? AfternoonEnd = null,
    IReadOnlyList<CycleBreakDto>? Breaks = null) : IRequest<CycleScheduleDto>;

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
                .Include(c => c.Breaks)
                .Where(c => c.PeriodId == defaultPeriod.Id).ToListAsync(ct)
            : [];
        return Map(s, cycles);
    }

    internal static SchoolDto Map(School s, List<CycleSchedule> cycles)
    {
        var slots = SlotCalculator.Compute(s, s.SlotsPerDay);
        var workingDays = SlotCalculator.ParseWorkingDays(s.WorkingDays);
        var cycleDtos = cycles.OrderBy(c => c.Cycle)
            .Select(c => MapCycle(c, SlotCalculator.Compute(c, s)))
            .ToList();
        return new SchoolDto(
            s.Id, s.Name, s.Slug,
            s.CenterCode, s.Locality, s.Community,
            s.MinCourseLevel, s.MaxCourseLevel, s.AcademicYear,
            s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"),
            s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek,
            workingDays,
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            cycleDtos);
    }

    /// <summary>Convierte una entidad CycleSchedule + sus slots ya calculados en su DTO.</summary>
    internal static CycleScheduleDto MapCycle(CycleSchedule c, List<SlotInfo> computedSlots)
        => new(
            c.Cycle,
            c.MorningStart.ToString("HH:mm"),
            c.MorningEnd.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            c.AfternoonEnd?.ToString("HH:mm"),
            c.EndTime.ToString("HH:mm"),
            computedSlots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
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

        return stages.Select(s => new SchoolStageDto(
            s.Id, s.StageType, s.Name, s.MinLevel, s.MaxLevel, s.SortOrder,
            s.ScheduleType, s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"),
            s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek,
            SlotCalculator.ParseWorkingDays(s.WorkingDays))).ToList();
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
        return GetSchoolHandler.MapCycle(c, slots);
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
        var morningEnd   = TimeOnly.Parse(request.MorningEnd);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;
        var afternoonEnd   = request.AfternoonEnd   is not null ? TimeOnly.Parse(request.AfternoonEnd)   : (TimeOnly?)null;

        ValidateBoundaries(morningStart, morningEnd, afternoonStart, afternoonEnd);

        if (c is null)
        {
            c = new CycleSchedule
            {
                SchoolId = user.SchoolId, StageId = period.StageId, PeriodId = period.Id, Cycle = request.Cycle,
                MorningStart   = morningStart,
                MorningEnd     = morningEnd,
                AfternoonStart = afternoonStart,
                AfternoonEnd   = afternoonEnd,
                EndTime        = afternoonEnd ?? morningEnd,
            };
            db.CycleSchedules.Add(c);
        }
        else
        {
            c.MorningStart   = morningStart;
            c.MorningEnd     = morningEnd;
            c.AfternoonStart = afternoonStart;
            c.AfternoonEnd   = afternoonEnd;
            c.EndTime        = afternoonEnd ?? morningEnd;
        }

        if (request.Breaks is not null)
        {
            db.CycleBreaks.RemoveRange(c.Breaks);
            db.CycleBreaks.AddRange(request.Breaks.Select(b => new CycleBreak
            {
                CycleScheduleId = c.Id,
                AfterSlot = b.AfterSlot,
                Minutes = b.Minutes,
            }));
        }

        await db.SaveChangesAsync(ct);

        var slots = SlotCalculator.Compute(c, period);
        return GetSchoolHandler.MapCycle(c, slots);
    }

    internal static void ValidateBoundaries(
        TimeOnly morningStart, TimeOnly morningEnd,
        TimeOnly? afternoonStart, TimeOnly? afternoonEnd)
    {
        if (morningStart >= morningEnd)
            throw new InvalidOperationException("La entrada de la mañana debe ser anterior a la salida de la mañana.");
        if (afternoonStart.HasValue && !afternoonEnd.HasValue)
            throw new InvalidOperationException("Si se indica inicio de tarde, debe indicarse también la salida de tarde.");
        if (!afternoonStart.HasValue && afternoonEnd.HasValue)
            throw new InvalidOperationException("Si se indica salida de tarde, debe indicarse también el inicio de tarde.");
        if (afternoonStart.HasValue && afternoonEnd.HasValue)
        {
            if (afternoonStart.Value < morningEnd)
                throw new InvalidOperationException("La entrada de la tarde no puede ser anterior a la salida de la mañana.");
            if (afternoonStart.Value >= afternoonEnd.Value)
                throw new InvalidOperationException("La entrada de la tarde debe ser anterior a la salida de la tarde.");
        }
    }
}

// ── Period CRUD ─────────────────────────────────────────────────────────────────

public record SchoolPeriodDto(
    Guid Id, Guid StageId, string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    bool IsDefault, int SortOrder, IReadOnlyList<CycleScheduleDto> Cycles);

public record GetPeriodsQuery(Guid? StageId = null) : IRequest<List<SchoolPeriodDto>>;
public record GetPeriodQuery(Guid PeriodId) : IRequest<SchoolPeriodDto?>;
public record CreatePeriodCommand(
    Guid StageId, string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    int SortOrder) : IRequest<SchoolPeriodDto>;
public record UpdatePeriodCommand(
    Guid PeriodId, string? Name, IReadOnlyList<int>? Months,
    string? ScheduleType, int? SlotMinutes, int? SlotsPerDay, int? AfternoonSlots,
    int? SortOrder) : IRequest<SchoolPeriodDto>;
public record DeletePeriodCommand(Guid PeriodId) : IRequest;

public record GetPeriodCycleQuery(Guid PeriodId, int Cycle) : IRequest<CycleScheduleDto?>;
public record UpdatePeriodCycleCommand(
    Guid PeriodId,
    int Cycle,
    string MorningStart,
    string MorningEnd,
    string? AfternoonStart = null,
    string? AfternoonEnd = null,
    IReadOnlyList<CycleBreakDto>? Breaks = null) : IRequest<CycleScheduleDto>;

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

        return periods.Select(MapPeriod).ToList();
    }

    internal static SchoolPeriodDto MapPeriod(SchoolPeriod p)
    {
        var months = JsonSerializer.Deserialize<List<int>>(p.Months) ?? [];
        var cycleDtos = p.Cycles.OrderBy(c => c.Cycle)
            .Select(c => GetSchoolHandler.MapCycle(c, SlotCalculator.Compute(c, p)))
            .ToList();

        return new SchoolPeriodDto(
            p.Id, p.StageId, p.Key, p.Name, months,
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
            Months = JsonSerializer.Serialize(request.Months),
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
        return GetSchoolHandler.MapCycle(c, slots);
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

        var morningStart   = TimeOnly.Parse(request.MorningStart);
        var morningEnd     = TimeOnly.Parse(request.MorningEnd);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;
        var afternoonEnd   = request.AfternoonEnd   is not null ? TimeOnly.Parse(request.AfternoonEnd)   : (TimeOnly?)null;

        UpdateCycleScheduleHandler.ValidateBoundaries(morningStart, morningEnd, afternoonStart, afternoonEnd);

        if (c is null)
        {
            c = new CycleSchedule
            {
                SchoolId       = user.SchoolId,
                StageId        = period.StageId,
                PeriodId       = request.PeriodId,
                Cycle          = request.Cycle,
                MorningStart   = morningStart,
                MorningEnd     = morningEnd,
                AfternoonStart = afternoonStart,
                AfternoonEnd   = afternoonEnd,
                EndTime        = afternoonEnd ?? morningEnd,
            };
            db.CycleSchedules.Add(c);
        }
        else
        {
            c.MorningStart   = morningStart;
            c.MorningEnd     = morningEnd;
            c.AfternoonStart = afternoonStart;
            c.AfternoonEnd   = afternoonEnd;
            c.EndTime        = afternoonEnd ?? morningEnd;
        }

        if (request.Breaks is not null)
        {
            db.CycleBreaks.RemoveRange(c.Breaks);
            db.CycleBreaks.AddRange(request.Breaks.Select(b => new CycleBreak
            {
                CycleScheduleId = c.Id,
                AfterSlot = b.AfterSlot,
                Minutes = b.Minutes,
            }));
        }

        await db.SaveChangesAsync(ct);

        var slots = SlotCalculator.Compute(c, period);
        return GetSchoolHandler.MapCycle(c, slots);
    }
}

// ── Stage CRUD ─────────────────────────────────────────────────────────────────

public record CreateStageCommand(string StageType) : IRequest<SchoolStageDto>;

public record DeleteStageCommand(Guid StageId) : IRequest;

public record UpdateStageCommand(
    Guid StageId, string? Name = null, int? MinLevel = null, int? MaxLevel = null,
    int? SortOrder = null, string? ScheduleType = null, string? MorningStart = null,
    int? SlotMinutes = null, int? BreakAfterSlot = null, int? BreakMinutes = null,
    int? SlotsPerDay = null, int? AfternoonSlots = null, int? DaysPerWeek = null,
    string? AfternoonStart = null, IReadOnlyList<int>? WorkingDays = null) : IRequest<SchoolStageDto>;

public sealed class CreateStageHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CreateStageCommand, SchoolStageDto>
{
    public async Task<SchoolStageDto> Handle(CreateStageCommand request, CancellationToken ct)
    {
        var stageType = request.StageType.ToLowerInvariant();

        var exists = await db.SchoolStages
            .AnyAsync(st => st.SchoolId == user.SchoolId && st.StageType == stageType, ct);
        if (exists)
            throw new InvalidOperationException($"La etapa '{stageType}' ya existe en este centro.");

        string name;
        int minLevel;
        int maxLevel;
        int sortOrder;
        int slotsPerDay;
        TimeOnly morningStart;
        int breakAfterSlot;

        switch (stageType)
        {
            case StageTypes.Infantil:
                name = "Educación Infantil";
                minLevel = 1;
                maxLevel = 2;
                sortOrder = 0;
                slotsPerDay = 5;
                morningStart = new TimeOnly(9, 0);
                breakAfterSlot = 2;
                break;
            case StageTypes.Primaria:
                name = "Educación Primaria";
                minLevel = 1;
                maxLevel = 6;
                sortOrder = 1;
                slotsPerDay = 5;
                morningStart = new TimeOnly(9, 0);
                breakAfterSlot = 2;
                break;
            case StageTypes.Secundaria:
                name = "Educación Secundaria (ESO)";
                minLevel = 1;
                maxLevel = 4;
                sortOrder = 2;
                slotsPerDay = 6;
                morningStart = new TimeOnly(8, 30);
                breakAfterSlot = 3;
                break;
            default:
                name = char.ToUpper(stageType[0]) + stageType[1..];
                minLevel = 1;
                maxLevel = 6;
                sortOrder = 3;
                slotsPerDay = 5;
                morningStart = new TimeOnly(9, 0);
                breakAfterSlot = 2;
                break;
        }

        var newStage = new SchoolStage
        {
            SchoolId = user.SchoolId,
            StageType = stageType,
            Name = name,
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            SortOrder = sortOrder,
            SlotsPerDay = slotsPerDay,
            MorningStart = morningStart,
            BreakAfterSlot = breakAfterSlot,
            ScheduleType = "continua",
            AfternoonStart = null,
            SlotMinutes = 60,
            BreakMinutes = 30,
            DaysPerWeek = 5,
            WorkingDays = "[1,2,3,4,5]"
        };

        db.SchoolStages.Add(newStage);

        var newPeriod = new SchoolPeriod
        {
            SchoolId = user.SchoolId,
            StageId = newStage.Id,
            Key = "ordinario",
            Name = "Jornada ordinaria",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = newStage.ScheduleType,
            SlotMinutes = newStage.SlotMinutes,
            SlotsPerDay = newStage.SlotsPerDay,
            AfternoonSlots = newStage.AfternoonSlots,
            IsDefault = true,
            SortOrder = 0
        };

        var breaksList = newStage.BreakAfterSlot >= 0 && newStage.BreakMinutes > 0
            ? new[] { (newStage.BreakAfterSlot, newStage.BreakMinutes) }
            : Array.Empty<(int, int)>();

        var cycleEnd = SlotCalculator.ComputeEndTime(
            totalSlots: newPeriod.SlotsPerDay,
            slotMinutes: newPeriod.SlotMinutes,
            breaks: breaksList,
            afternoonSlots: newPeriod.AfternoonSlots,
            morningStart: newStage.MorningStart,
            afternoonStart: null,
            isPartida: false);

        for (int c = 1; c <= 3; c++)
        {
            var cycleSchedule = new CycleSchedule
            {
                SchoolId = user.SchoolId,
                StageId = newStage.Id,
                PeriodId = newPeriod.Id,
                Cycle = c,
                MorningStart = newStage.MorningStart,
                EndTime = cycleEnd,
                AfternoonStart = null
            };

            if (newStage.BreakAfterSlot >= 0 && newStage.BreakMinutes > 0)
            {
                cycleSchedule.Breaks.Add(new CycleBreak
                {
                    CycleScheduleId = cycleSchedule.Id,
                    AfterSlot = newStage.BreakAfterSlot,
                    Minutes = newStage.BreakMinutes
                });
            }

            newPeriod.Cycles.Add(cycleSchedule);
        }

        db.SchoolPeriods.Add(newPeriod);
        await db.SaveChangesAsync(ct);

        return new SchoolStageDto(
            newStage.Id, newStage.StageType, newStage.Name,
            newStage.MinLevel, newStage.MaxLevel, newStage.SortOrder,
            newStage.ScheduleType,
            newStage.MorningStart.ToString("HH:mm"),
            newStage.AfternoonStart?.ToString("HH:mm"),
            newStage.SlotMinutes, newStage.BreakAfterSlot, newStage.BreakMinutes,
            newStage.SlotsPerDay, newStage.AfternoonSlots, newStage.DaysPerWeek,
            SlotCalculator.ParseWorkingDays(newStage.WorkingDays));
    }
}

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
            if (request.WorkingDays.Count == 0)
                throw new InvalidOperationException("Debe haber al menos un día lectivo.");
            if (request.WorkingDays.Distinct().Count() != request.WorkingDays.Count)
                throw new InvalidOperationException("Los días lectivos no pueden repetirse.");
            stage.WorkingDays = JsonSerializer.Serialize(request.WorkingDays);
        }

        await db.SaveChangesAsync(ct);

        return new SchoolStageDto(
            stage.Id, stage.StageType, stage.Name,
            stage.MinLevel, stage.MaxLevel, stage.SortOrder,
            stage.ScheduleType,
            stage.MorningStart.ToString("HH:mm"),
            stage.AfternoonStart?.ToString("HH:mm"),
            stage.SlotMinutes, stage.BreakAfterSlot, stage.BreakMinutes,
            stage.SlotsPerDay, stage.AfternoonSlots, stage.DaysPerWeek,
            SlotCalculator.ParseWorkingDays(stage.WorkingDays));
    }
}
