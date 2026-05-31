# Phân Hệ Đơn Hàng & E-Commerce (Sales Order Domain) - Technical Specification

## 1. Triết Lý Thiết Kế & Kiến Trúc (Architectural Philosophy)
- **Tư duy cốt lõi:** Đảm bảo tính nhất quán dữ liệu ở mức cao nhất (High Availability) cho kênh bán hàng Web/App. Phân tách rõ ràng giữa trạng thái Giữ hàng ảo (Allocation) và Xuất kho thực tế (Fulfillment).
- **Mô hình giao tiếp:** Kết hợp đồng bộ (REST API) khi khách hàng thao tác Giỏ hàng / Check-out và bất đồng bộ (Kafka/MassTransit) cho toàn bộ luồng xử lý đơn hàng, thanh toán, và điều phối kho.

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)

### 2.1. Aggregate Root: `SalesOrder`
Quản lý vòng đời đơn đặt hàng của khách hàng.
- `Id`: `Guid` (Khóa chính)
- `OrderNumber`: `String` (Mã đơn hàng hiển thị cho khách, ví dụ: `SO-2026-99812`)
- `CustomerId`: `Guid` (Id của khách hàng từ CRM/Identity)
- `Status`: `SalesOrderStatus` (Enum: `PendingPayment`, `Processing`, `StockAllocated`, `Shipping`, `Delivered`, `Cancelled`, `Refunded`, `Failed`)
- `TotalAmount`: `Decimal` (Tổng tiền khách phải trả)
- `SubTotal`: `Decimal` (Tổng tiền hàng trước giảm giá)
- `DiscountAmount`: `Decimal` (Tổng số tiền được giảm giá)
- `ShippingFee`: `Decimal` (Phí vận chuyển)
- `PaymentMethod`: `PaymentMethodType` (Enum: `COD`, `BankTransfer`, `EWallet`, `CreditCard`)
- `ShippingAddress`: `ShippingAddressVO` (Value Object lưu thông tin nhận hàng)
- `Items`: `IReadOnlyCollection<SalesOrderItem>`
- `Transactions`: `IReadOnlyCollection<PaymentTransaction>`
- `CreatedAt`: `DateTime`
- `UpdatedAt`: `DateTime?`

### 2.2. Entity: `SalesOrderItem`
- `Id`: `Guid`
- `SalesOrderId`: `Guid`
- `ProductId`: `Guid`
- `Sku`: `String` (Mã phiên bản sản phẩm)
- `ProductName`: `String` (Lưu snapshot tên tại thời điểm mua)
- `Quantity`: `Int`
- `UnitPrice`: `Decimal` (Giá gốc tại thời điểm mua)
- `AppliedDiscount`: `Decimal` (Số tiền giảm giá áp riêng cho line item này)
- `NetPrice`: `Decimal` (Thành tiền thực tế = (UnitPrice * Quantity) - AppliedDiscount)

### 2.3. Entity: `InventoryReservation` (Giữ hàng ảo tạm thời)
- `Id`: `Guid`
- `SalesOrderId`: `Guid`
- `ProductId`: `Guid`
- `Quantity`: `Int`
- `ExpiresAt`: `DateTime` (Thời gian hết hạn giữ hàng, thường là CreatedAt + 15 phút)
- `IsReleased`: `Bool` (Đã giải phóng kho do hủy đơn hoặc thanh toán thành công chưa)

### 2.4. Entity: `PaymentTransaction`
- `Id`: `Guid`
- `SalesOrderId`: `Guid`
- `ExternalTransactionId`: `String` (Mã giao dịch từ cổng VNPAY, Momo, Stripe...)
- `Amount`: `Decimal`
- `Gateway`: `String` (Tên cổng thanh toán)
- `Status`: `PaymentStatus` (Enum: `Pending`, `Success`, `Failed`, `Refunded`)
- `RawWebhookData`: `String` (Lưu log JSON thô từ cổng thanh toán gửi về để đối soát)
- `Timestamp`: `DateTime`

### 2.5. Aggregate Root: `CustomerCart` (Giỏ hàng tạm thời)
- `CustomerId`: `Guid`
- `CartItems`: `List<CartItem>` (Lưu cache Redis hoặc DB tạm, tự động xóa khi chuyển thành SalesOrder)

