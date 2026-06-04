using Microsoft.Extensions.DependencyInjection;
using HorariosEscolares.Domain.Assignments;
using HorariosEscolares.Domain.Classrooms;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Groups;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Subjects;
using HorariosEscolares.Domain.Teachers;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Normative;
using HorariosEscolares.Infrastructure.Persistence.Repositories;

namespace HorariosEscolares.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IScheduleEngine, BacktrackingScheduleEngine>();
        services.AddScoped<INormativeValidator, NormativeValidator>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();

        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IClassroomRepository, ClassroomRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IConstraintRepository, ConstraintRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<ICycleScheduleRepository, CycleScheduleRepository>();

        return services;
    }
}
