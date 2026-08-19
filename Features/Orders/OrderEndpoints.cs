using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Features.Orders.DeleteOrder;
using ThinhPhat.Api.Features.Orders.GetOrderById;
using ThinhPhat.Api.Features.Orders.GetOrders;
using ThinhPhat.Api.Features.Orders.UpdateOrder;
using ThinhPhat.Api.Features.Orders.UpdateOrderStatus;

namespace ThinhPhat.Api.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization(); // Yêu cầu đăng nhập để quản lý đơn chốt

        group.MapCreateOrder();
        group.MapGetOrders();
        group.MapGetOrderById();
        group.MapUpdateOrder();
        group.MapUpdateOrderStatus();
        group.MapDeleteOrder();

        return app;
    }
}
