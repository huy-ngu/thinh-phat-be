using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Filters;
using ThinhPhat.Api.Shared.Services.Ghtk;

namespace ThinhPhat.Api.Features.Ghtk.CreateGhtkOrder;

// 1. Validator
public sealed class CreateGhtkOrderValidator : AbstractValidator<GhtkCreateOrderRequest>
{
    public CreateGhtkOrderValidator()
    {
        RuleFor(x => x.Products)
            .NotEmpty().WithMessage("Danh sách sản phẩm (products) không được để trống.");

        RuleForEach(x => x.Products).ChildRules(product =>
        {
            product.RuleFor(p => p.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.");
            product.RuleFor(p => p.Weight)
                .GreaterThan(0).WithMessage("Khối lượng sản phẩm phải lớn hơn 0.");
            product.RuleFor(p => p.Quantity)
                .GreaterThan(0).WithMessage("Số lượng sản phẩm phải lớn hơn 0.");
            product.RuleFor(p => p.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Giá sản phẩm không được âm.");
        });

        RuleFor(x => x.Order).NotNull().WithMessage("Thông tin đơn hàng (order) không được để trống.");

        When(x => x.Order is not null, () =>
        {
            RuleFor(x => x.Order.Id)
                .NotEmpty().WithMessage("Mã đơn hàng (order.id) không được để trống.");
            RuleFor(x => x.Order.PickName)
                .NotEmpty().WithMessage("Tên người gửi (pick_name) không được để trống.");
            RuleFor(x => x.Order.PickAddress)
                .NotEmpty().WithMessage("Địa chỉ lấy hàng (pick_address) không được để trống.");
            RuleFor(x => x.Order.PickProvince)
                .NotEmpty().WithMessage("Tỉnh/Thành lấy hàng (pick_province) không được để trống.");
            RuleFor(x => x.Order.PickTel)
                .NotEmpty().WithMessage("Số điện thoại lấy hàng (pick_tel) không được để trống.");
            RuleFor(x => x.Order.Name)
                .NotEmpty().WithMessage("Tên người nhận (name) không được để trống.");
            RuleFor(x => x.Order.Address)
                .NotEmpty().WithMessage("Địa chỉ nhận hàng (address) không được để trống.");
            RuleFor(x => x.Order.Province)
                .NotEmpty().WithMessage("Tỉnh/Thành nhận hàng (province) không được để trống.");
            RuleFor(x => x.Order.Tel)
                .NotEmpty().WithMessage("Số điện thoại người nhận (tel) không được để trống.");
        });
    }
}

// 2. Endpoint Slice & Handler
public static class CreateGhtkOrderEndpoint
{
    public static RouteGroupBuilder MapCreateGhtkOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/shipment/order", HandleAsync)
            .WithName("CreateGhtkOrder")
            .WithSummary("Creates a shipping order directly on GHTK (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<GhtkCreateOrderRequest>()
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status200OK)
            .Produces<GhtkCreateOrderResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        GhtkCreateOrderRequest request,
        IGhtkService ghtkService,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Gọi API GHTK
        var ghtkResponse = await ghtkService.CreateOrderAsync(request, cancellationToken);

        if (!ghtkResponse.Success)
        {
            return TypedResults.BadRequest(ghtkResponse);
        }

        // 2. Đồng bộ kết quả GHTK vào DeliveryOrder dựa trên partner_id / order.id
        var partnerId = ghtkResponse.Order?.PartnerId ?? request.Order.Id;
        if (Guid.TryParse(partnerId, out var deliveryOrderId))
        {
            var deliveryOrder = await db.DeliveryOrders
                .FirstOrDefaultAsync(d => d.Id == deliveryOrderId, cancellationToken);

            if (deliveryOrder is not null)
            {
                // Gán GHTK Label (Mã vận đơn)
                if (!string.IsNullOrWhiteSpace(ghtkResponse.Order?.Label))
                {
                    deliveryOrder.GhtkId = ghtkResponse.Order.Label;
                }

                // Gán Phí vận chuyển (Fee)
                if (ghtkResponse.Order?.Fee.HasValue == true)
                {
                    deliveryOrder.ShippingFee = ghtkResponse.Order.Fee.Value;
                }

                // Gán Trạng thái theo status_id từ GHTK nếu hợp lệ
                if (ghtkResponse.Order?.StatusId.HasValue == true && Enum.IsDefined(typeof(DeliveryOrderStatus), ghtkResponse.Order.StatusId.Value))
                {
                    deliveryOrder.Status = (DeliveryOrderStatus)ghtkResponse.Order.StatusId.Value;
                }
                else
                {
                    deliveryOrder.Status = DeliveryOrderStatus.Pending; // 1: Chưa tiếp nhận
                }

                // Gán thời gian lấy hàng và giao hàng dự kiến
                deliveryOrder.EstimatedPickTime = ghtkResponse.Order?.EstimatedPickTime;
                deliveryOrder.EstimatedDeliverTime = ghtkResponse.Order?.EstimatedDeliverTime;
                deliveryOrder.UpdatedAtUtc = DateTime.UtcNow;

                var history = new DeliveryOrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    DeliveryOrderId = deliveryOrder.Id,
                    Status = deliveryOrder.Status,
                    Note = $"Tạo đơn GHTK thành công. Mã vận đơn: {ghtkResponse.Order?.Label}, Phí: {deliveryOrder.ShippingFee:N0}đ, Lấy hàng: {deliveryOrder.EstimatedPickTime ?? "N/A"}, Giao hàng: {deliveryOrder.EstimatedDeliverTime ?? "N/A"}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                await db.DeliveryOrderStatusHistories.AddAsync(history, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return TypedResults.Ok(ghtkResponse);
    }
}
