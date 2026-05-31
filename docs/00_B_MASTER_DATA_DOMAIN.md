# Phân Hệ Danh Mục Dùng Chung (Master Data Management - MDM)

## 1. Triết Lý Thiết Kế & Kiến Trúc (Architectural Philosophy)
- **Shared Kernel / Centralized Catalog:** Danh mục dùng chung được thiết kế theo dạng Agnostic (trung lập). Nó cung cấp ID dữ liệu chuẩn cho toàn bộ hệ thống.
- **Data Propagation (Lan truyền dữ liệu):** Vì các Microservices khác cần truy vấn danh mục này với tốc độ cao, hệ thống áp dụng mô hình **Cập nhật tập trung - Truy vấn phân tán**. Khi có một Mặt hàng hoặc Tỉnh thành mới được tạo ở MDM, một sự kiện Integration Event (Kafka) sẽ bắn ra để các dịch vụ khác (WMS, SO) tự cập nhật vào bộ nhớ cache hoặc DB local của chúng (Read Model).

## 2. Danh Mục Sản Phẩm & Vật Tư (Product Catalog Cluster)

Hệ thống phân tầng danh mục sản phẩm theo cấu trúc hình cây: `Nhóm hàng ──> Loại mặt hàng ──> Mặt hàng`.

### 2.1. Aggregate Root: `ProductGroup` (Nhóm hàng)
Phân loại ở mức vĩ mô (Ví dụ: Hàng Điện Tử, Hàng Gia Dụng, Thực Phẩm Fresh).
- `Id`: `Guid`
- `Code`: `String` (Unique, ví dụ: `GRP-ELECTRONIC`)
- `Name`: `String`
- `IsActive`: `Bool`

### 2.2. Entity: `ProductCategory` (Loại mặt hàng)
Thuộc một Nhóm hàng cụ thể (Ví dụ: Nhóm Hàng Điện Tử sẽ có các loại: Điện thoại, Laptop, Phụ kiện).
- `Id`: `Guid`
- `ProductGroupId`: `Guid` (Khóa ngoại nối với `ProductGroup`)
- `Code`: `String` (Ví dụ: `CAT-SMARTPHONE`)
- `Name`: `String`

### 2.3. Aggregate Root: `Product` (Mặt hàng / Sản phẩm)
Thông tin snapshot cốt lõi của một sản phẩm.
- `Id`: `Guid`
- `ProductCategoryId`: `Guid`
- `Sku`: `String` (Mã quản lý tồn kho duy nhất - Unique SKU)
- `BarCode`: `String` (Mã vạch phục vụ quét máy quét tại kho)
- `Name`: `String`
- `DefaultUomId`: `Guid` (Đơn vị tính cơ sở mặc định)
- `PurchasePrice`: `Decimal` (Giá mua vào tiêu chuẩn)
- `RetailPrice`: `Decimal` (Giá bán lẻ tiêu chuẩn)
- `Attributes`: `String (JSON)` (Lưu các thuộc tính động: Màu sắc, kích cỡ, dung lượng)

### 2.4. Aggregate Root: `UnitOfMeasure` (Đơn vị tính - UoM)
- `Id`: `Guid`
- `Code`: `String` (Ví dụ: `PCS`, `BOX`, `KG`, `PACK`)
- `Name`: `String`

### 2.5. Value Object: `UomConversion` (Logic quy đổi đơn vị tính)
*Quy tắc:* Hỗ trợ bài toán mua một kiểu, bán một kiểu (Ví dụ: Nhập kho theo `BOX` 24 chai, nhưng bán lẻ ra E-commerce theo `PCS` từng chai).
- `FromUomId`: `Guid` (Đơn vị gốc, ví dụ: `BOX`)
- `ToUomId`: `Guid` (Đơn vị đích, ví dụ: `PCS`)
- `ConversionFactor`: `Decimal` (Tỷ lệ quy đổi, ví dụ: `24.00`)

---

## 3. Danh Mục Địa Lý & Hành Chính (Geographic Catalog Cluster)

Cấu trúc phân cấp chuẩn của Việt Nam và Quốc tế: `Quốc gia ──> Tỉnh/Thành ──> Quận/Huyện ──> Phường/Xã`.

### 3.1. Aggregate Root: `Country` (Quốc gia)
- `Id`: `Guid`
- `IsoCode`: `String` (Mã chuẩn quốc tế, ví dụ: `VN`, `US`, `JP`)
- `Name`: `String`
- `PhoneCode`: `String` (Ví dụ: `+84`)

### 3.2. Entity: `Province` (Tỉnh / Thành phố)
- `Id`: `Guid`
- `CountryId`: `Guid`
- `Code`: `String` (Mã tỉnh theo Tổng cục Thống kê, ví dụ: `79` cho TP.HCM, `01` cho Hà Nội)
- `Name`: `String`
- `Type`: `String` (Phân loại: `Thành phố Trung ương`, `Tỉnh`)

### 3.3. Entity: `District` (Quận / Huyện / Thị xã)
- `Id`: `Guid`
- `ProvinceId`: `Guid`
- `Code`: `String` (Mã quận huyện)
- `Name`: `String`

### 3.4. Entity: `Ward` (Phường / Xã / Thị trấn)
- `Id`: `Guid`
- `DistrictId`: `Guid`
- `Code`: `String` (Mã phường xã)
- `Name`: `String`

---

## 4. Các Sự Kiện Tích Hợp (Integration Events)
Khi danh mục thay đổi, phát ra các Event sau để đồng bộ hệ thống:
- `ProductCreatedEvent` / `ProductUpdatedEvent`: Gửi thông tin Sản phẩm mới + Đơn vị tính kèm theo sang cho Warehouse Service và Sales Order Service để cập nhật danh mục cục bộ.
- `GeographicDataSyncEvent`: Thường dùng để cập nhật đồng loạt dữ liệu địa lý khi có thay đổi hành chính từ hệ thống gốc.

## 5. Ràng Buộc Nghiệp Vụ (Business Rules)
- **Rule_MDM_01:** Mã `Sku` và `BarCode` của Mặt hàng là duy nhất trên toàn hệ thống, không cho phép trùng lặp để tránh lỗi quét mã vạch nhận nhầm hàng tại Kho.
- **Rule_MDM_02:** Không được phép xóa (Hard Delete) các bản ghi danh mục (như Đơn vị tính hoặc Tỉnh thành) một khi chúng đã được tham chiếu trong bất kỳ giao dịch nào (PO, SO, GRN). Hệ thống chỉ cho phép chuyển trạng thái sang `IsActive = false` (Soft Delete).