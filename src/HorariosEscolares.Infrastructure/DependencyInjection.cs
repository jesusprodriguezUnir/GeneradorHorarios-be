using Microsoft.Extensions.DependencyInjection;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Normative;

namespace HorariosEscolares.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IScheduleEngine, BacktrackingScheduleEngine>();
        services.AddScoped<INormativeValidator, NormativeValidator>();
        return services;
    }
}
