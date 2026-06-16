using MediatR;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;

namespace HorariosEscolares.Application.Features.Schools;

public record DeletePeriodCommand(Guid PeriodId) : IRequest;

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
