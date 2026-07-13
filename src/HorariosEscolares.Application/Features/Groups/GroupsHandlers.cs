using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Groups;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Groups;

public record GroupDto(Guid Id, Guid StageId, int CourseLevel, string GroupLabel, string DisplayName,
    int StudentCount, Guid? TutorId, string? TutorName, Guid? HomeClassroomId, Dictionary<string, int> SubjectHours, int Cycle);

public record GetAllGroupsQuery(Guid? StageId = null) : IRequest<List<GroupDto>>;
public record CreateGroupCommand(Guid StageId, int CourseLevel, string GroupLabel, int StudentCount,
    Guid? TutorId, Guid? HomeClassroomId, Dictionary<string, int>? SubjectHours) : IRequest<GroupDto>;
public record UpdateGroupCommand(Guid Id, int? CourseLevel, string? GroupLabel, int? StudentCount,
    Guid? TutorId, Guid? HomeClassroomId, Dictionary<string, int>? SubjectHours) : IRequest<GroupDto>;
public record DeleteGroupCommand(Guid Id) : IRequest;

internal static class GroupMapping
{
    public static GroupDto ToDto(CourseGroup gr, Dictionary<Guid, string> tutorNames, int cycle)
    {
        var subjectHours = gr.SubjectHoursList?.ToDictionary(x => x.SubjectKey, x => x.Hours) ?? new();
        return new(gr.Id, gr.StageId, gr.CourseLevel, gr.GroupLabel, gr.DisplayName, gr.StudentCount,
            gr.TutorId, gr.TutorId.HasValue && tutorNames.TryGetValue(gr.TutorId.Value, out var n) ? n : null,
            gr.HomeClassroomId, subjectHours, cycle);
    }
}

public sealed class GetAllGroupsHandler(IAppDbContext db, ICurrentUser user, ICycleResolver cycleResolver)
    : IRequestHandler<GetAllGroupsQuery, List<GroupDto>>
{
    public async Task<List<GroupDto>> Handle(GetAllGroupsQuery request, CancellationToken ct)
    {
        var query = db.CourseGroups.AsNoTracking()
            .Include(x => x.SubjectHoursList)
            .Where(x => x.SchoolId == user.SchoolId);
        if (request.StageId.HasValue)
            query = query.Where(x => x.StageId == request.StageId.Value);

        var groups = await query
            .OrderBy(x => x.CourseLevel).ThenBy(x => x.GroupLabel)
            .ToListAsync(ct);
        var tutorNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == user.SchoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);
        var stageTypes = await db.SchoolStages.AsNoTracking()
            .Where(s => s.SchoolId == user.SchoolId)
            .ToDictionaryAsync(s => s.Id, s => s.StageType, ct);

        return groups.Select(gr => GroupMapping.ToDto(gr, tutorNames,
            cycleResolver.ResolveCycle(stageTypes.GetValueOrDefault(gr.StageId, StageTypes.Primaria), gr.CourseLevel))).ToList();
    }
}

public sealed class CreateGroupHandler(IAppDbContext db, IGroupRepository repository, ICurrentUser user, ICycleResolver cycleResolver)
    : IRequestHandler<CreateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(CreateGroupCommand request, CancellationToken ct)
    {
        var stage = await db.SchoolStages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StageId && s.SchoolId == user.SchoolId, ct)
            ?? throw new NotFoundException("La etapa indicada no existe en este centro.");

        var gr = new CourseGroup
        {
            SchoolId = user.SchoolId, StageId = request.StageId,
            CourseLevel = request.CourseLevel, GroupLabel = request.GroupLabel,
            StudentCount = request.StudentCount, TutorId = request.TutorId, HomeClassroomId = request.HomeClassroomId,
        };
        if (request.SubjectHours is not null)
        {
            gr.SubjectHoursList = request.SubjectHours.Select(kv => new GroupSubjectHour
            {
                GroupId = gr.Id,
                SubjectKey = kv.Key.ToLower().Trim(),
                Hours = kv.Value
            }).ToList();
        }
        await repository.AddAsync(gr, ct);
        await repository.SaveChangesAsync(ct);
        return GroupMapping.ToDto(gr, [], cycleResolver.ResolveCycle(stage.StageType, gr.CourseLevel));
    }
}

public sealed class UpdateGroupHandler(IAppDbContext db, IGroupRepository repository, ICurrentUser user, ICycleResolver cycleResolver)
    : IRequestHandler<UpdateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(UpdateGroupCommand request, CancellationToken ct)
    {
        var gr = await repository.GetByIdAsync(request.Id, ct);
        if (gr is null || gr.SchoolId != user.SchoolId) throw new NotFoundException($"Group {request.Id} not found");
        if (request.CourseLevel.HasValue) gr.CourseLevel = request.CourseLevel.Value;
        if (request.GroupLabel is not null) gr.GroupLabel = request.GroupLabel;
        if (request.StudentCount.HasValue) gr.StudentCount = request.StudentCount.Value;
        gr.TutorId = request.TutorId;
        gr.HomeClassroomId = request.HomeClassroomId;
        if (request.SubjectHours is not null)
        {
            db.GroupSubjectHours.RemoveRange(gr.SubjectHoursList);
            gr.SubjectHoursList = request.SubjectHours.Select(kv => new GroupSubjectHour
            {
                GroupId = gr.Id,
                SubjectKey = kv.Key.ToLower().Trim(),
                Hours = kv.Value
            }).ToList();
        }
        await repository.SaveChangesAsync(ct);
        var tutorNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == user.SchoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);
        var stageType = await db.SchoolStages.AsNoTracking()
            .Where(s => s.Id == gr.StageId)
            .Select(s => s.StageType)
            .FirstOrDefaultAsync(ct) ?? StageTypes.Primaria;
        return GroupMapping.ToDto(gr, tutorNames, cycleResolver.ResolveCycle(stageType, gr.CourseLevel));
    }
}

public sealed class DeleteGroupHandler(IGroupRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteGroupCommand>
{
    public async Task Handle(DeleteGroupCommand request, CancellationToken ct)
    {
        var gr = await repository.GetByIdAsync(request.Id, ct);
        if (gr is null || gr.SchoolId != user.SchoolId) throw new NotFoundException($"Group {request.Id} not found");
        await repository.DeleteAsync(gr, ct);
        await repository.SaveChangesAsync(ct);
    }
}
