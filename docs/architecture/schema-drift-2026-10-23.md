# Schema drift report (2026-10-23) — ROADMAP P0-1 and P0-9

The first real run of the platform (SQL Server installed on the host, 16 ERP databases already holding seed data)
showed that the hand-written SQL migrations in `src/Database/**` and the EF models in the services did **not**
describe the same schema. Unit and architecture tests never saw it because they use the EF InMemory provider.
**Status: closed.** `LiveSchemaDriftTests` reports no drift on the host databases and on a database built from
`src/Database` alone, and the POS and storefront flows pass in Docker.

## The oracle

`tests/unit/ERP.Architecture.Tests/LiveSchemaDriftTests.cs` builds the real EF model of every service and compares it
with a running SQL Server. It is a no-op unless `ERP_DRIFT_SQL` is set (server connection string, no database):

```bash
ERP_DRIFT_SQL="Server=127.0.0.1,14330;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False" \
  dotnet test tests/unit/ERP.Architecture.Tests --filter LiveSchemaDriftTests
```

| Line | Meaning |
|---|---|
| `MISSING` | a table/column the model reads or writes that the database does not have |
| `BLOCKER` | a NOT NULL column without default that the model never writes (every INSERT fails) |
| `TYPE` | model and database disagree on the kind of value (int / text / guid / date …; length ignored) |
| `NULLABLE` | a `Nullable<T>` property mapped to a NOT NULL column without default |
| `COMPUTED` | the model writes a column the database computes |

Not compared: indexes, foreign keys, string lengths.

## Clean-room proof (repeat after changing any migration)

```bash
docker run -d --name erp-drift-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Drift_Test#2026x' -p 127.0.0.1:14330:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
# run deployment/docker/run-migrations.sh twice against it (db-migrate image, SQL_HOST=host.docker.internal,14330)
# then the drift test above; connect with 127.0.0.1, not localhost (IPv6); remove the container afterwards
```

2026-10-23 result: pass 1 — **0 SQL errors**; pass 2 — only "already exists" (1913/2714) and duplicate seed keys
(2627/2601), which are expected because the 001 scripts are not idempotent; drift — none. Two passes are needed on a new
database because the Reporting ETL seeds read other services' databases and skip (with a notice) until those exist.

## What was wrong and how it was fixed

| Class | Examples | Fix |
|---|---|---|
| Legacy NOT NULL column no entity writes | CRM Contacts.FirstName/LastName, Cases.Subject; HRM LeaveRequests.Days/LeaveType; Promotions.Type; Warehouse Reservations.ProductId/Quantity; Reporting Fact*.Id and *DateKey; Sales SalesOrderItems.TenantId | allow NULL (data kept); fact `Id` gets `DEFAULT NEWID()` |
| Missing BaseEntity columns | 32 tables in MasterData, Notification, POS, Promotion, Purchasing, Shipping; Payment InstallmentPlans/SavedPaymentMethods; Shipping ReturnShipments; Product (5 tables); Identity token tables | SQL migrations (the hand-run `AllServices/004_FixMissingBaseEntityColumns.ps1`, which embedded an sa password, was deleted) |
| Child rows without tenant | POS TransactionItems, Shipping ShipmentItems/TrackingEvents/ReturnShipments, Warehouse InventoryValuationSnapshotLines, Identity ExternalLogins | `TenantId` backfilled from the parent row |
| Derived columns | FiscalPeriods.FiscalYear/PeriodMonth; PaySlips.PeriodStart/PeriodEnd | persisted computed columns |
| Computed column the entity writes | HRM PaySlips.NetSalary | now a stored column (values copied) |
| Wrong type | POS Transactions.PaymentMethod INT vs text; CRM Cases/Opportunities.AssignedToUserId, Notification NotificationPreferences.UserId, Promotion LoyaltyAccounts.UserId nvarchar vs Guid | converted in place (stops if a value is not a GUID) |
| Name differs | GoodsReceipts.POId, Shipments.TotalWeightKg/ActualDelivery, ShipmentItems.WeightKg, Settlements.CompletedAt | **model** mapping (`HasColumnName`) — the SQL names are used by indexes and seeds |
| Model bug | SalesQuotationItems key defaulted to int | `li.Property<Guid>("Id")` |
| Chain order | HRM 003 stopped at a blocked rename so later steps never ran; Promotion seed ran before its columns; Reporting ETL ran before the source databases | reordered / guarded; scratch file Identity `015_test_wrapped.sql` removed |

## Code defects found by running the flows

| Defect | Effect | Fix |
|---|---|---|
| `JToken.Value<Guid?>(name)` throws on a GUID string (41 calls, 14 consumers) | every Kafka message with an id was dropped | `EventJson.GuidValue`; `NoValueGuidConversionTests` |
| `payload["Lines"] as JArray` — Outbox lists are `{"$type","$values":[…]}` | POS sales never moved stock; PO receipts and Promotion order lines read as empty | `EventJson.ArrayValue`; same guard test |
| SaleOrder `UserContextMiddleware` blocked `/internal/jwks.json` | Warehouse/Promotion could not validate SaleOrder's service token → every order create failed with "WareHouseService returned 401" | path opened |
| `OrderNumberGenerator`: `SingleAsync` over `MERGE … OUTPUT` | no order could be created | `ToListAsync` |
| `SalesOrderRepository.GetByIdAsync` was `AsNoTracking` | submit/approve/cancel failed on the shadow-key line items | tracked load |
| Accounting `Bills.SupplierName` / `Invoices.CustomerName` NOT NULL, not mapped | no bill/invoice could be booked | empty default (the name is still not carried — P1-1) |

## Flows verified in Docker (host SQL Server)

- POS: sale of 2 units → Warehouse stock 103 → 101; void → 103; movements `POS-{tx}` and `POS-VOID-{tx}`.
- Storefront-style order → submit → approve → payment captured → inventory reserved → shipment created with the real
  recipient (Nguyen Van A), address (12 Le Loi, Quan 1, Ho Chi Minh, VN), carrier and lines.
- Purchase order → manager + finance approval → one Accounting bill with the PO total.

## Known limits

- The saga stops at "payment pending" until a payment is captured; checkout does not call Payment yet (P1-10).
- Redelivery idempotency of the POS → stock message was not exercised.
- `ChatBot`, `Logging` and `Identity` OpenIddict tables were not part of the model comparison beyond the shared DbContexts.
- Reporting facts seeded by the ETL are only populated on the second `db-migrate` pass.
