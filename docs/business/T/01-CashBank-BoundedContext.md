# 01 — Bounded Context: Cash & Bank (Tiền mặt & Ngân hàng)

> Tham chiếu nền tảng: `00-Ubiquitous-Language-Context-Map.md` (thuật ngữ, Shared Kernel: `Money`, `Currency`, `ExchangeRate`, `Account`, `AccountingPeriod`, `BusinessPartner`, `AnalysisTag`, `Employee`, `Branch`).
> Nguồn xác minh nghiệp vụ: `F:\40.SOURCES.GREE\01.ERP8\04.ASOFT-T\Business\ApplicationFacades\AF0061Facade.cs`, `AF0062Facade.cs`, `AF0102Facade.cs`, `AF0103Facade.cs`, `AF0104Facade.cs`, `AF0099Facade.cs`, `AF0100Facade.cs`, `AF0101.cs`, `AF0370Facade.cs`, `DataAccess\ServiceComponents\AF0038Service.cs`, `AF0068Service.cs`; shared kernel `01.ASOFT-SM\Business\ApplicationFacades\AP0077Facade.cs`, `AP1111Facade.cs`.

## A. Tổng quan nghiệp vụ

**Mục đích**: quản lý toàn bộ dòng tiền mặt và tiền gửi ngân hàng của doanh nghiệp — ghi nhận thu/chi, tạm ứng có kiểm soát qua duyệt, chuyển đổi ngoại tệ quỹ, và cập nhật tỷ giá tham chiếu.

**Actor**: Kế toán tiền mặt/ngân hàng (lập phiếu), Kế toán trưởng/Trưởng phòng (duyệt tạm ứng cấp 1), Giám đốc tài chính (duyệt tạm ứng cấp 2 — giá trị lớn).

**Use case chính** (mã AF gốc để truy vết):

| Use case | Mã gốc |
|---|---|
| UC-CB-01 Lập phiếu thu tiền mặt | AF0061 |
| UC-CB-02 Lập phiếu chi tiền mặt | AF0062 |
| UC-CB-03 Lập phiếu thu qua ngân hàng | AF0102 |
| UC-CB-04 Lập phiếu chi qua ngân hàng | AF0103 |
| UC-CB-05 Lập phiếu chuyển khoản ngân hàng | AF0104 |
| UC-CB-06 Lập phiếu đổi tiền (quỹ) | AF0068 |
| UC-CB-07 Cập nhật tỷ giá | AF0038 |
| UC-CB-08 Lập phiếu tạm thu / tạm chi | AF0099 / AF0100 |
| UC-CB-09 Duyệt tạm thu-chi cấp 1 | AF0101 |
| UC-CB-10 Duyệt tạm chi cấp 2 | AF0370 |

## B. Domain Model (DDD)

### B.1 Entity tham chiếu riêng của BC này

**`CashFund`** (Entity, reference data — quỹ tiền mặt, có thể có nhiều quỹ theo chi nhánh/loại tiền)

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| FundCode | string, PK | |
| FundName | string | |
| BranchId | FK Branch | |
| AccountCode | FK Account | Tài khoản kế toán đại diện quỹ (vd 1111) |
| CurrencyCode | FK Currency | |
| AllowNegativeBalance | bool | Nguồn: `SYS.CurrentDefaultSys.IsNegativeCash` — chuyển từ cấu hình toàn hệ thống sang cấu hình theo từng quỹ (linh hoạt hơn, mỗi quỹ có thể khác nhau) |

**`BankAccount`** (Entity, reference data)

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| BankAccountId | string, PK | |
| BankAccountNo | string | |
| BankName | string | |
| BranchId | FK Branch | |
| AccountCode | FK Account | |
| CurrencyCode | FK Currency | |

### B.2 Aggregate: `CashReceiptVoucher` (Phiếu thu tiền mặt)

**Aggregate Root**, 1 chứng từ = 1 dòng hạch toán (không có multi-line trong hệ cũ cho loại phiếu này).

