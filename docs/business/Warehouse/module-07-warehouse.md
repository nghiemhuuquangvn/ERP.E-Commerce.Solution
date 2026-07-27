# Module 7 — Warehouse
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Phụ thuộc**: cần Module 3 (Item, UOM, Warehouse — Warehouse entity cơ bản đã có) và Module 5 (Sales — consumer của event `DeliveryRequested`). Module 7 cũng là nơi phát sinh dữ liệu cho Module 8 Inventory (Stock Ledger/Valuation) — nên rạch ròi ranh giới giữa hai module này ngay từ đầu (xem mục 10).

---

## 1. Mục tiêu và phạm vi

Warehouse quản lý vận động vật lý của hàng hoá: nhập, xuất, chuyển kho, kiểm kê — tại mức **giao dịch** (transaction). Warehouse trả lời câu hỏi "hàng đã di chuyển thế nào", còn Module 8 Inventory trả lời "tồn kho hiện tại là bao nhiêu và giá vốn bao nhiêu" — hai câu hỏi khác nhau, nên tách domain rõ ràng dù hay bị gộp chung khi thiết kế.

Phạm vi **không bao gồm**: tính giá vốn (FIFO/Moving Average — thuộc Module 8), quản lý sản xuất (thuộc Module 9).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Warehouse Staff | Thực hiện Receipt/Issue/Transfer, quét Barcode |
| Warehouse Manager | Duyệt Adjustment, khởi tạo Cycle Count |
| Sales (hệ thống) | Gửi `DeliveryRequested`, nhận `DeliveryCompleted` |
| Purchasing (hệ thống) | Gửi yêu cầu Goods Receipt khi PO về hàng |

## 3. Quy trình nghiệp vụ chính

1. Nhận event `DeliveryRequested` từ Sales → tạo **Issue** (phiếu xuất) → Warehouse Staff pick hàng theo Bin Location (ưu tiên FEFO nếu có Lot/hạn dùng) → xác nhận xuất → publish `DeliveryCompleted`.
2. Nhận yêu cầu từ Purchasing (PO về hàng) → tạo **Receipt** (phiếu nhập) → quét Barcode/Serial từng item → gán Bin Location → xác nhận nhập.
3. **Transfer**: chuyển hàng giữa 2 Warehouse hoặc giữa 2 Bin Location trong cùng kho — sinh 1 Issue (kho nguồn) + 1 Receipt (kho đích) liên kết nhau.
4. **Cycle Count**: định kỳ đếm một phần kho (không đếm toàn bộ) → so sánh với hệ thống → nếu lệch, sinh **Adjustment** cần Warehouse Manager duyệt.
5. Lot/Batch/Serial: gán khi Receipt (nếu Item yêu cầu truy xuất nguồn gốc) → theo dõi xuyên suốt Issue/Transfer/Return để phục vụ truy xuất và cảnh báo hạn dùng.

## 4. Domain Model (Aggregate Roots — DDD)

```
WarehouseLayout (Entity, thuộc Warehouse đã có)
 └─ BinLocation (Entity) — Zone/Aisle/Rack/Bin

GoodsReceipt (Aggregate Root)
 └─ GoodsReceiptLine (Entity) — ItemId, Quantity, BinLocationId, LotId (nullable)

GoodsIssue (Aggregate Root)
 └─ GoodsIssueLine (Entity)

StockTransfer (Aggregate Root)
 └─ StockTransferLine (Entity) — liên kết 1 Issue + 1 Receipt

StockAdjustment (Aggregate Root)
 └─ StockAdjustmentLine (Entity) — SystemQuantity, CountedQuantity, VarianceReason

CycleCount (Aggregate Root)
 └─ CycleCountLine (Entity)

Lot (Aggregate Root) — hoặc gộp chung Batch nếu nghiệp vụ không phân biệt 2 khái niệm
Serial (Aggregate Root) — cho item cần track từng đơn vị (VD: điện tử, máy móc)
```

**Bounded Context**: `Warehouse` — publish event mọi thay đổi số lượng, **không tự giữ số dư tồn kho** (số dư thuộc Module 8 Inventory, build từ event của Warehouse). Đây là nguyên tắc Event Sourcing nhẹ: Warehouse ghi *sự kiện di chuyển*, Inventory build *trạng thái hiện tại*.

## 5. Database Design (đề xuất bảng)

