# Implementation Guide: Scaffolding Remaining Services

This guide explains how to scaffold the remaining 6 services (HRM, PaymentService, ShippingService, POSService, PromotionService, NotificationService, ReportingService) following the pattern established by **ProductService** and **AccountingService/CRMService**.

## Pattern Overview

Each service follows this structure:

```
src/services/{ServiceName}/
├── {ServiceName}.sln
├── README.md
└── src/
    ├── ERP.{CODE}.API/
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── appsettings.Docker.json
    │   ├── Controllers/ (placeholder)
    │   └── ERP.{CODE}.API.csproj
    ├── ERP.{CODE}.Application/
    │   ├── DependencyInjection.cs
    │   ├── Features/ (Commands, Queries, Handlers)
    │   └── ERP.{CODE}.Application.csproj
    ├── ERP.{CODE}.Domain/
    │   ├── Entities/ (Domain aggregates)
    │   ├── Enums/
    │   ├── Events/ (Domain events)
    │   ├── ValueObjects/
    │   └── ERP.{CODE}.Domain.csproj
    └── ERP.{CODE}.Infrastructure/
        ├── DependencyInjection.cs
        ├── Persistence/
        │   ├── {Name}DbContext.cs
        │   ├── Configurations/ (EF entity configs)
        │   └── Migrations/
        ├── BackgroundJobs/
        │   └── OutboxProcessor.cs
        ├── MessageBus/
        │   ├── KafkaMessageBus.cs
        │   └── IntegrationEvents/
        ├── Repositories/
        └── ERP.{CODE}.Infrastructure.csproj
```

## Service Codes & Ports

| Service | Code | Port | Database | Status |
|---------|------|------|----------|--------|
| ProductService | PRD | 5008 | ProductDB | ✅ Done |
| AccountingService | ACC | 5005 | AccountingDB | ✅ Done |
| CRMService | CRM | 5006 | CrmDB | ✅ Done |
| HRMService | HRM | 5007 | HrmDB | ⏳ TODO |
| PaymentService | PAY | 5009 | PaymentDB | ⏳ TODO |
| ShippingService | SHP | 5010 | ShippingDB | ⏳ TODO |
| POSService | POS | 5011 | PosDB | ⏳ TODO |
| PromotionService | PRM | 5012 | PromotionDB | ⏳ TODO |
| NotificationService | NTF | 5013 | NotificationDB | ⏳ TODO |
| ReportingService | RPT | 5014 | ReportingDB | ⏳ TODO |

## Step-by-Step Scaffolding (For Each Service)

### 1. Create Solution File
Copy the pattern from `AccountingService.sln`:
```
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "ERP.{CODE}.API", "src\ERP.{CODE}.API\ERP.{CODE}.API.csproj", "{GUID}"
...
```
**GUID Generation:** Use `[System.Guid]::NewGuid()` in PowerShell for each project GUID.

### 2. Create README.md
Template from docs (see each service READMEs at `docs/services/{NAME}/`):

Key sections:
- **Overview** — What the service does
- **Core Domain Model** — Aggregates, entities, enums
- **Key Business Rules** — Critical invariants
- **Database** — Database name + key tables
- **Integration Events** — Published & consumed events
- **API Endpoints** — REST endpoint listing
- **Dependencies** — Services it depends on
- **Technology Stack**

### 3. Create .csproj Files (4 files per service)

**Pattern for all .csproj files:**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web"> <!-- .Web for API, .Sdk for others -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Optional for non-API: <GenerateDocumentationFile>true</GenerateDocumentationFile> -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Add package references -->
  </ItemGroup>

  <ItemGroup>
    <!-- Add project references to Shared -->
  </ItemGroup>
</Project>
```

**Reference:** See ProductService `.csproj` files for exact package versions:
- API: `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`
- Application: `MediatR`, `FluentValidation`
- Domain: No external dependencies
- Infrastructure: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Confluent.Kafka`

**Project References (all services):**
- All reference `ERP.Shared.Core`, `ERP.Shared.Messaging`, `ERP.Shared.Contracts`
- API also references Application + Infrastructure
- Application references Domain + Shared
- Infrastructure references Domain + Shared

### 4. Create Program.cs (API Layer)

Copy from ProductService and update:
```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
```

Only change the service titles in Swagger:
```csharp
options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "{Service Name} API",
    Version = "v1",
    Description = "..."
});
```

### 5. Create appsettings Files

Copy from ProductService (3 files: `appsettings.json`, `.Development.json`, `.Docker.json`)

Update only:
```json
{
  "ConnectionStrings": {
    "{ServiceName}DB": "Server=localhost;Database={ServiceName}DB;..."
  }
}
```

### 6. Create Domain Layer

**Minimum files:**
- `Domain/Entities/{MainAggregate}.cs` — Main aggregate root (e.g., Employee.cs for HRM)
- `Domain/Enums/` — Status, type enums
- `Domain/Events/` — {AggregateRoot}{Action}DomainEvent.cs

Use ProductService domain entities as template:
- Inherit from `AggregateRoot` for aggregate roots
- Use `BaseEntity` for child entities
- Implement `Create()` factory method
- Call `AddDomainEvent()` for state changes

