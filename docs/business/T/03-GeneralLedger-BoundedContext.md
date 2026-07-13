# 03 — Bounded Context: General Ledger (Bút toán tổng hợp / Sổ cái / Phân bổ / Khoá kỳ)

> Tham chiếu nền tảng: `00-Ubiquitous-Language-Context-Map.md`. BC này là **downstream** của `01-CashBank`, `02-ARAP`, `04-PurchaseSales` (tổng hợp mọi `PostingLineRecorded`) và đồng thời sở hữu dữ liệu `AccountingPeriod` (khoá kỳ) mà cả 4 BC phải tuân theo.
> Nguồn xác minh nghiệp vụ: `04.ASOFT-T\Business\ApplicationFacades\AF0067Facade.cs` (bút toán thủ công), `DataAccess\ServiceComponents\AF0043Service.cs`/`AF0044.cs` (kết chuyển), `AF0045Service.cs`/`AF0047Service.cs`/`AF0353Service.cs` (phân bổ), `AF0212Service.cs`, `AF0234Service.cs`/`AF0236Service.cs` (ngoài bảng), `AF0170Service.cs` (sổ cái), `UI\Utilities\AX0001.cs` (khoá kỳ); dataset shared kernel `AT9000/9001/9004/9999/1703/1704/1705/7801Dataset.cs`; T-layer `AT9005DataSet.cs`, `AT1610DataSet.cs`.

## A. Tổng quan nghiệp vụ

**Mục đích**: là nơi tổng hợp toàn bộ bút toán kế toán (từ chính BC này lẫn từ Cash&Bank/AR-AP/Purchase&Sales) thành Sổ cái và Bảng cân đối phát sinh; cung cấp các nghiệp vụ điều chỉnh cuối kỳ (kết chuyển, phân bổ, chênh lệch tỷ giá, bút toán ngoài bảng, số dư đầu kỳ); và kiểm soát khoá kỳ kế toán.

**Actor**: Kế toán tổng hợp, Kế toán trưởng (khoá/mở kỳ).

**Use case chính**:

| Use case | Mã gốc |
|---|---|
| UC-GL-01 Lập bút toán tổng hợp (thủ công) | AF0067 |
| UC-GL-02 Lập/xoá bút toán kết chuyển cuối kỳ | AF0043/AF0044 |
| UC-GL-03 Kết chuyển theo mã phân tích | AF0045/AF0046 |
| UC-GL-04 Khai báo & chạy phân bổ chi phí | AF0047-53 |
| UC-GL-05 Phân bổ chi phí nhiều cấp | AF0353/AF0354 |
| UC-GL-06 Chênh lệch tỷ giá cuối kỳ / thanh toán / xuất | AF0027/AF0030/AF0252 |
| UC-GL-07 Kết chuyển chênh lệch giá xuất kho | AF0332 |
| UC-GL-08 Bút toán ngoài bảng (Ghi Nợ/Ghi Có) | AF0234/AF0235 |
| UC-GL-09 Bút toán số dư đầu kỳ | AF0089 |
| UC-GL-10 Khoá / mở kỳ kế toán | AX0001 (hạ tầng) |
| UC-GL-11 Xem Sổ cái tài khoản / Bảng cân đối phát sinh | AF0170/AF0344/AF0345/AF0346 |

## B. Domain Model (DDD)

### B.1 Aggregate: `JournalEntry` (thay thế toàn bộ AT9000/AT9001/AT9005 dùng cho bút toán "trên bảng")

