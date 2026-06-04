using MediatR;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Application.Common.Exceptions;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public sealed class GenerateScheduleHandler(
    ICurrentUser user,
    IScheduleGenerationOrchestrator orchestrator)
    : IRequestHandler<GenerateScheduleCommand, GenerateScheduleResult>
{
    public async Task<GenerateScheduleResult> Handle(GenerateScheduleCommand request, CancellationToken ct)
    {
        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Solo administradores pueden generar horarios.");

        return await orchestrator.GenerateAsync(
            user.SchoolId, request.AcademicYear, request.TimeoutSeconds, null, ct);
    }
}
