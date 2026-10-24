# Module 8 — Inventory
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Phụ thuộc trực tiếp**: Module 8 build hoàn toàn từ event của Module 7 (`GoodsReceiptConfirmed`, `DeliveryCompleted`, `StockTransferCompleted`, `StockAdjustmentApproved`) và Module 5 (`SalesOrderConfirmed` để reserve). Đây là module **derive-only** — không có UI nhập liệu trực tiếp cho số dư tồn kho, mọi thay đổi đều đến từ event.

---

## 1. Mục tiêu và phạm vi

Inventory trả lời hai câu hỏi tài chính-vận hành quan trọng nhất của một ERP: **"tồn kho hiện tại là bao nhiêu"** (Available Quantity) và **"giá vốn hàng tồn là bao nhiêu"** (Valuation — FIFO/Moving Average). Đây là module bắt buộc phải chính xác tuyệt đối vì ảnh hưởng trực tiếp đến Finance (giá vốn hàng bán COGS) và Sales (có đủ hàng để bán không).

Phạm vi **không bao gồm**: giao dịch vật lý nhập/xuất/chuyển (thuộc Module 7 — Inventory chỉ *lắng nghe* event, không tự tạo giao dịch).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Warehouse (hệ thống) | Publish event giao dịch, Inventory subscribe |
| Sales (hệ thống) | Query Available Quantity trước khi confirm Order, gửi yêu cầu Reservation |
| Finance | Đọc Inventory Valuation cho báo cáo giá vốn/tồn kho cuối kỳ |
| Inventory Manager | Xem báo cáo, không sửa trực tiếp (mọi điều chỉnh phải qua Module 7 Adjustment) |

## 3. Quy trình nghiệp vụ chính

1. Nhận `GoodsReceiptConfirmed` từ Warehouse → ghi vào `StockLedger` (dòng nhập, +Quantity) → tạo `CostLayer` mới (giá nhập, dùng cho FIFO).
2. Nhận `DeliveryCompleted` → ghi `StockLedger` (dòng xuất, -Quantity) → tính giá vốn xuất theo phương pháp cấu hình (FIFO: lấy từ CostLayer cũ nhất; Moving Average: tính lại giá bình quân sau mỗi lần nhập).
3. Sales gọi API kiểm tra **Available Quantity** = Tồn thực tế − Reserved (chưa xuất nhưng đã cam kết cho Order khác) trước khi confirm Order.
4. `SalesOrderConfirmed` → tạo `Reservation` (giữ chỗ số lượng) → khi `DeliveryCompleted` thì Reservation được release và trừ thật vào StockLedger.
5. Cuối kỳ (tháng/quý), chạy **Inventory Valuation Snapshot** → tổng hợp giá trị tồn kho theo Item/Warehouse, gửi sang Finance để đối chiếu GL.

## 4. Domain Model (Aggregate Roots — DDD)

```
StockLedger (Aggregate Root, append-only — nguồn sự thật duy nhất về số lượng)
 └─ StockLedgerEntry (Entity) — mỗi dòng = 1 giao dịch tăng/giảm

CostLayer (Aggregate Root — chỉ dùng nếu phương pháp = FIFO)
 └─ đại diện từng lô giá nhập, bị "ăn dần" khi xuất kho theo thứ tự cũ nhất

MovingAverageCost (Aggregate Root — chỉ dùng nếu phương pháp = Moving Average)
 └─ 1 bản ghi hiện tại theo (ItemId, WarehouseId), cập nhật lại mỗi lần nhập

Reservation (Aggregate Root)
 └─ trạng thái: Reserved → Released (khi xuất thật) hoặc Cancelled (khi huỷ Order)

InventoryValuationSnapshot (Aggregate Root — chốt số theo kỳ, immutable sau khi chốt)
```

**Bounded Context**: `Inventory` — hoàn toàn read-model/derived, không nhận lệnh trực tiếp từ người dùng cuối để sửa số lượng (trừ trường hợp đặc biệt như migrate dữ liệu ban đầu).

## 5. Database Design (đề xuất bảng)

**Nhóm Stock Ledger (lõi)**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `StockLedgerEntries` | Id, ItemId, VariantId, WarehouseId, BinLocationId, TransactionType (Receipt/Issue/Transfer/Adjustment), SourceRefId, Quantity (dương/âm), RunningBalance, OccurredAt |
| 2 | `StockBalances` | Id, ItemId, VariantId, WarehouseId, OnHandQuantity, ReservedQuantity, AvailableQuantity (computed hoặc denormalized, cập nhật mỗi lần có ledger entry) | Bảng "cache" để query nhanh, không phải nguồn sự thật |

