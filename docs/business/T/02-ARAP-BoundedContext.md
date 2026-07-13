# 02 — Bounded Context: Debt (AR/AP) — Công nợ phải thu / phải trả

> Tham chiếu nền tảng: `00-Ubiquitous-Language-Context-Map.md`. BC này cung cấp `CreditLimitChecker` cho `01-CashBank` và `04-PurchaseSales`; nhận Domain Event từ 2 BC đó để dựng `OpenDebitItem`/`OpenCreditItem` (xem Context Map mục 3.2 tài liệu 00).
> Nguồn xác minh nghiệp vụ: `04.ASOFT-T\DataAccess\ServiceComponents\AF0011Service.cs` (engine khớp AR), `AF0025Service.cs` (khớp AP), `AF0600Service.cs` (bù trừ), `AF0109Service.cs` (lãi phạt); `01.ASOFT-SM\Business\ApplicationFacades\AP0311Facade.cs` (tự động khớp), `AP0305Facade.cs` (chuyển đổi nguyên tệ công nợ); `04.ASOFT-T\Business\ApplicationFacades\AP0077Facade.cs` (hạn mức tín dụng); dataset `01.ASOFT-SM\Crosscutting\DataComponents\AT0301/0302/0303Dataset.cs`, `AT1018/1019Dataset.cs`, `AT3131Dataset.cs`, `AT1202DataSet.cs`.

## A. Tổng quan nghiệp vụ

**Mục đích**: theo dõi và giải trừ công nợ phải thu/phải trả phát sinh từ các nghiệp vụ khác (bán hàng, mua hàng, thu, chi), theo mô hình **khớp chứng từ mở (open-item matching)** — không phải mô hình số dư luỹ kế (running balance). Ngoài ra quản lý hạn mức tín dụng và tính lãi phạt/thưởng thanh toán sớm-trễ hạn.

**Actor**: Kế toán công nợ, Kế toán trưởng (duyệt bù trừ/khớp giá trị lớn).

**Use case chính**:

| Use case | Mã gốc |
|---|---|
| UC-DEBT-01 Khớp công nợ phải thu (thủ công) | AF0009 + AF0011 (engine) |
| UC-DEBT-02 Khớp công nợ phải trả (thủ công) | AF0023 + AF0025 (engine) |
| UC-DEBT-03 Giải trừ tự động công nợ phải thu/phải trả | AF0012 / AF0026 (→ AP0311) |
| UC-DEBT-04 Huỷ khớp công nợ | AF0094 / AF0276 |
| UC-DEBT-05 Bù trừ công nợ (đối tượng vừa là KH vừa là NCC) | AF0600 |
| UC-DEBT-06 Chuyển đổi nguyên tệ công nợ (đánh giá lại theo tỷ giá mới) | AF0129 / AF0118 (→ AP0305) |
| UC-DEBT-07 Tính lãi phạt và tiền thưởng sớm hạn | AF0109 |
| UC-DEBT-08 Khai báo chính sách lãi suất | AF0035 |
| UC-DEBT-09 Kiểm tra hạn mức tín dụng (Domain Service dùng bởi BC khác) | AP0077 |

## B. Domain Model (DDD)

### B.1 Aggregate: `OpenDebtItem` (thay thế chung cho cả "OpenDebitItem"/"OpenCreditItem" trong ngôn ngữ chung — dùng 1 Aggregate với thuộc tính `Side`)

Đại diện một dòng công nợ phát sinh từ BC khác (hoá đơn bán hàng → Side=Debit/phải thu; hoá đơn mua hàng → Side=Credit/phải trả; phiếu thu/chi cũng có thể tạo item ở chiều ngược lại khi thanh toán trước/tạm ứng). Được **tạo tự động khi Debt BC nhận Domain Event** từ `04-PurchaseSales` (`SalesInvoicePosted`, `PurchaseInvoicePosted`) hoặc `01-CashBank` (`CashReceiptPosted`, `CashPaymentPosted`, `BankReceiptPosted`, `BankPaymentPosted` khi gắn `Partner`) — Debt BC không tự "lập chứng từ" loại này, chỉ phản chiếu (local projection) để phục vụ khớp.

