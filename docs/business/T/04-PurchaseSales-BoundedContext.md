# 04 — Bounded Context: Purchase & Sales (Accounting) — Mua hàng & Bán hàng (hạch toán)

> Tham chiếu nền tảng: `00-Ubiquitous-Language-Context-Map.md`. BC này là **Upstream** của `02-ARAP` (mỗi hoá đơn phát sinh công nợ) và `03-GeneralLedger` (mỗi hoá đơn phát sinh bút toán). Phạm vi: **hạch toán kế toán** của nghiệp vụ mua/bán hàng — không bao gồm quản lý kho/tồn kho chi tiết (thuộc BC Warehouse, ngoài phạm vi).
> Nguồn xác minh nghiệp vụ: `04.ASOFT-T\Business\ApplicationFacades\AF0063Facade.cs` (mua hàng), `AF0066Facade.cs` (bán hàng), `AF0069Facade.cs`/`AF0073Facade.cs` (trả lại), `DataAccess\ServiceComponents\AF3101Service.cs` (chiết khấu), `01.ASOFT-SM\Business\ApplicationFacades\AF0144Facade.cs` (bán hàng theo bộ/KIT), `04.ASOFT-T\Business\ApplicationFacades\AF0314Facade.cs` (phân bổ chi phí mua hàng).

## A. Tổng quan nghiệp vụ

**Mục đích**: ghi nhận hoá đơn mua hàng/bán hàng và các điều chỉnh liên quan (trả lại, chiết khấu, phân bổ chi phí mua hàng, bán hàng theo bộ) làm phát sinh công nợ và bút toán kế toán tương ứng, tuân thủ quy định thuế GTGT/TTĐB.

**Actor**: Kế toán mua hàng, Kế toán bán hàng, Kế toán thuế.

**Use case chính**:

| Use case | Mã gốc |
|---|---|
| UC-PS-01 Lập phiếu mua hàng | AF0063 |
| UC-PS-02 Lập hoá đơn bán hàng | AF0066 |
| UC-PS-03 Lập chứng từ hàng mua trả lại | AF0073 |
| UC-PS-04 Lập chứng từ hàng bán trả lại | AF0069 |
| UC-PS-05 Bán hàng theo bộ (KIT) | AF0144 |
| UC-PS-06 Lập phiếu chiết khấu | AF3101 |
| UC-PS-07 Phân bổ chi phí mua hàng | AF0314 |
| UC-PS-08 Kết chuyển bán hàng từ POS | AF0281 |

## B. Domain Model (DDD)

### B.1 Entity tham chiếu riêng của BC này

**`Item`** (Entity, reference data — Vật tư/Hàng hoá)

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| ItemCode | string, PK | |
| ItemName | string | |
| DefaultPurchaseAccountCode | FK Account | Mặc định TK Nợ khi mua |
| DefaultSalesAccountCode | FK Account | Mặc định TK Có (doanh thu) khi bán |
| CostingMethod | enum{FIFO, WeightedAverage, Specific} | |
| IsKit | bool | true nếu là mặt hàng "bộ" (bundle) |
| UnitOfMeasure | string | |
| Disabled | bool | |

**`KitFormula`** (Entity — công thức cấu thành hàng bộ)

| Thuộc tính | Kiểu |
|---|---|
| KitItemCode | FK Item (mặt hàng bộ) |
| ComponentItemCode | FK Item (mặt hàng thành phần) |
| ComponentQuantityRatio | decimal | Tỷ lệ/số lượng thành phần trên 1 đơn vị bộ |

**`VatType`** (Entity, reference data)

| Thuộc tính | Kiểu |
|---|---|
| VatTypeCode | string, PK |
| VatTypeName | string |
| IsInputVat | bool | true = hoá đơn đầu vào (mua), false = đầu ra (bán) |
| Rate | decimal |

### B.2 Aggregate: `PurchaseInvoice` (Phiếu mua hàng)

