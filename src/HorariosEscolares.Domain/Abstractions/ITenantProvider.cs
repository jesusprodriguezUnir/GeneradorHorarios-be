namespace HorariosEscolares.Domain.Abstractions;

/// <summary>
/// SchoolId del tenant de la petición actual. Devuelve null cuando no hay
/// contexto de usuario (seed inicial, jobs de Hangfire, middleware de auth),
/// en cuyo caso los query filters globales quedan desactivados.
/// </summary>
public interface ITenantProvider
{
    Guid? SchoolId { get; }
}
