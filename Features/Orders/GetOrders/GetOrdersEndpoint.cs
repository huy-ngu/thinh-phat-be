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
            .WithSummary("Retrieves a paginated and filtered list of orders (Admin only)")
            .RequireAuthorization("AdminOnly")
            .Produces<GetOrdersResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        OrderStatus? status,
        Guid? customerId,
        Guid? warehouseId,
        Guid? productId,
        string? ghtkId,
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

        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistories)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(o => o.WarehouseId == warehouseId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(o => o.Items.Any(i => i.ProductId == productId.Value));
        }

        if (!string.IsNullOrWhiteSpace(ghtkId))
        {
            var cleanGhtkId = ghtkId.Trim().ToLower();
            query = query.Where(o => o.GhtkId != null && o.GhtkId.ToLower().Contains(cleanGhtkId));
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
                (o.Note != null && o.Note.ToLower().Contains(keyword)) ||
                (o.GhtkId != null && o.GhtkId.ToLower().Contains(keyword)) ||
                (o.Customer != null && (o.Customer.Name.ToLower().Contains(keyword) || o.Customer.PhoneNumber.Contains(keyword))) ||
                o.Items.Any(i => i.Product != null && i.Product.Name.ToLower().Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = orders.Select(o => new OrderDto(
            Id: o.Id,
            CustomerId: o.CustomerId,
            CustomerName: o.Customer?.Name ?? string.Empty,
            CustomerPhone: o.Customer?.PhoneNumber ?? string.Empty,
            WarehouseId: o.WarehouseId,
            WarehouseName: o.Warehouse?.Name,
            Address: o.Address,
            TotalAmount: o.TotalAmount,
            ShippingFee: o.ShippingFee,
            Weight: o.Weight,
            Status: o.Status,
            StatusName: CreateOrderEndpoint.GetStatusDisplayName(o.Status),
            Note: o.Note,
            GhtkId: o.GhtkId,
            EstimatedPickTime: o.EstimatedPickTime,
            EstimatedDeliverTime: o.EstimatedDeliverTime,
            Items: o.Items.Select(i => new OrderItemDto(
                Id: i.Id,
                ProductId: i.ProductId,
                ProductName: i.Product?.Name ?? string.Empty,
                ProductPrice: i.Product?.Price ?? 0m,
                ProductImageUrl: i.Product?.ImageUrl ?? string.Empty,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice,
                TotalPrice: i.TotalPrice
            )).ToList(),
            StatusHistories: o.StatusHistories
                .OrderByDescending(h => h.CreatedAtUtc)
                .Select(h => new OrderStatusHistoryDto(
                    Id: h.Id,
                    Status: h.Status,
                    StatusName: CreateOrderEndpoint.GetStatusDisplayName(h.Status),
                    Note: h.Note,
                    CreatedAtUtc: h.CreatedAtUtc
                )).ToList(),
            CreatedAtUtc: o.CreatedAtUtc,
            UpdatedAtUtc: o.UpdatedAtUtc
        )).ToList();

        var response = new GetOrdersResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
