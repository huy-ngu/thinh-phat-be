using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Orders.CreateOrder;

namespace ThinhPhat.Api.Features.Orders.GetOrderById;

public static class GetOrderByIdEndpoint
{
    public static RouteGroupBuilder MapGetOrderById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetOrderById")
            .WithSummary("Retrieves closing order details by ID")
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
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
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Order with ID '{id}' was not found."
            );
        }

        return TypedResults.Ok(order);
    }
}
