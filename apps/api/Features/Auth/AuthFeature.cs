using MediatR;
using HorariosEscolares.Application.Features.Auth;

namespace HorariosEscolares.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapGet("/me", async (HttpContext ctx, ISender sender) =>
        {
            if (ctx.Items["CurrentUser"] is null) return Results.Unauthorized();
            var result = await sender.Send(new GetCurrentUserQuery());
            return Results.Ok(result);
        });

        g.MapGet("/demo-users", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDemoUsersQuery());
            return Results.Ok(result);
        });

        return app;
    }
}
