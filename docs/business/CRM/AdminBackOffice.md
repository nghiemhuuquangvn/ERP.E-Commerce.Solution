# CRM — Admin back office (screen `Customers (CRM)`)

Business analysis of the Admin screen `/customers` (five tabs: Contacts, Leads, Opportunities, Cases, Activities) as shipped on 2026-11-01 (ROADMAP P2-9, U3 batch 4). It describes the process, the rules the system enforces, what the screen exposes and what it does not. The service-level model is in [module-10-crm.md](module-10-crm.md); this document is about **how a person works the process on the screen**. Vietnamese user guide: [docs/user-guides/crm.md](../../user-guides/crm.md).

## 1. Purpose and scope
Give a shop's sales and support staff one place to keep customers' contact data, follow a sales lead until it becomes (or fails to become) revenue, and answer customers' problems. In scope: the Admin screen and the CRMService API behind it. Out of scope: marketing campaigns, quotes, e-mail sending, SLA timers, customer-facing portal (Storefront), duplicate detection, reports beyond the pipeline counters.

## 2. Actors and permissions
| Actor | Does | Needs |
|---|---|---|
| Sales rep | Maintains contacts and tags, works leads and opportunities, logs activities | `crm:contacts|leads|opportunities|activities:manage` |
| Sales manager | Watches the pipeline counters, reassigns opportunities (assignee at creation) | the same + `view` |
| Support agent | Opens, assigns, resolves, closes cases; logs activities against a case | `crm:cases:manage`, `crm:activities:manage` |
| Read-only user | Sees lists | `crm:<area>:view` |
| Tenant admin | Grants the above | role management |
A tab is shown only to a user with `crm:<area>:view`; every create / change button needs `crm:<area>:manage`. A user with no `view` permission sees a "no permission" state.

## 3. Process overview
```
Contact ──┬─> Lead ──(Contacted)──(Qualify)──> Opportunity ──(Advance ×n)──> Won | Lost
          │      └──────────(Lose + reason)──────────────────────────────────> Lost
          ├─> Case: Open → InProgress (assign) → Resolved (resolution) → Closed
          └─> Activity (Call/Email/Meeting/Note/Task) — optionally tied to an opportunity or a case
```
Contacts are the hub: leads, opportunities, cases and activities all point to a contact. A contact is also created automatically when a customer registers in the Storefront (event `UserRegistered`).

## 4. State models and rules
### 4.1 Lead (`LeadStatus`)
| From | Action (who) | To | Rule |
|---|---|---|---|
| — | Create | New | name required; source (Website, Referral, Social media, Email, Phone, Other); estimated value ≥ 0, contact optional |
| New | Mark contacted | Contacted | only from New |
| New, Contacted | **Qualify** (needs a linked contact; without one the server answers 400 "Link a contact to this lead before qualifying it", and a lead already Qualified / Lost is refused too - R83, fixed 2026-11-02) | Qualified | **creates an opportunity** inheriting name, contact and estimated value, expected close = today + 3 months, stage Prospecting; the lead becomes final |
| New, Contacted | Mark lost + **reason** | Lost | reason mandatory; final |
Qualified and Lost leads have no actions. **Screen:** Qualify opens a confirmation that says the opportunity is created (the side effect was silent before).

### 4.2 Opportunity (`OpportunityStage`)
Prospecting 10 % → Qualification 20 % → Proposal 40 % → Negotiation 60 % → **Won 100 %** | **Lost 0 %**.
- **Advance** moves exactly one step and never reaches Won; **Win** and **Mark lost** are possible from any open stage; Lost needs a **reason**; Won and Lost are final and show no actions.
- Value ≥ 0; the expected close date **must be in the future** (server rule). **Screen:** the field proposes **today + 30 days** and refuses today or earlier with a visible message (it used to propose today, which the server always rejected).
- The six stage counters above the table (`pipeline/summary`) are **filter buttons**: pressing one filters the list, pressing it again clears it.