| Thuộc tính | Kiểu |
|---|---|
| VoucherId | Guid, PK |
| VoucherNo | string |
| BranchId | FK Branch |
| VoucherDate | date |
| CashFundId | FK CashFund (bên Nợ) |
| CreditAccountCode | FK Account (bên Có — nguồn gốc tiền: doanh thu, phải thu KH, tạm ứng hoàn...) |
| Partner | FK BusinessPartner, nullable | Bắt buộc nếu `CreditAccountCode.RequiresPartner = true` |
| Money | Value Object `Money` |
| AnalysisTags | List\<AnalysisTag\> |
| Description | string |
| EmployeeId | FK Employee |
| CreatedAt/CreatedBy | audit |

**Invariant/Business rule**:
- **[CB-R01] Kiểm tra hạn mức tín dụng khi thu tiền gắn đối tượng công nợ.** Nếu `CreditAccountCode` là tài khoản phải thu (`RequiresPartner=true`) và giao dịch làm giảm công nợ phải thu của `Partner`, hệ thống gọi `Debt.CreditLimitChecker` (Domain Service sở hữu bởi BC `02-ARAP`, xem tài liệu đó) **trước khi cho phép hoàn tất**; nếu vượt hạn mức, chặn lưu và trả về thông điệp lỗi có chứa hạn mức cho phép hiện tại.
  Nguồn: `AF0061.cs:2457 CheckDebitLimit(...)` gọi `AP0077Facade.GetData(...)`.
- **[CB-R02] Tỷ giá & quy đổi.** Khi `Money.CurrencyCode` khác `Currency.BaseCurrency`, `ExchangeRate` được lấy tự động từ `ExchangeRate` (Shared Kernel) tại `VoucherDate`; người dùng có thể sửa tay tỷ giá cho từng phiếu (ghi đè), hệ thống tính lại `ConvertedAmount`.
  Nguồn: `AF0061.cs:2481 cboCurrencyIDChangeProcessing`.
- **[CB-R03] Không cho lập/sửa/xoá phiếu nếu `AccountingPeriod` của `VoucherDate` đã khoá** (rule chung Shared Kernel, mục 4.5 tài liệu 00).

Domain Event phát ra khi hoàn tất: `CashReceiptPosted{VoucherId, BranchId, VoucherDate, DebitAccount=CashFund.AccountCode, CreditAccount, Money, Partner?, AnalysisTags}`.

### B.3 Aggregate: `CashPaymentVoucher` (Phiếu chi tiền mặt)

Cấu trúc đối xứng với `CashReceiptVoucher` (đảo Nợ/Có: `DebitAccountCode` là tài khoản đích chi tiêu, `CashFundId` là bên Có).

**Invariant/Business rule**:
- **[CB-R04] Kiểm tra tồn quỹ đủ chi (chặn cứng).** Nếu `CashFund.AllowNegativeBalance = false`, tính `AvailableBalance = FundBalanceChecker.GetBalance(CashFundId, upToDate=VoucherDate)`; nếu `AvailableBalance < Money.Amount`, **chặn lưu** với thông điệp "Số tiền tồn quỹ không còn đủ để chi."
  Nguồn: `AF0062.cs:814`, gọi `AP1111Facade.GetValue(...)`; message `AFML000064`.
- **[CB-R05] Cảnh báo vượt ngân sách chi (soft warning, không chặn).** Nếu tổng chi luỹ kế trong kỳ theo (`DebitAccountCode` + `AnalysisTags`) vượt `ExpenseBudget` đã khai báo cho kỳ đó, hiển thị xác nhận "Chi vượt ngân sách số tiền {X}. Bạn có muốn lưu không?" — người dùng có thể chọn tiếp tục lưu.
  Nguồn: `AF0062.cs:669-900`, message `AFML000607`; bảng ngân sách nguồn `AT9090`.
  *Ghi chú thiết kế*: `ExpenseBudget` là một khái niệm chưa thuộc phạm vi 4 BC đã chọn (có thể trở thành BC "Budget" riêng trong tương lai) — ở đây định nghĩa tối thiểu 1 Domain Service `BudgetChecker.CheckSoftLimit(accountCode, analysisTags, period, amount) -> Warning?`.
- **[CB-R06] Kiểm tra trùng số hoá đơn/serial theo đối tượng** (nếu phiếu chi gắn hoá đơn đầu vào). Nguồn: message `AFML000361`/`AFML000621`.
- **[CB-R01’] Không áp dụng kiểm tra hạn mức tín dụng cho phiếu chi** (hạn mức chỉ áp dụng chiều thu — AR).
- **[CB-R03]** áp dụng như trên.

