# ERP E-Commerce Solution — Complete Deployment Guide

## Overview

This guide covers setting up and running the entire ERP E-Commerce microservices platform locally for development and testing.

**System Requirements:**
- Windows 10/11 or Linux/macOS with Docker Desktop
- .NET 10 SDK
- Node.js 18+ and npm
- SQL Server 2019+ (or use Docker)
- 8GB RAM minimum, 4+ CPU cores
- 20GB free disk space

---

## 📊 Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Frontend Layer                          │
├──────────────────────┬─────────────────┬──────────────────────┤
│  Admin Dashboard     │  Customer       │  Mobile Apps         │
│  (React + Vite)      │  Storefront     │  (React Native)      │
│  Port: 3000          │  (Vue + Vite)   │  (Expo)              │
│                      │  Port: 3001     │                      │
└──────────────────────┴─────────────────┴──────────────────────┘
           ↓                    ↓                      ↓
┌──────────────────────────────────────────────────────────────────┐
│              API Gateway (Ocelot.API)                            │
│              Port: 5000 - routes to all services                │
└──────────────────────────────────────────────────────────────────┘
           ↓
┌──────────────────────────────────────────────────────────────────┐
│                    Microservices (13 services)                   │
├────────────┬──────────┬──────────┬───────────┬─────────────────┤
│ Identity   │ Product  │ Sales    │ Warehouse │ & More...       │
│ Port: 5001 │ 5008     │ 5002     │ 5004      │                 │
└────────────┴──────────┴──────────┴───────────┴─────────────────┘
           ↓
┌──────────────────────────────────────────────────────────────────┐
│         Databases (15 databases - one per service)              │
│   SQL Server 2019+ / Docker SQL Server                          │
└──────────────────────────────────────────────────────────────────┘
           ↓
┌──────────────────────────────────────────────────────────────────┐
│  Message Brokers & Event Streaming                              │
│  RabbitMQ (for commands)  │  Kafka (for events & analytics)   │
│  Port: 5672, 15672       │  Port: 9092, 9101                 │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start (5 minutes)

### 1. Database Setup

```bash
# Navigate to database directory
cd src\Database

# Run setup script (creates all 15 databases + SuperAdmin user)
.\RunAllScripts_sqlcmd.ps1

# Credentials created:
# Email: superadmin@system.local
# Password: SuperAdmin@123
```

### 2. Backend Setup

```bash
# Navigate to solution root
cd D:\Documents\ERP\Sources\ERP.E-Commerce.Solution

# Build all services
dotnet build

# Run specific service (example: Identity Service)
cd src\services\IdentityService\src\ERP.Identity.Api
dotnet run

# Or run all services via Docker (see Docker section below)
```

### 3. Frontend Setup

```bash
# Admin Dashboard (React)
cd src\frontend\admin
npm install
npm run dev
# Opens at http://localhost:3000

# Storefront (Vue) - in another terminal
cd src\frontend\storefront
npm install
npm run dev
# Opens at http://localhost:3001
```

### 4. Login to Admin Dashboard

Visit: http://localhost:3000
```
Email: superadmin@system.local
Password: SuperAdmin@123
```

---

## 🐳 Docker Deployment

### Option A: Full Docker Setup (Recommended)

Create `docker-compose.yml` in project root:

