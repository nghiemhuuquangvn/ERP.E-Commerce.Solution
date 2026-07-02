# Extensible Attributes Pattern

## When to reach for this

A record needs fields that vary per category/type/record, and the naive options
both fail: adding a nullable column per possible field causes sprawl (and every
new spec needs a schema migration), while a single free-text or JSON blob field
can't be validated, filtered, or queried per-field in SQL. First implemented for
ProductService (`src/services/ProductService`, see `003_AddProductAttributesAndDimensions.sql`)
to model product/variant specs like color, size, material, and technical
specifications — none of which existed in any structured form before (only a
single free-text `ProductVariants.Dimensions` string).

## The three-tier checklist

Decide per-field, not per-entity — a single aggregate can (and usually should) use
all three tiers at once:

1. **First-class typed column** — use when *every* instance of the entity has this
   field, and a downstream consumer needs to compute over it numerically or filter
   on it constantly (e.g. `ProductVariants.Length/Width/Height/Weight` — every
   physical variant has these, and WarehouseService/ShippingService need to do real
   arithmetic on them for bin-packing/volumetric-weight calculations). Never put
   these behind EAV or JSON — that would force text-parsing just to do `L*W*H`.

2. **EAV-lite `{Entity}Attributes`/`{Entity}AttributeOptions`/`{Entity}AttributeValues`**
   — use when the field varies by category/type (some products have "Screen Size",
   others have "Fabric Type") and still needs to be filterable/searchable per-field.
   Sparse **typed** value columns (`ValueText`/`ValueNumber`/`ValueBoolean`/
   `ValueDate`/option reference) avoid the classic "everything is a string" EAV
   pitfall while letting new specs be added as data (new `{Entity}Attributes` rows),
   not schema migrations.

3. **Opaque `ExtendedDataJson NVARCHAR(MAX)` escape hatch** — use only for
   genuinely arbitrary, non-searchable, integration-passthrough data (e.g. a
   supplier's proprietary field set that doesn't warrant a formal attribute
   definition). This mirrors the repo's existing "blob never queried in SQL" idiom
   (`OutboxMessages.Content`, `Identity.TwoFactorSettings.BackupCodes`,
   `HRM.LeaveRequests.Attachments`) — parsed app-side only, never via
   `JSON_VALUE`/`OPENJSON`. **Never** put a filterable/searchable spec here — that
   belongs in tier 2.

## Table shape template

Copy this shape, renaming `{Entity}` per adoption (e.g. HRM:
`EmployeeAttributes`/`EmployeeAttributeOptions`/`EmployeeAttributeValues` with
`OwnerType` distinguishing `Employee` vs. `Position`; CRM:
`ContactAttributes`/... distinguishing `Contact` vs. `Account`). See
`src/Database/Product/003_AddProductAttributesAndDimensions.sql` for the working
reference implementation.

