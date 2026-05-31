# Phân Hệ Quản Lý Kho (Warehouse Management Domain - WMS)

## 1. Triết Lý Thiết Kế
- **Mô hình T tồn kho chính xác (Double-Entry Inventory):** Quản lý dòng chảy của vật tư giống như kế toán tài chính. Mọi biến động kho phải sinh ra một bản ghi đối ứng.
- **Tách biệt Trạng thái Kho:** Phân định rõ số lượng thực tế trong bốn bức tường của kho (Physical) và số lượng hệ thống được phép bán (Available-to-Promise - ATP).

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)
### 2.1. Aggregate Root: `Warehouse`
- `Id`: `Guid`
- `Code`: `String` (Ví dụ: `WH-HCM-01`)
- `Name`: `String`
- `Zones`: `IReadOnlyCollection<WarehouseZone>`

### 2.2. Entity: `WarehouseZone` & `BinLocation`
- `ZoneId`: `Guid` (Phân loại: Cực lạnh, Hàng khô, Hàng giá trị cao)
- `BinLocationId`: `Guid` (Mã định vị ô kệ, ví dụ: `A-RACK2-ROW3-BIN4`)
- `MaxWeightCapacity`: `Decimal`

### 2.3. Aggregate Root: `InventoryLevel` (Bảng cân đối tồn kho)
- `Id`: `Guid`
- `WarehouseId`: `Guid`
- `BinLocationId`: `Guid`
- `ProductId`: `Guid`
- `PhysicalStock`: `Int` (Hàng đang nằm trên kệ)
- `AllocatedStock`: `Int` (Hàng đã có khách đặt và trả tiền, đang chờ đóng gói)
- `IncomingStock`: `Int` (Hàng đang trên đường về từ các PO đã duyệt)
- `RowVersion`: `Byte[]` (Phục vụ Optimistic Concurrency Control chống tranh chấp dữ liệu)

### 2.4. Aggregate Root: `GoodsReceiptNote` (GRN - Phiếu nhập kho từ PO)
- `Id`: `Guid`
- `GRNCode`: `String` (`GRN-2026-XXXX`)
- `PurchaseOrderId`: `Guid` (Tham chiếu đơn mua)
- `Status`: `GRNStatus` (`Draft`, `Receiving`, `Completed`)
- `Items`: `List<GoodsReceiptItem>`

### 2.5. Aggregate Root: `GoodsDeliveryNote` (GDN - Phiếu xuất kho cho SO)
- `Id`: `Guid`
- `GDNCode`: `String` (`GDN-2026-XXXX`)
- `SalesOrderId`: `Guid` (Tham chiếu đơn bán)
- `Status`: `GDNStatus` (`Picking`, `Packed`, `Dispatched`)
- `Items`: `List<GoodsDeliveryItem>`

### 2.6. Aggregate Root: `StocktakeSession` (Phiếu kiểm kho định kỳ)
- `Id`: `Guid`
- `Status`: `StocktakeStatus` (`Open`, `Counting`, `Adjusted`, `Closed`)
- `Items`: `List<StocktakeItem>` (Lưu số lượng hệ thống vs Số lượng đếm thực tế)

### 2.7. Entity: `StockMovementLog` (Nhật ký di biến động kho)
- `Id`: `Guid`
- `ProductId`: `Guid`
- `FromBin`: `Guid?`
- `ToBin`: `Guid?`
- `Quantity`: `Int`
- `ReferenceDocument`: `String` (Ghi rõ do GRN, GDN hay Stocktake nào kích hoạt)

## 3. Công Thức Nghiệp Vụ Tồn Kho
Số lượng hàng có thể bán hiển thị trên Website E-commerce được tính toán tự động qua công thức:
$$Available\text{-}to\text{-}Promise\ (ATP) = PhysicalStock - AllocatedStock$$

## 4. Các Sự Kiện Tích Hợp (Integration Events)
- `GoodsReceiptConfirmedEvent`: Phát ra khi kho xác nhận nhận đủ hàng từ PO $\rightarrow$ Kích hoạt PO Service đổi trạng thái sang `Completed`.
- `GoodsDeliveryDispatchedEvent`: Phát ra khi shipper lấy hàng đi khỏi kho $\rightarrow$ Kích hoạt SO Service chuyển sang trạng thái `Shipping`.
- `InventoryThresholdReachedEvent`: Phát ra khi hàng trong kho xuống dưới mức an toàn (Safety Stock) $\rightarrow$ CRM hoặc Purchase Service tự động cảnh báo tạo đơn mua hàng mới.