**Nhóm Valuation — chọn 1 trong 2 tuỳ Item hoặc toàn hệ thống**
| # | Bảng | Cột chính |
|---|---|---|
| 3 | `CostLayers` | Id, ItemId, WarehouseId, ReceiptRefId, UnitCost, OriginalQuantity, RemainingQuantity, ReceivedDate | FIFO |
| 4 | `MovingAverageCosts` | Id, ItemId, WarehouseId, CurrentUnitCost, TotalQuantity, LastUpdatedAt | Moving Average |
| 5 | `CostConsumptionLogs` | Id, IssueRefId, ItemId, CostLayerId (nullable nếu Moving Average), QuantityConsumed, UnitCostUsed | Chi tiết giá vốn đã dùng cho từng lần xuất — quan trọng cho audit COGS |

**Nhóm Reservation**
| # | Bảng | Cột chính |
|---|---|---|
| 6 | `Reservations` | Id, ItemId, VariantId, WarehouseId, SourceType (SalesOrder), SourceRefId, Quantity, Status (Reserved/Released/Cancelled), ExpiresAt |

**Nhóm Valuation Snapshot (báo cáo/kế toán)**
| # | Bảng | Cột chính |
|---|---|---|
| 7 | `InventoryValuationSnapshots` | Id, PeriodCode, WarehouseId, SnapshotDate, IsClosed |
| 8 | `InventoryValuationSnapshotLines` | Id, SnapshotId, ItemId, Quantity, UnitCost, TotalValue |

→ **8 bảng cốt lõi** (chọn CostLayers HOẶC MovingAverageCosts tuỳ phương pháp áp dụng, không cần cả hai cùng lúc trừ khi hệ thống cho phép cấu hình phương pháp theo từng Item Group) — rất xa so với ~60 bảng đề xuất ban đầu.

## 6. API Design (REST)

```
GET    /api/v1/inventory/items/{itemId}/available-quantity?warehouseId=   → dùng khi Sales confirm Order
POST   /api/v1/inventory/reservations                                      → tạo Reservation (nội bộ, gọi từ Sales event)
POST   /api/v1/inventory/reservations/{id}/release
GET    /api/v1/inventory/stock-ledger?itemId=&warehouseId=&from=&to=       → PaginatedResult<StockLedgerEntryDto>
GET    /api/v1/inventory/valuation/current?warehouseId=
POST   /api/v1/inventory/valuation/snapshots                                → chốt kỳ (Finance trigger)
GET    /api/v1/inventory/valuation/snapshots/{period}
```

## 7. Event Design (Inventory chủ yếu là **consumer**, ít publish hơn các module khác)

| Event nhận | Từ module | Xử lý |
|---|---|---|
| `GoodsReceiptConfirmed` | Warehouse | +StockLedger, tạo CostLayer mới hoặc cập nhật MovingAverage |
| `DeliveryCompleted` | Warehouse | −StockLedger, ghi CostConsumptionLog, release Reservation |
| `SalesOrderConfirmed` | Sales | Tạo Reservation |
| `SalesOrderCancelled` | Sales | Cancel Reservation |
| `StockAdjustmentApproved` | Warehouse | Ghi StockLedger loại Adjustment |

| Event publish | Trigger | Consumer |
|---|---|---|
| `InventoryValuationSnapshotClosed` | Chốt kỳ | Finance (ghi nhận giá trị tồn kho vào GL) |
| `LowStockDetected` | AvailableQuantity < ngưỡng cấu hình | Notification, Purchasing (gợi ý tạo PO) |

## 8. Business Rules & Validation

