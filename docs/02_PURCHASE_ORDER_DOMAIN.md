# Phân Hệ Mua Hàng (Purchase Order Domain) - Technical Specification

## 1. Triết Lý Thiết Kế & Kiến Trúc (Architectural Philosophy)
- **Tư duy thiết kế:** Áp dụng Domain-Driven Design (DDD). Tách biệt tuyệt đối giữa Business Logic (Domain Layer) và Infrastructure Layer.
- **Mô hình trạng thái:** Trạng thái của Đơn mua hàng (Purchase Order - PO) được quản lý chặt chẽ qua State Pattern hoặc cơ chế đóng gói (Encapsulation), không cho phép sửa đổi trực tiếp từ bên ngoài mà phải thông qua các Domain Methods (Rich Domain Model).
- **Giao tiếp liên dịch vụ:** Sử dụng Event-Driven Architecture qua Kafka (hoặc MassTransit) để đồng bộ dữ liệu bất đồng bộ với Phân hệ Kho (Warehouse Management) và Phân hệ Kế toán/Tài chính.

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)

### 2.1. Aggregate Root: `PurchaseOrder`
Quản lý vòng đời và toàn bộ thông tin của một đơn mua hàng.
- `Id`: `Guid` (Khóa chính)
- `POCode`: `String` (Mã PO tự động sinh theo format, ví dụ: `PO-05-2026-00001`)
- `VendorId`: `Guid` (Liên kết sang thực thể Vendor)
- `Status`: `PurchaseOrderStatus` (Enum: `Draft`, `PendingApproval`, `Approved`, `Ordering`, `PartiallyReceived`, `Completed`, `Cancelled`)
- `TotalAmount`: `Decimal` (Tổng giá trị đơn hàng, tính tự động từ danh sách Items)
- `TaxRate`: `Decimal` (Thuế suất áp dụng)
- `Notes`: `String` (Ghi chú mua hàng)
- `Items`: `IReadOnlyCollection<PurchaseOrderItem>` (Danh sách vật tư mua)
- `CreatedAt`: `DateTime`
- `UpdatedAt`: `DateTime?`

### 2.2. Entity: `PurchaseOrderItem`
Chi tiết từng mặt hàng trong đơn mua.
- `Id`: `Guid`
- `PurchaseOrderId`: `Guid`
- `ProductId`: `Guid` (Mã vật tư/sản phẩm)
- `Quantity`: `Int` (Số lượng đặt mua)
- `ReceivedQuantity`: `Int` (Số lượng thực tế kho đã nhận)
- `UnitPrice`: `Decimal` (Đơn giá mua)
- `LineAmount`: `Decimal` (Thành tiền = Quantity * UnitPrice)

### 2.3. Value Object: `PriceQuotation`
Thông tin bảng báo giá từ nhà cung cấp áp dụng cho PO này.
- `QuotationCode`: `String`
- `ValidUntil`: `DateTime`
- `DiscountPercentage`: `Decimal`

### 2.4. Aggregate Root: `Vendor` (Nhà Cung Cấp)
- `Id`: `Guid`
- `Code`: `String`
- `Name`: `String`
- `TaxCode`: `String`
- `Address`: `String`
- `ContactEmail`: `String`
- `IsActive`: `Bool`

