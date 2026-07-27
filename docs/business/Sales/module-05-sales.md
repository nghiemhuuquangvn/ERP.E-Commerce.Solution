# Module 5 — Sales
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Phụ thuộc**: Module 5 cần Module 3 (Item, Customer, PriceList, PaymentTerm, Currency) và Module 1 (Department/CostCenter cho báo cáo doanh số theo đơn vị) hoàn thiện trước. Đây cũng là module có tác động doanh thu trực tiếp — cần review kỹ Business Rules trước khi code, vì sai ở đây ảnh hưởng đến Finance ngay lập tức.

---

## 1. Mục tiêu và phạm vi

Sales quản lý toàn bộ vòng đời bán hàng: báo giá → đơn hàng → giao hàng → hoá đơn → trả hàng. Đây là module lớn nhất trong ERP-Ecommerce vì kết nối trực tiếp với Ecommerce (Module 18), Warehouse (Module 7), và Finance (Module 12).

Phạm vi **không bao gồm**: xử lý thanh toán online/checkout UI (thuộc Ecommerce Module 18 — Sales chỉ nhận `Invoice` đã tạo và theo dõi công nợ), quản lý tồn kho vật lý (thuộc Warehouse — Sales chỉ *reserve*, không trừ kho trực tiếp).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Sales Rep | Tạo Quotation/Order, theo dõi Commission |
| Sales Manager | Duyệt Discount vượt hạn mức, xem Sales Forecast |
| Customer (qua Ecommerce) | Tạo Order trực tiếp từ website (Order Source = Ecommerce) |
| Warehouse | Nhận yêu cầu Delivery, xác nhận đã giao |
| Finance | Xác nhận Invoice, xử lý Credit Note, đối chiếu công nợ |

## 3. Quy trình nghiệp vụ chính

1. **Quotation** → khách duyệt → convert thành **Sales Order** (giữ nguyên giá đã báo, hoặc áp dụng Price List mới nếu quotation hết hạn).
2. Sales Order → kiểm tra tồn kho khả dụng (gọi sang Inventory) → nếu đủ, tạo **Delivery Request** gửi Warehouse.
3. Warehouse xác nhận xuất kho → Delivery hoàn tất → tự động tạo **Invoice** (hoặc tạo thủ công nếu tách chu kỳ xuất hoá đơn).
4. Khách trả hàng → tạo **Return** (tham chiếu Delivery/Invoice gốc) → nếu đã thanh toán, tạo **Credit Note**.
5. Discount/Promotion áp dụng ở cấp Order hoặc từng dòng (line-level), theo rule (% , số tiền cố định, mua X tặng Y).
6. Cuối kỳ, tính **Commission** cho Sales Rep dựa trên Invoice đã thu tiền (không tính trên Order chưa giao, tránh commission ảo).
7. **Sales Forecast**: dự báo dựa trên lịch sử Order + pipeline Quotation đang mở (không phải AI phức tạp ở giai đoạn đầu — có thể là moving average đơn giản trước).

## 4. Domain Model (Aggregate Roots — DDD)

```
SalesQuotation (Aggregate Root)
 └─ SalesQuotationLine (Entity)

SalesOrder (Aggregate Root)
 ├─ SalesOrderLine (Entity)
 ├─ SalesOrderDiscount (Entity — áp dụng Promotion)
 └─ trạng thái: Draft → Confirmed → PartiallyDelivered → Delivered → Invoiced → Closed → Cancelled

Delivery (Aggregate Root)
 └─ DeliveryLine (Entity, tham chiếu SalesOrderLine)

Invoice (Aggregate Root)
 ├─ InvoiceLine (Entity)
 └─ trạng thái: Draft → Issued → PartiallyPaid → Paid → Overdue

SalesReturn (Aggregate Root)
 └─ SalesReturnLine (Entity, tham chiếu DeliveryLine/InvoiceLine)

CreditNote (Aggregate Root, tham chiếu SalesReturn hoặc Invoice)

Promotion (Aggregate Root) — rule engine đơn giản (% / fixed / buy-x-get-y)
Commission (Aggregate Root) — CommissionPlan (rule) + CommissionRecord (kết quả tính)
SalesForecast (Read Model — build từ aggregate query, không phải bảng nhập liệu)
```

