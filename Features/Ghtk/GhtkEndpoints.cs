using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Ghtk.CreateGhtkOrder;
using ThinhPhat.Api.Features.Ghtk.GhtkWebhook;

namespace ThinhPhat.Api.Features.Ghtk;

public static class GhtkEndpoints
{
    public static IEndpointRouteBuilder MapGhtkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ghtk")
            .WithTags("GHTK")
            .RequireAuthorization();

        // 1. Authenticated API Endpoints
        group.MapCreateGhtkOrder();

        // 2. Webhook Endpoints (AllowAnonymous & DisableAntiforgery)
        group.MapGhtkWebhook();

        return app;
    }
}