**Quyết định thiết kế quan trọng**: hệ cũ biểu diễn mỗi *dòng* bút toán như một cặp Nợ/Có tự cân bằng (1 dòng = 1 `DebitAccountID` + 1 `CreditAccountID` + 1 số tiền), khiến một bút toán nhiều tài khoản phải tách thành nhiều dòng độc lập không có ràng buộc tổng Nợ = tổng Có xuyên dòng. Thiết kế mới áp dụng **mô hình bút toán kép chuẩn (proper double-entry)**: `JournalEntry` là aggregate gồm nhiều `JournalEntryLine`, mỗi dòng chỉ có **một** tài khoản và **một** phía (Nợ hoặc Có); ràng buộc bắt buộc ở cấp aggregate: `Σ dòng Nợ = Σ dòng Có`. Đây là điểm khác biệt lớn nhất so với hệ cũ, giúp hỗ trợ bút toán đa dòng linh hoạt hơn (vd 1 Nợ nhiều Có, hoặc nhiều Nợ nhiều Có) mà mô hình cũ không hỗ trợ trực tiếp.

| Thuộc tính | Kiểu |
|---|---|
| JournalEntryId | Guid, PK |
| BranchId | FK Branch |
| EntryType | enum{Manual, PeriodEndTransfer, AnalysisTransfer, Opening, FxRevaluation, SettlementFxDifference, InventoryCostVarianceTransfer, Allocation, MultiLevelAllocation, PayrollTransfer, PosTransfer} |
| VoucherNo | string |
| VoucherTypeCode | FK VoucherType |
| VoucherDate | date |
| EmployeeId | FK Employee |
| Description | string |
| Lines | List\<JournalEntryLine\> |

`JournalEntryLine` (Entity con):

| Thuộc tính | Kiểu |
|---|---|
| LineNo | int |
| AccountCode | FK Account |
| Side | enum{Debit, Credit} |
| Partner | FK BusinessPartner, nullable |
| Money | Value Object |
| AnalysisTags | List\<AnalysisTag\> |
| Description | string |

**Invariant/Business rule**:
- **[GL-R01] `Σ Money(Debit lines) = Σ Money(Credit lines)` trong cùng 1 `JournalEntry`, cùng 1 loại tiền hạch toán (`ConvertedAmount`).** Đây là invariant lõi thay thế cho tính "tự cân bằng theo dòng" của hệ cũ.
- **[GL-R02] Không cho lập/sửa/xoá `JournalEntry` nếu kỳ của `VoucherDate` đã khoá** (rule chung, Shared Kernel doc 00 mục 4.5).
- **[GL-R03] Kết chuyển cuối kỳ (`EntryType=PeriodEndTransfer`/`AnalysisTransfer`) chỉ dùng `VoucherType` thuộc nhóm `PeriodEndTransfer` (tương đương nhóm 98/99 hệ cũ), và chỉ được tạo/xoá khi kỳ của `VoucherDate` là **kỳ hiện hành** (đang mở, không chỉ "chưa khoá") — chặt hơn rule chung [GL-R02].** Nguồn: `AF0044.cs` — nút Xoá/Kết chuyển chỉ bật khi `_TranMonth==CurrentMonth && _TranYear==CurrentYear`.
- **[GL-R04] Bút toán số dư đầu kỳ (`EntryType=Opening`) chỉ được tạo tại ngày đầu tiên của kỳ đầu tiên sử dụng hệ thống cho mỗi tài khoản** (ràng buộc nghiệp vụ, không phải ràng buộc kỹ thuật — cần xác nhận thêm, xem mục F).

Domain Event: `JournalEntryPosted{JournalEntryId, EntryType, Lines[]}` → publish thành `PostingLineRecorded` chuẩn hoá cho từng `JournalEntryLine` (dùng cho `TrialBalanceProjector`).

### B.2 Aggregate: `AllocationJob` (Phân bổ chi phí đơn cấp)

| Thuộc tính | Kiểu |
|---|---|
| JobId | Guid, PK |
| JobCode | string |
| Description | string |
| SourceAccountCode | FK Account (tài khoản nguồn, vd chi phí trả trước 242) |
| TargetAccountCode | FK Account (tài khoản phân bổ tới, vd chi phí kỳ 641/642) |
| TotalMoney | Value Object |
| TotalPeriods | int |
| BeginYear / BeginMonth | int |
| Partner | FK BusinessPartner, nullable |
| Status | enum{Active, Completed, Cancelled} |
| Splits | List\<AllocationSplit\> |
| Schedule | List\<AllocationScheduleLine\> |

