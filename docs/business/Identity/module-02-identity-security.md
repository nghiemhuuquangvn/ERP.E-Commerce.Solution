# Module 2 — Identity & Security
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Liên quan trực tiếp**: `Tenant` trong module này trùng phạm vi với "multi-tenant precedence override" đã lên kế hoạch trong Settings/Configuration Management. Nên làm Module 2 **trước** Settings, vì Settings phụ thuộc vào Tenant model, không phải ngược lại.

---

## 1. Mục tiêu và phạm vi

Identity & Security là service độc lập chịu trách nhiệm authentication, authorization (RBAC + ABAC/data-level permission), và audit truy cập. Đây là service **bắt buộc phải tách riêng khỏi modular monolith sớm nhất** trong 20 module, vì lý do bảo mật (blast radius) và vì nhiều hệ thống con (Admin, Frontend, Mobile, POS, Tablet — đúng như UI mockup bạn đã yêu cầu trước đây) đều cần cùng một identity provider.

Phạm vi **không bao gồm**: Employee profile (HRM), Organization structure (Module 1) — Identity chỉ giữ `UserId` và liên kết lỏng (loose FK) sang Employee/Department qua ID.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| End User | Đăng nhập, đổi mật khẩu, MFA |
| System Admin | Quản lý Role/Permission toàn hệ thống |
| Tenant Admin | Quản lý User/Role trong phạm vi tenant mình (nếu multi-tenant) |
| Auditor | Xem Audit Login/Session logs, không có quyền sửa |
| External Identity Provider | Google/Microsoft/SSO qua OAuth |

## 3. Quy trình nghiệp vụ chính

1. Đăng ký/tạo User → gán Role → Role gắn nhiều Permission → Permission kiểm soát Menu/Screen/Action/API.
2. Đăng nhập → sinh JWT (access token ngắn hạn) + Refresh Token (dài hạn, lưu hash trong DB).
3. MFA (nếu bật) → xác thực OTP trước khi cấp token.
4. External Login (OAuth/SSO) → map external identity vào User nội bộ qua bảng `ExternalLogins`.
5. Mỗi request API → middleware kiểm tra JWT → kiểm tra Permission (theo Menu/Screen/Action) → kiểm tra Data Permission (theo Tenant/Branch/Department scope) → ghi Audit nếu là action nhạy cảm.

## 4. Domain Model (Aggregate Roots — DDD)

```
Tenant (Aggregate Root)
 └─ User (Entity)
     ├─ UserRole (join)
     ├─ RefreshToken (Entity)
     ├─ ExternalLogin (Entity)
     ├─ MfaSetting (Entity)
     └─ Session (Entity)

Role (Aggregate Root)
 └─ RolePermission (join) → Permission

Permission (Aggregate Root, flat reference)
 ├─ Menu / Screen / Action (tree hoặc flat, tham chiếu UI hiện có: Admin/Frontend/Mobile/POS/Tablet)
 └─ ApiPermission (map Permission ↔ API endpoint pattern)

DataPermission (Aggregate Root) — scope theo Tenant/Branch/Department/Owner
AuditLogin (Read Model / Append-only log)
```

**Bounded Context**: `Identity` — hoàn toàn độc lập, publish event khi User/Role thay đổi để các service khác cache lại (không gọi trực tiếp Identity DB từ service khác).

## 5. Database Design (đề xuất bảng — nhóm theo chức năng)