**Bounded Context**: `Sales` — độc lập, chỉ tham chiếu ID sang `MasterData` (Item, Customer, PriceList) và `Organization` (Department/CostCenter), publish event cho `Warehouse`/`Finance`, không gọi trực tiếp DB của các context đó.

## 5. Database Design (đề xuất bảng)

**Nhóm Quotation & Order**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `SalesQuotations` | Id, CustomerId, QuotationNo, Status, ValidUntil, CurrencyId, TotalAmount |
| 2 | `SalesQuotationLines` | Id, QuotationId, ItemId, VariantId, Quantity, UnitPrice, DiscountPercent |
| 3 | `SalesOrders` | Id, QuotationId (nullable), CustomerId, OrderNo, OrderSource (Manual/Ecommerce/POS), Status, DepartmentId, SalesRepId (FK→User) |
| 4 | `SalesOrderLines` | Id, OrderId, ItemId, VariantId, Quantity, UnitPrice, DiscountAmount, TaxAmount, LineTotal |
| 5 | `SalesOrderStatusHistories` | Id, OrderId, FromStatus, ToStatus, ChangedAt, ChangedBy | Audit trail trạng thái, quan trọng cho tranh chấp sau này |

**Nhóm Delivery & Invoice**
| # | Bảng | Cột chính |
|---|---|---|
| 6 | `Deliveries` | Id, OrderId, DeliveryNo, WarehouseId (FK→đã có), Status, ShippedDate, ShippingMethodId (FK→đã có) |
| 7 | `DeliveryLines` | Id, DeliveryId, OrderLineId, QuantityDelivered |
| 8 | `Invoices` | Id, OrderId, DeliveryId (nullable), InvoiceNo, IssuedDate, DueDate, PaymentTermId (FK→đã có), Status, TotalAmount, PaidAmount |
| 9 | `InvoiceLines` | Id, InvoiceId, ItemId, Quantity, UnitPrice, TaxAmount, LineTotal |
| 10 | `Payments` | Id, InvoiceId, Amount, PaidDate, PaymentMethod, ReferenceNo | Ghi nhận thu tiền — cân nhắc: có thể thuộc Finance thay vì Sales, xem mục 10 |

**Nhóm Return & Credit**
| # | Bảng | Cột chính |
|---|---|---|
| 11 | `SalesReturns` | Id, DeliveryId, ReturnNo, Reason, Status |
| 12 | `SalesReturnLines` | Id, ReturnId, DeliveryLineId, QuantityReturned |
| 13 | `CreditNotes` | Id, ReturnId (nullable), InvoiceId, CreditNoteNo, Amount, Status |

**Nhóm Discount/Promotion/Commission**
| # | Bảng | Cột chính |
|---|---|---|
| 14 | `Promotions` | Id, Code, Name, Type (Percent/FixedAmount/BuyXGetY), EffectiveFrom, EffectiveTo, Conditions (JSON) |
| 15 | `PromotionApplications` | Id, PromotionId, OrderId, DiscountAmount |
| 16 | `CommissionPlans` | Id, Name, RateType (Percent/Tiered), RateValue |
| 17 | `CommissionRecords` | Id, PlanId, SalesRepId, InvoiceId, Amount, CalculatedAt, Status (Pending/Approved/Paid) |

→ **17 bảng cốt lõi**, không phải ~100 như ước tính ban đầu. Con số 100 trong bản gốc rất có thể tính luôn các bảng phụ (audit, config, template email, log...) — với module mới build lần đầu, 17 bảng lõi + mở rộng dần theo nhu cầu là hợp lý hơn nhiều so với thiết kế 100 bảng trước khi có dữ liệu thực tế.

## 6. API Design (REST)

```
POST   /api/v1/sales/quotations                          → ApiResponse<SalesQuotationDto>
POST   /api/v1/sales/quotations/{id}/convert-to-order
GET    /api/v1/sales/orders?customerId=&status=&from=&to= → PaginatedResult<SalesOrderDto>
POST   /api/v1/sales/orders/{id}/confirm
POST   /api/v1/sales/orders/{id}/request-delivery         → gửi event sang Warehouse
POST   /api/v1/sales/deliveries/{id}/complete              → trigger tạo Invoice
GET    /api/v1/sales/invoices/{id}/payment-status
POST   /api/v1/sales/returns
GET    /api/v1/sales/commissions?salesRepId=&period=
GET    /api/v1/sales/forecast?departmentId=&period=
```

## 7. Event Design

