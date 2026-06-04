using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Teachers;

namespace HorariosEscolares.Application.Features.Teachers;

public record TeacherDto(
    Guid Id, string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey,
    int AssignedHours);

public record GetAllTeachersQuery : IRequest<List<TeacherDto>>;
public record GetTeacherByIdQuery(Guid Id) : IRequest<TeacherDto?>;
public record CreateTeacherCommand(
    string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey) : IRequest<TeacherDto>;
public record UpdateTeacherCommand(
    Guid Id, string? FullName, string? Email, string? TeacherType,
    int? MaxWeeklyHours, string[]? Specialties, string? ColorKey) : IRequest<TeacherDto>;
public record DeleteTeacherCommand(Guid Id) : IRequest;

public sealed class GetAllTeachersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllTeachersQuery, List<TeacherDto>>
{
    public async Task<List<TeacherDto>> Handle(GetAllTeachersQuery request, CancellationToken ct)
    {
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var hoursMap = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .GroupBy(a => a.TeacherId)
            .Select(g => new { TeacherId = g.Key, Hours = g.Sum(a => a.WeeklyHours) })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Hours, ct);

        return teachers.Select(t => ToDto(t, hoursMap.GetValueOrDefault(t.Id))).ToList();
    }

    private static TeacherDto ToDto(Teacher t, int assignedHours)
    {
        string[] specialties;
        try { specialties = JsonSerializer.Deserialize<string[]>(t.Specialties) ?? []; }
        catch { specialties = []; }
        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, specialties, t.ColorKey, assignedHours);
    }
}

public sealed class GetTeacherByIdHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetTeacherByIdQuery, TeacherDto?>
{
    public async Task<TeacherDto?> Handle(GetTeacherByIdQuery request, CancellationToken ct)
    {
        var t = await db.Teachers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (t is null) return null;
        var hours = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id).SumAsync(a => a.WeeklyHours, ct);

        string[] specialties;
        try { specialties = JsonSerializer.Deserialize<string[]>(t.Specialties) ?? []; }
        catch { specialties = []; }
        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, specialties, t.ColorKey, hours);
    }
}

public sealed class CreateTeacherHandler(ITeacherRepository repository, ICurrentUser user)
    : IRequestHandler<CreateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(CreateTeacherCommand request, CancellationToken ct)
    {
        var t = new Teacher
        {
            SchoolId = user.SchoolId, FullName = request.FullName, Email = request.Email,
            TeacherType = request.TeacherType, MaxWeeklyHours = request.MaxWeeklyHours,
            Specialties = JsonSerializer.Serialize(request.Specialties),
            ColorKey = request.ColorKey,
        };
        await repository.AddAsync(t, ct);
        await repository.SaveChangesAsync(ct);
        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, request.Specialties, t.ColorKey, 0);
    }
}

public sealed class UpdateTeacherHandler(IAppDbContext db, ITeacherRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherCommand request, CancellationToken ct)
    {
        var t = await repository.GetByIdAsync(request.Id, ct);
        if (t is null || t.SchoolId != user.SchoolId) throw new NotFoundException($"Teacher {request.Id} not found");
        if (request.FullName is not null) t.FullName = request.FullName;
        if (request.Email is not null) t.Email = request.Email;
        if (request.TeacherType is not null) t.TeacherType = request.TeacherType;
        if (request.MaxWeeklyHours.HasValue) t.MaxWeeklyHours = request.MaxWeeklyHours.Value;
        if (request.Specialties is not null) t.Specialties = JsonSerializer.Serialize(request.Specialties);
        if (request.ColorKey is not null) t.ColorKey = request.ColorKey;
        await repository.SaveChangesAsync(ct);
        var hours = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id).SumAsync(a => a.WeeklyHours, ct);
        string[] specialties;
        try { specialties = JsonSerializer.Deserialize<string[]>(t.Specialties) ?? []; }
        catch { specialties = []; }
        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, specialties, t.ColorKey, hours);
    }
}

public sealed class DeleteTeacherHandler(ITeacherRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteTeacherCommand>
{
    public async Task Handle(DeleteTeacherCommand request, CancellationToken ct)
    {
        var t = await repository.GetByIdAsync(request.Id, ct);
        if (t is null || t.SchoolId != user.SchoolId) throw new NotFoundException($"Teacher {request.Id} not found");
        await repository.DeleteAsync(t, ct);
        await repository.SaveChangesAsync(ct);
    }
}
