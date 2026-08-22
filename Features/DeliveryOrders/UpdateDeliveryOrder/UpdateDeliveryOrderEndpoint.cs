using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.DeliveryOrders.UpdateDeliveryOrder;

public sealed record UpdateDeliveryOrderRequest(
    Guid? OrderId,
    Guid? WarehouseId,
    string? GhtkId,
    DeliveryOrderStatus Status,
    decimal TotalAmount,
    decimal ShippingFee,
    decimal Weight,
    string? Note
);

public sealed class UpdateDeliveryOrderValidator : AbstractValidator<UpdateDeliveryOrderRequest>
{
    public UpdateDeliveryOrderValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid delivery order status.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Total amount must be greater than or equal to 0.");

        RuleFor(x => x.ShippingFee)
            .GreaterThanOrEqualTo(0).WithMessage("Shipping fee must be greater than or equal to 0.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0).WithMessage("Weight must be greater than or equal to 0.");

        RuleFor(x => x.GhtkId)
            .MaximumLength(100).WithMessage("GhtkId must not exceed 100 characters.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

public static class UpdateDeliveryOrderEndpoint
{
    public static RouteGroupBuilder MapUpdateDeliveryOrder(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateDeliveryOrder")
            .WithSummary("Updates an existing delivery order and logs status changes to history if status is modified")
            .WithRequestValidation<UpdateDeliveryOrderRequest>()
            .Produces<DeliveryOrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateDeliveryOrderRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .Include(d => d.Warehouse)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (deliveryOrder is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Delivery order with ID '{id}' was not found."
            );
        }

        // 1. Kiểm tra tồn tại Order nếu có cập nhật
        if (request.OrderId.HasValue && request.OrderId != deliveryOrder.OrderId)
        {
            var orderExists = await db.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == request.OrderId.Value, cancellationToken);

            if (!orderExists)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Order with ID '{request.OrderId.Value}' does not exist."
                );
            }
        }

        // 2. Kiểm tra tồn tại Warehouse nếu có cập nhật
        string? warehouseName = deliveryOrder.Warehouse?.Name;
        if (request.WarehouseId.HasValue && request.WarehouseId != deliveryOrder.WarehouseId)
        {
            var warehouse = await db.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value, cancellationToken);

            if (warehouse is null)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Warehouse with ID '{request.WarehouseId.Value}' does not exist."
                );
            }

            warehouseName = warehouse.Name;
        }
        else if (!request.WarehouseId.HasValue)
        {
            warehouseName = null;
        }

        // 3. Nếu trạng thái thay đổi -> Ghi nhận vào lịch sử
        if (deliveryOrder.Status != request.Status)
        {
            var statusHistory = new DeliveryOrderStatusHistory
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = deliveryOrder.Id,
                Status = request.Status,
                Note = $"Cập nhật trạng thái từ '{CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(deliveryOrder.Status)}' sang '{CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(request.Status)}'",
                CreatedAtUtc = DateTime.UtcNow
            };

            await db.DeliveryOrderStatusHistories.AddAsync(statusHistory, cancellationToken);
        }

        // 4. Cập nhật thông tin
        deliveryOrder.OrderId = request.OrderId;
        deliveryOrder.WarehouseId = request.WarehouseId;
        deliveryOrder.GhtkId = request.GhtkId?.Trim();
        deliveryOrder.Status = request.Status;
        deliveryOrder.TotalAmount = request.TotalAmount;
        deliveryOrder.ShippingFee = request.ShippingFee;
        deliveryOrder.Weight = request.Weight;
        deliveryOrder.Note = request.Note?.Trim();
        deliveryOrder.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var response = new DeliveryOrderDto(
            Id: deliveryOrder.Id,
            OrderId: deliveryOrder.OrderId,
            WarehouseId: deliveryOrder.WarehouseId,
            WarehouseName: warehouseName,
            GhtkId: deliveryOrder.GhtkId,
            Status: deliveryOrder.Status,
            StatusName: CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(deliveryOrder.Status),
            TotalAmount: deliveryOrder.TotalAmount,
            ShippingFee: deliveryOrder.ShippingFee,
            Weight: deliveryOrder.Weight,
            Note: deliveryOrder.Note,
            EstimatedPickTime: deliveryOrder.EstimatedPickTime,
            EstimatedDeliverTime: deliveryOrder.EstimatedDeliverTime,
            CreatedAtUtc: deliveryOrder.CreatedAtUtc,
            UpdatedAtUtc: deliveryOrder.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