**Nhóm User & Tenant**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Tenants` | Id, Code, Name, IsActive, PlanType |
| 2 | `Users` | Id, TenantId, Username, Email, PasswordHash, PasswordSalt, IsActive, EmployeeId (nullable FK loose) |
| 3 | `UserProfiles` | Id, UserId, DisplayName, AvatarUrl, Locale, TimeZone |
| 4 | `RefreshTokens` | Id, UserId, TokenHash, ExpiresAt, IsRevoked, CreatedByIp |
| 5 | `ExternalLogins` | Id, UserId, Provider (Google/Microsoft/Facebook), ProviderKey |
| 6 | `MfaSettings` | Id, UserId, Method (TOTP/SMS/Email), SecretKeyEncrypted, IsEnabled |
| 7 | `Sessions` | Id, UserId, DeviceInfo, IpAddress, LoginAt, LogoutAt, IsActive |

**Nhóm Role & Permission**
| # | Bảng | Cột chính |
|---|---|---|
| 8 | `Roles` | Id, TenantId (nullable = system role), Code, Name |
| 9 | `UserRoles` | UserId, RoleId, EffectiveFrom, EffectiveTo |
| 10 | `Permissions` | Id, Code, Name, GroupCode |
| 11 | `RolePermissions` | RoleId, PermissionId |
| 12 | `Menus` | Id, ParentMenuId, Code, Name, Icon, SortOrder, ClientApp (Admin/Frontend/Mobile/POS/Tablet) |
| 13 | `Screens` | Id, MenuId, Code, Name, Route |
| 14 | `Actions` | Id, ScreenId, Code, Name (View/Create/Edit/Delete/Approve/Export) |
| 15 | `ApiPermissions` | Id, PermissionId, HttpMethod, RoutePattern |
| 16 | `DataPermissions` | Id, RoleId, ScopeType (Tenant/Branch/Department/Owner), ScopeValue |

**Nhóm Audit & Security**
| # | Bảng | Cột chính |
|---|---|---|
| 17 | `AuditLogins` | Id, UserId, Result (Success/Failed/Locked), IpAddress, UserAgent, AttemptedAt |
| 18 | `AuditActions` | Id, UserId, EntityName, EntityId, Action, ChangedAt, OldValue, NewValue (JSON) |
| 19 | `PasswordPolicies` | Id, TenantId, MinLength, RequireSpecialChar, ExpiryDays, HistoryCount |
| 20 | `PasswordHistories` | Id, UserId, PasswordHash, ChangedAt |
| 21 | `LoginLockouts` | Id, UserId, FailedAttempts, LockedUntil |
| 22 | `ApiKeys` | Id, TenantId, KeyHash, Name, ExpiresAt, IsActive (cho tích hợp system-to-system, ví dụ POS offline sync) |

→ **22 bảng**, nằm trong khoảng 30–40 đề xuất ban đầu nhưng ở mức tối giản hợp lý (không cần OAuth Client/Scope riêng nếu chưa làm public API platform).

## 6. API Design (REST)

```
POST   /api/v1/auth/login                    → ApiResponse<TokenResponseDto> (access + refresh token)
POST   /api/v1/auth/refresh-token
POST   /api/v1/auth/logout
POST   /api/v1/auth/mfa/verify
GET    /api/v1/auth/external-login/{provider}/callback
GET    /api/v1/users?tenantId=                → PaginatedResult<UserDto>
POST   /api/v1/roles/{roleId}/permissions     → gán permission cho role
GET    /api/v1/permissions/menu-tree?clientApp=Admin   → build menu động theo user hiện tại
GET    /api/v1/audit/logins?userId=&from=&to=
```

## 7. Event Design

| Event | Trigger | Consumer |
|---|---|---|
| `UserCreated` / `UserDeactivated` | CRUD User | Notification, HRM (nếu link Employee) |
| `RolePermissionChanged` | Cập nhật quyền | Tất cả service khác cần invalidate permission cache (Redis) |
| `LoginFailed` (x N lần liên tiếp) | Đăng nhập sai | Security alert / Lockout |
| `TenantCreated` | Provisioning tenant mới | Tạo default Role/Department cho tenant (liên kết Module 1) |

## 8. Permission Matrix (mẫu — Permission của chính module này)

| Role | Users | Roles/Permissions | Audit Logs | ApiKeys |
|---|---|---|---|---|
| System Admin | CRUD | CRUD | Read | CRUD |
| Tenant Admin | CRUD (own tenant) | CRUD (own tenant, không sửa system role) | Read (own tenant) | CRUD (own tenant) |
| Auditor | Read | Read | Read | — |
| End User | Read/Update (own profile) | — | — | — |

## 9. Business Rules & Validation

- Password: hash bằng bcrypt/argon2, **không** dùng SHA/MD5. Lưu `PasswordSalt` riêng nếu không dùng thuật toán tự sinh salt.
- Refresh Token: rotate mỗi lần dùng (one-time use), revoke toàn bộ khi phát hiện reuse (chống replay attack).
- MFA secret key: mã hoá at-rest (Azure Key Vault — project đã có tích hợp Key Vault, tận dụng luôn).
- DataPermission áp dụng qua Global Query Filter của EF Core (`HasQueryFilter`) theo TenantId, tránh phải tự viết WHERE ở từng service.
- Account lockout: khoá 15 phút sau 5 lần sai (cấu hình qua `PasswordPolicies`, không hardcode).
- JWT access token: TTL ngắn (15 phút), Refresh Token TTL dài (7-30 ngày), lưu ý đồng bộ với FE/Mobile/POS đã có (kiểm tra token refresh flow trên các client này).

## 10. Liên hệ với Settings/Configuration Management (module bạn định làm tiếp theo)

- `Tenant` ở đây chính là gốc của "multi-tenant precedence override" trong kế hoạch Settings — **nên build Tenant entity ở Module 2 trước**, Settings sẽ tham chiếu `TenantId` thay vì tự định nghĩa lại khái niệm tenant.
- `PasswordPolicies`, cấu hình MFA bắt buộc hay không... bản chất là Setting theo Tenant — có thể lưu trong `SettingValue` (pattern SettingCategory/SettingDefinition/SettingValue đã định) thay vì bảng riêng `PasswordPolicies`, để nhất quán 1 nguồn config duy nhất. **Cân nhắc gộp** thay vì tách 2 hệ thống config song song.
- Azure Key Vault cho MFA secret / ApiKey đã khớp với hướng Settings đang định làm (Redis cache, Key Vault-backed secrets).

## 11. Khuyến nghị triển khai .NET

- Đây là module **nên tách microservice riêng sớm** (khác với Module 1) vì: (a) bảo mật cần isolate, (b) mọi client app (Admin/Frontend/Mobile/POS/Tablet) đều gọi vào, tải cao và ổn định khác biệt so với business module.
- Dùng ASP.NET Core Identity làm nền (không tự viết lại User/Password từ đầu) rồi mở rộng thêm Tenant/Permission/DataPermission theo mô hình trên — tiết kiệm đáng kể thời gian so với tự build toàn bộ authentication.
- JWT: `Microsoft.AspNetCore.Authentication.JwtBearer`, ký bằng key lưu ở Key Vault.
- Cache Permission/Menu tree ở Redis, key theo `UserId` + version, invalidate qua event `RolePermissionChanged`.
- Audit log: cân nhắc bảng append-only riêng biệt (không chung DbContext transaction với business data) để tránh audit table phình to ảnh hưởng performance các bảng khác — có thể tách sang database riêng hoặc dùng bảng partition theo tháng.

## 12. Reconciliation — Kiến trúc Permission thực tế sau đợt tái cấu trúc (2026-07-29)

> Mục này ghi lại trạng thái **thật** của cơ chế permission sau khi thực hiện, khác với §4-§8 vốn là **đề xuất thiết kế ban đầu** (nhiều bảng ở đó — `Menus`, `Screens`, `Actions`, `ApiPermissions`, `DataPermissions` — chưa từng được implement; AuthDB thật chỉ có `Users`, `Roles`, `UserRoles`, `Permissions`, `RolePermissions`, `RefreshTokens`, `Tenants`, `ApiKeys` + các bảng Settings/License theo từng category).

**Bối cảnh**: người dùng phát hiện pattern `[Authorize(Policy = "Permission")]` + `[RequirePermission(Permissions.X)]` trong IdentityService khó bảo trì. Rà soát cho thấy vấn đề lớn hơn: hệ thống chạy **2 cơ chế authorization song song, không nhất quán**, cộng thêm frontend giữ bản sao permission string riêng không đồng bộ.

**Trước khi tái cấu trúc**:
| | IdentityService (cũ) | 13 service còn lại |
|---|---|---|
| Attribute | `[Authorize(Policy="Permission")]` + `[RequirePermission(...)]` | `[AuthorizePermission("...")]` |
| Cơ chế | ASP.NET Core policy → `PermissionRequirement` → `PermissionAuthorizationHandler` | `IAsyncAuthorizationFilter` độc lập |
| Nơi kiểm tra | **Gọi DB mỗi request** (`IPermissionService.HasPermissionAsync` join `UserRoles→RolePermissions→Permissions`) | **Chỉ đọc JWT claim** `"permission"`, không gọi DB |
| Kho permission string | `ERP.Identity.Application.Authorization.Permissions` (21 hằng số, riêng của Identity) | `ERP.Shared.Authorization.PermissionConstants` (~230 hằng số nghiệp vụ) |
| Nguồn Role→Permission | Bảng DB `RolePermissions` (chỉ 2 role SuperAdmin/Admin có seed, chỉ phủ 21 permission của Identity) | Dictionary hardcode `RoleDefinitions.RolePermissions` trong `ERP.Shared`, biên dịch vào **cả 14 service** |

**Sau khi tái cấu trúc (2 phase)**:
- **Phase 1**: Gộp toàn bộ 14 class permission cục bộ của Identity (21 hằng số `Permissions.cs` + 13 class `*SettingsPermissions`/`LicensePermissions`, tổng ~90 hằng số) vào `PermissionConstants.cs` — tổng cộng còn lại **244 permission code duy nhất**, không nơi nào khác định nghĩa permission string nữa. Sửa kèm 1 bug thật: `PermissionService.CreateAsync/UpdateAsync/DeleteAsync` (tạo permission mới qua UI) không gọi `SaveChangesAsync` — thay đổi bị mất âm thầm; đã sửa bằng cách route qua `IUnitOfWork` giống `RoleService`.
- **Phase 2**: 21 controller của Identity chuyển từ `[Authorize(Policy="Permission")]`+`[RequirePermission]` sang `[AuthorizePermission(...)]` — cùng cơ chế JWT-claims với 13 service kia, bỏ hẳn round-trip DB riêng. Xóa `PermissionRequirement`, `PermissionAuthorizationHandler`, `RequirePermissionAttribute`, policy `"Permission"`.
- **Nguồn Role→Permission chuyển hẳn về DB**: `AuthService.IssueTokensAsync` trước đây `permissions ∪ RoleDefinitions.GetPermissionsForRoles(roles)` (union DB + hardcode) — giờ **chỉ đọc DB**. Migration một-lần `src/Database/Identity/013_SeedRoleDefinitionsIntoRolePermissions.sql` (tự sinh bằng script phân tích trực tiếp `PermissionConstants.cs`/`RoleDefinitions.cs`, không gõ tay, đã đối chiếu số lượng thủ công) seed đúng y hệt tập quyền `RoleDefinitions.cs` đang cấp cho SuperAdmin/Admin/Manager/Purchaser/Viewer vào bảng `RolePermissions`, khớp theo `RoleCode` trên **mọi tenant** (không hardcode GUID role). Kết quả: JWT permission claim sau khi chạy migration giống hệt trước — chỉ khác *nguồn*, không khác *tập quyền*. Từ nay, sửa quyền của 1 role chỉ cần sửa dữ liệu qua `RolesController` (đã hoạt động, có gọi `SaveChangesAsync`), không cần sửa code + build lại 14 service.
- **`RoleDefinitions.cs` vẫn được giữ lại** (không xóa) — `src/Gateway/Ocelot.API/Authorization/GatewayAuthorizationMiddleware.cs` vẫn gọi trực tiếp để tính header `X-User-Permissions`. Header này thực chất là **dead weight đã có từ trước**: mọi service đọc permission từ JWT claim `"permission"` (qua `JwtClaimsUserContextService`), không đọc header Gateway forward — phát hiện này nằm ngoài phạm vi đợt tái cấu trúc, chưa xử lý.
- **Guard test mới**: `tests/unit/ERP.Identity.UnitTests/Authorization/PermissionGuardTests.cs` — quét reflection mọi `[AuthorizePermission]`/`[AuthorizeAnyPermission]` trong assembly IdentityService, assert từng permission string tồn tại trong `PermissionConstants.GetAllPermissions()`. Chặn lại đúng lớp lỗi đã từng xảy ra ở Accounting/CRM/HRM (permission string dùng trong code nhưng thiếu hằng số → 403 âm thầm với mọi role kể cả SuperAdmin).
- **Chưa làm (Phase 3, còn tồn đọng)**: Admin frontend (`src/frontend/admin`) hardcode 169 chuỗi permission literal rải rác ở 89 file, không dùng chung nguồn với backend — cần codegen từ `PermissionConstants.cs` sang 1 file TypeScript constants.
- **Môi trường xác minh**: không có SQL Server chạy sẵn trong sandbox lúc thực hiện (không docker, không sqlcmd kết nối được) — migration 013 mới được kiểm tra bằng đối chiếu thủ công + script sinh tự động, **chưa chạy thử trên DB thật**. Xem `deployment/docker/README.md` (mới thêm) để bootstrap SQL Server qua Docker và chạy thử.
