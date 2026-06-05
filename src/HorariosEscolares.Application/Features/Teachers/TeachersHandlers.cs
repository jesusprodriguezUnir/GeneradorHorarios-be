using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Teachers;

namespace HorariosEscolares.Application.Features.Teachers;

public record StageAssignmentDto(Guid StageId, string StageName, string StageType, int? Cycle);

public record TeacherDto(
    Guid Id, string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey,
    int AssignedHours, StageAssignmentDto[] StageAssignments);

public record StageAssignmentInput(Guid StageId, int? Cycle);

public record GetAllTeachersQuery : IRequest<List<TeacherDto>>;
public record GetTeacherByIdQuery(Guid Id) : IRequest<TeacherDto?>;
public record CreateTeacherCommand(
    string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;
public record UpdateTeacherCommand(
    Guid Id, string? FullName, string? Email, string? TeacherType,
    int? MaxWeeklyHours, string[]? Specialties, string? ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;
public record DeleteTeacherCommand(Guid Id) : IRequest;

public sealed class GetAllTeachersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllTeachersQuery, List<TeacherDto>>
{
    public async Task<List<TeacherDto>> Handle(GetAllTeachersQuery request, CancellationToken ct)
    {
        var teachers = await db.Teachers.AsNoTracking()
            .Include(t => t.StageAssignments)
            .Where(t => t.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var hoursMap = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == user.SchoolId)
            .GroupBy(a => a.TeacherId)
            .Select(g => new { TeacherId = g.Key, Hours = g.Sum(a => a.WeeklyHours) })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Hours, ct);

        var stageIds = teachers.SelectMany(t => t.StageAssignments).Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return teachers.Select(t => TeacherMapper.ToDto(t, hoursMap.GetValueOrDefault(t.Id), stagesMap)).ToList();
    }
}

public sealed class GetTeacherByIdHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetTeacherByIdQuery, TeacherDto?>
{
    public async Task<TeacherDto?> Handle(GetTeacherByIdQuery request, CancellationToken ct)
    {
        var t = await db.Teachers.AsNoTracking()
            .Include(x => x.StageAssignments)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (t is null) return null;

        var hours = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id).SumAsync(a => a.WeeklyHours, ct);

        var stageIds = t.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return TeacherMapper.ToDto(t, hours, stagesMap);
    }
}

public sealed class CreateTeacherHandler(IAppDbContext db, ITeacherRepository repository, ICurrentUser user)
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

        if (request.StageAssignments is { Length: > 0 })
        {
            foreach (var sa in request.StageAssignments)
            {
                t.StageAssignments.Add(new TeacherStageAssignment
                {
                    TeacherId = t.Id,
                    StageId = sa.StageId,
                    Cycle = sa.Cycle,
                });
            }
        }

        await repository.AddAsync(t, ct);
        await repository.SaveChangesAsync(ct);

        var stagesMap = new Dictionary<Guid, SchoolStage>();
        if (t.StageAssignments.Count > 0)
        {
            var stageIds = t.StageAssignments.Select(sa => sa.StageId).Distinct();
            stagesMap = await db.SchoolStages.AsNoTracking()
                .Where(s => stageIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);
        }

        return TeacherMapper.ToDto(t, 0, stagesMap);
    }
}

public sealed class UpdateTeacherHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherCommand request, CancellationToken ct)
    {
        var t = await db.Teachers
            .Include(x => x.StageAssignments)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (t is null) throw new NotFoundException($"Teacher {request.Id} not found");

        if (request.FullName is not null) t.FullName = request.FullName;
        if (request.Email is not null) t.Email = request.Email;
        if (request.TeacherType is not null) t.TeacherType = request.TeacherType;
        if (request.MaxWeeklyHours.HasValue) t.MaxWeeklyHours = request.MaxWeeklyHours.Value;
        if (request.Specialties is not null) t.Specialties = JsonSerializer.Serialize(request.Specialties);
        if (request.ColorKey is not null) t.ColorKey = request.ColorKey;

        if (request.StageAssignments is not null)
        {
            // Replace all stage assignments
            t.StageAssignments.Clear();
            foreach (var sa in request.StageAssignments)
            {
                t.StageAssignments.Add(new TeacherStageAssignment
                {
                    TeacherId = t.Id,
                    StageId = sa.StageId,
                    Cycle = sa.Cycle,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var hours = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id).SumAsync(a => a.WeeklyHours, ct);

        var stageIds = t.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return TeacherMapper.ToDto(t, hours, stagesMap);
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

static class TeacherMapper
{
    public static TeacherDto ToDto(Teacher t, int assignedHours, Dictionary<Guid, SchoolStage> stagesMap)
    {
        string[] specialties;
        try { specialties = JsonSerializer.Deserialize<string[]>(t.Specialties) ?? []; }
        catch { specialties = []; }

        var stageAssignments = t.StageAssignments
            .Select(sa =>
            {
                stagesMap.TryGetValue(sa.StageId, out var stage);
                return new StageAssignmentDto(
                    sa.StageId,
                    stage?.Name ?? "",
                    stage?.StageType ?? "",
                    sa.Cycle);
            })
            .ToArray();

        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, specialties, t.ColorKey, assignedHours, stageAssignments);
    }
}

