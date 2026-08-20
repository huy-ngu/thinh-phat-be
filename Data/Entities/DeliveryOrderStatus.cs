namespace ThinhPhat.Api.Data.Entities;

public enum DeliveryOrderStatus
{
    Draft = 0,                                // Nháp
    Cancelled = -1,                           // Hủy đơn hàng
    Pending = 1,                              // Chưa tiếp nhận
    Accepted = 2,                             // Đã tiếp nhận
    PickedOrStocked = 3,                      // Đã lấy hàng/Đã nhập kho
    Delivering = 4,                           // Đã điều phối giao hàng/Đang giao hàng
    DeliveredUnreconciled = 5,                // Đã giao hàng/Chưa đối soát
    Reconciled = 6,                           // Đã đối soát
    CannotPick = 7,                           // Không lấy được hàng
    DelayPick = 8,                            // Hoãn lấy hàng
    CannotDeliver = 9,                        // Không giao được hàng
    DelayDelivery = 10,                       // Delay giao hàng
    ReturnReconciled = 11,                    // Đã đối soát công nợ trả hàng
    Picking = 12,                             // Đã điều phối lấy hàng/Đang lấy hàng
    Compensated = 13,                         // Đơn hàng bồi hoàn
    Returning = 20,                           // Đang trả hàng (COD cầm hàng đi trả)
    Returned = 21,                            // Đã trả hàng (COD đã trả xong hàng)
    // ShipperReportedPicked = 123,              // Shipper báo đã lấy hàng
    // ShipperReportedCannotPick = 127,          // Shipper (nhân viên lấy/giao hàng) báo không lấy được hàng
    // ShipperReportedDelayPick = 128,           // Shipper báo delay lấy hàng
    // ShipperReportedDelivered = 45,            // Shipper báo đã giao hàng
    // ShipperReportedCannotDeliver = 49,        // Shipper báo không giao được giao hàng
    // ShipperReportedDelayDelivery = 410        // Shipper báo delay giao hàng
}
