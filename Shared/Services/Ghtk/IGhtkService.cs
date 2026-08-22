namespace ThinhPhat.Api.Shared.Services.Ghtk;

public interface IGhtkService
{
    Task<GhtkCreateOrderResponse> CreateOrderAsync(
        GhtkCreateOrderRequest request,
        CancellationToken cancellationToken = default);
}
