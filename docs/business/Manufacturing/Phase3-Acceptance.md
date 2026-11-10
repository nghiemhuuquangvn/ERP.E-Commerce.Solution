# Manufacturing - Phase 3 acceptance and wrap-up (production orders, M1 milestone)

Added 2026-11-10 (P3-7). Source of the criteria: `docs/business/M/02_PRODUCTION_DEV_TASKS.md` T3.14 and **Milestone M1** ("TC-E2E-001 passes; every P1 test case of the groups PO, MAT, OP, FG passes; 0 Critical / High defects"), test cases `03_PRODUCTION_TEST_CASES.md`. This document is the sign-off view of Phase 3: what was delivered, how it was proven, where it differs from the specification, how to deploy it and what is left. The business rules themselves are in the area documents listed in §1.

## 1. What Phase 3 delivered
| Milestone | Delivered | Business document |
|---|---|---|
| P3-1 | Production order core: snapshot of BOM and routing, state machine, scheduling backward / forward / late, outbox and first events, permissions | `ProductionOrders.md` (part 1) |
| P3-2 | Release with availability check and reservation, unrelease, cancel, hold / resume, change of quantity | `ProductionOrders.md` (part 2) |
| P3-3 | The stock-document engine (pending-document pattern, retry job, idempotency) with material issue and return | `MaterialIssueReturn.md` |
| P3-4 | Operations: time logs, operation report, readiness and completion, backflush | `OperationsAndBackflush.md` |
| P3-5 | Finished-goods receipt with lots and quality hold, backflush of unbound materials, complete / short-close / reopen / close | `FinishedGoodsAndClosing.md` |
| P3-6 | Admin screens S15 / S16 / S18 / S19 and the stock-document list | `ProductionOrderScreens.md` |
| P3-7 | End-to-end proof (TC-E2E-001, TC-E2E-005), owner decisions R219 and R234, PostgreSQL schema-drift test, traceability matrix, this document | here |

Decisions of the architecture: `docs/architecture/adr-manufacturing-phase3.md` (with the "Phase 3 as built" table).

## 2. Proof
### 2.1 End-to-end runs (`scripts/manufacturing-live/mfg-p37-e2e.js`)
Run on glcheck and SYSTEM through the Gateway with the real Warehouse, each scenario on a fresh DS-01 data set.

**TC-E2E-001 (MVP, one order from creation to close)** - result "ALL OK" on both tenants. Observed with order MO-A (TP-BAN01 x 100):
| Step | Observed |
|---|---|
| 1 Create | Draft, 100 |
| 2 Release | Released; the only shortage is BTP-CHAN01 (need 400, available 300, short 100); operation 10 Ready, 20 and 30 Pending |
| 3 Child order | A second manual order BTP-CHAN01 x 100, issued, reported, received (output warehouse = the raw-material warehouse where the parent takes its legs from) -> Completed by itself; legs in stock 400 |
| 4 Issue | MAT01 100, CHAN 400 (300 from the reservation + 100 from free stock), KEO01 10, SON 52.50 in one document `MI-...`; the paint came first-expiry-first (lot A 30 -> 0, lot B 50 -> 27.5; the expired lot X untouched); screws refused `MFG_MAT_013`; glue 10.31 refused `MFG_MAT_002`; order InProgress |
| 5 Op10 Good 98, Scrap 2 | report `OR-...` Posted; backflush document `BF-...`; **screws consumed 1 600** (= (98 + 2) x 16), warehouse 2 000 -> 400; operation 10 Completed, its time log closed; scrapped 2 |
| 6, 7 Op20, Op30 Good 98 | **cartons 98, foam 196** (warehouse 500 -> 402, 1 000 -> 804); all operations Completed |
| 8 Receive 98 | `FR-...` Posted, lot `<item>-yymmdd-01` on quality hold in the quarantine warehouse; CompletedQty 98; order still InProgress |
| 9 Quality release | the lot Available in the finished-goods warehouse, **finished goods +98** |
| 10 Complete | Completed without a reason (98 >= 98), actual end set, nothing of the order still reserved in the Warehouse |
| 11 Return paint 1.00 | **accepted on the Completed order** (`MR-...`, lot B back to 28.5); an *issue* on the Completed order is still refused `MFG_MAT_001` |
| 12 Close | checklist lists the unsettled material; close without confirmation `MFG_PO_013`; with confirmation Closed; consumed: paint 51.50, glue 10, tops 100, legs 400, screws 1 600, cartons 98, foam 196; any later action `MFG_PO_014` |
| Final | history Released / InProgress / Completed / Closed; every document numbered and unique (`MI-`, `MR-`, `BF-`, `FR-`), none Pending; Warehouse reconciliation clean |