## 3. Quy Trình Nghiệp Vụ & Luồng Trạng Thái (Business Workflow)
1. **Khởi tạo (Draft):** Nhân viên mua hàng tạo PO ở trạng thái Nháp, thêm các mặt hàng và chọn Nhà cung cấp.
2. **Gửi duyệt (PendingApproval):** Hệ thống khóa không cho sửa đổi PO và kích hoạt Luồng phê duyệt (Approval Workflow).
3. **Phê duyệt (Approved):** Cấp trưởng phòng/giám đốc bấm duyệt. Hệ thống xuất bản (Publish) sự kiện `PurchaseOrderApprovedEvent` ra Kafka.
4. **Đặt hàng (Ordering):** PO được gửi chính thức tới Vendor, hệ thống Warehouse nhận thông tin để chuẩn bị khu vực hạ hàng và tăng số lượng hàng dự kiến về (`IncomingStock`).
5. **Nhận hàng một phần (PartiallyReceived):** Kho nhận trước một lượng hàng và cập nhật số lượng thông qua Goods Receipt Note (GRN). Số lượng `ReceivedQuantity` trong PO tăng lên.
6. **Hoàn thành (Completed):** Toàn bộ số lượng hàng đã được nhận đủ (`ReceivedQuantity == Quantity` cho tất cả Items). PO tự động chuyển sang trạng thái Hoàn thành.
7. **Hủy đơn (Cancelled):** PO chỉ có thể hủy khi đang ở trạng thái `Draft`, `PendingApproval` hoặc `Approved` nhưng chưa phát sinh nhận hàng kho. Khi hủy, hệ thống xuất bản event `PurchaseOrderCancelledEvent` để hạ số lượng hàng dự kiến về trong kho.

## 4. Đặc Tả Tích Hợp Hệ Thống (Integration & Events)

Phân hệ PO giao tiếp với Warehouse Service thông qua Message Broker. Các Event cấu trúc như sau:

### 4.1. Sự kiện: `PurchaseOrderApprovedEvent`
*Mục đích:* Thông báo cho Kho biết có đơn hàng đã duyệt để chuẩn bị kế hoạch nhận hàng.
```json
{
  "EventId": "guid",
  "Timestamp": "2026-05-24T13:54:58Z",
  "PurchaseOrderId": "guid",
  "POCode": "PO-2026-00001",
  "WarehouseId": "guid",
  "Items": [
    {
      "ProductId": "guid",
      "ExpectedQuantity": 100
    }
  ]
}

### 4.2. Sự kiện lắng nghe: `GoodsReceiptNoteConfirmedEvent`
*Mục đích:* Lắng nghe từ Warehouse Service khi kho xác nhận đã nhận hàng thực tế thành công, từ đó cập nhật tiến độ cho PO.
```json
{
  "EventId": "guid",
  "Timestamp": "2026-05-24T13:54:58Z",
  "PurchaseOrderId": "guid",
  "GRNCode": "GRN-2026-0089",
  "ReceivedItems": [
    {
      "ProductId": "guid",
      "ActualQuantity": 50
    }
  ]
}

## 5. Ràng Buộc Nghiệp Vụ & Quy Tắc Kiểm Tra (Business Rules)

Khi viết mã nguồn, Code phải tuân thủ nghiêm ngặt các quy tắc phân tích sau:

1. **Rule_PO_01:** Tổng số lượng nhận (ReceivedQuantity) của một mặt hàng không bao giờ được phép vượt quá số lượng đặt mua ban đầu (Quantity) trong các kịch bản chuẩn. Nếu Vendor giao thừa, phải xử lý qua luồng phê duyệt phát sinh hoặc tạo PO bổ sung.

2. **Rule_PO_02:** Đơn giá (UnitPrice) của vật tư phải lớn hơn 0 và tự động áp dụng giá từ bảng giá (PriceQuotation) gần nhất của Vendor đó nếu có.

3. **Rule_PO_03:** Không cho phép xóa một PO đã chuyển sang trạng thái Approved hoặc các trạng thái sau đó. Hệ thống chỉ cho phép chuyển trạng thái thành Cancelled nhằm lưu vết lịch sử (Audit Trail).

4. **Rule_PO_04 (Concurrency):** Khi cập nhật số lượng hàng nhận từ sự kiện của Kho, phải sử dụng cơ chế Optimistic Concurrency Control (dùng thuộc tính RowVersion hoặc Version) để tránh tình trạng ghi đè dữ liệu khi nhiều lệnh nhập kho diễn ra đồng thời.