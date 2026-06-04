using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Constraints;

public record ConstraintDto(
    Guid Id, Guid TeacherId, string TeacherName,
    string ConstraintType, int DayOfWeek, int SlotIndex,
    int Weight, string? Reason);

public record GetAllConstraintsQuery : IRequest<List<ConstraintDto>>;
public record GetConstraintsByTeacherQuery(Guid TeacherId) : IRequest<List<ConstraintDto>>;
public record CreateConstraintCommand(
    Guid TeacherId, string ConstraintType,
    int DayOfWeek, int SlotIndex,
    int Weight, string? Reason) : IRequest<ConstraintDto>;
public record DeleteConstraintCommand(Guid Id) : IRequest;

public sealed class GetAllConstraintsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllConstraintsQuery, List<ConstraintDto>>
{
    public async Task<List<ConstraintDto>> Handle(GetAllConstraintsQuery request, CancellationToken ct)
    {
        var constraints = await db.TeacherConstraints.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId)
            .ToListAsync(ct);
        var teacherNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == user.SchoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);
        return constraints.Select(c => ToDto(c, teacherNames)).ToList();
    }

    private static ConstraintDto ToDto(TeacherConstraint c, Dictionary<Guid, string> names)
        => new(c.Id, c.TeacherId, names.GetValueOrDefault(c.TeacherId, "?"),
            c.ConstraintType, c.DayOfWeek, c.SlotIndex, c.Weight, c.Reason);
}

public sealed class GetConstraintsByTeacherHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetConstraintsByTeacherQuery, List<ConstraintDto>>
{
    public async Task<List<ConstraintDto>> Handle(GetConstraintsByTeacherQuery request, CancellationToken ct)
    {
        var constraints = await db.TeacherConstraints.AsNoTracking()
            .Where(c => c.SchoolId == user.SchoolId && c.TeacherId == request.TeacherId)
            .ToListAsync(ct);
        var name = await db.Teachers.AsNoTracking()
            .Where(t => t.Id == request.TeacherId && t.SchoolId == user.SchoolId)
            .Select(t => t.FullName).FirstOrDefaultAsync(ct);
        var nameMap = name is not null ? new Dictionary<Guid, string> { [request.TeacherId] = name } : new();
        return constraints.Select(c => new ConstraintDto(c.Id, c.TeacherId,
            nameMap.GetValueOrDefault(c.TeacherId, "?"), c.ConstraintType,
            c.DayOfWeek, c.SlotIndex, c.Weight, c.Reason)).ToList();
    }
}

public sealed class CreateConstraintHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CreateConstraintCommand, ConstraintDto>
{
    public async Task<ConstraintDto> Handle(CreateConstraintCommand request, CancellationToken ct)
    {
        var c = new TeacherConstraint
        {
            SchoolId = user.SchoolId, TeacherId = request.TeacherId,
            ConstraintType = request.ConstraintType, DayOfWeek = request.DayOfWeek,
            SlotIndex = request.SlotIndex, Weight = request.Weight, Reason = request.Reason,
        };
        db.TeacherConstraints.Add(c);
        await db.SaveChangesAsync(ct);
        var name = await db.Teachers.AsNoTracking()
            .Where(t => t.Id == request.TeacherId).Select(t => t.FullName).FirstOrDefaultAsync(ct) ?? "?";
        return new ConstraintDto(c.Id, c.TeacherId, name, c.ConstraintType,
            c.DayOfWeek, c.SlotIndex, c.Weight, c.Reason);
    }
}

public sealed class DeleteConstraintHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteConstraintCommand>
{
    public async Task Handle(DeleteConstraintCommand request, CancellationToken ct)
    {
        var c = await db.TeacherConstraints.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (c is null) throw new NotFoundException($"Constraint {request.Id} not found");
        db.TeacherConstraints.Remove(c);
        await db.SaveChangesAsync(ct);
    }
}