**Nhóm Layout & Truy xuất nguồn gốc**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `BinLocations` | Id, WarehouseId (FK→đã có), Zone, Aisle, Rack, Bin, Code |
| 2 | `Lots` | Id, ItemId, LotNo, ManufactureDate, ExpiryDate, SupplierId (nullable) |
| 3 | `Serials` | Id, ItemId, SerialNo, Status (InStock/Sold/Returned) |

**Nhóm Nhập/Xuất**
| # | Bảng | Cột chính |
|---|---|---|
| 4 | `GoodsReceipts` | Id, WarehouseId, ReceiptNo, SourceType (PurchaseOrder/Return/Transfer/Manual), SourceRefId, Status, ReceivedDate |
| 5 | `GoodsReceiptLines` | Id, ReceiptId, ItemId, VariantId, Quantity, UomId, BinLocationId, LotId (nullable), SerialIds (join table nếu nhiều serial) |
| 6 | `GoodsIssues` | Id, WarehouseId, IssueNo, SourceType (SalesOrder/Transfer/Manual/Scrap), SourceRefId, Status, IssuedDate |
| 7 | `GoodsIssueLines` | Id, IssueId, ItemId, VariantId, Quantity, UomId, BinLocationId, LotId (nullable) |

**Nhóm Transfer & Adjustment**
| # | Bảng | Cột chính |
|---|---|---|
| 8 | `StockTransfers` | Id, FromWarehouseId, ToWarehouseId, TransferNo, Status, IssueId (FK), ReceiptId (FK, nullable đến khi nhận) |
| 9 | `StockAdjustments` | Id, WarehouseId, AdjustmentNo, Reason, Status (Pending/Approved/Rejected), ApprovedBy |
| 10 | `StockAdjustmentLines` | Id, AdjustmentId, ItemId, BinLocationId, SystemQuantity, CountedQuantity, VarianceQuantity |
| 11 | `CycleCounts` | Id, WarehouseId, CountNo, ScheduledDate, Status, ScopeFilter (JSON: theo ItemGroup/BinLocation) |
| 12 | `CycleCountLines` | Id, CycleCountId, ItemId, BinLocationId, SystemQuantity, CountedQuantity |

**Nhóm hỗ trợ**
| # | Bảng | Cột chính |
|---|---|---|
| 13 | `BarcodeMapping` | Id, ItemId, VariantId, Barcode, BarcodeType (EAN13/QR/Custom) |
| 14 | `WarehouseTransactionLogs` | Id, TransactionType, TransactionId, ItemId, Quantity, WarehouseId, BinLocationId, OccurredAt | Append-only — nguồn cho Module 8 Inventory build stock ledger |

→ **14 bảng cốt lõi**, so với ~100 đề xuất ban đầu — con số gốc rất có thể gộp cả bảng của Module 8 Inventory (Stock Ledger, Cost Layer...) vào chung, trong khi hai domain này nên tách theo mục 1.

## 6. API Design (REST)

```
POST   /api/v1/warehouse/receipts                        → ApiResponse<GoodsReceiptDto>
POST   /api/v1/warehouse/receipts/{id}/confirm             → publish GoodsReceiptConfirmed
POST   /api/v1/warehouse/issues                            → tạo từ DeliveryRequested event, hoặc thủ công
POST   /api/v1/warehouse/issues/{id}/pick                  → gán BinLocation/Lot thực tế đã lấy
POST   /api/v1/warehouse/issues/{id}/confirm                → publish DeliveryCompleted
POST   /api/v1/warehouse/transfers
POST   /api/v1/warehouse/cycle-counts/{id}/submit-count
POST   /api/v1/warehouse/adjustments/{id}/approve
GET    /api/v1/warehouse/bin-locations/{warehouseId}/layout
GET    /api/v1/warehouse/barcode/{code}/resolve            → tra cứu Item/Variant từ Barcode (dùng cho máy quét)
```

## 7. Event Design

| Event | Trigger | Consumer |
|---|---|---|
| `GoodsReceiptConfirmed` | Nhập kho xác nhận | Inventory (tăng stock ledger), Purchasing (đóng PO) |
| `DeliveryCompleted` | Xuất kho xác nhận (phản hồi Sales) | Sales (tạo Invoice), Inventory (giảm stock ledger) |
| `StockTransferCompleted` | Chuyển kho xong | Inventory (cập nhật vị trí) |
| `StockAdjustmentApproved` | Duyệt điều chỉnh | Inventory (ghi nhận chênh lệch), Finance (nếu ảnh hưởng giá trị tồn kho) |
| `LotExpiringWarning` | Lot sắp hết hạn (scheduled job) | Notification, Sales (ưu tiên xuất FEFO) |

## 8. Business Rules & Validation

