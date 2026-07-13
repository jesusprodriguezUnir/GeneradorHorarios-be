using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using HorariosEscolares.Application.Common.Behaviors;
using HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

namespace HorariosEscolares.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IScheduleGenerationOrchestrator, GenerateScheduleOrchestrator>();
        services.AddScoped<ScheduleResultPersister>();

        return services;
    }
}
