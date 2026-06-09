using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Classrooms;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Classrooms;

public record ClassroomDto(Guid Id, string Name, string ClassroomType, int Capacity, bool IsShared, Guid? StageId);

public record GetAllClassroomsQuery : IRequest<List<ClassroomDto>>;
public record CreateClassroomCommand(string Name, string ClassroomType, int Capacity, bool IsShared, Guid? StageId) : IRequest<ClassroomDto>;
public record UpdateClassroomCommand(Guid Id, string? Name, string? ClassroomType, int? Capacity, bool? IsShared, Guid? StageId) : IRequest<ClassroomDto>;
public record DeleteClassroomCommand(Guid Id) : IRequest;

public sealed class GetAllClassroomsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllClassroomsQuery, List<ClassroomDto>>
{
    public async Task<List<ClassroomDto>> Handle(GetAllClassroomsQuery request, CancellationToken ct)
    {
        return await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId)
            .OrderBy(c => c.Name)
            .Select(c => new ClassroomDto(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared, c.StageId))
            .ToListAsync(ct);
    }
}

public sealed class CreateClassroomHandler(IClassroomRepository repository, ICurrentUser user)
    : IRequestHandler<CreateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(CreateClassroomCommand request, CancellationToken ct)
    {
        var c = new Classroom
        {
            SchoolId = user.SchoolId, Name = request.Name, ClassroomType = request.ClassroomType,
            Capacity = request.Capacity, IsShared = request.IsShared, StageId = request.StageId,
        };
        await repository.AddAsync(c, ct);
        await repository.SaveChangesAsync(ct);
        return new(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared, c.StageId);
    }
}

public sealed class UpdateClassroomHandler(IClassroomRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(UpdateClassroomCommand request, CancellationToken ct)
    {
        var c = await repository.GetByIdAsync(request.Id, ct);
        if (c is null || c.SchoolId != user.SchoolId) throw new NotFoundException($"Classroom {request.Id} not found");
        if (request.Name is not null) c.Name = request.Name;
        if (request.ClassroomType is not null) c.ClassroomType = request.ClassroomType;
        if (request.Capacity.HasValue) c.Capacity = request.Capacity.Value;
        if (request.IsShared.HasValue) c.IsShared = request.IsShared.Value;
        if (request.StageId.HasValue) c.StageId = request.StageId;
        await repository.SaveChangesAsync(ct);
        return new(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared, c.StageId);
    }
}

public sealed class DeleteClassroomHandler(IClassroomRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteClassroomCommand>
{
    public async Task Handle(DeleteClassroomCommand request, CancellationToken ct)
    {
        var c = await repository.GetByIdAsync(request.Id, ct);
        if (c is null || c.SchoolId != user.SchoolId) throw new NotFoundException($"Classroom {request.Id} not found");
        await repository.DeleteAsync(c, ct);
        await repository.SaveChangesAsync(ct);
    }
}