`AllocationSplit` (Entity con — tương đương `AT1705`): `AccountCode`, `AnalysisTags`, `PercentValue`.

`AllocationScheduleLine` (Entity con — tương đương `AT1704`): `PeriodYear`, `PeriodMonth`, `PlannedAmount`, `PostedJournalEntryId` (nullable, set khi đã kết chuyển kỳ đó), `Status{Planned, Posted}`.

**Invariant/Business rule**:
- **[GL-R05] `Σ AllocationSplit.PercentValue = 100%`** trong 1 `AllocationJob`. Nguồn: bản chất bảng `AT1705` (tỷ lệ phân bổ theo tài khoản/chiều phân tích).
- **[GL-R06] `Σ AllocationScheduleLine.PlannedAmount ≈ TotalMoney`** (sai số làm tròn ở kỳ cuối được cộng dồn vào dòng cuối để tổng khớp tuyệt đối — kỹ thuật phổ biến chia đều có làm tròn).
- **[GL-R07] Khi một `AllocationScheduleLine` được "chạy" (post kỳ đó), hệ thống tự sinh 1 `JournalEntry{EntryType=Allocation}`** với các dòng theo `AllocationSplit` (Nợ `TargetAccountCode`×%, Có `SourceAccountCode` tổng) và set `PostedJournalEntryId`/`Status=Posted`.
- Không cho chạy lại (post) một `AllocationScheduleLine` đã `Posted`.

Domain Event: `AllocationJobDeclared`, `AllocationPeriodPosted{JobId, PeriodYear, PeriodMonth, JournalEntryId}`.

### B.3 Aggregate: `MultiLevelAllocationChain` (Phân bổ nhiều cấp)

| Thuộc tính | Kiểu |
|---|---|
| ChainId | Guid, PK |
| ChainCode | string |
| Levels | List\<AllocationLevel\> (định nghĩa thứ tự cấp — tương đương `AT1610`) |
| Runs | List\<AllocationLevelRun\> |

`AllocationLevel`: `LevelNo`, `LevelName`, `LevelType`.

`AllocationLevelRun` (Entity con — tương đương `AT9005`): `LevelNo`, `JournalEntryId` (FK `JournalEntry{EntryType=MultiLevelAllocation}` đã sinh cho cấp này), `InheritRunId` (FK chính `AllocationLevelRun` của cấp trước, nullable ở cấp 1).

**Invariant/Business rule**:
- **[GL-R08] Cấp N (N>1) chỉ được chạy sau khi cấp N-1 đã có `AllocationLevelRun` hoàn tất trong cùng kỳ.** Nguồn: chuỗi `InheritVoucherID` quan sát trong `AT9005`.
- Không cho bỏ qua cấp (không có `AllocationLevelRun` cho cấp N mà thiếu cấp N-1 tương ứng trong cùng kỳ).

Domain Event: `AllocationLevelRunPosted{ChainId, LevelNo, JournalEntryId, InheritRunId}`.

### B.4 Aggregate: `OffBalanceSheetEntry` (Bút toán ngoài bảng)

Khác với `JournalEntry`, đây là **hạch toán đơn (single-entry)** — chỉ có 1 tài khoản, 1 phía Nợ hoặc Có, không có tài khoản đối ứng.

| Thuộc tính | Kiểu |
|---|---|
| EntryId | Guid, PK |
| BranchId | FK Branch |
| VoucherNo | string |
| VoucherDate | date |
| AccountCode | FK Account |
| Side | enum{Debit, Credit} |
| Partner | FK BusinessPartner, nullable |
| Money | Value Object |
| Description | string |

