# 00 — Ngôn ngữ chung (Ubiquitous Language) & Context Map
## Phân hệ Kế toán ASOFT-T — Tài liệu nền tảng cho bộ DDD-BDD-TDD

> **Vai trò của tài liệu này**: đây là tài liệu nền, được nạp/tham chiếu bởi 4 tài liệu bounded-context còn lại (`01-CashBank`, `02-ARAP`, `03-GeneralLedger`, `04-PurchaseSales`). Mọi entity dùng chung (Shared Kernel) được định nghĩa **một lần duy nhất** ở đây; các tài liệu khác chỉ trích dẫn bằng tên (vd `` `BusinessPartner` (xem 00 mục 4.10) ``), không định nghĩa lại.
>
> **Nguồn tham chiếu nghiệp vụ**: toàn bộ nội dung được xác minh từ mã nguồn hệ thống cũ tại `F:\40.SOURCES.GREE\01.ERP8\04.ASOFT-T` (phân hệ T — Kế toán doanh nghiệp) và shared-kernel `F:\40.SOURCES.GREE\01.ERP8\01.ASOFT-SM`. Legacy code **chỉ dùng để xác định đúng nghiệp vụ/luật hiện hành**, KHÔNG dùng làm khuôn thiết kế schema — schema mới được chuẩn hoá lại theo từng Bounded Context (BC), không tái sử dụng mô hình "bảng chứng từ vạn năng" (AT9000/AT9001/AT9004/AT9010) của hệ cũ.

---

## 1. Phạm vi bộ tài liệu

Bộ tài liệu bao phủ 4 Bounded Context lõi của nghiệp vụ kế toán doanh nghiệp:

| # | Bounded Context | Tài liệu | Nghiệp vụ chính |
|---|---|---|---|
| 1 | **Cash & Bank** (Tiền mặt & Ngân hàng) | `01-CashBank-BoundedContext.md` | Thu/chi tiền mặt, thu/chi/chuyển khoản ngân hàng, tạm ứng thu-chi + duyệt 2 cấp, đổi tiền, cập nhật tỷ giá |
| 2 | **Debt (AR/AP)** (Công nợ phải thu/phải trả) | `02-ARAP-BoundedContext.md` | Khớp công nợ mở (thủ công + tự động), bù trừ công nợ, chuyển đổi nguyên tệ công nợ, tính lãi phạt/thưởng sớm hạn, hạn mức tín dụng |
| 3 | **General Ledger** (Bút toán tổng hợp/Sổ cái) | `03-GeneralLedger-BoundedContext.md` | Bút toán thủ công, kết chuyển cuối kỳ, phân bổ chi phí (đơn cấp + nhiều cấp), chênh lệch tỷ giá cuối kỳ, bút toán ngoài bảng, khóa kỳ kế toán, sổ cái/bảng cân đối phát sinh |
| 4 | **Purchase & Sales (Accounting)** (Mua hàng & Bán hàng — hạch toán) | `04-PurchaseSales-BoundedContext.md` | Hóa đơn mua/bán, hàng trả lại, bán hàng theo bộ (KIT), chiết khấu, phân bổ chi phí mua hàng, VAT/thuế TTĐB |

Ngoài phạm vi (out of scope, không phân tích trong bộ tài liệu này): Quản lý kho/vật tư chi tiết (WM), Tài sản cố định (FA), Tiền lương/HRM, Hợp đồng hạn mức vay ngân hàng/giải ngân (thuộc nhóm "Quản lý vay" — trùng từ khoá "hạn mức" với credit-limit AR nhưng là domain khác), CRM/POS front-end.

---

## 2. Bảng thuật ngữ song ngữ (Ubiquitous Language)

Quy ước: **Domain Term** là tên chuẩn dùng trong toàn bộ tài liệu DDD/BDD/TDD (code, class, event đều đặt tên theo cột này). Cột "Mã cũ" chỉ để truy vết, không dùng trong thiết kế mới.

### 2.1 Chứng từ & giao dịch chung

