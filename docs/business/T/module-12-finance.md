# Module 12 — Finance & Accounting
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Module lớn nhất và rủi ro cao nhất** trong toàn hệ thống — sai ở đây không chỉ là bug, mà có thể sai lệch báo cáo tài chính/thuế thực tế. Khuyến nghị: build core GL/AR/AP trước, **hoãn** Budget/Multi-currency revaluation phức tạp cho đến khi có nhu cầu thật.

---

## 1. Mục tiêu và phạm vi

Finance là **nguồn sự thật kế toán duy nhất** (single source of truth) của toàn hệ thống — mọi module nghiệp vụ (Sales, Purchasing, Inventory) chỉ publish event, Finance là nơi ghi sổ cái (GL) theo nguyên tắc double-entry (ghi Nợ/Có cân bằng). Đây là nguyên tắc quan trọng nhất xuyên suốt module này.

Phạm vi **không bao gồm**: tạo Invoice gốc (thuộc Sales/Purchasing — Finance chỉ *ghi nhận* Invoice đã phát hành vào công nợ), tính giá vốn tồn kho (thuộc Inventory — Finance chỉ nhận snapshot đã chốt).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Accountant | Nhập Journal thủ công, đối chiếu Bank, chốt kỳ |
| Chief Accountant / CFO | Duyệt Closing, xem Budget vs Actual |
| Sales/Purchasing (hệ thống) | Publish event Invoice → tạo bút toán AR/AP tự động |
| Auditor | Xem GL, không có quyền sửa (chỉ Accountant/CFO có quyền ghi, và chỉ trong kỳ mở) |

## 3. Quy trình nghiệp vụ chính

1. Nhận `InvoiceIssued` từ Sales → tự động sinh **Journal Entry**: Nợ AR (Phải thu) / Có Doanh thu + Thuế đầu ra → ghi vào **GL**.
2. Nhận `InvoicePaid` (khách thanh toán) hoặc ghi nhận thủ công qua **Payment/Bank Reconciliation** → Nợ Cash/Bank / Có AR.
3. Tương tự cho Purchasing: nhận `SupplierInvoiceReceived` → Nợ Chi phí/Hàng tồn kho / Có AP → thanh toán nhà cung cấp → Nợ AP / Có Cash/Bank.
4. Nhận `InventoryValuationSnapshotClosed` từ Module 8 → bút toán điều chỉnh giá trị tồn kho nếu có chênh lệch (VD: sau kiểm kê).
5. Cuối kỳ (tháng): **Period Closing** — khoá kỳ cũ (không cho ghi thêm Journal vào kỳ đã đóng), tính chênh lệch tỷ giá cuối kỳ (nếu đa tiền tệ), lập báo cáo (P&L, Balance Sheet — thuộc BI/Dashboard, không phải bảng riêng ở đây).
6. **Budget**: nhập dự toán theo Department/CostCenter (liên kết Module 1) đầu kỳ → so sánh Actual (từ GL) vs Budget theo thời gian thực.

## 4. Domain Model (Aggregate Roots — DDD)

```
ChartOfAccounts (Aggregate Root, cây tài khoản kế toán — nền tảng, cần setup theo chuẩn kế toán VN nếu áp dụng tại VN)

JournalEntry (Aggregate Root — mọi bút toán, kể cả tự động lẫn thủ công)
 └─ JournalEntryLine (Entity) — mỗi dòng Nợ hoặc Có, tổng Nợ = tổng Có bắt buộc

GLEntry (Read Model / append-only — build từ JournalEntry đã Posted, dùng cho báo cáo nhanh)

AccountsReceivable (Aggregate Root — subledger theo Customer)
 └─ ARTransaction (Entity) — Invoice/Payment/CreditNote, tham chiếu ID từ Sales

AccountsPayable (Aggregate Root — subledger theo Supplier)
 └─ APTransaction (Entity)

CashAccount / BankAccount (Aggregate Root)
 └─ BankStatementLine (Entity) — nhập từ sao kê ngân hàng để đối chiếu

Budget (Aggregate Root)
 └─ BudgetLine (Entity) — theo Department/CostCenter/Account/Period

AccountingPeriod (Aggregate Root) — trạng thái Open/Closed, khoá ghi sổ khi Closed

TaxDeclaration (Aggregate Root — tổng hợp thuế đầu vào/đầu ra theo kỳ, phục vụ kê khai)
```

**Bounded Context**: `Finance` — là service **nhận nhiều event nhất** trong toàn hệ thống nhưng publish rất ít (chủ yếu publish `PeriodClosed` để khoá các module khác không ghi lùi kỳ).