| Thuộc tính | Kiểu |
|---|---|
| ItemId | Guid, PK |
| Side | enum{Debit, Credit} — Debit = tăng phải thu/giảm phải trả; Credit = giảm phải thu/tăng phải trả |
| PartnerId | FK BusinessPartner |
| AccountCode | FK Account (tài khoản công nợ, vd 131/331) |
| SourceBoundedContext | string (vd "PurchaseSales", "CashBank") |
| SourceVoucherType / SourceVoucherId | tham chiếu ngược chứng từ gốc |
| Money | Value Object (nguyên tệ gốc) |
| VoucherDate | date |
| DueDate | date, nullable |
| RemainingAmount | Money — **derived/cached**, nguồn sự thật là tổng `DebtMatchLine.MatchedAmount` đã áp dụng cho item này |

**Invariant**:
- **[DEBT-R01] Số dư còn lại luôn = Gốc − Tổng đã khớp, tính từ `DebtMatchLine`, không cho set tay.** Nguồn: cơ chế `AT0303` + subquery `RemainOriginal = OriginalAmountCN - SUM(matched)` trong `AF0011Service.cs`.
- Item chỉ đủ điều kiện khớp khi `RemainingAmount.Amount > 0`.

Domain Event: `DebtItemOpened{ItemId, Side, PartnerId, Money, ...}` (nội bộ BC, phục vụ audit).

### B.2 Aggregate: `DebtMatch` (Khớp công nợ — thủ công hoặc tự động)

| Thuộc tính | Kiểu |
|---|---|
| MatchId | Guid, PK (tương đương `GiveUpID`) |
| MatchDate | date |
| EmployeeId | FK Employee |
| Lines | List\<DebtMatchLine\> |

`DebtMatchLine` (Entity con trong Aggregate):

| Thuộc tính | Kiểu |
|---|---|
| DebitItemId | FK OpenDebtItem (Side=Debit) |
| CreditItemId | FK OpenDebtItem (Side=Credit) |
| MatchedMoney | Value Object |
| IsFxRevaluationDiff | bool — đánh dấu phần chênh lệch tỷ giá phát sinh khi khớp 2 nguyên tệ khác thời điểm |

**Invariant/Business rule**:
- **[DEBT-R02] Một cặp khớp chỉ hợp lệ khi 2 item cùng `PartnerId`, cùng `AccountCode`, cùng `Currency`.** Nguồn: `AV0301`/`AV0302` cùng khoá `ObjectID/AccountID/CurrencyID`.
- **[DEBT-R03] Tổng `MatchedMoney` trên các line không được vượt `RemainingAmount` của item liên quan tại thời điểm khớp** (chặn khớp âm).
- **[DEBT-R04] Giải trừ tự động (AutoDebtClearing) dùng thuật toán FIFO** theo 1 trong 3 chế độ chọn bởi tham số `Formula`: `0` = ưu tiên chứng từ có `VoucherDate` cũ nhất trước; `1` = ưu tiên `DueDate` gần nhất trước; `2` = công thức tuỳ biến (quan sát: có biến thể theo khách hàng cụ thể trong hệ cũ — coi là extension point, không phải rule chuẩn). Phạm vi áp dụng giới hạn theo khoảng đối tượng, khoảng ngày, và tối đa 3 chiều phân tích. Nguồn: `AP0311Facade.Execute(...)`.
- **[DEBT-R05] Tỷ giá dùng khi khớp 2 dòng khác kỳ/năm tài chính lấy tỷ giá đánh giá lại gần nhất** (dòng loại "PeriodEndFxRevaluation" mới nhất cho cùng đối tượng/loại tiền/tài khoản) — không dùng tỷ giá gốc của item cũ. Nguồn: subquery `ExchangeRateNew` lặp lại trong `AF0011Service.cs`.
- **[DEBT-R06] Huỷ khớp (`UnmatchDebt`) xoá hoàn toàn 1 `DebtMatch`, khôi phục `RemainingAmount` của mọi item liên quan.** Hỗ trợ huỷ từng phần (huỷ 1 `DebtMatchLine`) hay bắt buộc huỷ nguyên `DebtMatch`: **[Gap — xem mục F]**, hệ cũ có cả `DeleteGiveUpID` (huỷ nguyên) và `DoAT0303Update` (sửa) — cần xác nhận UX mong muốn ở hệ mới.