### 4.3 Case (`CaseStatus`)
Open → (Assign) In progress → (Resolve + **resolution text**) Resolved → (Close) Closed.
- Number `CASE-yyyyMMdd-xxxxxxxx`; priority Low / Medium / High / Critical chosen by hand (no SLA yet, `SlaDeadline` unused); subject, contact and description required.
- Assign can be repeated (re-assign; the dialog is then titled "Reassign"). Resolve is offered until Resolved; **Close is offered only after Resolved** and asks for confirmation because a closed case is final. (The service would also allow Close from Open / In progress; the screen deliberately does not expose it.)

### 4.4 Activity (`ActivityStatus`)
Type Call / Email / Meeting / Note / Task; contact and subject required; optional notes, scheduled time, opportunity ID and case ID (pasted — there is no picker). Status at creation: **Scheduled** if the time is in the future, otherwise **Completed**. While Scheduled: **Complete** (optional notes) or **Cancel** (asks for confirmation). Completed and Cancelled are final. **Author (R82, fixed 2026-11-02):** every activity records who created it - the signed-in user for an activity logged in the Admin (taken from the token, a value in the request body is ignored), `SalesOrderService` for the note written when an order is placed; the database column `Activities.CreatedBy` is mandatory. Completing an activity twice, completing a cancelled one or cancelling a completed one is refused with a readable message (HTTP 400).

### 4.5 Contact and tags
Name required; e-mail, phone, company optional; source is always "Manual" when created here. Tags are free text per contact (add / remove). No duplicate check on e-mail / phone.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| CRM-1 | A user sees only the tabs they may view, and the open tab survives a reload | Tab chosen by `?tab=`; arrow keys move between tabs; no `view` → "no permission" state |
| CRM-2 | Every dialog validates before sending | Empty required field → "Bắt buộc" under the field, field marked invalid, nothing sent; Esc / Cancel close without saving |
| CRM-3 | A server refusal is explained, not generic | The server's own reason appears in a red box in the dialog; the dialog stays open with the typed data |
| CRM-4 | A successful change is confirmed | Green "Đã lưu." toast, list refreshed |
| CRM-5 | Qualify never surprises | Confirmation states an opportunity is created; cancelling changes nothing |
| CRM-6 | Final states are final | Qualified / Lost lead, Won / Lost opportunity, Closed case, Completed / Cancelled activity show no action buttons |
| CRM-8 | Activities are saved and attributed | Logging an activity returns 201 with its author set (the signed-in user; `SalesOrderService` for order notes); each placed order adds one "Sales order placed" note to the customer's contact, and a message that cannot be processed is retried 5 times and then logged and dropped (it never loops) |
| CRM-9 | Wrong-state actions are refusals, not server errors | Qualify without contact or twice, complete twice, cancel a completed activity answer 400 with the reason and change nothing |
| CRM-7 | Irreversible steps are confirmed | Close case, Cancel activity, Qualify ask first; Lose / Resolve ask for their text |
| CRM-8 | A new opportunity can be saved with the proposed date | Default close date is in the future and accepted by the server |
| CRM-9 | Filters can be undone | A filter or search with no result shows "Không có kết quả" and a **Xóa lọc** button |
| CRM-10 | The screen is usable by keyboard and screen reader | Dialogs trap focus, tabs have roles, every field has a label, axe finds no violation |

## 6. Use cases
**UC-1 Work a lead (main):** create the lead (with its contact) → Mark contacted → Qualify (confirm) → the new opportunity appears in Opportunities → Advance through the stages → Win. *Alternatives:* lose with a reason at any open step; qualify directly from New. *Exceptions:* the lead has no contact → the service refuses the qualification with a generic error (DEFERRED R83), link a contact first; the server refuses a value or date → the dialog shows its reason.
**UC-2 Handle a case:** open (contact, subject, description, priority) → Assign → Resolve with the resolution → Close (confirm). *Alternative:* re-assign. *Exception:* resolve without text → required error.
**UC-3 Plan and complete an activity:** log with a future time (Scheduled) → Complete with notes, or Cancel (confirm). *Exception:* completing a finished activity or cancelling a completed one shows the server's reason in the dialog. (Creating an activity used to fail with a generic error - R82, fixed.)
**UC-4 Tag a contact:** open Tags → add / remove tags; each action is applied at once.