**Invariant**:
- **[GL-R09] `AccountCode.IsOffBalanceSheet` phải = `true`** (tương đương hệ cũ giới hạn `AT1005.GroupID='G00'`). Từ chối nếu tài khoản không thuộc nhóm ngoài bảng.
- Không tham gia ràng buộc [GL-R01] (không cần cân đối Nợ=Có vì bản chất là hạch toán đơn/ghi nhớ).

Domain Event: `OffBalanceSheetEntryPosted{EntryId, AccountCode, Side, Money}`.

### B.5 Domain Service: `TrialBalanceProjector` (CQRS Read Model)

Không có bảng số dư lưu trữ — **giữ nguyên triết lý hệ cũ** (Sổ cái/Bảng cân đối phát sinh luôn là view tính động, không lưu trạng thái), nhưng thay vì SQL view trực tiếp trên bảng vạn năng, projector này là một tiến trình đọc **toàn bộ `PostingLineRecorded`** phát ra từ 4 BC (kể cả `JournalEntry` của chính GL) để dựng 2 read-model:

- `AccountLedgerView{AccountCode, PeriodYear, PeriodMonth, OpeningBalance, PeriodDebit, PeriodCredit, ClosingBalance}` — tương đương `AV7101`.
- `TrialBalanceView{AccountCode, PeriodYear, PeriodMonth, OpeningDebit, OpeningCredit, PeriodDebit, PeriodCredit, ClosingDebit, ClosingCredit}` — tương đương `AF0344/345`.

**Invariant**: `ClosingBalance(kỳ N) = OpeningBalance(kỳ N+1)` theo từng tài khoản/chi nhánh (bắc cầu liên kỳ) — bất biến toàn vẹn cốt lõi của kế toán, phải được kiểm chứng bằng test tích hợp chạy qua nhiều kỳ liên tiếp.

### B.6 Aggregate: `AccountingPeriod` (đã định nghĩa Shared Kernel, GL sở hữu vòng đời)

Bổ sung Domain Service `CloseAccountingPeriod(branchId, year, month)`:
- **[GL-R10]** Trước khi khoá kỳ, khuyến nghị (⚠️ **cần xác nhận bắt buộc hay tuỳ chọn — xem mục F**) kiểm tra `TrialBalanceView` của kỳ đó cân đối (`Σ Debit = Σ Credit` toàn hệ thống) — không tìm thấy rule chặn tương ứng rõ ràng trong hệ cũ, nhưng đây là thông lệ kế toán chuẩn nên đề xuất thêm.
- Khi khoá kỳ N, tự động set `AccountingPeriod` kỳ N+1 sang `Status=Open` nếu chưa tồn tại (bootstrap kỳ mới) — quan sát gián tiếp qua `AX0001.System(...)` load kỳ hiện hành khi khởi động phiên làm việc.

## C. Đề xuất DB Schema mới