- `StockLedgerEntries` **append-only tuyệt đối** — không update/delete, mọi điều chỉnh phải ghi thêm dòng bù trừ (correction entry), giữ nguyên lịch sử cho audit.
- `AvailableQuantity = OnHandQuantity - ReservedQuantity`, luôn tính lại (không tin tưởng giá trị cache cũ) khi có bất kỳ ledger entry hoặc reservation mới — cân nhắc dùng computed column hoặc trigger cập nhật `StockBalances` ngay khi ghi ledger.
- FIFO: khi xuất kho, phải tiêu thụ `CostLayer` theo đúng thứ tự `ReceivedDate` cũ nhất trước — nếu 1 lần xuất cần lấy từ nhiều CostLayer (không đủ 1 lớp), phải tách `CostConsumptionLogs` thành nhiều dòng tương ứng.
- Reservation có `ExpiresAt` — nếu Sales Order ở trạng thái Draft quá lâu không confirm thật (VD: giỏ hàng Ecommerce bỏ dở), cần job tự động release Reservation hết hạn để tránh khoá tồn kho ảo.
- `InventoryValuationSnapshot` sau khi `IsClosed = true` thì **immutable** — không cho sửa lại số liệu kỳ đã chốt, nếu phát hiện sai phải tạo bút toán điều chỉnh kỳ hiện tại (nguyên tắc kế toán, không hồi tố).

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 7 (Warehouse) | Nguồn event chính — Inventory hoàn toàn phụ thuộc, không có logic nghiệp vụ nhập/xuất riêng |
| Module 5 (Sales) | Hai chiều: Sales query Available Quantity, Sales publish event tạo/huỷ Reservation |
| Module 12 (Finance) | Publish `InventoryValuationSnapshotClosed` để Finance ghi bút toán tồn kho vào GL — Inventory không tự ghi sổ kế toán. Từ 2026-10-24 (P1-1b) mỗi `StockMovementRecordedEvent` mang `UnitCost` + `ValueDelta` và AccountingService tự ghi bút toán nhập/xuất/điều chỉnh (xem docs/business/Accounting/AutoPosting.md) |
| Module 6 (Purchasing) | `LowStockDetected` gợi ý tạo Purchase Request |

## 10. So sánh & Reconciliation

> **Cập nhật 2026-07-27** — mục này ban đầu viết khi module chưa build. Sau khi đối chiếu
> với code thật của `WareHouseService`/`SaleOrderService`/`PurchaseOrderService`, phần
> dưới đây thay thế hoàn toàn nội dung cũ. Xem §12 để biết chi tiết đầy đủ.

Đề xuất ban đầu liệt kê FIFO/Moving Average như hai "module con" riêng — thực tế đây là
**hai chiến lược thay thế nhau** (Strategy pattern) trên cùng một field `ValuationMethod`
của `Inventory`, không phải hai bộ bảng song song. Cả hai đã tồn tại từ Module 7
(`Inventory.ReceiveStock` tính đúng weighted-average khi `ValuationMethod=Average`;
`InventoryLot`/FIFO-FEFO đã có) — không cần build lại, chỉ cần vá các lỗ hổng thật được
liệt kê ở §12.

## 11. Khuyến nghị triển khai .NET

- `StockLedgerEntries` là bảng ghi rất nhiều, tương tự `WarehouseTransactionLogs` ở Module 7 — cân nhắc 2 bảng này thực chất có thể là **một** nếu muốn đơn giản hoá (Inventory subscribe trực tiếp từ WarehouseTransactionLogs thay vì có bảng ledger riêng). Quyết định tuỳ mức độ tách rời microservice thực sự giữa 2 module trong tương lai.
- Tính `AvailableQuantity` nên qua Redis cache (invalidate theo event) thay vì query tổng hợp mỗi lần Sales check tồn — đây là API được gọi nhiều nhất trong toàn hệ thống (mỗi lần thêm giỏ hàng Ecommerce đều có thể check).
- Domain Event consumer nên idempotent (dùng `SourceRefId` + `TransactionType` làm unique constraint) — tránh ghi trùng ledger nếu event bị deliver lại (rất quan trọng khi chuyển sang Kafka/RabbitMQ thật, vì message broker thường "at-least-once delivery").
- `CostConsumptionLogs` cần join hiệu quả với `Invoices`/`SalesOrderLines` để tính COGS theo từng đơn hàng — đánh index `(SourceRefId)`.

## 12. Reconciliation (2026-07-27) — Inventory KHÔNG phải service riêng, đã merge vào WareHouseService từ Module 7

`Inventory` **không** phải bounded context "derive-only" tách biệt như tài liệu này mô tả —
từ Module 7, `WareHouseService` đã gộp Warehouse (di chuyển vật lý) và Inventory (số dư/giá
vốn) làm một, và quyết định đó được giữ nguyên (permanent). Việc đối chiếu lần này không cố
tách lại thành 2 service, mà kiểm tra xem các nghiệp vụ Inventory cốt lõi (COGS, valuation,
reservation, idempotency) đã đúng chưa bên trong `WareHouseService`.

### Bug thật đã tìm thấy và sửa (không chỉ là tính năng thiếu)

1. **`SalesOrderShippedEventHandler` không hề trừ kho.** Trước bản vá này, handler chỉ đổi
   `Reservation.Status` thành `Fulfilled` — `Inventory.TotalQuantity` không bao giờ giảm
   qua luồng bán hàng bình thường (đặt hàng → thanh toán → giữ chỗ → giao hàng). Đây là lỗi
   nghiêm trọng nhất tìm được, vì nó vi phạm trực tiếp lời hứa cốt lõi của module này
   ("tồn kho hiện tại là bao nhiêu").
