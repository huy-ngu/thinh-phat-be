using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Warehouses.CreateWarehouse;

namespace ThinhPhat.Api.Features.Warehouses.GetWarehouseById;

public static class GetWarehouseByIdEndpoint
{
    public static RouteGroupBuilder MapGetWarehouseById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetWarehouseById")
            .WithSummary("Retrieves a warehouse by its ID")
            .Produces<WarehouseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Name,
                w.Province,
                w.District,
                w.Ward,
                w.Address,
                w.PhoneNumber,
                w.CreatedAtUtc,
                w.UpdatedAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Warehouse with ID '{id}' was not found."
            );
        }

        return TypedResults.Ok(warehouse);
    }
}
