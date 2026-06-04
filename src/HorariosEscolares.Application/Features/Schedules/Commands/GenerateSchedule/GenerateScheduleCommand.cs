using MediatR;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public record GenerateScheduleCommand(
    string AcademicYear,
    int TimeoutSeconds = 30) : IRequest<GenerateScheduleResult>;