```yaml
version: '3.8'

services:
  # SQL Server Database
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: erp-sqlserver
    environment:
      SA_PASSWORD: "YourSecurePassword123!"
      ACCEPT_EULA: "Y"
      MSSQL_AGENT_ENABLED: "true"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourSecurePassword123!" -Q "SELECT 1"
      interval: 10s
      timeout: 3s
      retries: 10
    networks:
      - erp-network

  # RabbitMQ Message Broker
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: erp-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS: admin
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmqdata:/var/lib/rabbitmq
    networks:
      - erp-network

  # Kafka Event Streaming
  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    container_name: erp-zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_INIT_LIMIT: 5
    ports:
      - "2181:2181"
    volumes:
      - zookeeperdata:/var/lib/zookeeper/data
    networks:
      - erp-network

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    container_name: erp-kafka
    depends_on:
      - zookeeper
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"
    ports:
      - "9092:9092"
    volumes:
      - kafkadata:/var/lib/kafka/data
    networks:
      - erp-network

  # API Gateway (Ocelot)
  api-gateway:
    build:
      context: .
      dockerfile: src/Gateway/Ocelot.API/Dockerfile
    container_name: erp-api-gateway
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=AuthDB;User Id=sa;Password=YourSecurePassword123!;TrustServerCertificate=true;"
    depends_on:
      sqlserver:
        condition: service_healthy
    networks:
      - erp-network

  # Identity Service
  identity-service:
    build:
      context: .
      dockerfile: src/services/IdentityService/src/ERP.Identity.Api/Dockerfile
    container_name: erp-identity-service
    ports:
      - "5001:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=AuthDB;User Id=sa;Password=YourSecurePassword123!;TrustServerCertificate=true;"
    depends_on:
      sqlserver:
        condition: service_healthy
    networks:
      - erp-network

  # Admin Frontend (React)
  admin-frontend:
    build:
      context: src/frontend/admin
      dockerfile: Dockerfile
    container_name: erp-admin-frontend
    ports:
      - "3000:80"
    environment:
      VITE_API_URL: http://api-gateway:5000/api
    depends_on:
      - api-gateway
    networks:
      - erp-network

  # Storefront Frontend (Vue)
  storefront:
    build:
      context: src/frontend/storefront
      dockerfile: Dockerfile
    container_name: erp-storefront
    ports:
      - "3001:80"
    environment:
      VITE_API_URL: http://api-gateway:5000/api
    depends_on:
      - api-gateway
    networks:
      - erp-network

volumes:
  sqldata:
  rabbitmqdata:
  zookeeperdata:
  kafkadata:

networks:
  erp-network:
    driver: bridge
```

Run with:
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Option B: Local Development (Without Docker)

```bash
# 1. Start SQL Server (if using local installation)
# Ensure Windows service "MSSQL Server" is running

# 2. Start RabbitMQ (optional, for messaging)
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management

# 3. Build and run backend services
dotnet build
cd src\services\IdentityService\src\ERP.Identity.Api
dotnet run

# 4. In new terminals, run other services:
# Repeat for each service you need to develop

# 5. Run frontend (in separate terminals)
cd src\frontend\admin && npm install && npm run dev
cd src\frontend\storefront && npm install && npm run dev
```

---

## 🔧 Service Ports

| Service | Port | Type | Notes |
|---------|------|------|-------|
| API Gateway (Ocelot) | 5000 | Backend | Routes to all microservices |
| Identity Service | 5001 | Backend | Authentication & Authorization |
| Sales Order Service | 5002 | Backend | Order processing |
| Purchase Order Service | 5003 | Backend | Supplier orders |
| Warehouse Service | 5004 | Backend | Inventory management |
| Accounting Service | 5005 | Backend | General ledger & AR/AP |
| CRM Service | 5006 | Backend | Customer relationships |
| HRM Service | 5007 | Backend | Human resources |
| Product Service | 5008 | Backend | Product catalog |
| Payment Service | 5009 | Backend | Payment processing |
| Shipping Service | 5010 | Backend | Shipment tracking |
| POS Service | 5011 | Backend | Point of sale |
| Promotion Service | 5012 | Backend | Promotions & discounts |
| Notification Service | 5013 | Backend | Email/SMS notifications |
| Reporting Service | 5014 | Backend | BI & analytics |
| Admin Dashboard | 3000 | Frontend | React admin SPA |
| Customer Storefront | 3001 | Frontend | Vue storefront SPA |
| RabbitMQ | 5672 | Message Broker | Command bus |
| RabbitMQ Admin | 15672 | Admin UI | RabbitMQ management |
| Kafka | 9092 | Event Stream | Event streaming |
| SQL Server | 1433 | Database | Data persistence |

---

## 📱 Frontend Apps

### Admin Dashboard (React + TypeScript)
```bash
cd src\frontend\admin
npm install          # Install dependencies
npm run dev          # Start dev server (port 3000)
npm run build        # Build for production
npm run preview      # Preview production build
npm run lint         # Run ESLint
```

Features:
- System monitoring & analytics
- User & role management
- Master data (departments, positions, employees, units, etc.)
- Product management
- Inventory management
- Order management
- Reporting

### Customer Storefront (Vue 3 + TypeScript)
```bash
cd src\frontend\storefront
npm install          # Install dependencies
npm run dev          # Start dev server (port 3001)
npm run build        # Build for production
npm run preview      # Preview production build
```

Features:
- Product browsing & search
- Shopping cart
- Order checkout
- Order tracking
- Customer account management
- Wishlist & reviews

---

## 🔐 Environment Configuration

### Backend Services

