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
public sealed record UpdateOrderRequest(
    Guid ProductId,
    Guid CustomerId,
    string Address,
    decimal TotalAmount,
    OrderStatus Status,
    string? Note
);

// 2. Validator
public sealed class UpdateOrderValidator : AbstractValidator<UpdateOrderRequest>
{
    public UpdateOrderValidator()
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
public static class UpdateOrderEndpoint
{
    public static RouteGroupBuilder MapUpdateOrder(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateOrder")
            .WithSummary("Updates closing order details (Admin only)")
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
            .Include(o => o.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Order with ID '{id}' was not found."
            );
        }

        // Verify product exists if changed
        if (order.ProductId != request.ProductId)
        {
            var productExists = await db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.ProductId, cancellationToken);

            if (!productExists)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Product with ID '{request.ProductId}' does not exist."
                );
            }

            order.ProductId = request.ProductId;
        }

        // Verify customer exists if changed
        if (order.CustomerId != request.CustomerId)
        {
            var customerExists = await db.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CustomerId, cancellationToken);

            if (!customerExists)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Customer with ID '{request.CustomerId}' does not exist."
                );
            }

            order.CustomerId = request.CustomerId;
        }

        order.Address = request.Address.Trim();
        order.TotalAmount = request.TotalAmount;
        order.Status = request.Status;
        order.Note = request.Note?.Trim();
        order.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // Fetch refreshed details for response
        var response = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDto(
                o.Id,
                o.ProductId,
                o.Product != null ? o.Product.Name : string.Empty,
                o.Product != null ? o.Product.Price : 0m,
                o.Product != null ? o.Product.ImageUrl : string.Empty,
                o.CustomerId,
                o.Customer != null ? o.Customer.Name : string.Empty,
                o.Customer != null ? o.Customer.PhoneNumber : string.Empty,
                o.Address,
                o.TotalAmount,
                o.Status,
                CreateOrderEndpoint.GetStatusDisplayName(o.Status),
                o.Note,
                o.CreatedAtUtc,
                o.UpdatedAtUtc
            ))
            .FirstAsync(cancellationToken);

        return TypedResults.Ok(response);
    }
}