Domain Event: `CashPaymentPosted{...}`.

### B.4 Aggregate: `BankReceiptVoucher`, `BankPaymentVoucher`

Cấu trúc như `CashReceiptVoucher`/`CashPaymentVoucher`, thay `CashFundId` bằng `BankAccountId`.

**Invariant/Business rule**:
- **[CB-R07] Hiển thị hạn mức tín dụng còn lại nhưng KHÔNG chặn lưu** — khác với `CashReceiptVoucher` [CB-R01]. Đây là hành vi quan sát được trong hệ cũ (`AF0102` chỉ hiển thị `lblReCreditLimit1` bind `AP0131.ReCreditLimit`, không tìm thấy code chặn).
  ⚠️ **[Quyết định cần xác nhận với chủ sản phẩm]**: đây nhiều khả năng là thiếu sót lịch sử (inconsistency) hơn là chủ đích nghiệp vụ — vì bản chất "thu tiền qua ngân hàng" và "thu tiền mặt" đều làm giảm công nợ phải thu, không có lý do nghiệp vụ để áp dụng rule khác nhau. **Khuyến nghị**: áp dụng thống nhất [CB-R01] (chặn cứng) cho cả `BankReceiptVoucher`. Đánh dấu ở mục F để xác nhận trước khi code-gen.
- Không có rule tồn quỹ ngân hàng âm được tìm thấy trong `AF0103` (khác `CashPaymentVoucher` [CB-R04]) — tài khoản ngân hàng thường cho phép thấu chi/không kiểm soát chặt như quỹ tiền mặt. Giữ nguyên: `BankPaymentVoucher` **không có** invariant tồn quỹ tối thiểu bắt buộc (có thể cấu hình `BankAccount.AllowNegativeBalance` mặc định `true`).

Domain Event: `BankReceiptPosted{...}`, `BankPaymentPosted{...}`.

### B.5 Aggregate: `BankTransferVoucher` (Chuyển khoản ngân hàng)

| Thuộc tính | Kiểu |
|---|---|
| VoucherId, VoucherNo, BranchId, VoucherDate | như trên |
| FromBankAccountId | FK BankAccount (bên Có) |
| ToBankAccountId | FK BankAccount (bên Nợ) |
| Money | Value Object |
| Description | string |

**Invariant**: `FromBankAccountId ≠ ToBankAccountId`. Không có kiểm tra hạn mức/tồn quỹ (quan sát: đây là facade nhỏ nhất, luồng đơn giản nhất trong nhóm).

Domain Event: `BankTransferPosted{...}`.

### B.6 Aggregate: `CurrencyConversionVoucher` (Phiếu đổi tiền)

| Thuộc tính | Kiểu |
|---|---|
| VoucherId, VoucherNo, BranchId, VoucherDate | như trên |
| FromFundId | FK CashFund hoặc BankAccount, loại tiền nguồn |
| ToFundId | FK CashFund hoặc BankAccount, loại tiền đích |
| SourceMoney | Money (loại tiền nguồn) |
| CrossRate | decimal(18,6) |
| TargetMoney | Money (loại tiền đích) = SourceMoney.Amount × CrossRate |

Nguồn: `AF0068Service.cs` (cập nhật `ExchangeRateCN`, `CurrencyIDCN`, `OriginalAmountCN` — tỷ giá chéo giữa 2 loại tiền không phải VND).

Domain Event: `CurrencyConverted{...}`.

### B.7 Aggregate: `TempAdvanceVoucher` (Phiếu tạm thu / tạm chi)

Đại diện chung cho cả tạm thu và tạm chi (tiền mặt hoặc ngân hàng), có thêm state machine phê duyệt.

| Thuộc tính | Kiểu |
|---|---|
| VoucherId, VoucherNo, BranchId, VoucherDate | như trên |
| Direction | enum{Receipt, Payment} |
| FundReference | CashFundId hoặc BankAccountId |
| CounterAccountCode | FK Account |
| Partner | FK BusinessPartner, nullable |
| Money | Value Object |
| ApprovalStatus | enum{PendingLevel1, Level1Approved, PendingLevel2, Level2Approved, Rejected} |
| Level1ApprovedBy / At | Employee, datetime, nullable |
| Level2ApprovedBy / At | Employee, datetime, nullable (chỉ áp dụng khi `Direction=Payment` và `Money.ConvertedAmount ≥ Level2Threshold`) |
| RejectionReason | string, nullable |

