using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Services.Ghtk;

namespace ThinhPhat.Api.Features.Ghtk.CancelGhtkOrder;

public static class CancelGhtkOrderEndpoint
{
    private static readonly OrderStatus[] AllowedStatuses =
    [
        OrderStatus.Confirmed, // 1 - Đã chốt đơn / Chưa tiếp nhận
        OrderStatus.Accepted,  // 2 - GHTK Đã tiếp nhận
        OrderStatus.Picking    // 12 - Đang lấy hàng
    ];

    public static RouteGroupBuilder MapCancelGhtkOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/shipment/cancel/{ghtkId}", HandleAsync)
            .WithName("CancelGhtkOrder")
            .WithSummary("Hủy đơn hàng GHTK theo mã vận đơn GHTK (GhtkId chuỗi, chỉ cho phép khi status = 1, 2, hoặc 12, không cần body)")
            .RequireAuthorization("AdminOnly")
            .Produces<GhtkCancelOrderResponse>(StatusCodes.Status200OK)
            .Produces<GhtkCancelOrderResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        string ghtkId,
        IGhtkService ghtkService,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var cleanGhtkId = ghtkId.Trim();

        // 1. Tải thông tin Order theo Mã vận đơn GHTK (GhtkId)
        var order = await db.Orders
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.GhtkId == cleanGhtkId, cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Không tìm thấy đơn hàng nào có mã vận đơn GHTK '{cleanGhtkId}' trong hệ thống."
            );
        }

        // 2. Kiểm tra điều kiện trạng thái: Chỉ cho phép hủy khi ở trạng thái 1 (Confirmed), 2 (Accepted), 12 (Picking)
        if (!AllowedStatuses.Contains(order.Status))
        {
            var currentStatusName = CreateOrderEndpoint.GetStatusDisplayName(order.Status);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order Status For Cancellation",
                detail: $"Chỉ có thể hủy đơn GHTK khi đơn hàng ở trạng thái 1 (Chốt đơn/Chờ GHTK), 2 (GHTK Đã tiếp nhận) hoặc 12 (Đang lấy hàng). Trạng thái hiện tại của đơn là '{currentStatusName}' (Mã trạng thái: {(int)order.Status})."
            );
        }

        // 3. Gọi API hủy đơn của GHTK: POST /services/shipment/cancel/{TRACKING_ORDER} (Không cần body)
        var ghtkResponse = await ghtkService.CancelOrderAsync(cleanGhtkId, cancellationToken);

        if (!ghtkResponse.Success)
        {
            return TypedResults.BadRequest(ghtkResponse);
        }

        // 4. Cập nhật trạng thái đơn hàng sang GhtkCancelled (-1) và ghi nhận lịch sử trạng thái
        order.Status = OrderStatus.GhtkCancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;

        var noteDetails = new List<string> { $"Hủy đơn hàng trên GHTK thành công (Mã GHTK: {cleanGhtkId})" };
        if (!string.IsNullOrWhiteSpace(ghtkResponse.Message))
        {
            noteDetails.Add($"Phản hồi GHTK: {ghtkResponse.Message.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(ghtkResponse.LogId))
        {
            noteDetails.Add($"Log ID: {ghtkResponse.LogId.Trim()}");
        }

        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = OrderStatus.GhtkCancelled,
            Note = string.Join(" | ", noteDetails),
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.OrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ghtkResponse);
    }
}