| Thuật ngữ VN | Domain Term (EN) | Định nghĩa | Mã cũ (truy vết) |
|---|---|---|---|
| Chứng từ | Voucher | Một bản ghi nghiệp vụ kế toán đã phát sinh, có số chứng từ, ngày chứng từ, thuộc một loại chứng từ | AT9000/AT9010 (mọi loại) |
| Số chứng từ | VoucherNo | Số định danh chứng từ, duy nhất theo (Chi nhánh, Loại chứng từ, Kỳ) | VoucherNo |
| Loại chứng từ | VoucherType | Danh mục phân loại chứng từ (phiếu thu, phiếu chi, bút toán tổng hợp...), có nhóm chứng từ (VoucherGroup) | AT1007 |
| Nhóm chứng từ | VoucherGroup | Phân nhóm loại chứng từ, dùng để giới hạn phạm vi sử dụng màn hình (vd nhóm 98/99 dành riêng cho bút toán kết chuyển) | AT1007.VoucherGroupID |
| Dòng hạch toán | PostingLine / JournalLine | Một dòng ghi Nợ 1 tài khoản và Có 1 tài khoản với cùng số tiền — đơn vị hạch toán nhỏ nhất | 1 dòng AT9000 |
| Ghi Nợ / Ghi Có | Debit / Credit | Hai vế của bút toán kép | DebitAccountID/CreditAccountID |
| Đối tượng (khách hàng/NCC) | BusinessPartner | Bên thứ ba có phát sinh công nợ hoặc giao dịch (khách hàng, nhà cung cấp, hoặc cả hai) | AT1202 |
| Nhân viên | Employee | Người lập/phê duyệt chứng từ | AT1103 |
| Chi nhánh | Branch / Division | Đơn vị hạch toán độc lập trong cùng công ty | DivisionID |
| Tài khoản kế toán | Account (Chart of Accounts) | Tài khoản trong hệ thống tài khoản kế toán | AT1005 |
| Loại tiền | Currency | Đơn vị tiền tệ giao dịch | AT1004 |
| Tỷ giá | ExchangeRate | Tỷ giá quy đổi từ nguyên tệ sang tiền hạch toán (VND) tại một ngày | AF003801 |
| Nguyên tệ / Tiền quy đổi | OriginalAmount / ConvertedAmount | Số tiền theo loại tiền giao dịch gốc, và số tiền đã quy đổi sang tiền hạch toán chuẩn | OriginalAmount/ConvertedAmount |
| Kỳ kế toán | AccountingPeriod | Khoảng thời gian (tháng/năm) dùng để khoá sổ | AT9999 |
| Khóa sổ | PeriodClosing | Trạng thái một kỳ kế toán không còn cho phép ghi/sửa/xoá chứng từ | AT9999.Closing |
| Mã phân tích | AnalysisDimension / CostCenter tag | Chiều phân tích chi phí/doanh thu bổ sung ngoài tài khoản (tối đa 10 chiều trong hệ cũ) | AT1011, Ana01..Ana10ID |
| Duyệt chứng từ | VoucherApproval / Audit | Xác nhận chứng từ đã được kiểm duyệt, khoá không cho sửa | IsAudit |
| Trạng thái chứng từ | VoucherStatus | 0 = chưa xử lý/mở; 1 = đã xử lý/đã khớp/đã khoá (ý nghĩa cụ thể theo từng BC, xem tài liệu con) | Status |

### 2.2 Tiền mặt & Ngân hàng

| Thuật ngữ VN | Domain Term (EN) | Định nghĩa | Mã cũ |
|---|---|---|---|
| Phiếu thu | CashReceiptVoucher | Chứng từ ghi nhận thu tiền mặt | AF0061 |
| Phiếu chi | CashPaymentVoucher | Chứng từ ghi nhận chi tiền mặt | AF0062 |
| Phiếu thu/chi qua ngân hàng | BankReceiptVoucher / BankPaymentVoucher | Thu/chi qua tài khoản ngân hàng | AF0102/AF0103 |
| Phiếu chuyển khoản ngân hàng | BankTransferVoucher | Chuyển tiền giữa 2 tài khoản ngân hàng | AF0104 |
| Phiếu đổi tiền | CurrencyConversionVoucher | Chuyển đổi số dư quỹ từ loại tiền này sang loại tiền khác | AF0068 |
| Phiếu tạm thu / tạm chi | TempAdvanceReceipt / TempAdvancePayment | Thu/chi tạm ứng, cần duyệt trước khi ghi nhận chính thức | AF0099/AF0100 |
| Duyệt tạm thu-chi (cấp 1) | AdvanceApprovalLevel1 | Bước duyệt đầu cho phiếu tạm ứng | AF0101 |
| Duyệt tạm chi cấp 2 | AdvanceApprovalLevel2 | Bước duyệt thứ hai, áp dụng cho tạm chi giá trị lớn/theo cấu hình | AF0370 |
| Tồn quỹ | FundBalance | Số dư tiền mặt/ngân hàng khả dụng tại một thời điểm | tính động từ PostingLine |
| Hạn mức tín dụng (bên mua) | ReceivableCreditLimit | Mức công nợ phải thu tối đa cho phép với một đối tượng | AT1202.ReCreditLimit |
| Cho phép âm quỹ | AllowNegativeFund | Cấu hình hệ thống có cho phép chi vượt tồn quỹ hay không | SYS.IsNegativeCash |
| Ngân sách chi | ExpenseBudget | Hạn mức chi theo tài khoản + kỳ + phân tích, vượt thì cảnh báo (không chặn) | AT9090 |