```sql
CREATE TABLE accounting_period (
  id          BIGINT IDENTITY PRIMARY KEY,
  branch_id   BIGINT NOT NULL REFERENCES branch(id),
  year        INT NOT NULL,
  month       INT NOT NULL,
  start_date  DATE NOT NULL,
  end_date    DATE NOT NULL,
  status      VARCHAR(10) NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','Closed')),
  UNIQUE (branch_id, year, month)
);

CREATE TABLE journal_entry (
  id                BIGINT IDENTITY PRIMARY KEY,
  branch_id         BIGINT NOT NULL REFERENCES branch(id),
  entry_type        VARCHAR(30) NOT NULL,
  voucher_no        VARCHAR(30) NOT NULL,
  voucher_type_code VARCHAR(20) NOT NULL REFERENCES voucher_type(voucher_type_code),
  voucher_date      DATE NOT NULL,
  employee_id       BIGINT NOT NULL REFERENCES employee(id),
  description       NVARCHAR(500),
  created_at        DATETIME2 NOT NULL,
  created_by        BIGINT NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

CREATE TABLE journal_entry_line (
  id                BIGINT IDENTITY PRIMARY KEY,
  journal_entry_id  BIGINT NOT NULL REFERENCES journal_entry(id),
  line_no           INT NOT NULL,
  account_code      VARCHAR(20) NOT NULL REFERENCES account(account_code),
  side              VARCHAR(10) NOT NULL CHECK (side IN ('Debit','Credit')),
  partner_id        BIGINT NULL REFERENCES business_partner(id),
  currency_code     CHAR(3) NOT NULL REFERENCES currency(currency_code),
  exchange_rate     DECIMAL(18,6) NOT NULL,
  original_amount   DECIMAL(18,2) NOT NULL,
  converted_amount  DECIMAL(18,2) NOT NULL,
  description       NVARCHAR(500),
  UNIQUE (journal_entry_id, line_no)
);

CREATE TABLE journal_entry_line_analysis_tag (
  journal_entry_line_id BIGINT NOT NULL REFERENCES journal_entry_line(id),
  dimension_type_code    VARCHAR(20) NOT NULL,
  tag_code                VARCHAR(20) NOT NULL,
  PRIMARY KEY (journal_entry_line_id, dimension_type_code)
);

CREATE TABLE allocation_job (
  id                  BIGINT IDENTITY PRIMARY KEY,
  job_code            VARCHAR(30) NOT NULL UNIQUE,
  description         NVARCHAR(200),
  source_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  target_account_code VARCHAR(20) NOT NULL REFERENCES account(account_code),
  currency_code       CHAR(3) NOT NULL REFERENCES currency(currency_code),
  total_original_amount  DECIMAL(18,2) NOT NULL,
  total_periods        INT NOT NULL,
  begin_year            INT NOT NULL,
  begin_month           INT NOT NULL,
  partner_id            BIGINT NULL REFERENCES business_partner(id),
  status                 VARCHAR(10) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Completed','Cancelled'))
);

CREATE TABLE allocation_split (
  id                  BIGINT IDENTITY PRIMARY KEY,
  allocation_job_id   BIGINT NOT NULL REFERENCES allocation_job(id),
  account_code        VARCHAR(20) NOT NULL REFERENCES account(account_code),
  percent_value        DECIMAL(9,4) NOT NULL
);

CREATE TABLE allocation_split_analysis_tag (
  allocation_split_id BIGINT NOT NULL REFERENCES allocation_split(id),
  dimension_type_code  VARCHAR(20) NOT NULL,
  tag_code              VARCHAR(20) NOT NULL,
  PRIMARY KEY (allocation_split_id, dimension_type_code)
);

CREATE TABLE allocation_schedule_line (
  id                    BIGINT IDENTITY PRIMARY KEY,
  allocation_job_id     BIGINT NOT NULL REFERENCES allocation_job(id),
  period_year           INT NOT NULL,
  period_month          INT NOT NULL,
  planned_amount        DECIMAL(18,2) NOT NULL,
  posted_journal_entry_id BIGINT NULL REFERENCES journal_entry(id),
  status                  VARCHAR(10) NOT NULL DEFAULT 'Planned' CHECK (status IN ('Planned','Posted')),
  UNIQUE (allocation_job_id, period_year, period_month)
);

CREATE TABLE allocation_level (
  id          BIGINT IDENTITY PRIMARY KEY,
  chain_code  VARCHAR(30) NOT NULL,
  level_no    INT NOT NULL,
  level_name  NVARCHAR(200) NOT NULL,
  UNIQUE (chain_code, level_no)
);

CREATE TABLE allocation_level_run (
  id                  BIGINT IDENTITY PRIMARY KEY,
  chain_code          VARCHAR(30) NOT NULL,
  level_no            INT NOT NULL,
  period_year          INT NOT NULL,
  period_month         INT NOT NULL,
  journal_entry_id     BIGINT NOT NULL REFERENCES journal_entry(id),
  inherit_run_id       BIGINT NULL REFERENCES allocation_level_run(id),
  UNIQUE (chain_code, level_no, period_year, period_month)
);

CREATE TABLE off_balance_sheet_entry (
  id            BIGINT IDENTITY PRIMARY KEY,
  branch_id     BIGINT NOT NULL REFERENCES branch(id),
  voucher_no    VARCHAR(30) NOT NULL,
  voucher_date  DATE NOT NULL,
  account_code  VARCHAR(20) NOT NULL REFERENCES account(account_code),
  side          VARCHAR(10) NOT NULL CHECK (side IN ('Debit','Credit')),
  partner_id    BIGINT NULL REFERENCES business_partner(id),
  currency_code CHAR(3) NOT NULL REFERENCES currency(currency_code),
  original_amount DECIMAL(18,2) NOT NULL,
  description     NVARCHAR(500),
  created_at      DATETIME2 NOT NULL,
  UNIQUE (branch_id, voucher_no)
);

-- Read model (CQRS projection), có thể vật lý hoá bằng materialized view / bảng tổng hợp định kỳ
CREATE TABLE account_ledger_view (
  account_code    VARCHAR(20) NOT NULL REFERENCES account(account_code),
  branch_id       BIGINT NOT NULL REFERENCES branch(id),
  period_year     INT NOT NULL,
  period_month    INT NOT NULL,
  opening_balance DECIMAL(18,2) NOT NULL,
  period_debit    DECIMAL(18,2) NOT NULL,
  period_credit   DECIMAL(18,2) NOT NULL,
  closing_balance DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (account_code, branch_id, period_year, period_month)
);
```

