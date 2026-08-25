namespace ThinhPhat.Api.Shared.Services.GoogleSheets;

public interface IGoogleSheetService
{
    Task SendOrderToSheetAsync(string scriptUrl, GoogleSheetOrderPayload payload, CancellationToken cancellationToken = default);
}
