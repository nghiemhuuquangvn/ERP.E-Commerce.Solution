# Development Environment Setup Guide

Complete guide for setting up the ERP E-Commerce Solution development environment.

## Prerequisites

### System Requirements

- **OS**: Windows 10/11 or Linux/macOS with WSL2
- **RAM**: Minimum 8GB (16GB recommended)
- **Disk Space**: Minimum 20GB free space
- **CPU**: Dual-core processor (4+ cores recommended)

### Required Software

1. **.NET 10 SDK** (Latest)
   - Download: https://dotnet.microsoft.com/download
   - Verify: `dotnet --version`

2. **SQL Server 2019 or Later**
   - Download: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Or use Docker: `docker run -e SA_PASSWORD=YourPassword123 -d mcr.microsoft.com/mssql/server`

3. **Docker & Docker Compose**
   - Download: https://www.docker.com/products/docker-desktop
   - Verify: `docker --version` && `docker-compose --version`

4. **Git**
   - Download: https://git-scm.com/
   - Verify: `git --version`

5. **Visual Studio 2022 Community** (Recommended)
   - Download: https://visualstudio.microsoft.com/downloads/
   - Or VS Code: https://code.visualstudio.com/

6. **RabbitMQ** (via Docker)
   ```bash
   docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
   ```

7. **Kafka** (optional, via Docker)
   ```bash
   docker-compose up -d kafka zookeeper
   ```

## Repository Setup

### 1. Clone Repository

```bash
git clone https://github.com/your-org/erp-ecommerce-solution.git
cd erp-ecommerce-solution
```

### 2. Verify Structure

```bash
# Check main directories exist
ls -la src/services/
ls -la src/Shared/
ls -la docs/
```

## Database Setup

### 1. Create Databases

```sql
-- Connect to SQL Server
USE master;

-- Identity Database
CREATE DATABASE IdentityDB;

-- Sales Database
CREATE DATABASE SalesDB;

-- Purchasing Database
CREATE DATABASE PurchasingDB;

-- Warehouse Database
CREATE DATABASE WarehouseDB;
```

### 2. Apply Migrations

```bash
# Navigate to each service and apply migrations

# Identity Service
cd src/services/IdentityService
dotnet ef database update

# Sales Service
cd ../SaleOrderService
dotnet ef database update

# Purchasing Service
cd ../PurchaseOrderService
dotnet ef database update

# Warehouse Service
cd ../WareHouseService
dotnet ef database update
```

### 3. Seed Initial Data

```bash
# Optional: Run seed scripts
dotnet run -- --seed
```

## Infrastructure Setup

### 1. Start Message Brokers

#### RabbitMQ
```bash
docker run -d \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:3-management

# Access management UI: http://localhost:15672
# Username: guest, Password: guest
```

#### Kafka (Optional)
```bash
# Use docker-compose
docker-compose -f deployment/docker/kafka-compose.yml up -d
```

### 2. Verify Connectivity

```bash
# Test RabbitMQ
curl -u guest:guest http://localhost:15672/api/whoami

# Test SQL Server
sqlcmd -S localhost -U sa -P YourPassword123 -Q "SELECT @@VERSION"
```

## Application Setup

### 1. Build Solution

```bash
# Restore all dependencies
dotnet restore

# Build solution
dotnet build

# Verify build succeeds
echo $?  # Should be 0
```

### 2. Configuration Files

Create `appsettings.Development.json` in each service:

