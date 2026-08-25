namespace ThinhPhat.Api.Data.Entities;

public enum OrderStatus
{
    // ==========================================
    // 1. TRẠNG THÁI TELESALE (Số Âm & 0)
    // ==========================================
    Done = 0,                                 // Đã chốt thành công
    New = -1,                                 // Mới tạo - Chờ nhân viên gọi điện
    NoAnswer = -2,                            // Không nghe máy
    CallbackLater = -3,                       // Hẹn gọi lại sau
    Cancelled = -4,                           // Khách hủy / Không mua
    OutOfStock = -5,                          // Hết hàng
    Duplicate = -6,                           // Trùng đơn

    // ==========================================
    // 2. TRẠNG THÁI GIAO VẬN GHTK (Khớp 100% mã status_id của GHTK)
    // ==========================================
    Confirmed = 1,                            // Đã chốt đơn / Chưa tiếp nhận (GHTK: 1)
    Accepted = 2,                             // Đã tiếp nhận (GHTK: 2)
    PickedOrStocked = 3,                      // Đã lấy hàng / Đã nhập kho (GHTK: 3)
    Delivering = 4,                           // Đang giao hàng (GHTK: 4)
    DeliveredUnreconciled = 5,                // Đã giao hàng / Chưa đối soát (GHTK: 5)
    Reconciled = 6,                           // Đã đối soát (GHTK: 6)
    CannotPick = 7,                           // Không lấy được hàng (GHTK: 7)
    DelayPick = 8,                            // Hoãn lấy hàng (GHTK: 8)
    CannotDeliver = 9,                        // Không giao được hàng (GHTK: 9)
    DelayDelivery = 10,                       // Delay giao hàng (GHTK: 10)
    ReturnReconciled = 11,                    // Đã đối soát công nợ trả hàng (GHTK: 11)
    Picking = 12,                             // Đang lấy hàng (GHTK: 12)
    Compensated = 13,                         // Đơn hàng bồi hoàn (GHTK: 13)
    Returning = 20,                           // Đang trả hàng (COD cầm hàng đi trả - GHTK: 20)
    Returned = 21,                            // Đã trả hàng (COD đã trả xong hàng - GHTK: 21)
    ShipperReportedPicked = 123,              // Shipper báo đã lấy hàng (GHTK: 123)
    ShipperReportedCannotPick = 127,          // Shipper báo không lấy được hàng (GHTK: 127)
    ShipperReportedDelayPick = 128,           // Shipper báo delay lấy hàng (GHTK: 128)
    ShipperReportedDelivered = 45,            // Shipper báo đã giao hàng (GHTK: 45)
    ShipperReportedCannotDeliver = 49,        // Shipper báo không giao được hàng (GHTK: 49)
    ShipperReportedDelayDelivery = 410        // Shipper báo delay giao hàng (GHTK: 410)
}
