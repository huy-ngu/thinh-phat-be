using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.Warehouses.DeleteWarehouse;

public static class DeleteWarehouseEndpoint
{
    public static RouteGroupBuilder MapDeleteWarehouse(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync)
            .WithName("DeleteWarehouse")
            .WithSummary("Deletes a warehouse by its ID (Admin only)")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Warehouse with ID '{id}' was not found."
            );
        }

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
