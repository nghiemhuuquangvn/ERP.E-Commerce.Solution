# Identity Service

Centralized authentication, authorization, user management, and role-based access control (RBAC) for the ERP E-Commerce Solution.

## Overview

The Identity Service is the security backbone of the entire microservices ecosystem. It provides:

- **Authentication**: User login, token generation, and validation
- **Authorization**: Role-based access control (RBAC) and permission management
- **User Management**: User registration, profile management, and lifecycle
- **Integration Events**: Publishes user lifecycle events for other services to react

## Responsibilities

### Authentication
- User registration with email validation
- Password authentication with secure hashing
- JWT token generation and refresh
- External login / SSO: Google, Microsoft, Facebook (client obtains a token via the
  provider's SDK, server verifies it via `IExternalAuthProvider`), and Twitter/X (a
  separate OAuth 2.0 Authorization Code + PKCE redirect flow via `TwitterOAuthService`,
  since Twitter has no equivalent single-shot verifiable token)
- Two-factor authentication (2FA) via SMS or Email: after password verification, if
  required, a hashed one-shot code is issued (`TwoFactorChallenge`) and delivered
  through NotificationService via Kafka (`identity-topic`) — never a direct call
- Auth settings — two-tier toggle gating both of the above: `SystemAuthSettings`
  (SuperAdmin-only, all tenants) and `TenantAuthSettings` (per-tenant; requires the
  System switch to also allow it). See `docs/business/Identity/ExternalAuthAndTwoFactor.md`.
- Session management

### Authorization
- Role definition and assignment
- Permission definition and enforcement
- Role hierarchies
- Dynamic permission checking
- Cross-service authorization

### User Management
- User registration and profile management
- Password reset and change
- Account activation/deactivation
- User search and listing
- Bulk user operations
- User audit trail

## Architecture

### Technology Stack
- **Framework**: ASP.NET Core 10
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Authentication**: JWT (JSON Web Tokens)
- **Patterns**: DDD, CQRS (via MediatR), Event-Driven
- **Message Bus**: RabbitMQ
- **Caching**: Redis (optional)

### Project Structure
```
IdentityService/
├── API/                         # REST endpoints
├── Application/                 # Commands, Queries, Handlers
├── Domain/                      # Entities, aggregates, business rules
├── Infrastructure/              # Database, repositories, external services
├── Shared/                      # DTOs, constants
└── Tests/                       # Unit & integration tests
```

## API Endpoints

### Authentication Endpoints

#### Register User
```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}

Response: 201 Created
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "createdAt": "2026-06-13T10:30:00Z"
}
```

#### Login
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}

Response: 200 OK
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

#### Refresh Token
```http
POST /api/v1/auth/refresh-token
Content-Type: application/json

{
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}

Response: 200 OK
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 3600
}
```

#### Validate Token
```http
GET /api/v1/auth/validate
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...

Response: 200 OK
{
  "isValid": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "roles": ["Admin", "Manager"]
}
```

### User Management Endpoints

#### Get User
```http
GET /api/v1/users/{userId}
Authorization: Bearer <token>

Response: 200 OK
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "roles": ["Admin", "Manager"],
  "isActive": true,
  "createdAt": "2026-06-13T10:30:00Z",
  "lastLoginAt": "2026-06-13T15:45:00Z"
}
```

#### Update User
```http
PUT /api/v1/users/{userId}
Authorization: Bearer <token>
Content-Type: application/json

{
  "firstName": "Jane",
  "lastName": "Smith",
  "phoneNumber": "+0987654321"
}

Response: 200 OK
```

#### List Users
```http
GET /api/v1/users?skip=0&take=20&search=john
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 100,
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "roles": ["Manager"]
    }
  ]
}
```

#### Change Password
```http
POST /api/v1/users/{userId}/change-password
Authorization: Bearer <token>
Content-Type: application/json

{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewPassword456!"
}

Response: 200 OK
```

### Role Management Endpoints

#### Get Role
```http
GET /api/v1/roles/{roleId}
Authorization: Bearer <token>

Response: 200 OK
{
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "name": "Admin",
  "description": "Administrator with full access",
  "permissions": ["user.create", "user.edit", "user.delete", "order.manage"],
  "isSystem": false,
  "createdAt": "2026-06-13T10:30:00Z"
}
```

