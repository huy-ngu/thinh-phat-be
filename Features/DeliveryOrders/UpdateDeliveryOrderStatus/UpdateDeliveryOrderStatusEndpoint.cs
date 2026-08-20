using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.DeliveryOrders.UpdateDeliveryOrderStatus;

public sealed record UpdateDeliveryOrderStatusRequest(
    DeliveryOrderStatus Status,
    string? Note
);

public sealed class UpdateDeliveryOrderStatusValidator : AbstractValidator<UpdateDeliveryOrderStatusRequest>
{
    public UpdateDeliveryOrderStatusValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid delivery order status.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

public static class UpdateDeliveryOrderStatusEndpoint
{
    public static RouteGroupBuilder MapUpdateDeliveryOrderStatus(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/status", HandleAsync)
            .WithName("UpdateDeliveryOrderStatus")
            .WithSummary("Quickly updates delivery order status and appends a record to status history")
            .WithRequestValidation<UpdateDeliveryOrderStatusRequest>()
            .Produces<DeliveryOrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateDeliveryOrderStatusRequest request,
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

        var oldStatus = deliveryOrder.Status;
        deliveryOrder.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            deliveryOrder.Note = request.Note.Trim();
        }
        deliveryOrder.UpdatedAtUtc = DateTime.UtcNow;

        // Ghi nhận lịch sử trạng thái mới
        var historyNote = !string.IsNullOrWhiteSpace(request.Note)
            ? request.Note.Trim()
            : $"Đổi trạng thái từ '{CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(oldStatus)}' sang '{CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(request.Status)}'";

        var history = new DeliveryOrderStatusHistory
        {
            Id = Guid.NewGuid(),
            DeliveryOrderId = deliveryOrder.Id,
            Status = request.Status,
            Note = historyNote,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.DeliveryOrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = new DeliveryOrderDto(
            Id: deliveryOrder.Id,
            OrderId: deliveryOrder.OrderId,
            WarehouseId: deliveryOrder.WarehouseId,
            WarehouseName: deliveryOrder.Warehouse?.Name,
            GhtkId: deliveryOrder.GhtkId,
            Status: deliveryOrder.Status,
            StatusName: CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(deliveryOrder.Status),
            TotalAmount: deliveryOrder.TotalAmount,
            ShippingFee: deliveryOrder.ShippingFee,
            Weight: deliveryOrder.Weight,
            Note: deliveryOrder.Note,
            CreatedAtUtc: deliveryOrder.CreatedAtUtc,
            UpdatedAtUtc: deliveryOrder.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
