using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;

namespace ThinhPhat.Api.Features.Statistics.GetOverview;

public sealed record SummaryOverviewDto(
    decimal TotalRevenue,
    int TotalOrders,
    int SuccessfulOrders,
    int ProcessingOrders,
    int TelesaleOrders,
    int CancelledOrReturnedOrders,
    decimal SuccessRate
);

public sealed record RevenueChartPointDto(
    string Date,
    decimal Revenue,
    int TotalOrders,
    int SuccessfulOrders
);

public sealed record TopProductDto(
    Guid ProductId,
    string ProductName,
    string ProductImageUrl,
    int QuantitySold,
    decimal TotalRevenue
);

public sealed record OrderStatusStatDto(
    string GroupKey,
    string GroupName,
    int Count,
    decimal Percentage
);

public sealed record GetOverviewResponse(
    SummaryOverviewDto Summary,
    List<RevenueChartPointDto> RevenueChart,
    List<TopProductDto> TopProducts,
    List<OrderStatusStatDto> StatusDistribution
);

public static class GetOverviewEndpoint
{
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private static readonly HashSet<OrderStatus> SuccessfulStatuses = [
        OrderStatus.DeliveredUnreconciled,
        OrderStatus.Reconciled
    ];

    private static readonly HashSet<OrderStatus> ProcessingStatuses = [
        OrderStatus.Confirmed,
        OrderStatus.Accepted,
        OrderStatus.PickedOrStocked,
        OrderStatus.Delivering,
        OrderStatus.Picking,
        OrderStatus.DelayPick,
        OrderStatus.DelayDelivery,
        OrderStatus.ShipperReportedPicked,
        OrderStatus.ShipperReportedDelivered,
        OrderStatus.ShipperReportedDelayPick,
        OrderStatus.ShipperReportedDelayDelivery
    ];

    private static readonly HashSet<OrderStatus> TelesaleStatuses = [
        OrderStatus.Done,
        OrderStatus.New,
        OrderStatus.NoAnswer,
        OrderStatus.CallbackLater,
        OrderStatus.OutOfStock,
        OrderStatus.Duplicate
    ];

    private static readonly HashSet<OrderStatus> CancelledOrReturnedStatuses = [
        OrderStatus.Cancelled,
        OrderStatus.GhtkCancelled,
        OrderStatus.CannotPick,
        OrderStatus.CannotDeliver,
        OrderStatus.Returning,
        OrderStatus.Returned,
        OrderStatus.ReturnReconciled,
        OrderStatus.Compensated,
        OrderStatus.ShipperReportedCannotPick,
        OrderStatus.ShipperReportedCannotDeliver
    ];

