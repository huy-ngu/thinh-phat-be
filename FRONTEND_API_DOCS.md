# 📘 TÀI LIỆU API & ENUMS DÀNH CHO FRONTEND (ThinhPhat System)

Tài liệu tổng hợp danh sách **Enums / Constants** và toàn bộ **Endpoints API** của hệ thống backend **ThinhPhat.Api** để đội ngũ Frontend dễ dàng tích hợp và định nghĩa TypeScript types/models.

> [!IMPORTANT]
> **Quy Tắc Phân Quyền (RBAC - Role Based Access Control):**
> - **Public:** Các API xác thực cơ bản (`Login`, `Register`, `Refresh`, `Revoke`), xem sản phẩm (`Get Products`) và Webhook tiếp nhận callback từ GHTK.
> - **Bearer (Authenticated):** Người dùng đăng nhập có thể xem danh sách / chi tiết dữ liệu (Khách hàng, Đơn hàng, Kho hàng, Thông tin cá nhân `/me`).
> - **Admin Only:** Toàn bộ các thao tác **Thêm mới (Create), Chỉnh sửa (Update), Cập nhật trạng thái (Patch Status), Xóa (Delete)** trên Khách hàng, Sản phẩm, Đơn hàng, Kho hàng và **Đẩy đơn GHTK (`/api/v1/ghtk/shipment/order`)** đều **BẮT BUỘC có Role `Admin`** trong JWT Token. Nếu tài khoản có Role `User` cố gắng thực hiện sẽ nhận mã lỗi `403 Forbidden`.

---