**TC-E2E-005 (hold and change of quantity)** - MO-E (100): glue 5 issued, op10 Good 40 (screws 640); hold without a reason `MFG_PO_009`, with a reason OnHold; a report and an issue while OnHold refused `MFG_PO_011` (TC-OP-018); resume -> InProgress; change quantity to 120: required paint 63, glue 12, screws 1 920, tops 120, issued glue stays 5, reserved paint 63 and glue 7, schedule recomputed, history shows hold, resume and the change; a change below the completed 20 refused `MFG_PO_010`; reports bring screws to 1 920, cartons 120, foam 240; receipts 20 + 100 -> **Completed at 120 by itself**, reservation released; closed after the quality release.

**Other checks in the same run:** TC-GEN-006 (50 parallel creates -> 50 different `MO-yymm-nnnnn`, no conflict), TC-GEN-002 (no DELETE of documents), TC-GEN-005 (six parallel changes of one order answer only 200 or 409), TC-ITM-001, TC-SEC-002 / 003 (403), R219 (a plant with a Draft order cannot be deactivated, it can once the order is cancelled), R234 (screws cannot be issued by hand).

### 2.2 Regression
`scripts/manufacturing-live/run-all.js --tenant glcheck|system` runs every live check of the module in order (m4-m6 engineering, p31-p36 order cycle and screens contract, p37 end-to-end, wh-w3 / w5 / w6 Warehouse integration). Result of the P3-7 run: §6.

### 2.3 Unit tests (at the end of P3-7)
Manufacturing 648, Warehouse 134, Architecture 87 (+ `PostgreSqlSchemaDriftTests`, opt-in), Shared 151, Admin 424.

### 2.4 PostgreSQL schema drift (R161 closed)
`PostgreSqlSchemaDriftTests` (opt-in `ERP_DRIFT_PG`): creates a scratch database, applies the 10 scripts of `src/Database/Manufacturing` **twice**, compares 31 tables / 599 columns with the `MfgDbContext` model, drops the database. Result: **no drift**; the scripts are idempotent.

### 2.5 Test-case traceability (`scripts/manufacturing-live/tc-trace.js`)
The matrix in §7 lists every test case of the M1 groups with the tests that cite it. **All 101 P1 cases of GEN, SEC, INT, SET, PLT, ITM, CAL, WC, MCH, RSN, BOM, RT, PO, SCH, MAT, OP, BF, FG, UI and E2E are cited by at least one unit test, Admin test or live check**; the six that were not cited before P3-7 (GEN-002, GEN-005, GEN-008, SEC-002, SEC-003, OP-018) now have their proofs (§2.1 and `GeneralRulesTests`, `ManufacturingApiRulesTests`). A citation is text, so the matrix says "a test names this case", not "the case was reviewed against the test"; the reading was done for the P1 cases that were not cited at the start of P3-7 and for the groups PO, MAT, OP, FG, BF in their milestones.

Cases of **later phases** (not part of M1): TC-PO-035 (period lock, Phase 6), TC-PO-043 (child orders, T4.12), TC-MAT-018..020 (substitutes, unplanned material, Phase 4), TC-SCH-006 (Gantt), TC-UI-002..005 (terminal), TC-INT-003 (costing from the Warehouse, Phase 6), TC-E2E-002..004 (quality, MRP, costing), and the groups REV, MQ, DT, TR, QC, NCR, RW, CST, MRP, CAP, KPI, RPT, PERF. A few P2 / P3 cases of M1 groups are not cited either (GEN-001 audit on create, GEN-004 soft delete, GEN-011 plant time zone, SEC-004 / 005 permission matrix, INT-004 / 005, PLT-004 / 005, MCH-002, BOM-052): their behaviour exists but no test names them; see §7.

## 3. M1 criteria
| Criterion | Result |
|---|---|
| TC-E2E-001 passes | **Yes**, on glcheck and SYSTEM (adaptations in §4) |
| Every P1 case of PO, MAT, OP, FG passes | **Yes** - all cited and green (unit, live) |
| 0 Critical / High defects | **Yes** - none open; the defects found during Phase 3 were fixed (§5) |
| Note | Not run in a real browser (R179, R251); the UI is proven by jsdom + axe + the API contract script |

