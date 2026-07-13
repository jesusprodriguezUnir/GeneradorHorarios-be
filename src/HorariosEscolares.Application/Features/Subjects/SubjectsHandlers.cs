using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Subjects;

namespace HorariosEscolares.Application.Features.Subjects;

public record SubjectDto(
    Guid Id, string SubjectName, string SubjectShort, string SubjectKey,
    int WeeklyHoursMin, int WeeklyHoursMax, int WeeklyHoursDefault,
    bool RequiresSpecialist, string? RequiredClassroomType,
    int MaxConsecutiveSlots, bool SplittableAcrossDays, bool IsOfficial,
    int? Cycle, int? CourseLevel);

public record UpdateSubjectHoursRequest(int WeeklyHoursDefault);

public record GetAllSubjectsQuery : IRequest<List<SubjectDto>>;
public record CloneOfficialCurriculumCommand : IRequest<List<SubjectDto>>;
public record UpdateSubjectHoursCommand(Guid Id, int WeeklyHoursDefault) : IRequest<SubjectDto>;
public record CreateSubjectCommand(
    string SubjectName, string SubjectShort, string SubjectKey,
    int WeeklyHoursMin, int WeeklyHoursMax, int WeeklyHoursDefault,
    bool RequiresSpecialist, string? RequiredClassroomType,
    int MaxConsecutiveSlots, bool SplittableAcrossDays,
    int? Cycle, int? CourseLevel) : IRequest<SubjectDto>;
public record UpdateSubjectCommand(
    Guid Id, string? SubjectName, string? SubjectShort, string? SubjectKey,
    int? WeeklyHoursMin, int? WeeklyHoursMax, int? WeeklyHoursDefault,
    bool? RequiresSpecialist, string? RequiredClassroomType,
    int? MaxConsecutiveSlots, bool? SplittableAcrossDays,
    int? Cycle, int? CourseLevel) : IRequest<SubjectDto>;
public record DeleteSubjectCommand(Guid Id) : IRequest;

public sealed class GetAllSubjectsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllSubjectsQuery, List<SubjectDto>>
{
    public async Task<List<SubjectDto>> Handle(GetAllSubjectsQuery request, CancellationToken ct)
    {
        var customTemplate = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchoolId == user.SchoolId, ct);
        var templateId = customTemplate?.Id;
        if (templateId == null)
        {
            var officialTemplate = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsOfficial, ct);
            templateId = officialTemplate?.Id;
        }
        if (templateId == null) return [];

        var allocations = await db.SubjectAllocations.AsNoTracking()
            .Where(a => a.TemplateId == templateId)
            .ToListAsync(ct);

        return allocations.Select(a => new SubjectDto(
            a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
            a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
            a.RequiresSpecialist, a.RequiredClassroomType,
            a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial,
            a.Cycle, a.CourseLevel)).ToList();
    }
}

public sealed class CloneOfficialCurriculumHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CloneOfficialCurriculumCommand, List<SubjectDto>>
{
    public async Task<List<SubjectDto>> Handle(CloneOfficialCurriculumCommand request, CancellationToken ct)
    {
        var exists = await db.CurriculumTemplates.AsNoTracking()
            .AnyAsync(t => t.SchoolId == user.SchoolId, ct);
        if (exists)
            throw new InvalidOperationException("El colegio ya tiene una plantilla configurada.");

        var officialTemplate = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsOfficial, ct)
            ?? throw new NotFoundException("Plantilla LOMLOE oficial no encontrada.");

        var schoolName = await db.Schools.Where(s => s.Id == user.SchoolId)
            .Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "Colegio";

        var newTemplate = new CurriculumTemplate
        {
            SchoolId = user.SchoolId,
            Name = $"Plantilla de {schoolName}",
            Region = officialTemplate.Region,
            Stage = officialTemplate.Stage,
            IsOfficial = false
        };
        db.CurriculumTemplates.Add(newTemplate);

        var officialAllocations = await db.SubjectAllocations.AsNoTracking()
            .Where(a => a.TemplateId == officialTemplate.Id)
            .ToListAsync(ct);

        var clonedAllocations = officialAllocations.Select(a => new SubjectAllocation
        {
            TemplateId = newTemplate.Id,
            SubjectName = a.SubjectName, SubjectShort = a.SubjectShort, SubjectKey = a.SubjectKey,
            WeeklyHoursMin = a.WeeklyHoursMin, WeeklyHoursMax = a.WeeklyHoursMax, WeeklyHoursDefault = a.WeeklyHoursDefault,
            RequiresSpecialist = a.RequiresSpecialist, RequiredClassroomType = a.RequiredClassroomType,
            MaxConsecutiveSlots = a.MaxConsecutiveSlots, SplittableAcrossDays = a.SplittableAcrossDays,
            Cycle = a.Cycle, CourseLevel = a.CourseLevel,
            IsOfficial = false
        }).ToList();

        db.SubjectAllocations.AddRange(clonedAllocations);
        await db.SaveChangesAsync(ct);

        return clonedAllocations.Select(a => new SubjectDto(
            a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
            a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
            a.RequiresSpecialist, a.RequiredClassroomType,
            a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial,
            a.Cycle, a.CourseLevel)).ToList();
    }
}

