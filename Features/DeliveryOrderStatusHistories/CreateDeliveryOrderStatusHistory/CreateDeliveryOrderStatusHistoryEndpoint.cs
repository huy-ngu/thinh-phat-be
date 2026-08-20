using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.DeliveryOrderStatusHistories.CreateDeliveryOrderStatusHistory;

public sealed record CreateDeliveryOrderStatusHistoryRequest(
    DeliveryOrderStatus Status,
    string? Note
);

public sealed class CreateDeliveryOrderStatusHistoryValidator : AbstractValidator<CreateDeliveryOrderStatusHistoryRequest>
{
    public CreateDeliveryOrderStatusHistoryValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid delivery order status.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

public static class CreateDeliveryOrderStatusHistoryEndpoint
{
    public static RouteGroupBuilder MapCreateDeliveryOrderStatusHistory(this RouteGroupBuilder group)
    {
        group.MapPost("/{deliveryOrderId:guid}/histories", HandleAsync)
            .WithName("CreateDeliveryOrderStatusHistory")
            .WithSummary("Creates a new status history record and automatically syncs the delivery order status")
            .WithRequestValidation<CreateDeliveryOrderStatusHistoryRequest>()
            .Produces<DeliveryOrderStatusHistoryDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid deliveryOrderId,
        CreateDeliveryOrderStatusHistoryRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .FirstOrDefaultAsync(d => d.Id == deliveryOrderId, cancellationToken);

        if (deliveryOrder is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Delivery order with ID '{deliveryOrderId}' was not found."
            );
        }

        // Tạo bản ghi lịch sử mới
        var history = new DeliveryOrderStatusHistory
        {
            Id = Guid.NewGuid(),
            DeliveryOrderId = deliveryOrderId,
            Status = request.Status,
            Note = request.Note?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Đồng bộ trạng thái mới sang DeliveryOrder
        deliveryOrder.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            deliveryOrder.Note = request.Note.Trim();
        }
        deliveryOrder.UpdatedAtUtc = DateTime.UtcNow;

        await db.DeliveryOrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = new DeliveryOrderStatusHistoryDto(
            Id: history.Id,
            DeliveryOrderId: history.DeliveryOrderId,
            Status: history.Status,
            StatusName: CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(history.Status),
            Note: history.Note,
            CreatedAtUtc: history.CreatedAtUtc
        );

        return TypedResults.Created($"/api/v1/delivery-orders/{deliveryOrderId}/histories/{history.Id}", response);
    }
}
