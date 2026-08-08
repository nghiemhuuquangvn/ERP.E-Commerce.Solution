# Module 19 — Reporting
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (19) được gán mới trong đợt rà soát 2026-07-29. Viết theo hướng **reconciliation** hoàn toàn. ReportingService là service duy nhất trong hệ thống thuộc dạng **read-only OLAP** — không publish Outbox/domain event nào ra ngoài, chỉ tiêu thụ.

---

## 1. Mục tiêu và phạm vi

ReportingService (Port 5014, Code: RPT, database `ReportingDB`) là kho dữ liệu phân tích (data warehouse) dạng star-schema: nhận event từ các service nghiệp vụ khác, phi chuẩn hoá (denormalize) vào bảng Fact/Dimension, phục vụ dashboard và export CSV.

Phạm vi **không bao gồm**: ghi dữ liệu giao dịch gốc (chỉ đọc/nhận qua event), tính toán nghiệp vụ thời gian thực — đây là lớp BI/báo cáo, độ trễ vài giây tới vài phút là chấp nhận được.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Ban điều hành/Quản lý | Xem Dashboard doanh số, thanh toán, tồn kho, nhân sự, POS |
| Hệ thống BI ngoài | Truy vấn trực tiếp qua REST hoặc export CSV |
| Các service nghiệp vụ khác | Publish event, ReportingService tự động tiêu thụ và nạp vào Fact table |

## 3. Quy trình nghiệp vụ chính

1. Mỗi service nghiệp vụ publish domain event của mình (SalesOrder, Payment, Warehouse, POS, HRM, Product) → 7 consumer (6 Kafka `BackgroundService` + 1 RabbitMQ `IHostedService`) của ReportingService lắng nghe và tự parse JSON (dùng helper `JDoc` tra cứu tên field không phân biệt hoa/thường, nhiều tên field ứng viên — chống lệch schema/casing giữa các service publisher) rồi ghi vào bảng Fact tương ứng.
2. **Nguồn dữ liệu Sales thật sự là RabbitMQ, không phải Kafka**: `ReportingSalesOrderRabbitMqConsumer` (bind exchange `sales_order_events`) mới là consumer thật hoạt động; `SalesOrderEventConsumer` (Kafka) là code chết vì SaleOrderService không publish qua Kafka — comment trong chính code đã tự thừa nhận điều này.
3. Dashboard/API đọc trực tiếp từ Fact/Dimension bằng LINQ-to-EF, gộp nhóm (group by) **trong bộ nhớ sau khi tải hết dữ liệu** (`ToListAsync()` trước, group sau) — không đẩy `GROUP BY` xuống SQL Server. Chấp nhận được ở quy mô hiện tại nhưng sẽ chậm khi khoảng thời gian truy vấn lớn — điểm cần theo dõi hiệu năng.
4. Xuất báo cáo: `GET /sales/export`, `GET /payments/export` trả CSV dựng tay (StringBuilder), không qua MediatR, gọi thẳng Repository.

## 4. Domain Model (Fact/Dimension, đã xác nhận đúng code thật)

```
DimDate     — Year, Quarter, Month, Week, Day, MonthName, DayName, IsWeekend
DimProduct  — Name, Category, Price, IsActive
(DimCustomers tồn tại trong SQL nhưng KHÔNG có model C# nào — schema-only, chưa ai đọc/ghi)

FactSalesOrder     — OrderId, CustomerId, CustomerName, Status, TotalAmount, ItemCount,
                      OrderDate, ApprovedAt, ShippedAt, DeliveredAt, DateKey
FactPayment        — PaymentId, OrderId, Amount, Currency, Method, Status, ProcessedAt, DateKey
FactInventoryMovement — MovementId, ProductId, MovementType, Quantity, OccurredAt, DateKey
FactPOSTransaction — TransactionId, StoreId, CashierId, TotalAmount, TaxAmount, ItemCount, OccurredAt, DateKey
FactHRPayslip      — PayslipId, EmployeeId, EmployeeName, Period, BasicSalary, OvertimePay, TotalAmount, PaidAt, DateKey
```

Đây là các POCO thuần (anemic), không có domain method/event — đúng bản chất bảng OLAP chỉ-đọc.

## 5. Database Design (`ReportingDB`) — Phát hiện lỗi cụ thể quan trọng nhất của module này