#### Create Role
```http
POST /api/v1/roles
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Manager",
  "description": "Manager with limited admin access",
  "permissions": ["order.manage", "report.view"]
}

Response: 201 Created
```

#### List Roles
```http
GET /api/v1/roles?search=admin
Authorization: Bearer <token>

Response: 200 OK
{
  "items": [
    {
      "id": "660e8400-e29b-41d4-a716-446655440000",
      "name": "Admin",
      "permissionCount": 50
    }
  ]
}
```

#### Assign Role to User
```http
POST /api/v1/users/{userId}/roles/{roleId}
Authorization: Bearer <token>

Response: 200 OK
```

#### Remove Role from User
```http
DELETE /api/v1/users/{userId}/roles/{roleId}
Authorization: Bearer <token>

Response: 204 No Content
```

### Permission Management Endpoints

#### List Permissions
```http
GET /api/v1/permissions
Authorization: Bearer <token>

Response: 200 OK
{
  "items": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440000",
      "name": "user.create",
      "description": "Create new users",
      "category": "User Management"
    }
  ]
}
```

### External Login (SSO) Endpoints

```http
POST /api/auth/external-login          # Google / Microsoft / Facebook — body: { provider, providerKey, idToken, tenantCode, userType }
GET  /api/auth/external-login/twitter/authorize-url?tenantCode=   # Twitter/X step 1 (PKCE)
POST /api/auth/external-login/twitter/callback                   # Twitter/X step 2 — body: { code, state }
GET  /api/auth/login-options?tenantCode=   # [AllowAnonymous] — which providers/2FA are enabled for this tenant, for rendering a login page before any JWT exists
```

All return the same `LoginResponse` shape as `POST /api/auth/login`.

### Two-Factor Authentication Endpoints

```http
POST /api/auth/2fa/verify              # body: { challengeId, code } → LoginResponse (tokens if correct)
POST /api/auth/2fa/resend              # body: { challengeId } → LoginResponse with a new challengeId
POST /api/users/{id}/2fa/enable        # [Authorize, self-service only] body: { method: "Sms"|"Email", phoneNumber? }
POST /api/users/{id}/2fa/disable       # [Authorize, self-service only] — rejected if the tenant has TwoFactorRequired
```

`LoginResponse` gained `requiresTwoFactor`, `challengeId`, `twoFactorMethod`,
`expiresInSeconds` — when `requiresTwoFactor` is true, no tokens are issued yet; the
client must call `2fa/verify` next.

### Settings Endpoints

```http
GET  /api/settings/system              # [settings:system:view]   — SuperAdmin scope
PUT  /api/settings/system              # [settings:system:manage]
GET  /api/settings/tenant/{tenantId}   # [settings:tenant:view]    — own tenant, or any tenant if caller also has settings:system:manage
PUT  /api/settings/tenant/{tenantId}   # [settings:tenant:manage]
```

Each returns/accepts `{ googleEnabled, microsoftEnabled, facebookEnabled,
twitterEnabled, twoFactorEnabled }` (system) or the same plus `tenantId`/
`twoFactorRequired` (tenant).

## Published Events

### UserCreatedEvent
Emitted when a new user is registered successfully.