### 2.3 Công nợ phải thu/phải trả (AR/AP)

| Thuật ngữ VN | Domain Term (EN) | Định nghĩa | Mã cũ |
|---|---|---|---|
| Chứng từ nợ mở | OpenDebitItem | Dòng hoá đơn/phải thu chưa được khớp hết | AT0301, view AV0301 |
| Chứng từ có mở | OpenCreditItem | Dòng thu tiền/phải trả chưa được khớp hết | AT0302, view AV0302 |
| Khớp công nợ (giải trừ) | DebtClearing / OpenItemMatch | Hành động ghép một hoặc nhiều dòng nợ mở với một hoặc nhiều dòng có mở, tạo bản ghi khớp | AT0303 |
| Mã khớp | MatchID | Định danh một lần khớp | GiveUpID |
| Giải trừ tự động | AutoDebtClearing | Khớp công nợ hàng loạt theo thuật toán FIFO (theo ngày chứng từ hoặc ngày đến hạn) trong một khoảng đối tượng/kỳ | AP0311 |
| Bù trừ công nợ | DebtNetting | Cấn trừ công nợ phải thu và phải trả của cùng một đối tượng (vừa là KH vừa là NCC) | AF0600 (AT0600/AT0601) |
| Hủy khớp | UnmatchDebt | Đảo ngược một MatchID đã tạo | DeleteGiveUpID |
| Số dư còn lại (mở) | RemainingOpenAmount | Số tiền chưa khớp = Gốc − Tổng đã khớp; **luôn tính động, không lưu trữ** | tính từ AT0303 |
| Chính sách lãi phạt/thưởng | InterestPolicy | Quy tắc tính lãi phạt trễ hạn hoặc thưởng thanh toán sớm | AT1018 |
| Bậc lãi suất | InterestBracket | Khung số ngày (hoặc số tiền) ứng với một mức lãi suất % trong chính sách lãi phạt | AT1019 |
| Ngày đến hạn | DueDate | Ngày phải thanh toán theo điều khoản công nợ | DueDate |
| Điều khoản thanh toán | PaymentTerm | Số ngày cho phép nợ, áp dụng theo đối tượng | ReDueDays/PaDueDays |

### 2.4 Sổ cái/Kết chuyển/Phân bổ (GL)

