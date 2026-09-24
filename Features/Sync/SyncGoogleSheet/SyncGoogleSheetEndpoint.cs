using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Features.Orders.CreateOrder;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Sync.SyncGoogleSheet;

// 1. DTOs
public sealed record SyncGoogleSheetRequest(
    Guid ProductId,
    string PhoneNumber,
    string? CustomerName = null,
    string? Address = null,
    DateTime? CreatedAt = null,
    int Quantity = 1,
    Guid? WarehouseId = null,
    string? Note = null,
    string? SheetNote = null
);

public sealed record SyncGoogleSheetResponse(
    bool Success,
    string Message,
    Guid? OrderId,
    Guid? CustomerId,
    string? CustomerName,
    string? PhoneNumber,
    string? ProductName,
    Guid? WarehouseId,
    decimal? TotalAmount,
    DateTime? CreatedAtUtc
);

// 2. Request Validator
public sealed class SyncGoogleSheetValidator : AbstractValidator<SyncGoogleSheetRequest>
{
    public SyncGoogleSheetValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId là bắt buộc.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại là bắt buộc.")
            .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.");

        RuleFor(x => x.CustomerName)
            .MaximumLength(150).WithMessage("Tên khách hàng không được vượt quá 150 ký tự.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng sản phẩm phải lớn hơn 0.")
            .LessThanOrEqualTo(1000).WithMessage("Số lượng sản phẩm không được vượt quá 1000.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ không được vượt quá 300 ký tự.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");

        RuleFor(x => x.SheetNote)
            .MaximumLength(1000).WithMessage("Ghi chú Sheet không được vượt quá 1000 ký tự.");
    }
}

// 3. Endpoint Slice & Handler
public static class SyncGoogleSheetEndpoint
{
    public const string DefaultWarehouseIdString = "d396b002-4f56-497d-b56a-ebf2e71c05e5";

