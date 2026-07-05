# Quick Start Guide — ERP E-Commerce Solution

Get the entire platform running in 5-10 minutes!

---

## ✅ Prerequisites

- ✅ Databases: 15 databases created with SuperAdmin user
- ✅ Node.js 18+: v24.18.0 installed
- ✅ npm: 11.16.0 installed
- ✅ Dependencies: All installed (admin ✅, storefront ✅)
- ✅ .NET 10 SDK: For backend services

**Verified:**
- 15 SQL databases created successfully
- Admin frontend dependencies installed ✅
- Storefront frontend dependencies installed ✅
- SuperAdmin user created in AuthDB ✅

---

## 🚀 Running Everything (5 minutes)

### Option 1: Frontend Only (Recommended for UI Development)

This runs the frontend applications connecting to backend APIs.

#### Terminal 1: Admin Dashboard (React)
```bash
cd src\frontend\admin
npm run dev
```
**Opens:** http://localhost:3000

#### Terminal 2: Storefront (Vue)
```bash
cd src\frontend\storefront
npm run dev
```
**Opens:** http://localhost:3001

**Status:** Frontend running but needs backend services
- Admin will show API connection error until API Gateway is running
- Both apps proxy requests to `http://localhost:5000/api`

---

### Option 2: Full Stack Development (Recommended)

#### Step 1: Start Backend API Gateway

```bash
cd src\Gateway\Ocelot.API
dotnet run
```
**Port:** 5000  
**Status:** Routes requests to microservices

#### Step 2: Start at Least One Backend Service (Identity Service)

In a new terminal:
```bash
cd src\services\IdentityService\src\ERP.Identity.Api
dotnet run
```
**Port:** 5001  
**Status:** Handles authentication

#### Step 3: Start Frontend Apps (in separate terminals)

```bash
# Terminal 3
cd src\frontend\admin
npm run dev
# Opens http://localhost:3000

# Terminal 4
cd src\frontend\storefront
npm run dev
# Opens http://localhost:3001
```

---

### Option 3: Docker Deployment (Production-like)

See [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) for full docker-compose setup.

```bash
# Build and run all services
docker-compose up -d

# View logs
docker-compose logs -f

# Access applications
# Admin: http://localhost:3000
# Storefront: http://localhost:3001
```

---

## 🔐 Login Credentials

### SuperAdmin Account (Created during database setup)

```
Email:    superadmin@system.local
Password: SuperAdmin@123
TenantCode: (optional, system default)
```

**Permissions:**
- View System Monitoring
- Manage Users
- Manage Roles
- Full System Administrator Access

**First Steps:**
1. Login to http://localhost:3000
2. Navigate to Settings
3. Create a new admin user with your credentials
4. Change SuperAdmin password (Settings → Security)

---

## 📝 Current Application Status

### Database Setup ✅
- **Status:** All 15 databases created successfully
- **Location:** SQL Server (localhost:1433)
- **SuperAdmin:** Created with full permissions
- **Script:** Completed in 0.3 minutes

```
✅ AccountingDB
✅ AuthDB
✅ CrmDB
✅ HrmDB
✅ MasterDataDB
✅ NotificationDB
✅ PaymentDB
✅ PosDB
✅ ProductDb
✅ PromotionDB
✅ PurchasingDB
✅ ReportingDB
✅ SalesDB
✅ ShippingDB
✅ WarehouseDB
```

### Frontend Setup ✅
- **Admin:** React 18 + TypeScript (Port 3000)
  - Dependencies: ✅ Installed
  - Status: Ready to run `npm run dev`

- **Storefront:** Vue 3.5 + TypeScript (Port 3001)
  - Dependencies: ✅ Installed
  - Status: Ready to run `npm run dev`

### Backend Services
- **API Gateway:** Ready to run (Port 5000)
- **13 Microservices:** Ready to run (Ports 5001-5014)
- **Database connections:** All configured in appsettings.json
- **Authentication:** JWT-based via Identity Service

---

## 🎯 What You Can Do Now

### Immediately (Frontend Only)
1. Run `npm run dev` in admin and storefront directories
2. Browse the UI without login (backend connection required for actions)
3. Review component structure and styling
4. Modify frontend code and see hot-reload

### After Starting Backend
1. Login to admin dashboard with SuperAdmin credentials
2. Create new users and assign roles/permissions
3. Browse products and place orders
4. Monitor system in real-time
5. Review analytics and reports

