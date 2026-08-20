namespace ThinhPhat.Api.Data.Entities;

public sealed class DeliveryOrder
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? GhtkId { get; set; }
    public DeliveryOrderStatus Status { get; set; } = DeliveryOrderStatus.Draft;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Weight { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation properties
    public Order? Order { get; set; }
    public ICollection<DeliveryOrderStatusHistory> StatusHistories { get; set; } = new List<DeliveryOrderStatusHistory>();
}
