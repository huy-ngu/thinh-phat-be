namespace ThinhPhat.Api.Data.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Order? Order { get; set; }
}
