using System.Globalization;
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
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

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
        string? fromDate,
        string? toDate,
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
            .IgnoreQueryFilters()
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

        var startUtc = ParseDateFilter(fromDate, isEndOfDay: false);
        if (startUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc >= startUtc.Value);
        }

        var endUtc = ParseDateFilter(toDate, isEndOfDay: true);
        if (endUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc <= endUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(o =>
                (o.Address != null && o.Address.ToLower().Contains(keyword)) ||
                (o.Province != null && o.Province.ToLower().Contains(keyword)) ||
                (o.District != null && o.District.ToLower().Contains(keyword)) ||
                (o.Ward != null && o.Ward.ToLower().Contains(keyword)) ||
                (o.Note != null && o.Note.ToLower().Contains(keyword)) ||
                (o.SheetNote != null && o.SheetNote.ToLower().Contains(keyword)) ||
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
            CustomerHasOrdered: o.Customer?.HasOrdered ?? false,
            WarehouseId: o.WarehouseId,
            WarehouseName: o.Warehouse?.Name,
            Province: o.Province,
            District: o.District,
            Ward: o.Ward,
            Address: o.Address,
            TotalAmount: o.TotalAmount,
            ShippingFee: o.ShippingFee,
            Weight: o.Weight,
            Status: o.Status,
            StatusName: CreateOrderEndpoint.GetStatusDisplayName(o.Status),
            Note: o.Note,
            SheetNote: o.SheetNote,
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

    /// <summary>
    /// Chuyển đổi linh hoạt chuỗi ngày/tháng/năm sang DateTime UTC theo múi giờ Việt Nam (+07:00).
    /// - Nếu chỉ truyền ngày "yyyy-MM-dd":
    ///   + isEndOfDay = false -> 00:00:00.000 GMT+7 (đầu ngày) -> quy đổi sang UTC
    ///   + isEndOfDay = true  -> 23:59:59.999 GMT+7 (cuối ngày) -> quy đổi sang UTC
    /// - Nếu truyền chuỗi DateTime/ISO đầy đủ (VD: "2026-09-25T14:30:00+07:00" hoặc "2026-09-25T07:30:00Z"):
    ///   + Parse chính xác mốc thời gian đó và quy về UTC.
    /// </summary>
    public static DateTime? ParseDateFilter(string? input, bool isEndOfDay)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        // 1. Chỉ truyền ngày: "yyyy-MM-dd" (VD: "2026-09-25")
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            var time = isEndOfDay ? new TimeOnly(23, 59, 59, 999) : new TimeOnly(0, 0, 0, 0);
            var localDateTime = dateOnly.ToDateTime(time);
            var offsetDateTime = new DateTimeOffset(localDateTime, VnOffset);
            return offsetDateTime.UtcDateTime;
        }

        // 2. Chuỗi có thông tin offset rõ ràng (VD: "+07:00", "Z")
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            return dto.UtcDateTime;
        }

        // 3. Chuỗi có ngày và giờ nhưng không có offset (VD: "2026-09-25T14:30:00") -> mặc định hiểu là giờ VN
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            if (dt.Kind == DateTimeKind.Utc)
            {
                return dt;
            }

            var offsetDateTime = new DateTimeOffset(dt, VnOffset);
            return offsetDateTime.UtcDateTime;
        }

        return null;
    }
}
