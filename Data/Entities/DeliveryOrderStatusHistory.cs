namespace ThinhPhat.Api.Data.Entities;

public sealed class DeliveryOrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid DeliveryOrderId { get; set; }
    public DeliveryOrderStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public DeliveryOrder? DeliveryOrder { get; set; }
}