Domain Event: `DebtCleared{MatchId, Lines[]}`, `DebtMatchUndone{MatchId}`.

### B.3 Aggregate: `DebtNetting` (Bù trừ công nợ)

Đại diện nghiệp vụ cấn trừ công nợ phải thu và phải trả của **cùng một đối tượng** khi đối tượng đó vừa là khách hàng vừa là nhà cung cấp.

| Thuộc tính | Kiểu |
|---|---|
| NettingId | Guid, PK |
| VoucherNo | string |
| BranchId | FK Branch |
| VoucherDate | date |
| PartnerId | FK BusinessPartner |
| Lines | List\<DebtNettingLine\> |
| Description | string |

`DebtNettingLine`: `SourceItemId` (FK `OpenDebtItem`), `OffsetMoney`.

**Invariant**:
- **[DEBT-R07] Chỉ áp dụng khi `Partner.IsCustomer = true AND Partner.IsSupplier = true`.** Nguồn: bản chất nghiệp vụ AF0600 — cấn trừ AR và AP cùng đối tượng.
- **[DEBT-R08] Tổng `OffsetMoney` các line phía phải thu phải bằng tổng phía phải trả** (nguyên tắc cấn trừ cân đối).
- Khi hoàn tất, `RemainingAmount` của các `OpenDebtItem` liên quan giảm tương ứng (giống hiệu ứng của `DebtMatch` nhưng ghi nhận là một loại nghiệp vụ riêng để phân biệt lý do giải trừ trên báo cáo).

⚠️ **[Gap — xem mục F]**: hệ cũ không thấy bút toán Nợ/Có GL mới phát sinh khi bù trừ (chỉ đổi trạng thái/khớp) — cần xác nhận nghiệp vụ có yêu cầu phát sinh bút toán chuyển khoản nợ giữa 2 tài khoản (131↔331) trong hệ mới hay không. Khuyến nghị mặc định: `DebtNetting` publish thêm 1 Domain Event `PostingLineRecorded` (Nợ 331/Có 131 theo `OffsetMoney`) để General Ledger phản ánh đúng bản chất kế toán kép — đây là cải tiến so với hệ cũ, cần chốt với chủ sản phẩm.

Domain Event: `DebtNetted{NettingId, PartnerId, Lines[]}`.

### B.4 Aggregate: `InterestPolicy` (Chính sách lãi phạt/thưởng)

| Thuộc tính | Kiểu |
|---|---|
| PolicyId | string, PK |
| Description | string |
| AppliesTo | enum{Receivable, Payable} |
| IsFlatRate | bool |
| FlatPercent | decimal, nullable (khi `IsFlatRate=true`) |
| BonusPercent | decimal | Thưởng thanh toán sớm hạn |
| GraceDays | int | Số ngày ân hạn trước khi tính phạt |
| ApplicableVoucherTypes | List\<VoucherTypeCode\> |
| Brackets | List\<InterestBracket\> (khi `IsFlatRate=false`) |

`InterestBracket`: `FromDays`, `ToDays`, `PercentValue`.

Nguồn: `AT1018` (`IsConstant`↔`IsFlatRate` đảo nghĩa, `PercentValues`↔`FlatPercent`, `LaterDays`↔`GraceDays`), `AT1019` (bracket).

### B.5 Aggregate: `InterestCalculationBatch`

| Thuộc tính | Kiểu |
|---|---|
| BatchId | Guid, PK (tương đương `CalID`) |
| CalcMonth / CalcYear | int |
| Lines | List\<InterestCalculationLine\> |

`InterestCalculationLine`: `DebtItemId`, `RemainingAmountAtCalc`, `OverdueDays`, `InterestAmount`, `BonusAmount`.

**Invariant/Business rule**:
- **[DEBT-R09] Công thức tính** (suy luận từ dữ liệu, **cần xác nhận nghiệp vụ — xem mục F**): với mỗi `OpenDebtItem` quá hạn (`Today − DueDate > GraceDays`), `InterestAmount = RemainingAmount × rate(OverdueDays) × OverdueDays/360`, `rate` tra theo `InterestPolicy` (flat hoặc theo `InterestBracket` ứng với `OverdueDays`). `BonusAmount` áp dụng khi thanh toán trước `DueDate`, tính trên `BonusPercent`.
- Một batch tính có thể tính lại toàn bộ (xoá batch cũ theo `BatchId`, tạo batch mới) — không cộng dồn kết quả cũ.

