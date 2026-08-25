using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Filters;
using ThinhPhat.Api.Shared.Services.Ghtk;

namespace ThinhPhat.Api.Features.Ghtk.CreateGhtkOrder;

// 1. DTO
public sealed record PushGhtkOrderRequest(
    Guid OrderId,
    string? Note = null
);

// 2. Validator
public sealed class PushGhtkOrderValidator : AbstractValidator<PushGhtkOrderRequest>
{
    public PushGhtkOrderValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("Mã đơn hàng (OrderId) là bắt buộc.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateGhtkOrderEndpoint
{
    public static RouteGroupBuilder MapCreateGhtkOrder(this RouteGroupBuilder group)
    {
        // 1. Đẩy đơn bằng POST body: { orderId: "..." }
        group.MapPost("/shipment/order", HandleWithBodyAsync)
            .WithName("CreateGhtkOrder")
            .WithSummary("Pushes a completed order (Status = Done / 0) to GHTK API (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<PushGhtkOrderRequest>()
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status200OK)
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // 2. Đẩy đơn trực tiếp qua Path Param: POST /shipment/order/{orderId}
        group.MapPost("/shipment/order/{orderId:guid}", HandleWithPathAsync)
            .WithName("CreateGhtkOrderByPath")
            .WithSummary("Pushes an order by ID to GHTK API (Admin only)")
            .RequireAuthorization("AdminOnly")
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status200OK)
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleWithBodyAsync(
        PushGhtkOrderRequest request,
        IGhtkService ghtkService,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await ProcessPushOrderAsync(request.OrderId, request.Note, ghtkService, db, cancellationToken);
    }

    private static async Task<IResult> HandleWithPathAsync(
        Guid orderId,
        [FromQuery] string? note,
        IGhtkService ghtkService,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await ProcessPushOrderAsync(orderId, note, ghtkService, db, cancellationToken);
    }

    private static async Task<IResult> ProcessPushOrderAsync(
        Guid orderId,
        string? overrideNote,
        IGhtkService ghtkService,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Tải thông tin Order, Customer, Warehouse, Items & Product
        var order = await db.Orders
            .IgnoreQueryFilters()
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Đơn hàng với ID '{orderId}' không tồn tại trong hệ thống."
            );
        }

        // 2. Kiểm tra điều kiện trạng thái: Chỉ đơn hàng Done (0) mới được đẩy
        if (order.Status != OrderStatus.Done)
        {
            var currentStatusName = CreateOrderEndpoint.GetStatusDisplayName(order.Status);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order Status",
                detail: $"Chỉ những đơn hàng ở trạng thái 'Đã chốt thành công' (Done = 0) mới được phép đẩy sang GHTK. Trạng thái hiện tại của đơn là: '{currentStatusName}' ({order.Status})."
            );
        }

        // 3. Kiểm tra thông tin Kho hàng xuất
        if (order.Warehouse is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing Warehouse",
                detail: "Đơn hàng chưa được chọn Kho xuất hàng (Warehouse). Vui lòng cập nhật kho trước khi đẩy sang GHTK."
            );
        }

        // 4. Kiểm tra thông tin Khách hàng & Địa chỉ giao hàng
        if (order.Customer is null ||
            string.IsNullOrWhiteSpace(order.Province) ||
            string.IsNullOrWhiteSpace(order.District) ||
            string.IsNullOrWhiteSpace(order.Address))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Incomplete Customer Address",
                detail: "Thông tin khách hàng hoặc địa chỉ giao hàng (Tỉnh/Thành, Quận/Huyện, Địa chỉ chi tiết) chưa đầy đủ."
            );
        }

        // 5. Kiểm tra danh sách sản phẩm
        if (order.Items == null || order.Items.Count == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Empty Order Items",
                detail: "Đơn hàng không có sản phẩm nào để tạo kiện giao GHTK."
            );
        }

        // 6. Tự động chuyển đổi danh sách sản phẩm sang chuẩn GHTK (Weight mặc định là 3, không truyền product_code)
        var defaultWeight = (double)(order.Weight.HasValue && order.Weight.Value > 0
            ? order.Weight.Value
            : 3m);

        var ghtkProducts = order.Items.Select(item => new GhtkProductItem(
            Name: item.Product?.Name ?? "Sản phẩm",
            Weight: defaultWeight,
            Quantity: item.Quantity,
            Price: item.UnitPrice,
            ProductCode: null
        )).ToList();

        // 7. Tự động tính toán & điền đầy đủ các trường thông tin theo mẫu chuẩn GHTK
        var goodsValue = order.TotalAmount - (order.ShippingFee ?? 0m);
        if (goodsValue <= 0) goodsValue = order.TotalAmount;

        var noteToUse = !string.IsNullOrWhiteSpace(overrideNote)
            ? overrideNote.Trim()
            : (!string.IsNullOrWhiteSpace(order.Note)
                ? order.Note.Trim()
                : "Cho khách xem hàng, gọi điện trước khi giao hàng");

        var ghtkOrderInfo = new GhtkOrderInfo(
            Id: order.Id.ToString(),
            PickName: order.Warehouse.Name,
            PickAddress: order.Warehouse.Address,
            PickProvince: order.Warehouse.Province,
            PickDistrict: order.Warehouse.District,
            PickWard: string.IsNullOrWhiteSpace(order.Warehouse.Ward) ? order.Warehouse.District : order.Warehouse.Ward,
            PickTel: order.Warehouse.PhoneNumber,
            Name: order.Customer.Name,
            Address: order.Address,
            Province: order.Province,
            District: order.District,
            Ward: string.IsNullOrWhiteSpace(order.Ward) ? order.District : order.Ward,
            Street: order.Address,
            Hamlet: "Khác",
            Tel: order.Customer.PhoneNumber,
            IsFreeship: 1,
            PickDate: DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd"),
            Transport: "road",
            PickOption: "cod",
            Note: noteToUse,
            Value: goodsValue,
            PickMoney: order.TotalAmount
        );

        var ghtkRequest = new GhtkCreateOrderRequest(ghtkProducts, ghtkOrderInfo);

        // 8. Gọi API GHTK
        var ghtkResponse = await ghtkService.CreateOrderAsync(ghtkRequest, cancellationToken);

        if (!ghtkResponse.Success)
        {
            return TypedResults.BadRequest(ghtkResponse);
        }

        // 9. Cập nhật Order & Lịch sử trạng thái sau khi GHTK tạo đơn thành công
        if (!string.IsNullOrWhiteSpace(ghtkResponse.Order?.Label))
        {
            order.GhtkId = ghtkResponse.Order.Label;
        }

        if (ghtkResponse.Order?.Fee.HasValue == true)
        {
            order.ShippingFee = ghtkResponse.Order.Fee.Value;
        }

        // Chuyển trạng thái sang Confirmed (1 - Đã chốt đơn / Chờ GHTK tiếp nhận)
        order.Status = OrderStatus.Confirmed;
        order.EstimatedPickTime = ghtkResponse.Order?.EstimatedPickTime;
        order.EstimatedDeliverTime = ghtkResponse.Order?.EstimatedDeliverTime;
        order.UpdatedAtUtc = DateTime.UtcNow;

        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = OrderStatus.Confirmed,
            Note = $"Đẩy đơn sang GHTK thành công. Mã vận đơn: {order.GhtkId}, Phí: {order.ShippingFee:N0}đ, Lấy hàng: {order.EstimatedPickTime ?? "N/A"}, Giao hàng: {order.EstimatedDeliverTime ?? "N/A"}",
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.OrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ghtkResponse);
    }
}
