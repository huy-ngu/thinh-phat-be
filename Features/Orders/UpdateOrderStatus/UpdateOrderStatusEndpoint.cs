using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Orders.UpdateOrderStatus;

public sealed record UpdateOrderStatusRequest(
    OrderStatus Status,
    string? Note
);

public sealed class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

public static class UpdateOrderStatusEndpoint
{
    public static RouteGroupBuilder MapUpdateOrderStatus(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/status", HandleAsync)
            .WithName("UpdateOrderStatus")
            .WithSummary("Quickly updates order status with audit timeline logging (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<UpdateOrderStatusRequest>()
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
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

        var statusDisplayName = CreateOrderEndpoint.GetStatusDisplayName(request.Status);
        var historyNote = string.IsNullOrWhiteSpace(request.Note)
            ? $"Chuyển trạng thái: {statusDisplayName}"
            : $"Chuyển trạng thái: {statusDisplayName} | Ghi chú: {request.Note.Trim()}";

        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = request.Status,
            Note = historyNote,
            CreatedAtUtc = DateTime.UtcNow
        };

        order.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            order.Note = request.Note.Trim();
        }
        order.UpdatedAtUtc = DateTime.UtcNow;

        await db.OrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var refreshedOrder = await db.Orders
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistories)
            .FirstAsync(o => o.Id == id, cancellationToken);

        var response = new OrderDto(
            Id: refreshedOrder.Id,
            CustomerId: refreshedOrder.CustomerId,
            CustomerName: refreshedOrder.Customer?.Name ?? string.Empty,
            CustomerPhone: refreshedOrder.Customer?.PhoneNumber ?? string.Empty,
            WarehouseId: refreshedOrder.WarehouseId,
            WarehouseName: refreshedOrder.Warehouse?.Name,
            Province: refreshedOrder.Province,
            District: refreshedOrder.District,
            Ward: refreshedOrder.Ward,
            Address: refreshedOrder.Address,
            TotalAmount: refreshedOrder.TotalAmount,
            ShippingFee: refreshedOrder.ShippingFee,
            Weight: refreshedOrder.Weight,
            Status: refreshedOrder.Status,
            StatusName: CreateOrderEndpoint.GetStatusDisplayName(refreshedOrder.Status),
            Note: refreshedOrder.Note,
            GhtkId: refreshedOrder.GhtkId,
            EstimatedPickTime: refreshedOrder.EstimatedPickTime,
            EstimatedDeliverTime: refreshedOrder.EstimatedDeliverTime,
            Items: refreshedOrder.Items.Select(i => new OrderItemDto(
                Id: i.Id,
                ProductId: i.ProductId,
                ProductName: i.Product?.Name ?? string.Empty,
                ProductPrice: i.Product?.Price ?? 0m,
                ProductImageUrl: i.Product?.ImageUrl ?? string.Empty,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice,
                TotalPrice: i.TotalPrice
            )).ToList(),
            StatusHistories: refreshedOrder.StatusHistories
                .OrderByDescending(h => h.CreatedAtUtc)
                .Select(h => new OrderStatusHistoryDto(
                    Id: h.Id,
                    Status: h.Status,
                    StatusName: CreateOrderEndpoint.GetStatusDisplayName(h.Status),
                    Note: h.Note,
                    CreatedAtUtc: h.CreatedAtUtc
                )).ToList(),
            CreatedAtUtc: refreshedOrder.CreatedAtUtc,
            UpdatedAtUtc: refreshedOrder.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