2. **Double-depletion giữa Reservation (FEFO) và Sold movement (FIFO/LIFO).** FEFO lúc giữ
   chỗ trước đây trừ thẳng `InventoryLot.RemainingQuantity` ngay lập tức; luồng "Sold"
   thủ công lại trừ tiếp cùng lô đó lần nữa nếu cả hai đường cùng chạy cho cùng một đơn
   hàng. Đã sửa: Reservation giờ chỉ là **soft hold** (`InventoryLot.ReservedQuantity`,
   không đổi `RemainingQuantity`); chỉ bước giao hàng thật (ship) mới trừ `RemainingQuantity`,
   đúng tinh thần §3 bước 4 của tài liệu này ("khi DeliveryCompleted thì Reservation được
   release và trừ thật vào StockLedger").

### Đối chiếu từng đề xuất trong tài liệu

| Đề xuất trong tài liệu | Thực tế trước 2026-07-27 | Đã build lần này |
|---|---|---|
| `StockLedger` append-only | Tương đương `StockMovement` (đã có từ Module 7) | Không đổi — đã đủ |
| `CostLayer` (FIFO) | `InventoryLot` đã có, FEFO cho reservation | Không đổi cấu trúc, chỉ sửa cách dùng (soft hold) |
| `MovingAverageCost` | `Inventory.Cost` + `ValuationMethod=Average`, tính đúng khi nhận hàng | Không đổi — đã đúng |
| `Reservation` + `ExpiresAt` | Field `HoldUntil` đã có nhưng `IsExpired()`/`Expire()` là dead code, không job nào chạy | **Mới**: `ReservationExpiryProcessor`, quét mỗi 5 phút |
| `InventoryValuationSnapshot` | Không tồn tại — chỉ có query valuation tức thời | **Mới**: `InventoryValuationSnapshot`/`Line`, chốt 1 lần, publish `InventoryValuationSnapshotClosed` |
| `CostConsumptionLogs` | Không tồn tại — `StockMovement` không ghi unit cost | **Mới**: `CostConsumptionLog`, ghi tại thời điểm ship |
| Idempotent consumer (`SourceRefId`+`TransactionType`) | Không có — redelivery có thể giữ chỗ/nhận hàng trùng lặp | **Mới**: dedupe theo `OrderId` (reservation) và theo `Reference` (nhận hàng PO) — cùng tinh thần, khác khoá tự nhiên |
| Redis cache cho `AvailableQuantity` | Đã cache 2 phút nhưng **không có invalidate khi ghi** — có thể trả dữ liệu cũ tới 2 phút sau mọi thay đổi | **Mới**: invalidate cache ở mọi handler ghi Inventory |
| `LowStockDetected` → gợi ý Purchase Request | Sự kiện thật tên là `StockAlertEvent` (đã publish từ Module 7) nhưng **chưa ai tiêu thụ** | **Mới**: `PurchaseOrderService` tiêu thụ, tạo `PurchaseRequest` **chỉ mang tính gợi ý** (không tự tạo PO thật) |
| Sales gọi API kiểm tra Available Quantity trước khi confirm Order | **Không có** — `SaleOrderService` tạo đơn + thu tiền xong mới nhờ Warehouse giữ chỗ (sau thanh toán); nếu thiếu hàng thì hoàn tiền + huỷ đơn (bù trừ đúng, nhưng khách đã bị trừ tiền trước) | **Mới**: pre-check đồng bộ (`IWarehouseServiceClient`) trước khi tạo đơn/thu tiền — fail-closed nếu không gọi được Warehouse |

### Việc rõ ràng KHÔNG làm trong đợt này

- Không tách Inventory thành service riêng — đã chấp nhận merge vĩnh viễn từ Module 7.
- Không tự động ghi bút toán GL từ `InventoryValuationSnapshotClosed` — AccountingService
  đã có consumer cho luồng movement nhưng tự nhận việc post GL thật còn phải chờ tính năng
  Chart-of-Accounts mapping; publish event trước, chưa có bên tiêu thụ thật.
- Không tự động tạo `PurchaseOrder` từ `PurchaseRequest` — người dùng Purchasing vẫn phải
  tự tạo PO qua luồng có sẵn; `PurchaseRequest` chỉ là gợi ý.
- Pre-check tồn kho ở Sales không thay thế bước giữ chỗ thật (vẫn chạy sau thanh toán,
  không đổi) — chỉ thu hẹp khoảng hở race condition, luồng hoàn tiền/huỷ đơn khi thiếu hàng
  vẫn là lưới an toàn cho phần race còn lại.