- Xuất kho theo nguyên tắc **FEFO** (First Expired First Out) nếu Item có quản lý Lot/hạn dùng; **FIFO** nếu không — hệ thống nên gợi ý Bin Location/Lot ưu tiên, không bắt buộc cứng (Warehouse Staff có thể override có ghi log lý do).
- `GoodsIssueLine.Quantity` không được vượt quá tồn khả dụng tại `BinLocationId` cụ thể (không chỉ tổng tồn kho) — tránh xuất "ảo" từ vị trí không có hàng.
- `StockAdjustment` **bắt buộc** phải qua duyệt (`Status = Approved`) trước khi tác động đến Inventory — không cho phép ghi thẳng.
- Serial-tracked Item: mỗi `SerialNo` chỉ tồn tại ở đúng 1 trạng thái tại 1 thời điểm (không cho xuất trùng serial đã xuất).
- `StockTransfer` giữa 2 Warehouge khác Company (nếu multi-company) cần review thêm điều kiện thuế/chuyển giá nội bộ — nên flag riêng, không xử lý ngầm.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 3 (Master Data) | Item, UOM, Barcode — read-only reference |
| Module 5 (Sales) | Event-driven hai chiều: nhận `DeliveryRequested`, publish `DeliveryCompleted` |
| Module 6 (Purchasing) | Nhận yêu cầu Goods Receipt khi PO về hàng, publish `GoodsReceiptConfirmed` để đóng PO |
| Module 8 (Inventory) | Warehouse publish `WarehouseTransactionLogs` (append-only), Inventory subscribe để build Stock Ledger/Valuation — **Warehouse không tính giá vốn** |

## 10. Ranh giới Warehouse vs Inventory (điểm hay nhầm lẫn nhất)

| | Warehouse (Module 7) | Inventory (Module 8) |
|---|---|---|
| Câu hỏi trả lời | Hàng di chuyển thế nào, ở vị trí nào | Tồn bao nhiêu, giá vốn bao nhiêu |
| Dữ liệu giữ | Giao dịch (Receipt/Issue/Transfer) | Số dư (Stock Ledger, Cost Layer) |
| Cách cập nhật | Ghi trực tiếp khi Staff thao tác | Build/derive từ event của Warehouse |
| Ảnh hưởng | Vận hành (operations) | Tài chính (kế toán tồn kho) |

**Khuyến nghị**: nếu áp lực thời gian, có thể **gộp tạm** hai module này thành 1 bounded context trong giai đoạn đầu (vẫn tách namespace `Warehouse` và `Inventory` trong code để dễ tách sau), nhưng **không nên gộp bảng dữ liệu** — Stock Ledger nên là bảng append-only riêng ngay từ đầu để tránh phải migrate dữ liệu lớn sau này khi tách thật.

## 11. Khuyến nghị triển khai .NET

- `WarehouseTransactionLogs` nên là bảng append-only, cân nhắc partition theo tháng nếu dữ liệu lớn — đây sẽ là bảng ghi nhiều nhất trong toàn hệ thống (mỗi dòng Receipt/Issue đều ghi 1 log).
- Pick logic (FEFO/FIFO gợi ý) implement ở Application layer dạng Strategy pattern — dễ đổi chiến lược theo từng Item Group sau này.
- Barcode resolve API cần response cực nhanh (dùng cho máy quét cầm tay) — cân nhắc cache Redis cho `BarcodeMapping`.
- Đồng bộ `GoodsIssue`/`GoodsReceipt` với Sales/Purchasing qua Domain Event (MediatR) trước, đúng hướng đã áp dụng ở Module 5.

## 12. So sánh & Reconciliation với project hiện tại (2026-07-27)

`WareHouseService` đã tồn tại từ trước nhưng dùng một mô hình domain hoàn
toàn khác với đề xuất ở mục 4-6: thay vì các aggregate document riêng biệt
(GoodsReceipt/GoodsIssue/StockAdjustment/CycleCount), service này có
`Inventory` (số dư tồn theo sản phẩm) + `StockMovement` (log) + `Reservation`
+ `InventoryLot`/`InventorySerial` + `WarehouseLocation` (phẳng) +
`TransferOrder` + `Stocktake`. Quan trọng nhất: **Warehouse và Inventory đã
bị gộp chung thành một bounded context** — đúng như mục 10 của tài liệu gốc
cho phép "gộp tạm khi áp lực thời gian" — nên các thay đổi dưới đây chấp
nhận việc gộp này là vĩnh viễn, không cố tách lại.