| Event | Trigger | Consumer |
|---|---|---|
| `SalesOrderConfirmed` | Xác nhận Order | Inventory (reserve stock) |
| `DeliveryRequested` | Yêu cầu giao hàng | Warehouse |
| `DeliveryCompleted` | Warehouse xác nhận xuất kho | Sales (tạo Invoice), Inventory (trừ kho thật) |
| `InvoiceIssued` | Phát hành hoá đơn | Finance (ghi nhận công nợ AR) |
| `InvoicePaid` | Ghi nhận thanh toán đủ | Commission (kích hoạt tính hoa hồng) |
| `SalesReturnApproved` | Duyệt trả hàng | Inventory (nhập lại kho), Finance (tạo Credit Note) |

## 8. Business Rules & Validation

- Sales Order không cho confirm nếu `Customer.CreditLimit` vượt quá công nợ hiện tại + giá trị Order mới (cần query sang Finance hoặc cache credit balance).
- Discount vượt hạn mức cho phép của Sales Rep → bắt buộc workflow approval (liên kết Module 19 Workflow Engine sau này; trước mắt có thể chỉ cần flag `RequiresApproval` + `ApprovedBy`).
- `SalesOrderLine.Quantity` đã Deliver không được vượt quá Quantity đặt hàng (partial delivery hợp lệ, over-delivery thì không).
- Commission chỉ tính trên `InvoicePaid`, không tính trên `SalesOrderConfirmed` — tránh commission ảo khi khách chưa thanh toán/huỷ đơn.
- Return chỉ cho phép trong thời hạn cấu hình (VD: 30 ngày từ Delivery) trừ khi có phê duyệt đặc biệt.
- Giá trong `SalesOrderLine` phải **snapshot** tại thời điểm tạo đơn (copy từ PriceList, không tham chiếu động) — nếu PriceList đổi giá sau đó, Order cũ không bị ảnh hưởng.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 3 (Master Data) | Item, Customer, PriceList, PaymentTerm — read-only reference |
| Module 7 (Warehouse) | Event-driven: `DeliveryRequested` → `DeliveryCompleted` |
| Module 12 (Finance) | Event-driven: `InvoiceIssued`/`InvoicePaid` — Sales không tự giữ sổ AR, chỉ publish, Finance là nguồn sự thật kế toán |
| Module 18 (Ecommerce) | `SalesOrder.OrderSource = Ecommerce` — Order tạo từ website gọi thẳng API này, không có UI riêng |

## 10. Điểm cần quyết định trước khi code

- **`Payments` nên thuộc Sales hay Finance?** Khuyến nghị: Sales chỉ lưu `PaidAmount` tổng hợp trên Invoice (denormalized, cập nhật qua event), còn chi tiết từng lần thu tiền/đối chiếu ngân hàng thuộc Finance (Module 12, bảng AR). Tránh 2 module cùng là nguồn sự thật cho một loại dữ liệu.
- **Promotion rule engine**: nếu rule phức tạp (buy-x-get-y, combo, theo tier khách hàng), nên dùng JSON `Conditions` + interpreter đơn giản trong code trước, chưa cần rule engine tổng quát (Module 19) ngay — tránh over-engineering khi chưa có đủ use case thực tế.

## 11. Khuyến nghị triển khai .NET

- `SalesOrder` là Aggregate Root lớn — dùng `IUnitOfWork` để đảm bảo Order + OrderLines + StatusHistory ghi cùng transaction.
- State machine cho `SalesOrder.Status` nên implement rõ ràng (enum + validate transition hợp lệ), tránh set status tuỳ tiện từ nhiều nơi trong code — dễ sinh ra trạng thái không nhất quán.
- Đánh index `(CustomerId, Status, CreatedAt)` trên `SalesOrders` — đây là truy vấn phổ biến nhất cho dashboard Sales.
- Domain Event nội bộ (MediatR) trước, để dễ tách sang Kafka/RabbitMQ thật sau khi có nhiều service độc lập cần consume.

## 12. So sánh & Reconciliation với project hiện tại (2026-07-27)

`SaleOrderService` đã tồn tại từ trước với duy nhất aggregate `SalesOrder`
(+ owned LineItems/Notes/StatusHistory). So sánh với đề xuất ở mục 4-6:

