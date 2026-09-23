namespace ThinhPhat.Api.Data.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? WarehouseId { get; set; }
    
    // ==========================================
    // Cấu trúc địa chỉ giao hàng chuẩn hóa
    // ==========================================
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public string? Address { get; set; }

    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public string? Note { get; set; }

    // ==========================================
    // Thông tin giao hàng & GHTK
    // ==========================================
    public string? GhtkId { get; set; }
    public decimal? ShippingFee { get; set; }
    public decimal? Weight { get; set; }
    public string? EstimatedPickTime { get; set; }
    public string? EstimatedDeliverTime { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
}
