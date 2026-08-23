namespace ThinhPhat.Api.Data.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; } // Giá bán lẻ
    public decimal CostPrice { get; set; } // Giá nhập vốn
    public string ImageUrl { get; set; } = string.Empty;
    public string? ImagePublicId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