## 📌 MỤC LỤC
1. [Quy Chuẩn Chung & Xác Thực (Authentication)](#1-quy-chuẩn-chung--xác-thực-authentication)
2. [Cấu Trúc Phản Hồi Lỗi (Error Responses)](#2-cấu-trúc-phản-hồi-lỗi-error-responses)
3. [Tổng Hợp Enums & Constants (TypeScript Definitions)](#3-tổng-hợp-enums--constants-typescript-definitions)
4. [Danh Sách Chi Tiết Endpoints API](#4-danh-sách-chi-tiết-endpoints-api)
   - [4.1 Authentication (`/api/v1/auth`)](#41-authentication-apiv1auth)
   - [4.2 Customers (`/api/v1/customers`)](#42-customers-apiv1customers)
   - [4.3 Products (`/api/v1/products`)](#43-products-apiv1products)
   - [4.4 Orders - Đơn Hàng Tích Hợp (`/api/v1/orders`)](#44-orders---đơn-hàng-tích-hợp-apiv1orders)
   - [4.5 Warehouses - Kho Hàng (`/api/v1/warehouses`)](#45-warehouses---kho-hàng-apiv1warehouses)
   - [4.6 GHTK Integration (`/api/v1/ghtk`)](#46-ghtk-integration-apiv1ghtk)
   - [4.7 System Health Check](#47-system-health-check)
5. [Bảng Tổng Hợp Nhanh (Cheat Sheet)](#5-bảng-tổng-hợp-nhanh-cheat-sheet)

---

## 1. Quy Chuẩn Chung & Xác Thực (Authentication)

- **Base URL (Local/Dev):** `http://localhost:5000` hoặc `https://localhost:5001`
- **Prefix:** `/api/v1`
- **Default Format:** `application/json` (trừ upload file dùng `multipart/form-data`).
- **Authorization Header:**
  ```http
  Authorization: Bearer <AccessToken>
  ```
- **Access Token Expiry:** `15 phút` (900 giây).
- **Refresh Token Expiry:** `7 ngày`.
- **Cơ chế Token Rotation & Security:**
  - Khi token hết hạn (HTTP 401), Frontend tự động gọi `POST /api/v1/auth/refresh` kèm `refreshToken`.
  - Backend sử dụng cơ chế xoay vòng Refresh Token (Token Rotation) và phát hiện token bị đánh cắp (Compromise Detection).
- **Quy tắc phân trang chuẩn (Pagination):**
  - Query parameters: `page` (mặc định 1, min 1), `pageSize` (mặc định 20, max 100).
  - Cấu trúc trả về phân trang:
    ```typescript
    interface PaginatedResponse<T> {
      items: T[];
      totalCount: number;
      page: number;
      pageSize: number;
    }
    ```

---

## 2. Cấu Trúc Phản Hồi Lỗi (Error Responses)

Backend trả về lỗi theo chuẩn RFC 7807 / RFC 9457 `ProblemDetails` hoặc `HttpValidationProblemDetails`:

### 2.1 Lỗi Phân Quyền & Logic Nghiệp Vụ (401, 403, 404, 409)
* **401 Unauthorized:** Token không hợp lệ hoặc đã hết hạn.
* **403 Forbidden:** Người dùng không có quyền `Admin` để thực hiện thao tác này.
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "User does not have permission to perform this action."
}
```

### 2.2 Lỗi Validate Dữ Liệu (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["Email is required.", "Invalid email format."],
    "Password": ["Password must be at least 8 characters."]
  }
}
```

---

## 3. Tổng Hợp Enums & Constants (TypeScript Definitions)

Frontend có thể copy trực tiếp các định nghĩa TypeScript dưới đây:

```typescript
// ==========================================
// 1. VAI TRÒ HỆ THỐNG (ROLES)
// ==========================================
export enum RoleType {
  Admin = 'Admin',
  User = 'User'
}

// ==========================================
// 2. TRẠNG THÁI ĐƠN HÀNG TOÀN DIỆN (ORDER STATUS)
// ==========================================
export enum OrderStatus {
  // Giai đoạn Telesale (Số Âm & 0)
  Draft = 0,                                // Đơn nháp / Lead mới từ Landing page
  New = -1,                                 // Mới tạo - Chờ nhân viên gọi điện
  NoAnswer = -2,                            // Không nghe máy
  CallbackLater = -3,                       // Hẹn gọi lại sau
  Cancelled = -4,                           // Khách hủy / Không mua
  OutOfStock = -5,                          // Hết hàng
  Duplicate = -6,                           // Trùng đơn

  // Giai đoạn Giao vận GHTK (Khớp 100% mã status_id của GHTK)
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
  Returning = 20,                           // Đang trả hàng (GHTK: 20)
  Returned = 21,                            // Đã trả hàng (GHTK: 21)
  ShipperReportedPicked = 123,              // Shipper báo đã lấy hàng
  ShipperReportedCannotPick = 127,          // Shipper báo không lấy được hàng
  ShipperReportedDelayPick = 128,           // Shipper báo delay lấy hàng
  ShipperReportedDelivered = 45,            // Shipper báo đã giao hàng
  ShipperReportedCannotDeliver = 49,        // Shipper báo không giao được hàng
  ShipperReportedDelayDelivery = 410        // Shipper báo delay giao hàng
}

// Helper Text Tiếng Việt
export const OrderStatusLabels: Record<OrderStatus, string> = {
  [OrderStatus.Draft]: 'Nháp',
  [OrderStatus.New]: 'Mới tạo',
  [OrderStatus.NoAnswer]: 'Không nghe máy',
  [OrderStatus.CallbackLater]: 'Hẹn gọi lại',
  [OrderStatus.Cancelled]: 'Hủy đơn hàng',
  [OrderStatus.OutOfStock]: 'Hết hàng',
  [OrderStatus.Duplicate]: 'Trùng đơn',
  [OrderStatus.Confirmed]: 'Chốt đơn (Chờ GHTK)',
  [OrderStatus.Accepted]: 'GHTK Đã tiếp nhận',
  [OrderStatus.PickedOrStocked]: 'Đã lấy hàng/Đã nhập kho',
  [OrderStatus.Delivering]: 'Đang giao hàng',
  [OrderStatus.DeliveredUnreconciled]: 'Đã giao hàng/Chưa đối soát',
  [OrderStatus.Reconciled]: 'Đã đối soát',
  [OrderStatus.CannotPick]: 'Không lấy được hàng',
  [OrderStatus.DelayPick]: 'Hoãn lấy hàng',
  [OrderStatus.CannotDeliver]: 'Không giao được hàng',
  [OrderStatus.DelayDelivery]: 'Delay giao hàng',
  [OrderStatus.ReturnReconciled]: 'Đã đối soát công nợ trả hàng',
  [OrderStatus.Picking]: 'Đang lấy hàng',
  [OrderStatus.Compensated]: 'Đơn hàng bồi hoàn',
  [OrderStatus.Returning]: 'Đang trả hàng',
  [OrderStatus.Returned]: 'Đã trả hàng',
  [OrderStatus.ShipperReportedPicked]: 'Shipper báo đã lấy hàng',
  [OrderStatus.ShipperReportedCannotPick]: 'Shipper báo không lấy được hàng',
  [OrderStatus.ShipperReportedDelayPick]: 'Shipper báo delay lấy hàng',
  [OrderStatus.ShipperReportedDelivered]: 'Shipper báo đã giao hàng',
  [OrderStatus.ShipperReportedCannotDeliver]: 'Shipper báo không giao được hàng',
  [OrderStatus.ShipperReportedDelayDelivery]: 'Shipper báo delay giao hàng'
};

// ==========================================
// 3. MODELS & INTERFACES
// ==========================================
export interface ProductDto {
  id: string;
  name: string;
  price: number;
  costPrice: number;
  imageUrl: string;
  isDeleted: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface OrderItemDto {
  id: string;
  productId: string;
  productName: string;
  productPrice: number;
  productImageUrl: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface OrderStatusHistoryDto {
  id: string;
  status: OrderStatus;
  statusName: string;
  note: string | null;
  createdAtUtc: string;
}

export interface WarehouseDto {
  id: string;
  name: string;
  province: string;
  district: string;
  ward: string | null;
  address: string;
  phoneNumber: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface OrderDto {
  id: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  warehouseId: string | null;
  warehouseName: string | null;
  province: string;
  district: string;
  ward: string | null;
  address: string;
  totalAmount: number;
  shippingFee: number | null;
  weight: number | null;
  status: OrderStatus;
  statusName: string;
  note: string | null;
  ghtkId: string | null;
  estimatedPickTime: string | null;
  estimatedDeliverTime: string | null;
  items: OrderItemDto[];
  statusHistories: OrderStatusHistoryDto[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
```

---

## 4. Danh Sách Chi Tiết Endpoints API

### 4.1 Authentication (`/api/v1/auth`)

> [!TIP]
> **Cơ Chế Bảo Mật Hybrid (Chuẩn OWASP Production):**
> - **Refresh Token:** Được tự động lưu trong **`HttpOnly, Secure, SameSite=Lax` Cookie** (`Path=/api/v1/auth`). Trình duyệt tự động gửi kèm cookie khi gọi `/refresh` hoặc `/revoke`. Javascript không thể truy cập cookie này, ngăn chặn 100% rủi ro bị đánh cắp session qua tấn công XSS.
> - **Access Token:** Trả về trong response body. Frontend lưu trong **RAM (Memory State)** (Pinia/Redux/Zustand), không lưu vào `localStorage`.
> - **Cấu hình Frontend (Axios / Fetch):** Luôn bật **`withCredentials: true`** (hoặc `credentials: 'include'`).

---

#### 🔹 [POST] `/api/v1/auth/login`
- **Mô tả:** Đăng nhập hệ thống. Trình duyệt tự động nhận Cookie `refreshToken`.
- **Quyền hạn:** Public
- **Payload:**
  ```json
  {
    "email": "user@example.com",
    "password": "Password123"
  }
  ```
- **Response Headers:** `Set-Cookie: refreshToken=...; Path=/api/v1/auth; HttpOnly; SameSite=Lax`
- **Response (200 OK):**
  ```json
  {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "7f8b9c0d-1e2f-3a4b...",
    "expiresIn": 900,
    "refreshTokenExpiresAtUtc": "2026-08-30T10:00:00Z",
    "user": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "user@example.com",
      "fullName": "Nguyễn Văn A",
      "role": "Admin"
    }
  }
  ```

---

#### 🔹 [POST] `/api/v1/auth/register`
- **Mô tả:** Đăng ký tài khoản người dùng mới.
- **Quyền hạn:** Public
- **Payload:**
  ```json
  {
    "email": "newuser@example.com",
    "password": "Password123",
    "fullName": "Nguyễn Văn B"
  }
  ```
- **Response (201 Created):** Trả về thông tin tài khoản vừa tạo.

---

#### 🔹 [POST] `/api/v1/auth/refresh`
- **Mô tả:** Lấy Access Token mới khi Access Token hết hạn (401) hoặc khi người dùng F5 tải lại trang.
- **Quyền hạn:** Public
- **Cơ chế:**
  - **Web SPA:** Gửi request rỗng `{}` kèm `withCredentials: true` (Trình duyệt tự động gửi HttpOnly Cookie `refreshToken`).
  - **Mobile App / Swagger:** Có thể truyền token trong body: `{ "refreshToken": "..." }`.
- **Response Headers:** `Set-Cookie: refreshToken=<mới>; Path=/api/v1/auth; HttpOnly; SameSite=Lax`
- **Response (200 OK):**
  ```json
  {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "new-refresh-token...",
    "expiresIn": 900,
    "refreshTokenExpiresAtUtc": "2026-08-30T10:00:00Z"
  }
  ```

---

#### 🔹 [POST] `/api/v1/auth/revoke`
- **Mô tả:** Đăng xuất (Logout). Thu hồi Refresh Token trong CSDL và xóa HttpOnly Cookie khỏi trình duyệt.
- **Quyền hạn:** Public
- **Payload (Optional):** `{}` (đọc từ Cookie) hoặc `{ "refreshToken": "..." }`.
- **Response Headers:** `Set-Cookie: refreshToken=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/api/v1/auth`
- **Response (200 OK):**
  ```json
  {
    "message": "Token revoked successfully and cookie cleared."
  }
  ```

---

#### 🔹 [GET] `/api/v1/auth/me`
- **Mô tả:** Lấy thông tin cá nhân của phiên đăng nhập hiện tại.
- **Quyền hạn:** `Bearer <Token>` (Authenticated)
- **Response (200 OK):** Đối tượng `UserDto`.

---

### 4.2 Customers (`/api/v1/customers`)

#### 🔹 [GET] `/api/v1/customers`
- **Mô tả:** Danh sách khách hàng (phân trang & tìm kiếm theo tên, SĐT).
- **Quyền hạn:** `Bearer <Token>` (Authenticated)
- **Query Params:** `search`, `page`, `pageSize`.
- **Response (200 OK):** `PaginatedResponse<CustomerDto>`.

---

#### 🔹 [GET] `/api/v1/customers/{id}`
- **Mô tả:** Lấy chi tiết khách hàng theo ID.
- **Quyền hạn:** `Bearer <Token>` (Authenticated)
- **Response (200 OK):** `CustomerDto`.

---

#### 🔹 [POST] `/api/v1/customers`
- **Mô tả:** Tạo khách hàng mới (Số điện thoại không được trùng).
- **Quyền hạn:** 👑 **Admin Only**
- **Payload:**
  ```json
  {
    "name": "Nguyễn Văn Khách",
    "phoneNumber": "0912345678"
  }
  ```
- **Response (201 Created):** `CustomerDto`.

---

#### 🔹 [PUT] `/api/v1/customers/{id}`
- **Mô tả:** Cập nhật thông tin khách hàng.
- **Quyền hạn:** 👑 **Admin Only**
- **Payload:** `UpdateCustomerRequest`.
- **Response (200 OK):** `CustomerDto`.

---

#### 🔹 [DELETE] `/api/v1/customers/{id}`
- **Mô tả:** Xóa khách hàng theo ID.
- **Quyền hạn:** 👑 **Admin Only**
- **Response (204 No Content):** Không có body.

---

### 4.3 Products (`/api/v1/products`)

#### 🔹 [GET] `/api/v1/products`
- **Mô tả:** Danh sách sản phẩm (phân trang & tìm kiếm). Mặc định chỉ lấy các sản phẩm đang kinh doanh (`isDeleted = false`).
- **Quyền hạn:** Public
- **Query Params:**
  - `search` *(string, optional)*: Tìm kiếm theo tên sản phẩm.
  - `includeDeleted` *(boolean, optional, default: false)*: `true` để lấy cả các sản phẩm đã xóa mềm / ngừng kinh doanh.
  - `page` *(number, optional, default: 1)*
  - `pageSize` *(number, optional, default: 20, max: 100)*
- **Response (200 OK):** `PaginatedResponse<ProductDto>`.

---

#### 🔹 [GET] `/api/v1/products/{id}`
- **Mô tả:** Lấy thông tin chi tiết sản phẩm.
- **Quyền hạn:** Public
- **Query Params:**
  - `includeDeleted` *(boolean, optional, default: false)*: `true` nếu muốn xem chi tiết sản phẩm đã xóa mềm.
- **Response (200 OK):** `ProductDto`.

---

#### 🔹 [POST] `/api/v1/products`
- **Mô tả:** Tạo sản phẩm mới kèm upload ảnh lên Cloudinary.
- **Quyền hạn:** 👑 **Admin Only**
- **Content-Type:** `multipart/form-data`
- **Form Data:**
  - `name` *(string, required)*: Tên sản phẩm.
  - `price` *(number, required)*: Giá bán lẻ (> 0).
  - `costPrice` *(number, optional/required)*: Giá nhập vốn (>= 0).
  - `image` *(File, required)*: File ảnh sản phẩm.
- **Response (201 Created):** `ProductDto`.

---

#### 🔹 [PUT] `/api/v1/products/{id}`
- **Mô tả:** Cập nhật sản phẩm & thay ảnh Cloudinary mới nếu có.
- **Quyền hạn:** 👑 **Admin Only**
- **Content-Type:** `multipart/form-data`
- **Form Data:**
  - `name` *(string, required)*: Tên sản phẩm.
  - `price` *(number, required)*: Giá bán lẻ (> 0).
  - `costPrice` *(number, required)*: Giá nhập vốn (>= 0).
  - `image` *(File, optional)*: File ảnh mới (nếu muốn thay đổi ảnh).
- **Response (200 OK):** `ProductDto`.

---

#### 🔹 [DELETE] `/api/v1/products/{id}`
- **Mô tả:** Xóa mềm sản phẩm (Soft Delete: đánh dấu `isDeleted = true` và ngừng bán). Dữ liệu lịch sử trong các đơn hàng cũ và ảnh sản phẩm vẫn được bảo toàn nguyên vẹn.
- **Quyền hạn:** 👑 **Admin Only**
- **Response (204 No Content):** Không có body.

---

### 4.4 Orders - Đơn Hàng Tích Hợp (`/api/v1/orders`)

Quản lý tập trung toàn bộ vòng đời đơn hàng: từ đơn khách đặt (Draft), gọi chốt (Confirmed) với danh sách nhiều sản phẩm, đến cập nhật thông tin giao vận GHTK.

---

#### 🔹 [GET] `/api/v1/orders`
- **Mô tả:** Lấy danh sách đơn hàng (hỗ trợ phân trang và lọc đa tiêu chí).
- **Quyền hạn:** 👑 **Admin Only**
- **Query Params:**
  - `status` *(number, optional)*: Lọc theo mã `OrderStatus` (VD: `0`: Nháp, `-1`: Mới tạo, `1`: Đã chốt, `4`: Đang giao,...).
  - `customerId` *(Guid, optional)*: Lọc theo khách hàng.
  - `warehouseId` *(Guid, optional)*: Lọc theo kho xuất hàng.
  - `productId` *(Guid, optional)*: Lọc theo sản phẩm có trong đơn.
  - `ghtkId` *(string, optional)*: Lọc theo mã vận đơn GHTK.
  - `search` *(string, optional)*: Tìm kiếm theo Địa chỉ, Tỉnh/Thành, Quận/Huyện, Phường/Xã, Tên khách hàng, SĐT, Mã GHTK, hoặc Tên sản phẩm.
  - `fromDate` *(ISO Date string, optional)*: Từ ngày.
  - `toDate` *(ISO Date string, optional)*: Đến ngày.
  - `page` *(number, optional, default: 1)*
  - `pageSize` *(number, optional, default: 20, max: 100)*
- **Response (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "11111111-2222-3333-4444-555555555555",
        "customerId": "c1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c",
        "customerName": "Nguyễn Văn Khách",
        "customerPhone": "0912345678",
        "warehouseId": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
        "warehouseName": "Kho Tổng TP.HCM",
        "province": "TP. Hồ Chí Minh",
        "district": "Quận 1",
        "ward": "Phường Bến Nghé",
        "address": "Số 123 Đường Lê Lợi",
        "totalAmount": 522000.0,
        "shippingFee": 22000.0,
        "weight": 0.5,
        "status": 4,
        "statusName": "Đang giao hàng",
        "note": "Cho khách kiểm tra hàng",
        "ghtkId": "S21234567.BO.MB1.123456789",
        "estimatedPickTime": "Chiều 2026-08-23",
        "estimatedDeliverTime": "Sáng 2026-08-24",
        "items": [
          {
            "id": "22222222-3333-4444-5555-666666666666",
            "productId": "e1f2a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b",
            "productName": "Sản phẩm A",
            "productPrice": 250000.0,
            "productImageUrl": "https://res.cloudinary.com/.../img.jpg",
            "quantity": 2,
            "unitPrice": 250000.0,
            "totalPrice": 500000.0
          }
        ],
        "statusHistories": [
          {
            "id": "33333333-4444-5555-6666-777777777777",
            "status": 4,
            "statusName": "Đang giao hàng",
            "note": "GHTK cập nhật: Đang giao hàng",
            "createdAtUtc": "2026-08-23T08:30:00Z"
          }
        ],
        "createdAtUtc": "2026-08-22T10:30:00Z",
        "updatedAtUtc": "2026-08-23T08:30:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20
  }
  ```

---

#### 🔹 [GET] `/api/v1/orders/{id}`
- **Mô tả:** Lấy chi tiết toàn diện 1 đơn hàng theo ID (kèm Khách hàng, Kho hàng, danh sách Sản phẩm và Timeline lịch sử).
- **Quyền hạn:** 👑 **Admin Only**
- **Response (200 OK):** Đối tượng `OrderDto`.

---

#### 🔹 [POST] `/api/v1/orders`
- **Mô tả:** Tạo đơn hàng mới (hỗ trợ 4 cấp địa chỉ, danh sách nhiều sản phẩm `items`, cân nặng, phí ship, kho hàng).
- **Quyền hạn:** 👑 **Admin Only**
- **Headers:** 
  - `Authorization: Bearer <AdminAccessToken>`
  - `Content-Type: application/json`
- **Payload:**
  ```json
  {
    "customerId": "c1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c",
    "warehouseId": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
    "province": "TP. Hồ Chí Minh",
    "district": "Quận 1",
    "ward": "Phường Bến Nghé",
    "address": "Số 123 Đường Lê Lợi",
    "shippingFee": 22000.0,
    "weight": 0.5,
    "status": 1,
    "note": "Khách dặn gọi trước khi giao",
    "items": [
      {
        "productId": "e1f2a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b",
        "quantity": 2,
        "unitPrice": 250000.0
      }
    ]
  }
  ```
  *(Ghi chú: `shippingFee` và `weight` là tùy chọn (nullable), có thể để trống khi đơn mới tạo từ Landing page / Lead nháp; nếu không truyền `totalAmount`, backend sẽ tự tính = Tổng tiền sản phẩm + (shippingFee ?? 0))*.
- **Response (201 Created):** Đối tượng `OrderDto`.

---

#### 🔹 [POST] `/api/v1/orders/with-customer`
- **Mô tả:** Tạo khách hàng (tự động kiểm tra theo SĐT: nếu chưa có thì tạo mới, đã có thì liên kết khách hàng cũ) và tạo đơn hàng mới với trạng thái mặc định `-1` (`OrderStatus.New` - Mới tạo). Thích hợp cho form đặt hàng Landing Page, Telesale hoặc Website.
- **Quyền hạn:** Public / Anonymous (Không bắt buộc JWT, hoặc có thể gửi kèm Bearer Token nếu có).
- **Headers:** 
  - `Content-Type: application/json`
- **Payload:**
  ```json
  {
    "customerName": "Nguyễn Văn A",
    "phoneNumber": "0912345678",
    "province": "Thành phố Hồ Chí Minh",
    "district": "Quận 1",
    "ward": "Phường Bến Nghé",
    "address": "123 Đường Lê Lợi",
    "items": [
      {
        "productId": "e1f2a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b",
        "quantity": 2
      }
    ],
    "sheetUrl": "https://script.google.com/macros/s/.../exec"
  }
  ```
  *(Ghi chú: `sheetUrl` là tùy chọn (optional) - Link Webhook Google Apps Script của Sheet sản phẩm để tự động đẩy thông tin đơn hàng 7 cột sang Google Sheet; `totalAmount` tự động tính theo giá sản phẩm trong DB * số lượng; `weight` mặc định = 3; `shippingFee`, `note`, `warehouseId` để null).*
- **Response (201 Created):** Đối tượng `OrderDto`.

---

#### 🔹 [PUT] `/api/v1/orders/{id}`
- **Mô tả:** Cập nhật toàn bộ thông tin đơn hàng, địa chỉ chuẩn hóa, danh sách sản phẩm, kho hàng, phí ship và trạng thái.
- **Quyền hạn:** 👑 **Admin Only**
- **Headers:** `Authorization: Bearer <AdminAccessToken>`
- **Payload:** Cấu trúc tương tự `POST /api/v1/orders`.
- **Response (200 OK):** Đối tượng `OrderDto`.

---

#### 🔹 [PATCH] `/api/v1/orders/{id}/status`
- **Mô tả:** Cập nhật nhanh trạng thái đơn hàng (tự động ghi log vào timeline `statusHistories`).
- **Quyền hạn:** 👑 **Admin Only**
- **Headers:** `Authorization: Bearer <AdminAccessToken>`
- **Payload:**
  ```json
  {
    "status": 1,
    "note": "Đã chốt đơn thành công với khách hàng"
  }
  ```
- **Response (200 OK):** Đối tượng `OrderDto`.

---

#### 🔹 [DELETE] `/api/v1/orders/{id}`
- **Mô tả:** Xóa đơn hàng theo ID (tự động xóa cascade các `items` và `statusHistories`).
- **Quyền hạn:** 👑 **Admin Only**
- **Response (204 No Content):** Không có body.

---

### 4.5 Warehouses - Kho Hàng (`/api/v1/warehouses`)

#### 🔹 [GET] `/api/v1/warehouses`
- **Mô tả:** Danh sách kho hàng.
- **Quyền hạn:** `Bearer <Token>` (Authenticated)
- **Response (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
        "name": "Kho Tổng TP.HCM",
        "province": "TP. Hồ Chí Minh",
        "district": "Quận Bình Tân",
        "ward": "Phường An Lạc",
        "address": "Số 10 Quốc Lộ 1A",
        "phoneNumber": "02838123456",
        "createdAtUtc": "2026-08-01T08:00:00Z",
        "updatedAtUtc": null
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20
  }
  ```

---

#### 🔹 [GET] `/api/v1/warehouses/{id}`
- **Mô tả:** Chi tiết kho hàng.
- **Quyền hạn:** `Bearer <Token>` (Authenticated)
- **Response (200 OK):** `WarehouseDto`.

---

#### 🔹 [POST] `/api/v1/warehouses`
- **Mô tả:** Thêm kho hàng mới.
- **Quyền hạn:** 👑 **Admin Only**
- **Payload:**
  ```json
  {
    "name": "Kho Tổng TP.HCM",
    "province": "TP. Hồ Chí Minh",
    "district": "Quận Bình Tân",
    "ward": "Phường An Lạc",
    "address": "Số 10 Quốc Lộ 1A",
    "phoneNumber": "02838123456"
  }
  ```
- **Response (201 Created):** `WarehouseDto`.

---

#### 🔹 [PUT] `/api/v1/warehouses/{id}`
- **Mô tả:** Cập nhật thông tin kho hàng.
- **Quyền hạn:** 👑 **Admin Only**
- **Payload:**
  ```json
  {
    "name": "Kho Tổng TP.HCM (Mới)",
    "province": "TP. Hồ Chí Minh",
    "district": "Quận Bình Tân",
    "ward": "Phường An Lạc",
    "address": "Số 20 Quốc Lộ 1A",
    "phoneNumber": "02838123456"
  }
  ```
- **Response (200 OK):** `WarehouseDto`.

---

#### 🔹 [DELETE] `/api/v1/warehouses/{id}`
- **Mô tả:** Xóa kho hàng.
- **Quyền hạn:** 👑 **Admin Only**
- **Response (204 No Content):** Không có body.

---

#### 🔹 [POST] `/api/v1/ghtk/shipment/order` hoặc `/api/v1/ghtk/shipment/order/{orderId}`
- **Mô tả:** Đẩy đơn hàng đã chốt sang Giao Hàng Tiết Kiệm (GHTK API).
  * **Điều kiện:** Chỉ đơn hàng có trạng thái **`Done` (`0`: Đã chốt thành công)** mới được phép đẩy.
  * **Tự động hóa:** Backend tự động lấy toàn bộ thông tin kho xuất hàng (`pick_...`), thông tin người nhận, danh sách sản phẩm, tự động thiết lập các trường mặc định (`is_freeship = 1`, `transport = "road"`, `pick_option = "cod"`, `hamlet = "Khác"`, `pick_date = Ngày hiện tại`), sau đó gọi GHTK API và cập nhật mã vận đơn `ghtkId`, `shippingFee`, `status = 1 (Confirmed)`.
- **Quyền hạn:** 👑 **Admin Only**
- **Headers:** 
  - `Authorization: Bearer <AdminAccessToken>`
  - `Content-Type: application/json`
- **Cách 1: Gửi Body JSON (`POST /api/v1/ghtk/shipment/order`):**
  ```json
  {
    "orderId": "11111111-2222-3333-4444-555555555555",
    "note": "Cho khách xem hàng, gọi điện trước khi giao hàng"
  }
  ```
- **Cách 2: Gửi Path Param (`POST /api/v1/ghtk/shipment/order/11111111-2222-3333-4444-555555555555`):**
  *(Không cần body, hoặc truyền thêm query param `?note=...` nếu muốn ghi chú riêng)*.
- **Response (200 OK):**
  ```json
  {
    "success": true,
    "message": "Đơn hàng đã được tạo thành công.",
    "order": {
      "partner_id": "11111111-2222-3333-4444-555555555555",
      "label": "S21234567.BO.MB1.123456789",
      "area": "1",
      "fee": 22000.0,
      "insurance_fee": 0.0,
      "estimated_pick_time": "Chiều 2026-08-23",
      "estimated_deliver_time": "Sáng 2026-08-24",
      "status_id": 1
    }
  }
  ```
- **Lỗi thường gặp:**
  - `400 Bad Request`: Đơn hàng chưa ở trạng thái `Done (0)`, hoặc thiếu kho hàng / địa chỉ.
  - `404 Not Found`: Đơn hàng không tồn tại.

---

#### 🔹 [POST] `/api/v1/ghtk/shipment/cancel/{ghtkId}`
- **Mô tả:** Hủy đơn hàng đã gửi sang GHTK bằng **Mã vận đơn GHTK (`ghtkId` - chuỗi string, ví dụ: `S21234567.BO.MB1.123456789`)**. **Không cần truyền body payload**.
  * **Điều kiện hiển thị trên Frontend:** Nút "Hủy đơn GHTK" **CHỈ hiển thị khi đơn hàng ở 1 trong 3 trạng thái**:
    - `1` (`OrderStatus.Confirmed` - Đã chốt đơn / Chờ GHTK tiếp nhận)
    - `2` (`OrderStatus.Accepted` - GHTK Đã tiếp nhận)
    - `12` (`OrderStatus.Picking` - Đang lấy hàng)
  * **Hành vi xử lý:** Nếu GHTK phản hồi hủy thành công (`success: true`), hệ thống tự động:
    - Chuyển trạng thái đơn hàng sang **`-4` (`OrderStatus.Cancelled` - Hủy đơn hàng)**.
    - Thêm một bản ghi mới vào lịch sử trạng thái **`OrderStatusHistory`** (ghi nhận thời gian và log_id từ GHTK).
- **Quyền hạn:** 👑 **Admin Only**
- **Headers:** 
  - `Authorization: Bearer <AdminAccessToken>`
- **Request Body:** Không cần body (`body: null / empty`).
- **Response (200 OK):**
  ```json
  {
    "success": true,
    "message": "Hủy đơn hàng thành công",
    "log_id": "99999999-aaaa-bbbb-cccc-dddddddddddd"
  }
  ```
- **Lỗi thường gặp:**
  - `400 Bad Request`: Đơn hàng không ở trạng thái 1, 2, hoặc 12, hoặc GHTK từ chối hủy (ví dụ đơn đã chuyển sang trạng thái đang giao 4).
  - `404 Not Found`: Không tìm thấy đơn hàng nào có mã vận đơn GHTK này trong hệ thống.

---

#### 🔹 [POST] `/api/v1/ghtk/webhook` & `/api/v1/ghtk/updateShipment`
- **Mô tả:** Webhook tiếp nhận callback cập nhật trạng thái tự động từ GHTK khi shipper lấy hàng / giao hàng / đổi trạng thái.
- **Quyền hạn:** Public / Server-to-Server
- **Content-Type Hỗ trợ:** `application/x-www-form-urlencoded`, `multipart/form-data`, hoặc `application/json`.
- **Payload Format:**
  ```json
  {
    "label_id": "S21234567.BO.MB1.123456789",
    "partner_id": "11111111-2222-3333-4444-555555555555",
    "status_id": 4,
    "action_time": "2026-08-23T08:30:00Z",
    "reason_code": "000",
    "reason": "Shipper đang đi giao",
    "weight": 0.5,
    "fee": 22000.0,
    "return_part_package": 0,
    "pick_money": 522000.0
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "success": true,
    "message": "Đồng bộ trạng thái GHTK thành công: Đang giao hàng"
  }
  ```

---

### 4.7 System Health Check

#### 🔹 [GET] `/healthz`
- **Mô tả:** Kiểm tra tình trạng hoạt động của API và Database PostgreSQL.
- **Quyền hạn:** Public
- **Response (200 OK):** `Healthy`

---

## 5. Bảng Tổng Hợp Nhanh (Cheat Sheet)

| Endpoint | Method | Auth Scope | Content-Type | Thao tác |
| :--- | :--- | :--- | :--- | :--- |
| `/api/v1/auth/login` | **POST** | Public | `application/json` | Đăng nhập tài khoản |
| `/api/v1/auth/register` | **POST** | Public | `application/json` | Đăng ký tài khoản mới |
| `/api/v1/auth/refresh` | **POST** | Public | `application/json` | Làm mới Access Token |
| `/api/v1/auth/revoke` | **POST** | Public | `application/json` | Đăng xuất / Thu hồi Token |
| `/api/v1/auth/me` | **GET** | Bearer (User/Admin) | - | Lấy profile user hiện tại |
| `/api/v1/customers` | **GET** | Bearer (User/Admin) | - | Danh sách khách hàng (phân trang) |
| `/api/v1/customers/{id}` | **GET** | Bearer (User/Admin) | - | Chi tiết khách hàng |
| `/api/v1/customers` | **POST** | 👑 **Admin Only** | `application/json` | Tạo mới khách hàng |
| `/api/v1/customers/{id}` | **PUT** | 👑 **Admin Only** | `application/json` | Cập nhật khách hàng |
| `/api/v1/customers/{id}` | **DELETE** | 👑 **Admin Only** | - | Xóa khách hàng |
| `/api/v1/products` | **GET** | Public | - | Danh sách sản phẩm (phân trang) |
| `/api/v1/products/{id}` | **GET** | Public | - | Chi tiết sản phẩm |
| `/api/v1/products` | **POST** | 👑 **Admin Only** | `multipart/form-data` | Thêm sản phẩm + Upload ảnh |
| `/api/v1/products/{id}` | **PUT** | 👑 **Admin Only** | `multipart/form-data` | Sửa sản phẩm + Đổi ảnh |
| `/api/v1/products/{id}` | **DELETE** | 👑 **Admin Only** | - | Xóa sản phẩm + Dọn ảnh |
| `/api/v1/orders` | **GET** | 👑 **Admin Only** | - | Danh sách đơn hàng tích hợp (lọc đa điều kiện) |
| `/api/v1/orders/{id}` | **GET** | 👑 **Admin Only** | - | Chi tiết đơn hàng, sản phẩm & timeline |
| `/api/v1/orders` | **POST** | 👑 **Admin Only** | `application/json` | Tạo đơn hàng mới kèm danh sách món |
| `/api/v1/orders/{id}` | **PUT** | 👑 **Admin Only** | `application/json` | Cập nhật đơn hàng & sản phẩm |
| `/api/v1/orders/{id}/status` | **PATCH** | 👑 **Admin Only** | `application/json` | Cập nhật nhanh trạng thái đơn hàng |
| `/api/v1/orders/{id}` | **DELETE** | 👑 **Admin Only** | - | Xóa đơn hàng (cascade) |
| `/api/v1/warehouses` | **GET** | Bearer (User/Admin) | - | Danh sách kho hàng |
| `/api/v1/warehouses/{id}` | **GET** | Bearer (User/Admin) | - | Chi tiết kho hàng |
| `/api/v1/warehouses` | **POST** | 👑 **Admin Only** | `application/json` | Thêm kho hàng |
| `/api/v1/warehouses/{id}` | **PUT** | 👑 **Admin Only** | `application/json` | Cập nhật kho hàng |
| `/api/v1/warehouses/{id}` | **DELETE** | 👑 **Admin Only** | - | Xóa kho hàng |
| `/api/v1/ghtk/shipment/order` | **POST** | 👑 **Admin Only** | `application/json` | Đẩy đơn sang GHTK |
| `/api/v1/ghtk/webhook` | **POST** | Public (Server Callback) | Form / JSON | Webhook tiếp nhận callback GHTK |
| `/api/v1/ghtk/updateShipment` | **POST** | Public (Server Callback) | Form / JSON | Alias Webhook GHTK |
| `/healthz` | **GET** | Public | - | Kiểm tra sức khỏe hệ thống |