| Thuộc tính | Kiểu |
|---|---|
| InvoiceId | Guid, PK |
| BranchId | FK Branch |
| VoucherNo | string |
| VoucherDate | date |
| Partner | FK BusinessPartner (nhà cung cấp) |
| InvoiceNo / InvoiceDate / Serial | thông tin hoá đơn giấy/điện tử gốc |
| EInvoiceGuid | string, nullable |
| Currency, ExchangeRate | như Shared Kernel |
| Lines | List\<PurchaseInvoiceLine\> |
| EmployeeId | FK Employee |
| Description | string |

`PurchaseInvoiceLine`:

| Thuộc tính | Kiểu |
|---|---|
| LineNo | int |
| ItemCode | FK Item |
| Quantity | decimal |
| UnitPrice | decimal |
| Money | Value Object (thành tiền dòng) |
| DebitAccountCode | FK Account — mặc định từ `Item.DefaultPurchaseAccountCode`, có thể sửa tay |
| CreditAccountCode | FK Account — TK phải trả (`Partner.DefaultPayableAccount`) |
| VatTypeCode | FK VatType |
| VatMoney | Value Object |
| ExpenseMoney | Value Object — chi phí phân bổ thêm (từ `PurchaseCostAllocation`, mặc định 0) |
| AnalysisTags | List\<AnalysisTag\> |

**Invariant/Business rule**:
- **[PS-R01] Thuế GTGT tính theo `VatType.Rate` áp trên `Money` của dòng, ghi nhận riêng ở `VatMoney`, không cộng gộp vào `Money` của dòng hàng hoá.** Nguồn: `AF0063Facade.cs` khối xử lý VAT/`GetValueSQL118` làm tròn VAT.
- **[PS-R02] Thuế tiêu thụ đặc biệt (TTĐB)** áp dụng cho một số nhóm hàng theo quy định — công thức chi tiết **chưa xác minh đầy đủ** (thay đổi theo quy định 2026, xem mục F).
- **[PS-R03] Xác định tài khoản Nợ mặc định lấy từ `Item.DefaultPurchaseAccountCode`; người dùng có thể sửa tay theo từng dòng** — không phải rule cứng bắt buộc.
- **[PS-R04] Kiểm tra trùng số chứng từ/hoá đơn**: `VoucherNo` không trùng trong `(BranchId, VoucherTypeCode, Period)`; `InvoiceNo` không trùng trong phạm vi cùng `Partner` (đối với hoá đơn xuất). Nguồn: changelog `AF0066Facade.cs` "Check trùng số chứng từ", `IsExistSQL_154`.
- **[PS-R05]** áp dụng [GL-R02] khoá kỳ.

Domain Event: `PurchaseInvoicePosted{InvoiceId, PartnerId, Lines[]}` → (a) publish `PostingLineRecorded` cho từng dòng (Nợ `DebitAccountCode`+`VatMoney` nếu có TK thuế riêng, Có `CreditAccountCode`) tới GL; (b) publish tạo `OpenDebtItem{Side=Credit... }` — chính xác: hoá đơn mua hàng tăng phải trả, nên tạo `OpenDebtItem{Side=Credit, AccountCode=CreditAccountCode}` cho Debt BC theo dõi (vì "Credit" trong ngôn ngữ Debt BC = tăng phải trả/giảm phải thu, xem `02-ARAP-BoundedContext.md` mục B.1).

### B.3 Aggregate: `SalesInvoice` (Hoá đơn bán hàng)

Cấu trúc đối xứng `PurchaseInvoice` (đảo Nợ/Có: `DebitAccountCode` = phải thu, `CreditAccountCode` = doanh thu), bổ sung:

| Thuộc tính bổ sung | Kiểu |
|---|---|
| DiscountRate / DiscountAmount | trên từng `SalesInvoiceLine` |
| IsKitComponent / ParentLineNo | đánh dấu dòng được nổ ra từ 1 dòng hàng-bộ (KIT) |

