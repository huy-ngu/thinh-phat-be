namespace ThinhPhat.Api.Data.Entities;

public enum DeliveryOrderStatus
{
    Draft = 1,              // Nháp / Chưa đẩy vận đơn
    PendingPickup = 2,      // Chờ lấy hàng / Đã tiếp nhận
    Picking = 3,            // Đang lấy hàng
    InTransit = 4,          // Đang luân chuyển
    Delivering = 5,         // Đang giao hàng
    Delivered = 6,          // Giao thành công
    DeliveryFailed = 7,     // Giao hàng thất bại
    PendingRedelivery = 8,  // Chờ giao lại
    Returning = 9,          // Đang chuyển hoàn
    Returned = 10,          // Đã chuyển hoàn
    Cancelled = 11,         // Đã huỷ đơn
    LostOrDamaged = 12      // Thất lạc / Hư hại
}
