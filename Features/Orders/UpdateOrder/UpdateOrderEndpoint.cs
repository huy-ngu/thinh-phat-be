using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Orders.UpdateOrder;

// 1. DTOs
public sealed record UpdateOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal? UnitPrice = null
);

public sealed record UpdateOrderRequest(
    Guid CustomerId,
    string Address,
    Guid? WarehouseId = null,
    decimal? TotalAmount = null,
    decimal ShippingFee = 0m,
    decimal Weight = 0m,
    OrderStatus Status = OrderStatus.Draft,
    string? Note = null,
    List<UpdateOrderItemRequest>? Items = null
);

// 2. Validator
public sealed class UpdateOrderValidator : AbstractValidator<UpdateOrderRequest>
{
    public UpdateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Delivery address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).When(x => x.TotalAmount.HasValue).WithMessage("Total amount must be greater than or equal to 0.");

        RuleFor(x => x.ShippingFee)
            .GreaterThanOrEqualTo(0).WithMessage("Shipping fee must be greater than or equal to 0.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0).WithMessage("Weight must be greater than or equal to 0.");

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
public static class UpdateOrderEndpoint
{
    public static RouteGroupBuilder MapUpdateOrder(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateOrder")
            .WithSummary("Updates order details, items and fulfillment settings (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<UpdateOrderRequest>()
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateOrderRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
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

        // 2. Kiểm tra Warehouse
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

        // 3. Xử lý trạng thái mới & ghi log lịch sử
        if (order.Status != request.Status)
        {
            var history = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = request.Status,
                Note = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Cập nhật trạng thái: {CreateOrderEndpoint.GetStatusDisplayName(request.Status)}"
                    : $"Cập nhật trạng thái sang {CreateOrderEndpoint.GetStatusDisplayName(request.Status)}: {request.Note.Trim()}",
                CreatedAtUtc = DateTime.UtcNow
            };

            await db.OrderStatusHistories.AddAsync(history, cancellationToken);
            order.Status = request.Status;
        }

        // 4. Cập nhật Order Items
        decimal calculatedItemsTotal = 0m;
        if (request.Items != null)
        {
            // Xóa các items cũ
            db.OrderItems.RemoveRange(order.Items);
            order.Items.Clear();

            if (request.Items.Count > 0)
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
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Quantity = itemReq.Quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice
                    };

                    order.Items.Add(orderItem);
                }
            }
        }
        else
        {
            // Giữ nguyên items cũ
            calculatedItemsTotal = order.Items.Sum(i => i.TotalPrice);
        }

        var finalTotalAmount = request.TotalAmount ?? (calculatedItemsTotal + request.ShippingFee);

        // 5. Cập nhật các trường chính
        order.CustomerId = request.CustomerId;
        order.WarehouseId = request.WarehouseId;
        order.Address = request.Address.Trim();
        order.TotalAmount = finalTotalAmount;
        order.ShippingFee = request.ShippingFee;
        order.Weight = request.Weight;
        order.Note = request.Note?.Trim();
        order.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // 6. Trả về kết quả hoàn chỉnh
        var refreshedOrder = await db.Orders
            .AsNoTracking()
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
            WarehouseName: refreshedOrder.Warehouse?.Name ?? warehouseName,
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
