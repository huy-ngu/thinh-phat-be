namespace ThinhPhat.Api.Data.Entities;

public sealed class DeliveryOrder
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? GhtkId { get; set; }
    public DeliveryOrderStatus Status { get; set; } = DeliveryOrderStatus.Draft;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Weight { get; set; }
    public string? Note { get; set; }

    // Thời gian lấy hàng và giao hàng dự kiến từ GHTK
    public string? EstimatedPickTime { get; set; }
    public string? EstimatedDeliverTime { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation properties
    public Order? Order { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<DeliveryOrderStatusHistory> StatusHistories { get; set; } = new List<DeliveryOrderStatusHistory>();
}