### 7. Create Application Layer

**Single file: `DependencyInjection.cs`**

Copy from ProductService exactly:
```csharp
services.AddMediatR(config => 
    config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
```

### 8. Create Infrastructure Layer

**DependencyInjection.cs:**
- Register DbContext with SqlServer
- Configure Kafka ProducerConfig
- Register IMessageBus implementation
- Register OutboxProcessor as hosted service

**{ServiceName}DbContext.cs:**
- Inherit from DbContext
- Override `OnModelCreating()` to apply entity configs
- Override `SaveChangesAsync()` to:
  - Collect domain events from entities
  - Serialize to OutboxMessages
  - Auto-clear events after save

**OutboxProcessor.cs:**
- Implement IHostedService
- Poll every 5 seconds
- Deserialize events, publish to Kafka
- Mark as processed

**KafkaMessageBus.cs:**
- Implement IMessageBus
- Publish to `{service-name}-topic`

### 9. Create Minimal Folder Structure

Ensure these folders exist (can be empty):
- `src/ERP.{CODE}.API/Controllers/`
- `src/ERP.{CODE}.Application/Features/`
- `src/ERP.{CODE}.Domain/Entities/`
- `src/ERP.{CODE}.Domain/Enums/`
- `src/ERP.{CODE}.Domain/Events/`
- `src/ERP.{CODE}.Infrastructure/Persistence/Configurations/`
- `src/ERP.{CODE}.Infrastructure/MessageBus/IntegrationEvents/`
- `src/ERP.{CODE}.Infrastructure/Repositories/`

## Integration Events Pattern

When creating integration events for your service:

**File:** `Infrastructure/MessageBus/IntegrationEvents/{EventName}.cs`

```csharp
public class {AggregateRoot}{Action}IntegrationEvent : IntegrationEventBase
{
    public Guid {AggregateRootId} { get; set; }
    // Other relevant fields
    
    public {AggregateRoot}{Action}IntegrationEvent(
        Guid {aggregateRootId}, 
        /* other params */)
    {
        {AggregateRootId} = {aggregateRootId};
        // Set other fields
    }
}
```

Naming: `{AggregateRoot}{Action}IntegrationEvent`
- ✅ UserCreatedIntegrationEvent
- ✅ SalesOrderApprovedIntegrationEvent
- ❌ UserCreationIntegrationEvent (use verb form)

## Service Dependencies Checklist

Before finalizing a service, verify:
- [ ] All consumed events are documented in README
- [ ] All published events are documented in README
- [ ] DbContext auto-includes Outbox tables
- [ ] OutboxProcessor is registered as hosted service
- [ ] Kafka topic name matches pattern: `{service-name}-topic`
- [ ] Port is unique (check port list above)
- [ ] Database name follows pattern: `{ServiceName}DB`
- [ ] All .csproj files reference shared libraries correctly

## Example: HRMService Checklist

```
src/services/HRMService/
├── HRMService.sln                              (Copy from ACC pattern)
├── README.md                                    (Use template from docs)
└── src/
    ├── ERP.HRM.API/
    │   ├── Program.cs                          (Copy from PRD, update title)
    │   ├── appsettings*.json                   (Copy from PRD, change DB name)
    │   ├── Controllers/                        (empty folder)
    │   └── ERP.HRM.API.csproj
    ├── ERP.HRM.Application/
    │   ├── DependencyInjection.cs              (Copy exactly from PRD)
    │   ├── Features/                           (empty folder)
    │   └── ERP.HRM.Application.csproj
    ├── ERP.HRM.Domain/
    │   ├── Entities/Employee.cs                (Create main aggregate)
    │   ├── Enums/EmploymentStatus.cs           (Create enum)
    │   ├── Events/EmployeeHiredDomainEvent.cs  (Create domain events)
    │   └── ERP.HRM.Domain.csproj
    └── ERP.HRM.Infrastructure/
        ├── DependencyInjection.cs              (Update DB config)
        ├── Persistence/HrmDbContext.cs         (Create context)
        ├── BackgroundJobs/OutboxProcessor.cs   (Copy exactly from PRD)
        ├── MessageBus/KafkaMessageBus.cs       (Copy exactly from PRD)
        └── ERP.HRM.Infrastructure.csproj
```

## Key Files to Copy Exactly

These files can be copied with minimal changes (just class/namespace names):
1. ✅ **OutboxProcessor.cs** — No service-specific logic
2. ✅ **KafkaMessageBus.cs** — No service-specific logic
3. ✅ **Application/DependencyInjection.cs** — Just copy as-is
4. ✅ **Program.cs** — Update Swagger title only

## Files That Need Custom Content

These require service-specific business logic:
1. 🔧 **Domain/Entities/** — Domain model specific to service
2. 🔧 **Domain/Enums/** — Status/type enums for aggregates
3. 🔧 **Domain/Events/** — Events published by aggregates
4. 🔧 **Infrastructure/{ServiceName}DbContext.cs** — DbSet properties + entity configs

---

**Quick Reference:** For questions on what domain events/entities to create, see the README.md for each service in `docs/services/{ServiceName}/` or check the kéy referenced from docs/architecture/event-catalog.md.
