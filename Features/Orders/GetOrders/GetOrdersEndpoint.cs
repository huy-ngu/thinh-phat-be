using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;

namespace ThinhPhat.Api.Features.Orders.GetOrders;

public sealed record GetOrdersResponse(
    List<OrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public static class GetOrdersEndpoint
{
    public static RouteGroupBuilder MapGetOrders(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetOrders")
            .WithSummary("Retrieves a paginated and filtered list of closing orders")
            .Produces<GetOrdersResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        OrderStatus? status,
        Guid? customerId,
        Guid? productId,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => pageSize
        };

        var query = db.Orders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(o => o.ProductId == productId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc >= fromDate.Value.ToUniversalTime());
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc <= toDate.Value.ToUniversalTime());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(o =>
                o.Address.ToLower().Contains(keyword) ||
                (o.Customer != null && (o.Customer.Name.ToLower().Contains(keyword) || o.Customer.PhoneNumber.Contains(keyword))) ||
                (o.Product != null && o.Product.Name.ToLower().Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Zero-tracking projection straight into DTO
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(cancellationToken);

        var response = new GetOrdersResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