| Hạng mục đề xuất | Trạng thái | Ghi chú |
|---|---|---|
| `GoodsReceipt`/`GoodsIssue` (document riêng, nhiều dòng) | ❌ Không build | Không có bằng chứng thực tế cần multi-line-per-PO hay partial-receipt; `ReceiveStockCommand`/`SalesOrderShippedEventHandler` đã đủ dùng |
| `BinLocation` (Zone/Aisle/Rack/Bin theo Warehouse) | ✅ Đã build một phần | Thêm entity `Warehouse` (site) làm cha của `WarehouseLocation` — không làm hierarchy Zone→Aisle→Rack→Bin sâu hơn, vì `WarehouseLocation` hiện tại đã đủ dùng ở mức 1 tầng |
| Tồn kho theo Bin, kiểm tra khả dụng theo `BinLocationId` cụ thể khi xuất | ✅ Đã build | `InventoryLocationStock` mới (theo ProductId+LocationId), cộng thêm bên cạnh `Inventory` (không thay thế) — additive, không backfill dữ liệu cũ |
| FEFO khi xuất kho (Lot sắp hết hạn trước) | ✅ Đã build | `ReserveInventoryCommandHandler` trước đây hardcode `"DEFAULT_LOCATION"`, không hề chọn Lot — nay chọn Lot theo ExpiryDate tăng dần, ghi lại LotId trên `Reservation` |
| `StockAdjustment` **bắt buộc phải qua duyệt** trước khi tác động Inventory | ✅ Đã build (fix vi phạm rule) | Trước đây `AdjustStockCommand` ghi thẳng vào `Inventory`, không hề có bước duyệt — vi phạm trực tiếp rule của tài liệu gốc. Nay tách thành Request (Pending, không đổi Inventory) → Approve/Reject |
| `CycleCount` (Stocktake) — tương tự, không ghi thẳng | ✅ Đã build (fix vi phạm rule tương tự) | `CompleteStocktakeCommand` trước đây cũng ghi thẳng biến động vào Inventory ngay khi "hoàn tất" đếm — cùng lỗi như StockAdjustment. Nay tách Complete() (khoá đếm, tính variance) → Approve() (mới thực sự ghi) |
| `BarcodeMapping`, endpoint resolve nhanh cho máy quét | ✅ Đã build | Hoàn toàn chưa có trước đây; cache 60s qua `ICacheService` (Redis) vì cần phản hồi nhanh cho scanner |
| `WarehouseTransactionLogs` (append-only) | ⏸ Không build riêng | `StockMovements` đã đóng vai trò tương tự dù không hoàn toàn append-only theo đúng nghĩa (coexist với bảng số dư `Inventory`) — chấp nhận hiện trạng, không tách bảng mới |
| `StockTransfer` giữa 2 Warehouse — kiểm tra tồn kho nguồn | ✅ Đã build | `CreateTransferOrderCommand` trước đây không hề kiểm tra tồn kho tại kho nguồn — đã thêm, ưu tiên `InventoryLocationStock` nếu có dữ liệu, fallback về tồn tổng |
| Tên event đề xuất (`GoodsReceiptConfirmed`, `DeliveryCompleted`...) | ❌ Không đổi tên | Không khớp code thật lẫn CLAUDE.md; giữ nguyên `ReceiptCompletedEvent`/`InventoryUpdatedEvent`/... đã có, chỉ thêm `StockAdjustmentApprovedEvent`/`StockAdjustmentRejectedEvent` mới |
| Path API `/api/v1/warehouse/...` | ❌ Không đổi | Path thật là `/api/v1/inventory/...` (+ các controller mới `/stock-adjustments`, `/warehouses`, `/barcode` cùng prefix gateway `/wh/`) |

**Phát hiện thêm ngoài phạm vi tài liệu gốc**: 7+ domain event của service này
trước đây publish thẳng ra RabbitMQ ngay trong request (không transactional
với DB write) — đã retrofit Outbox pattern (cùng pattern đã áp dụng ở
SaleOrderService trước đó), nhưng vì các entity ở đây không kế thừa
`AggregateRoot`/`BaseEntity` (không có `DomainEvents` collection), retrofit
dùng cơ chế theo-từng-event (`IEventOutboxWriter.Stage(...)`) thay vì
theo-từng-aggregate như bên Sales. Quá trình này cũng phát hiện và sửa một
bug thật: `EventPublisher.PublishAsync<T>` tính routing key từ generic `T`
tại compile-time thay vì runtime type — nếu không sửa, processor mới sẽ
publish mọi event outbox với routing key sai (`warehouse.Object`).