    public static RouteGroupBuilder MapSyncGoogleSheet(this RouteGroupBuilder group)
    {
        group.MapPost("/google-sheet", HandleAsync)
            .WithName("SyncGoogleSheet")
            .WithSummary("Nhận webhook đồng bộ đơn hàng từ Google Sheet (bảo mật bằng X-Sync-Key / X-Api-Key)")
            .AllowAnonymous()
            .WithRequestValidation<SyncGoogleSheetRequest>()
            .Produces<SyncGoogleSheetResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        SyncGoogleSheetRequest request,
        HttpContext httpContext,
        AppDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // 1. Xác thực bảo mật qua Header (X-Sync-Key hoặc X-Api-Key) hoặc Query Parameter (key)
        var expectedKey = configuration["Sync:GoogleSheetApiKey"]
            ?? configuration["Sync:ApiKey"]
            ?? "thinhphat_gsheet_sync_secret_key_2026";

        var providedKey = httpContext.Request.Headers["X-Sync-Key"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? httpContext.Request.Query["key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedKey) || !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized Sync Request",
                detail: "Khóa bảo mật đồng bộ (X-Sync-Key / X-Api-Key) không hợp lệ hoặc bị thiếu."
            );
        }

        // 2. Chuẩn hóa số điện thoại
        var cleanPhone = Regex.Replace(request.PhoneNumber.Trim(), @"[^\d+]", "");
        if (cleanPhone.StartsWith("84") && !cleanPhone.StartsWith("+84") && cleanPhone.Length > 9)
        {
            cleanPhone = "0" + cleanPhone[2..];
        }
        else if (cleanPhone.StartsWith("+84"))
        {
            cleanPhone = "0" + cleanPhone[3..];
        }
        else if (cleanPhone.Length > 0 && !cleanPhone.StartsWith("0"))
        {
            cleanPhone = "0" + cleanPhone;
        }

        // Tên khách hàng: nếu trống hoặc không có thì lưu mặc định là "a"
        var customerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "a" : request.CustomerName.Trim();

        // 3. Tìm hoặc tự động tạo Khách hàng
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == cleanPhone, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = customerName,
                PhoneNumber = cleanPhone,
                CreatedAtUtc = DateTime.UtcNow
            };
            await db.Customers.AddAsync(customer, cancellationToken);
        }
        else if (customer.Name == "a" && customerName != "a")
        {
            // Cập nhật tên mới nếu trước đó đang là tên tạm "a"
            customer.Name = customerName;
        }

        // 4. Kiểm tra và lấy thông tin Sản phẩm
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: $"Sản phẩm với ID '{request.ProductId}' không tồn tại trong hệ thống."
            );
        }

        // 5. Xác định và kiểm tra Kho hàng (Mặc định là d396b002-4f56-497d-b56a-ebf2e71c05e5 nếu không truyền)
        var defaultWhSetting = configuration["Sync:DefaultWarehouseId"] ?? DefaultWarehouseIdString;
        Guid? targetWarehouseId = request.WarehouseId;

        if (!targetWarehouseId.HasValue && Guid.TryParse(defaultWhSetting, out var parsedDefaultWh))
        {
            targetWarehouseId = parsedDefaultWh;
        }

        if (targetWarehouseId.HasValue)
        {
            var warehouseExists = await db.Warehouses
                .AnyAsync(w => w.Id == targetWarehouseId.Value, cancellationToken);

            if (!warehouseExists)
            {
                if (request.WarehouseId.HasValue)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Warehouse Not Found",
                        detail: $"Kho hàng với ID '{targetWarehouseId.Value}' không tồn tại trong hệ thống."
                    );
                }
                else
                {
                    // Nếu default warehouse chưa có trong DB, fallback lấy kho đầu tiên (nếu có)
                    var fallbackWh = await db.Warehouses
                        .Select(w => (Guid?)w.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    targetWarehouseId = fallbackWh;
                }
            }
        }

        // 6. Tính toán tiền và khởi tạo đơn hàng
        var quantity = request.Quantity > 0 ? request.Quantity : 1;
        
        // Tự động bóc tách giá tiền từ Cột E (SheetNote) nếu có (ví dụ: "GIÁ: 1.750.000đ", "giá: 2.100.000đ")
        var extractedPrice = TryExtractPriceFromSheetNote(request.SheetNote);
        decimal totalAmount;
        decimal unitPrice;

        if (extractedPrice.HasValue && extractedPrice.Value > 0)
        {
            totalAmount = extractedPrice.Value;
            unitPrice = totalAmount / quantity;
        }
        else
        {
            unitPrice = product.Price;
            totalAmount = unitPrice * quantity;
        }

        var orderId = Guid.NewGuid();

        var orderItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = product.Id,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = totalAmount
        };

        // 7. Xử lý thời gian tạo đơn: nếu có truyền từ Sheet thì lấy thời gian đó (quy đổi về UTC), nếu rỗng thì lấy thời gian hiện tại
        DateTime orderCreatedAt;
        if (request.CreatedAt.HasValue)
        {
            var dt = request.CreatedAt.Value;
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                // Thời gian nhập trên Google Sheet thường là giờ Việt Nam (UTC+7) -> chuyển về chuẩn UTC
                orderCreatedAt = DateTime.SpecifyKind(dt.AddHours(-7), DateTimeKind.Utc);
            }
            else
            {
                orderCreatedAt = dt.ToUniversalTime();
            }
        }
        else
        {
            orderCreatedAt = DateTime.UtcNow;
        }

        var order = new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            WarehouseId = targetWarehouseId,
            Province = null,
            District = null,
            Ward = null,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            TotalAmount = totalAmount,
            ShippingFee = null,
            Weight = 3m,
            Status = OrderStatus.New, // -2 Mới tạo
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SheetNote = string.IsNullOrWhiteSpace(request.SheetNote) ? null : request.SheetNote.Trim(),
            CreatedAtUtc = orderCreatedAt,
            Items = [orderItem]
        };

        // 8. Khởi tạo lịch sử trạng thái ban đầu
        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = order.Status,
            Note = $"Khởi tạo đơn hàng từ Google Sheet: {CreateOrderEndpoint.GetStatusDisplayName(order.Status)}",
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Orders.AddAsync(order, cancellationToken);
        await db.OrderStatusHistories.AddAsync(history, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 9. Trả về kết quả
        var response = new SyncGoogleSheetResponse(
            Success: true,
            Message: "Đồng bộ đơn hàng từ Google Sheet thành công.",
            OrderId: order.Id,
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            ProductName: product.Name,
            WarehouseId: order.WarehouseId,
            TotalAmount: order.TotalAmount,
            CreatedAtUtc: order.CreatedAtUtc
        );

        return TypedResults.Created($"/api/v1/orders/{order.Id}", response);
    }

    /// <summary>
    /// Bóc tách số tiền từ nội dung text cột E (SheetNote).
    /// Hỗ trợ các định dạng:
    /// - "Mua 1 MÁY THÁI RAU CỦ GIÁ: 1.750.000đ (Miễn phí vận chuyển)" -> 1750000
    /// - "Mua 1 Máy cưa xích + sào nối 2m7 + 1 Pin 20cell giá: 2.100.000đ" -> 2100000
    /// - "Giá: 500k", "Giá: 1,500,000 VND", "1.750.000đ"
    /// </summary>
    public static decimal? TryExtractPriceFromSheetNote(string? sheetNote)
    {
        if (string.IsNullOrWhiteSpace(sheetNote))
        {
            return null;
        }

        // 1. Tìm mẫu ưu tiên: "giá", "gia", "price" theo sau bởi dấu hai chấm / khoảng trắng và dãy số
        var match = Regex.Match(
            sheetNote,
            @"(?:gi[áa]|price)\s*[:=\-]?\s*([\d\.,]+(?:\s*[kK])?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        // 2. Fallback nếu không có từ khóa "giá" nhưng có mẫu số tiền rõ ràng kèm "đ", "vnđ", "vnd"
        if (!match.Success)
        {
            match = Regex.Match(
                sheetNote,
                @"(?:^|\s)([\d\.,]{4,}(?:\s*[kK])?)\s*(?:đ|vnđ|vnd)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
        }

        if (!match.Success)
        {
            return null;
        }

        var rawValue = match.Groups[1].Value.Trim();

        // Xử lý hậu tố 'k' hoặc 'K' (ví dụ: 500k -> 500.000)
        var isThousandMultiplier = false;
        if (rawValue.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            isThousandMultiplier = true;
            rawValue = rawValue[..^1].Trim();
        }

        // Loại bỏ các ký tự không phải số (dấu chấm, phẩy ngăn cách hàng nghìn)
        var cleanDigits = Regex.Replace(rawValue, @"[^\d]", "");
        if (decimal.TryParse(cleanDigits, out var parsedNumber))
        {
            if (isThousandMultiplier)
            {
                parsedNumber *= 1000m;
            }

            if (parsedNumber > 0)
            {
                return parsedNumber;
            }
        }

        return null;
    }
}
