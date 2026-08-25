using System.Text.Json.Serialization;

namespace ThinhPhat.Api.Shared.Services.GoogleSheets;

public sealed record GoogleSheetOrderPayload(
    [property: JsonPropertyName("orderTime")] string OrderTime,
    [property: JsonPropertyName("customerName")] string CustomerName,
    [property: JsonPropertyName("phoneNumber")] string PhoneNumber,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("products")] string Products,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("totalAmount")] string TotalAmount
);
