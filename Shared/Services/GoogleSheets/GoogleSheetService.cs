using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ThinhPhat.Api.Shared.Services.GoogleSheets;

public sealed class GoogleSheetService : IGoogleSheetService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleSheetService> _logger;

    public const string HttpClientName = "GoogleSheetClient";

    public GoogleSheetService(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleSheetService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendOrderToSheetAsync(
        string scriptUrl,
        GoogleSheetOrderPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptUrl))
        {
            _logger.LogWarning("Google Sheet Script URL is null or empty. Skipping sync for customer: {CustomerName}", payload.CustomerName);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            
            // Google Apps Script Web App nhận POST với JSON payload
            var response = await client.PostAsJsonAsync(scriptUrl.Trim(), payload, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully synced order to Google Sheet for customer: {CustomerName} ({Phone})", payload.CustomerName, payload.PhoneNumber);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to sync order to Google Sheet. Status: {StatusCode}, Detail: {Detail}", response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            // Fail-safe: ghi log lỗi nhưng không throw để tránh làm sập luồng chính
            _logger.LogError(ex, "Error occurred while syncing order to Google Sheet URL: {Url}", scriptUrl);
        }
    }
}