**Invariant/Business rule**:
- **[CB-R08] Số dư quỹ được ghi nhận ngay khi tạo phiếu tạm ứng** (không chờ duyệt) — quan sát: hệ cũ ghi `Status="0"` vào bảng giao dịch ngay khi tạo, tức phiếu đã ảnh hưởng số dư quỹ tức thời; bước duyệt là kiểm soát tuân thủ (compliance sign-off), không phải điều kiện để tiền "thực sự" di chuyển.
- **[CB-R09] Tạm chi có 2 cấp duyệt; tạm thu chỉ có 1 cấp.** Cấp 1 (AF0101) áp dụng cho cả 2 chiều; cấp 2 (AF0370) chỉ quan sát thấy cho tạm chi ("Duyệt phiếu tạm chi cấp 2"). **[Gap — xem mục F]**: ngưỡng giá trị kích hoạt duyệt cấp 2 chưa xác định được từ code trong lần khảo sát này; cần xác nhận business rule cụ thể (theo số tiền? theo loại tài khoản?) trước khi code-gen phần điều kiện `PendingLevel2`.
- **[CB-R10]** áp dụng [CB-R03] (khoá kỳ), [CB-R04]/[CB-R05] khi `Direction=Payment` dùng quỹ tiền mặt.

Domain Event: `TempAdvanceRequested`, `TempAdvanceLevel1Approved`, `TempAdvanceLevel2Approved`, `TempAdvanceRejected`.

### B.8 Tích hợp với General Ledger

Mọi Domain Event ở mục B.2–B.7 (`CashReceiptPosted`, `CashPaymentPosted`, `BankReceiptPosted`, `BankPaymentPosted`, `BankTransferPosted`, `CurrencyConverted`, và các sự kiện phát sinh từ `TempAdvanceVoucher` khi đã ảnh hưởng số dư quỹ) đồng thời publish **2** sự kiện `PostingLineRecorded` chuẩn hoá (1 dòng Nợ + 1 dòng Có, xem định dạng tại `00-Ubiquitous-Language-Context-Map.md` mục 4.12 điểm 6) để `03-GeneralLedger-BoundedContext.md` tổng hợp lên Sổ cái/Bảng cân đối phát sinh — theo đúng nguyên tắc tích hợp tại Context Map (tài liệu 00, mục 3.2).

### B.9 Domain Service

| Domain Service | Vai trò | Sở hữu bởi |
|---|---|---|
| `FundBalanceChecker` | Tính số dư khả dụng của 1 `CashFund`/`BankAccount` tại 1 thời điểm (tổng hợp từ các Domain Event `CashReceiptPosted/CashPaymentPosted/...` đã publish) | Cash & Bank |
| `BudgetChecker` | Kiểm tra ngân sách chi theo tài khoản+kỳ+phân tích, trả cảnh báo (không chặn) | Cash & Bank (tạm thời — có thể tách BC Budget riêng sau) |
| `CreditLimitChecker` | Kiểm tra hạn mức tín dụng còn lại của 1 `BusinessPartner` | **Debt (AR/AP)** — Cash&Bank chỉ gọi qua interface, xem `02-ARAP-BoundedContext.md` |
| `PeriodLockGuard` | Kiểm tra kỳ kế toán chưa khoá trước khi ghi/sửa/xoá | Shared Kernel (General Ledger sở hữu dữ liệu) |

### B.10 Repository

`ICashReceiptVoucherRepository`, `ICashPaymentVoucherRepository`, `IBankReceiptVoucherRepository`, `IBankPaymentVoucherRepository`, `IBankTransferVoucherRepository`, `ICurrencyConversionVoucherRepository`, `ITempAdvanceVoucherRepository`, `ICashFundRepository`, `IBankAccountRepository` — mỗi repository thao tác đúng 1 Aggregate Root (chuẩn DDD, khác hệ cũ dùng chung 1 service cho mọi loại chứng từ).

## C. Đề xuất DB Schema mới

