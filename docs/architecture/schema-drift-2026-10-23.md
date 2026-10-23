# Schema drift report — first Docker run (2026-10-23)

The first real run of the platform (ROADMAP P0-1, SQL Server installed on the host, 16 ERP databases that
already held seed data) showed that the hand-written SQL migrations in `src/Database/**` and the EF models
in the services do **not** describe the same schema. Unit and architecture tests never saw it because
they use the EF InMemory provider.

## How to measure it

`tests/unit/ERP.Architecture.Tests/LiveSchemaDriftTests.cs` builds the real EF model of every service and
compares it with a running SQL Server. It is a no-op unless `ERP_DRIFT_SQL` is set:

```bash
ERP_DRIFT_SQL="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False" \
  dotnet test tests/unit/ERP.Architecture.Tests --filter LiveSchemaDriftTests
```

- `MISSING` — a table/column the model reads or writes that the database does not have.
- `BLOCKER` — a `NOT NULL` column without a default that the model never writes: every INSERT fails.
- It does **not** compare column types yet (see "Not measured").

## Fixed on `fix/docker-first-run`

| Where | Problem | Fix |
|---|---|---|
| AuthDB `EmailConfirmTokens`, `PasswordResetTokens` | `UpdatedAt`/`IsDeleted` never added → register and `tenant-signup` returned 500 | Identity `025` |
| AccountingDB `Bills.SupplierName`, `Invoices.CustomerName` | `NOT NULL`, entity does not map them → no bill/invoice could be booked | Accounting `007` (empty default; the name is still not carried — see P1-1) |
| PosDB `TransactionItems` | no `TenantId` → adding an item to a sale returned 500 | POS `006` |
| ProductDB `ProductExternalMappings` | legacy shape, 004 skipped | Product `016` |
| ProductDB `ProductBundleItems`, `ProductRelations`, `ProductReviews`, `ProductUomConversions` | legacy shape without `BaseEntity` columns | Product `017` |
| ShippingDB 002 | `Status_New` compile error on every re-run | `002` edited (dynamic SQL) |
| Accounting Kafka consumer | `JObject.Value<Guid?>` throws on a GUID string → **every** PO/payment/stock message lost | `EventJson.GuidValue` (Shared) in Accounting only |
| `run-migrations.sh` | reported "All migrations applied successfully" with 162 SQL errors | counts `Msg N, Level` lines and says so |
| `*.sh` checked out with CRLF | admin/storefront containers crash-looped (`exec /docker-entrypoint.sh: no such file`) | `.gitattributes` `*.sh text eol=lf` |

## Still open (input for P0-9)

`BLOCKER` (30): FiscalPeriods.FiscalYear/PeriodMonth; Crm Activities.Type, Cases.Subject, Contacts.FirstName/LastName,
Opportunities.Name; Hrm EmployeeSnapshots.EmployeeId, LeaveRequests.Days/LeaveType, PaySlips.BasicSalary/PeriodStart/PeriodEnd;
Promotion LoyaltyTransactions.Type, Promotions.Type; Purchasing GoodsReceipts.POId; **Sales SalesOrderItems.TenantId**;
Warehouse Reservations.ProductId/Quantity; Reporting DimDate.DayOfWeek/FullDate and the `Id`/`*DateKey`/`TerminalId`
columns of FactInventoryMovements, FactPOSTransactions, FactPayments, FactSalesOrders.

`MISSING` (37): Auth ExternalLogins (CreatedAt, TenantId), UserPermissions.IsGranted; Payment InstallmentPlans and
SavedPaymentMethods (BaseEntity columns), Settlements (SettledAt, TransactionCount); Product PriceLists.RowVersion;
Purchasing GoodsReceipts.PurchaseOrderId, SupplierItems (Description, RowVersion); Shipping Carriers.TrackingUrlTemplate,
ReturnShipments (BaseEntity columns), ShipmentItems (TenantId, Weight), ShipmentTrackingEvents.TenantId,
Shipments (DeliveredAt, PickedUpAt, TotalWeight); Warehouse InventoryValuationSnapshotLines.TenantId.

Some of these may be mapping renames rather than missing columns (e.g. `GoodsReceipts.POId` vs `PurchaseOrderId`) —
decide per case whether to change the SQL or the model.

## Not measured

- Column **types**: POS `Transactions.PaymentMethod` is `INT` in 001 while the entity writes a string, so completing a
  sale fails with "Conversion failed when converting the nvarchar value 'Cash' to data type int".
- Nullability, lengths, indexes, foreign keys.
- 40 more `JObject.Value<Guid?>(...)` calls in 14 files (Warehouse, CRM, POS, Notification, SaleOrder, Payment,
  Promotion, MasterData, Product consumers) have the same defect as the Accounting one and are untested.

## Flows not yet verified

POS sale → stock decreases / void → restored (blocked by the PaymentMethod type and by the Kafka Guid defect),
storefront order → approve → shipment (blocked by `SalesOrderItems.TenantId`).
