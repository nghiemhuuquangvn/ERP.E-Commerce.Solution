# Module 1 — Organization Service
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Cảnh báo trùng lặp**: Company, Department (hierarchical), Position (levels 1–8) đã tồn tại trong Master Data API hiện tại. Xem mục 10 "So sánh & Reconciliation" trước khi implement — tránh tạo bảng trùng.

---

## 1. Mục tiêu và phạm vi

Organization Service là nền tảng định nghĩa cấu trúc tổ chức: công ty → chi nhánh → phòng ban → nhóm, cùng lịch làm việc và ca kíp. Mọi module khác (Sales, Purchasing, HRM, Finance) đều tham chiếu vào Organization Service để xác định "ai thuộc đơn vị nào, chịu trách nhiệm ngân sách nào".

Phạm vi module này **không bao gồm**: thông tin nhân sự chi tiết (hồ sơ, hợp đồng, lương — thuộc Module 14 HRM), authentication/authorization (thuộc Module 2).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| System Admin | Cấu hình cấu trúc tổ chức toàn hệ thống |
| HR Admin | Quản lý Department/Team/Position trong phạm vi được phân quyền |
| Branch Manager | Xem/đề xuất thay đổi cấu trúc chi nhánh mình quản lý |
| Finance | Đọc Cost Center/Profit Center để lập báo cáo |

## 3. Quy trình nghiệp vụ chính

1. Tạo Company → tạo Business Unit → tạo Branch → tạo Division → tạo Department → tạo Team.
2. Gán Employee vào Team + Position (Employee reference sang HRM, không sở hữu dữ liệu nhân sự).
3. Định nghĩa Calendar/Holiday theo từng Company/Branch (một công ty đa quốc gia có thể có nhiều Calendar).
4. Định nghĩa Shift, gán Shift cho Team hoặc Employee.
5. Cost Center/Profit Center gắn với Department để phục vụ phân bổ chi phí trong Finance.

## 4. Domain Model (Aggregate Roots — DDD)

```
Company (Aggregate Root)
 ├─ Branch (Entity)
 │   └─ Division (Entity)
 │       └─ Department (Entity)   ⚠️ đã có ở Master Data
 │           └─ Team (Entity)
 ├─ BusinessUnit (Entity)
 ├─ CostCenter (Entity)
 └─ ProfitCenter (Entity)

OrganizationCalendar (Aggregate Root)
 ├─ Holiday (Entity)
 └─ Shift (Entity)

Position (Aggregate Root, flat reference table) ⚠️ đã có ở Master Data
OrganizationTree (Read Model — không phải bảng riêng, build từ Company→Team qua recursive CTE hoặc materialized view)
```

**Bounded Context**: `Organization` — độc lập với `Identity` và `HRM`, chỉ expose ID reference (Employee chỉ là FK sang HRM.Employee, không duplicate field).

## 5. Database Design (đề xuất bảng)

| # | Bảng | Cột chính | Ghi chú |
|---|---|---|---|
| 1 | `Companies` | Id, Code, Name, TaxCode, LegalAddress, CurrencyId, IsActive | ⚠️ trùng `Companies`/`Customers` hiện có — xem mục 10 |
| 2 | `Branches` | Id, CompanyId, Code, Name, AddressId, ManagerEmployeeId | |
| 3 | `Divisions` | Id, BranchId, Code, Name, ParentDivisionId (self-ref) | |
| 4 | `Departments` | Id, DivisionId, Code, Name, ParentDepartmentId | ⚠️ trùng bảng Department hierarchical đã build |
| 5 | `Teams` | Id, DepartmentId, Code, Name, LeaderEmployeeId | |
| 6 | `Positions` | Id, Code, Name, Level (1-8), ParentPositionId | ⚠️ trùng Position levels 1–8 đã build |
| 7 | `BusinessUnits` | Id, CompanyId, Code, Name | |
| 8 | `CostCenters` | Id, CompanyId, DepartmentId, Code, Name, BudgetAmount | |
| 9 | `ProfitCenters` | Id, CompanyId, BusinessUnitId, Code, Name | |
| 10 | `OrganizationCalendars` | Id, CompanyId, Year, Name, IsDefault | |
| 11 | `Holidays` | Id, CalendarId, Date, Name, IsRecurringYearly | |
| 12 | `Shifts` | Id, CompanyId, Code, Name, StartTime, EndTime, BreakMinutes | |
| 13 | `EmployeeShiftAssignments` | Id, EmployeeId (FK→HRM), ShiftId, EffectiveFrom, EffectiveTo | |
| 14 | `EmployeeTeamAssignments` | Id, EmployeeId (FK→HRM), TeamId, PositionId, EffectiveFrom, EffectiveTo | Lịch sử điều chuyển |
| 15 | `OrganizationAuditLogs` | Id, EntityName, EntityId, Action, ChangedBy, ChangedAt, OldValue, NewValue (JSON) | |