    public static RouteGroupBuilder MapGetOverview(this RouteGroupBuilder group)
    {
        group.MapGet("/overview", HandleAsync)
            .WithName("GetStatisticsOverview")
            .WithSummary("Lấy dữ liệu thống kê tổng quan cho Admin Dashboard")
            .RequireAuthorization("AdminOnly")
            .Produces<GetOverviewResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken = default)
    {
        // 1. Chuẩn hóa khoảng thời gian (Mặc định 30 ngày gần nhất tính theo UTC)
        var parsedEnd = ParseDateFilter(toDate, isEndOfDay: true);
        var parsedStart = ParseDateFilter(fromDate, isEndOfDay: false);

        var endUtc = parsedEnd ?? DateTime.UtcNow;
        var startUtc = parsedStart ?? endUtc.AddDays(-30);

        if (startUtc > endUtc)
        {
            (startUtc, endUtc) = (endUtc, startUtc);
        }

        // 2. Truy vấn đơn hàng trong kỳ (Chỉ chọn các trường cần thiết để tiết kiệm RAM tối đa)
        var rawOrders = await db.Orders
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(o => o.CreatedAtUtc >= startUtc && o.CreatedAtUtc <= endUtc)
            .Select(o => new
            {
                o.Id,
                o.Status,
                o.TotalAmount,
                o.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        // 3. Tính toán các chỉ số tóm tắt (Summary KPIs)
        var totalOrders = rawOrders.Count;
        var successfulOrders = rawOrders.Count(o => SuccessfulStatuses.Contains(o.Status));
        var processingOrders = rawOrders.Count(o => ProcessingStatuses.Contains(o.Status));
        var telesaleOrders = rawOrders.Count(o => TelesaleStatuses.Contains(o.Status));
        var cancelledOrReturnedOrders = rawOrders.Count(o => CancelledOrReturnedStatuses.Contains(o.Status));
        var totalRevenue = rawOrders
            .Where(o => SuccessfulStatuses.Contains(o.Status))
            .Sum(o => o.TotalAmount);
        var successRate = totalOrders > 0
            ? Math.Round((decimal)successfulOrders * 100m / totalOrders, 2)
            : 0m;

        var summary = new SummaryOverviewDto(
            TotalRevenue: totalRevenue,
            TotalOrders: totalOrders,
            SuccessfulOrders: successfulOrders,
            ProcessingOrders: processingOrders,
            TelesaleOrders: telesaleOrders,
            CancelledOrReturnedOrders: cancelledOrReturnedOrders,
            SuccessRate: successRate
        );

        // 4. Phân bổ trạng thái đơn hàng (Status Distribution - 4 nhóm)
        decimal CalcPct(int count) => totalOrders > 0 ? Math.Round((decimal)count * 100m / totalOrders, 2) : 0m;

        var statusDistribution = new List<OrderStatusStatDto>
        {
            new("successful", "Thành công", successfulOrders, CalcPct(successfulOrders)),
            new("processing", "Đang giao", processingOrders, CalcPct(processingOrders)),
            new("telesale", "Chờ xử lý", telesaleOrders, CalcPct(telesaleOrders)),
            new("cancelled_returned", "Hủy / Hoàn", cancelledOrReturnedOrders, CalcPct(cancelledOrReturnedOrders))
        };

        // 5. Chuỗi dữ liệu biểu đồ doanh thu theo ngày liên tục (Quy đổi múi giờ Việt Nam UTC+7)
        var startDateVn = DateOnly.FromDateTime(startUtc.Add(VnOffset));
        var endDateVn = DateOnly.FromDateTime(endUtc.Add(VnOffset));

        var ordersByVnDate = rawOrders
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAtUtc.Add(VnOffset)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var revenueChart = new List<RevenueChartPointDto>();
        for (var date = startDateVn; date <= endDateVn; date = date.AddDays(1))
        {
            if (ordersByVnDate.TryGetValue(date, out var dayOrders))
            {
                var daySuccess = dayOrders.Where(o => SuccessfulStatuses.Contains(o.Status)).ToList();
                revenueChart.Add(new RevenueChartPointDto(
                    Date: date.ToString("yyyy-MM-dd"),
                    Revenue: daySuccess.Sum(o => o.TotalAmount),
                    TotalOrders: dayOrders.Count,
                    SuccessfulOrders: daySuccess.Count
                ));
            }
            else
            {
                revenueChart.Add(new RevenueChartPointDto(
                    Date: date.ToString("yyyy-MM-dd"),
                    Revenue: 0m,
                    TotalOrders: 0,
                    SuccessfulOrders: 0
                ));
            }
        }

        // 6. Top 5 sản phẩm bán chạy nhất trong kỳ (Dựa trên số lượng bán của các đơn thành công)
        var successfulOrderIds = rawOrders
            .Where(o => SuccessfulStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToList();

        var topProducts = new List<TopProductDto>();

        if (successfulOrderIds.Count > 0)
        {
            var topProductStats = await db.OrderItems
                .AsNoTracking()
                .Where(i => successfulOrderIds.Contains(i.OrderId))
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    QuantitySold = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(p => p.QuantitySold)
                .ThenByDescending(p => p.TotalRevenue)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (topProductStats.Count > 0)
            {
                var productIds = topProductStats.Select(x => x.ProductId).ToList();
                var products = await db.Products
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                topProducts = topProductStats.Select(x =>
                {
                    products.TryGetValue(x.ProductId, out var product);
                    return new TopProductDto(
                        ProductId: x.ProductId,
                        ProductName: product?.Name ?? "Sản phẩm không xác định",
                        ProductImageUrl: product?.ImageUrl ?? string.Empty,
                        QuantitySold: x.QuantitySold,
                        TotalRevenue: x.TotalRevenue
                    );
                }).ToList();
            }
        }

        var response = new GetOverviewResponse(
            Summary: summary,
            RevenueChart: revenueChart,
            TopProducts: topProducts,
            StatusDistribution: statusDistribution
        );

        return TypedResults.Ok(response);
    }

    public static DateTime? ParseDateFilter(string? input, bool isEndOfDay)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        // 1. Chỉ truyền ngày "yyyy-MM-dd"
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            var time = isEndOfDay ? new TimeOnly(23, 59, 59, 999) : new TimeOnly(0, 0, 0, 0);
            var localDateTime = dateOnly.ToDateTime(time);
            var offsetDateTime = new DateTimeOffset(localDateTime, VnOffset);
            return offsetDateTime.UtcDateTime;
        }

        // 2. Chuỗi có offset rõ ràng (+07:00, Z, v.v.)
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            return dto.UtcDateTime;
        }

        // 3. Chuỗi có ngày và giờ nhưng không có offset (VD: "2026-09-25T14:30:00")
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