## 5. Database Design (đề xuất bảng — nhóm theo phân hệ kế toán)

**Nhóm GL (lõi, bắt buộc có trước tất cả)**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `ChartOfAccounts` | Id, AccountCode, AccountName, AccountType (Asset/Liability/Equity/Revenue/Expense), ParentAccountId |
| 2 | `JournalEntries` | Id, JournalNo, EntryDate, PeriodId, SourceType (Sales/Purchasing/Manual/Inventory), SourceRefId, Status (Draft/Posted/Reversed), CreatedBy |
| 3 | `JournalEntryLines` | Id, JournalEntryId, AccountId, DebitAmount, CreditAmount, CostCenterId (FK→Module 1), CurrencyId, ExchangeRate |
| 4 | `AccountingPeriods` | Id, PeriodCode (2026-07), StartDate, EndDate, Status (Open/Closed), ClosedBy, ClosedAt |

**Nhóm AR/AP (subledger)**
| # | Bảng | Cột chính |
|---|---|---|
| 5 | `ARTransactions` | Id, CustomerId, SourceType (Invoice/Payment/CreditNote), SourceRefId, Amount, Balance, TransactionDate |
| 6 | `APTransactions` | Id, SupplierId, SourceType (SupplierInvoice/Payment/DebitNote), SourceRefId, Amount, Balance, TransactionDate |
| 7 | `PaymentReceipts` | Id, CustomerId, InvoiceId, Amount, PaymentMethod, ReceivedDate, BankAccountId |
| 8 | `PaymentDisbursements` | Id, SupplierId, SupplierInvoiceId, Amount, PaymentMethod, PaidDate, BankAccountId |

**Nhóm Cash/Bank**
| # | Bảng | Cột chính |
|---|---|---|
| 9 | `BankAccounts` | Id, CompanyId (FK→Module 1), BankName, AccountNumber, CurrencyId |
| 10 | `BankStatementLines` | Id, BankAccountId, TransactionDate, Amount, Description, IsReconciled, MatchedJournalEntryId |

**Nhóm Budget**
| # | Bảng | Cột chính |
|---|---|---|
| 11 | `Budgets` | Id, PeriodId, DepartmentId (FK→Module 1), Status |
| 12 | `BudgetLines` | Id, BudgetId, AccountId, CostCenterId, PlannedAmount |

**Nhóm Tax (kê khai, khác Tax Category ở Master Data — đây là tổng hợp báo cáo)**
| # | Bảng | Cột chính |
|---|---|---|
| 13 | `TaxDeclarations` | Id, PeriodId, TaxType (VAT/CIT), InputTaxAmount, OutputTaxAmount, PayableAmount, Status |

→ **13 bảng cốt lõi** cho GL/AR/AP/Cash/Budget/Tax cơ bản — rất xa so với ~200 bảng đề xuất ban đầu (200 bảng phù hợp cho hệ thống kế toán đa quốc gia/đa chuẩn IFRS phức tạp, không cần thiết ở giai đoạn hiện tại của một ERP-Ecommerce đơn công ty).

## 6. API Design (REST)

```
POST   /api/v1/finance/journal-entries                          → tạo thủ công, hoặc hệ thống tự tạo từ event
POST   /api/v1/finance/journal-entries/{id}/post                → chuyển Draft → Posted (bắt buộc validate Debit = Credit)
GET    /api/v1/finance/gl?accountId=&periodId=                  → PaginatedResult<GLEntryDto>
GET    /api/v1/finance/ar/customers/{id}/balance
GET    /api/v1/finance/ap/suppliers/{id}/balance
POST   /api/v1/finance/bank-statements/import                    → import file sao kê (Excel/MT940)
POST   /api/v1/finance/bank-statements/{id}/reconcile
POST   /api/v1/finance/periods/{id}/close                        → validate không còn Journal Draft trước khi khoá
GET    /api/v1/finance/reports/trial-balance?periodId=
GET    /api/v1/finance/budget/{departmentId}/vs-actual?periodId=
```

## 7. Event Design (Finance chủ yếu **consume**, publish rất ít để giữ vai trò nguồn sự thật)

| Event nhận | Từ module | Xử lý |
|---|---|---|
| `InvoiceIssued` | Sales | Tạo JournalEntry (AR/Revenue/Tax), tạo ARTransaction |
| `InvoicePaid` / thu tiền thủ công | Sales/Accountant | Tạo JournalEntry (Cash/AR), cập nhật ARTransaction.Balance |
| `SalesReturnApproved` → `CreditNoteIssued` | Sales | JournalEntry đảo Revenue/AR |
| `SupplierInvoiceReceived` | Purchasing (Module 6) | Tạo JournalEntry (Expense hoặc Inventory/AP), APTransaction |
| `InventoryValuationSnapshotClosed` | Inventory (Module 8) | JournalEntry điều chỉnh giá trị tồn kho nếu lệch |