```csharp
public class UserCreatedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<string> Roles { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Subscribers**: Sales Service, Purchasing Service, Warehouse Service

### UserUpdatedEvent
Emitted when user information is updated.

```csharp
public class UserUpdatedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<string> Roles { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### RoleAssignedEvent
Emitted when a role is assigned to a user.

```csharp
public class RoleAssignedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string RoleName { get; set; }
    public DateTime AssignedAt { get; set; }
}
```

### TwoFactorCodeGeneratedEvent
Raised when a login requires 2FA and a one-shot OTP is generated. Carries the
plaintext code (the DB only ever stores the hash) — consumed by NotificationService
to send it via SMS or Email. Published to Kafka topic `identity-topic`.

```csharp
public class TwoFactorCodeGeneratedDomainEvent : IDomainEvent
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public string Method { get; }       // "Sms" | "Email"
    public string Destination { get; }  // masked phone/email
    public string Code { get; }         // plaintext, one-shot
    public int ExpiryMinutes { get; }
    public DateTime OccurredOn { get; }
}
```

**Subscribers**: Notification Service (`IdentityEventConsumer`)

## Consumed Events

**None** - Identity Service is an upstream service with no dependencies on other services.

## Database Schema

### Tables

#### Users
```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    PasswordHash NVARCHAR(MAX),
    PhoneNumber NVARCHAR(20),
    IsActive BIT DEFAULT 1,
    IsEmailConfirmed BIT DEFAULT 0,
    EmailConfirmationToken NVARCHAR(MAX),
    PasswordResetToken NVARCHAR(MAX),
    PasswordResetTokenExpiration DATETIME2,
    LastLoginAt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    DeletedAt DATETIME2
);
```

#### Roles
```sql
CREATE TABLE Roles (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500),
    IsSystem BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2
);
```

#### Permissions
```sql
CREATE TABLE Permissions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500),
    Category NVARCHAR(100),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

#### UserRoles
```sql
CREATE TABLE UserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    AssignedAt DATETIME2 DEFAULT GETUTCDATE(),
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);
```

#### RolePermissions
```sql
CREATE TABLE RolePermissions (
    RoleId UNIQUEIDENTIFIER NOT NULL,
    PermissionId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (RoleId, PermissionId),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id),
    FOREIGN KEY (PermissionId) REFERENCES Permissions(Id)
);
```

#### SystemAuthSettings / TenantAuthSettings / TwoFactorChallenges / OAuthStates
```sql
-- Single well-known-GUID row; SuperAdmin-only master switch (all tenants)
CREATE TABLE SystemAuthSettings (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    GoogleEnabled BIT, MicrosoftEnabled BIT, FacebookEnabled BIT, TwitterEnabled BIT,
    TwoFactorEnabled BIT
);

-- One row per tenant; effective only if SystemAuthSettings also allows it
CREATE TABLE TenantAuthSettings (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    GoogleEnabled BIT, MicrosoftEnabled BIT, FacebookEnabled BIT, TwitterEnabled BIT,
    TwoFactorEnabled BIT, TwoFactorRequired BIT
);

-- One-shot OTP challenges created at login when 2FA is required
CREATE TABLE TwoFactorChallenges (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL, TenantId UNIQUEIDENTIFIER NOT NULL,
    CodeHash NVARCHAR(500) NOT NULL, Method NVARCHAR(16) NOT NULL,
    Destination NVARCHAR(256), ExpiredAt DATETIME2 NOT NULL,
    AttemptCount INT, MaxAttempts INT, VerifiedAt DATETIME2
);

