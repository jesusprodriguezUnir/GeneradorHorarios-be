namespace HorariosEscolares.Features.Auth;

public sealed class AdminOnlyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.GetCurrentUserOrFail();
        if (!user.IsAdmin)
        {
            return Results.StatusCode(403);
        }

        return await next(context);
    }
}

public static class AdminOnlyFilterExtensions
{
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<AdminOnlyFilter>();
}
