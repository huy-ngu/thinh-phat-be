using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Features.Warehouses.CreateWarehouse;

namespace ThinhPhat.Api.Features.DeliveryOrders.GetDeliveryOrderById;

public sealed record DeliveryOrderDetailDto(
    Guid Id,
    Guid? OrderId,
    OrderDto? Order,
    Guid? WarehouseId,
    WarehouseDto? Warehouse,
    string? GhtkId,
    DeliveryOrderStatus Status,
    string StatusName,
    decimal TotalAmount,
    decimal ShippingFee,
    decimal Weight,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    List<DeliveryOrderStatusHistoryDto> StatusHistories
);

public static class GetDeliveryOrderByIdEndpoint
{
    public static RouteGroupBuilder MapGetDeliveryOrderById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetDeliveryOrderById")
            .WithSummary("Retrieves detailed delivery order info including warehouse, order, and status history timeline")
            .Produces<DeliveryOrderDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .AsNoTracking()
            .Include(d => d.Warehouse)
            .Include(d => d.Order)
                .ThenInclude(o => o!.Customer)
            .Include(d => d.Order)
                .ThenInclude(o => o!.Product)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (deliveryOrder is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Delivery order with ID '{id}' was not found."
            );
        }

        WarehouseDto? warehouseDto = deliveryOrder.Warehouse != null
            ? new WarehouseDto(
                deliveryOrder.Warehouse.Id,
                deliveryOrder.Warehouse.Name,
                deliveryOrder.Warehouse.Address,
                deliveryOrder.Warehouse.PhoneNumber,
                deliveryOrder.Warehouse.CreatedAtUtc,
                deliveryOrder.Warehouse.UpdatedAtUtc
            )
            : null;

        OrderDto? orderDto = deliveryOrder.Order != null
            ? new OrderDto(
                deliveryOrder.Order.Id,
                deliveryOrder.Order.ProductId,
                deliveryOrder.Order.Product?.Name ?? string.Empty,
                deliveryOrder.Order.Product?.Price ?? 0m,
                deliveryOrder.Order.Product?.ImageUrl ?? string.Empty,
                deliveryOrder.Order.CustomerId,
                deliveryOrder.Order.Customer?.Name ?? string.Empty,
                deliveryOrder.Order.Customer?.PhoneNumber ?? string.Empty,
                deliveryOrder.Order.Address,
                deliveryOrder.Order.TotalAmount,
                deliveryOrder.Order.Status,
                CreateOrderEndpoint.GetStatusDisplayName(deliveryOrder.Order.Status),
                deliveryOrder.Order.Note,
                deliveryOrder.Order.CreatedAtUtc,
                deliveryOrder.Order.UpdatedAtUtc
            )
            : null;

        var historyDtos = deliveryOrder.StatusHistories
            .OrderByDescending(h => h.CreatedAtUtc)
            .Select(h => new DeliveryOrderStatusHistoryDto(
                h.Id,
                h.DeliveryOrderId,
                h.Status,
                CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(h.Status),
                h.Note,
                h.CreatedAtUtc
            ))
            .ToList();

        var response = new DeliveryOrderDetailDto(
            Id: deliveryOrder.Id,
            OrderId: deliveryOrder.OrderId,
            Order: orderDto,
            WarehouseId: deliveryOrder.WarehouseId,
            Warehouse: warehouseDto,
            GhtkId: deliveryOrder.GhtkId,
            Status: deliveryOrder.Status,
            StatusName: CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(deliveryOrder.Status),
            TotalAmount: deliveryOrder.TotalAmount,
            ShippingFee: deliveryOrder.ShippingFee,
            Weight: deliveryOrder.Weight,
            Note: deliveryOrder.Note,
            CreatedAtUtc: deliveryOrder.CreatedAtUtc,
            UpdatedAtUtc: deliveryOrder.UpdatedAtUtc,
            StatusHistories: historyDtos
        );

        return TypedResults.Ok(response);
    }
}
