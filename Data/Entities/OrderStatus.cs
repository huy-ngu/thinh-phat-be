namespace ThinhPhat.Api.Data.Entities;

public enum OrderStatus
{
    New = 1,          // Mới tạo
    Confirmed = 2,    // Chốt đơn
    NoAnswer = 3,     // Không nghe máy
    Cancelled = 4,    // Không mua
    Uploaded = 5,     // Đã up đơn
    OutOfStock = 6    // Hết hàng
}