Domain Event: `InterestCalculated{BatchId, Lines[]}`.

### B.6 Tích hợp với General Ledger

Khác với `01-CashBank` và `04-PurchaseSales`, các Aggregate của BC này (`OpenDebtItem`, `DebtMatch`) **không** phát sinh bút toán GL mới — `DebtMatch` chỉ liên kết lại các `PostingLineRecorded` đã được BC nguồn (Cash&Bank/Purchase&Sales) publish từ trước, không tạo dòng Nợ/Có mới. Ngoại lệ: `DebtNetting` **có khuyến nghị** publish thêm `PostingLineRecorded` (xem [DEBT-R07] và mục F) vì bản chất là chuyển số dư giữa 2 tài khoản (131↔331) — đây là điểm cần chốt trước khi code-gen.

### B.7 Domain Service: `CreditLimitChecker` (Open Host Service — expose cho BC khác)

```
CreditLimitChecker.CheckReceivable(partnerId, additionalAmount) -> { Allowed: bool, AvailableLimit: Money, Message?: string }
```

**Invariant**:
- **[DEBT-R10]** `TotalOutstandingReceivable(partnerId) = Σ RemainingAmount` của mọi `OpenDebtItem{Side=Debit, AccountCode ∈ nhóm phải thu}` thuộc `partnerId`.
- Chặn nếu `TotalOutstandingReceivable + additionalAmount > Partner.ReceivableCreditLimit` (khi `ReceivableCreditLimit` có giá trị; **giả định**: `null`/`0` = không giới hạn — **cần xác nhận, xem mục F**).
- Nguồn: `AP0077_STORE`, được gọi từ nhiều BC (Cash&Bank khi thu tiền, Purchase&Sales khi lập hoá đơn bán chịu).

Tương tự `CheckPayable` cho phía phải trả (dùng `PayableCreditLimit`) — quan sát trong hệ cũ ít được enforce (chỉ có `PaCreditLimit` trên `AT1202` nhưng không thấy lời gọi chặn tương đương `AP0077` phía AP) — modeled for symmetry, đánh dấu **[Gap]**.

## C. Đề xuất DB Schema mới