**Invariant/Business rule** (ngoài các rule tương đương [PS-R01]-[PS-R05] phía bán):
- **[PS-R06] Nổ hàng bộ (KIT explosion)**: khi 1 dòng tham chiếu `Item.IsKit=true`, hệ thống tự sinh N dòng con theo `KitFormula` (mỗi dòng con `Quantity = ParentQuantity × ComponentQuantityRatio`); **`Σ Money(dòng con) = Money(dòng cha)`** (invariant bắt buộc — phân bổ giá trị dòng cha xuống các dòng con theo tỷ trọng, làm tròn dồn vào dòng cuối). Nguồn: `AF0144Facade.GetDataSQL064`, cờ `IsCom`.
- **[PS-R07]** áp dụng [PS-R01]-[PS-R05] tương ứng phía bán, và [DEBT-R01]-liên-quan: mỗi `SalesInvoice` publish tạo `OpenDebtItem{Side=Debit}` cho Debt BC.

Domain Event: `SalesInvoicePosted{InvoiceId, PartnerId, Lines[]}`.

### B.4 Aggregate: `PurchaseReturn`, `SalesReturn`

| Thuộc tính | Kiểu |
|---|---|
| ReturnId | Guid, PK |
| SourceInvoiceId | FK `PurchaseInvoice`/`SalesInvoice` (chứng từ gốc bị trả lại) |
| Lines | tương tự Invoice Line, số lượng/giá trị **âm hoặc dương với cờ đảo chiều** so với hoá đơn gốc |

**Invariant**:
- **[PS-08] `Σ Quantity(Return lines cho 1 ItemCode)` không được vượt `Σ Quantity` còn lại chưa trả của `SourceInvoiceId`** (chặn trả quá số đã mua/bán).
- Nếu dòng trả lại là hàng bộ (KIT), tham chiếu ngược `KitFormula` để nổ dòng con tương tự [PS-R06] (nguồn: `AF0069Facade.cs` gán `TableID="AT1326"` khi trả hàng bộ).
- Publish Domain Event **ngược chiều** so với hoá đơn gốc để Debt BC giảm tương ứng `OpenDebtItem` gốc (không tạo `OpenDebtItem` mới độc lập, mà điều chỉnh/giảm số dư gốc — chi tiết cơ chế **cần thiết kế thêm ở tầng Application**, xem mục F).

Domain Event: `PurchaseReturnPosted`, `SalesReturnPosted`.

### B.5 Aggregate: `DiscountVoucher` (Chiết khấu)

| Thuộc tính | Kiểu |
|---|---|
| DiscountId | Guid, PK |
| VoucherNo, BranchId, VoucherDate | như trên |
| SalesInvoiceId | FK `SalesInvoice` (hoá đơn được chiết khấu) |
| Partner | FK BusinessPartner |
| DiscountMoney | Value Object |
| Description | string |

**Invariant/Business rule**:
- **[PS-R09] `DiscountMoney` không được vượt số dư còn lại (chưa thu) của `SalesInvoiceId` tại thời điểm lập.** Nguồn: `AF3101Service.cs`, view `AV0274` (danh sách hoá đơn đủ điều kiện — lọc `TransactionTypeID='T04'`), trường `EndOriginalAmount`.
- Hệ cũ **không phát sinh bút toán Nợ/Có GL mới** khi lập chiết khấu — chỉ giảm trực tiếp số dư còn phải thu của hoá đơn gốc (một dạng giải trừ công nợ đặc biệt, không qua khớp với 1 khoản thu tiền thật).
  ⚠️ **[Quyết định cần xác nhận — xem mục F]**: về bản chất kế toán chuẩn, chiết khấu thương mại/thanh toán phải ghi nhận bút toán (Nợ TK chiết khấu 521/635, Có TK phải thu 131) để phản ánh đúng doanh thu thuần và số dư công nợ nhất quán với General Ledger. Khuyến nghị hệ mới: `DiscountVoucher` khi hoàn tất publish **cả hai** — (a) `PostingLineRecorded` tới GL (Nợ chi phí/giảm doanh thu, Có phải thu) và (b) một `OpenDebtItem{Side=Credit}` tương ứng để Debt BC khớp tự động với hoá đơn gốc qua `DebtMatch` (tái sử dụng cơ chế khớp công nợ mở thay vì cơ chế giảm-trực-tiếp `EndOriginalAmount` của hệ cũ) — nhất quán hơn với kiến trúc DDD đã chọn, cần chủ sản phẩm xác nhận trước khi code-gen.