| Event publish | Trigger | Consumer |
|---|---|---|
| `PeriodClosed` | Chốt kỳ kế toán | Tất cả module khác — khoá không cho tạo giao dịch lùi vào kỳ đã đóng (Sales không cho sửa Invoice cũ, Warehouse không cho ghi Receipt ngày cũ...) |

## 8. Business Rules & Validation

- **Nguyên tắc double-entry bất di bất dịch**: mọi `JournalEntry` khi Post phải có tổng `DebitAmount` = tổng `CreditAmount` trên toàn bộ `JournalEntryLines` — validate ở Application layer trước khi cho phép Post, không tin dữ liệu từ event.
- `JournalEntries.Status = Posted` là **immutable** — không sửa/xoá, chỉ được **Reverse** (tạo bút toán đảo ngược tham chiếu ngược lại) — đúng chuẩn kế toán, giữ toàn vẹn audit trail.
- Không cho ghi Journal vào `AccountingPeriod` có `Status = Closed` — kể cả bút toán tự động từ event khác (nếu event đến trễ sau khi đã đóng kỳ, phải route sang kỳ hiện tại kèm cảnh báo, không được ghi lùi).
- Multi-currency: `JournalEntryLine` lưu cả `CurrencyId` gốc và `ExchangeRate` tại thời điểm ghi — không tính lại giá trị quy đổi sau này (snapshot, giống nguyên tắc ở SalesOrderLine).
- Bank Reconciliation: một `BankStatementLine` chỉ match với đúng 1 `JournalEntry`/`PaymentReceipt` — cảnh báo nếu số tiền không khớp tuyệt đối (không tự động match gần đúng để tránh sai sót).

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 1 (Organization) | CostCenter/ProfitCenter/Department cho phân bổ GL và Budget |
| Module 3 (Master Data) | Currency/ExchangeRate — dùng snapshot tại thời điểm ghi, không tham chiếu động |
| Module 5 (Sales) | Event-driven: `InvoiceIssued`, `InvoicePaid`, `SalesReturnApproved` |
| Module 6 (Purchasing) | Event-driven: `SupplierInvoiceReceived`, thanh toán nhà cung cấp |
| Module 8 (Inventory) | Nhận `InventoryValuationSnapshotClosed` để ghi bút toán điều chỉnh tồn kho |

## 10. So sánh & Reconciliation với project hiện tại

Module này **hoàn toàn chưa có** trong project — không có phần tái sử dụng như các module trước. Điểm cần lưu ý duy nhất: `Currency`/`ExchangeRate` đã được thiết kế ở Module 3 (Master Data) — Finance **tái sử dụng**, không tạo lại, chỉ thêm bảng snapshot tỷ giá tại thời điểm ghi sổ (`JournalEntryLine.ExchangeRate`) để tránh phụ thuộc dữ liệu động thay đổi sau này.

## 11. Khuyến nghị triển khai .NET

- Đây là module nên áp dụng **Event Sourcing thực sự** rõ ràng nhất trong toàn hệ thống — `JournalEntries` về bản chất đã là event log immutable, cân nhắc thiết kế ngay từ đầu theo hướng này thay vì CRUD thông thường.
- Validate Debit = Credit nên nằm trong **Domain layer** (aggregate `JournalEntry` tự validate khi thêm line), không chỉ ở Application/API layer — đảm bảo bất biến (invariant) được bảo vệ ở mức domain, tránh trường hợp có luồng code khác vô tình bypass.
- Consumer xử lý event từ Sales/Purchasing/Inventory bắt buộc **idempotent** (dùng `SourceType + SourceRefId` làm unique constraint trên JournalEntries) — vì đây là nơi hậu quả của việc ghi trùng nghiêm trọng nhất trong toàn hệ thống.
- Period Closing nên là background job có checklist rõ ràng (không còn Journal Draft, đã đối chiếu Bank, đã chốt Inventory Valuation) trước khi cho phép `Status = Closed` — nên implement dạng Saga/Workflow thay vì 1 API call đơn giản.
- **Khuyến nghị mạnh**: cân nhắc thuê/tư vấn kế toán viên có chứng chỉ (hoặc dùng chuẩn tài khoản kế toán VN — Thông tư 200) để thiết kế `ChartOfAccounts` đúng ngay từ đầu — đây là phần duy nhất trong 20 module mà lỗi thiết kế sai không chỉ là bug kỹ thuật mà còn là rủi ro pháp lý/thuế.
