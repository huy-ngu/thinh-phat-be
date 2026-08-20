using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Warehouses.CreateWarehouse;
using ThinhPhat.Api.Features.Warehouses.DeleteWarehouse;
using ThinhPhat.Api.Features.Warehouses.GetWarehouseById;
using ThinhPhat.Api.Features.Warehouses.GetWarehouses;
using ThinhPhat.Api.Features.Warehouses.UpdateWarehouse;

namespace ThinhPhat.Api.Features.Warehouses;

public static class WarehouseEndpoints
{
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/warehouses")
            .WithTags("Warehouses")
            .RequireAuthorization();

        group.MapCreateWarehouse();
        group.MapGetWarehouses();
        group.MapGetWarehouseById();
        group.MapUpdateWarehouse();
        group.MapDeleteWarehouse();

        return app;
    }
}