Quy ước: khoá chính `Id BIGINT IDENTITY` (hoặc UUID tuỳ hạ tầng), `VoucherNo` unique theo `(BranchId, VoucherTypeCode, Year, Month)`. Bảng tham chiếu Shared Kernel (`account`, `currency`, `business_partner`, `employee`, `branch`, `analysis_tag`...) định nghĩa 1 lần, không lặp lại ở đây.

```sql
CREATE TABLE cash_fund (
  id            BIGINT IDENTITY PRIMARY KEY,
  fund_code     VARCHAR(20) NOT NULL UNIQUE,
  fund_name     NVARCHAR(200) NOT NULL,
  branch_id     BIGINT NOT NULL REFERENCES branch(id),
  account_code  VARCHAR(20) NOT NULL REFERENCES account(account_code),
  currency_code CHAR(3) NOT NULL REFERENCES currency(currency_code),
  allow_negative_balance BIT NOT NULL DEFAULT 0,
  disabled      BIT NOT NULL DEFAULT 0
);

CREATE TABLE bank_account (
  id            BIGINT IDENTITY PRIMARY KEY,
  bank_account_no VARCHAR(50) NOT NULL,
  bank_name     NVARCHAR(200) NOT NULL,
  branch_id     BIGINT NOT NULL REFERENCES branch(id),
  account_code  VARCHAR(20) NOT NULL REFERENCES account(account_code),
  currency_code CHAR(3) NOT NULL REFERENCES currency(currency_code),
  disabled      BIT NOT NULL DEFAULT 0
);

CREATE TABLE cash_receipt_voucher (
  id                  BIGINT IDENTITY PRIMARY KEY,
  voucher_no          VARCHAR(30) NOT NULL,
  branch_id           BIGINT NOT NULL REFERENCES branch(id),
  voucher_date        DATE NOT NULL,
  cash_fund_id        BIGINT NOT NULL REFERENCES cash_fund(id),
  credit_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  partner_id          BIGINT NULL REFERENCES business_partner(id),
  currency_code       CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate       DECIMAL(18,6) NOT NULL,
  original_amount     DECIMAL(18,2) NOT NULL,
  converted_amount    DECIMAL(18,2) NOT NULL,
  description         NVARCHAR(500),
  employee_id         BIGINT NOT NULL REFERENCES employee(id),
  created_at          DATETIME2 NOT NULL,
  created_by          BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE cash_receipt_voucher_analysis_tag (
  cash_receipt_voucher_id BIGINT NOT NULL REFERENCES cash_receipt_voucher(id),
  dimension_type_code     VARCHAR(20) NOT NULL,
  tag_code                VARCHAR(20) NOT NULL,
  PRIMARY KEY (cash_receipt_voucher_id, dimension_type_code)
);

-- cash_payment_voucher: cấu trúc đối xứng, thay credit_account_code -> debit_account_code
CREATE TABLE cash_payment_voucher (
  id                 BIGINT IDENTITY PRIMARY KEY,
  voucher_no         VARCHAR(30) NOT NULL,
  branch_id          BIGINT NOT NULL REFERENCES branch(id),
  voucher_date       DATE NOT NULL,
  cash_fund_id       BIGINT NOT NULL REFERENCES cash_fund(id),
  debit_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  partner_id         BIGINT NULL REFERENCES business_partner(id),
  currency_code      CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate      DECIMAL(18,6) NOT NULL,
  original_amount    DECIMAL(18,2) NOT NULL,
  converted_amount   DECIMAL(18,2) NOT NULL,
  description        NVARCHAR(500),
  employee_id        BIGINT NOT NULL REFERENCES employee(id),
  created_at         DATETIME2 NOT NULL,
  created_by         BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

-- bank_receipt_voucher / bank_payment_voucher: như trên, thay cash_fund_id -> bank_account_id
CREATE TABLE bank_receipt_voucher (
  id                  BIGINT IDENTITY PRIMARY KEY,
  voucher_no          VARCHAR(30) NOT NULL,
  branch_id           BIGINT NOT NULL REFERENCES branch(id),
  voucher_date        DATE NOT NULL,
  bank_account_id     BIGINT NOT NULL REFERENCES bank_account(id),
  credit_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  partner_id          BIGINT NULL REFERENCES business_partner(id),
  currency_code       CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate       DECIMAL(18,6) NOT NULL,
  original_amount     DECIMAL(18,2) NOT NULL,
  converted_amount    DECIMAL(18,2) NOT NULL,
  description         NVARCHAR(500),
  employee_id         BIGINT NOT NULL REFERENCES employee(id),
  created_at          DATETIME2 NOT NULL,
  created_by          BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE bank_payment_voucher (
  id                 BIGINT IDENTITY PRIMARY KEY,
  voucher_no         VARCHAR(30) NOT NULL,
  branch_id          BIGINT NOT NULL REFERENCES branch(id),
  voucher_date       DATE NOT NULL,
  bank_account_id    BIGINT NOT NULL REFERENCES bank_account(id),
  debit_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  partner_id         BIGINT NULL REFERENCES business_partner(id),
  currency_code      CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate      DECIMAL(18,6) NOT NULL,
  original_amount    DECIMAL(18,2) NOT NULL,
  converted_amount   DECIMAL(18,2) NOT NULL,
  description        NVARCHAR(500),
  employee_id        BIGINT NOT NULL REFERENCES employee(id),
  created_at         DATETIME2 NOT NULL,
  created_by         BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE bank_transfer_voucher (
  id                   BIGINT IDENTITY PRIMARY KEY,
  voucher_no           VARCHAR(30) NOT NULL,
  branch_id            BIGINT NOT NULL REFERENCES branch(id),
  voucher_date         DATE NOT NULL,
  from_bank_account_id BIGINT NOT NULL REFERENCES bank_account(id),
  to_bank_account_id   BIGINT NOT NULL REFERENCES bank_account(id),
  currency_code        CHAR(3) NOT NULL REFERENCES currency(currency_code),
  original_amount      DECIMAL(18,2) NOT NULL,
  description           NVARCHAR(500),
  created_at            DATETIME2 NOT NULL,
  created_by            BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no),
  CHECK (from_bank_account_id <> to_bank_account_id)
);

CREATE TABLE currency_conversion_voucher (
  id                BIGINT IDENTITY PRIMARY KEY,
  voucher_no        VARCHAR(30) NOT NULL,
  branch_id         BIGINT NOT NULL REFERENCES branch(id),
  voucher_date      DATE NOT NULL,
  from_fund_ref     VARCHAR(50) NOT NULL, -- 'CASH:<id>' hoặc 'BANK:<id>'
  to_fund_ref       VARCHAR(50) NOT NULL,
  from_currency_code CHAR(3) NOT NULL REFERENCES currency(currency_code),
  to_currency_code   CHAR(3) NOT NULL REFERENCES currency(currency_code),
  source_amount     DECIMAL(18,2) NOT NULL,
  cross_rate        DECIMAL(18,6) NOT NULL,
  target_amount     DECIMAL(18,2) NOT NULL,
  created_at        DATETIME2 NOT NULL,
  created_by        BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE temp_advance_voucher (
  id                   BIGINT IDENTITY PRIMARY KEY,
  voucher_no           VARCHAR(30) NOT NULL,
  branch_id            BIGINT NOT NULL REFERENCES branch(id),
  voucher_date         DATE NOT NULL,
  direction             VARCHAR(10) NOT NULL CHECK (direction IN ('Receipt','Payment')),
  cash_fund_id          BIGINT NULL REFERENCES cash_fund(id),
  bank_account_id       BIGINT NULL REFERENCES bank_account(id),
  counter_account_code  VARCHAR(20) NOT NULL REFERENCES account(account_code),
  partner_id            BIGINT NULL REFERENCES business_partner(id),
  currency_code         CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate         DECIMAL(18,6) NOT NULL,
  original_amount       DECIMAL(18,2) NOT NULL,
  converted_amount      DECIMAL(18,2) NOT NULL,
  approval_status        VARCHAR(20) NOT NULL DEFAULT 'PendingLevel1'
                          CHECK (approval_status IN ('PendingLevel1','Level1Approved','PendingLevel2','Level2Approved','Rejected')),
  level1_approved_by     BIGINT NULL REFERENCES employee(id),
  level1_approved_at     DATETIME2 NULL,
  level2_approved_by     BIGINT NULL REFERENCES employee(id),
  level2_approved_at     DATETIME2 NULL,
  rejection_reason       NVARCHAR(500) NULL,
  created_at             DATETIME2 NOT NULL,
  created_by             BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no),
  CHECK ((cash_fund_id IS NOT NULL) <> (bank_account_id IS NOT NULL))
);
```