Domain Event: `DiscountGranted{DiscountId, SalesInvoiceId, DiscountMoney}`.

### B.6 Aggregate: `PurchaseCostAllocation` (Phân bổ chi phí mua hàng)

| Thuộc tính | Kiểu |
|---|---|
| AllocationId | Guid, PK |
| PurchaseInvoiceId | FK `PurchaseInvoice` |
| TotalCostMoney | Value Object (tổng chi phí cần phân bổ — vận chuyển, nhập khẩu...) |
| AllocationBasis | enum{ByLineValue, ByQuantity} |
| Lines | List\<PurchaseCostAllocationLine\> |

`PurchaseCostAllocationLine`: `PurchaseInvoiceLineId`, `AllocatedMoney`.

**Invariant**:
- **[PS-R10] `Σ AllocatedMoney = TotalCostMoney`** (phân bổ hết, không dư/thiếu — làm tròn dồn vào dòng cuối).
- Sau khi phân bổ, cập nhật `PurchaseInvoiceLine.ExpenseMoney` tương ứng (ảnh hưởng giá vốn hàng nhập).
- ⚠️ Cơ sở phân bổ chính xác (theo giá trị dòng hay theo số lượng) **chưa xác minh đầy đủ** từ `AF0314Facade.cs` (146 dòng, khảo sát chưa sâu) — xem mục F.

Domain Event: `PurchaseCostAllocated{AllocationId, PurchaseInvoiceId, Lines[]}`.

## C. Đề xuất DB Schema mới

