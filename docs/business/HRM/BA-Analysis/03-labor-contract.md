# 03 — Labor Contract — Hợp đồng lao động & Ký số điện tử

> Bounded Context: **Labor Contract**. Tham chiếu `00-domain-overview.md` để biết ubiquitous language chung, context map, quy ước đặt tên DB/code, và Shared Kernel Approval Engine (mô hình hoá đầy đủ tại `05-request-approval.md` — tài liệu này chỉ tham chiếu, không định nghĩa lại).

## Phần 1 — DDD

### 1.1 Bounded Context

**Labor Contract** quản lý vòng đời hợp đồng lao động của nhân viên: khởi tạo, xác định loại/thời hạn, in ấn, và đặc biệt là luồng **ký số điện tử (e-signature)** tích hợp nền tảng bên thứ 3 (kiểu FPT eSign). Context này là **downstream** của `Employee Profile` (01) và `Recruitment` (02, qua kết quả thử việc), và là **upstream** của `Payroll` (06) (hệ số/mức lương trên hợp đồng làm cơ sở tính lương).

Khác với thiết kế cũ (nhúng tối đa 3 chu kỳ hợp đồng trực tiếp trên bản ghi nhân viên: `LaborContractDate`, `LaborContractStart1-3/End1-3`, `StatusContract`, `WorkingStatus`), thiết kế mới **tách hẳn hợp đồng thành aggregate độc lập**, quan hệ 1-N với `Employee`, không giới hạn số chu kỳ hợp đồng theo thời gian.

### 1.2 Ubiquitous Language (bổ sung riêng cho context này)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Số hợp đồng | Contract No | `GetContractNo` (HRMF2180BL) | Mã hợp đồng tự sinh, duy nhất |
| Loại hợp đồng | Contract Type | `ContractTypeID` | Thử việc / xác định thời hạn (1 năm, 3 năm) / không xác định thời hạn |
| Phong bì ký số | Signature Envelope | `EContractResponse.envelopeId` | Gói tài liệu gửi cho các bên ký qua nền tảng thứ 3 |
| Bên ký | Signature Party | `PartieRespone/RecipientResponse` | Người lao động hoặc người sử dụng lao động tham gia ký |
| Trạng thái nhận (ký) | Signature Status | `HT1360StatusRecieve` | Trạng thái ký của hợp đồng/phong bì, đồng bộ lookup `HT0099` (`CodeMaster='StatusRecieve'`) |
| Trường động văn bản | Document Field | `HeaderResponse/HeaderField` | Field động chèn vào template hợp đồng (họ tên, ngày sinh, mức lương...) |
| Mẫu hợp đồng | Contract Template | *(in template hợp đồng)* | Mẫu văn bản dùng để in/sinh hợp đồng |
| Hợp đồng hiện hành | Current Contract | *(suy ra từ StatusContract/WorkingStatus cũ)* | Hợp đồng đang có hiệu lực áp dụng cho nhân viên tại thời điểm hiện tại |
| Kết quả thử việc | Probation Result | `HRMF2200/2201` (xem `02-recruitment.md`) | Điều kiện tiên quyết thường gặp trước khi ký hợp đồng chính thức |

### 1.3 Aggregate / Entity / Value Object

#### 1.3.1 Aggregate Root: `Contract`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên sở hữu hợp đồng (tham chiếu `Employee` — 01-employee-profile.md) |
| ContractNo | string(50) | Có | Số hợp đồng, tự sinh, duy nhất toàn hệ thống (mã cũ: `GetContractNo`, HRMF2180BL) |
| ContractTypeId | Guid (FK) | Có | Loại hợp đồng, tham chiếu `ContractType` |
| ContractStatus | enum `ContractStatus` | Có | Trạng thái ký/hiệu lực hợp đồng — xem 1.3.4 (mã cũ: `HT1360StatusRecieve`) |
| SignDate | DateOnly? | Không | Ngày ký thực tế (khi cả hai bên đã hoàn tất ký) |
| EffectiveDate | DateOnly | Có | Ngày hợp đồng có hiệu lực |
| ExpiryDate | DateOnly? | Không | Ngày hết hạn hợp đồng; `NULL` nếu `ContractType.IsIndefiniteTerm = true` |
| DurationMonths | int? | Không | Số tháng hợp đồng, suy ra tự động từ `ContractTypeId` (mã cũ: `GetMonthsByContractTypeID`, HRMF2180BL) |
| JobTitle | string(200) | Có | Chức danh công việc ghi trên hợp đồng |
| WorkplaceAddress | string(500) | Không | Địa điểm làm việc |
| BaseSalaryAmount | decimal(18,2) | Không | Mức lương cơ bản ghi trên hợp đồng (đầu vào tham khảo cho Payroll) |
| ProbationResultId | Guid? | Không | Tham chiếu lỏng (không FK cứng, khác bounded context) tới `ProbationResult` (02-recruitment.md) |
| ContractTemplateId | Guid? (FK) | Không | Mẫu hợp đồng dùng để in/sinh văn bản |
| CurrentEnvelopeId | Guid? (FK) | Không | Phong bì ký số đang hiệu lực (tham chiếu `SignatureEnvelope`) |
| IsCurrentContract | bool | Có | Đánh dấu đây là hợp đồng hiện hành của nhân viên (chỉ 1 bản ghi `true` tại một thời điểm cho mỗi `EmployeeId`) |
| Note | string(1000) | Không | Ghi chú |
| IsDeleted | bool | Có | Soft delete |
| DeletedAtUtc | DateTime? | Không | Thời điểm xoá mềm |
| CreatedAtUtc / CreatedBy | DateTime / string | Có | Audit tạo |
| LastModifiedAtUtc / LastModifiedBy | DateTime? / string | Không | Audit sửa |

