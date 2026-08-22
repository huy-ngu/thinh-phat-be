using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.DeliveryOrders.DeleteDeliveryOrder;

public static class DeleteDeliveryOrderEndpoint
{
    public static RouteGroupBuilder MapDeleteDeliveryOrder(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync)
            .WithName("DeleteDeliveryOrder")
            .WithSummary("Deletes a delivery order and cascades delete to its status histories (Admin only)")
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
        var deliveryOrder = await db.DeliveryOrders.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (deliveryOrder is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Delivery order with ID '{id}' was not found."
            );
        }

        db.DeliveryOrders.Remove(deliveryOrder);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
