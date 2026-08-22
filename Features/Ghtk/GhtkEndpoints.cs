using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Ghtk.CreateGhtkOrder;

namespace ThinhPhat.Api.Features.Ghtk;

public static class GhtkEndpoints
{
    public static IEndpointRouteBuilder MapGhtkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ghtk")
            .WithTags("GHTK")
            .RequireAuthorization();

        group.MapCreateGhtkOrder();

        return app;
    }
}