#### 1.3.2 Entity: `ContractType` (lookup có cấu hình — Configuration/Policy)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Code | string(20) | Có | Mã loại hợp đồng, duy nhất |
| Name | string(200) | Có | Tên loại (VD: "Thử việc", "Xác định thời hạn 12 tháng", "Không xác định thời hạn") |
| DurationMonths | int? | Không | Số tháng mặc định áp dụng khi tạo `Contract` thuộc loại này; `NULL` nếu `IsIndefiniteTerm = true` |
| IsProbationType | bool | Có | Đánh dấu loại hợp đồng thử việc |
| IsIndefiniteTerm | bool | Có | Đánh dấu loại không xác định thời hạn |
| DisplayOrder | int | Không | Thứ tự hiển thị |
| IsActive | bool | Có | Còn sử dụng hay đã ngưng cấu hình |
| IsDeleted / DeletedAtUtc | bool / DateTime? | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.3 Entity: `ContractTemplate`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Name | string(200) | Có | Tên mẫu hợp đồng |
| Description | string(500) | Không | Mô tả |
| FileTemplatePath | string(500) | Có | Đường dẫn/định danh file mẫu dùng khi in |
| IsActive | bool | Có | Còn sử dụng |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.4 Value Object: `ContractStatus` (enum)

Suy ra trực tiếp từ bộ mã trạng thái thực tế `HT1360StatusRecieve` (đồng bộ lookup `HT0099`, `CodeMaster='StatusRecieve'`), giữ nguyên đúng 6 giá trị:

| Giá trị enum | Mã cũ | Ý nghĩa |
|---|---|---|
| `Unsigned` | `"0"` | Chưa ký — hợp đồng đã tạo nhưng chưa gửi/chưa có bên nào ký |
| `Signed` | `"1"` | Đã ký — tất cả các bên đã hoàn tất ký |
| `Ended` | `"2"` | Đã kết thúc/hết hiệu lực (sau khi đã ký, hết hạn thực hiện hoặc chấm dứt hợp đồng) |
| `Paused` | `"3"` | Tạm dừng (VD: tạm hoãn thực hiện hợp đồng) |
| `Expired` | `"4"` | Hết hạn — phong bì ký (envelope) quá hạn ký (`DueInDays`) mà chưa hoàn tất |
| `Processing` | `"99"` | Đang xử lý — người lao động đã ký, người sử dụng lao động chưa ký |

