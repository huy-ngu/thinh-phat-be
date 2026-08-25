using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThinhPhat.Api.Shared.Services.Ghtk;

public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) return l.ToString();
                if (reader.TryGetDecimal(out var d)) return d.ToString();
                if (reader.TryGetDouble(out var db)) return db.ToString();
                return reader.GetDecimal().ToString();
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            default:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.ToString();
                }
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

public sealed class FlexibleDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                return decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

public sealed class FlexibleIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetInt32();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                return int.TryParse(str, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

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
    [property: JsonPropertyName("message")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? Message,

    [property: JsonPropertyName("order")] GhtkOrderResponseData? Order,

    [property: JsonPropertyName("warning_message")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? WarningMessage = null
);

public sealed record GhtkOrderResponseData(
    [property: JsonPropertyName("partner_id")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? PartnerId,

    [property: JsonPropertyName("label")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? Label,

    [property: JsonPropertyName("area")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? Area,

    [property: JsonPropertyName("fee")]
    [property: JsonConverter(typeof(FlexibleDecimalConverter))]
    decimal? Fee,

    [property: JsonPropertyName("insurance_fee")]
    [property: JsonConverter(typeof(FlexibleDecimalConverter))]
    decimal? InsuranceFee,

    [property: JsonPropertyName("tracking_id")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? TrackingId,

    [property: JsonPropertyName("estimated_pick_time")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? EstimatedPickTime,

    [property: JsonPropertyName("estimated_deliver_time")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? EstimatedDeliverTime,

    [property: JsonPropertyName("status_id")]
    [property: JsonConverter(typeof(FlexibleIntConverter))]
    int? StatusId
);

public sealed record GhtkCancelOrderResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? Message,

    [property: JsonPropertyName("log_id")]
    [property: JsonConverter(typeof(FlexibleStringConverter))]
    string? LogId = null
);