| Thuật ngữ VN | Domain Term (EN) | Định nghĩa | Mã cũ |
|---|---|---|---|
| Bút toán tổng hợp | ManualJournalEntry | Bút toán Nợ/Có nhập tay, không phát sinh từ nghiệp vụ khác | AF0067 |
| Kết chuyển cuối kỳ | PeriodEndTransferEntry | Bút toán chuyển số dư giữa các tài khoản vào cuối kỳ, chỉ tạo/xoá được trong kỳ hiện hành | AF0043/44 |
| Kết chuyển theo mã phân tích | AnalysisTransferEntry | Kết chuyển có gắn chiều phân tích/hệ số phân bổ | AF0045/46 |
| Phân bổ chi phí | CostAllocation | Phân chia một khoản chi phí/doanh thu trả trước thành nhiều kỳ và/hoặc nhiều đối tượng phân tích theo tỷ lệ % | AF0047-53 |
| Công việc phân bổ | AllocationJob | Bản khai báo một lần phân bổ: tổng tiền, số kỳ, tỷ lệ % theo từng (tài khoản, chiều phân tích) | AT1703 |
| Lịch phân bổ | AllocationSchedule | Danh sách các kỳ và số tiền đã/sẽ phát sinh của một AllocationJob | AT1704 |
| Bảng tỷ lệ phân bổ | AllocationSplitTable | Tỷ lệ % phân chia theo (tài khoản, chiều phân tích) của một AllocationJob | AT1705 |
| Phân bổ nhiều cấp | MultiLevelAllocation | Phân bổ theo chuỗi: kết quả cấp 1 tiếp tục làm đầu vào phân bổ cấp 2 | AF0353/354 (AT9005 kế thừa qua InheritVoucherID) |
| Chênh lệch tỷ giá cuối kỳ | PeriodEndFxRevaluation | Đánh giá lại số dư nguyên tệ theo tỷ giá cuối kỳ, phát sinh lãi/lỗ chênh lệch | AF0027 |
| Chênh lệch tỷ giá thanh toán | SettlementFxDifference | Chênh lệch tỷ giá phát sinh khi thanh toán công nợ nguyên tệ | AF0030 |
| Kết chuyển chênh lệch giá xuất kho | InventoryCostVarianceTransfer | Kết chuyển chênh lệch do phương pháp tính giá xuất kho | AF0332 |
| Bút toán ngoài bảng | OffBalanceSheetEntry | Bút toán hạch toán đơn (1 vế), không ảnh hưởng báo cáo tài chính chính thức | AF0234/235 (AT9004) |
| Số dư đầu kỳ | OpeningBalanceEntry | Bút toán khởi tạo số dư đầu kỳ cho tài khoản | AF0089 |
| Sổ cái tài khoản | AccountLedger | Báo cáo liệt kê phát sinh + số dư đầu/cuối của một tài khoản theo kỳ | view AV7101 |
| Bảng cân đối phát sinh (trial balance) | TrialBalanceReport | Báo cáo tổng hợp số dư đầu kỳ, phát sinh Nợ/Có, số dư cuối kỳ của tất cả tài khoản | AF0344/345 |

### 2.5 Mua hàng & Bán hàng (hạch toán)

| Thuật ngữ VN | Domain Term (EN) | Định nghĩa | Mã cũ |
|---|---|---|---|
| Phiếu mua hàng | PurchaseInvoice | Chứng từ ghi nhận mua hàng hoá/dịch vụ, phát sinh công nợ phải trả | AF0063 |
| Hóa đơn bán hàng | SalesInvoice | Chứng từ ghi nhận bán hàng hoá/dịch vụ, phát sinh công nợ phải thu | AF0066 |
| Hàng mua trả lại | PurchaseReturn | Trả lại hàng đã mua cho NCC | AF0073 |
| Hàng bán trả lại | SalesReturn | Nhận lại hàng đã bán từ KH | AF0069 |
| Bán hàng theo bộ (KIT) | KitSalesInvoice | Hóa đơn bán một bộ sản phẩm được "nổ" thành nhiều dòng thành phần theo công thức | AF0144 |
| Công thức bộ (KIT) | KitFormula (Bill of Materials) | Danh sách thành phần và tỷ lệ cấu thành một mặt hàng bộ | AT1326 |
| Chiết khấu | DiscountVoucher | Chứng từ giảm trừ công nợ phải thu của một hóa đơn bán hàng đã lập, không tạo bút toán Nợ/Có mới | AF3101 (AT3101) |
| Phân bổ chi phí mua hàng | PurchaseCostAllocation | Phân bổ chi phí phát sinh thêm (vận chuyển, nhập khẩu...) vào giá trị các dòng mua hàng | AF0314 |
| Vật tư/Hàng hóa | Item / Product | Danh mục hàng hoá, vật tư, dịch vụ mua bán | AT1302 |
| Loại thuế GTGT | VatType | Phân loại thuế GTGT áp dụng cho dòng hàng hoá | AT1009 |
| Hóa đơn điện tử | EInvoice | Hóa đơn điện tử liên kết với hóa đơn giấy/hệ thống, có mã định danh riêng | InvoiceGuid |

---

## 3. Context Map

### 3.1 Sơ đồ quan hệ Bounded Context