```sql
CREATE TABLE open_debt_item (
  id                    BIGINT IDENTITY PRIMARY KEY,
  side                  VARCHAR(10) NOT NULL CHECK (side IN ('Debit','Credit')),
  partner_id            BIGINT NOT NULL REFERENCES business_partner(id),
  account_code          VARCHAR(20) NOT NULL REFERENCES account(account_code),
  source_bounded_context VARCHAR(50) NOT NULL,
  source_voucher_type   VARCHAR(20) NOT NULL,
  source_voucher_id     VARCHAR(50) NOT NULL,
  currency_code         CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate         DECIMAL(18,6) NOT NULL,
  original_amount       DECIMAL(18,2) NOT NULL,
  converted_amount      DECIMAL(18,2) NOT NULL,
  voucher_date          DATE NOT NULL,
  due_date              DATE NULL,
  remaining_amount      DECIMAL(18,2) NOT NULL, -- cache, luôn recompute từ debt_match_line khi ghi
  created_at             DATETIME2 NOT NULL,
  UNIQUE (source_bounded_context, source_voucher_type, source_voucher_id)
);
CREATE INDEX ix_open_debt_item_partner ON open_debt_item(partner_id, side, account_code, currency_code);

CREATE TABLE debt_match (
  id            BIGINT IDENTITY PRIMARY KEY,
  match_no      VARCHAR(30) NOT NULL UNIQUE,
  match_date    DATE NOT NULL,
  employee_id   BIGINT NOT NULL REFERENCES employee(id),
  created_at    DATETIME2 NOT NULL
);

CREATE TABLE debt_match_line (
  id                    BIGINT IDENTITY PRIMARY KEY,
  debt_match_id         BIGINT NOT NULL REFERENCES debt_match(id),
  debit_item_id         BIGINT NOT NULL REFERENCES open_debt_item(id),
  credit_item_id        BIGINT NOT NULL REFERENCES open_debt_item(id),
  matched_original_amount   DECIMAL(18,2) NOT NULL,
  matched_converted_amount  DECIMAL(18,2) NOT NULL,
  is_fx_revaluation_diff    BIT NOT NULL DEFAULT 0
);

CREATE TABLE debt_netting (
  id            BIGINT IDENTITY PRIMARY KEY,
  voucher_no    VARCHAR(30) NOT NULL,
  branch_id     BIGINT NOT NULL REFERENCES branch(id),
  voucher_date  DATE NOT NULL,
  partner_id    BIGINT NOT NULL REFERENCES business_partner(id),
  description   NVARCHAR(500),
  created_at    DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE debt_netting_line (
  id                BIGINT IDENTITY PRIMARY KEY,
  debt_netting_id   BIGINT NOT NULL REFERENCES debt_netting(id),
  source_item_id    BIGINT NOT NULL REFERENCES open_debt_item(id),
  offset_original_amount  DECIMAL(18,2) NOT NULL,
  offset_converted_amount DECIMAL(18,2) NOT NULL
);

CREATE TABLE interest_policy (
  id                BIGINT IDENTITY PRIMARY KEY,
  policy_code       VARCHAR(20) NOT NULL UNIQUE,
  description       NVARCHAR(200),
  applies_to        VARCHAR(10) NOT NULL CHECK (applies_to IN ('Receivable','Payable')),
  is_flat_rate      BIT NOT NULL,
  flat_percent      DECIMAL(9,4) NULL,
  bonus_percent     DECIMAL(9,4) NOT NULL DEFAULT 0,
  grace_days        INT NOT NULL DEFAULT 0
);

CREATE TABLE interest_policy_voucher_type (
  interest_policy_id BIGINT NOT NULL REFERENCES interest_policy(id),
  voucher_type_code  VARCHAR(20) NOT NULL,
  PRIMARY KEY (interest_policy_id, voucher_type_code)
);

CREATE TABLE interest_bracket (
  id                 BIGINT IDENTITY PRIMARY KEY,
  interest_policy_id BIGINT NOT NULL REFERENCES interest_policy(id),
  line_no            INT NOT NULL,
  from_days          INT NOT NULL,
  to_days            INT NULL,
  percent_value      DECIMAL(9,4) NOT NULL
);

CREATE TABLE interest_calculation_batch (
  id           BIGINT IDENTITY PRIMARY KEY,
  batch_no     VARCHAR(30) NOT NULL UNIQUE,
  calc_month   INT NOT NULL,
  calc_year    INT NOT NULL,
  created_at   DATETIME2 NOT NULL,
  created_by   BIGINT NOT NULL
);

CREATE TABLE interest_calculation_line (
  id                     BIGINT IDENTITY PRIMARY KEY,
  batch_id               BIGINT NOT NULL REFERENCES interest_calculation_batch(id),
  debt_item_id           BIGINT NOT NULL REFERENCES open_debt_item(id),
  remaining_amount_at_calc DECIMAL(18,2) NOT NULL,
  overdue_days           INT NOT NULL,
  interest_amount        DECIMAL(18,2) NOT NULL DEFAULT 0,
  bonus_amount           DECIMAL(18,2) NOT NULL DEFAULT 0
);
```

**Mapping ngược hệ cũ**: `open_debt_item` ↔ view `AV0301`/`AV0302` (snapshot từ `AT9000`); `debt_match`/`debt_match_line` ↔ `AT0303` (nhóm theo `GiveUpID`); `debt_netting*` ↔ `AT0600`/`AT0601`; `interest_policy`/`interest_bracket` ↔ `AT1018`/`AT1019`; `interest_calculation_*` ↔ `AT3131`.

## D. BDD Scenarios (Gherkin)

