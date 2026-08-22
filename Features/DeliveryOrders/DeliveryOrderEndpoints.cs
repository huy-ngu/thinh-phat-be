using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.DeliveryOrders.CreateDeliveryOrder;
using ThinhPhat.Api.Features.DeliveryOrders.DeleteDeliveryOrder;
using ThinhPhat.Api.Features.DeliveryOrders.GetDeliveryOrderById;
using ThinhPhat.Api.Features.DeliveryOrders.GetDeliveryOrders;
using ThinhPhat.Api.Features.DeliveryOrders.UpdateDeliveryOrder;
using ThinhPhat.Api.Features.DeliveryOrders.UpdateDeliveryOrderStatus;
using ThinhPhat.Api.Features.DeliveryOrderStatusHistories.CreateDeliveryOrderStatusHistory;
using ThinhPhat.Api.Features.DeliveryOrderStatusHistories.DeleteDeliveryOrderStatusHistory;
using ThinhPhat.Api.Features.DeliveryOrderStatusHistories.GetDeliveryOrderHistories;

namespace ThinhPhat.Api.Features.DeliveryOrders;

public static class DeliveryOrderEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/delivery-orders")
            .WithTags("DeliveryOrders")
            .RequireAuthorization();

        // Delivery Order CRUD
        group.MapCreateDeliveryOrder();
        group.MapGetDeliveryOrders();
        group.MapGetDeliveryOrderById();
        group.MapUpdateDeliveryOrder();
        group.MapUpdateDeliveryOrderStatus();
        group.MapDeleteDeliveryOrder();

        // Delivery Order Status History CRUD & Timeline
        group.MapGetDeliveryOrderHistories();
        group.MapCreateDeliveryOrderStatusHistory();
        group.MapDeleteDeliveryOrderStatusHistory();

        return app;
    }
}
