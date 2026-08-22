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
            .WithSummary("Quickly updates order status (Admin only)")
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
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Order with ID '{id}' was not found."
            );
        }

        order.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            order.Note = request.Note.Trim();
        }
        order.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

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
