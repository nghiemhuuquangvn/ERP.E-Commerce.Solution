# Quick Start Guide - Code First Setup

## For Windows (Quick Setup)

### First Time Setup
```bash
cd src/services/IdentityService
.\setup.ps1 -Action setup
```

### Create New Migration
```bash
.\setup.ps1 -Action migrate -MigrationName "YourMigrationName"
```

### Update Database
```bash
.\setup.ps1 -Action update
```

---

## Run with Docker (Port 8081)

### Build image
```bash
docker build --pull --rm -f "src/services/IdentityService/Dockerfile.yml" -t "erpecommerce:latest" "src/services/IdentityService"
```

### Run container and connect to host SQL Server
```bash
docker run --name identity-api -p 8081:8081 \
  -e "ConnectionStrings__AuthDB=Server=localhost;Database=AuthDB;user id=sa;password=!Edw@rd!123456!;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True" \
  erpecommerce:latest
```

### Notes
- `Server=.` and `Server=localhost` inside container point to the container itself, not your host.
- Use SQL Server authentication (username/password). Windows Integrated Authentication is not supported in this Linux container flow.
- Ensure SQL Server accepts TCP/IP on port `1433` and allows remote connections.

---

## For Linux/Mac (Quick Setup)

### First Time Setup
```bash
cd src/services/IdentityService
chmod +x setup.sh
./setup.sh setup
```

### Create New Migration
```bash
./setup.sh migrate YourMigrationName
```

### Update Database
```bash
./setup.sh update
```

---

## For Windows Command Prompt

### First Time Setup
```bash
cd src\services\IdentityService
setup.bat setup
```

### Create New Migration
```bash
setup.bat migrate YourMigrationName
```

### Update Database
```bash
setup.bat update
```

---

## Manual Commands (If Scripts Don't Work)

### Install EF Tools
```bash
dotnet tool install --global dotnet-ef
```

### Initial Setup
```bash
cd src/services/IdentityService/ERP.Identity.Infrastructure

# First time only - create migration
dotnet ef migrations add "InitialCreate" \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj

# Apply migration to create database
dotnet ef database update \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

### Create New Migration
```bash
dotnet ef migrations add "YourMigrationName" \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj

dotnet ef database update \
  --project ERP.Identity.Infrastructure.csproj \
  --startup-project ../ERP.Identity.Api/ERP.Identity.Api.csproj
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `dotnet ef` not found | Run: `dotnet tool install --global dotnet-ef` |
| Connection string failed | Update `appsettings.Development.json` with correct server/database |
| Migrations folder missing | First migration will create it automatically |
| Database already exists | Use `setup.bat reset` or `./setup.sh reset` to drop and recreate |

---

## Database Connection String

Edit `appsettings.Development.json`:

### Windows Authentication
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=AuthDB;Integrated Security=true;TrustServerCertificate=true;"
}
```

### SQL Server Authentication
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=AuthDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;"
}
```

---

## Running on New Device

1. Clone repository
2. Update connection string in `appsettings.Development.json`
3. Run setup script (or manual commands above)
4. Run `dotnet run` in `ERP.Identity.Api` directory

That's it! Your database will be created automatically.

---

## Key Database Tables

- **Users** - User accounts
- **Roles** - User roles
- **Permissions** - System permissions
- **UserRoles** - User-to-role mapping
- **RolePermissions** - Role-to-permission mapping
- **RefreshTokens** - JWT refresh tokens
- **ExternalLogins** - External provider logins
- **EmailConfirmTokens** - Email confirmation tokens
- **Tenants** - Multi-tenant support
- **OutboxMessages** - Domain events outbox

---

## Notes

- All scripts are idempotent (safe to run multiple times)
- Timestamps are stored in UTC
- Soft deletes supported via `IsDeleted` field
- Audit fields managed automatically
- Migrations create clean database schema

See `CODE_FIRST_SETUP.md` for detailed documentation.