```gherkin
Feature: Khớp công nợ thủ công
  # UC-DEBT-01, UC-DEBT-02

  Scenario: Khớp một hoá đơn phải thu với một phiếu thu cùng đối tượng và loại tiền
    # [DEBT-R01] [DEBT-R02]
    Given hoá đơn bán hàng "HD001" của đối tượng "KH001" còn phải thu 10,000,000 VND
    And phiếu thu "PT001" của đối tượng "KH001" có 10,000,000 VND chưa khớp
    When kế toán khớp "HD001" với "PT001" toàn bộ số tiền
    Then số dư còn lại của "HD001" là 0
    And số dư còn lại của "PT001" là 0
    And sự kiện "DebtCleared" được phát ra

  Scenario: Không cho khớp vượt quá số dư còn lại của một item
    # [DEBT-R03]
    Given hoá đơn bán hàng "HD002" còn phải thu 5,000,000 VND
    And phiếu thu "PT002" có 8,000,000 VND chưa khớp
    When kế toán khớp "HD002" với "PT002" với số tiền 6,000,000 VND
    Then hệ thống từ chối yêu cầu khớp vì vượt số dư còn lại của "HD002"

  Scenario: Huỷ khớp khôi phục lại số dư còn lại của các item liên quan
    # [DEBT-R06]
    Given "HD001" đã được khớp toàn bộ với "PT001" (số dư còn lại = 0 cho cả hai)
    When kế toán huỷ khớp giữa "HD001" và "PT001"
    Then số dư còn lại của "HD001" trở lại 10,000,000 VND
    And số dư còn lại của "PT001" trở lại 10,000,000 VND

Feature: Giải trừ tự động công nợ
  # UC-DEBT-03

  Scenario: Giải trừ tự động ưu tiên hoá đơn có ngày đến hạn sớm nhất trước
    # [DEBT-R04]
    Given đối tượng "KH003" có 2 hoá đơn phải thu mở: "HD010" đến hạn 01/06/2026 còn 3,000,000 VND, "HD011" đến hạn 15/06/2026 còn 4,000,000 VND
    And đối tượng "KH003" có 1 phiếu thu mở 3,000,000 VND
    When hệ thống chạy giải trừ tự động cho "KH003" với chế độ "theo ngày đến hạn"
    Then "HD010" được khớp hết trước
    And "HD011" vẫn còn mở 4,000,000 VND

Feature: Bù trừ công nợ
  # UC-DEBT-05

  Scenario: Chặn bù trừ khi đối tượng không đồng thời là khách hàng và nhà cung cấp
    # [DEBT-R07]
    Given đối tượng "DT001" chỉ có vai trò khách hàng (IsCustomer=true, IsSupplier=false)
    When kế toán lập phiếu bù trừ công nợ cho "DT001"
    Then hệ thống từ chối lập phiếu bù trừ

  Scenario: Bù trừ công nợ thành công cho đối tượng có cả 2 vai trò
    # [DEBT-R07] [DEBT-R08]
    Given đối tượng "DT002" vừa là khách hàng vừa là nhà cung cấp (IsCustomer=true, IsSupplier=true)
    And "DT002" có khoản phải thu mở 5,000,000 VND và khoản phải trả mở 5,000,000 VND
    When kế toán lập phiếu bù trừ công nợ 5,000,000 VND cho "DT002"
    Then khoản phải thu và khoản phải trả của "DT002" đều còn 0
    And sự kiện "DebtNetted" được phát ra

Feature: Kiểm tra hạn mức tín dụng
  # UC-DEBT-09 — dùng bởi Cash&Bank và Purchase&Sales

  Scenario: Từ chối khi vượt hạn mức tín dụng phải thu
    # [DEBT-R10]
    Given đối tượng "KH004" có hạn mức phải thu 100,000,000 VND
    And tổng công nợ phải thu hiện tại của "KH004" là 95,000,000 VND
    When một BC khác gọi CreditLimitChecker.CheckReceivable("KH004", 10,000,000)
    Then kết quả trả về Allowed = false
    And AvailableLimit = 5,000,000 VND

Feature: Tính lãi phạt và tiền thưởng sớm hạn
  # UC-DEBT-07

  Scenario: Tính lãi phạt cho hoá đơn quá hạn thanh toán
    # [DEBT-R09]
    Given chính sách lãi phạt "IP01" áp dụng cho phải thu, không tính lãi trong 5 ngày ân hạn, bậc thang: 1-30 ngày = 0.05%/ngày
    And hoá đơn "HD020" còn phải thu 10,000,000 VND, quá hạn 20 ngày (đã trừ ân hạn)
    When hệ thống chạy tính lãi phạt kỳ 06/2026
    Then "HD020" có InterestAmount = 10,000,000 × 0.05% × 20 / 100 (theo công thức đã khai báo)
```

