using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Orders.CreateOrder;

// 1. DTOs
public sealed record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal? UnitPrice = null
);

public sealed record CreateOrderRequest(
    Guid CustomerId,
    string Province,
    string District,
    string? Ward,
    string Address,
    Guid? WarehouseId = null,
    decimal? TotalAmount = null,
    decimal? ShippingFee = null,
    decimal? Weight = null,
    OrderStatus Status = OrderStatus.Draft,
    string? Note = null,
    List<CreateOrderItemRequest>? Items = null
);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal ProductPrice,
    string ProductImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public sealed record OrderStatusHistoryDto(
    Guid Id,
    OrderStatus Status,
    string StatusName,
    string? Note,
    DateTime CreatedAtUtc
);

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid? WarehouseId,
    string? WarehouseName,
    string Province,
    string District,
    string? Ward,
    string Address,
    decimal TotalAmount,
    decimal? ShippingFee,
    decimal? Weight,
    OrderStatus Status,
    string StatusName,
    string? Note,
    string? GhtkId,
    string? EstimatedPickTime,
    string? EstimatedDeliverTime,
    List<OrderItemDto> Items,
    List<OrderStatusHistoryDto> StatusHistories,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// 2. Request Validator
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Province)
            .NotEmpty().WithMessage("Province is required.")
            .MaximumLength(100).WithMessage("Province must not exceed 100 characters.");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("District is required.")
            .MaximumLength(100).WithMessage("District must not exceed 100 characters.");

        RuleFor(x => x.Ward)
            .MaximumLength(100).WithMessage("Ward must not exceed 100 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Delivery address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).When(x => x.TotalAmount.HasValue).WithMessage("Total amount must be greater than or equal to 0.");

        RuleFor(x => x.ShippingFee)
            .GreaterThanOrEqualTo(0).When(x => x.ShippingFee.HasValue).WithMessage("Shipping fee must be greater than or equal to 0.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0).When(x => x.Weight.HasValue).WithMessage("Weight must be greater than or equal to 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");

        When(x => x.Items != null && x.Items.Count > 0, () =>
        {
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("ProductId is required for each item.");
                item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0.");
                item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).When(i => i.UnitPrice.HasValue).WithMessage("UnitPrice must not be negative.");
            });
        });
    }
}