| Hạng mục đề xuất | Trạng thái | Ghi chú |
|---|---|---|
| `SalesQuotation` + `SalesQuotationLine` | ✅ Đã build | Aggregate mới, lifecycle Draft→Sent→Accepted/Rejected/Expired→Converted, convert-to-order snapshot giá y nguyên (chưa re-price theo PriceList mới khi hết hạn — để giai đoạn sau) |
| `SalesOrder.OrderNo`, `OrderSource`, `DepartmentId`, `SalesRepId`, `QuotationId` | ✅ Đã build | Sinh `OrderNo`/`QuotationNo` qua counter theo tenant/tháng (`SO-yyyyMM-00001`), không phải cột có sẵn |
| `SalesOrderLine.VariantId`, line-level `DiscountAmount`/`TaxAmount` | ⏸ Chưa làm | Không có nguồn dữ liệu sinh ra 2 field này hiện tại (chỉ có discount ở cấp Order qua coupon); cột đã tồn tại sẵn trong DB script gốc nhưng chưa map — để khi có nhu cầu thật |
| `Delivery`/`DeliveryLine` | ✅ Đã có sẵn tương đương | Không build aggregate riêng — đã có Ship/ShipLineItems/MarkAsDelivered + tích hợp ShippingService, đúng tinh thần đề xuất mà không trùng lặp |
| `Invoice`/`InvoiceLine`, `InvoiceIssued`, endpoint payment-status | ❌ Không build ở đây | **AccountingService đã tự tạo Invoice** ngay khi nhận `SalesOrderCreatedEvent` (`SalesOrderCreatedEventHandler`) — đây là module sở hữu thật, Sales không cần (và không nên) làm lại |
| `Payments` | ❌ Không build ở đây | PaymentService sở hữu; SaleOrderService chỉ consume `PaymentProcessedEvent`/`PaymentFailedEvent` — đúng khuyến nghị ở mục 10 của tài liệu gốc |
| `SalesReturn`/`SalesReturnLine`, `CreditNote` | ✅ Return đã có sẵn / ❌ CreditNote chưa | Return đã là action có sẵn trên `SalesOrder` (RequestReturn/CompleteReturn); CreditNote là nghiệp vụ kế toán, nên để AccountingService phản ứng với `SalesOrderReturnedEvent`, chưa build |
| `Promotion` rule engine | ❌ Không build ở đây | PromotionService sở hữu; Sales chỉ gọi validate coupon — nhưng trước đây `PromotionId` bị bỏ qua sau khi validate (bug thật), nay đã lưu lại cùng `DiscountAmount` |
| `Commission`/`CommissionPlan`/`CommissionRecord` | ✅ Đã build | Không service nào khác nhận phần này; trigger bằng `PaymentProcessedEvent` (đã consume sẵn) thay vì `InvoicePaid` (event này không tồn tại trong hệ thống) |
| `SalesForecast` | ❌ Không build ở đây | Thuộc trách nhiệm ReportingService (BI/analytics), không phải Sales |
| Credit limit check (`Customer.CreditLimit`) | ✅ Đã build, nhưng khác thiết kế gốc | Không service nào trong hệ thống có field `CreditLimit` (CRMService không có Customer entity, User của Identity cũng không) → Sales tự sở hữu qua `CustomerSalesProfile` mới, không sync từ nơi khác |
| Tên event đề xuất (`SalesOrderConfirmed`, `DeliveryRequested`, `InvoicePaid`...) | ❌ Không đổi tên | Không khớp với event thật lẫn CLAUDE.md; giữ nguyên `SalesOrderApprovedEvent`/`SalesOrderShippedEvent`/... đã có |
| Path API `/api/v1/sales/...` | ❌ Không đổi | Path thật là `/api/v1/orders/...`, quotation mới thêm dùng `/api/v1/quotations` (không có prefix `/sales/`) |

**Phát hiện thêm ngoài phạm vi tài liệu gốc**: 7 domain event của `SalesOrder`
trước đây publish thẳng ra RabbitMQ ngay sau khi lưu DB, không transactional —
nếu crash giữa 2 bước thì mất event. Đã retrofit Outbox pattern (bảng
`OutboxMessages` dùng chung với luồng Settings, phân biệt bằng `Type`) để đảm
bảo publish đáng tin cậy, đúng tinh thần "Outbox Pattern" đã nêu trong kiến
trúc tổng thể (CLAUDE.md) nhưng trước đây service này chưa thực sự áp dụng
cho các sự kiện nghiệp vụ chính.