## 4. Differences from the specification (accepted)
| Spec | As built | Why |
|---|---|---|
| One database transaction with Inventory (BR-MAT-06, NFR-05) | **Pending-document pattern**: a stock document is stored first and sent to the Warehouse under the key `<DocNo>#<line>`; the order shows it when the Warehouse has booked it; a retry job finishes outages; a refusal reverses what was booked | Manufacturing and the Warehouse are separate services |
| Reservation adjusted in place | A reconciler brings the Warehouse reservation in line after every change (flag + job) | The Warehouse cannot adjust a reservation |
| Document numbers per plant | MO, MI, MR, BF, FR numbers are per **tenant** and month (they are the Warehouse's idempotency key); `OR` numbers per plant | Two plants collided on the same key |
| Receipt and backflush separate | One `FR` document carries the receipt lines and the backflush of materials no operation owns | One effect, one engine pass |
| Posting date chosen by the user | Always the plant's today | No period lock before Phase 6 |
| BR-MAT-01: material only on Released / InProgress | **A return is also accepted on a Completed order** (settle before close) - confirmed by the owner 2026-11-10 (R245 closed). An issue is not | The specification's own TC-E2E-001 step 11 returns material after Complete |
| Manual issue of any material | **A Backflush material cannot be issued by hand** (`MFG_MAT_013`, R234) | It would be consumed twice |
| A plant may be deactivated at any time | **Refused while it has orders that are not Closed or Cancelled** (`MFG_PLT_001`, R219) | Its orders would become unreachable |
| FQC document at receipt (BR-FG-04) | The lot goes on quality hold; the interim action `qc:disposition` moves it and makes it Available; **Phase 4 replaces it** | FQC is Phase 4 |
| Child order created automatically | A second manual order | T4.12 (Phase 4) |
| Unit cost from the Warehouse lots | Receipts use the item cost; actual costing is Phase 6 | Costing is Phase 6 |
| Printing (PDF, labels, QR), Gantt S14, material requests S17, terminal S20 | Not built | Owner decision 3 and later phases |
| Serial-controlled items | Refused (`MFG_MAT_011`) | The Warehouse has no per-serial production tracking (R206) |

## 5. Defects found while building Phase 3 (all fixed)
Order numbers collided between two plants (made per tenant); the operation-report number was unique per tenant but numbered per plant (unique index changed to plant + number); the Warehouse had no inventory record for a product that was never stocked (the first `ProductionReceipt` now creates it); the Warehouse did not refuse a duplicate lot number (Manufacturing checks the Warehouse lot list and its own pending lines; a Warehouse unique index is the remaining hardening, R242); EF mapped three public collections as navigations (ignored in the model); plant deactivation left orphan orders (R219, now refused); manual issue of a backflush material could consume twice (R234, now refused). No Critical / High defect is open.

## 6. Deployment and operation
**Order:** (1) Warehouse (the lot-list endpoint and the inventory-record creation of P3-5), (2) Identity script 034 (permissions `manufacturing:order:*`, `material:*`, `operation:*`, `receipt:post`, `qc:disposition`, `stock-document:retry`), (3) `pg-migrate` (Manufacturing scripts 001-010, idempotent, can run on every start), (4) ManufacturingService (needs `KafkaSettings`, the service-token trust for Product / MasterData / Warehouse and `Security` keys as in the compose file; `Manufacturing:EnableDemoSeed` off in production), (5) Admin.
**Hosted services of ManufacturingService:** `OutboxProcessor` (events to `manufacturing-topic`), `ReservationSyncJob` (every 15 s), `StockPostingRetryJob` (every 10 s). They are safe to run on several replicas (idempotent calls, rows claimed by `xmin`) but this was not tested with more than one.
**Watch:** documents in status Pending for long (`GET stock-documents?status=Pending`, or the Admin page *Stock documents*); orders with `reservationSyncPending`; Kafka lag on `manufacturing-topic` (no consumer yet).
**Open deployment item:** the Kubernetes manifest for PostgreSQL (`postgres.yaml`) is still to be written; the local stack uses the host server.
**Regression result of P3-7** (`run-all.js`, 2026-11-10, after the deployment of this milestone):
| Script | glcheck | SYSTEM |
|---|---|---|
| mfg-m4, mfg-m6 | ALL OK | not run (they need the `ZZ-MFG-WH*` warehouses that only glcheck has) |
| mfg-m5, mfg-p31, mfg-p32, mfg-p34, mfg-p35, mfg-p36-contract-check, mfg-p37-e2e | ALL OK | ALL OK |
| mfg-p33 | ALL OK after one fix* | ALL OK after one fix* |
| wh-w3, wh-w5, wh-w6 | ALL OK | ALL OK |

\* The first run of `mfg-p33` failed three checks on both tenants because it issued the cartons (a backflush material) by hand, which R234 now refuses with `MFG_MAT_013` - the expected consequence of the owner's decision. The script now uses the Manual item "tops" for those lines and passed on both tenants. Service logs held only the expected refusals and the deliberate Warehouse stops.

## 7. Test-case matrix of the M1 groups
Generated by `node scripts/manufacturing-live/tc-trace.js --groups GEN,SEC,INT,SET,PLT,ITM,CAL,WC,MCH,RSN,BOM,RT,PO,SCH,MAT,OP,BF,FG,UI,E2E` (the file is regenerated, not edited by hand).

226 cases in GEN, SEC, INT, SET, PLT, ITM, CAL, WC, MCH, RSN, BOM, RT, PO, SCH, MAT, OP, BF, FG, UI, E2E: 204 cited; P1: 101, cited 101, not cited 0

| TC | Pri | Title | Cited by |
|---|---|---|---|
| TC-GEN-001 | P2 | Audit khi tạo | **not cited** |
| TC-GEN-002 | P1 | Không xóa cứng chứng từ | unit:GeneralRulesTests.cs, unit:ManufacturingApiRulesTests.cs, live:mfg-p37-e2e.js |
| TC-GEN-003 | P1 | Xóa danh mục đã dùng | unit:CalendarTests.cs, unit:CatalogTests.cs |
| TC-GEN-004 | P2 | Xóa mềm danh mục chưa dùng | **not cited** |
| TC-GEN-005 | P1 | Xung đột đồng thời | unit:GeneralRulesTests.cs, live:mfg-p37-e2e.js |
| TC-GEN-006 | P1 | Số chứng từ đồng thời | live:mfg-p37-e2e.js |
| TC-GEN-007 | P2 | Định dạng số chứng từ | unit:FoundationTests.cs |
| TC-GEN-008 | P1 | Ngày chứng từ tương lai | unit:GeneralRulesTests.cs, unit:ManufacturingApiRulesTests.cs |
| TC-GEN-009 | P2 | Thông điệp tiếng Việt | unit:FoundationTests.cs |
| TC-GEN-011 | P2 | Ngày nghiệp vụ theo múi giờ nhà máy | **not cited** |
| TC-GEN-010 | P1 | Idempotency giao dịch | unit:StockCtx.cs, unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-SEC-001 | P1 | Cách ly nhà máy | unit:PlantTests.cs |
| TC-SEC-002 | P1 | Không quyền phê duyệt BOM | live:mfg-p37-e2e.js |
| TC-SEC-003 | P1 | Công nhân không xuất kho | live:mfg-p37-e2e.js |
| TC-SEC-004 | P2 | Thủ kho không short-close | **not cited** |
| TC-SEC-005 | P2 | Ma trận quyền | **not cited** |
| TC-INT-001 | P2 | Quy đổi UoM | unit:BomTests.cs |
| TC-INT-002 | P1 | Rollback khi Inventory lỗi | unit:StockDocumentTests.cs |
| TC-INT-003 | P2 | UnitCost từ Inventory | **not cited** |
| TC-INT-004 | P2 | Không truy cập bảng module khác | **not cited** |
| TC-INT-005 | P2 | Sự kiện sau commit | **not cited** |
| TC-SET-001 | P1 | Tự tạo tham số | unit:PlantTests.cs |
| TC-SET-002 | P2 | Biên dung sai | unit:SettingsAndProfileTests.cs |
| TC-SET-003 | P2 | Không hồi tố | unit:StockDocumentTests.cs, unit:SettingsAndProfileTests.cs |
| TC-SET-004 | P3 | Audit tham số | unit:SettingsAndProfileTests.cs |
| TC-SET-005 | P1 | Đổi phương pháp giá khi còn lệnh mở | unit:SettingsAndProfileTests.cs |
| TC-SET-006 | P2 | Đổi phương pháp giá hợp lệ | unit:SettingsAndProfileTests.cs |
| TC-PLT-001 | P2 | Mã nhà máy trùng | unit:PlantTests.cs |
| TC-PLT-002 | P3 | Mã sai định dạng | unit:PlantTests.cs |
| TC-PLT-003 | P2 | Thiếu kho bắt buộc | unit:PlantTests.cs |
| TC-PLT-004 | P2 | Kho khác công ty | **not cited** |
| TC-PLT-005 | P3 | Lịch mặc định | **not cited** |
| TC-ITM-001 | P1 | Item Buy không được tạo lệnh | unit:CreateOrderTests.cs, live:mfg-p37-e2e.js |
| TC-ITM-002 | P2 | Item ngừng hoạt động | unit:SettingsAndProfileTests.cs |
| TC-ITM-003 | P2 | FixedQty thiếu số lượng | unit:SettingsAndProfileTests.cs |
| TC-ITM-004 | P2 | MinMultiple thiếu bội số | unit:SettingsAndProfileTests.cs |
| TC-ITM-005 | P3 | PeriodOrder biên | unit:SettingsAndProfileTests.cs |
| TC-ITM-006 | P2 | LowLevelCode chỉ đọc | unit:SettingsAndProfileTests.cs |
| TC-CAL-001 | P1 | Thời lượng ca thường | unit:CalendarTests.cs |
| TC-CAL-002 | P1 | Ca qua đêm | unit:CalendarTests.cs |
| TC-CAL-003 | P3 | Ca 24h | unit:CalendarTests.cs |
| TC-CAL-004 | P2 | Nghỉ ≥ thời lượng | unit:CalendarTests.cs |
| TC-CAL-005 | P1 | Chồng lấn ca | unit:CalendarTests.cs |
| TC-CAL-006 | P2 | Chồng lấn với ca qua đêm | unit:CalendarTests.cs |
| TC-CAL-007 | P2 | Ca tiếp giáp | unit:CalendarTests.cs |
| TC-CAL-008 | P1 | Phút làm việc theo ngày | unit:CalendarTests.cs |
| TC-CAL-009 | P3 | Ngoại lệ trùng ngày | unit:CalendarTests.cs |
| TC-CAL-010 | P1 | Lùi phút qua cuối tuần | unit:CalendarTests.cs |
| TC-CAL-011 | P1 | Cộng ngày làm việc | unit:CalendarTests.cs |
| TC-CAL-012 | P2 | Trừ ngày qua ngày lễ | unit:CalendarTests.cs |
| TC-WC-001 | P1 | Tạo WC hợp lệ | unit:CatalogTests.cs |
| TC-WC-002 | P2 | Mã trùng trong nhà máy | unit:CatalogTests.cs |
| TC-WC-003 | P3 | Mã sai định dạng | unit:CatalogTests.cs |
| TC-WC-004 | P1 | Năng lực ngày | unit:CatalogTests.cs |
| TC-WC-005 | P1 | Năng lực có hiệu suất | unit:CatalogTests.cs |
| TC-WC-006 | P2 | Biên hiệu suất | unit:CatalogTests.cs |
| TC-WC-007 | P1 | Ngừng hoạt động khi có lệnh mở | unit:CatalogTests.cs |
| TC-WC-008 | P2 | Ngừng hoạt động hợp lệ | unit:CatalogTests.cs |
| TC-WC-009 | P2 | Gia công thiếu vendor | unit:CatalogTests.cs |
| TC-MCH-001 | P2 | Tạo máy hợp lệ | unit:CatalogTests.cs |
| TC-MCH-002 | P3 | Chu kỳ lý tưởng = 0 | **not cited** |
| TC-MCH-003 | P2 | Máy Retired khi báo cáo | unit:CatalogTests.cs |
| TC-MCH-004 | P3 | Máy bảo trì | unit:CatalogTests.cs |
| TC-RSN-001 | P2 | Seed lý do | unit:CatalogTests.cs |
| TC-RSN-002 | P3 | Trùng mã theo Type | unit:CatalogTests.cs |
| TC-RSN-003 | P3 | Downtime thiếu Category | unit:CatalogTests.cs |
| TC-BOM-001 | P1 | Tạo BOM Draft | unit:BomTests.cs |
| TC-BOM-002 | P1 | BOM cho item Buy | unit:BomTests.cs |
| TC-BOM-003 | P2 | Thành phần không hoạt động | unit:BomTests.cs |
| TC-BOM-004 | P1 | Sửa BOM Approved | unit:BomTests.cs |
| TC-BOM-005 | P1 | Phê duyệt cần quyền | unit:BomTests.cs |
| TC-BOM-006 | P1 | Tự đóng hiệu lực bản trước | unit:BomTests.cs |
| TC-BOM-007 | P1 | Chồng lấn hiệu lực | unit:BomTests.cs |
| TC-BOM-008 | P1 | BOM áp dụng theo ngày | unit:BomTests.cs |
| TC-BOM-009 | P1 | Tự tham chiếu | unit:BomTests.cs |
| TC-BOM-010 | P1 | Vòng lặp đa cấp | unit:BomTests.cs |
| TC-BOM-011 | P2 | Số lượng không hợp lệ | unit:BomTests.cs |
| TC-BOM-012 | P3 | Biên hao hụt/yield | unit:BomTests.cs |
| TC-BOM-013 | P2 | UoM không quy đổi | unit:BomTests.cs |
| TC-BOM-014 | P2 | Tổng phân bổ đồng phẩm | unit:BomTests.cs |
| TC-BOM-015 | P2 | Nhóm thay thế thiếu ưu tiên 1 | unit:BomTests.cs |
| TC-BOM-016 | P2 | Nhóm thay thế 2 ưu tiên 1 | unit:BomTests.cs |
| TC-BOM-017 | P2 | Trùng thành phần cùng công đoạn | unit:BomTests.cs |
| TC-BOM-018 | P3 | Cùng thành phần khác công đoạn | unit:BomTests.cs |
| TC-BOM-019 | P1 | LowLevelCode | unit:BomTests.cs, live:mfg-m6.js |
| TC-BOM-020 | P2 | Obsolete không ảnh hưởng lệnh | unit:BomTests.cs |
| TC-BOM-021 | P3 | Cảnh báo công đoạn không tồn tại | unit:RoutingTests.cs |
| TC-BOM-030 | P1 | Nhu cầu chuẩn MO-A | unit:BomExplosionTests.cs |
| TC-BOM-031 | P1 | Làm tròn lên | unit:BomExplosionTests.cs |
| TC-BOM-032 | P1 | Phantom xuyên qua | unit:BomExplosionTests.cs |
| TC-BOM-033 | P2 | Phantom thiếu BOM | unit:BomExplosionTests.cs |
| TC-BOM-034 | P1 | Chỉ lấy ưu tiên 1 | unit:BomExplosionTests.cs |
| TC-BOM-035 | P2 | Số lượng cố định | unit:BomExplosionTests.cs |
| TC-BOM-036 | P2 | BaseQuantity ≠ 1 | unit:BomExplosionTests.cs |
| TC-BOM-037 | P2 | Yield < 100 | unit:BomExplosionTests.cs |
| TC-BOM-038 | P1 | Đầu ra phụ phẩm | unit:BomExplosionTests.cs |
| TC-BOM-039 | P2 | Đa cấp indented | unit:BomExplosionTests.cs |
| TC-BOM-040 | P3 | Vượt số cấp tối đa | unit:BomExplosionTests.cs |
| TC-BOM-050 | P2 | Where-used | unit:BomTests.cs |
| TC-BOM-051 | P3 | So sánh phiên bản | unit:BomTests.cs |
| TC-BOM-052 | P2 | UI cây BOM | **not cited** |
| TC-BOM-053 | P3 | Import Excel lỗi dòng | unit:ImportTests.cs |
| TC-RT-001 | P1 | Trùng số công đoạn | unit:RoutingTests.cs |
| TC-RT-002 | P3 | Số công đoạn ≤ 0 | unit:RoutingTests.cs |
| TC-RT-003 | P2 | Thời gian không hợp lệ | unit:RoutingTests.cs |
| TC-RT-004 | P2 | WC khác nhà máy / ngừng | unit:RoutingTests.cs |
| TC-RT-005 | P2 | Phiên bản routing | unit:RoutingTests.cs |
| TC-RT-006 | P1 | Thời lượng PerUnit + hiệu suất | unit:RoutingTests.cs |
| TC-RT-007 | P2 | Thời lượng PerBatch | unit:RoutingTests.cs |
| TC-RT-008 | P1 | Lead time routing | unit:RoutingTests.cs |
| TC-RT-009 | P3 | Công đoạn gia công sai WC | unit:RoutingTests.cs |
| TC-RT-010 | P2 | Lệnh không routing | unit:RoutingTests.cs |
| TC-PO-001 | P1 | Tạo lệnh chuẩn | unit:CreateOrderTests.cs, live:tc-trace.js |
| TC-PO-002 | P1 | Số lượng 0/âm | unit:CreateOrderTests.cs |
| TC-PO-003 | P3 | Số lượng lẻ với UoM 0 số lẻ | unit:CreateOrderTests.cs |
| TC-PO-004 | P1 | Ngày yêu cầu quá khứ | unit:CreateOrderTests.cs |
| TC-PO-005 | P2 | Biên ngày yêu cầu hôm nay | unit:CreateOrderTests.cs |
| TC-PO-006 | P1 | Không có BOM hiệu lực | unit:CreateOrderTests.cs |
| TC-PO-007 | P2 | Chọn BOM không hiệu lực | unit:CreateOrderTests.cs |
| TC-PO-008 | P1 | Regenerate khi đổi SL ở Draft | unit:CreateOrderTests.cs |
| TC-PO-009 | P1 | Snapshot độc lập BOM gốc | unit:CreateOrderTests.cs |
| TC-PO-010 | P3 | Ưu tiên ngoài khoảng | unit:CreateOrderTests.cs |
| TC-PO-011 | P1 | Kiểm tra khả dụng | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js, live:mfg-p37-e2e.js |
| TC-PO-012 | P1 | Release có thiếu – không chặn | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js, live:mfg-p37-e2e.js |
| TC-PO-013 | P1 | Release có thiếu – chặn | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:tc-trace.js |
| TC-PO-014 | P2 | Release khi thành phần ngừng | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs |
| TC-PO-015 | P2 | Không tuần tự | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs |
| TC-PO-016 | P1 | Unrelease hợp lệ | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-017 | P1 | Unrelease khi đã xuất | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-PO-018 | P1 | Tự chuyển InProgress | unit:ReleaseCtx.cs, unit:StockDocumentTests.cs |
| TC-PO-019 | P2 | Hold thiếu lý do | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-020 | P1 | Giao dịch khi OnHold | unit:ReleaseCtx.cs |
| TC-PO-021 | P2 | Hold tự Pause công đoạn | unit:ReleaseCtx.cs |
| TC-PO-022 | P1 | Resume về trạng thái trước | unit:OrderStateMachineTests.cs, unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-023 | P1 | Đổi SL nhỏ hơn đã hoàn thành | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs |
| TC-PO-024 | P1 | Đổi SL sau Release | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-025 | P2 | Đổi SL không quyền | unit:ReleaseCtx.cs |
| TC-PO-026 | P1 | Hủy Draft | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-027 | P1 | Hủy khi đã giao dịch | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs |
| TC-PO-028 | P2 | Hủy Released giải phóng giữ chỗ | unit:ReleaseCtx.cs, unit:ReleaseOrderTests.cs, live:mfg-p32.js |
| TC-PO-029 | P1 | Tự Complete | unit:ReceiptTests.cs, live:mfg-p35.js, live:tc-trace.js |
| TC-PO-030 | P1 | Complete trong dung sai | unit:ReceiptTests.cs, live:tc-trace.js |
| TC-PO-031 | P1 | Short-close thiếu lý do | unit:ReceiptTests.cs, live:mfg-p35.js, live:tc-trace.js |
| TC-PO-032 | P1 | Short-close hợp lệ | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-033 | P2 | Complete khi công đoạn Running | unit:ReceiptTests.cs |
| TC-PO-034 | P2 | Reopen | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-035 | P3 | Reopen khi kỳ khóa | **not cited** |
| TC-PO-036 | P1 | Close thiếu điều kiện | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-037 | P1 | Close với vật tư chưa quyết toán | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-038 | P1 | Close hợp lệ | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-039 | P1 | Sửa lệnh Closed | unit:OrderStateMachineTests.cs, unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-PO-040 | P2 | Chuyển trạng thái sai | unit:OrderStateMachineTests.cs, unit:ReceiptTests.cs |
| TC-PO-041 | P1 | Bảng chuyển trạng thái | unit:OrderStateMachineTests.cs |
| TC-PO-042 | P2 | Lịch sử trạng thái | unit:OrderStateMachineTests.cs, unit:ReceiptTests.cs |
| TC-PO-043 | P2 | Lệnh con | **not cited** |
| TC-PO-044 | P2 | CompletedQty/ScrappedQty | live:mfg-p37-e2e.js |
| TC-SCH-001 | P1 | Lập lịch lùi chuẩn | unit:OrderSchedulerTests.cs |
| TC-SCH-002 | P1 | Trễ → lập lịch tiến | unit:OrderSchedulerTests.cs |
| TC-SCH-003 | P2 | Bỏ qua Chủ nhật | unit:OrderSchedulerTests.cs |
| TC-SCH-004 | P2 | Lệnh không routing | unit:OrderSchedulerTests.cs |
| TC-SCH-005 | P2 | Đổi DueDate tính lại lịch | unit:OrderSchedulerTests.cs |
| TC-SCH-006 | P3 | Gantt kéo thả sai thứ tự | **not cited** |
| TC-MAT-001 | P1 | Xuất hợp lệ | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-002 | P1 | Xuất khi Draft | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-003 | P2 | Xuất khi Completed | unit:StockDocumentTests.cs |
| TC-MAT-004 | P1 | Biên dung sai xuất | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-005 | P1 | Vượt dung sai | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-006 | P2 | Vượt dung sai có quyền | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-007 | P1 | Vượt tồn | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-008 | P1 | Thiếu lô | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-009 | P1 | Lô hết hạn | unit:StockDocumentTests.cs, live:wh-w6.js |
| TC-MAT-010 | P1 | Gợi ý FEFO | live:wh-w6.js |
| TC-MAT-011 | P2 | Lô chờ kiểm | unit:StockDocumentTests.cs, live:wh-w6.js |
| TC-MAT-012 | P3 | Serial không khớp | unit:StockDocumentTests.cs |
| TC-MAT-013 | P1 | Xuất nhiều dòng nguyên tử | unit:StockDocumentTests.cs |
| TC-MAT-014 | P1 | Trả hợp lệ | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-015 | P1 | Trả vượt theo lô | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-016 | P2 | Trả vượt theo dòng | unit:StockDocumentTests.cs, live:mfg-p33.js |
| TC-MAT-017 | P3 | Trả không tạo lại giữ chỗ | unit:StockDocumentTests.cs |
| TC-MAT-018 | P2 | Thay thế hợp lệ | **not cited** |
| TC-MAT-019 | P2 | Thay thế ngoài nhóm | **not cited** |
| TC-MAT-020 | P3 | Vật tư ngoài định mức | **not cited** |
| TC-OP-001 | P1 | Start công đoạn Ready | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-002 | P1 | Start công đoạn Pending | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-003 | P2 | Nhiều công nhân | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-004 | P2 | Chồng lấn time log | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-005 | P1 | Thời lượng có Pause | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-006 | P2 | Nhập tay thời gian sai | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-007 | P1 | Báo cáo hợp lệ | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-008 | P1 | Báo cáo toàn 0 | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-009 | P2 | Số âm | unit:OperationTests.cs |
| TC-OP-010 | P1 | Phế thiếu lý do | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-011 | P2 | Làm lại thiếu lý do | unit:OperationTests.cs |
| TC-OP-012 | P1 | Vượt đầu vào công đoạn sau | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-013 | P1 | Biên đầu vào công đoạn đầu | unit:OperationTests.cs |
| TC-OP-014 | P1 | Tự hoàn thành công đoạn đầu | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-015 | P2 | Không tự hoàn thành khi công đoạn trước chưa xong | unit:OperationTests.cs |
| TC-OP-016 | P2 | Báo cáo vào công đoạn đã xong | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-017 | P2 | Hoàn thành thủ công & Reopen | unit:OperationTests.cs, live:mfg-p34.js |
| TC-OP-018 | P1 | Báo cáo khi OnHold | unit:OperationTests.cs, live:mfg-p37-e2e.js |
| TC-BF-001 | P1 | Backflush theo công đoạn | unit:OperationTests.cs, live:mfg-p34.js |
| TC-BF-002 | P1 | Backflush lũy kế | unit:OperationTests.cs, live:mfg-p34.js |
| TC-BF-003 | P1 | Phantom backflush ở op30 | unit:OperationTests.cs, live:mfg-p34.js |
| TC-BF-004 | P1 | Làm tròn lũy kế | unit:BomCtx.cs, unit:OperationTests.cs |
| TC-BF-005 | P1 | Thiếu tồn – chặn | unit:OperationTests.cs, live:mfg-p34.js |
| TC-BF-006 | P2 | Thiếu tồn – cho âm | unit:OperationTests.cs, live:mfg-p34.js |
| TC-BF-007 | P2 | Backflush khi nhập kho | unit:BomCtx.cs, unit:OrderCtx.cs, unit:ReceiptTests.cs |
| TC-BF-008 | P2 | Backflush theo FEFO | unit:BomCtx.cs, unit:OperationTests.cs |
| TC-FG-001 | P1 | Nhập kho cơ bản | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-002 | P1 | Vượt Good công đoạn cuối | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-003 | P1 | Vượt dung sai lệnh | unit:ReceiptTests.cs |
| TC-FG-004 | P2 | Biên dung sai | unit:ReceiptTests.cs |
| TC-FG-005 | P2 | Nhập nhiều lần cùng ngày | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-006 | P2 | Lô nhập tay trùng | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-007 | P2 | Hạn dùng | unit:ReceiptTests.cs |
| TC-FG-008 | P1 | Không FQC | unit:ReceiptTests.cs |
| TC-FG-009 | P2 | Lệnh không routing | unit:ReceiptTests.cs |
| TC-FG-010 | P2 | Phụ phẩm | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-011 | P3 | Phụ phẩm vượt dung sai | unit:ReceiptTests.cs, live:mfg-p35.js |
| TC-FG-012 | P3 | Serial thành phẩm | unit:ReceiptTests.cs |
| TC-FG-013 | P3 | Không tự Complete | unit:ReceiptTests.cs |
| TC-UI-001 | P1 | Nút hành động theo trạng thái | admin:orderActions.test.ts, admin:orderActions.ts |
| TC-UI-002 | P2 | Terminal: quét QR | **not cited** |
| TC-UI-003 | P2 | Terminal: ≤ 3 chạm báo cáo | **not cited** |
| TC-UI-004 | P2 | Hiển thị lỗi tiếng Việt | **not cited** |
| TC-UI-005 | P2 | Mất kết nối | **not cited** |

## 8. UAT sign-off checklist
1. Walk TC-E2E-001 in the browser (`ProductionOrderScreens.md` §12) as planner, storekeeper, supervisor and manager.
2. Walk TC-E2E-005 (hold, resume, change quantity to 120, finish at 120).
3. Stop the Warehouse container during a receipt: the document waits as Pending and posts by itself afterwards; start it again.
4. Try to deactivate a plant that has a Draft order (refused), and to issue screws by hand (refused).
5. Sign in as a viewer: no action button anywhere.
6. Decide the open items of §9.

## 9. Open items after Phase 3
Consolidated in `docs/DEFERRED-ISSUES.md` ("Manufacturing Phase 3 - open list"). The main ones: no real-browser run (R251); the Kubernetes PostgreSQL manifest; assumed opening costs of DS-01 (A-1) and about 1 000 seed `Reserved` values (R201); serial tracking (R206); a Warehouse unique index on lot numbers (R242); item-cost receipts until Phase 6 (R241); reversal of receipts and reports (Phase 4, T4.2, R246); FQC documents (Phase 4, R244); child orders (T4.12); multi-replica test of the jobs; throw-away data `ZZ-*` left on glcheck and SYSTEM (R250, R260, R261).

## 10. Test data left behind (not cleaned, by the owner's decision)
Prefixes and plants created by the live checks: `ZZ-P31-*` ... `ZZ-P35-*`, `ZZ-P36-*`, `ZZ-P37A-*` / `ZZ-P37B-*` (products `ZZP3xx-...`), `ZZ-W*` from the Warehouse checks, `ZZ-*` from the earlier milestones. They hold orders in all states (most Closed, Completed or Cancelled; a few Released / InProgress), lots on hold in quarantine warehouses, stock movements and 50 cancelled Draft orders per run of the end-to-end check. The plants `ZZ-P37B-*` are left inactive. Nothing was deleted: neither the Warehouse nor Manufacturing has a safe delete.
