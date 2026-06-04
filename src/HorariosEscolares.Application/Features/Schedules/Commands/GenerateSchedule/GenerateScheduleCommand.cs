using MediatR;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public record GenerateScheduleCommand(
    Guid PeriodId,
    string AcademicYear,
    int TimeoutSeconds = 30) : IRequest<GenerateScheduleResult>;
