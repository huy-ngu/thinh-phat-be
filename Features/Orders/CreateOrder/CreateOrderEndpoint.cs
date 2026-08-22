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
public sealed record CreateOrderRequest(
    Guid ProductId,
    Guid CustomerId,
    string Address,
    decimal TotalAmount,
    OrderStatus Status = OrderStatus.New,
    string? Note = null
);

public sealed record OrderDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal ProductPrice,
    string ProductImageUrl,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    decimal TotalAmount,
    OrderStatus Status,
    string StatusName,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// 2. Request Validator
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Delivery address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Total amount must be greater than or equal to 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates a new closing order (Admin only)")
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
        // 1. AsNoTracking Check for Product and Customer existence
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Product with ID '{request.ProductId}' does not exist."
            );
        }

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

        // 2. Manual Mapping & Persistence
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            CustomerId = request.CustomerId,
            Address = request.Address.Trim(),
            TotalAmount = request.TotalAmount,
            Status = request.Status,
            Note = request.Note?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Orders.AddAsync(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 3. Response Mapping
        var response = new OrderDto(
            Id: order.Id,
            ProductId: product.Id,
            ProductName: product.Name,
            ProductPrice: product.Price,
            ProductImageUrl: product.ImageUrl,
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            CustomerPhone: customer.PhoneNumber,
            Address: order.Address,
            TotalAmount: order.TotalAmount,
            Status: order.Status,
            StatusName: GetStatusDisplayName(order.Status),
            Note: order.Note,
            CreatedAtUtc: order.CreatedAtUtc,
            UpdatedAtUtc: order.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/orders/{order.Id}", response);
    }

    public static string GetStatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.New => "Mới tạo",
        OrderStatus.Confirmed => "Chốt đơn",
        OrderStatus.NoAnswer => "Không nghe máy",
        OrderStatus.Cancelled => "Không mua",
        OrderStatus.Uploaded => "Đã up đơn",
        OrderStatus.OutOfStock => "Hết hàng",
        _ => status.ToString()
    };
}
                                                                                                                                                                                                                                                     