#### 1.3.5 Entity: `SignatureEnvelope` (con của `Contract`, 1 Contract - N Envelope theo lịch sử gửi ký)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| ContractId | Guid (FK) | Có | Hợp đồng liên quan |
| ExternalEnvelopeId | string(100) | Có | Mã phong bì phía nền tảng ký số bên thứ 3 (mã cũ: `EContractResponse.envelopeId`) |
| Provider | string(50) | Có | Tên nhà cung cấp dịch vụ ký số (VD: `FptEsign`) |
| SignUrl | string(1000) | Không | URL ký (mã cũ: `EContractResponse.signUrl`) |
| IframeUrl | string(1000) | Không | URL nhúng iframe ký (mã cũ: `EContractResponse.iframeUrl`) |
| DueInDays | int? | Không | Số ngày hiệu lực để hoàn tất ký (mã cũ: `FileResponse.dueInDays`) |
| FlowFinish | bool | Có | Cờ luồng ký đã hoàn tất phía nhà cung cấp (mã cũ: `FileResponse.flowFinish`) |
| EnvelopeStatus | enum `ContractStatus` | Có | Trạng thái ký của riêng phong bì này (đồng bộ vào `Contract.ContractStatus` khi là `CurrentEnvelopeId`) |
| RequestedAtUtc | DateTime | Có | Thời điểm gửi yêu cầu ký |
| CompletedAtUtc | DateTime? | Không | Thời điểm hoàn tất ký (tất cả các bên) |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.6 Entity: `SignatureParty` (con của `SignatureEnvelope`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EnvelopeId | Guid (FK) | Có | Phong bì ký chứa bên ký này |
| PartyRole | enum (`Employee`, `Employer`) | Có | Vai trò bên ký (mã cũ: model từ `PartieRespone/RecipientResponse`) |
| RecipientName | string(200) | Có | Tên người ký (mã cũ: `ContactModel`) |
| RecipientEmail | string(200) | Không | Email người ký |
| RecipientPhone | string(20) | Không | SĐT người ký |
| SignStatus | enum `ContractStatus` | Có | Trạng thái ký của riêng bên này (`Unsigned`/`Signed`) |
| SignedAtUtc | DateTime? | Không | Thời điểm bên này ký xong |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.7 Value Object: `ContractDocumentField` (field động trên văn bản)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| ContractId | Guid (FK) | Có | Hợp đồng chứa field |
| FieldKey | string(100) | Có | Khoá field động (mã cũ: `HeaderField`) |
| FieldValue | string(1000) | Không | Giá trị field |
| DisplayOrder | int | Không | Thứ tự hiển thị trên văn bản |

#### 1.3.8 Entity: `SignatureProviderConfig` (Configuration/Policy Aggregate — theo ghi chú Data Gaps ở 00)

Lưu cấu hình tích hợp OAuth với nền tảng ký số bên thứ 3 (mã cũ: `LoginReponse`/OAuth token), không hard-code trong code.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| ProviderName | string(50) | Có | Tên nhà cung cấp |
| ClientId | string(200) | Có | OAuth client id |
| ClientSecretEncrypted | string(500) | Có | OAuth client secret (mã hoá khi lưu) |
| TokenEndpointUrl | string(500) | Có | Endpoint lấy access token |
| ApiBaseUrl | string(500) | Có | Base URL gọi API bên thứ 3 |
| IsActive | bool | Có | Cấu hình đang dùng |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

### 1.4 Domain Events

| Event | Phát sinh khi | Hệ quả |
|---|---|---|
| `ContractCreated` | Tạo mới `Contract` (đã có `ContractNo` tự sinh) | Có thể trigger workflow duyệt nội bộ trước khi gửi ký (qua Approval Engine, 05) |
| `ContractSubmittedForSigning` | Gửi hợp đồng cho nền tảng ký số, tạo `SignatureEnvelope` | Đồng bộ `ContractStatus = Unsigned`, lưu `SignUrl/IframeUrl` |
| `ContractPartySigned` | Một `SignatureParty` hoàn tất ký (webhook/callback từ bên thứ 3) | Nếu còn bên chưa ký → `ContractStatus = Processing`; nếu là bên cuối → phát `ContractFullySigned` |
| `ContractFullySigned` | Tất cả `SignatureParty` đã ký | `ContractStatus = Signed`, khoá chỉnh sửa nội dung hợp đồng (BR-03) |
| `ContractSigningExpired` | Quá `DueInDays` mà chưa hoàn tất ký | `ContractStatus = Expired` |
| `ContractPaused` | Nghiệp vụ tạm dừng thực hiện hợp đồng | `ContractStatus = Paused` |
| `ContractEnded` | Hợp đồng đã ký hết hiệu lực (`ExpiryDate` qua) hoặc chấm dứt theo quyết định nhân sự (11) | `ContractStatus = Ended` |
| `CurrentContractChanged` | Hợp đồng mới được đánh dấu `IsCurrentContract = true` | Hợp đồng hiện hành cũ (nếu có) được set `IsCurrentContract = false`; `Employee.CurrentContractId` cập nhật |
| `ContractPartyMesSyncRequested` | `ContractStatus` hoặc trạng thái làm việc `Employee` đổi | Integration event đồng bộ user MES (mã cũ: `UpdateIsUserMES`, HRMF2210BL) — không model chi tiết trong context này |

### 1.5 Business Rules & Invariants

- **BR-01**: `ContractNo` phải được hệ thống tự sinh, duy nhất toàn hệ thống, theo quy tắc tuần tự (mã cũ: `GetContractNo`, HRMF2180BL). Không cho nhập tay trùng số.
- **BR-02**: `DurationMonths` được suy ra tự động từ `ContractTypeId` (mã cũ: `GetMonthsByContractTypeID`, HRMF2180BL); `ExpiryDate = EffectiveDate + DurationMonths` tháng, ngoại trừ khi `ContractType.IsIndefiniteTerm = true` thì `ExpiryDate = NULL`.
- **BR-03**: Một khi `Contract.ContractStatus = Signed`, **không được sửa** các trường nội dung pháp lý của hợp đồng (`JobTitle`, `EffectiveDate`, `ExpiryDate`, `BaseSalaryAmount`, `ContractDocumentField`...) — invariant cứng. Mọi thay đổi sau ký phải thực hiện qua hợp đồng mới/phụ lục (aggregate `Contract` mới), không amend tại chỗ.
- **BR-04**: Hợp đồng chính thức (loại `ContractType.IsProbationType = false`) chỉ nên được khởi tạo khi có `ProbationResultId` tham chiếu tới một `ProbationResult` (02-recruitment.md) với kết quả Đạt; nếu thiếu, hệ thống phải cảnh báo (không nhất thiết chặn cứng, vì có thể có trường hợp tuyển thẳng không qua thử việc).
- **BR-05**: Một `Employee` có thể có nhiều `Contract` (1-N) không giới hạn theo thời gian; tại một thời điểm chỉ duy nhất một `Contract` của cùng `EmployeeId` được đánh dấu `IsCurrentContract = true`.
- **BR-06**: `ContractStatus` tuân theo state machine cố định 6 trạng thái. Chỉ các chuyển đổi sau được phép:
  - `Unsigned → Processing` (một bên ký, thường là người lao động ký trước)
  - `Unsigned → Signed` (trường hợp ký đồng thời/ký một lần cho tất cả các bên)
  - `Processing → Signed` (bên còn lại — người sử dụng lao động — ký xong)
  - `Unsigned → Expired`, `Processing → Expired` (quá `DueInDays` mà chưa hoàn tất ký)
  - `Signed → Ended` (hết hiệu lực/chấm dứt sau khi đã ký)
  - `{Unsigned, Processing, Signed} → Paused` và `Paused → {Unsigned, Processing, Signed}` (tạm dừng/khôi phục)
  Mọi chuyển đổi khác (VD: `Ended → Signed`) bị từ chối.
- **BR-07**: Trạng thái `Processing` chỉ hợp lệ khi đúng một `SignatureParty` có `SignStatus = Signed` và ít nhất một `SignatureParty` còn `Unsigned` trong cùng `SignatureEnvelope`.
- **BR-08**: `Expired` áp dụng cho `SignatureEnvelope` chưa hoàn tất ký khi quá hạn (`DueInDays`); khác với `Ended` dùng cho hợp đồng **đã ký xong** (`Signed`) nhưng đã hết hiệu lực theo `ExpiryDate` hoặc bị chấm dứt bởi quyết định nhân sự. Hai trạng thái không được dùng lẫn cho nhau.
- **BR-09**: Khi `ContractStatus` hoặc trạng thái làm việc của `Employee` thay đổi, hệ thống phát sinh integration event đồng bộ user MES (mã cũ: `UpdateIsUserMES`, HRMF2210BL); chi tiết đồng bộ không thuộc phạm vi domain model của context này.

### 1.6 Đề xuất DB Schema (prefix `Con_`)

```
Con_ContractType (
    Id                  PK
    Code                UNIQUE
    Name
    DurationMonths      NULL
    IsProbationType
    IsIndefiniteTerm
    DisplayOrder
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Con_ContractTemplate (
    Id                  PK
    Name
    Description
    FileTemplatePath
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Con_Contract (
    Id                  PK
    EmployeeId          FK -> Emp_Employee.Id           (INDEX IX_Con_Contract_EmployeeId)
    ContractNo          UNIQUE                            (INDEX UX_Con_Contract_ContractNo)
    ContractTypeId      FK -> Con_ContractType.Id
    ContractStatus      (enum, INDEX IX_Con_Contract_ContractStatus)
    SignDate            NULL
    EffectiveDate
    ExpiryDate          NULL
    DurationMonths      NULL
    JobTitle
    WorkplaceAddress    NULL
    BaseSalaryAmount    NULL
    ProbationResultId   NULL   -- tham chiếu lỏng sang Recruitment (02), không FK cứng khác bounded context
    ContractTemplateId  FK -> Con_ContractTemplate.Id     NULL
    CurrentEnvelopeId   FK -> Con_SignatureEnvelope.Id    NULL
    IsCurrentContract                                     (INDEX IX_Con_Contract_EmployeeId_IsCurrentContract UNIQUE FILTERED WHERE IsCurrentContract = 1)
    Note                NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Con_ContractDocumentField (
    Id                  PK
    ContractId          FK -> Con_Contract.Id             (INDEX IX_Con_ContractDocumentField_ContractId)
    FieldKey
    FieldValue          NULL
    DisplayOrder        NULL
)

Con_SignatureEnvelope (
    Id                  PK
    ContractId          FK -> Con_Contract.Id             (INDEX IX_Con_SignatureEnvelope_ContractId)
    ExternalEnvelopeId  UNIQUE                             (INDEX UX_Con_SignatureEnvelope_ExternalEnvelopeId)
    Provider
    SignUrl             NULL
    IframeUrl           NULL
    DueInDays           NULL
    FlowFinish
    EnvelopeStatus      (enum)
    RequestedAtUtc
    CompletedAtUtc      NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Con_SignatureParty (
    Id                  PK
    EnvelopeId          FK -> Con_SignatureEnvelope.Id    (INDEX IX_Con_SignatureParty_EnvelopeId)
    PartyRole           (enum: Employee, Employer)
    RecipientName
    RecipientEmail      NULL
    RecipientPhone      NULL
    SignStatus          (enum)
    SignedAtUtc         NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Con_SignatureProviderConfig (
    Id                  PK
    ProviderName        UNIQUE
    ClientId
    ClientSecretEncrypted
    TokenEndpointUrl
    ApiBaseUrl
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)
```

> **Thay đổi liên quan tới `Employee Profile` (01)**: đề xuất bổ sung cột `CurrentContractId (FK -> Con_Contract.Id, NULL)` trên bảng `Emp_Employee`, đồng bộ hai chiều với `Con_Contract.IsCurrentContract` qua domain event `CurrentContractChanged`. Đây thay thế hoàn toàn thiết kế cũ nhúng `LaborContractDate/LaborContractStart1-3/End1-3/StatusContract/WorkingStatus` trực tiếp trên bản ghi nhân viên.

### 1.7 Quan hệ với bounded context khác

| Context | Chiều quan hệ | Nội dung |
|---|---|---|
| `Employee Profile` (01) | Contract N-1 Employee | Mỗi `Contract` thuộc về một `Employee`; `Employee.CurrentContractId` trỏ tới hợp đồng hiện hành |
| `Recruitment` (02) | Tham chiếu lỏng | `Contract.ProbationResultId` liên kết tới `ProbationResult` — hợp đồng chính thức thường ký sau khi có kết quả thử việc đạt (BR-04) |
| `Request & Approval` (05) | Có thể dùng Shared Kernel | Quy trình duyệt nội bộ trước khi gửi hợp đồng cho ký số (nếu doanh nghiệp yêu cầu) sử dụng Approval Engine chung — không định nghĩa lại tại đây |
| `Payroll` (06) | Upstream → Downstream | `Contract.BaseSalaryAmount`/hệ số lương là dữ liệu đầu vào tham khảo cho công thức lương |
| `Personnel Decisions` (11) | Sự kiện làm thay đổi trạng thái | Quyết định thôi việc/chấm dứt hợp đồng làm phát sinh `ContractEnded` |
| Hệ thống ngoài: nền tảng ký số bên thứ 3 (FPT eSign-like) | External System | `SignatureEnvelope`/`SignatureParty` phản ánh dữ liệu đồng bộ 2 chiều qua API/webhook của nhà cung cấp |
| Hệ thống ngoài: MES | External System (integration event) | Đồng bộ trạng thái user MES khi hợp đồng/trạng thái làm việc thay đổi (BR-09), không model chi tiết |

## Phần 2 — BDD

### User Stories

- **US-01**: Là nhân viên nhân sự, tôi muốn hệ thống tự sinh số hợp đồng khi tạo mới, để tránh trùng lặp và sai sót thủ công.
- **US-02**: Là nhân viên nhân sự, tôi muốn hệ thống tự suy ra số tháng và ngày hết hạn hợp đồng theo loại hợp đồng đã chọn.
- **US-03**: Là nhân viên nhân sự, tôi muốn gửi hợp đồng cho các bên ký số qua nền tảng bên thứ 3 và theo dõi trạng thái ký theo thời gian thực.
- **US-04**: Là hệ thống, tôi cần ngăn chỉnh sửa nội dung hợp đồng đã ký để đảm bảo giá trị pháp lý của văn bản.
- **US-05**: Là nhân viên nhân sự, tôi muốn được cảnh báo khi tạo hợp đồng chính thức cho nhân viên chưa có kết quả thử việc đạt.
- **US-06**: Là nhân viên nhân sự, tôi muốn quản lý nhiều hợp đồng theo thời gian cho một nhân viên và luôn xác định đúng hợp đồng hiện hành.
- **US-07**: Là hệ thống, tôi cần phân biệt đúng trạng thái `Expired` (hết hạn ký) và `Ended` (hết hiệu lực hợp đồng) để không nhầm lẫn báo cáo.

### Feature: Tạo mới hợp đồng lao động

```gherkin
Feature: Tạo mới hợp đồng lao động
  Là nhân viên nhân sự, tôi muốn tạo hợp đồng lao động với số hợp đồng và thời hạn được tự động tính đúng

  Scenario: Tự sinh số hợp đồng duy nhất khi tạo mới (BR-01)
    Given nhân viên "Nguyễn Văn A" chưa có hợp đồng nào trong hệ thống
    When nhân viên nhân sự tạo mới một "Contract" cho "Nguyễn Văn A"
    Then hệ thống tự sinh "ContractNo" duy nhất, không trùng với bất kỳ hợp đồng nào đã tồn tại
    And "ContractNo" được lưu lại trên bản ghi hợp đồng vừa tạo

  Scenario: Tự suy ra số tháng và ngày hết hạn theo loại hợp đồng xác định thời hạn (BR-02)
    Given loại hợp đồng "Xác định thời hạn 12 tháng" có "DurationMonths" = 12
    And ngày hiệu lực hợp đồng "EffectiveDate" là "2026-08-01"
    When nhân viên nhân sự tạo "Contract" với loại hợp đồng trên
    Then "Contract.DurationMonths" được gán tự động là 12
    And "Contract.ExpiryDate" được tính tự động là "2027-07-31"

  Scenario: Hợp đồng không xác định thời hạn không có ngày hết hạn (BR-02)
    Given loại hợp đồng "Không xác định thời hạn" có "IsIndefiniteTerm" = true
    When nhân viên nhân sự tạo "Contract" với loại hợp đồng trên
    Then "Contract.DurationMonths" là null
    And "Contract.ExpiryDate" là null
```

### Feature: Bảo vệ hợp đồng đã ký

```gherkin
Feature: Bảo vệ hợp đồng đã ký
  Là hệ thống, tôi cần đảm bảo hợp đồng đã ký không bị chỉnh sửa nội dung pháp lý

  Scenario: Từ chối sửa hợp đồng khi trạng thái là Signed (BR-03)
    Given hợp đồng "HD-2026-0012" đang có "ContractStatus" = "Signed"
    When nhân viên nhân sự cố gắng sửa "JobTitle" của hợp đồng "HD-2026-0012"
    Then hệ thống từ chối thao tác và trả về lỗi nghiệp vụ "Contract already signed, cannot be edited"
    And nội dung hợp đồng "HD-2026-0012" không thay đổi

  Scenario: Cho phép sửa hợp đồng khi trạng thái còn Unsigned (BR-03)
    Given hợp đồng "HD-2026-0013" đang có "ContractStatus" = "Unsigned"
    When nhân viên nhân sự sửa "JobTitle" của hợp đồng "HD-2026-0013"
    Then hệ thống chấp nhận thay đổi và cập nhật nội dung hợp đồng
```

### Feature: Liên kết thử việc — hợp đồng chính thức

```gherkin
Feature: Liên kết kết quả thử việc với hợp đồng chính thức
  Là nhân viên nhân sự, tôi muốn hệ thống cảnh báo khi tạo hợp đồng chính thức thiếu kết quả thử việc đạt

  Scenario: Cảnh báo khi tạo hợp đồng chính thức nhưng chưa có kết quả thử việc đạt (BR-04)
    Given nhân viên "Trần Thị B" không có "ProbationResult" nào với kết quả Đạt
    When nhân viên nhân sự tạo "Contract" loại "Xác định thời hạn" (không phải loại thử việc) cho "Trần Thị B"
    Then hệ thống hiển thị cảnh báo "Missing passed probation result before formal contract"
    And vẫn cho phép nhân viên nhân sự xác nhận tạo hợp đồng nếu chủ động bỏ qua cảnh báo

  Scenario: Không cảnh báo khi đã có kết quả thử việc đạt (BR-04)
    Given nhân viên "Trần Thị B" có "ProbationResult" với kết quả Đạt
    When nhân viên nhân sự tạo "Contract" loại "Xác định thời hạn" cho "Trần Thị B" và tham chiếu "ProbationResultId" tương ứng
    Then hệ thống không hiển thị cảnh báo
    And hợp đồng được tạo thành công
```

### Feature: Quản lý nhiều hợp đồng theo thời gian

```gherkin
Feature: Xác định hợp đồng hiện hành
  Là hệ thống, tôi cần luôn xác định đúng một hợp đồng hiện hành cho mỗi nhân viên

  Scenario: Chỉ có một hợp đồng hiện hành tại một thời điểm (BR-05)
    Given nhân viên "Lê Văn C" đang có hợp đồng "HD-2025-0090" là hợp đồng hiện hành ("IsCurrentContract" = true)
    When nhân viên nhân sự tạo hợp đồng mới "HD-2026-0101" cho "Lê Văn C" và đánh dấu là hợp đồng hiện hành
    Then hợp đồng "HD-2025-0090" tự động chuyển "IsCurrentContract" thành false
    And hợp đồng "HD-2026-0101" có "IsCurrentContract" = true
    And "Employee.CurrentContractId" của "Lê Văn C" trỏ tới "HD-2026-0101"
```

### Feature: Ký số điện tử — state machine

```gherkin
Feature: Trạng thái ký số điện tử của hợp đồng
  Là hệ thống, tôi cần phản ánh đúng trạng thái ký thực tế từ nền tảng ký số bên thứ 3

  Scenario: Chuyển sang Processing khi người lao động đã ký nhưng người sử dụng lao động chưa ký (BR-06, BR-07)
    Given hợp đồng "HD-2026-0201" đã gửi phong bì ký với "ContractStatus" = "Unsigned"
    And phong bì có hai bên ký: "Employee" và "Employer"
    When bên "Employee" hoàn tất ký qua nền tảng ký số bên thứ 3
    Then "SignatureParty" của "Employee" có "SignStatus" = "Signed"
    And "Contract.ContractStatus" chuyển thành "Processing"

  Scenario: Chuyển sang Signed khi cả hai bên đã ký (BR-06)
    Given hợp đồng "HD-2026-0201" đang ở trạng thái "Processing" (bên "Employee" đã ký, bên "Employer" chưa ký)
    When bên "Employer" hoàn tất ký qua nền tảng ký số bên thứ 3
    Then "Contract.ContractStatus" chuyển thành "Signed"
    And "Contract.SignDate" được ghi nhận là thời điểm bên ký cuối cùng hoàn tất

  Scenario: Chuyển sang Expired khi quá hạn ký (BR-06, BR-08)
    Given hợp đồng "HD-2026-0202" có phong bì ký với "DueInDays" = 7 và đang ở trạng thái "Unsigned"
    When quá 7 ngày mà chưa có bên nào hoàn tất ký toàn bộ phong bì
    Then "Contract.ContractStatus" chuyển thành "Expired"

  Scenario: Ended chỉ áp dụng cho hợp đồng đã ký xong và hết hiệu lực (BR-08)
    Given hợp đồng "HD-2025-0050" đang ở trạng thái "Signed" và có "ExpiryDate" là "2026-07-01"
    When ngày hệ thống vượt qua "ExpiryDate" của hợp đồng
    Then "Contract.ContractStatus" chuyển thành "Ended"
    And hệ thống không cho phép gán trực tiếp "ContractStatus" = "Ended" cho hợp đồng đang ở trạng thái "Unsigned" hoặc "Processing"

  Scenario Outline: Chỉ cho phép các chuyển đổi trạng thái hợp lệ (BR-06)
    Given hợp đồng đang ở trạng thái "<TrangThaiHienTai>"
    When hệ thống nhận yêu cầu chuyển sang trạng thái "<TrangThaiMoi>"
    Then kết quả chuyển đổi là "<KetQua>"

    Examples:
      | TrangThaiHienTai | TrangThaiMoi | KetQua      |
      | Unsigned         | Processing   | Chấp nhận   |
      | Processing        | Signed      | Chấp nhận   |
      | Unsigned         | Expired      | Chấp nhận   |
      | Signed           | Ended        | Chấp nhận   |
      | Signed           | Paused       | Chấp nhận   |
      | Ended            | Signed       | Từ chối     |
      | Expired          | Processing   | Từ chối     |
```

## Phần 3 — TDD

### 3.1 Bảng Test Case

| TC-xx | Scenario ánh xạ | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Tự sinh số hợp đồng duy nhất (BR-01) | Unit | Tạo `Contract` mới, không truyền `ContractNo` | `ContractNo` khác null, khác rỗng, không trùng bản ghi hiện có |
| TC-02 | `ContractNo` không trùng khi tạo đồng thời (BR-01) | Integration | 2 request tạo `Contract` gửi song song | 2 `ContractNo` sinh ra khác nhau, không có ràng buộc `UX_Con_Contract_ContractNo` bị vi phạm |
| TC-03 | Suy ra `DurationMonths`/`ExpiryDate` cho loại xác định thời hạn (BR-02) | Unit | `ContractTypeId` có `DurationMonths=12`, `EffectiveDate=2026-08-01` | `DurationMonths=12`, `ExpiryDate=2027-07-31` |
| TC-04 | `ExpiryDate` null cho loại không xác định thời hạn (BR-02) | Unit | `ContractTypeId` có `IsIndefiniteTerm=true` | `DurationMonths=null`, `ExpiryDate=null` |
| TC-05 | Chặn sửa hợp đồng khi `ContractStatus=Signed` (BR-03) | Unit | Gọi `UpdateContract` trên `Contract` có `ContractStatus=Signed` | Ném `ContractAlreadySignedException`, dữ liệu không đổi |
| TC-06 | Cho phép sửa khi `ContractStatus=Unsigned` (BR-03) | Unit | Gọi `UpdateContract` trên `Contract` có `ContractStatus=Unsigned` | Cập nhật thành công |
| TC-07 | Cảnh báo thiếu `ProbationResult` khi tạo hợp đồng chính thức (BR-04) | Unit | `EmployeeId` không có `ProbationResult` Đạt, `ContractType.IsProbationType=false` | Trả về warning `MissingPassedProbationResult`, không chặn tạo |
| TC-08 | Không cảnh báo khi đã có `ProbationResult` Đạt (BR-04) | Unit | `ProbationResultId` hợp lệ được truyền vào | Không có warning |
| TC-09 | Chỉ một `IsCurrentContract=true` mỗi `EmployeeId` (BR-05) | Integration | Tạo hợp đồng mới đánh dấu current cho nhân viên đã có current contract khác | Hợp đồng cũ chuyển `IsCurrentContract=false`, hợp đồng mới `=true`, `Employee.CurrentContractId` cập nhật đúng |
| TC-10 | Chuyển `Unsigned → Processing` khi 1 bên ký (BR-06, BR-07) | Unit | Webhook báo `Employee` đã ký, `Employer` chưa ký | `ContractStatus=Processing` |
| TC-11 | Chuyển `Processing → Signed` khi bên còn lại ký xong (BR-06, BR-07) | Unit | Webhook báo `Employer` ký xong khi đang `Processing` | `ContractStatus=Signed`, `SignDate` được set |
| TC-12 | Chuyển `→ Expired` khi quá `DueInDays` (BR-06, BR-08) | Unit | `DueInDays=7`, thời gian hệ thống vượt hạn, chưa hoàn tất ký | `ContractStatus=Expired` |
| TC-13 | Từ chối chuyển đổi trạng thái không hợp lệ (BR-06) | Unit | Gọi transition `Ended → Signed` | Ném `InvalidContractStatusTransitionException` |
| TC-14 | Phân biệt `Expired` và `Ended` (BR-08) | Unit | Gọi `MarkEnded()` trên hợp đồng đang `Unsigned` | Ném lỗi nghiệp vụ, không cho set `Ended` khi chưa `Signed` |
| TC-15 | Trạng thái `Processing` không hợp lệ nếu cả 2 bên đều `Unsigned` hoặc đều `Signed` (BR-07, edge case) | Unit | Set state với 0 hoặc 2 `SignatureParty.SignStatus=Signed` | Ném lỗi bất biến `InvalidProcessingStateException` |

### 3.2 Gợi ý chữ ký code

```csharp
public enum ContractStatus
{
    Unsigned = 0,
    Signed = 1,
    Ended = 2,
    Paused = 3,
    Expired = 4,
    Processing = 99
}

public interface IContractNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
}

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contract?> GetCurrentContractByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task AddAsync(Contract contract, CancellationToken ct = default);
    Task UpdateAsync(Contract contract, CancellationToken ct = default);
}

public class ContractDurationCalculator
{
    // BR-02: suy ra DurationMonths/ExpiryDate theo ContractType
    public (int? DurationMonths, DateOnly? ExpiryDate) Calculate(ContractType contractType, DateOnly effectiveDate);
}

public class ContractStatusTransitionValidator
{
    // BR-06: kiểm tra chuyển đổi trạng thái hợp lệ trong state machine 6 trạng thái
    public bool CanTransition(ContractStatus from, ContractStatus to);
    public void EnsureCanTransition(ContractStatus from, ContractStatus to); // throws InvalidContractStatusTransitionException
}

public class ContractEditGuard
{
    // BR-03: chặn sửa hợp đồng đã ký
    public void EnsureEditable(Contract contract); // throws ContractAlreadySignedException khi ContractStatus == Signed
}

public class CurrentContractAssigner
{
    // BR-05: đảm bảo chỉ 1 current contract mỗi Employee
    public Task AssignAsCurrentAsync(Guid employeeId, Guid newContractId, CancellationToken ct = default);
}

public interface IESignatureProviderClient
{
    Task<SignatureEnvelope> CreateEnvelopeAsync(Contract contract, CancellationToken ct = default);
    Task<SignatureEnvelopeStatusDto> GetEnvelopeStatusAsync(string externalEnvelopeId, CancellationToken ct = default);
}

public class SignatureEnvelopeStatusHandler
{
    // BR-06, BR-07: xử lý webhook/callback cập nhật SignatureParty và suy ra ContractStatus
    public Task HandlePartySignedAsync(Guid envelopeId, Guid partyId, DateTime signedAtUtc, CancellationToken ct = default);
    public Task HandleEnvelopeExpiredAsync(Guid envelopeId, CancellationToken ct = default);
}

public class ProbationLinkageValidator
{
    // BR-04: cảnh báo khi thiếu ProbationResult Đạt cho hợp đồng chính thức
    public ContractWarning? ValidateProbationLinkage(Contract contract, ContractType contractType, ProbationResultLookup probationLookup);
}
```

### 3.3 Edge case / boundary value cần lưu ý

- **Trạng thái `Processing` khi 1 bên đã ký**: bất biến chỉ hợp lệ khi đúng 1/N `SignatureParty` đã `Signed` (N ≥ 2). Trường hợp N > 2 bên ký (nếu tương lai mở rộng nhiều bên chứng kiến) cần định nghĩa lại ngưỡng chuyển `Processing → Signed` là "tất cả bên bắt buộc (`IsRequiredSigner=true`) đã ký", không chỉ đơn thuần đếm số lượng.
- **Ranh giới `DueInDays`**: cần xác định rõ mốc tính (theo `RequestedAtUtc + DueInDays` ngày lịch hay ngày làm việc) và test boundary đúng ngày cuối cùng (còn hạn) so với ngày kế tiếp (`Expired`).
- **Đồng thời hai webhook callback** cho cùng envelope (VD: cả hai bên ký gần như đồng thời) — cần idempotency theo `ExternalEnvelopeId` + `PartyId` để tránh sinh trùng domain event `ContractFullySigned`.
- **Xoá mềm (`IsDeleted`) hợp đồng đang là `CurrentContract`**: phải chặn hoặc yêu cầu gán lại `IsCurrentContract` cho hợp đồng khác trước khi cho xoá.
- **`ContractType` bị đổi cấu hình `DurationMonths` sau khi đã có hợp đồng dùng loại đó**: hợp đồng hiện có không bị tính lại tự động (giá trị `DurationMonths`/`ExpiryDate` đã snapshot tại thời điểm tạo), chỉ áp dụng cho hợp đồng tạo mới sau thay đổi.
