using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;

// 1. DTOs
public sealed record CreateDeliveryOrderRequest(
    Guid? OrderId,
    Guid? WarehouseId,
    decimal TotalAmount = 0m,
    decimal Weight = 0m,
    string? Note = null
);

public sealed record DeliveryOrderDto(
    Guid Id,
    Guid? OrderId,
    Guid? WarehouseId,
    string? WarehouseName,
    string? GhtkId,
    DeliveryOrderStatus Status,
    string StatusName,
    decimal TotalAmount,
    decimal ShippingFee,
    decimal Weight,
    string? Note,
    string? EstimatedPickTime,
    string? EstimatedDeliverTime,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public sealed record DeliveryOrderStatusHistoryDto(
    Guid Id,
    Guid DeliveryOrderId,
    DeliveryOrderStatus Status,
    string StatusName,
    string? Note,
    DateTime CreatedAtUtc
);

// 2. Request Validator
public sealed class CreateDeliveryOrderValidator : AbstractValidator<CreateDeliveryOrderRequest>
{
    public CreateDeliveryOrderValidator()
    {
        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Total amount must be greater than or equal to 0.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0).WithMessage("Weight must be greater than or equal to 0.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateDeliveryOrderEndpoint
{
    public static RouteGroupBuilder MapCreateDeliveryOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateDeliveryOrder")
            .WithSummary("Creates a new delivery order with initial status history")
            .WithRequestValidation<CreateDeliveryOrderRequest>()
            .Produces<DeliveryOrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        CreateDeliveryOrderRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tồn tại Order nếu có truyền OrderId
        if (request.OrderId.HasValue)
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

        // 2. Kiểm tra tồn tại Warehouse nếu có truyền WarehouseId
        string? warehouseName = null;
        if (request.WarehouseId.HasValue)
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

        // 3. Tạo DeliveryOrder với giá trị mặc định: GhtkId = null, ShippingFee = 0, Estimated times = null
        var deliveryOrder = new DeliveryOrder
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            WarehouseId = request.WarehouseId,
            GhtkId = null,
            Status = DeliveryOrderStatus.Draft,
            TotalAmount = request.TotalAmount,
            ShippingFee = 0m,
            Weight = request.Weight,
            Note = request.Note?.Trim(),
            EstimatedPickTime = null,
            EstimatedDeliverTime = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 4. Tự động ghi nhận lịch sử trạng thái ban đầu
        var initialHistory = new DeliveryOrderStatusHistory
        {
            Id = Guid.NewGuid(),
            DeliveryOrderId = deliveryOrder.Id,
            Status = deliveryOrder.Status,
            Note = "Khởi tạo đơn giao hàng",
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.DeliveryOrders.AddAsync(deliveryOrder, cancellationToken);
        await db.DeliveryOrderStatusHistories.AddAsync(initialHistory, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = new DeliveryOrderDto(
            Id: deliveryOrder.Id,
            OrderId: deliveryOrder.OrderId,
            WarehouseId: deliveryOrder.WarehouseId,
            WarehouseName: warehouseName,
            GhtkId: deliveryOrder.GhtkId,
            Status: deliveryOrder.Status,
            StatusName: GetDeliveryStatusDisplayName(deliveryOrder.Status),
            TotalAmount: deliveryOrder.TotalAmount,
            ShippingFee: deliveryOrder.ShippingFee,
            Weight: deliveryOrder.Weight,
            Note: deliveryOrder.Note,
            EstimatedPickTime: deliveryOrder.EstimatedPickTime,
            EstimatedDeliverTime: deliveryOrder.EstimatedDeliverTime,
            CreatedAtUtc: deliveryOrder.CreatedAtUtc,
            UpdatedAtUtc: deliveryOrder.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/delivery-orders/{deliveryOrder.Id}", response);
    }

    public static string GetDeliveryStatusDisplayName(DeliveryOrderStatus status) => status switch
    {
        DeliveryOrderStatus.Draft => "Nháp",
        DeliveryOrderStatus.Cancelled => "Hủy đơn hàng",
        DeliveryOrderStatus.Pending => "Chưa tiếp nhận",
        DeliveryOrderStatus.Accepted => "Đã tiếp nhận",
        DeliveryOrderStatus.PickedOrStocked => "Đã lấy hàng/Đã nhập kho",
        DeliveryOrderStatus.Delivering => "Đã điều phối giao hàng/Đang giao hàng",
        DeliveryOrderStatus.DeliveredUnreconciled => "Đã giao hàng/Chưa đối soát",
        DeliveryOrderStatus.Reconciled => "Đã đối soát",
        DeliveryOrderStatus.CannotPick => "Không lấy được hàng",
        DeliveryOrderStatus.DelayPick => "Hoãn lấy hàng",
        DeliveryOrderStatus.CannotDeliver => "Không giao được hàng",
        DeliveryOrderStatus.DelayDelivery => "Delay giao hàng",
        DeliveryOrderStatus.ReturnReconciled => "Đã đối soát công nợ trả hàng",
        DeliveryOrderStatus.Picking => "Đã điều phối lấy hàng/Đang lấy hàng",
        DeliveryOrderStatus.Compensated => "Đơn hàng bồi hoàn",
        DeliveryOrderStatus.Returning => "Đang trả hàng (COD cầm hàng đi trả)",
        DeliveryOrderStatus.Returned => "Đã trả hàng (COD đã trả xong hàng)",
        DeliveryOrderStatus.ShipperReportedPicked => "Shipper báo đã lấy hàng",
        DeliveryOrderStatus.ShipperReportedCannotPick => "Shipper báo không lấy được hàng",
        DeliveryOrderStatus.ShipperReportedDelayPick => "Shipper báo delay lấy hàng",
        DeliveryOrderStatus.ShipperReportedDelivered => "Shipper báo đã giao hàng",
        DeliveryOrderStatus.ShipperReportedCannotDeliver => "Shipper báo không giao được hàng",
        DeliveryOrderStatus.ShipperReportedDelayDelivery => "Shipper báo delay giao hàng",
        _ => status.ToString()
    };
}