public sealed class UpdateSubjectHoursHandler(IAppDbContext db, ISubjectRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateSubjectHoursCommand, SubjectDto>
{
    public async Task<SubjectDto> Handle(UpdateSubjectHoursCommand request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.Id, ct);
        if (a is null) throw new NotFoundException($"Subject {request.Id} not found");

        var template = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == a.TemplateId, ct);
        if (template is null)
            throw new InvalidOperationException("La plantilla asociada no existe.");
        if (template.IsOfficial)
            throw new InvalidOperationException("No se puede modificar la plantilla LOMLOE oficial. Primero debes clonarla para tu centro.");
        if (template.SchoolId != user.SchoolId)
            throw new UnauthorizedAccessException();

        if (request.WeeklyHoursDefault < a.WeeklyHoursMin || request.WeeklyHoursDefault > a.WeeklyHoursMax)
            throw new InvalidOperationException($"Las horas deben estar entre {a.WeeklyHoursMin} y {a.WeeklyHoursMax} según la normativa LOMLOE Madrid.");

        a.WeeklyHoursDefault = request.WeeklyHoursDefault;
        await repository.SaveChangesAsync(ct);
        return new SubjectDto(a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
            a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
            a.RequiresSpecialist, a.RequiredClassroomType,
            a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial,
            a.Cycle, a.CourseLevel);
    }
}

public sealed class CreateSubjectHandler(IAppDbContext db, ISubjectRepository repository, ICurrentUser user)
    : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        var customTemplate = await db.CurriculumTemplates
            .FirstOrDefaultAsync(t => t.SchoolId == user.SchoolId && !t.IsOfficial, ct);
        if (customTemplate is null)
            throw new InvalidOperationException("Debes personalizar (clonar) el currículo LOMLOE antes de añadir asignaturas.");

        var s = new SubjectAllocation
        {
            TemplateId = customTemplate.Id,
            SubjectName = request.SubjectName, SubjectShort = request.SubjectShort,
            SubjectKey = request.SubjectKey.ToLower().Trim(),
            WeeklyHoursMin = request.WeeklyHoursMin, WeeklyHoursMax = request.WeeklyHoursMax,
            WeeklyHoursDefault = request.WeeklyHoursDefault,
            RequiresSpecialist = request.RequiresSpecialist,
            RequiredClassroomType = request.RequiredClassroomType,
            MaxConsecutiveSlots = request.MaxConsecutiveSlots,
            SplittableAcrossDays = request.SplittableAcrossDays,
            Cycle = request.Cycle,
            CourseLevel = request.CourseLevel,
            IsOfficial = false
        };
        await repository.AddAsync(s, ct);
        await repository.SaveChangesAsync(ct);
        return new SubjectDto(s.Id, s.SubjectName, s.SubjectShort, s.SubjectKey,
            s.WeeklyHoursMin, s.WeeklyHoursMax, s.WeeklyHoursDefault,
            s.RequiresSpecialist, s.RequiredClassroomType,
            s.MaxConsecutiveSlots, s.SplittableAcrossDays, s.IsOfficial,
            s.Cycle, s.CourseLevel);
    }
}

public sealed class UpdateSubjectHandler(IAppDbContext db, ISubjectRepository repository, ICurrentUser user)
    : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    public async Task<SubjectDto> Handle(UpdateSubjectCommand request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.Id, ct);
        if (a is null) throw new NotFoundException($"Subject {request.Id} not found");

        var template = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == a.TemplateId, ct);
        if (template is null || template.IsOfficial || template.SchoolId != user.SchoolId)
            throw new UnauthorizedAccessException();

        if (request.SubjectName is not null) a.SubjectName = request.SubjectName;
        if (request.SubjectShort is not null) a.SubjectShort = request.SubjectShort;
        if (request.SubjectKey is not null) a.SubjectKey = request.SubjectKey.ToLower().Trim();
        if (request.WeeklyHoursMin.HasValue) a.WeeklyHoursMin = request.WeeklyHoursMin.Value;
        if (request.WeeklyHoursMax.HasValue) a.WeeklyHoursMax = request.WeeklyHoursMax.Value;
        if (request.WeeklyHoursDefault.HasValue) a.WeeklyHoursDefault = request.WeeklyHoursDefault.Value;
        if (request.RequiresSpecialist.HasValue) a.RequiresSpecialist = request.RequiresSpecialist.Value;
        a.RequiredClassroomType = request.RequiredClassroomType;
        if (request.MaxConsecutiveSlots.HasValue) a.MaxConsecutiveSlots = request.MaxConsecutiveSlots.Value;
        if (request.SplittableAcrossDays.HasValue) a.SplittableAcrossDays = request.SplittableAcrossDays.Value;
        a.Cycle = request.Cycle;
        a.CourseLevel = request.CourseLevel;
        await repository.SaveChangesAsync(ct);

        return new SubjectDto(a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
            a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
            a.RequiresSpecialist, a.RequiredClassroomType,
            a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial,
            a.Cycle, a.CourseLevel);
    }
}

public sealed class DeleteSubjectHandler(IAppDbContext db, ISubjectRepository repository, ICurrentUser user)
    : IRequestHandler<DeleteSubjectCommand>
{
    public async Task Handle(DeleteSubjectCommand request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.Id, ct);
        if (a is null) throw new NotFoundException($"Subject {request.Id} not found");

        var template = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == a.TemplateId, ct);
        if (template is null || template.IsOfficial || template.SchoolId != user.SchoolId)
            throw new UnauthorizedAccessException();

        await repository.DeleteAsync(a, ct);
        await repository.SaveChangesAsync(ct);
    }
}