## E. TDD Test Plan

| Layer | Test case | Map Gherkin |
|---|---|---|
| Domain unit | `Should_ZeroOutRemainingAmount_When_FullyMatched` | "Khớp một hoá đơn phải thu với một phiếu thu..." |
| Domain unit | `Should_ThrowDomainException_When_MatchAmountExceedsRemaining` | "Không cho khớp vượt quá số dư còn lại..." |
| Domain unit | `Should_RestoreRemainingAmount_When_MatchUndone` | "Huỷ khớp khôi phục lại số dư..." |
| Domain unit | `Should_RejectPair_When_ItemsHaveDifferentPartnerOrCurrency` | [DEBT-R02] |
| Domain unit | `Should_RejectNetting_When_PartnerIsNotBothCustomerAndSupplier` | "Chặn bù trừ khi đối tượng không đồng thời..." |
| Domain unit | `Should_BalanceReceivableAndPayableLines_When_NettingCreated` | [DEBT-R08] |
| Domain unit | `Should_ComputeAvailableLimit_When_CheckingCreditLimit` | "Từ chối khi vượt hạn mức tín dụng..." |
| Domain unit | `Should_ApplyBracketRate_ByOverdueDays_When_CalculatingInterest` | "Tính lãi phạt cho hoá đơn quá hạn..." |
| Domain unit | `Should_ApplyBonusPercent_When_PaidBeforeDueDate` | [DEBT-R09] (bonus case, bổ sung Gherkin nếu cần chi tiết hơn) |
| Application/use-case | `Should_PrioritizeEarliestDueDate_When_AutoClearingWithDueDateFormula` | "Giải trừ tự động ưu tiên hoá đơn..." |
| Application/use-case | `Should_UseLatestFxRevaluationRate_When_MatchingCrossYearItems` | [DEBT-R05] |
| Application/use-case | `Should_CreateOpenDebtItem_When_ConsumingSalesInvoicePostedEvent` | tích hợp với BC 04 |
| Integration | `Should_EnforceUniqueSourceVoucherReference_On_OpenDebtItem` | ràng buộc `UNIQUE(source_bounded_context, source_voucher_type, source_voucher_id)` |
| Acceptance | Toàn bộ scenario mục D | 1-1 |

## F. Open Questions / Gaps

1. **[DEBT-R06]** Huỷ khớp: hệ mới có cho phép huỷ từng `DebtMatchLine` riêng lẻ trong một `DebtMatch` nhiều dòng, hay chỉ huỷ nguyên cả `DebtMatch`? Cần xác nhận UX.
2. **[DEBT-R07]/DebtNetting**: chưa xác nhận được hệ cũ có phát sinh bút toán Nợ/Có GL khi bù trừ hay không — tài liệu đề xuất bổ sung `PostingLineRecorded` (cải tiến), cần chốt với chủ sản phẩm trước khi code-gen.
3. **[DEBT-R09]** Công thức tính lãi phạt/thưởng chính xác (đơn vị thời gian /360 hay /365, cách làm tròn, có áp dụng lãi kép hay không) chưa xác minh được 100% từ code trong thời gian khảo sát — cần đối chiếu với kế toán trưởng hoặc tài liệu nghiệp vụ gốc trước khi code-gen tầng tính toán.
4. **[DEBT-R10]** Giả định `ReceivableCreditLimit = null/0` nghĩa là không giới hạn — cần xác nhận. Ngoài ra, phía phải trả (`PayableCreditLimit`) có `CheckPayable` tương tự trong hệ mới hay không chưa được enforce rõ trong hệ cũ (chỉ thấy field, không thấy lời gọi chặn) — đánh dấu "nice-to-have", không bắt buộc theo hành vi hiện tại.
5. Cơ chế mutation trạng thái duyệt/khớp trong màn hình `AF0101`/`AF0276` (undo UI) không truy được đầy đủ logic — chỉ suy luận từ toolbar; cần xác nhận lại chi tiết quy trình duyệt trước khi hoàn thiện Application layer.
