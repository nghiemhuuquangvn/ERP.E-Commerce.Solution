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
| Module 12 (Finance) | Publish `InventoryValuationSnapshotClosed` để Finance ghi bút toán tồn kho vào GL — Inventory không tự ghi sổ kế toán |
| Module 6 (Purchasing) | `LowStockDetected` gợi ý tạo Purchase Request |

## 10. So sánh & Reconciliation

Không có phần "đã có sẵn" ở module này (chưa build), nhưng cần lưu ý: đề xuất ban đầu liệt kê FIFO/Moving Average như hai "module con" riêng — thực tế đây là **hai chiến lược thay thế nhau** (Strategy pattern), không phải hai bộ bảng song song cần build cả hai. Chọn 1 phương pháp mặc định cho toàn hệ thống trước (khuyến nghị Moving Average — đơn giản hơn FIFO nhiều để implement đúng ngay từ đầu), có thể mở rộng hỗ trợ cấu hình theo Item Group sau nếu nghiệp vụ yêu cầu (VD: hàng có hạn dùng bắt buộc FIFO).

## 11. Khuyến nghị triển khai .NET

- `StockLedgerEntries` là bảng ghi rất nhiều, tương tự `WarehouseTransactionLogs` ở Module 7 — cân nhắc 2 bảng này thực chất có thể là **một** nếu muốn đơn giản hoá (Inventory subscribe trực tiếp từ WarehouseTransactionLogs thay vì có bảng ledger riêng). Quyết định tuỳ mức độ tách rời microservice thực sự giữa 2 module trong tương lai.
- Tính `AvailableQuantity` nên qua Redis cache (invalidate theo event) thay vì query tổng hợp mỗi lần Sales check tồn — đây là API được gọi nhiều nhất trong toàn hệ thống (mỗi lần thêm giỏ hàng Ecommerce đều có thể check).
- Domain Event consumer nên idempotent (dùng `SourceRefId` + `TransactionType` làm unique constraint) — tránh ghi trùng ledger nếu event bị deliver lại (rất quan trọng khi chuyển sang Kafka/RabbitMQ thật, vì message broker thường "at-least-once delivery").
- `CostConsumptionLogs` cần join hiệu quả với `Invoices`/`SalesOrderLines` để tính COGS theo từng đơn hàng — đánh index `(SourceRefId)`.