**Mapping ngược hệ cũ**: `cash_fund`/`bank_account` ↔ (suy ra từ `AT1005` account + cấu hình quỹ, hệ cũ không có bảng quỹ độc lập); `cash_receipt_voucher`/`cash_payment_voucher`/`bank_*` ↔ dòng `AT9000` lọc theo `TransactionTypeID` tương ứng từng form; `temp_advance_voucher` ↔ `AT9000` (Status/IsAudit) + hành vi duyệt của AF0101/AF0370.

## D. BDD Scenarios (Gherkin)

```gherkin
Feature: Lập phiếu thu tiền mặt
  # UC-CB-01

  Scenario: Lập phiếu thu thành công trong hạn mức tín dụng
    # [CB-R01]
    Given đối tượng "KH001" có hạn mức phải thu cho phép là 500,000,000 VND
    And công nợ phải thu hiện tại của "KH001" là 300,000,000 VND
    When kế toán lập phiếu thu 100,000,000 VND giảm trừ công nợ của "KH001"
    Then phiếu thu được lưu thành công
    And sự kiện "CashReceiptPosted" được phát ra

  Scenario: Chặn lập phiếu thu vượt hạn mức tín dụng
    # [CB-R01]
    Given đối tượng "KH002" có hạn mức phải thu cho phép là 200,000,000 VND
    And công nợ phải thu hiện tại của "KH002" là 190,000,000 VND
    When kế toán lập phiếu thu bù trừ công nợ 50,000,000 VND cho "KH002"
    Then hệ thống từ chối lưu phiếu
    And thông báo hiển thị hạn mức tín dụng còn lại cho phép

  Scenario: Chặn lập/sửa phiếu thu khi kỳ kế toán đã khoá
    # [CB-R03]
    Given kỳ kế toán tháng 05/2026 của chi nhánh "HN" đã bị khoá
    When kế toán lập phiếu thu có ngày chứng từ 15/05/2026
    Then hệ thống từ chối lưu phiếu với lý do "kỳ kế toán đã khoá"

Feature: Lập phiếu chi tiền mặt
  # UC-CB-02

  Scenario: Chặn chi khi không đủ tồn quỹ và quỹ không cho phép âm
    # [CB-R04]
    Given quỹ tiền mặt "QUY-HN" không cho phép âm quỹ
    And số dư khả dụng hiện tại của "QUY-HN" là 10,000,000 VND
    When kế toán lập phiếu chi 15,000,000 VND từ "QUY-HN"
    Then hệ thống từ chối lưu phiếu với thông báo "Số tiền tồn quỹ không còn đủ để chi."

  Scenario: Cho phép chi âm quỹ khi quỹ được cấu hình cho phép
    # [CB-R04]
    Given quỹ tiền mặt "QUY-HCM" cho phép âm quỹ
    And số dư khả dụng hiện tại của "QUY-HCM" là 5,000,000 VND
    When kế toán lập phiếu chi 8,000,000 VND từ "QUY-HCM"
    Then phiếu chi được lưu thành công

  Scenario: Cảnh báo vượt ngân sách nhưng vẫn cho phép lưu nếu người dùng xác nhận
    # [CB-R05]
    Given ngân sách chi cho tài khoản "6421" kỳ 06/2026 là 50,000,000 VND
    And tổng chi luỹ kế trong kỳ cho tài khoản "6421" hiện là 45,000,000 VND
    When kế toán lập phiếu chi 10,000,000 VND vào tài khoản "6421" trong kỳ 06/2026
    Then hệ thống hiển thị cảnh báo vượt ngân sách 5,000,000 VND kèm xác nhận lưu tiếp
    And nếu kế toán xác nhận "Có" thì phiếu chi được lưu thành công

Feature: Duyệt phiếu tạm ứng
  # UC-CB-08, UC-CB-09, UC-CB-10

  Scenario: Tạm chi giá trị lớn cần đủ 2 cấp duyệt trước khi coi là hoàn tất
    # [CB-R09]
    Given phiếu tạm chi "TU-001" đã được tạo và đang ở trạng thái "PendingLevel1"
    When cấp 1 duyệt phiếu "TU-001"
    Then trạng thái phiếu chuyển thành "PendingLevel2"
    When cấp 2 duyệt phiếu "TU-001"
    Then trạng thái phiếu chuyển thành "Level2Approved"

  Scenario: Tạm thu chỉ cần 1 cấp duyệt
    # [CB-R09]
    Given phiếu tạm thu "TU-002" đã được tạo và đang ở trạng thái "PendingLevel1"
    When cấp 1 duyệt phiếu "TU-002"
    Then trạng thái phiếu chuyển thành "Level1Approved"
```

