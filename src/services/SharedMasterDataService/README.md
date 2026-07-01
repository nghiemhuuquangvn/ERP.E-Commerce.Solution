# Shared Master Data Service

Cross-cutting reference data owned by a single authoritative service for the ERP E-Commerce Solution: organizational structure (Department/Position/Employee), units of measure, geography (Country → Province → District → Ward), and supported languages.

## Overview

The Shared Master Data Service centralizes reference data that multiple services need but no single business-domain service should own:
- **Departments** — Organizational structure and hierarchy
- **Positions** — Job titles/roles, optionally scoped to a department
- **Employees** — Employee master records and employment lifecycle (moved here from HRMService)
- **Units of Measure** — Weight, volume, length, count, area units with conversion factors
- **Geography** — Country, Province, District, Ward hierarchy
- **Supported Languages** — Admin-managed language list for i18n (Code, Name, NativeName, IsDefault, IsActive, SortOrder)

## Core Domain Model

### Aggregates

**Department** (Aggregate Root)
- DepartmentName, Code, ManagerId?, ParentDepartmentId?
- Raises `DepartmentCreatedEvent` / `DepartmentUpdatedEvent`

**Position** (Aggregate Root)
- Title, Code, Description?, DepartmentId?, IsActive
- Raises `PositionCreatedEvent`

**Employee** (Aggregate Root)
- EmployeeNumber (format `EMP-{year}-{random4}`), FirstName, LastName, Email?, Phone?
- DateOfHire, DepartmentId, PositionId?, EmploymentStatus, EmploymentType
- Raises `EmployeeHiredEvent`, `EmployeeTerminatedEvent`, `EmployeeTransferredEvent`

**UnitOfMeasure** (Aggregate Root)
- Code, Name, Symbol, UnitType (Weight/Volume/Length/Count/Area), BaseUnitId?, ConversionFactor
- Raises `UnitOfMeasureCreatedEvent`

**Country** (Aggregate Root)
- Code (ISO alpha-2), Name, PhoneCode?
- Raises `CountryCreatedEvent`

**Province / District / Ward** (Aggregate Roots)
- Hierarchical FKs (CountryId → Province, ProvinceId → District, DistrictId → Ward), Code, Name
- Low-churn seed data — no domain events

**SupportedLanguage** (Aggregate Root)
- Code, Name, NativeName, IsDefault, IsActive, SortOrder
- No translation child (it IS the language table)

### Translation Child Entities

Each translatable aggregate (Department, Position, UnitOfMeasure, Country, Province, District, Ward) has a corresponding `{Entity}Translation` table extending `ERP.Shared.Core.Domain.TranslationEntity` (`OwnerId`, `LanguageCode`). The owning aggregate's own Name/Title/Description columns remain the fallback value when no translation row exists for the requested language.

## Database

**Database Name:** `MasterDataDB`

**Key Tables:**
- Departments, Positions, Employees, UnitsOfMeasure
- Countries, Provinces, Districts, Wards, SupportedLanguages
- DepartmentTranslations, PositionTranslations, UnitOfMeasureTranslations, CountryTranslations, ProvinceTranslations, DistrictTranslations, WardTranslations
- OutboxMessages

## Integration Events

### Published Events (Kafka topic: `mst-topic`)

- `DepartmentCreatedEvent` / `DepartmentUpdatedEvent`
- `PositionCreatedEvent`
- `EmployeeHiredEvent` / `EmployeeTerminatedEvent` / `EmployeeTransferredEvent`
- `UnitOfMeasureCreatedEvent`
- `CountryCreatedEvent`

Subscribers: HRMService (Employee/Department snapshot sync), NotificationService, ReportingService.

### Consumed Events

**UserRegisteredIntegrationEvent** (from Identity Service)
→ Automatically hires an Employee record when `IsEmployee=true` and a `DefaultDepartmentId` is supplied.

## API Endpoints

```
GET    /api/v1/masterdata/departments                              - List departments
POST   /api/v1/masterdata/departments                              - Create department
GET    /api/v1/masterdata/departments/{id}                         - Department detail
PUT    /api/v1/masterdata/departments/{id}                         - Update department

GET    /api/v1/masterdata/positions                                - List positions
POST   /api/v1/masterdata/positions                                - Create position
GET    /api/v1/masterdata/positions/{id}                           - Position detail
PUT    /api/v1/masterdata/positions/{id}                           - Update position
POST   /api/v1/masterdata/positions/{id}/deactivate                - Deactivate position
POST   /api/v1/masterdata/positions/{id}/activate                  - Activate position

GET    /api/v1/masterdata/employees                                - List employees
POST   /api/v1/masterdata/employees                                - Hire employee
GET    /api/v1/masterdata/employees/{id}                           - Employee detail
POST   /api/v1/masterdata/employees/{id}/terminate                 - Terminate employee
POST   /api/v1/masterdata/employees/{id}/transfer                  - Transfer employee
PUT    /api/v1/masterdata/employees/{id}/profile                   - Update employee profile

GET    /api/v1/masterdata/units-of-measure                         - List units of measure
POST   /api/v1/masterdata/units-of-measure                         - Create unit of measure
GET    /api/v1/masterdata/units-of-measure/{id}                    - Unit of measure detail
PUT    /api/v1/masterdata/units-of-measure/{id}                    - Update unit of measure

GET    /api/v1/masterdata/geography/countries                      - List countries
POST   /api/v1/masterdata/geography/countries                      - Create country
GET    /api/v1/masterdata/geography/countries/{id}                 - Country detail
GET    /api/v1/masterdata/geography/countries/{id}/provinces       - List provinces
POST   /api/v1/masterdata/geography/countries/{id}/provinces       - Create province
GET    /api/v1/masterdata/geography/provinces/{id}                 - Province detail
GET    /api/v1/masterdata/geography/provinces/{id}/districts       - List districts
POST   /api/v1/masterdata/geography/provinces/{id}/districts       - Create district
GET    /api/v1/masterdata/geography/districts/{id}                 - District detail
GET    /api/v1/masterdata/geography/districts/{id}/wards           - List wards
POST   /api/v1/masterdata/geography/districts/{id}/wards           - Create ward
GET    /api/v1/masterdata/geography/wards/{id}                     - Ward detail

GET    /api/v1/masterdata/languages                                - List supported languages
POST   /api/v1/masterdata/languages                                - Create supported language
GET    /api/v1/masterdata/languages/{id}                           - Language detail
```

All read endpoints accept an optional `languageCode` query parameter for content translation lookups (falls back to the base entity's default-language fields when no translation row exists).

## Dependencies

**Services that depend on Shared Master Data Service:**
- HRMService (consumes Employee/Department events for a local read-model snapshot)
- NotificationService (consumes events)
- ReportingService (consumes events)

**Services Shared Master Data Service depends on:**
- IdentityService (consumes `UserRegistered` to auto-hire employees)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — MasterDataDB
- **Kafka** — Event streaming (topic: `mst-topic`)
- **MediatR** — CQRS
- **FluentValidation** — Command validation

---

**Last Updated:** 2026-07-01
**Version:** 1.0.0