### 2.6. Value Object: `Promotion` (Thông tin khuyến mãi áp dụng)
- `PromotionId`: `Guid`
- `Code`: `String`
- `Type`: `PromotionType` (Percentage, FixedAmount, FreeShipping)
- `Value`: `Decimal`

---

## 3. Quy Trình Nghiệp Vụ Chi Tiết (Business Workflow)

1. **Khởi tạo đơn đặt hàng:** Khách hàng nhấn "Đặt hàng", hệ thống chuyển dữ liệu từ `CustomerCart` sang tạo `SalesOrder` với trạng thái `PendingPayment`. Đồng thời tạo bản ghi `InventoryReservation` để khóa giữ số lượng tồn kho ảo cho khách trong 15 phút.
2. **Xử lý Thanh toán (Webhook):** Cổng thanh toán gọi Webhook về API hệ thống.
   - *Thành công:* Cập nhật trạng thái `PaymentTransaction` thành `Success`, chuyển trạng thái `SalesOrder` sang `Processing`. Xuất bản sự kiện `SalesOrderPlacedEvent`.
   - *Thất bại / Quá hạn 15 phút:* Hệ thống tự động chuyển trạng thái đơn sang `Cancelled`, đặt `IsReleased = true` trong `InventoryReservation` để nhả lại hàng cho hệ thống bán tiếp.
3. **Điều phối và Đóng gói (Warehouse Integration):** Kho lắng nghe `SalesOrderPlacedEvent` để tiến hành gom hàng (Picking) và đóng gói (Packing). Trạng thái đơn chuyển sang `StockAllocated`.
4. **Giao hàng & Hoàn tất:** Đơn vị vận chuyển lấy hàng, đơn hàng chuyển sang `Shipping`. Khi giao thành công, nhận event từ vận chuyển chuyển đơn hàng sang `Delivered`.

---

## 4. Đặc Tả Tích Hợp Hệ Thống (Integration & Events)

### 4.1. Sự kiện xuất bản: `SalesOrderPlacedEvent`
*Mục đích:* Bắn sang Warehouse Service để chính thức tạo phiếu xuất kho đóng gói.
```json
{
  "EventId": "guid",
  "Timestamp": "2026-05-24T14:15:16Z",
  "SalesOrderId": "guid",
  "OrderNumber": "SO-2026-99812",
  "Items": [
    {
      "ProductId": "guid",
      "Sku": "SKU-IP15PM",
      "Quantity": 1
    }
  ]
}


### 4.2. Sự kiện lắng nghe: `SalesOrderStockReservationFailedEvent`

*Mục đích:* Lắng nghe từ Kho nếu xảy ra lỗi tranh chấp kho (ví dụ: kho thực tế bị hỏng, lệch tồn kho vật lý không đủ xuất).
```json
{
  "EventId": "guid",
  "Timestamp": "2026-05-24T14:16:00Z",
  "SalesOrderId": "guid",
  "Reason": "Out of physical stock due to inventory mismatch"
}

## 5. Ràng Buộc Nghiệp Vụ & Quy Tắc Kiểm Tra (Business Rules)

1. **Rule_SO_01 (Chống Over-selling):** Khi tạo SalesOrder, hệ thống phải kiểm tra số lượng tồn khả dụng (Available-to-Promise) thông qua Read-Model tối ưu (như Redis Cache). Việc giữ hàng ảo bằng InventoryReservation phải chạy qua một hàng đợi hoặc sử dụng cơ chế Lock (Pessimistic/Optimistic Lock) để đảm bảo không bị bán âm kho.

2. **Rule_SO_02 (Định giá bất biến):** Giá trị của UnitPrice và NetPrice trong SalesOrderItem phải được lưu cứng dạng Snapshot ngay khi tạo đơn. Mọi thay đổi về giá sản phẩm trên CMS sau đó không được phép làm ảnh hưởng đến giá trị của đơn hàng cũ.

3. **Rule_SO_03 (Hợp lệ Khuyến mãi):** Một mã khuyến mãi (Promotion) chỉ được coi là áp dụng thành công nếu Sales Order đáp ứng đầy đủ điều kiện (Ví dụ: Tổng tiền tối thiểu, danh mục sản phẩm hợp lệ) tại giây phút bấm Check-out.