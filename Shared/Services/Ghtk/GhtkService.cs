using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThinhPhat.Api.Shared.Services.Ghtk;

public sealed class GhtkService : IGhtkService
{
    private readonly HttpClient _httpClient;
    private readonly GhtkOptions _options;
    private readonly ILogger<GhtkService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
        Converters =
        {
            new FlexibleStringConverter(),
            new FlexibleDecimalConverter(),
            new FlexibleIntConverter()
        }
    };

    public GhtkService(
        HttpClient httpClient,
        IOptions<GhtkOptions> options,
        ILogger<GhtkService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GhtkCreateOrderResponse> CreateOrderAsync(
        GhtkCreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "services/shipment/order");
        
        // Headers theo yêu cầu của GHTK
        httpRequest.Headers.Add("Token", _options.ApiToken);
        if (!string.IsNullOrWhiteSpace(_options.PartnerCode))
        {
            httpRequest.Headers.Add("X-Client-Source", _options.PartnerCode);
        }

        var jsonPayload = JsonSerializer.Serialize(request);
        httpRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending create order request to GHTK. OrderId: {OrderId}", request.Order.Id);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GHTK API returned non-success code: {StatusCode}. Response: {Response}", response.StatusCode, responseContent);
        }

        try
        {
            var result = JsonSerializer.Deserialize<GhtkCreateOrderResponse>(responseContent, JsonOptions);
            return result ?? new GhtkCreateOrderResponse(false, "Không thể đọc dữ liệu phản hồi từ GHTK", null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GHTK response: {Response}", responseContent);
            return new GhtkCreateOrderResponse(false, $"Lỗi xử lý phản hồi từ GHTK: {ex.Message}", null);
        }
    }

    public async Task<GhtkCancelOrderResponse> CancelOrderAsync(
        string trackingOrder,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"services/shipment/cancel/{trackingOrder}");

        // Headers theo yêu cầu của GHTK
        httpRequest.Headers.Add("Token", _options.ApiToken);
        if (!string.IsNullOrWhiteSpace(_options.PartnerCode))
        {
            httpRequest.Headers.Add("X-Client-Source", _options.PartnerCode);
        }

        _logger.LogInformation("Sending cancel order request to GHTK. TrackingOrder: {TrackingOrder}", trackingOrder);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GHTK Cancel API returned non-success code: {StatusCode}. Response: {Response}", response.StatusCode, responseContent);
        }

        try
        {
            var result = JsonSerializer.Deserialize<GhtkCancelOrderResponse>(responseContent, JsonOptions);
            return result ?? new GhtkCancelOrderResponse(false, "Không thể đọc dữ liệu phản hồi từ GHTK", null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GHTK cancel response: {Response}", responseContent);
            return new GhtkCancelOrderResponse(false, $"Lỗi xử lý phản hồi từ GHTK: {ex.Message}", null);
        }
    }
}