// 3. Endpoint Slice & Handler
public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates a new order with items and initial status (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<CreateOrderRequest>()
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        CreateOrderRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Customer
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Customer with ID '{request.CustomerId}' does not exist."
            );
        }

        // 2. Kiểm tra Warehouse nếu có
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

        // 3. Xử lý Order Items & tính toán TotalAmount
        var orderId = Guid.NewGuid();
        var orderItems = new List<OrderItem>();
        var itemDtos = new List<OrderItemDto>();
        decimal calculatedItemsTotal = 0m;

        if (request.Items != null && request.Items.Count > 0)
        {
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var itemReq in request.Items)
            {
                if (!products.TryGetValue(itemReq.ProductId, out var product))
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not Found",
                        detail: $"Product with ID '{itemReq.ProductId}' does not exist."
                    );
                }

                var unitPrice = itemReq.UnitPrice ?? product.Price;
                var totalPrice = unitPrice * itemReq.Quantity;
                calculatedItemsTotal += totalPrice;

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = product.Id,
                    Quantity = itemReq.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = totalPrice
                };

                orderItems.Add(orderItem);

                itemDtos.Add(new OrderItemDto(
                    Id: orderItem.Id,
                    ProductId: product.Id,
                    ProductName: product.Name,
                    ProductPrice: product.Price,
                    ProductImageUrl: product.ImageUrl,
                    Quantity: orderItem.Quantity,
                    UnitPrice: orderItem.UnitPrice,
                    TotalPrice: orderItem.TotalPrice
                ));
            }
        }

        var finalTotalAmount = request.TotalAmount ?? (calculatedItemsTotal + (request.ShippingFee ?? 0m));

        // 4. Khởi tạo Order
        var order = new Order
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            Province = request.Province.Trim(),
            District = request.District.Trim(),
            Ward = string.IsNullOrWhiteSpace(request.Ward) ? null : request.Ward.Trim(),
            Address = request.Address.Trim(),
            TotalAmount = finalTotalAmount,
            ShippingFee = request.ShippingFee,
            Weight = request.Weight,
            Status = request.Status,
            Note = request.Note?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Items = orderItems
        };

        // 5. Khởi tạo lịch sử trạng thái ban đầu
        var initialHistory = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = order.Status,
            Note = $"Khởi tạo đơn hàng: {GetStatusDisplayName(order.Status)}",
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Orders.AddAsync(order, cancellationToken);
        await db.OrderStatusHistories.AddAsync(initialHistory, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 6. Response Mapping
        var historyDtos = new List<OrderStatusHistoryDto>
        {
            new(
                Id: initialHistory.Id,
                Status: initialHistory.Status,
                StatusName: GetStatusDisplayName(initialHistory.Status),
                Note: initialHistory.Note,
                CreatedAtUtc: initialHistory.CreatedAtUtc
            )
        };

        var response = new OrderDto(
            Id: order.Id,
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            CustomerPhone: customer.PhoneNumber,
            WarehouseId: order.WarehouseId,
            WarehouseName: warehouseName,
            Province: order.Province,
            District: order.District,
            Ward: order.Ward,
            Address: order.Address,
            TotalAmount: order.TotalAmount,
            ShippingFee: order.ShippingFee,
            Weight: order.Weight,
            Status: order.Status,
            StatusName: GetStatusDisplayName(order.Status),
            Note: order.Note,
            GhtkId: order.GhtkId,
            EstimatedPickTime: order.EstimatedPickTime,
            EstimatedDeliverTime: order.EstimatedDeliverTime,
            Items: itemDtos,
            StatusHistories: historyDtos,
            CreatedAtUtc: order.CreatedAtUtc,
            UpdatedAtUtc: order.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/orders/{order.Id}", response);
    }

    public static string GetStatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Nháp",
        OrderStatus.New => "Mới tạo",
        OrderStatus.NoAnswer => "Không nghe máy",
        OrderStatus.CallbackLater => "Hẹn gọi lại",
        OrderStatus.Cancelled => "Hủy đơn hàng",
        OrderStatus.OutOfStock => "Hết hàng",
        OrderStatus.Duplicate => "Trùng đơn",
        OrderStatus.Confirmed => "Chốt đơn (Chờ GHTK)",
        OrderStatus.Accepted => "GHTK Đã tiếp nhận",
        OrderStatus.PickedOrStocked => "Đã lấy hàng/Đã nhập kho",
        OrderStatus.Delivering => "Đang giao hàng",
        OrderStatus.DeliveredUnreconciled => "Đã giao hàng/Chưa đối soát",
        OrderStatus.Reconciled => "Đã đối soát",
        OrderStatus.CannotPick => "Không lấy được hàng",
        OrderStatus.DelayPick => "Hoãn lấy hàng",
        OrderStatus.CannotDeliver => "Không giao được hàng",
        OrderStatus.DelayDelivery => "Delay giao hàng",
        OrderStatus.ReturnReconciled => "Đã đối soát công nợ trả hàng",
        OrderStatus.Picking => "Đang lấy hàng",
        OrderStatus.Compensated => "Đơn hàng bồi hoàn",
        OrderStatus.Returning => "Đang trả hàng",
        OrderStatus.Returned => "Đã trả hàng",
        OrderStatus.ShipperReportedPicked => "Shipper báo đã lấy hàng",
        OrderStatus.ShipperReportedCannotPick => "Shipper báo không lấy được hàng",
        OrderStatus.ShipperReportedDelayPick => "Shipper báo delay lấy hàng",
        OrderStatus.ShipperReportedDelivered => "Shipper báo đã giao hàng",
        OrderStatus.ShipperReportedCannotDeliver => "Shipper báo không giao được hàng",
        OrderStatus.ShipperReportedDelayDelivery => "Shipper báo delay giao hàng",
        _ => status.ToString()
    };
}