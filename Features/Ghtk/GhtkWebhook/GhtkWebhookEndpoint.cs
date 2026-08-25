using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Services.Ghtk;

namespace ThinhPhat.Api.Features.Ghtk.GhtkWebhook;

public sealed record GhtkWebhookPayload
{
    [JsonPropertyName("label_id")]
    public string? LabelId { get; init; }

    [JsonPropertyName("partner_id")]
    public string? PartnerId { get; init; }

    [JsonPropertyName("status_id")]
    public int? StatusId { get; init; }

    [JsonPropertyName("action_time")]
    public string? ActionTime { get; init; }

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; init; }

    [JsonPropertyName("fee")]
    public decimal? Fee { get; init; }

    [JsonPropertyName("return_part_package")]
    public int? ReturnPartPackage { get; init; }

    [JsonPropertyName("pick_money")]
    public decimal? PickMoney { get; init; }
}

public sealed record GhtkWebhookResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message
);

public static class GhtkWebhookEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static IEndpointRouteBuilder MapGhtkWebhook(this IEndpointRouteBuilder app)
    {
        // 1. Endpoint chính thức theo chuẩn RESTful
        app.MapPost("/webhook", HandleWebhookAsync)
            .WithName("GhtkWebhook")
            .WithSummary("Receives shipment status updates from GHTK via application/x-www-form-urlencoded or JSON")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<GhtkWebhookResponse>(StatusCodes.Status200OK);

        // 2. Alias route phù hợp với callback link ví dụ trong tài liệu GHTK (updateShipment)
        app.MapPost("/updateShipment", HandleWebhookAsync)
            .WithName("GhtkUpdateShipmentAlias")
            .WithSummary("Alias endpoint for GHTK callback URL (e.g. /updateShipment?hash=XXX)")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<GhtkWebhookResponse>(StatusCodes.Status200OK);

        return app;
    }

    public static async Task<IResult> HandleWebhookAsync(
        HttpContext httpContext,
        AppDbContext db,
        IOptions<GhtkOptions> ghtkOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("GhtkWebhook");

        try
        {
            var options = ghtkOptions.Value;

            // 1. Kiểm tra xác thực hash bảo mật (nếu được cấu hình trong Ghtk:WebhookHash)
            if (!string.IsNullOrWhiteSpace(options.WebhookHash))
            {
                var queryHash = httpContext.Request.Query["hash"].FirstOrDefault();
                var headerHash = httpContext.Request.Headers["X-Webhook-Hash"].FirstOrDefault()
                                 ?? httpContext.Request.Headers["X-GHTK-Hash"].FirstOrDefault();

                var incomingHash = queryHash ?? headerHash;
                if (!string.Equals(incomingHash, options.WebhookHash, StringComparison.Ordinal))
                {
                    logger.LogWarning("Rejected GHTK webhook call due to invalid or missing hash. Incoming: {IncomingHash}", incomingHash);
                    return TypedResults.Ok(new GhtkWebhookResponse(false, "Chữ ký bảo mật (hash) không hợp lệ."));
                }
            }

            // 2. Trích xuất và phân tích payload
            var payload = await ExtractPayloadAsync(httpContext, cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("GHTK Webhook received empty or unparseable payload.");
                return TypedResults.Ok(new GhtkWebhookResponse(false, "Payload rỗng hoặc không đúng định dạng."));
            }

            logger.LogInformation(
                "Received GHTK Webhook: LabelId={LabelId}, PartnerId={PartnerId}, StatusId={StatusId}, ActionTime={ActionTime}, Reason={Reason}, Fee={Fee}, Weight={Weight}, PickMoney={PickMoney}",
                payload.LabelId, payload.PartnerId, payload.StatusId, payload.ActionTime, payload.Reason, payload.Fee, payload.Weight, payload.PickMoney);

            // 3. Tìm Order tương ứng
            var order = await FindOrderAsync(db, payload, cancellationToken);
            if (order is null)
            {
                logger.LogWarning(
                    "GHTK Webhook could not find matching Order for LabelId: '{LabelId}', PartnerId: '{PartnerId}'",
                    payload.LabelId, payload.PartnerId);

                // Trả về HTTP 200 OK để GHTK không retry liên tục đơn hàng không tồn tại
                return TypedResults.Ok(new GhtkWebhookResponse(true, "Tiếp nhận webhook thành công nhưng không tìm thấy đơn hàng tương ứng trong hệ thống."));
            }

            // 4. Cập nhật thông tin Order
            if (!string.IsNullOrWhiteSpace(payload.LabelId))
            {
                order.GhtkId = payload.LabelId.Trim();
            }

            if (payload.Fee.HasValue)
            {
                order.ShippingFee = payload.Fee.Value;
            }

            if (payload.Weight.HasValue && payload.Weight.Value > 0)
            {
                order.Weight = payload.Weight.Value;
            }

            // Cập nhật trạng thái
            var isStatusValid = payload.StatusId.HasValue && Enum.IsDefined(typeof(OrderStatus), payload.StatusId.Value);
            var newStatus = isStatusValid ? (OrderStatus)payload.StatusId!.Value : order.Status;

            if (isStatusValid)
            {
                order.Status = newStatus;
            }

            if (!string.IsNullOrWhiteSpace(payload.Reason))
            {
                order.Note = payload.Reason.Trim();
            }

            order.UpdatedAtUtc = DateTime.UtcNow;

            // 5. Xác định thời gian diễn ra sự kiện (action_time ISO 8601)
            var eventTimeUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(payload.ActionTime) &&
                DateTimeOffset.TryParse(payload.ActionTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOffset))
            {
                eventTimeUtc = parsedOffset.UtcDateTime;
            }

            // 6. Tạo nội dung lịch sử trạng thái (OrderStatusHistory)
            var statusDisplayName = CreateOrderEndpoint.GetStatusDisplayName(newStatus);
            var noteComponents = new List<string> { $"GHTK cập nhật: {statusDisplayName}" };

            if (!string.IsNullOrWhiteSpace(payload.Reason))
            {
                noteComponents.Add($"Lý do: {payload.Reason.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(payload.ReasonCode))
            {
                noteComponents.Add($"Mã lý do: {payload.ReasonCode.Trim()}");
            }

            if (payload.ReturnPartPackage == 1)
            {
                noteComponents.Add("Giao hàng một phần");
            }

            if (payload.Fee.HasValue)
            {
                noteComponents.Add($"Phí: {payload.Fee.Value:N0}đ");
            }

            if (payload.PickMoney.HasValue && payload.PickMoney.Value > 0)
            {
                noteComponents.Add($"Thu hộ COD: {payload.PickMoney.Value:N0}đ");
            }

            var historyNote = string.Join(" | ", noteComponents);

            // Kiểm tra tính Idempotent: tránh trùng lặp bản ghi lịch sử nếu GHTK retry
            var isDuplicateHistory = order.StatusHistories.Any(h =>
                h.Status == newStatus &&
                (Math.Abs((h.CreatedAtUtc - eventTimeUtc).TotalSeconds) < 60 || h.Note == historyNote));

            if (!isDuplicateHistory)
            {
                var history = new OrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = newStatus,
                    Note = historyNote,
                    CreatedAtUtc = eventTimeUtc
                };

                await db.OrderStatusHistories.AddAsync(history, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Successfully synchronized GHTK status for Order {Id} ({GhtkId}) to '{StatusName}' ({StatusId})",
                order.Id, order.GhtkId, statusDisplayName, payload.StatusId);

            return TypedResults.Ok(new GhtkWebhookResponse(true, $"Đồng bộ trạng thái GHTK thành công: {statusDisplayName}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while processing GHTK Webhook.");
            return TypedResults.Ok(new GhtkWebhookResponse(false, $"Lỗi xử lý webhook: {ex.Message}"));
        }
    }

    private static async Task<Order?> FindOrderAsync(
        AppDbContext db,
        GhtkWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        // 1. Tìm theo GhtkId == label_id (mã vận đơn GHTK)
        if (!string.IsNullOrWhiteSpace(payload.LabelId))
        {
            var cleanLabel = payload.LabelId.Trim();
            var order = await db.Orders
                .Include(o => o.StatusHistories)
                .FirstOrDefaultAsync(o => o.GhtkId == cleanLabel, cancellationToken);

            if (order is not null) return order;
        }

        // 2. Tìm theo partner_id (mã đơn hàng phía hệ thống)
        if (!string.IsNullOrWhiteSpace(payload.PartnerId))
        {
            var cleanPartnerId = payload.PartnerId.Trim();

            // 2.1 Nếu partner_id là Guid -> kiểm tra Order.Id
            if (Guid.TryParse(cleanPartnerId, out var partnerGuid))
            {
                var order = await db.Orders
                    .Include(o => o.StatusHistories)
                    .FirstOrDefaultAsync(o => o.Id == partnerGuid, cancellationToken);

                if (order is not null) return order;
            }

            // 2.2 Kiểm tra partner_id lưu dưới dạng GhtkId
            var byGhtkId = await db.Orders
                .Include(o => o.StatusHistories)
                .FirstOrDefaultAsync(o => o.GhtkId == cleanPartnerId, cancellationToken);

            if (byGhtkId is not null) return byGhtkId;
        }

        return null;
    }

    public static async Task<GhtkWebhookPayload?> ExtractPayloadAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = httpContext.Request;

        // Trường hợp 1: application/x-www-form-urlencoded hoặc multipart/form-data (Chuẩn chính thức của GHTK)
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            return ParseFromForm(form);
        }

        // Trường hợp 2: application/json (Dành cho kiểm thử hoặc webhook proxy)
        if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                request.EnableBuffering();
                if (request.Body.CanSeek)
                {
                    request.Body.Position = 0;
                }

                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var jsonText = await reader.ReadToEndAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    return JsonSerializer.Deserialize<GhtkWebhookPayload>(jsonText, JsonOptions);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Trường hợp 3: Thử đọc từ query string nếu là GET hoặc fallback
        if (request.Query.Count > 0)
        {
            return ParseFromQuery(request.Query);
        }

        return null;
    }

    private static GhtkWebhookPayload ParseFromForm(IFormCollection form)
    {
        return new GhtkWebhookPayload
        {
            LabelId = GetValue(form, "label_id", "labelId", "label"),
            PartnerId = GetValue(form, "partner_id", "partnerId", "id"),
            StatusId = ParseInt(GetValue(form, "status_id", "statusId", "status")),
            ActionTime = GetValue(form, "action_time", "actionTime"),
            ReasonCode = GetValue(form, "reason_code", "reasonCode"),
            Reason = GetValue(form, "reason", "Reason"),
            Weight = ParseDecimal(GetValue(form, "weight", "Weight")),
            Fee = ParseDecimal(GetValue(form, "fee", "Fee")),
            ReturnPartPackage = ParseInt(GetValue(form, "return_part_package", "returnPartPackage")),
            PickMoney = ParseDecimal(GetValue(form, "pick_money", "pickMoney", "pick_amount"))
        };
    }

    private static GhtkWebhookPayload ParseFromQuery(IQueryCollection query)
    {
        return new GhtkWebhookPayload
        {
            LabelId = GetValue(query, "label_id", "labelId", "label"),
            PartnerId = GetValue(query, "partner_id", "partnerId", "id"),
            StatusId = ParseInt(GetValue(query, "status_id", "statusId", "status")),
            ActionTime = GetValue(query, "action_time", "actionTime"),
            ReasonCode = GetValue(query, "reason_code", "reasonCode"),
            Reason = GetValue(query, "reason", "Reason"),
            Weight = ParseDecimal(GetValue(query, "weight", "Weight")),
            Fee = ParseDecimal(GetValue(query, "fee", "Fee")),
            ReturnPartPackage = ParseInt(GetValue(query, "return_part_package", "returnPartPackage")),
            PickMoney = ParseDecimal(GetValue(query, "pick_money", "pickMoney", "pick_amount"))
        };
    }

    private static string? GetValue(IFormCollection form, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val.ToString();
            }
        }
        return null;
    }

    private static string? GetValue(IQueryCollection query, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val.ToString();
            }
        }
        return null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleanValue = value.Trim().Replace(',', '.');
        return decimal.TryParse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}
