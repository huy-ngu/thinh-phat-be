using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Sync.SyncGoogleSheet;

namespace ThinhPhat.Api.Features.Sync;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sync")
            .WithTags("Sync");

        group.MapSyncGoogleSheet();

        return app;
    }
}
