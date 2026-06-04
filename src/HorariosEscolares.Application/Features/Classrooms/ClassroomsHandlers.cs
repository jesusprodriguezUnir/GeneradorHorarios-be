using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Classrooms;

public record ClassroomDto(Guid Id, string Name, string ClassroomType, int Capacity, bool IsShared);

public record GetAllClassroomsQuery : IRequest<List<ClassroomDto>>;
public record CreateClassroomCommand(string Name, string ClassroomType, int Capacity, bool IsShared) : IRequest<ClassroomDto>;
public record UpdateClassroomCommand(Guid Id, string? Name, string? ClassroomType, int? Capacity, bool? IsShared) : IRequest<ClassroomDto>;
public record DeleteClassroomCommand(Guid Id) : IRequest;

public sealed class GetAllClassroomsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllClassroomsQuery, List<ClassroomDto>>
{
    public async Task<List<ClassroomDto>> Handle(GetAllClassroomsQuery request, CancellationToken ct)
    {
        return await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId)
            .OrderBy(c => c.Name)
            .Select(c => new ClassroomDto(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared))
            .ToListAsync(ct);
    }
}

public sealed class CreateClassroomHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CreateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(CreateClassroomCommand request, CancellationToken ct)
    {
        var c = new Classroom
        {
            SchoolId = user.SchoolId, Name = request.Name, ClassroomType = request.ClassroomType,
            Capacity = request.Capacity, IsShared = request.IsShared,
        };
        db.Classrooms.Add(c);
        await db.SaveChangesAsync(ct);
        return new(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared);
    }
}

public sealed class UpdateClassroomHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(UpdateClassroomCommand request, CancellationToken ct)
    {
        var c = await db.Classrooms.FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (c is null) throw new NotFoundException($"Classroom {request.Id} not found");
        if (request.Name is not null) c.Name = request.Name;
        if (request.ClassroomType is not null) c.ClassroomType = request.ClassroomType;
        if (request.Capacity.HasValue) c.Capacity = request.Capacity.Value;
        if (request.IsShared.HasValue) c.IsShared = request.IsShared.Value;
        await db.SaveChangesAsync(ct);
        return new(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared);
    }
}

public sealed class DeleteClassroomHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteClassroomCommand>
{
    public async Task Handle(DeleteClassroomCommand request, CancellationToken ct)
    {
        var c = await db.Classrooms.FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (c is null) throw new NotFoundException($"Classroom {request.Id} not found");
        db.Classrooms.Remove(c);
        await db.SaveChangesAsync(ct);
    }
}
