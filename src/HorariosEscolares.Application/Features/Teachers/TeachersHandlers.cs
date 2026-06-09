using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Teachers;
using HorariosEscolares.Application.Features.Assignments;

namespace HorariosEscolares.Application.Features.Teachers;

public record StageAssignmentDto(Guid StageId, string StageName, string StageType, int? Cycle);
public record SubjectHourDto(string SubjectKey, int WeeklyHours);
public record SubjectHourInput(string SubjectKey, int WeeklyHours);
public record TeacherAssignmentDto(Guid Id, Guid GroupId, string GroupDisplay, Guid AllocationId, string SubjectName, int WeeklyHours);

public record TeacherDto(
    Guid Id, string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, SubjectHourDto[] SubjectHours, string ColorKey,
    int AssignedHours, StageAssignmentDto[] StageAssignments,
    TeacherAssignmentDto[] Assignments);

public record StageAssignmentInput(Guid StageId, int? Cycle);
public record TeacherAssignmentInput(Guid AllocationId, Guid GroupId, int WeeklyHours);
public record UpdateTeacherAssignmentsCommand(Guid TeacherId, TeacherAssignmentInput[] Assignments) : IRequest<TeacherDto>;

public record GetAllTeachersQuery : IRequest<List<TeacherDto>>;
public record GetTeacherByIdQuery(Guid Id) : IRequest<TeacherDto?>;
public record CreateTeacherCommand(
    string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, SubjectHourInput[]? SubjectHours, string ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;
public record UpdateTeacherCommand(
    Guid Id, string? FullName, string? Email, string? TeacherType,
    int? MaxWeeklyHours, SubjectHourInput[]? SubjectHours, string? ColorKey,
    StageAssignmentInput[]? StageAssignments) : IRequest<TeacherDto>;
public record DeleteTeacherCommand(Guid Id) : IRequest;

public sealed class GetAllTeachersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllTeachersQuery, List<TeacherDto>>
{
    public async Task<List<TeacherDto>> Handle(GetAllTeachersQuery request, CancellationToken ct)
    {
        var teachers = await db.Teachers.AsNoTracking()
            .Include(t => t.StageAssignments)
            .Include(t => t.SubjectHours)
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
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (t is null) return null;

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.Id && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var assignedHours = assignments.Sum(a => a.WeeklyHours);

        var stageIds = t.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        TeacherAssignmentDto[] assignmentDtos = [];
        if (assignments.Count > 0)
        {
            var groupIds = assignments.Select(a => a.GroupId).ToHashSet();
            var groupNames = await db.CourseGroups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);

            var allocationIds = assignments.Select(a => a.AllocationId).ToHashSet();
            var allocationNames = await db.SubjectAllocations.AsNoTracking()
                .Where(s => allocationIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SubjectName, ct);

            assignmentDtos = assignments.Select(a => new TeacherAssignmentDto(
                a.Id,
                a.GroupId,
                groupNames.GetValueOrDefault(a.GroupId, "?"),
                a.AllocationId,
                allocationNames.GetValueOrDefault(a.AllocationId, "?"),
                a.WeeklyHours
            )).ToArray();
        }

        return TeacherMapper.ToDto(t, assignedHours, stagesMap, assignmentDtos);
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
            ColorKey = request.ColorKey,
        };

        if (request.SubjectHours is not null)
        {
            foreach (var sh in request.SubjectHours)
            {
                t.SubjectHours.Add(new TeacherSubjectHour
                {
                    TeacherId = t.Id,
                    SubjectKey = sh.SubjectKey.ToLower().Trim(),
                    WeeklyHours = sh.WeeklyHours,
                });
            }
        }

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
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.SchoolId == user.SchoolId, ct);
        if (t is null) throw new NotFoundException($"Teacher {request.Id} not found");

        if (request.FullName is not null) t.FullName = request.FullName;
        if (request.Email is not null) t.Email = request.Email;
        if (request.TeacherType is not null) t.TeacherType = request.TeacherType;
        if (request.MaxWeeklyHours.HasValue) t.MaxWeeklyHours = request.MaxWeeklyHours.Value;
        if (request.ColorKey is not null) t.ColorKey = request.ColorKey;

        if (request.SubjectHours is not null)
        {
            var newHours = request.SubjectHours.ToDictionary(
                sh => sh.SubjectKey.ToLower().Trim(),
                sh => sh.WeeklyHours
            );

            // Eliminar las que ya no están
            var toRemove = t.SubjectHours
                .Where(sh => !newHours.ContainsKey(sh.SubjectKey))
                .ToList();
            foreach (var item in toRemove)
            {
                t.SubjectHours.Remove(item);
                db.TeacherSubjectHours.Remove(item);
            }

            // Actualizar existentes o añadir nuevas
            foreach (var kvp in newHours)
            {
                var existing = t.SubjectHours.FirstOrDefault(sh => sh.SubjectKey == kvp.Key);
                if (existing is not null)
                {
                    existing.WeeklyHours = kvp.Value;
                }
                else
                {
                    var newHour = new TeacherSubjectHour
                    {
                        TeacherId = t.Id,
                        SubjectKey = kvp.Key,
                        WeeklyHours = kvp.Value,
                    };
                    t.SubjectHours.Add(newHour);
                    db.TeacherSubjectHours.Add(newHour);
                }
            }
        }

        if (request.StageAssignments is not null)
        {
            var newStageIds = request.StageAssignments.Select(sa => sa.StageId).ToHashSet();

            // Eliminar solo si la StageId ya no está en la lista entrante
            var toRemove = t.StageAssignments
                .Where(sa => !newStageIds.Contains(sa.StageId))
                .ToList();
            foreach (var item in toRemove)
            {
                t.StageAssignments.Remove(item);
                db.TeacherStageAssignments.Remove(item);
            }

            // Añadir las que no existan exactamente (StageId + Cycle)
            foreach (var ns in request.StageAssignments)
            {
                var exists = t.StageAssignments.Any(sa => sa.StageId == ns.StageId && sa.Cycle == ns.Cycle);
                if (!exists)
                {
                    var newStage = new TeacherStageAssignment
                    {
                        TeacherId = t.Id,
                        StageId = ns.StageId,
                        Cycle = ns.Cycle,
                    };
                    t.StageAssignments.Add(newStage);
                    db.TeacherStageAssignments.Add(newStage);
                }
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

public sealed class UpdateTeacherAssignmentsHandler(IAppDbContext db, ICurrentUser user, ICycleResolver cycleResolver)
    : IRequestHandler<UpdateTeacherAssignmentsCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherAssignmentsCommand request, CancellationToken ct)
    {
        // 1. Verificar que el profesor pertenece al centro
        var teacherExists = await db.Teachers.AnyAsync(
            t => t.Id == request.TeacherId && t.SchoolId == user.SchoolId, ct);
        if (!teacherExists) throw new NotFoundException($"Teacher {request.TeacherId} not found");

        // 2. Validar que todos los groupId y allocationId existen en el centro
        if (request.Assignments.Length > 0)
        {
            var validGroupIds = (await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId)
                .Select(g => g.Id)
                .ToListAsync(ct)).ToHashSet();

            var validAllocationIds = (await db.SubjectAllocations.AsNoTracking()
                .Select(a => a.Id)
                .ToListAsync(ct)).ToHashSet();

            var badGroup = request.Assignments.FirstOrDefault(a => !validGroupIds.Contains(a.GroupId));
            if (badGroup is not null)
                throw new InvalidOperationException($"Grupo no válido: {badGroup.GroupId}");

            var badAlloc = request.Assignments.FirstOrDefault(a => !validAllocationIds.Contains(a.AllocationId));
            if (badAlloc is not null)
                throw new InvalidOperationException($"Asignatura no válida: {badAlloc.AllocationId}");
        }

        // 3. Cargar asignaciones actuales del profesor en este centro
        var existing = await db.Assignments
            .Where(a => a.TeacherId == request.TeacherId && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        // 4. Sincronizar por clave (GroupId, AllocationId)
        // Agrupar para evitar duplicados en la misma clave (GroupId, AllocationId)
        var newSet = request.Assignments
            .GroupBy(a => (a.GroupId, a.AllocationId))
            .ToDictionary(
                g => g.Key,
                g => new TeacherAssignmentInput(
                    g.Key.AllocationId,
                    g.Key.GroupId,
                    g.Sum(a => a.WeeklyHours)));

        // Eliminar las que ya no están
        var toRemove = existing
            .Where(e => !newSet.ContainsKey((e.GroupId, e.AllocationId)))
            .ToList();
        db.Assignments.RemoveRange(toRemove);

        // Actualizar existentes o añadir nuevas
        foreach (var (key, input) in newSet)
        {
            var match = existing.FirstOrDefault(
                e => e.GroupId == key.GroupId && e.AllocationId == key.AllocationId);
            if (match is not null)
            {
                match.WeeklyHours = input.WeeklyHours;
            }
            else
            {
                db.Assignments.Add(new Assignment
                {
                    SchoolId = user.SchoolId,
                    TeacherId = request.TeacherId,
                    GroupId = key.GroupId,
                    AllocationId = key.AllocationId,
                    WeeklyHours = input.WeeklyHours,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // 5. Asegurar TeacherStageAssignment para cada grupo asignado
        var distinctGroupIds = newSet.Keys.Select(k => k.GroupId).Distinct();
        foreach (var groupId in distinctGroupIds)
        {
            await TeacherStageAssignmentHelper.EnsureForGroupAsync(db, cycleResolver, request.TeacherId, groupId, ct);
        }
        await db.SaveChangesAsync(ct);

        // 6. Recomponer TeacherDto con assignments finales
        var teacher = await db.Teachers.AsNoTracking()
            .Include(x => x.StageAssignments)
            .Include(x => x.SubjectHours)
            .FirstOrDefaultAsync(x => x.Id == request.TeacherId, ct);

        var finalAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.TeacherId == request.TeacherId && a.SchoolId == user.SchoolId)
            .ToListAsync(ct);

        var assignedHours = finalAssignments.Sum(a => a.WeeklyHours);

        var groupIds = finalAssignments.Select(a => a.GroupId).ToHashSet();
        var groupNames = await db.CourseGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);

        var allocationIds = finalAssignments.Select(a => a.AllocationId).ToHashSet();
        var allocationNames = await db.SubjectAllocations.AsNoTracking()
            .Where(s => allocationIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SubjectName, ct);

        var assignmentDtos = finalAssignments.Select(a => new TeacherAssignmentDto(
            a.Id,
            a.GroupId,
            groupNames.GetValueOrDefault(a.GroupId, "?"),
            a.AllocationId,
            allocationNames.GetValueOrDefault(a.AllocationId, "?"),
            a.WeeklyHours
        )).ToArray();

        var stageIds = teacher!.StageAssignments.Select(sa => sa.StageId).Distinct();
        var stagesMap = await db.SchoolStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        return TeacherMapper.ToDto(teacher, assignedHours, stagesMap, assignmentDtos);
    }
}

static class TeacherMapper
{
    public static TeacherDto ToDto(Teacher t, int assignedHours, Dictionary<Guid, SchoolStage> stagesMap,
        TeacherAssignmentDto[]? assignments = null)
    {
        var subjectHours = t.SubjectHours
            .Select(sh => new SubjectHourDto(sh.SubjectKey, sh.WeeklyHours))
            .ToArray();

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
            t.MaxWeeklyHours, subjectHours, t.ColorKey, assignedHours, stageAssignments,
            assignments ?? []);
    }
}
