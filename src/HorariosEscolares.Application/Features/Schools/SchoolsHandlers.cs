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
        var cycles = await db.CycleSchedules.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId).ToListAsync(ct);
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
        var c = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.SchoolId == user.SchoolId && x.Cycle == request.Cycle, ct);
        if (c is null) return null;
        var s = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == user.SchoolId, ct);
        if (s is null) return null;
        var slots = SlotCalculator.Compute(c, s);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}

public sealed class UpdateCycleScheduleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCycleScheduleCommand, CycleScheduleDto>
{
    public async Task<CycleScheduleDto> Handle(UpdateCycleScheduleCommand request, CancellationToken ct)
    {
        var c = await db.CycleSchedules
            .Include(x => x.Breaks)
            .FirstOrDefaultAsync(x => x.SchoolId == user.SchoolId && x.Cycle == request.Cycle, ct);

        var morningStart = TimeOnly.Parse(request.MorningStart);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;

        if (c is null)
        {
            c = new CycleSchedule
            {
                SchoolId = user.SchoolId, Cycle = request.Cycle,
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

        var s = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == user.SchoolId, ct);
        if (s is not null)
        {
            c.EndTime = SlotCalculator.ComputeEndTime(c, s);
        }

        await db.SaveChangesAsync(ct);

        var slots = s is not null
            ? SlotCalculator.Compute(c, s)
            : SlotCalculator.Compute(s?.SlotsPerDay ?? 6, 60, [], 0, morningStart);
        return new CycleScheduleDto(
            c.Cycle, c.MorningStart.ToString("HH:mm"), c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            c.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());
    }
}
