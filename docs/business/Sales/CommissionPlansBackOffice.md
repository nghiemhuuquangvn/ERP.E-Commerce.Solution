# Sales — Admin screen: Commission plans

Business analysis of the **Commission plans** screen as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 8). Vietnamese user guide: [docs/user-guides/payments-shipping-sales-logs.md](../../user-guides/payments-shipping-sales-logs.md).

## 1. Purpose and scope
A commission plan is a named percentage of an order's value that a sales representative earns; the plan chosen on a sale decides the commission record. Screen: list, add, deactivate. Out of scope: editing a plan, tiered rates, per-product rates, retroactive recalculation.

## 2. Actors and permissions
Sales manager. The code gates add / deactivate with `sales_order:approve` (there is no dedicated commission permission — open issue R95).

## 3. Rules
- A plan has a name and a rate typed as a **percentage** (> 0 and ≤ 100); the service stores a fraction (5 → 0.05). The form refuses a rate outside that range before sending and says "5 means 5 % of the order".
- **Deactivate** (confirmation) stops the plan being offered for new sales; commission already earned is never rewritten. Failures are shown as a toast with the server's reason (they were silent).
- Active plans can be deactivated; there is no Activate (create a new plan).

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| CP-1 | The form validates | Empty name / rate → linked "Bắt buộc"/range message; rate 150 refused; 7.5 is sent as `0.075` |
| CP-2 | Deactivation is deliberate | Confirmation names the plan; nothing sent before; Esc keeps the plan active |
| CP-3 | Refusals are explained | Server reason as a toast / in the dialog |
| CP-4 | Usable list | Empty state with the Add button; rate right-aligned; status badge |

## 5. Use cases
**UC-1 Add a plan:** Add Plan → name, rate → Save ("Đã lưu."). **UC-2 Retire a plan:** Deactivate → confirm. *Exception:* the server refuses → toast.

## 6. Data and integrations
CommissionPlan(Name, RateValue, IsActive); CommissionRecords reference the plan. No events on this screen.

## 7. Open issues (DEFERRED R95)
No dedicated permission; no edit, no Activate, no tiers.

## 8. UAT scenarios
1. Add Plan → empty Save → linked errors. 2. Rate 150 → "Enter a rate above 0 and up to 100." 3. Rate 3.5 and a name → listed as 3.50 %. 4. Deactivate → confirmation → Esc keeps Active → confirm → Inactive.