```sql
CREATE TABLE item (
  id                          BIGINT IDENTITY PRIMARY KEY,
  item_code                   VARCHAR(30) NOT NULL UNIQUE,
  item_name                   NVARCHAR(200) NOT NULL,
  default_purchase_account_code VARCHAR(20) NULL REFERENCES account(account_code),
  default_sales_account_code    VARCHAR(20) NULL REFERENCES account(account_code),
  costing_method               VARCHAR(20) NOT NULL DEFAULT 'WeightedAverage',
  is_kit                        BIT NOT NULL DEFAULT 0,
  unit_of_measure               VARCHAR(20),
  disabled                      BIT NOT NULL DEFAULT 0
);

CREATE TABLE kit_formula (
  id                       BIGINT IDENTITY PRIMARY KEY,
  kit_item_code            VARCHAR(30) NOT NULL REFERENCES item(item_code),
  component_item_code      VARCHAR(30) NOT NULL REFERENCES item(item_code),
  component_quantity_ratio DECIMAL(18,6) NOT NULL,
  UNIQUE (kit_item_code, component_item_code)
);

CREATE TABLE vat_type (
  id            BIGINT IDENTITY PRIMARY KEY,
  vat_type_code VARCHAR(20) NOT NULL UNIQUE,
  vat_type_name NVARCHAR(100) NOT NULL,
  is_input_vat  BIT NOT NULL,
  rate          DECIMAL(9,4) NOT NULL
);

CREATE TABLE purchase_invoice (
  id             BIGINT IDENTITY PRIMARY KEY,
  branch_id      BIGINT NOT NULL REFERENCES branch(id),
  voucher_no     VARCHAR(30) NOT NULL,
  voucher_date   DATE NOT NULL,
  partner_id     BIGINT NOT NULL REFERENCES business_partner(id),
  invoice_no     VARCHAR(30),
  invoice_date   DATE,
  serial         VARCHAR(20),
  e_invoice_guid VARCHAR(100),
  currency_code  CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate  DECIMAL(18,6) NOT NULL,
  employee_id    BIGINT NOT NULL REFERENCES employee(id),
  description    NVARCHAR(500),
  created_at     DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE purchase_invoice_line (
  id                 BIGINT IDENTITY PRIMARY KEY,
  purchase_invoice_id BIGINT NOT NULL REFERENCES purchase_invoice(id),
  line_no             INT NOT NULL,
  item_code            VARCHAR(30) NOT NULL REFERENCES item(item_code),
  quantity              DECIMAL(18,4) NOT NULL,
  unit_price            DECIMAL(18,4) NOT NULL,
  original_amount       DECIMAL(18,2) NOT NULL,
  converted_amount      DECIMAL(18,2) NOT NULL,
  debit_account_code    VARCHAR(20) NOT NULL REFERENCES account(account_code),
  credit_account_code   VARCHAR(20) NOT NULL REFERENCES account(account_code),
  vat_type_code         VARCHAR(20) NULL REFERENCES vat_type(vat_type_code),
  vat_original_amount   DECIMAL(18,2) NOT NULL DEFAULT 0,
  vat_converted_amount  DECIMAL(18,2) NOT NULL DEFAULT 0,
  expense_original_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
  UNIQUE (purchase_invoice_id, line_no)
);

-- sales_invoice / sales_invoice_line: cấu trúc đối xứng, bổ sung 2 cột dưới đây trên line
-- discount_rate DECIMAL(9,4), discount_amount DECIMAL(18,2),
-- is_kit_component BIT NOT NULL DEFAULT 0, parent_line_no INT NULL
CREATE TABLE sales_invoice (
  id             BIGINT IDENTITY PRIMARY KEY,
  branch_id      BIGINT NOT NULL REFERENCES branch(id),
  voucher_no     VARCHAR(30) NOT NULL,
  voucher_date   DATE NOT NULL,
  partner_id     BIGINT NOT NULL REFERENCES business_partner(id),
  invoice_no     VARCHAR(30),
  invoice_date   DATE,
  serial         VARCHAR(20),
  e_invoice_guid VARCHAR(100),
  currency_code  CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate  DECIMAL(18,6) NOT NULL,
  employee_id    BIGINT NOT NULL REFERENCES employee(id),
  description    NVARCHAR(500),
  created_at     DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE sales_invoice_line (
  id                BIGINT IDENTITY PRIMARY KEY,
  sales_invoice_id  BIGINT NOT NULL REFERENCES sales_invoice(id),
  line_no            INT NOT NULL,
  item_code           VARCHAR(30) NOT NULL REFERENCES item(item_code),
  quantity             DECIMAL(18,4) NOT NULL,
  unit_price           DECIMAL(18,4) NOT NULL,
  original_amount      DECIMAL(18,2) NOT NULL,
  converted_amount     DECIMAL(18,2) NOT NULL,
  discount_rate         DECIMAL(9,4) NOT NULL DEFAULT 0,
  discount_amount       DECIMAL(18,2) NOT NULL DEFAULT 0,
  debit_account_code    VARCHAR(20) NOT NULL REFERENCES account(account_code),
  credit_account_code   VARCHAR(20) NOT NULL REFERENCES account(account_code),
  vat_type_code         VARCHAR(20) NULL REFERENCES vat_type(vat_type_code),
  vat_original_amount   DECIMAL(18,2) NOT NULL DEFAULT 0,
  vat_converted_amount  DECIMAL(18,2) NOT NULL DEFAULT 0,
  is_kit_component      BIT NOT NULL DEFAULT 0,
  parent_line_no        INT NULL,
  UNIQUE (sales_invoice_id, line_no)
);

CREATE TABLE purchase_return (
  id                    BIGINT IDENTITY PRIMARY KEY,
  branch_id             BIGINT NOT NULL REFERENCES branch(id),
  voucher_no            VARCHAR(30) NOT NULL,
  voucher_date          DATE NOT NULL,
  source_invoice_id     BIGINT NOT NULL REFERENCES purchase_invoice(id),
  created_at            DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);
CREATE TABLE purchase_return_line (
  id                    BIGINT IDENTITY PRIMARY KEY,
  purchase_return_id    BIGINT NOT NULL REFERENCES purchase_return(id),
  source_line_id        BIGINT NOT NULL REFERENCES purchase_invoice_line(id),
  quantity               DECIMAL(18,4) NOT NULL,
  original_amount        DECIMAL(18,2) NOT NULL
);

-- sales_return / sales_return_line: cấu trúc đối xứng tham chiếu sales_invoice(_line)
CREATE TABLE sales_return (
  id                    BIGINT IDENTITY PRIMARY KEY,
  branch_id             BIGINT NOT NULL REFERENCES branch(id),
  voucher_no            VARCHAR(30) NOT NULL,
  voucher_date          DATE NOT NULL,
  source_invoice_id     BIGINT NOT NULL REFERENCES sales_invoice(id),
  created_at            DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);
CREATE TABLE sales_return_line (
  id                 BIGINT IDENTITY PRIMARY KEY,
  sales_return_id    BIGINT NOT NULL REFERENCES sales_return(id),
  source_line_id     BIGINT NOT NULL REFERENCES sales_invoice_line(id),
  quantity            DECIMAL(18,4) NOT NULL,
  original_amount     DECIMAL(18,2) NOT NULL
);

CREATE TABLE discount_voucher (
  id                 BIGINT IDENTITY PRIMARY KEY,
  voucher_no         VARCHAR(30) NOT NULL,
  branch_id          BIGINT NOT NULL REFERENCES branch(id),
  voucher_date       DATE NOT NULL,
  sales_invoice_id   BIGINT NOT NULL REFERENCES sales_invoice(id),
  partner_id         BIGINT NOT NULL REFERENCES business_partner(id),
  discount_original_amount  DECIMAL(18,2) NOT NULL,
  discount_converted_amount DECIMAL(18,2) NOT NULL,
  description         NVARCHAR(500),
  created_at           DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE purchase_cost_allocation (
  id                    BIGINT IDENTITY PRIMARY KEY,
  purchase_invoice_id   BIGINT NOT NULL REFERENCES purchase_invoice(id),
  total_cost_original_amount DECIMAL(18,2) NOT NULL,
  allocation_basis       VARCHAR(20) NOT NULL CHECK (allocation_basis IN ('ByLineValue','ByQuantity')),
  created_at              DATETIME2 NOT NULL
);
CREATE TABLE purchase_cost_allocation_line (
  id                          BIGINT IDENTITY PRIMARY KEY,
  purchase_cost_allocation_id BIGINT NOT NULL REFERENCES purchase_cost_allocation(id),
  purchase_invoice_line_id    BIGINT NOT NULL REFERENCES purchase_invoice_line(id),
  allocated_original_amount   DECIMAL(18,2) NOT NULL
);
```

