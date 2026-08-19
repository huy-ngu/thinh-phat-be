using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.Orders.DeleteOrder;

public static class DeleteOrderEndpoint
{
    public static RouteGroupBuilder MapDeleteOrder(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync)
            .WithName("DeleteOrder")
            .WithSummary("Deletes a closing order by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var affectedRows = await db.Orders
            .Where(o => o.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affectedRows == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Order with ID '{id}' was not found."
            );
        }

        return TypedResults.NoContent();
    }
}
