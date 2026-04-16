# Code First Setup Guide

This project uses Entity Framework Core Code First approach. Follow these steps to set up on any device.

## Prerequisites

- .NET 9.0 SDK installed
- SQL Server (local or remote)
- Visual Studio Code or Visual Studio
- PowerShell or Command Prompt

## Initial Setup on New Device

### 1. Configure Connection String

Update the connection string in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=AuthDB;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

### 2. Install EF Core CLI Tools (First time only)

```bash
dotnet tool install --global dotnet-ef
```

Or update existing tools:

```bash
dotnet tool update --global dotnet-ef
```

### 3. Create Initial Database

Navigate to the Infrastructure project directory:

```bash
cd src/services/IdentityService/ERP.Identity.Infrastructure
```

#### Option A: Using Migrations (Recommended)

Create initial migration:

```bash
dotnet ef migrations add "InitialCreate" --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

Apply migration to database:

```bash
dotnet ef database update --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

#### Option B: Direct Database Creation

```bash
dotnet ef database update --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

### 4. Verify Database Creation

Check SQL Server to confirm tables were created:
- Users
- Roles
- Permissions
- UserRoles
- RolePermissions
- RefreshTokens
- ExternalLogins
- EmailConfirmTokens
- OutboxMessages
- Tenants

## Running on Different Devices

### On Development Machine 2+

1. **Copy Project**
   ```bash
   git clone <repo-url>
   cd ERP.E-Commerce.Solution
   ```

2. **Configure Connection String**
   - Update connection string in `appsettings.Development.json`

3. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

4. **Apply Migrations**
   ```bash
   cd src/services/IdentityService/ERP.Identity.Infrastructure
   dotnet ef database update --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
   ```

5. **Run Application**
   ```bash
   cd ../ERP.Identity.Api
   dotnet run
   ```

## Creating New Migrations

After modifying entities, create a new migration:

```bash
cd src/services/IdentityService/ERP.Identity.Infrastructure

dotnet ef migrations add "DescriptiveMigrationName" \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

Then apply:

```bash
dotnet ef database update \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

## Troubleshooting

### Migration Command Not Found

Ensure you're in the correct directory and the tools are installed:

```bash
dotnet ef --version
```

### Connection String Fails

- Check SQL Server is running
- Verify server name is correct
- Ensure database name is correct
- Check authentication mode (Windows/SQL)

### Migrations Folder Not Found

If migrations don't exist, create initial migration:

```bash
dotnet ef migrations add "InitialCreate" --idempotent
```

### Database Already Exists

To reset database:

```bash
dotnet ef database drop --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
dotnet ef database update --project ERP.Identity.Infrastructure.csproj --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

## Entity Structure

### BaseEntity Properties (Inherited by all entities)
- `Id` (Guid) - Primary Key
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime)
- `CreatedBy` (string)
- `UpdatedBy` (string)
- `IsDeleted` (bool)
- `DomainEvents` (List)

### Main Entities

1. **User**
   - Email, UserName, FullName, PasswordHash
   - IsActive, IsEmailConfirmed, IsPhoneConfirmed

2. **Role**
   - RoleCode, RoleName, IsActive

3. **Permission**
   - PermissionCode, PermissionName, Module

4. **RefreshToken**
   - UserId, TenantId, Token, ExpiredAt
   - RevokedAt, CreatedByIp, IsActive

5. **EmailConfirmToken**
   - UserId, Token, ExpiredAt
   - ConfirmedAt

6. **ExternalLogin**
   - UserId, Provider, ProviderKey, Email

## Database Schema

The database uses SQL Server with the following key relationships:

- Users → UserRoles → Roles
- Roles → RolePermissions → Permissions
- Users → RefreshTokens
- Users → ExternalLogins
- Users → EmailConfirmTokens
- Users → UserTenants → Tenants

## Notes

- All timestamps are stored in UTC
- Soft deletes are supported through `IsDeleted` field
- Domain events are captured and stored in OutboxMessages
- Audit fields (CreatedBy, UpdatedBy, CreatedAt, UpdatedAt) are automatically managed
