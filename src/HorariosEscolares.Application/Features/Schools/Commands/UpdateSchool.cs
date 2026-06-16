using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record UpdateSchoolCommand(
    string? Name, string? CenterCode, string? Locality, string? Community,
    int? MinCourseLevel, int? MaxCourseLevel, string? AcademicYear,
    string? ScheduleType, string? MorningStart, int? SlotMinutes,
    int? BreakAfterSlot, int? BreakMinutes, int? SlotsPerDay, int? AfternoonSlots,
    string? AfternoonStart, IReadOnlyList<int>? WorkingDays) : IRequest<SchoolDto>;

public sealed class UpdateSchoolHandler(IAppDbContext db, ISchoolRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateSchoolCommand, SchoolDto>
{
    public async Task<SchoolDto> Handle(UpdateSchoolCommand request, CancellationToken ct)
    {
        var school = await repository.GetByIdAsync(user.SchoolId, ct)
            ?? throw new NotFoundException("School not found");

        if (request.Name is not null) school.Name = request.Name;
        if (request.CenterCode is not null) school.CenterCode = request.CenterCode;
        if (request.Locality is not null) school.Locality = request.Locality;
        if (request.Community is not null) school.Community = request.Community;
        if (request.MinCourseLevel.HasValue) school.MinCourseLevel = request.MinCourseLevel.Value;
        if (request.MaxCourseLevel.HasValue) school.MaxCourseLevel = request.MaxCourseLevel.Value;
        if (request.AcademicYear is not null) school.AcademicYear = request.AcademicYear;
        if (request.ScheduleType is not null) school.ScheduleType = request.ScheduleType;
        if (request.MorningStart is not null) school.MorningStart = TimeOnly.Parse(request.MorningStart);
        if (request.SlotMinutes.HasValue) school.SlotMinutes = request.SlotMinutes.Value;
        if (request.BreakAfterSlot.HasValue) school.BreakAfterSlot = request.BreakAfterSlot.Value;
        if (request.BreakMinutes.HasValue) school.BreakMinutes = request.BreakMinutes.Value;
        if (request.SlotsPerDay.HasValue) school.SlotsPerDay = request.SlotsPerDay.Value;
        if (request.AfternoonSlots.HasValue) school.AfternoonSlots = request.AfternoonSlots.Value;
        school.AfternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : school.AfternoonStart;

        ValidateCourseLevels(request, school);

        if (request.AfternoonSlots.HasValue && request.AfternoonSlots >= (request.SlotsPerDay ?? school.SlotsPerDay))
            throw new InvalidOperationException("Los slots de tarde deben ser menores que el total de slots por día.");

        if (request.WorkingDays is not null)
        {
            SchoolValidationHelper.ValidateWorkingDays(request.WorkingDays);
            school.WorkingDays = System.Text.Json.JsonSerializer.Serialize(request.WorkingDays);
            school.DaysPerWeek = request.WorkingDays.Count;
        }

        if (request.AfternoonStart is not null && request.MorningStart is not null)
        {
            var morningSlots = (request.SlotsPerDay ?? school.SlotsPerDay) - (request.AfternoonSlots ?? school.AfternoonSlots);
            var morningEnd = SlotCalculator.ComputeEndTime(
                morningSlots,
                request.SlotMinutes ?? school.SlotMinutes,
                request.BreakAfterSlot ?? school.BreakAfterSlot,
                request.BreakMinutes ?? school.BreakMinutes,
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
        return SchoolMapping.MapSchool(school, cycles);
    }

    private static void ValidateCourseLevels(UpdateSchoolCommand request, School school)
    {
        var min = request.MinCourseLevel ?? school.MinCourseLevel;
        var max = request.MaxCourseLevel ?? school.MaxCourseLevel;
        if (min > max)
            throw new InvalidOperationException("El rango de cursos no es válido: el nivel mínimo no puede ser mayor que el máximo.");
    }
}
