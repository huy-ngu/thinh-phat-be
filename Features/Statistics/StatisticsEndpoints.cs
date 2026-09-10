using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Statistics.GetOverview;

namespace ThinhPhat.Api.Features.Statistics;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/statistics")
            .WithTags("Statistics")
            .RequireAuthorization("AdminOnly");

        group.MapGetOverview();

        return app;
    }
}