**Mapping ngược hệ cũ**: `journal_entry`/`journal_entry_line` ↔ `AT9000` (lọc theo `EntryType` suy ra từ `VoucherTypeID`/`TableID` cũ); `allocation_job/split/schedule_line` ↔ `AT1703/1705/1704`; `allocation_level`/`allocation_level_run` ↔ `AT1610`/`AT9005` (+ dòng chi tiết vốn ở `AT9001`); `off_balance_sheet_entry` ↔ `AT9004`; `accounting_period` ↔ `AT9999`; `account_ledger_view` ↔ view `AV7101`/`AV7211`.

## D. BDD Scenarios (Gherkin)

```gherkin
Feature: Lập bút toán tổng hợp
  # UC-GL-01

  Scenario: Chấp nhận bút toán có tổng Nợ bằng tổng Có
    # [GL-R01]
    Given kế toán lập bút toán gồm: Nợ TK "6421" 5,000,000 VND; Có TK "1111" 3,000,000 VND; Có TK "1121" 2,000,000 VND
    When kế toán lưu bút toán
    Then bút toán được lưu thành công
    And sự kiện "JournalEntryPosted" được phát ra

  Scenario: Từ chối bút toán mất cân đối Nợ/Có
    # [GL-R01]
    Given kế toán lập bút toán gồm: Nợ TK "6421" 5,000,000 VND; Có TK "1111" 4,000,000 VND
    When kế toán lưu bút toán
    Then hệ thống từ chối lưu với lý do "Tổng Nợ khác tổng Có"

Feature: Kết chuyển cuối kỳ
  # UC-GL-02

  Scenario: Chặn tạo bút toán kết chuyển ngoài kỳ hiện hành
    # [GL-R03]
    Given kỳ hiện hành của hệ thống là 06/2026
    When kế toán cố tạo bút toán kết chuyển cho kỳ 05/2026
    Then hệ thống từ chối vì kết chuyển chỉ được thực hiện trong kỳ hiện hành

  Scenario: Cho phép xoá bút toán kết chuyển trong kỳ hiện hành
    # [GL-R03]
    Given kỳ hiện hành của hệ thống là 06/2026
    And có bút toán kết chuyển "KC001" đã lập trong kỳ 06/2026
    When kế toán xoá bút toán "KC001"
    Then bút toán "KC001" bị xoá thành công

Feature: Phân bổ chi phí
  # UC-GL-04

  Scenario: Từ chối khai báo công việc phân bổ khi tổng tỷ lệ % khác 100
    # [GL-R05]
    Given kế toán khai báo công việc phân bổ "JOB001" với các dòng tỷ lệ: TK "6421" 60%, TK "6422" 30%
    When kế toán lưu khai báo
    Then hệ thống từ chối vì tổng tỷ lệ phân bổ là 90%, khác 100%

  Scenario: Chạy phân bổ một kỳ sinh đúng 1 bút toán phân bổ
    # [GL-R07]
    Given công việc phân bổ "JOB002" hợp lệ (tổng tỷ lệ 100%), có lịch phân bổ kỳ 06/2026 với số tiền 10,000,000 VND
    When hệ thống chạy phân bổ kỳ 06/2026 cho "JOB002"
    Then một "JournalEntry" loại "Allocation" được sinh ra với tổng tiền 10,000,000 VND
    And dòng lịch phân bổ kỳ 06/2026 của "JOB002" chuyển trạng thái "Posted"

  Scenario: Không cho chạy lại một kỳ phân bổ đã Posted
    Given dòng lịch phân bổ kỳ 06/2026 của "JOB002" đã ở trạng thái "Posted"
    When hệ thống cố chạy phân bổ kỳ 06/2026 cho "JOB002" lần nữa
    Then hệ thống từ chối vì kỳ này đã được phân bổ

Feature: Phân bổ nhiều cấp
  # UC-GL-05

  Scenario: Chặn chạy cấp 2 khi cấp 1 chưa có kết quả trong cùng kỳ
    # [GL-R08]
    Given chuỗi phân bổ "CHAIN01" định nghĩa 2 cấp
    And cấp 1 của "CHAIN01" chưa được chạy cho kỳ 06/2026
    When kế toán cố chạy cấp 2 của "CHAIN01" cho kỳ 06/2026
    Then hệ thống từ chối vì thiếu kết quả cấp 1 trong cùng kỳ

Feature: Bút toán ngoài bảng
  # UC-GL-08

  Scenario: Chặn hạch toán ngoài bảng vào tài khoản không thuộc nhóm ngoài bảng
    # [GL-R09]
    Given tài khoản "1111" không thuộc nhóm tài khoản ngoài bảng
    When kế toán lập bút toán Ghi Nợ ngoài bảng vào tài khoản "1111"
    Then hệ thống từ chối lưu

Feature: Khoá kỳ kế toán
  # UC-GL-10 — rule dùng chung toàn hệ thống

  Scenario: Chặn mọi BC tạo chứng từ vào kỳ đã khoá
    # rule chung Shared Kernel doc 00 mục 4.5, kiểm chứng tại GL
    Given kỳ 04/2026 của chi nhánh "HN" đã bị khoá
    When bất kỳ Bounded Context nào cố ghi 1 "PostingLineRecorded" có ngày thuộc kỳ 04/2026
    Then thao tác bị từ chối bởi "PeriodLockGuard"

Feature: Sổ cái / Bảng cân đối phát sinh
  # UC-GL-11

  Scenario: Số dư cuối kỳ liên kết đúng với số dư đầu kỳ sau
    Given tài khoản "1111" có số dư cuối kỳ 05/2026 là 50,000,000 VND
    When hệ thống dựng "AccountLedgerView" cho kỳ 06/2026 của tài khoản "1111"
    Then số dư đầu kỳ 06/2026 của tài khoản "1111" bằng 50,000,000 VND
```

