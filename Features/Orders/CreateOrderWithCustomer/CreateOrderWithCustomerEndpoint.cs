using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Orders.CreateOrderWithCustomer;

// 1. DTOs
public sealed record CreateOrderWithCustomerItemRequest(
    Guid ProductId,
    int Quantity
);

public sealed record CreateOrderWithCustomerRequest(
    // Thông tin khách hàng
    string CustomerName,
    string PhoneNumber,

    // Cấu trúc địa chỉ giao hàng (địa chỉ cụ thể và địa lí)
    string? Province,
    string? District,
    string? Ward,
    string? Address,

    // Danh sách sản phẩm & số lượng
    List<CreateOrderWithCustomerItemRequest> Items,

    // Kho hàng xuất đơn (tùy chọn)
    Guid? WarehouseId = null
);

// 2. Request Validator
public sealed class CreateOrderWithCustomerValidator : AbstractValidator<CreateOrderWithCustomerRequest>
{
    public CreateOrderWithCustomerValidator()
    {
        // Thông tin khách hàng
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Tên khách hàng là bắt buộc.")
            .MaximumLength(150).WithMessage("Tên khách hàng không được vượt quá 150 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại là bắt buộc.")
            .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.")
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Số điện thoại không đúng định dạng chuẩn Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

        // Địa chỉ giao hàng
        RuleFor(x => x.Province)
            .MaximumLength(100).WithMessage("Tỉnh/Thành phố không được vượt quá 100 ký tự.");

        RuleFor(x => x.District)
            .MaximumLength(100).WithMessage("Quận/Huyện không được vượt quá 100 ký tự.");

        RuleFor(x => x.Ward)
            .MaximumLength(100).WithMessage("Phường/Xã không được vượt quá 100 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ cụ thể không được vượt quá 300 ký tự.");

        // Danh sách sản phẩm
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Đơn hàng phải chứa ít nhất 1 sản phẩm.")
            .Must(items => items != null && items.Count > 0).WithMessage("Danh sách sản phẩm không được rỗng.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("ProductId là bắt buộc.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Số lượng sản phẩm phải lớn hơn 0.")
                .LessThanOrEqualTo(1000).WithMessage("Số lượng sản phẩm trong một đơn không được vượt quá 1000.");
        });
    }
}

// 3. Endpoint Slice & Handler
public static class CreateOrderWithCustomerEndpoint
{

    public static RouteGroupBuilder MapCreateOrderWithCustomer(this RouteGroupBuilder group)
    {
        group.MapPost("/with-customer", HandleAsync)
            .WithName("CreateOrderWithCustomer")
            .WithSummary("Tạo khách hàng (nếu SĐT chưa có trong DB) và tạo đơn hàng với trạng thái mặc định -2 (Mới tạo)")
            .AllowAnonymous()
            .WithRequestValidation<CreateOrderWithCustomerRequest>()
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        CreateOrderWithCustomerRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // 1. Kiểm tra Customer theo PhoneNumber (nếu chưa có thì thêm mới, đã có thì lấy lại)
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhone, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = request.CustomerName.Trim(),
                PhoneNumber = normalizedPhone,
                CreatedAtUtc = DateTime.UtcNow
            };

            await db.Customers.AddAsync(customer, cancellationToken);
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

        // 3. Xử lý danh sách Order Items và tự động tính tổng tiền theo giá sản phẩm trong DB
        var orderId = Guid.NewGuid();
        var orderItems = new List<OrderItem>();
        var itemDtos = new List<OrderItemDto>();
        decimal calculatedItemsTotal = 0m;

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

            var unitPrice = product.Price;
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

        // 4. Khởi tạo Order: totalAmount tự tính, weight = 3, shippingFee = null, note = null, status = -2 (New)
        var order = new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            WarehouseId = request.WarehouseId,
            Province = string.IsNullOrWhiteSpace(request.Province) ? null : request.Province.Trim(),
            District = string.IsNullOrWhiteSpace(request.District) ? null : request.District.Trim(),
            Ward = string.IsNullOrWhiteSpace(request.Ward) ? null : request.Ward.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            TotalAmount = calculatedItemsTotal,
            ShippingFee = null,
            Weight = 3m,
            Status = OrderStatus.New, // -2 Mới tạo
            Note = null,
            SheetNote = null,
            CreatedAtUtc = DateTime.UtcNow,
            Items = orderItems
        };

        // 5. Khởi tạo lịch sử trạng thái ban đầu
        var initialHistory = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = order.Status,
            Note = $"Khởi tạo đơn hàng: {CreateOrderEndpoint.GetStatusDisplayName(order.Status)}",
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
                StatusName: CreateOrderEndpoint.GetStatusDisplayName(initialHistory.Status),
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
            StatusName: CreateOrderEndpoint.GetStatusDisplayName(order.Status),
            Note: order.Note,
            SheetNote: order.SheetNote,
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
}
