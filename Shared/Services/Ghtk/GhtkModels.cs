using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThinhPhat.Api.Shared.Services.Ghtk;

public sealed record GhtkProductItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("weight")] double Weight,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("product_code")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProductCode = null
);

public sealed record GhtkOrderInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("pick_name")] string PickName,
    [property: JsonPropertyName("pick_address")] string PickAddress,
    [property: JsonPropertyName("pick_province")] string PickProvince,
    [property: JsonPropertyName("pick_district")] string PickDistrict,
    [property: JsonPropertyName("pick_ward")] string? PickWard,
    [property: JsonPropertyName("pick_tel")] string PickTel,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("district")] string District,
    [property: JsonPropertyName("ward")] string? Ward,
    [property: JsonPropertyName("street")] string? Street,
    [property: JsonPropertyName("hamlet")] string? Hamlet,
    [property: JsonPropertyName("tel")] string Tel,
    [property: JsonPropertyName("is_freeship")] int? IsFreeship,
    [property: JsonPropertyName("pick_date")] string? PickDate,
    [property: JsonPropertyName("transport")] string? Transport,
    [property: JsonPropertyName("pick_option")] string? PickOption,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("pick_money")] decimal PickMoney,
    [property: JsonPropertyName("pick_street")] string? PickStreet = null
);

public sealed record GhtkCreateOrderRequest(
    [property: JsonPropertyName("products")] List<GhtkProductItem> Products,
    [property: JsonPropertyName("order")] GhtkOrderInfo Order
);

public sealed record GhtkCreateOrderResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("order")] GhtkOrderResponseData? Order,
    [property: JsonPropertyName("warning_message")] string? WarningMessage = null
);

public sealed record GhtkOrderResponseData(
    [property: JsonPropertyName("partner_id")] string? PartnerId,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("fee")] object? RawFee,
    [property: JsonPropertyName("insurance_fee")] object? RawInsuranceFee,
    [property: JsonPropertyName("tracking_id")] object? TrackingId,
    [property: JsonPropertyName("estimated_pick_time")] string? EstimatedPickTime,
    [property: JsonPropertyName("estimated_deliver_time")] string? EstimatedDeliverTime,
    [property: JsonPropertyName("status_id")] int? StatusId
)
{
    [JsonIgnore]
    public decimal? Fee => ParseDecimal(RawFee);

    [JsonIgnore]
    public decimal? InsuranceFee => ParseDecimal(RawInsuranceFee);

    private static decimal? ParseDecimal(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decVal)) return decVal;
            if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out var strVal)) return strVal;
        }
        if (decimal.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }
}