## 7. Data model (screen view)
Contact(Id, Name, Email, Phone, Company, ContactSource, Tags[]) · Lead(LeadName, LeadSource, EstimatedValue, ContactId, LeadStatus) · Opportunity(OpportunityName, ContactId, Stage, ProbabilityPercent, EstimatedValue, ExpectedCloseDate, AssignedToUserId, LostReason) · Case(CaseNumber, ContactId, CaseSubject, CaseDescription, Priority, Status, ResolutionNote, AssignedToUserId) · Activity(ContactId, OpportunityId?, CaseId?, ActivityType, Subject, Notes, Status, ScheduledAt, CompletedAt). All rows carry the tenant.

## 8. Integrations
Published on Kafka `crm-topic` (Outbox): LeadCreated / Qualified / Lost, OpportunityCreated (not for the one made by qualification), OpportunityWon / Lost, CaseOpened / Resolved (closing publishes nothing), ContactCreated, ActivityCreated / Completed. Consumed: `UserRegistered` (creates a contact named by the e-mail), `SalesOrderCreated` (writes a Note activity for a known contact — **fails today for the same reason as R82**), `PaymentProcessed` (logged only). Reporting reads `crm-topic`; Notification sends the "case resolved" message.

## 9. Reports
Only the pipeline counters (count per stage; total and won value are returned by the API but not shown). No export.

## 10. Assumptions
One shop = one tenant; users are staff of the shop; the Contact picker lists the first 100 contacts of the shop.

## 11. Open issues (also in DEFERRED-ISSUES §6)
- **R82 fixed 2026-11-02:** the author is now set (see 4.4); the order consumer works and drained a backlog of 71 order messages into 71 "Sales order placed" notes.
- **R84 (high, security)** `ListContactsQuery` caches the contact list for 5 minutes under `crm:contacts:page:{page}:size:{size}` — **no tenant in the key** (another shop can be served this shop's page) and **no invalidation** (a new contact is missing from the picker for up to 5 minutes). Violates the rule "never cache tenant-owned data under a key without the tenant".
- **R83 fixed 2026-11-02:** qualifying without a contact, or an already qualified / lost lead, answers a clear 400. The lead screen still lets a lead be created without a contact; a "link a contact" step would be a UI follow-up.
- No duplicate-contact check; no SLA; no tag filter; activity notes cannot be edited from the screen (the API has a PATCH); opportunity / case IDs are pasted text; contact picker limited to 100.

## 12. UAT scenarios
1. Open `/customers?tab=leads` directly → Leads tab is active; press → → Opportunities opens.
2. In each tab press the add button, press Save empty → every required field shows "Bắt buộc"; press Esc → dialog closes.
3. Create a lead with a contact, Mark contacted, Qualify → confirmation text mentions the opportunity; confirm → lead shows Qualified without buttons, Opportunities shows a new Prospecting row closing in 3 months.
4. New opportunity: the date is 30 days ahead; set it to today → "Chọn ngày sau hôm nay"; save with the proposed date → "Đã lưu.".
5. Advance an opportunity → stage and probability change; Mark lost without a reason → required error; with a reason → Lost, no buttons.
6. Open a case, resolve it without text → error; with text → Resolved; Close → confirmation; confirm → Closed, no buttons.
7. Press a stage counter → list filtered and the counter is "pressed"; press again → all.
8. Search a contact name that does not exist → "Không có kết quả" + Xóa lọc; press it → full list.
9. Log an activity → saved, listed with its note; complete it twice → the second time shows "Activity is already completed." Cancel a scheduled activity (seeded) → confirmation first.
10. (API) Create a lead without a contact → Qualify → 400 "Link a contact to this lead before qualifying it"; link a contact, Qualify → 200 and an opportunity; Qualify again → 400.

## Caching (2026-11-02, R84)
Contact reads (the list and a contact by id) are **not cached**: they always come from the database on the caller's shop. The earlier 5-minute cache had no tenant in its key (another shop could be served the page) and was never evicted (a new contact missing from pickers for up to 5 minutes). See [SecurityReview-2026-10.md](../Identity/SecurityReview-2026-10.md).