## E. TDD Test Plan

| Layer | Test case | Map Gherkin |
|---|---|---|
| Domain unit | `Should_ThrowDomainException_When_ReceiptExceedsCreditLimit` | "Chặn lập phiếu thu vượt hạn mức tín dụng" |
| Domain unit | `Should_RecalculateConvertedAmount_When_ExchangeRateOverridden` | [CB-R02] |
| Domain unit | `Should_RejectVoucher_When_PeriodIsClosed` | "Chặn lập/sửa phiếu thu khi kỳ kế toán đã khoá" |
| Domain unit | `Should_ThrowDomainException_When_PaymentExceedsFundBalance_AndNegativeNotAllowed` | "Chặn chi khi không đủ tồn quỹ..." |
| Domain unit | `Should_AllowNegativeBalance_When_FundConfiguredToAllow` | "Cho phép chi âm quỹ..." |
| Domain unit | `Should_TransitionToPendingLevel2_When_Level1ApprovesPayment` | "Tạm chi giá trị lớn cần đủ 2 cấp duyệt" |
| Domain unit | `Should_TransitionToLevel1Approved_When_Level1ApprovesReceipt` | "Tạm thu chỉ cần 1 cấp duyệt" |
| Application/use-case | `Should_CallCreditLimitChecker_When_PostingCashReceiptAgainstReceivableAccount` | [CB-R01] — verify gọi ACL sang Debt BC |
| Application/use-case | `Should_RaiseSoftWarning_Not_Block_When_BudgetExceeded` | "Cảnh báo vượt ngân sách..." |
| Application/use-case | `Should_PublishCashPaymentPostedEvent_When_VoucherSavedSuccessfully` | (mọi scenario "lưu thành công") |
| Integration | `Should_PersistVoucher_And_EnforceUniqueVoucherNoPerBranchAndPeriod` | ràng buộc DB `UNIQUE(branch_id, voucher_no)` |
| Integration | `Should_RejectTransfer_When_FromAndToBankAccountAreSame` | CHECK constraint `bank_transfer_voucher` |
| Acceptance | Toàn bộ scenario mục D chạy qua API/Application layer end-to-end với DB test container | 1-1 với mục D |

