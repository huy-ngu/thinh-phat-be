using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.Customers.DeleteCustomer;

public static class DeleteCustomerEndpoint
{
    public static RouteGroupBuilder MapDeleteCustomer(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync)
            .WithName("DeleteCustomer")
            .WithSummary("Deletes a customer and all associated addresses (Admin only)")
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
        // High-performance EF Core ExecuteDeleteAsync (Single SQL query, Zero Change Tracker allocations)
        var affectedRows = await db.Customers
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affectedRows == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Customer with ID '{id}' was not found."
            );
        }

        return TypedResults.NoContent();
    }
}
