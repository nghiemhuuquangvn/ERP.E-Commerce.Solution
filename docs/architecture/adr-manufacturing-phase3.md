# ADR - Manufacturing Phase 3: production orders on the pending-document pattern

**Status:** accepted by the owner (2026-11-10); P3-1 (order core) implemented 2026-11-10. Follows `adr-manufacturing-service.md` (decision 5) and `adr-warehouse-upgrade.md` (W0-W6, the internal stock API this phase calls). Business detail: `docs/business/Manufacturing/ProductionOrders.md`.

## Context
Phase 0-2 gave Manufacturing its master data, bills of materials and routings. Phase 3 turns them into execution: a production order is created with a snapshot of the BOM and routing, scheduled, released (stock reserved), fed with material, reported by operation, receives finished goods and is completed and closed (spec `docs/business/M/02_PRODUCTION_DEV_TASKS.md` T3.1-T3.14). The spec assumed one database transaction with Inventory (BR-MAT-06, NFR-05); the Warehouse is another service with another database, so every stock effect is a call that can fail, time out or be repeated.

## Decisions (made with the owner, 2026-11-10)
1. **Pending-document engine.** A stock-affecting document (material issue / return / backflush, finished-goods receipt) is stored first as `Pending` with lines; each line is sent to the Warehouse under the key `<DocNo>#<line>` (the W5 contract: same key + same payload = the first result again, another payload = 409 `WH_IDEMPOTENCY_CONFLICT`). When every line is `Posted` the effect on the order (IssuedQty, CompletedQty ...) and the document status change in **one local transaction**. A transient failure leaves the document `Pending` (HTTP 202); a Warehouse refusal compensates the lines already posted (the opposite movement under `<DocNo>#<line>R`) and the document becomes `Failed` with the mapped `MFG_*` code. (P3-3)
2. **Retry job** (hosted, cross-tenant, one row claim per document with `FOR UPDATE SKIP LOCKED`): posts `Pending` documents older than 20 s with exponential back-off, 12 attempts, then `Failed (retries exhausted)`; manual retry by permission; a Pending document blocks Cancel / Unrelease / Close of its order. (P3-3)
3. **The effect of a document on the order is applied only when it is Posted.** An operation report with backflush lines waits for them. (P3-3/P3-4)
4. **Reservations: call first, then commit; both calls idempotent.** Release reserves in the Warehouse, then changes state; Unrelease / Cancel / Complete change state with a `ReservationReleasePending` flag, then release; a sweeper finishes what is left. Changing the quantity = release the remainder + reserve again (the Warehouse has no "adjust"). (P3-2)
5. **Domain events through an outbox** to Kafka topic `manufacturing-topic` (spec §8.1), written in the same transaction as the change, published by `OutboxProcessor`; no consumer in Phase 3 (costing is Phase 6). (P3-1, done)
6. **`Idempotency-Key` header** on the POST of issue / return / report / receipt, stored on the document. (P3-3)
7. **Scheduling:** infinite capacity; backward from the due date over the work centers' calendars, forward from now (and `IsLate`) when the start would lie in the past; no routing = the item's fixed lead time in working days on the plant calendar. (P3-1, done)
8. **Backflush moves into Phase 3** (owner): cumulative formula `ROUNDUP(output x Required / OrderQty) - Consumed`, lots first-expiry-first from the Warehouse suggestion, a shortage rejects the whole source transaction (`MFG_MAT_008`) unless the plant allows negative stock. Reversals stay Phase 4. (P3-4/P3-5)
9. **Serial-controlled items are refused** (`MFG_MAT_011`, new code): the Warehouse cannot move them yet (R206).
10. **Interim quality release** (owner): `manufacturing:qc:disposition` books the Warehouse `QcRelease` for a received lot until FQC inspections exist (Phase 4). (P3-5)

## Decisions taken while planning P3-1
1. **"Today" and "now" are the plant's**: `TimeProvider` injected, `PlantClock` converts with the plant's time zone; due dates, document-number months and the schedule use plant time; times are stored in UTC.
2. **BOM / routing choice (BR-PO-04):** a chosen version must be Approved and effective on the reference date (`MFG_PO_005`); the default is the version effective on the reference date, which is plant today for a new order and the old plan-start date (if still in the future) for a Draft that is regenerated; none = `MFG_PO_004`.
3. **Issue method = the BOM line's value.** The spec chain "BOM line -> item profile -> plant default" cannot be applied: a BOM line always carries Manual or Backflush, there is no "inherit".
4. **Quantity** must be > 0 with at most the decimals of the item's base unit (TC-PO-003); priority 1..5.
5. **Order numbers are unique per tenant** (one counter per tenant, document type and month; plant = `Guid.Empty`), deviating from BR-GEN-04 which counts per plant: the number is the idempotency key in the Warehouse (`<DocNo>#<line>`, unique per tenant, product and type), so two plants issuing `MO-2611-00001` would collide there. Found by the live check (two plants of one tenant). The counter increments outside the order's save, so a failed save leaves a gap (allowed by the spec).
6. **Draft regeneration** retires the old child rows (`IsDeleted`) and adds new ones; unique keys of the lines are filtered on `IsDeleted = false`. Nothing may refer to Draft lines (P3-2 must keep that true).
7. **Open order (BR-SET-03 / BR-WC-03):** Released, InProgress, OnHold (it was released) and Completed count; Draft, Closed and Cancelled do not. The guard is a real query now (`OrderBackedOpenOrderGuard`); the work-center and reason reference checks include orders.
8. **Warehouses of an order** default to the plant's raw-material (materials) and finished-goods (output) warehouses; an override must be one of the plant's own.

## Consequences
- Manufacturing and the Warehouse are never one transaction: a `Pending` window exists in which a document is not yet in the order's figures; the UI says so (P3-6).
- Every Warehouse call of Phase 3 is safe to repeat; the cost is the engine (P3-3) and a background job in a service that had none.
- P3-1 adds the first Manufacturing outbox, the first hosted service and the first Kafka producer of the service; the topic has no consumer yet.
- Behaviour that differs from the spec is listed in `docs/business/Manufacturing/00_Mapping-to-ERP.md`.
