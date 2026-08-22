using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.DeliveryOrderStatusHistories.DeleteDeliveryOrderStatusHistory;

public static class DeleteDeliveryOrderStatusHistoryEndpoint
{
    public static RouteGroupBuilder MapDeleteDeliveryOrderStatusHistory(this RouteGroupBuilder group)
    {
        group.MapDelete("/{deliveryOrderId:guid}/histories/{id:guid}", HandleAsync)
            .WithName("DeleteDeliveryOrderStatusHistory")
            .WithSummary("Deletes a specific status history record")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid deliveryOrderId,
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var history = await db.DeliveryOrderStatusHistories
            .FirstOrDefaultAsync(h => h.Id == id && h.DeliveryOrderId == deliveryOrderId, cancellationToken);

        if (history is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Status history with ID '{id}' was not found for delivery order '{deliveryOrderId}'."
            );
        }

        db.DeliveryOrderStatusHistories.Remove(history);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