→ **15 bảng chính**, không cần 20 như dự kiến ban đầu nếu tái sử dụng Company/Department/Position sẵn có (xem mục 10).

## 6. API Design (REST) — theo chuẩn `ApiResponse<T>` / `PaginatedResult<T>` hiện tại của project

```
GET    /api/v1/organization/companies/{id}/tree        → OrganizationTree (đệ quy Branch→Team)
GET    /api/v1/organization/departments?parentId=       → PaginatedResult<DepartmentDto>
POST   /api/v1/organization/departments                 → ApiResponse<DepartmentDto>
PUT    /api/v1/organization/departments/{id}
DELETE /api/v1/organization/departments/{id}             → soft delete (IsActive=false), không hard delete vì có FK từ Sales/Finance
GET    /api/v1/organization/cost-centers/{id}/budget-summary
POST   /api/v1/organization/employee-team-assignments   → điều chuyển nhân sự, ghi EffectiveFrom/To
```

## 7. Event Design (nếu dùng Kafka/RabbitMQ — hiện project chưa xác nhận đã tích hợp)

| Event | Trigger | Consumer |
|---|---|---|
| `OrganizationDepartmentCreated` | Tạo Department mới | HRM (đồng bộ cache), Finance (tạo CostCenter mặc định) |
| `EmployeeTeamAssignmentChanged` | Điều chuyển nhân sự | Payroll (tính lại phụ cấp theo phòng ban), Notification |
| `OrganizationCalendarUpdated` | Cập nhật Holiday | Attendance/Payroll |

Nếu chưa có message broker, có thể thay bằng Domain Event nội bộ (MediatR `INotification`) trước, migrate sang Kafka sau — tránh over-engineering ở giai đoạn này.

## 8. Permission Matrix (sơ bộ — chi tiết ở Module 2)

| Role | Company | Department | CostCenter | OrgTree (view) |
|---|---|---|---|---|
| System Admin | CRUD | CRUD | CRUD | ✓ |
| HR Admin | Read | CRUD (scope) | Read | ✓ |
| Branch Manager | Read (own) | Read (own) | Read (own) | ✓ (own branch) |
| Finance | Read | Read | CRUD | ✓ |

## 9. Business Rules & Validation

- Company Code, Department Code: unique trong phạm vi Company (không unique toàn hệ thống).
- Không cho xoá Department nếu còn Team/CostCenter/Employee active bên dưới — chỉ soft delete.
- `ParentDepartmentId` không được self-reference vòng lặp (kiểm tra bằng recursive check trước khi save).
- Shift.EndTime có thể qua ngày hôm sau (ca đêm) — cần cột `IsOvernightShift` thay vì so sánh EndTime < StartTime là lỗi.
- OrganizationTree nên cache (Redis) vì đọc nhiều, ghi ít — invalidate cache khi có event `OrganizationDepartmentCreated/Updated`.

## 10. So sánh & Reconciliation với project hiện tại

Đây là phần quan trọng nhất vì Master Data hiện tại **đã có** một phần chức năng này:

| Entity trong đề xuất Module 1 | Trạng thái trong ERP.E-Commerce hiện tại | Khuyến nghị |
|---|---|---|
| Company | Đã có (company/customer management) | **Không tạo mới** — mở rộng bảng hiện có nếu thiếu cột (TaxCode, LegalAddress...) |
| Department (hierarchical) | Đã có | **Không tạo mới** — dùng lại, chỉ thêm quan hệ với Division nếu cần thêm tầng |
| Position (levels 1–8) | Đã có | **Không tạo mới** — dùng lại nguyên vẹn |
| Branch, Division, Team | Chưa có | Cần bổ sung — đây là phần thực sự "mới" |
| CostCenter, ProfitCenter | Chưa có | Bổ sung, cần bàn với Finance trước vì ảnh hưởng đến module Finance sau này |
| Calendar, Holiday, Shift | Chưa có | Bổ sung — độc lập, ít rủi ro |
| OrganizationTree | Chưa có | Build như Read Model/View, không phải bảng vật lý riêng |
