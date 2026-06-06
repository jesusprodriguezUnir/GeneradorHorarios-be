using MediatR;
using HorariosEscolares.Application.Features.Roles;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Roles;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/roles").WithTags("Roles");

        g.MapGet("/", async (HttpContext ctx, ISender sender) =>
        {
            _ = ctx.GetCurrentUserOrFail();
            var result = await sender.Send(new GetRolesQuery());
            return Results.Ok(result);
        });

        return app;
    }
}
