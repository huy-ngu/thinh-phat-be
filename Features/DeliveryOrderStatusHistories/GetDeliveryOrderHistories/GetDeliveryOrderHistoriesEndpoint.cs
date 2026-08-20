using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;

namespace ThinhPhat.Api.Features.DeliveryOrderStatusHistories.GetDeliveryOrderHistories;

public static class GetDeliveryOrderHistoriesEndpoint
{
    public static RouteGroupBuilder MapGetDeliveryOrderHistories(this RouteGroupBuilder group)
    {
        group.MapGet("/{deliveryOrderId:guid}/histories", HandleAsync)
            .WithName("GetDeliveryOrderHistories")
            .WithSummary("Retrieves the complete status change history timeline for a delivery order")
            .Produces<List<DeliveryOrderStatusHistoryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid deliveryOrderId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var deliveryOrderExists = await db.DeliveryOrders
            .AsNoTracking()
            .AnyAsync(d => d.Id == deliveryOrderId, cancellationToken);

        if (!deliveryOrderExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Delivery order with ID '{deliveryOrderId}' was not found."
            );
        }

        var histories = await db.DeliveryOrderStatusHistories
            .AsNoTracking()
            .Where(h => h.DeliveryOrderId == deliveryOrderId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .Select(h => new DeliveryOrderStatusHistoryDto(
                h.Id,
                h.DeliveryOrderId,
                h.Status,
                CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(h.Status),
                h.Note,
                h.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(histories);
    }
}