```
                     ┌───────────────────────────┐
                     │   Shared Kernel (mục 4)    │
                     │ ChartOfAccounts, Currency, │
                     │ AccountingPeriod, Partner, │
                     │ Item, VoucherType, ...     │
                     └─────────────┬─────────────┘
                                   │ dùng chung bởi tất cả
        ┌──────────────────────────┼──────────────────────────┐
        │                          │                           │
┌───────▼────────┐        ┌────────▼────────┐         ┌────────▼─────────┐
│  Cash & Bank    │        │   Debt (AR/AP)  │         │ Purchase & Sales  │
│  (01)           │───────▶│   (02)          │◀────────│  (04)             │
│ Thu/chi/tạm ứng │ tạo    │ Khớp công nợ,   │ tạo hoá │ Hoá đơn mua/bán   │
│                 │ PostingLine│ hạn mức, lãi │ đơn = OpenItem│           │
└───────┬────────┘        └────────┬────────┘         └────────┬─────────┘
        │                          │                            │
        │        đều publish PostingRecorded / cần tổng hợp     │
        └──────────────────┬───────────────────────────────────┘
                            ▼
                  ┌───────────────────────┐
                  │   General Ledger (03)  │
                  │  Sổ cái, kết chuyển,   │
                  │  phân bổ, khoá kỳ      │
                  │  (Downstream — tổng    │
                  │  hợp mọi PostingLine)  │
                  └───────────────────────┘
```

### 3.2 Bảng quan hệ Upstream/Downstream

| Quan hệ | Loại | Mô tả |
|---|---|---|
| Cash&Bank → Debt | Customer/Supplier (Cash&Bank là downstream khi thu tiền cần kiểm tra hạn mức; là upstream khi phiếu thu/chi trở thành OpenCreditItem để Debt khớp) | Phiếu thu/chi tiền/ngân hàng khi gắn với một `BusinessPartner` sẽ sinh ra một `OpenCreditItem`/`OpenDebitItem` mà Debt Context dùng để khớp công nợ. Debt Context expose `CreditLimitChecker` (Domain Service) mà Cash&Bank gọi trước khi cho phép ghi phiếu thu. |
| Purchase&Sales → Debt | Upstream (Purchase&Sales là nguồn phát sinh công nợ) | Mỗi hoá đơn mua/bán tạo một `OpenDebitItem`/`OpenCreditItem` tương ứng để Debt khớp khi thanh toán. |
| Purchase&Sales → Cash&Bank | Không phụ thuộc trực tiếp | Thanh toán hoá đơn thực hiện qua Cash&Bank (phiếu thu/chi), liên kết qua Debt Context (khớp công nợ), không có liên kết trực tiếp giữa 2 BC này. |
| Cash&Bank, Debt, Purchase&Sales → General Ledger | Tất cả là Upstream, GL là Downstream (Conformist) | Mỗi hành vi nghiệp vụ tạo ra `PostingLine` (bút toán Nợ/Có) được publish sang GL để tổng hợp lên Sổ cái/Bảng cân đối phát sinh. GL không sinh nghiệp vụ gốc, chỉ tổng hợp + cho phép các bút toán điều chỉnh riêng (kết chuyển, phân bổ, chênh lệch tỷ giá, ngoài bảng). |
| General Ledger → tất cả | Shared Kernel / Open Host Service | `AccountingPeriod` (khoá kỳ) do GL sở hữu nhưng được toàn bộ hệ thống tuân theo: mọi BC phải kiểm tra kỳ chưa khoá trước khi ghi/sửa/xoá chứng từ. |

**Nguyên tắc tích hợp (Integration Pattern)**: dùng **Domain Event** `PostingLineRecorded` (mỗi khi một BC nguồn tạo/sửa/xoá một dòng hạch toán ảnh hưởng số dư tài khoản) làm cơ chế tích hợp chính giữa Cash&Bank/Debt/Purchase&Sales với General Ledger — General Ledger là read-model tổng hợp (event-sourced hoặc CQRS projection), thay vì để mọi BC ghi trực tiếp vào một bảng dùng chung như hệ cũ (AT9000). Chi tiết Domain Event của từng BC được định nghĩa trong tài liệu con tương ứng; General Ledger (`03-GeneralLedger-BoundedContext.md` mục B) định nghĩa cách consume các event này để dựng `AccountLedger`/`TrialBalance`.

---

## 4. Shared Kernel — Domain Model dùng chung

Các entity/value object sau được toàn bộ 4 BC tham chiếu. Định nghĩa chi tiết chỉ ở đây; các tài liệu con không lặp lại thuộc tính, chỉ nêu invariant bổ sung riêng (nếu có).