**Mapping ngược hệ cũ**: `item` ↔ `AT1302`; `kit_formula` ↔ `AT1326`; `vat_type` ↔ `AT1009`; `purchase_invoice(_line)`/`sales_invoice(_line)` ↔ `AT9000` lọc theo `TransactionTypeID` của AF0063/AF0066; `purchase_return`/`sales_return` ↔ `AT9000` (AF0073/AF0069); `discount_voucher` ↔ `AT3101`; `purchase_cost_allocation(_line)` ↔ dữ liệu AF0314 (chưa xác định rõ bảng gốc, xem mục F).

## D. BDD Scenarios (Gherkin)

```gherkin
Feature: Lập hoá đơn bán hàng
  # UC-PS-02

  Scenario: Tính thuế GTGT đầu ra đúng theo loại thuế
    # [PS-R01]
    Given loại thuế "VAT10" có thuế suất 10%
    When kế toán lập hoá đơn bán hàng 1 dòng hàng hoá trị giá 10,000,000 VND với loại thuế "VAT10"
    Then dòng hàng hoá có VatMoney = 1,000,000 VND

  Scenario: Chặn lập hoá đơn trùng số hoá đơn cho cùng đối tượng
    # [PS-R04]
    Given đối tượng "KH005" đã có hoá đơn số "INV-2026-001"
    When kế toán lập thêm 1 hoá đơn bán hàng số "INV-2026-001" cho "KH005"
    Then hệ thống từ chối lưu vì trùng số hoá đơn

  Scenario: Tài khoản Nợ/Có mặc định lấy từ danh mục vật tư nhưng cho phép sửa tay
    # [PS-R03]
    Given vật tư "VT001" có TK doanh thu mặc định là "5111"
    When kế toán thêm dòng hàng hoá "VT001" vào hoá đơn bán hàng
    Then TK Có của dòng mặc định là "5111"
    When kế toán sửa tay TK Có của dòng thành "5112"
    Then hệ thống chấp nhận giá trị đã sửa

  Scenario: Nổ hàng bán theo bộ đúng công thức và tổng giá trị
    # [PS-R06]
    Given mặt hàng bộ "BO001" gồm 2 thành phần: "TP01" tỷ lệ 2, "TP02" tỷ lệ 3
    When kế toán lập hoá đơn bán 1 "BO001" với thành tiền dòng cha là 5,000,000 VND
    Then hệ thống tự sinh 2 dòng con cho "TP01" và "TP02"
    And tổng thành tiền 2 dòng con bằng đúng 5,000,000 VND

Feature: Chiết khấu hoá đơn bán hàng
  # UC-PS-06

  Scenario: Chặn chiết khấu vượt số dư còn lại của hoá đơn
    # [PS-R09]
    Given hoá đơn "INV-2026-010" còn phải thu 3,000,000 VND
    When kế toán lập phiếu chiết khấu 4,000,000 VND cho "INV-2026-010"
    Then hệ thống từ chối lưu vì vượt số dư còn lại

  Scenario: Chiết khấu hợp lệ làm giảm đúng số dư còn lại
    # [PS-R09]
    Given hoá đơn "INV-2026-011" còn phải thu 5,000,000 VND
    When kế toán lập phiếu chiết khấu 2,000,000 VND cho "INV-2026-011"
    Then số dư còn lại của "INV-2026-011" (phản ánh qua OpenDebtItem liên quan) giảm còn 3,000,000 VND

Feature: Phân bổ chi phí mua hàng
  # UC-PS-07

  Scenario: Tổng chi phí phân bổ khớp đúng tổng chi phí khai báo
    # [PS-R10]
    Given phiếu mua hàng "PMH001" có 2 dòng: dòng 1 trị giá 6,000,000 VND, dòng 2 trị giá 4,000,000 VND
    When kế toán phân bổ chi phí vận chuyển 1,000,000 VND theo giá trị dòng
    Then dòng 1 nhận chi phí phân bổ 600,000 VND
    And dòng 2 nhận chi phí phân bổ 400,000 VND
    And tổng chi phí đã phân bổ bằng đúng 1,000,000 VND
```

