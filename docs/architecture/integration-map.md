# Integration Map

> This file was empty. The populated event-flow-matrix document is
> [`INTEGRATION_MAP_COMPLETE.md`](INTEGRATION_MAP_COMPLETE.md) — see it for the
> Order-to-Cash and Procure-to-Pay cycle diagrams across all services.
>
> Note (2026-07-10): `INTEGRATION_MAP_COMPLETE.md`'s event names/flow are a
> higher-level design narrative and drift from the actual wired code in a few
> places (e.g. it uses `GoodsReceivedEvent` where the real code publishes
> `PurchaseOrderReceivedDomainEvent`). For the code-accurate version of the PO
> and SO flows specifically, prefer `docs/business/Purchasing/PurchasingWorkflow.md`
> and `docs/business/Sales/SalesWorkflow.md`.