### Development Tasks
1. **Refactor frontend:** See [FRONTEND_REFACTORING.md](FRONTEND_REFACTORING.md)
2. **Backend work:** See [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
3. **Add new features:** Follow patterns in existing code
4. **Database changes:** See [src/Database/README.md](src/Database/README.md)

---

## 🔧 Common Commands

### Frontend (Admin)
```bash
cd src/frontend/admin

npm run dev          # Start dev server (http://localhost:3000)
npm run build        # Build for production
npm run preview      # Preview production build
npm run lint         # Run ESLint
npm install          # Install/update dependencies
```

### Frontend (Storefront)
```bash
cd src/frontend/storefront

npm run dev          # Start dev server (http://localhost:3001)
npm run build        # Build for production
npm run preview      # Preview production build
npm install          # Install/update dependencies
```

### Backend (Example: Identity Service)
```bash
cd src/services/IdentityService/src/ERP.Identity.Api

dotnet run           # Run the service
dotnet build         # Build the project
dotnet test          # Run tests
dotnet publish       # Publish for deployment
```

### Database
```bash
cd src/Database

# Re-run all database scripts
.\RunAllScripts_sqlcmd.ps1

# Run individual script
sqlcmd -S localhost -i Identity\001_CreateAuthDB.sql
```

---

## 🚦 Troubleshooting Quick Fixes

### Port Already in Use
```bash
# Find process using port 3000
netstat -ano | findstr :3000

# Kill process
taskkill /PID <PID> /F

# Or use different port in vite.config.ts
```

### npm ERR: Cannot find module
```bash
# Clean and reinstall
rm -r node_modules package-lock.json
npm install
```

### Backend API not responding
```bash
# Check API Gateway is running on port 5000
curl http://localhost:5000/api/health

# Check service logs
cd src/services/IdentityService/src/ERP.Identity.Api
dotnet run  # Run with console output
```

### Database connection error
```bash
# Check SQL Server is running
Get-Service MSSQLSERVER

# Start if not running
Start-Service MSSQLSERVER

# Verify databases exist
sqlcmd -S localhost -Q "SELECT name FROM sys.databases WHERE name LIKE '%DB' ORDER BY name;"
```

---

## 📚 Documentation Map

| Document | Purpose |
|----------|---------|
| **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** | Complete setup for all services, Docker, production |
| **[FRONTEND_REFACTORING.md](FRONTEND_REFACTORING.md)** | Frontend modernization, best practices, testing |
| **[DATABASE_SETUP_SUMMARY.md](DATABASE_SETUP_SUMMARY.md)** | Database creation details, schemas, verification |
| **[src/Database/README.md](src/Database/README.md)** | Database architecture, backup, maintenance |
| **[src/Database/SETUP_GUIDE.md](src/Database/SETUP_GUIDE.md)** | Step-by-step database setup |
| **[CLAUDE.md](CLAUDE.md)** | Architecture overview, service boundaries, coding standards |
| **[docs/architecture/](docs/architecture/)** | Event catalog, domain maps, service dependencies |
| **[docs/frontend/](docs/frontend/)** | Frontend patterns, i18n, CRUD conventions |

---

## 🎓 Learning Path

**New to the project?** Follow this learning path:

1. **Understanding:** Read [CLAUDE.md](CLAUDE.md) (architecture overview)
2. **Setup:** Follow this [QUICK_START.md](QUICK_START.md) guide
3. **Frontend:** Review [docs/frontend/](docs/frontend/) patterns
4. **Backend:** Study [docs/architecture/event-catalog.md](docs/architecture/event-catalog.md)
5. **Database:** Understand schema in [src/Database/README.md](src/Database/README.md)
6. **Development:** Follow patterns in existing code

---

## ✨ Next Immediate Steps

1. **Start frontend apps:**
   ```bash
   # Terminal 1: Admin
   cd src\frontend\admin && npm run dev
   
   # Terminal 2: Storefront  
   cd src\frontend\storefront && npm run dev
   ```

2. **Start API Gateway:**
   ```bash
   cd src\Gateway\Ocelot.API && dotnet run
   ```

3. **Start Identity Service:**
   ```bash
   cd src\services\IdentityService\src\ERP.Identity.Api && dotnet run
   ```

4. **Access Applications:**
   - Admin: http://localhost:3000
   - Storefront: http://localhost:3001
   - RabbitMQ: http://localhost:15672 (guest/guest)

5. **Login with:**
   - Email: `superadmin@system.local`
   - Password: `SuperAdmin@123`

---

## 💡 Pro Tips

- **Hot Reload:** Frontend changes reload automatically (Vite)
- **API Proxying:** Frontend routes `/api/*` to backend on port 5000
- **Zustand Devtools:** Use Redux DevTools to inspect state (Admin)
- **Vue DevTools:** Install browser extension for Vue debugging (Storefront)
- **Network Tab:** Check browser DevTools → Network for API calls
- **SQL Management:** Use SQL Server Management Studio to inspect databases

---

## 🎉 You're All Set!

Everything is configured and ready to run. Start with the commands above and you'll have:
- ✅ Frontend: React Admin + Vue Storefront running
- ✅ Backend: API Gateway + Microservices ready
- ✅ Database: 15 databases with sample data
- ✅ Authentication: SuperAdmin user ready to use

**Happy coding!** 🚀

---

**Last Updated:** June 30, 2026  
**Status:** ✅ Ready for Development  
**Version:** 1.0