#### IdentityService
```json
{
  "ConnectionStrings": {
    "IdentityDatabase": "Server=localhost;Database=IdentityDB;User Id=sa;Password=YourPassword123;"
  },
  "Jwt": {
    "SecretKey": "your-very-long-secret-key-here-minimum-32-characters-for-security",
    "Issuer": "https://erp-identity-service",
    "Audience": "erp-api",
    "ExpirationMinutes": 60
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

#### SalesOrderService
```json
{
  "ConnectionStrings": {
    "SalesDatabase": "Server=localhost;Database=SalesDB;User Id=sa;Password=YourPassword123;"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "ServiceUrls": {
    "IdentityService": "http://localhost:5001",
    "WarehouseService": "http://localhost:5004"
  }
}
```

Similar configuration for other services (update port numbers accordingly).

### 3. Run Individual Services

```bash
# Terminal 1: Identity Service
cd src/services/IdentityService
dotnet run
# Runs on http://localhost:5001

# Terminal 2: Sales Service
cd src/services/SaleOrderService
dotnet run
# Runs on http://localhost:5002

# Terminal 3: Purchasing Service
cd src/services/PurchaseOrderService
dotnet run
# Runs on http://localhost:5003

# Terminal 4: Warehouse Service
cd src/services/WareHouseService
dotnet run
# Runs on http://localhost:5004
```

## API Gateway Setup

### 1. Configure Ocelot

Edit `src/ApiGateway/ocelot.json`:
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/{path}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5001 }
      ],
      "UpstreamPathTemplate": "/identity/api/{path}",
      "UpstreamHttpMethod": [ "Get", "Post", "Put", "Delete" ]
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

### 2. Run API Gateway

```bash
cd src/ApiGateway
dotnet run
# Runs on http://localhost:5000
```

## Development Tools

### 1. Install Tools (Optional)

```bash
# EF Core Tools
dotnet tool install --global dotnet-ef

# User Secrets (for sensitive data)
dotnet user-secrets init

# Watch Mode
dotnet watch run
```

### 2. IDE Setup

#### Visual Studio 2022
- Install workload: ASP.NET and web development
- Install extension: EntityFramework Power Tools
- Install extension: Resharper (optional)

#### VS Code
- Install extension: C# Extension Pack
- Install extension: REST Client
- Create `.vscode/launch.json` for debugging

## Testing Setup

### 1. Unit Tests

```bash
# Run all unit tests
dotnet test --filter "Category=Unit"

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### 2. Integration Tests

```bash
# Run integration tests (requires databases)
dotnet test --filter "Category=Integration"
```

### 3. Generate Coverage Report

```bash
# Install coverlet
dotnet add package coverlet.collector

# Generate report
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Docker Compose Setup

### 1. Create docker-compose.yml

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      SA_PASSWORD: YourPassword123
      ACCEPT_EULA: Y
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq

  identity-service:
    build: ./src/services/IdentityService
    ports:
      - "5001:80"
    environment:
      ConnectionStrings__IdentityDatabase: "Server=sqlserver;Database=IdentityDB;..."
    depends_on:
      - sqlserver
      - rabbitmq

  sales-service:
    build: ./src/services/SaleOrderService
    ports:
      - "5002:80"
    environment:
      ConnectionStrings__SalesDatabase: "Server=sqlserver;Database=SalesDB;..."
    depends_on:
      - sqlserver
      - rabbitmq

  api-gateway:
    build: ./src/ApiGateway
    ports:
      - "5000:80"
    depends_on:
      - identity-service
      - sales-service

volumes:
  sqlserver-data:
  rabbitmq-data:
```

### 2. Run Everything

```bash
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down

# Clean up volumes
docker-compose down -v
```

## Verify Installation

### Health Checks

```bash
# Test Identity Service
curl http://localhost:5001/api/v1/health

# Test Sales Service
curl http://localhost:5002/api/v1/health

# Test API Gateway
curl http://localhost:5000/health

# Test RabbitMQ
curl -u guest:guest http://localhost:15672/api/whoami

# Test SQL Server
sqlcmd -S localhost -U sa -P YourPassword123 -Q "SELECT @@VERSION"
```

### First API Call

```bash
# Register a user
curl -X POST http://localhost:5000/identity/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePassword123!",
    "firstName": "John",
    "lastName": "Doe"
  }'

# Expected response: 201 Created with user details
```

## Troubleshooting

### Port Already in Use

```bash
# Find process using port
lsof -i :5000

# Kill process
kill -9 <PID>

# Or change port in appsettings.json
```

### Database Connection Failed

```bash
# Verify SQL Server is running
docker ps | grep mssql

# Check connection string
# Make sure: localhost:1433 is accessible
# Username: sa
# Password: matches in docker-compose.yml
```

### RabbitMQ Connection Failed

```bash
# Verify RabbitMQ is running
docker ps | grep rabbitmq

# Test connection
telnet localhost 5672
```

### Migration Fails

```bash
# Check pending migrations
dotnet ef migrations list

# Revert to previous migration
dotnet ef database update <PreviousMigration>

# Add new migration
dotnet ef migrations add <MigrationName>

# Update database
dotnet ef database update
```

## Useful Commands

```bash
# Watch for changes and rebuild
dotnet watch run

# Run with specific configuration
dotnet run --configuration Release

# Run tests with output
dotnet test --verbosity detailed

# Generate Entity Framework migration
dotnet ef migrations add <MigrationName>

# Generate code-first migration
dotnet ef migrations add -o Migrations/<ServiceName>/<MigrationName>
```

## Next Steps

1. Review [CLAUDE.md](../../CLAUDE.md) for project guidelines
2. Read [Service Map](./service-map.md) to understand architecture
3. Review [Event Catalog](./event-catalog.md) for integration events
4. Read individual service READMEs in `src/services/*/README.md`
5. Check out [Coding Standards](../conventions/coding-standard.md)
6. Start with creating a simple feature in one service

## Support

- **Issues**: Check [troubleshooting](#troubleshooting) section
- **Documentation**: See docs/ folder
- **Architecture**: See docs/architecture/ folder
- **Code Review**: Follow coding standards in docs/conventions/

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