### 4.1 `Money` (Value Object)
Đại diện một số tiền gắn với loại tiền và tỷ giá quy đổi tại thời điểm ghi nhận.

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| Amount | decimal(18,2) | Số tiền theo nguyên tệ |
| CurrencyCode | string(3) | Tham chiếu `Currency` |
| ExchangeRate | decimal(18,6) | Tỷ giá áp dụng tại ngày chứng từ |
| ConvertedAmount | decimal(18,2) | = Amount × ExchangeRate, làm tròn theo `Currency.RoundingRule`; luôn tính lại, không cho set tay để tránh lệch |

Invariant: `ConvertedAmount` phải luôn nhất quán với `Amount × ExchangeRate` (dung sai làm tròn ≤ 1 đơn vị tiền hạch toán nhỏ nhất). Value Object bất biến (immutable) — mọi thay đổi số tiền/tỷ giá tạo instance mới.

### 4.2 `Currency` (Entity, Reference Data)
Nguồn cũ: `AT1004`.

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| CurrencyCode | string(3), PK | ISO 4217 hoặc mã nội bộ |
| CurrencyName | string | |
| IsBaseCurrency | bool | Đúng 1 dòng có giá trị true trong toàn hệ thống (VND) |
| DecimalDigits | int | Số chữ số thập phân làm tròn |

### 4.3 `ExchangeRate` (Entity)
Nguồn cũ: `AF003801` (T-layer local table — không phải shared kernel thật sự trong hệ cũ, nhưng về mặt DDD nên coi là dữ liệu tham chiếu dùng chung cho mọi BC vì mọi giao dịch đa tệ đều cần).

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| CurrencyCode | FK Currency | |
| EffectiveDate | date | |
| Rate | decimal(18,6) | Tỷ giá quy đổi sang tiền hạch toán tại ngày này |

Invariant: (CurrencyCode, EffectiveDate) là khoá duy nhất. Khi tra cứu tỷ giá cho một giao dịch, lấy bản ghi có `EffectiveDate` gần nhất ≤ ngày chứng từ (nếu không có bản ghi đúng ngày).

### 4.4 `ChartOfAccounts` / `Account` (Entity)
Nguồn cũ: `AT1005`.

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| AccountCode | string, PK | |
| AccountName | string | |
| ParentAccountCode | FK Account, nullable | Hỗ trợ cây tài khoản cấp 1/2/3... |
| AccountGroup | enum | Nhóm tài khoản, dùng để giới hạn phạm vi sử dụng theo BC (vd nhóm "Chi phí/Mua-bán hàng" cho phân bổ, nhóm "Ngoài bảng" cho OffBalanceSheetEntry) |
| NormalBalanceSide | enum{Debit, Credit} | Bên số dư bình thường (thay `IsDebitBalance`) |
| RequiresPartner | bool | Bắt buộc gắn `BusinessPartner` khi hạch toán (vd TK công nợ 131/331) |
| RequiresAnalysisDimension | bool | Bắt buộc gắn `AnalysisDimension` khi hạch toán |
| IsOffBalanceSheet | bool | true nếu thuộc nhóm ngoài bảng (thay cho `GroupID='G00'`) |
| IsPostable | bool | Tài khoản lá (cho phép hạch toán trực tiếp) hay tài khoản tổng hợp (chỉ để tổng hợp báo cáo) |
| Disabled | bool | |

### 4.5 `AccountingPeriod` (Entity — sở hữu bởi General Ledger BC, dùng chung)
Nguồn cũ: `AT9999`.

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| BranchId | FK Branch | |
| Year | int | |
| Month | int | |
| StartDate / EndDate | date | |
| Status | enum{Open, Closed} | thay cho `Closing` bit |

Invariant xuyên suốt (áp dụng mọi BC): **không được tạo/sửa/xoá bất kỳ chứng từ nào có `VoucherDate` thuộc một `AccountingPeriod` có `Status = Closed`.** Đây là rule chặn cứng, không phải cảnh báo (khác với ExpenseBudget). Ngoại lệ đã quan sát trong hệ cũ: bút toán kết chuyển cuối kỳ chỉ được tạo/xoá khi kỳ đó **đang là kỳ hiện hành** (không chỉ "chưa đóng") — xem rule riêng ở `03-GeneralLedger-BoundedContext.md`.

### 4.6 `VoucherType` (Entity, Reference Data)
Nguồn cũ: `AT1007`.

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| VoucherTypeCode | string, PK | |
| VoucherTypeName | string | |
| VoucherGroup | enum | vd `PeriodEndTransfer` (thay nhóm 98/99), `Standard` |
| BoundedContext | enum | BC nào sở hữu loại chứng từ này (dùng để route khi tạo chứng từ) |
| NumberingPattern | string | Mẫu sinh số chứng từ tự động |

