using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Orders.CreateOrder;

namespace ThinhPhat.Api.Features.Orders.GetOrderById;

public static class GetOrderByIdEndpoint
{
    public static RouteGroupBuilder MapGetOrderById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetOrderById")
            .WithSummary("Retrieves comprehensive order details by ID (Admin only)")
            .RequireAuthorization("AdminOnly")
            .Produces<OrderDto>(StatusCodes.Status200OK)
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
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Order with ID '{id}' was not found."
            );
        }

        var response = new OrderDto(
            Id: order.Id,
            CustomerId: order.CustomerId,
            CustomerName: order.Customer?.Name ?? string.Empty,
            CustomerPhone: order.Customer?.PhoneNumber ?? string.Empty,
            WarehouseId: order.WarehouseId,
            WarehouseName: order.Warehouse?.Name,
            Province: order.Province,
            District: order.District,
            Ward: order.Ward,
            Address: order.Address,
            TotalAmount: order.TotalAmount,
            ShippingFee: order.ShippingFee,
            Weight: order.Weight,
            Status: order.Status,
            StatusName: CreateOrderEndpoint.GetStatusDisplayName(order.Status),
            Note: order.Note,
            GhtkId: order.GhtkId,
            EstimatedPickTime: order.EstimatedPickTime,
            EstimatedDeliverTime: order.EstimatedDeliverTime,
            Items: order.Items.Select(i => new OrderItemDto(
                Id: i.Id,
                ProductId: i.ProductId,
                ProductName: i.Product?.Name ?? string.Empty,
                ProductPrice: i.Product?.Price ?? 0m,
                ProductImageUrl: i.Product?.ImageUrl ?? string.Empty,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice,
                TotalPrice: i.TotalPrice
            )).ToList(),
            StatusHistories: order.StatusHistories
                .OrderByDescending(h => h.CreatedAtUtc)
                .Select(h => new OrderStatusHistoryDto(
                    Id: h.Id,
                    Status: h.Status,
                    StatusName: CreateOrderEndpoint.GetStatusDisplayName(h.Status),
                    Note: h.Note,
                    CreatedAtUtc: h.CreatedAtUtc
                )).ToList(),
            CreatedAtUtc: order.CreatedAtUtc,
            UpdatedAtUtc: order.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