```sql
CREATE TABLE {Entity}Attributes (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    CategoryId      UNIQUEIDENTIFIER NULL,       -- or whichever scoping concept applies; NULL = global
    Code            NVARCHAR(100)    NOT NULL,   -- stable machine key
    Name            NVARCHAR(200)    NOT NULL,   -- display label
    DataType        INT              NOT NULL,   -- 0=Text,1=Number,2=Boolean,3=Date,4=SingleSelect,5=MultiSelect
    Unit            NVARCHAR(20)     NULL,
    IsRequired      BIT              NOT NULL DEFAULT 0,
    IsFilterable    BIT              NOT NULL DEFAULT 1,
    SortOrder       INT              NOT NULL DEFAULT 0,
    -- + standard audit columns (CreatedBy/CreatedAt/UpdatedBy/UpdatedAt/IsDeleted)
);
CREATE UNIQUE INDEX IX_{Entity}Attributes_Code ON {Entity}Attributes (TenantId, Code) WHERE IsDeleted = 0;

CREATE TABLE {Entity}AttributeOptions (   -- controlled vocabulary for Select-type attributes
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    {Entity}AttributeId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES {Entity}Attributes(Id),
    Value               NVARCHAR(200)    NOT NULL,
    SortOrder           INT              NOT NULL DEFAULT 0,
    -- + audit columns
);
CREATE UNIQUE INDEX IX_{Entity}AttributeOptions_Attribute_Value ON {Entity}AttributeOptions ({Entity}AttributeId, Value) WHERE IsDeleted = 0;

CREATE TABLE {Entity}AttributeValues (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    {Entity}AttributeId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES {Entity}Attributes(Id),
    OwnerType           TINYINT          NOT NULL,  -- discriminates which local entity owns this value
    OwnerId             UNIQUEIDENTIFIER NOT NULL,   -- polymorphic — see trade-off below
    ValueText           NVARCHAR(500)    NULL,
    ValueNumber         DECIMAL(18, 4)   NULL,
    ValueBoolean        BIT              NULL,
    ValueDate           DATETIME2        NULL,
    {Entity}AttributeOptionId UNIQUEIDENTIFIER NULL FOREIGN KEY REFERENCES {Entity}AttributeOptions(Id),
    ValueOptionIdsJson  NVARCHAR(MAX)    NULL,       -- MultiSelect only; opaque, app-parsed
    -- + audit columns
    CONSTRAINT CK_{Entity}AttributeValue_OwnerType CHECK (OwnerType IN (0, 1 /*, ...*/))
);
CREATE UNIQUE INDEX IX_{Entity}AttributeValues_Owner_Attribute ON {Entity}AttributeValues (OwnerType, OwnerId, {Entity}AttributeId) WHERE IsDeleted = 0;
```

## Required-minimum trade-off: polymorphic `OwnerId`

`{Entity}AttributeValues.OwnerId` points at different tables depending on
`OwnerType` (e.g. `Products.Id` or `ProductVariants.Id`) — SQL Server has no
conditional/polymorphic FK mechanism, so this column **cannot** carry a real FK
constraint. This is the one deliberate deviation from this repo's otherwise
universal "every reference has an FK" convention. The mitigation is **mandatory
for every adoption**, not optional:
- `CHECK (OwnerType IN (...))` constraint, restricting to only the valid discriminator values.
- Application-layer existence validation before every write — load the owning
  record first and 404 if missing, exactly like `SetProductAttributeValueHandler`
  does (`ERP.PRD.Application/Features/Attributes/Commands/SetProductAttributeValueCommand.cs`).

## Non-goal: this is not a shared table

Each service stamps its **own** copy of these three tables into its **own**
database — exactly like the `{Entity}Translations` overlay pattern is never
shared across services. Cross-service consumers only ever see resolved attribute
values through that service's own DTOs or integration events, never via a live
cross-database FK or query (per `CLAUDE.md`'s database-per-service rule).

## Reference implementation

- `src/Database/Product/003_AddProductAttributesAndDimensions.sql` — the tables above, as actually deployed.
- `src/services/ProductService/src/ERP.PRD.Domain/Entities/ProductAttribute.cs`, `ProductAttributeOption.cs`, `ProductAttributeValue.cs`
- `src/services/ProductService/src/ERP.PRD.Application/Features/Attributes/` — Commands/Queries, including the upsert-by-unique-index pattern in `SetProductAttributeValueCommand.cs` (mirrors `Product.SetTranslation`'s find-or-create shape) and the per-`DataType` value validation.
- `src/services/ProductService/src/ERP.PRD.API/Controllers/AttributesController.cs`

## Related patterns in this codebase

- `docs/frontend/masterdata-admin-guide.md` — a different (CRUD, not attribute-modeling) extensibility pattern for admin-managed reference data.
- `TranslationEntity` (`src/Shared/Core/Domain/TranslationEntity.cs`) — the direct structural precedent this pattern's overlay-table shape is modeled on.