### 4.7 `AnalysisDimension` (thay thế mô hình Ana01-10 cứng)
Nguồn cũ: `AT1011` + 10 cột `Ana01ID..Ana10ID` trên mọi bảng giao dịch.

**Quyết định thiết kế**: hệ cũ dùng 10 cột cố định (Ana01ID..Ana10ID) trên mọi dòng giao dịch — cứng, khó mở rộng, đa số luôn NULL. Thiết kế mới thay bằng bảng liên kết linh hoạt:

`AnalysisDimensionType` (Entity — định nghĩa loại chiều phân tích, vd "Mã dự án", "Bộ phận", "Sản phẩm"):

| Thuộc tính | Kiểu |
|---|---|
| DimensionTypeCode | string, PK |
| DimensionTypeName | string |

`AnalysisTag` (Entity — giá trị cụ thể của một loại chiều):

| Thuộc tính | Kiểu |
|---|---|
| DimensionTypeCode | FK AnalysisDimensionType |
| TagCode | string |
| TagName | string |

`PostingLineAnalysisTag` (bảng liên kết N-N giữa 1 dòng hạch toán và N `AnalysisTag`) thay cho 10 cột cứng — mỗi BC khi định nghĩa `PostingLine` của mình tham chiếu tập hợp này thay vì Ana01..Ana10.

### 4.8 `Employee` (Entity, Reference Data)
Nguồn cũ: `AT1103`. Thuộc tính tối thiểu: `EmployeeCode`, `EmployeeName`, `Disabled`.

### 4.9 `Branch` / `Division` (Entity, Reference Data)
Nguồn cũ: `DivisionID` xuất hiện trên mọi bảng (`AT1101` — chưa khảo sát chi tiết, suy ra từ cách dùng). Thuộc tính tối thiểu: `BranchCode`, `BranchName`, `Disabled`.

### 4.10 `BusinessPartner` (Entity — sở hữu chính bởi Debt BC, dùng chung Cash&Bank + Purchase&Sales)
Nguồn cũ: `AT1202`. Định nghĩa đầy đủ (bao gồm invariant hạn mức tín dụng) ở `02-ARAP-BoundedContext.md` mục B — ở đây chỉ liệt kê để Cash&Bank/Purchase&Sales tham chiếu:

| Thuộc tính | Kiểu | Ghi chú |
|---|---|---|
| PartnerCode | string, PK | |
| PartnerName | string | |
| IsCustomer | bool | |
| IsSupplier | bool | Một partner có thể vừa là KH vừa là NCC (điều kiện tiên quyết cho `DebtNetting`) |
| DefaultReceivableAccount | FK Account | |
| DefaultPayableAccount | FK Account | |
| ReceivableCreditLimit | Money, nullable | |
| PayableCreditLimit | Money, nullable | |
| ReceivablePaymentTermDays | int | |
| PayablePaymentTermDays | int | |

### 4.11 `Item` / `Product` (Entity — sở hữu bởi Purchase&Sales BC, tham chiếu bởi Cash&Bank khi cần)
Nguồn cũ: `AT1302`. Định nghĩa đầy đủ ở `04-PurchaseSales-BoundedContext.md`.

### 4.12 Nguyên tắc thiết kế xuyên suốt (áp dụng mọi BC)