## E. TDD Test Plan

| Layer | Test case | Map Gherkin |
|---|---|---|
| Domain unit | `Should_AcceptEntry_When_TotalDebitEqualsTotalCredit` | "Chấp nhận bút toán có tổng Nợ bằng tổng Có" |
| Domain unit | `Should_ThrowDomainException_When_EntryIsUnbalanced` | "Từ chối bút toán mất cân đối..." |
| Domain unit | `Should_RejectCreate_When_PeriodEndTransferNotInCurrentPeriod` | "Chặn tạo bút toán kết chuyển ngoài kỳ hiện hành" |
| Domain unit | `Should_AllowDelete_When_PeriodEndTransferInCurrentPeriod` | "Cho phép xoá bút toán kết chuyển..." |
| Domain unit | `Should_ThrowDomainException_When_AllocationSplitPercentNotEqual100` | "Từ chối khai báo công việc phân bổ..." |
| Domain unit | `Should_MarkScheduleLinePosted_When_AllocationPeriodRun` | "Chạy phân bổ một kỳ sinh đúng 1 bút toán..." |
| Domain unit | `Should_RejectRePost_When_ScheduleLineAlreadyPosted` | "Không cho chạy lại một kỳ phân bổ đã Posted" |
| Domain unit | `Should_RejectLevelRun_When_PriorLevelRunMissingInSamePeriod` | "Chặn chạy cấp 2 khi cấp 1 chưa có kết quả..." |
| Domain unit | `Should_RejectEntry_When_AccountIsNotOffBalanceSheet` | "Chặn hạch toán ngoài bảng vào tài khoản không thuộc nhóm..." |
| Application/use-case | `Should_RejectAnyCommand_When_TargetPeriodIsClosed` | "Chặn mọi BC tạo chứng từ vào kỳ đã khoá" |
| Application/use-case | `Should_ProjectAccountLedger_FromPostingLineRecordedEvents` | (nền cho scenario Sổ cái) |
| Integration | `Should_CarryClosingBalanceForward_As_NextPeriodOpeningBalance` | "Số dư cuối kỳ liên kết đúng với số dư đầu kỳ sau" |
| Integration | `Should_EnforceUniqueVoucherNoPerBranch_On_JournalEntry` | ràng buộc DB |
| Acceptance | Toàn bộ scenario mục D | 1-1 |

