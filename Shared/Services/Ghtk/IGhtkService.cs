namespace ThinhPhat.Api.Shared.Services.Ghtk;

public interface IGhtkService
{
    Task<GhtkCreateOrderResponse> CreateOrderAsync(
        GhtkCreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<GhtkCancelOrderResponse> CancelOrderAsync(
        string trackingOrder,
        CancellationToken cancellationToken = default);
}