**Xác nhận `DateKey` là cột thật đang dùng, `OrderDateKey`/`PaymentDateKey` là cột chết**: toàn bộ code C# (`ReportingDbContext`, cả 3 consumer ghi Fact) chỉ đọc/ghi `DateKey` — không có property `OrderDateKey`/`PaymentDateKey` nào trong entity. Hai cột này chỉ tồn tại trong SQL DDL gốc (migration 001), không được EF đụng tới.

**Bug migration đã xác nhận (từ cả góc độ SQL lẫn C#)**: migration `001_CreateReportingDB.sql` tạo `IX_FactSalesOrders_DateKey` trên cột `OrderDateKey` (cũ) và `IX_FactPayments_DateKey` trên `PaymentDateKey` (cũ). Migration `002_AddMissingEntityColumns.sql` thêm cột `DateKey` thật (khớp C#) rồi cố gắng tạo index cùng tên — nhưng guard `IF NOT EXISTS (SELECT ... WHERE name = '...')` thấy tên đã tồn tại (trỏ tới cột cũ) nên **bỏ qua, không tạo**. Kết quả: **cột `DateKey` — cột duy nhất code thật sự dùng để lọc/join theo ngày — chưa từng có index vật lý trong bất kỳ môi trường nào chạy migration theo đúng thứ tự.** Đây là phát hiện B-Tree cụ thể quan trọng nhất của toàn bộ đợt rà soát Index (xem `docs/architecture/database-performance-indexing-review.md` §5) — không phải giả thuyết, đã xác nhận 2 chiều SQL+C#.

**Cột mồ côi khác cũng được phát hiện**: `FactSalesOrders.LineItemCount`/`Channel` (SQL gốc) không khớp property C# nào (`ItemCount` mới là tên thật); `FactPOSTransactions.CashierId` khai báo `NVARCHAR(100)` trong SQL nhưng C# dùng `Guid` — lệch kiểu dữ liệu tiềm ẩn.

**Không dùng EF Core Migrations** — không có folder `Migrations/`, không gọi `Database.Migrate()`/`EnsureCreated()` — toàn bộ thay đổi schema đều thủ công qua file `.sql`, các khai báo Fluent API (`HasIndex`/`HasKey`) trong `ReportingDbContext` chỉ mang tính chú thích thiết kế, không bao giờ tự sinh DDL.

## 6. API Design (REST)

```
GET /api/v1/reports/sales          (reports:sales:view)     → doanh thu, số đơn, AOV, khách hàng riêng biệt, doanh thu theo ngày
GET /api/v1/reports/payments       (reports:payments:view)  → tổng đã xử lý/hoàn tiền, theo phương thức
GET /api/v1/reports/inventory      (reports:inventory:view) → tồn kho ròng theo sản phẩm (cộng/trừ theo từ khoá loại giao dịch)
GET /api/v1/reports/hr             (reports:hr:view)        → số phiếu lương, tổng, trung bình theo kỳ
GET /api/v1/reports/pos            (reports:pos:view)
GET /api/v1/reports/sales/export, /payments/export          → CSV thô
```

## 7. Event Design

**Published**: **Không có** — đúng như CLAUDE.md ghi, đây là service consumer-only hoàn toàn.

**Consumed** — thực tế hẹp hơn nhiều so với claim "tất cả sự kiện từ tất cả service" trong CLAUDE.md:

| Domain | Transport | Trạng thái |
|---|---|---|
| Sales | RabbitMQ `sales_order_events` | Thật, đang chạy (`ReportingSalesOrderRabbitMqConsumer`) |
| Payment | Kafka `payment-topic` | Thật |
| Warehouse/Inventory | Kafka `warehouse-topic` | Thật |
| POS | Kafka `pos-topic` | Thật |
| HRM | Kafka `hrm-topic` | Thật |
| Product | Kafka `product-topic` | Thật |
| **Identity, Purchasing, Accounting, CRM, Shipping, Promotion, Notification, SharedMasterData** | — | **Không có consumer nào** — không nạp dữ liệu các domain này vào kho báo cáo |

## 8. Business Rules & Validation

- Báo cáo tồn kho: quy ước cộng/trừ dựa trên **từ khoá tên loại giao dịch** (`receipt`/`receive` → cộng, `shipment`/`ship`/`issue` → trừ, còn lại giữ nguyên dấu) — không dựa trên enum chuẩn hoá, dễ vỡ nếu Warehouse đổi tên loại giao dịch trong tương lai.
- Ngưỡng cảnh báo tồn kho trong báo cáo: ≤0 = Hết hàng, <10 = Sắp hết (ngưỡng cố định trong code, không cấu hình được).
- Toàn bộ query dashboard tải hết dữ liệu về ứng dụng rồi mới gộp nhóm trong bộ nhớ — không đẩy `GROUP BY` xuống SQL Server.

## 9. Tích hợp với module khác

Là service tiêu thụ event một chiều từ 6/14 domain khác (xem §7) — không có module nào phụ thuộc ngược lại vào ReportingService (đúng vai trò BI/dashboard cuối chuỗi).

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính:
1. **Bug chỉ mục `DateKey` chưa từng được tạo** — phát hiện quan trọng nhất, đã xác nhận từ cả SQL và C#, xem chi tiết + khuyến nghị khắc phục ở `docs/architecture/database-performance-indexing-review.md` §5.
2. **CLAUDE.md phóng đại phạm vi tiêu thụ event** — thực tế chỉ 6/14 domain có consumer thật, không phải "tất cả sự kiện từ tất cả service."
3. **`DimCustomers` là bảng chết** — tồn tại trong SQL, không có model/consumer C# nào ghi vào.
4. **`SalesOrderEventConsumer` (Kafka) là code chết đã tự thừa nhận** trong comment — giữ lại "phòng khi SaleOrderService có Kafka Outbox trong tương lai."
5. Không dùng EF Core Migrations — toàn bộ vá schema là thủ công qua SQL script, dễ tái diễn lỗi tương tự bug `DateKey` nếu không áp dụng quy tắc quản trị migration ở §9 của tài liệu Index Review.

## 11. Dashboard/KPI + sửa lỗi báo cáo (2026-08-04)

Toàn bộ 16 DB đã được seed 1000+ dòng/bảng chính (xem
`docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md` Part A) —
`003_SeedFactsFromSeedData.sql` là ETL một lần backfill Fact table trực tiếp
từ dữ liệu seed đó (ngoại lệ đọc chéo DB **đã ghi nhận rõ ràng, có chủ đích**:
chỉ chạy 1 lần lúc migrate, không nằm trên đường chạy runtime của bất kỳ
service nào).

**Tính năng mới**: Admin có trang Dashboard/Reports thật, đọc từ Fact/Dimension
đúng kiến trúc đã thiết kế (không query thẳng DB nghiệp vụ) — widget Top
Products (`ReportingRepository.GetTopProductsAsync`, mới) + tổng hợp tồn kho.

**3 bug sửa trong đợt này**:
1. **`FactPOSTransactions.CashierId`** khai báo `NVARCHAR(100)` trong SQL
   nhưng C# dùng `Guid` (đã ghi nhận là "lệch kiểu dữ liệu tiềm ẩn" ở §5 —
   nay xác nhận là bug thật khi có data thật) → `ALTER COLUMN ...
   UNIQUEIDENTIFIER NOT NULL`.
2. **`GetInventoryReportAsync` đảo dấu số lượng 2 lần** cho các dòng
   `Issue` — vừa dùng dấu đã lưu sẵn của movement, vừa đảo lại lần nữa trong
   code — khiến tồn kho tính ra sai với bất kỳ sản phẩm nào có cả nhập lẫn
   xuất. Bỏ lần đảo dấu thừa; đồng thời thêm cột `DimProducts.StockQuantity`
   (backfill từ `WarehouseDB.Inventories.AvailableQuantity`) để báo cáo tồn
   kho đọc trực tiếp thay vì tự dựng lại từ lịch sử movement mỗi lần truy vấn.
3. **`admin/src/api/reports.api.ts` chưa từng khớp đúng contract backend
   thật** — viết theo hình dạng đoán (`byDate`, thiếu `topProducts`/
   `newCustomers`) chưa từng tồn tại trên wire. Viết lại theo đúng DTO thật
   của `ReportingRepository`.

Đã kiểm chứng trực tiếp qua API thật sau khi rebuild+redeploy
`reporting-service`+`admin`: `/rpt/api/v1/reports/sales` và `/rpt/api/v1/
reports/inventory` trả đúng số liệu tính từ Fact table đã seed.
