using MediatR;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Teachers;

namespace HorariosEscolares.Application.Features.Teachers;

public record DeleteTeacherCommand(Guid Id) : IRequest;

public sealed class DeleteTeacherHandler(ITeacherRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteTeacherCommand>
{
    public async Task Handle(DeleteTeacherCommand request, CancellationToken ct)
    {
        var teacher = await repository.GetByIdAsync(request.Id, ct);
        if (teacher is null || teacher.SchoolId != user.SchoolId) throw new NotFoundException($"Teacher {request.Id} not found");
        await repository.DeleteAsync(teacher, ct);
        await repository.SaveChangesAsync(ct);
    }
}