## F. Open Questions / Gaps

1. **[GL-R04]** Ràng buộc "chỉ tạo số dư đầu kỳ một lần cho mỗi tài khoản/kỳ đầu tiên" là suy luận nghiệp vụ, chưa xác minh được validation cụ thể trong `AF0089Service.cs` (chỉ thấy thao tác Delete/Insert vào AT9000, không thấy rule chặn tạo trùng) — cần xác nhận trước khi code-gen.
2. **[GL-R10]** Chưa xác minh được hệ cũ có bắt buộc kiểm tra cân đối bảng cân đối phát sinh trước khi khoá kỳ hay không — đề xuất là cải tiến, cần chốt với chủ sản phẩm (bắt buộc hay cảnh báo).
3. Thuật toán tổng hợp cụ thể trong các view `AV7101`/`AV7211` (cách xử lý tài khoản có `NormalBalanceSide` khác nhau khi cộng dồn số dư, cách xử lý đa tiền tệ trong báo cáo — theo nguyên tệ hay chỉ theo tiền quy đổi) chưa được đọc trực tiếp (view SQL không nằm trong phạm vi khảo sát mã nguồn .NET) — cần lấy định nghĩa view thật từ DBA/DB script trước khi code-gen `TrialBalanceProjector`.
4. Danh sách đầy đủ giá trị `LevelType` của `AllocationLevel` (bản chất từng cấp phân bổ nhiều cấp đại diện cho cái gì về mặt nghiệp vụ — trung tâm chi phí trung gian? sản phẩm?) chưa được xác minh chi tiết — cần khảo sát thêm ví dụ dữ liệu thực tế của khách hàng đang dùng tính năng này trước khi hoàn thiện.