## E. TDD Test Plan

| Layer | Test case | Map Gherkin |
|---|---|---|
| Domain unit | `Should_ComputeVatAmount_ByLineAmountAndRate` | "Tính thuế GTGT đầu ra đúng theo loại thuế" |
| Domain unit | `Should_RejectInvoice_When_InvoiceNoDuplicatedForSamePartner` | "Chặn lập hoá đơn trùng số hoá đơn..." |
| Domain unit | `Should_DefaultAccountFromItemMaster_ButAllowOverride` | "Tài khoản Nợ/Có mặc định lấy từ danh mục vật tư..." |
| Domain unit | `Should_ExplodeKitLine_IntoComponentLines_SummingToParentAmount` | "Nổ hàng bán theo bộ đúng công thức..." |
| Domain unit | `Should_RejectDiscount_When_ExceedsRemainingInvoiceAmount` | "Chặn chiết khấu vượt số dư còn lại..." |
| Domain unit | `Should_ReduceRemainingAmount_When_DiscountGranted` | "Chiết khấu hợp lệ làm giảm đúng số dư..." |
| Domain unit | `Should_AllocateCostProportionally_ByLineValue_SummingToTotal` | "Tổng chi phí phân bổ khớp đúng tổng chi phí khai báo" |
| Domain unit | `Should_RejectReturn_When_QuantityExceedsRemainingSourceQuantity` | [PS-08] |
| Application/use-case | `Should_PublishOpenDebtItem_When_SalesInvoicePosted` | tích hợp với BC 02 |
| Application/use-case | `Should_PublishOpenDebtItem_When_PurchaseInvoicePosted` | tích hợp với BC 02 |
| Application/use-case | `Should_PublishPostingLineRecorded_ForEachInvoiceLine_When_InvoicePosted` | tích hợp với BC 03 |
| Integration | `Should_EnforceUniqueVoucherNoPerBranch_On_SalesInvoiceAndPurchaseInvoice` | ràng buộc DB |
| Acceptance | Toàn bộ scenario mục D | 1-1 |