1. **Đa tiền tệ**: mọi Aggregate ghi nhận số tiền phải dùng Value Object `Money` (nguyên tệ + tỷ giá + tiền quy đổi), không lưu số tiền trần.
2. **Đa chi nhánh**: mọi Aggregate Root phải có `BranchId`; không có nghiệp vụ liên chi nhánh ngầm định — chuyển tiền/hàng giữa 2 chi nhánh phải là 2 giao dịch riêng (ngoài phạm vi 4 BC này nếu chưa quan sát thấy trong khảo sát).
3. **Khoá kỳ kế toán**: mọi command tạo/sửa/xoá chứng từ phải kiểm tra `AccountingPeriod.Status` trước khi thực thi — implement như một Domain Service dùng chung `PeriodLockGuard`, không lặp lại logic ở từng BC.
4. **Phân tích đa chiều**: dùng `AnalysisTag` linh hoạt (mục 4.7), không giới hạn 10 chiều cứng.
5. **Đánh số chứng từ**: sinh tự động theo `VoucherType.NumberingPattern`, duy nhất theo (BranchId, VoucherTypeCode, Period); cho phép nhập tay có kiểm tra trùng (rule quan sát được ở Purchase&Sales — "check trùng số chứng từ" — áp dụng chung).
6. **Không dùng bảng hạch toán vạn năng**: khác với hệ cũ (AT9000 dùng chung cho mọi loại chứng từ), mỗi BC sở hữu Aggregate/bảng riêng theo đúng nghiệp vụ; điểm chung duy nhất là mọi Aggregate khi hoàn tất đều publish Domain Event chuẩn hoá `PostingLineRecorded` để GL tổng hợp.
   **Định dạng `PostingLineRecorded` là theo TỪNG DÒNG bút toán đơn (1 tài khoản + 1 phía Nợ/Có), không phải theo cặp**: `PostingLineRecorded{BranchId, VoucherDate, AccountCode, Side(Debit|Credit), Money, Partner?, AnalysisTags[], SourceBoundedContext, SourceVoucherType, SourceVoucherId}` — nhất quán với `JournalEntryLine` (xem `03-GeneralLedger-BoundedContext.md` mục B.1). Một nghiệp vụ hạch toán kép thông thường (vd phiếu thu, hoá đơn bán hàng) publish **đúng 2** sự kiện này (1 Nợ + 1 Có, cùng `Money`); một bút toán ngoài bảng (`OffBalanceSheetEntry`, hạch toán đơn) chỉ publish **1** sự kiện. Cách thiết kế theo-dòng này (thay vì theo-cặp) cho phép GL tổng hợp đồng nhất mọi nguồn mà không cần biết cấu trúc nội bộ của BC nguồn.

---

## 5. Bảng tra cứu ngược (Legacy Mapping) — phục vụ di trú dữ liệu

| Bảng/thực thể hệ cũ | Bounded Context mới | Aggregate/Entity tương ứng |
|---|---|---|
| AT9000, AT9010 (mọi form Cash/Bank) | Cash & Bank | `CashVoucher`, `BankVoucher`, `TempAdvanceVoucher` |
| AT9000 (mọi form P&S) | Purchase & Sales | `PurchaseInvoice`, `SalesInvoice`, `ReturnInvoice` |
| AT0301/AT0302/AT0303 | Debt (AR/AP) | `OpenDebitItem`, `OpenCreditItem`, `DebtMatch` |
| AT0600/AT0601 | Debt (AR/AP) | `DebtNetting` |
| AT1018/AT1019/AT3131 | Debt (AR/AP) | `InterestPolicy`, `InterestBracket`, `InterestCalculationBatch` |
| AT9000 (bút toán thủ công/kết chuyển/mở đầu kỳ), AT9001, AT9004, AT9005 | General Ledger | `ManualJournalEntry`, `PeriodEndTransferEntry`, `AllocationJob`, `OffBalanceSheetEntry` |
| AT1703/AT1704/AT1705, AT7801, AT1610 | General Ledger | `AllocationJob`, `AllocationSchedule`, `AllocationSplitTable`, `MultiLevelAllocationChain` |
| AT9999 | General Ledger (Shared) | `AccountingPeriod` |
| AT3101 | Purchase & Sales | `DiscountVoucher` |
| AT1326 | Purchase & Sales | `KitFormula` |
| AT1005, AT1004, AT1007, AT1011, AT1103, AT1202, AT1302, AT1009 | Shared Kernel | mục 4 ở trên |

---

## 6. Quy ước dùng trong tài liệu 01–04

- **Mã hoá rule**: mỗi business rule được đánh số `[BC-R##]` (vd `[CB-R01]`) và ghi rõ nguồn code cũ (file + hàm/dòng) để truy vết khi có tranh chấp nghiệp vụ.
- **Domain Event**: đặt tên dạng quá khứ (`CashReceiptPosted`, `DebtCleared`), publish sau khi Aggregate transaction thành công.
- **Gherkin**: mỗi feature file map 1-1 với một use case ở mục A của tài liệu con; tên scenario nêu rõ rule liên quan bằng mã `[BC-R##]` trong comment để truy vết 2 chiều sang mục TDD.
- **TDD**: test case đặt tên `Should_<expected>_When_<condition>`; mỗi Acceptance Test trích dẫn scenario Gherkin tương ứng.
