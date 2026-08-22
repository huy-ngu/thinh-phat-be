using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;

namespace ThinhPhat.Api.Features.DeliveryOrders.GetDeliveryOrders;

public sealed record GetDeliveryOrdersResponse(
    List<DeliveryOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public static class GetDeliveryOrdersEndpoint
{
    public static RouteGroupBuilder MapGetDeliveryOrders(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetDeliveryOrders")
            .WithSummary("Retrieves a paginated list of delivery orders with filtering options")
            .Produces<GetDeliveryOrdersResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        string? search,
        DeliveryOrderStatus? status,
        Guid? warehouseId,
        Guid? orderId,
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

        var query = db.DeliveryOrders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(d => d.WarehouseId == warehouseId.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(d => d.OrderId == orderId.Value);
        }

        if (fromDate.HasValue)
        {
            var utcFrom = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedAtUtc >= utcFrom);
        }

        if (toDate.HasValue)
        {
            var utcTo = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedAtUtc <= utcTo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchKeyword = search.Trim().ToLower();
            query = query.Where(d => (d.GhtkId != null && d.GhtkId.ToLower().Contains(searchKeyword))
                                  || (d.Note != null && d.Note.ToLower().Contains(searchKeyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeliveryOrderDto(
                d.Id,
                d.OrderId,
                d.WarehouseId,
                d.Warehouse != null ? d.Warehouse.Name : null,
                d.GhtkId,
                d.Status,
                CreateDeliveryOrderEndpoint.GetDeliveryStatusDisplayName(d.Status),
                d.TotalAmount,
                d.ShippingFee,
                d.Weight,
                d.Note,
                d.EstimatedPickTime,
                d.EstimatedDeliverTime,
                d.CreatedAtUtc,
                d.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var response = new GetDeliveryOrdersResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