## F. Open Questions / Gaps

1. **[CB-R07]** Bất nhất giữa `CashReceiptVoucher` (chặn cứng hạn mức tín dụng) và `BankReceiptVoucher` (chỉ hiển thị, không chặn) trong hệ cũ — cần chủ sản phẩm xác nhận có nên thống nhất hành vi trong hệ mới hay không (khuyến nghị: thống nhất, chặn cả 2).
2. **[CB-R09]** Ngưỡng giá trị hoặc điều kiện kích hoạt duyệt cấp 2 (AF0370) cho tạm chi chưa xác định được từ code — cần khảo sát thêm `AF0370Facade.cs`/cấu hình hệ thống hoặc hỏi nghiệp vụ trực tiếp trước khi chốt rule `PendingLevel2`.
3. Cơ chế mutation trạng thái duyệt thực tế (AF0101 chuyển `IsAudit`/`Status` như thế nào) không thấy rõ trong Facade — nghi ngờ nằm ở base class UI dùng chung toàn hệ thống (chưa khảo sát). Cần xác nhận state machine B.7 khớp thực tế trước khi code-gen tầng Application.
4. Side-effect stored procedure `AP3102` gọi khi mở phiếu tạm chi (`AF0100`) chưa rõ mục đích — cần xác minh có phải là một phần luồng nghiệp vụ cần giữ lại hay chỉ là tính năng phụ trợ UI.