-- Twitter/X Authorization Code + PKCE flow — short-lived
CREATE TABLE OAuthStates (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    State NVARCHAR(256) NOT NULL UNIQUE, CodeVerifier NVARCHAR(256) NOT NULL,
    TenantCode NVARCHAR(256) NOT NULL, ExpiredAt DATETIME2 NOT NULL
);
```
`Users` also gained `PhoneNumber`, `TwoFactorEnabled`, `TwoFactorMethod` columns (see
`src/Database/Identity/002_AddExternalAuthSettingsAndTwoFactor.sql`).

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server 2019+
- Docker (optional)

### Configuration

Create `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "IdentityDatabase": "Server=localhost;Database=IdentityDB;User Id=sa;Password=YourPassword;"
  },
  "Jwt": {
    "SecretKey": "your-very-long-secret-key-here-minimum-32-characters",
    "Issuer": "https://erp-identity-service",
    "Audience": "erp-api",
    "ExpirationMinutes": 60
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

For external login and 2FA, `appsettings.Development.json` also needs (see the actual
file for the real placeholder values already checked in):
```json
{
  "Authentication": {
    "Google": { "ClientId": "..." },
    "Microsoft": { "Authority": "...", "ClientId": "..." },
    "Facebook": { "AppId": "...", "AppSecret": "..." },
    "Twitter": { "ClientId": "...", "ClientSecret": "...", "RedirectUri": "http://localhost:5173/login/oauth/twitter/callback" }
  },
  "TwoFactorAuthentication": { "CodeLength": 6, "ExpiryMinutes": 5, "MaxAttempts": 3, "ResendCooldownSeconds": 60 }
}
```

### Running the Service

#### Local Development
```bash
# Restore dependencies
dotnet restore

# Apply migrations
dotnet ef database update

# Run the service
dotnet run

# Service available at: http://localhost:5001
```

#### Docker
```bash
# Build image
docker build -t identity-service:latest .

# Run container
docker run -d \
  -p 5001:80 \
  -e ConnectionStrings__IdentityDatabase="Server=sqlserver;Database=IdentityDB;..." \
  -e Jwt__SecretKey="your-secret-key" \
  --name identity-service \
  identity-service:latest
```

## Security Considerations

### Password Security
- Minimum 8 characters, 1 uppercase, 1 lowercase, 1 number, 1 special character
- Stored using bcrypt or Argon2
- Never logged or exposed in responses

### Token Security
- JWT tokens have 1-hour expiration
- Refresh tokens valid for 7 days
- Tokens include user ID, email, and roles
- Sign tokens with strong secret key (32+ characters)
- Always use HTTPS in production

### Rate Limiting
- Login attempts: 5 failures = 15-minute lockout
- Password reset: 3 attempts per hour
- API calls: 1000 requests per minute per user

### Account-security hardening (2026-10-19)
See `docs/business/Identity/SecurityReview-2026-10.md` for the full review.
- Self-registration (`/auth/register`) and external-login auto-provisioning always create
  **Customer** accounts; Staff/Admin only via admin-gated `POST /users/invite`.
- `/auth/forgot-password` never returns the token — the reset link is emailed via
  `PasswordResetRequestedDomainEvent` → NotificationService. Dev-only escape hatch:
  `Auth:ExposeTokensInResponseForTesting=true` (startup refuses it outside Development).
- `/auth/confirm-email` requires the emailed single-use token.
- Deactivated users / inactive tenants can't log in, verify 2FA or refresh; deactivate,
  password reset and password change revoke every refresh token + device session.
- Client IP (rate limits, brute-force counter, audit) = `RemoteIpAddress` after
  `UseForwardedHeaders`, which trusts `X-Forwarded-For` only from
  `ForwardedHeaders:KnownNetworks` — never the raw header.
- OIDC `/connect/login`: same lockout/attempt counter/IsActive/audit as REST login,
  antiforgery on both POSTs, ReturnUrl restricted to local `/connect/authorize`,
  2FA-required accounts denied (fail-closed).
- Webhooks: https + public host only, checked on save and against resolved IPs at connect
  time, no redirects.
- No shared token secret: access tokens are RS256 (signing certificate, Key Vault in
  production), OpenIddict tokens encrypted with a separate certificate; every other service
  validates via the JWKS (`docs/business/Identity/SigningKeys.md`).

### Audit Trail
- All login attempts are logged
- Role assignments are logged
- User creation/deletion is logged
- Search and filter by audit log

## Testing

### Unit Tests
```bash
dotnet test --filter "Category=Unit"
```

### Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Test Coverage
- Minimum 80% code coverage
- All business logic covered
- All API endpoints tested

## Development Notes

### Adding New Permissions
1. Add permission to `Permissions` table
2. Create migration
3. Assign to roles as needed
4. Update API documentation

### Adding New Roles
1. Create role via API
2. Assign permissions
3. Assign users to role
4. Test authorization in dependent services

### Customization
- Extend `User` entity for additional fields
- Add custom validators
- Implement additional authentication methods (LDAP, SAML)
- Add audit logging hooks

## Dependencies
- **SQL Server**: Data persistence
- **RabbitMQ**: Event publishing
- **No direct dependencies** on other microservices

## Support & Troubleshooting

### Common Issues

**Issue: Login fails with "Invalid credentials"**
- Check email is correct
- Verify password is correct
- Check user account is active
- Check email is confirmed

**Issue: Token validation fails**
- Verify JWT secret key matches in all services
- Check token hasn't expired
- Verify token structure is valid

**Issue: Permission denied errors**
- Verify user has required role
- Check role has required permission
- Verify role is assigned to user

## Related Documentation
- [Event Catalog](../../docs/architecture/event-catalog.md) - Event contracts
- [Coding Standards](../../docs/conventions/coding-standard.md) - Code style

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