## F. Open Questions / Gaps

1. **[PS-R02]** Công thức tính thuế tiêu thụ đặc biệt (TTĐB) theo quy định mới (ghi nhận trong changelog `AF0066Facade.cs` ngày 2026/06/03) chưa được đọc chi tiết trong lần khảo sát — cần xác nhận công thức cụ thể và danh mục hàng hoá chịu thuế TTĐB trước khi code-gen.
2. **Cơ chế giảm `OpenDebtItem` khi có `PurchaseReturn`/`SalesReturn`**: tài liệu đề xuất điều chỉnh trực tiếp số dư gốc thay vì tạo item độc lập, nhưng chi tiết kỹ thuật (điều chỉnh Money của `OpenDebtItem` gốc, hay tạo 1 `DebtMatch` tự động giữa item gốc và 1 "item ảo" đại diện phần trả lại) cần thiết kế thêm ở tầng Application, phối hợp với `02-ARAP-BoundedContext.md`.
3. **[PS-R09]/DiscountVoucher**: câu hỏi tương tự `DebtNetting` ở tài liệu 02 — có nên phát sinh bút toán GL khi chiết khấu hay không (hệ cũ không phát sinh). Khuyến nghị có — cần chốt với chủ sản phẩm.
4. **[PS-R10]** Cơ sở phân bổ chi phí mua hàng thực tế (theo giá trị dòng, theo số lượng, hay theo trọng lượng/thể tích) chưa xác minh đầy đủ do `AF0314Facade.cs` chưa được đọc sâu (146 dòng, mới chỉ biết nó cập nhật `ExpenseOriginalAmount`/`ExpenseConvertedAmount` trên `AT9000`) — cần đọc chi tiết file này trước khi hoàn thiện Domain Service phân bổ.
5. **Luồng hoá đơn điện tử** (phát hành/điều chỉnh/thay thế/huỷ — `mnuBusiness_PublishEInvoice` và các menu liên quan) chưa được mô hình hoá chi tiết (chỉ ghi nhận trường `EInvoiceGuid`) — nếu đây là yêu cầu bắt buộc (tuân thủ quy định hoá đơn điện tử VN), cần một pass khảo sát riêng và có thể tách thành sub-aggregate `EInvoiceLifecycle` trước khi code-gen.
6. **Kết chuyển bán hàng từ POS (AF0281)**: cơ chế ghi nhận cụ thể (tạo `SalesInvoice` mới hay cập nhật hoá đơn đã có) chưa được khảo sát sâu trong lần này — nếu POS là kênh bán hàng quan trọng, cần bổ sung 1 use case riêng trước khi code-gen.