Each backend service has `appsettings.json` that needs:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AuthDB;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "SecretKey": "your-very-long-secret-key-at-least-32-characters",
    "Issuer": "ERP.E-Commerce.Solution",
    "Audience": "ERP.E-Commerce.Client",
    "ExpiryMinutes": 15
  },
  "MessageBroker": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "erp-group"
  }
}
```

### Frontend Apps

Create `.env.local` file in each frontend directory:

**Admin (.env.local):**
```env
VITE_API_URL=http://localhost:5000/api
VITE_GOOGLE_CLIENT_ID=your-google-client-id
VITE_MSAL_CLIENT_ID=your-azure-client-id
```

**Storefront (.env.local):**
```env
VITE_API_URL=http://localhost:5000/api
```

---

## ✅ Verification Checklist

After deployment, verify everything is working:

### Databases
```sql
-- Check all 15 databases exist
SELECT name FROM sys.databases 
WHERE name LIKE '%DB' 
ORDER BY name;
```

### SuperAdmin User
```sql
USE AuthDB;
SELECT Email, UserName FROM Users 
WHERE Email = 'superadmin@system.local';
```

### Backend Services
```bash
# Test API Gateway (should return 200)
curl http://localhost:5000/api/health

# Test Identity Service
curl http://localhost:5001/api/health
```

### Frontend Apps
- Admin: http://localhost:3000 → Login with superadmin@system.local / SuperAdmin@123
- Storefront: http://localhost:3001 → Should load product catalog

---

## 🔄 Development Workflow

### Day-to-Day Development

1. **Start Infrastructure:**
   ```bash
   docker-compose up -d  # SQL Server, RabbitMQ, Kafka
   ```

2. **Run Backend Services:**
   ```bash
   # Terminal 1: API Gateway
   cd src/Gateway/Ocelot.API
   dotnet run

   # Terminal 2: Identity Service
   cd src/services/IdentityService/src/ERP.Identity.Api
   dotnet run

   # Terminal 3+: Other services as needed
   ```

3. **Run Frontend Apps:**
   ```bash
   # Terminal N: Admin
   cd src/frontend/admin
   npm run dev

   # Terminal N+1: Storefront
   cd src/frontend/storefront
   npm run dev
   ```

4. **Access Applications:**
   - Admin: http://localhost:3000
   - Storefront: http://localhost:3001
   - RabbitMQ Admin: http://localhost:15672 (admin/admin)

### Git Workflow

```bash
# Create feature branch
git checkout -b feature/your-feature

# Commit changes
git add .
git commit -m "feat: your feature description"

# Push to remote
git push origin feature/your-feature

# Create pull request and merge
```

---

## 🐛 Troubleshooting

### Issue: Port Already in Use

```bash
# Find process using port (e.g., 5000)
netstat -ano | findstr :5000

# Kill process
taskkill /PID <PID> /F

# Or use different port in appsettings.json
```

### Issue: SQL Server Connection Failed

```bash
# Check SQL Server is running
Get-Service MSSQLSERVER

# Start if not running
Start-Service MSSQLSERVER

# Test connection
sqlcmd -S localhost -U sa -P YourPassword
```

### Issue: npm Dependencies Conflict

```bash
# Clear node_modules and reinstall
rm -r node_modules
npm cache clean --force
npm install
```

### Issue: Backend APIs Not Responding

```bash
# Check API Gateway logs
curl http://localhost:5000/api/health

# Check service-specific logs
cd src/services/IdentityService/src/ERP.Identity.Api
dotnet run  # Run with console output
```

---

## 📚 Additional Resources

- **Architecture:** See [CLAUDE.md](CLAUDE.md) for service boundaries and design principles
- **Database:** See [src/Database/README.md](src/Database/README.md) for schema details
- **Frontend:** See [docs/frontend/README.md](docs/frontend/) for UI implementation guides
- **Events:** See [docs/architecture/event-catalog.md](docs/architecture/event-catalog.md) for integration events
- **API Docs:** Auto-generated Swagger at http://localhost:5000/swagger (after running)

---

## 🚀 Production Deployment

For production deployment to Kubernetes:

```bash
# Build images
docker build -t erp-api-gateway:latest -f src/Gateway/Ocelot.API/Dockerfile .
docker build -t erp-identity-service:latest -f src/services/IdentityService/src/ERP.Identity.Api/Dockerfile .

# Push to registry
docker tag erp-api-gateway:latest your-registry/erp-api-gateway:latest
docker push your-registry/erp-api-gateway:latest

# Deploy to Kubernetes
kubectl apply -f deployment/k8s/
```

See [deployment/k8s/](deployment/k8s/) for Kubernetes manifests.

---

**Status:** ✅ Ready for Development  
**Last Updated:** June 30, 2026  
**Version:** 1.0
