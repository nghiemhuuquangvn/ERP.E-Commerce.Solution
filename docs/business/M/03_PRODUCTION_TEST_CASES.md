# MODULE SẢN XUẤT — BỘ TEST CASE

> **Vai trò tác giả:** Senior QA (phối hợp BA, Dev)
> **Đầu vào:** `01_PRODUCTION_BA_SPEC.md` (BR, mã lỗi), `02_PRODUCTION_DEV_TASKS.md` (task)
> **Phiên bản:** 1.0 · 2026-11-04

---

## 0. CHIẾN LƯỢC KIỂM THỬ

### 0.1 Cấp độ & công cụ
| Loại | Ký hiệu | Phạm vi | Công cụ |
|---|---|---|---|
| Unit | UT | Domain method, calculator, state machine, validator | xUnit, FluentAssertions, NSubstitute |
| Integration | IT | Service + EF Core + SQL Server thật, transaction, migration | Testcontainers (SQL Server), Respawn |
| API | API | Controller, phân quyền, `ApiResponse<T>`, mã lỗi, Idempotency | WebApplicationFactory |
| UI | UI | Màn hình S**, luồng thao tác | Playwright |
| End-to-End | E2E | Luồng nghiệp vụ xuyên module | Playwright + API |
| Hiệu năng | PERF | NFR-01..04 | k6 / BenchmarkDotNet |
| Bảo mật | SEC | Ma trận quyền BA §9, cách ly nhà máy | API |

### 0.2 Mức ưu tiên test case
**P1** = chặn release (đưa vào Smoke/Regression bắt buộc) · **P2** = Regression đầy đủ · **P3** = mở rộng/biên hiếm.
Cột "Loại" là cấp độ **tối thiểu** cần tự động hóa. Tên test tự động: `{TC_ID}_{MoTaNgan}`, ví dụ `TC_PO_031_ReceiptOverTolerance_ReturnsPRD_PO_015`.

### 0.3 Tiêu chí vào/ra
- **Vào:** task Dev đạt DoD, migration chạy, seed bộ dữ liệu chuẩn DS-01 thành công.
- **Ra (mỗi milestone):** 100% TC P1 pass, ≥ 95% TC P2 pass, 0 lỗi Critical/High mở; lỗi Medium có kế hoạch xử lý.

### 0.4 Phân loại lỗi
Critical (sai số liệu tồn kho/giá trị, mất dữ liệu, sai trạng thái không phục hồi) · High (chặn luồng chính) · Medium (sai phụ, có workaround) · Low (giao diện, chính tả).

---

## 1. BỘ DỮ LIỆU CHUẨN DS-01

> Ngày hệ thống giả lập: **Thứ Tư 2026-11-04**, 08:00 (Asia/Ho_Chi_Minh). Dev seed bằng `ProductionTestDataSeeder` (T3.14.1). Mọi số liệu mong đợi bên dưới tính từ DS-01 trừ khi TC ghi "biến thể".

### 1.1 Nhà máy & tham số
- Plant **PL01** "Nhà máy Bình Dương"; kho: **WH-RM** (NVL), **WH-FG** (TP), **WH-QC** (chờ kiểm), **WH-SCRAP**; lịch mặc định **CAL-5D**.
- Tham số: OverProduction **5%**, UnderCompletion **2%**, OverIssue **3%**, BlockReleaseOnShortage **false**, AllowNegativeStockOnBackflush **false**, RequireSequentialOperations **true**, AutoCompleteOnFullReceipt **true**, DefaultIssueMethod Manual, CostingMethod **Actual**, MrpHorizonDays 90, MaxBomLevels 20, FgLotNumberFormat mặc định.

### 1.2 Ca & lịch
| Ca | Giờ | Nghỉ | Thời lượng |
|---|---|---|---|
| CA1 | 06:00–14:00 | 30 | 450 |
| CA2 | 14:00–22:00 | 30 | 450 |
| CA3 | 22:00–06:00 | 30 | 450 (qua đêm) |

**CAL-5D:** Thứ 2–6: CA1+CA2 (900 phút); Thứ 7: CA1 (450); Chủ nhật: 0. Ngoại lệ: 2027-01-01 Holiday.

### 1.3 Work center & máy
| WC | Loại | Nguồn lực | Eff % | Util % | Nhân công/h | Máy/h |
|---|---|---|---|---|---|---|
| WC-CUT | Machine | 2 | 100 | 90 | 60.000 | 120.000 |
| WC-ASM | Labor | 3 | 100 | 100 | 50.000 | 20.000 |
| WC-PNT | Machine | 1 | 80 | 100 | 55.000 | 150.000 |
| WC-PKG | Labor | 2 | 100 | 100 | 45.000 | 10.000 |

Máy: M-CUT-01, M-CUT-02 (WC-CUT, ideal 60s), M-PNT-01 (WC-PNT, ideal 120s), M-OLD-01 (WC-CUT, **Retired**). Tất cả WC dùng CAL-5D, Queue/Move = 0.

### 1.4 Mặt hàng & hồ sơ sản xuất
| Item | Tên | UoM (thập phân) | Lô | Make/Buy | LT | Chính sách lô | SS | Khác |
|---|---|---|---|---|---|---|---|---|
| TP-BAN01 | Bàn gỗ BG01 | Cái (0) | Có | Make | 2 | LotForLot | 10 | RequiresFqc, plan QP-BAN01 |
| BTP-MAT01 | Mặt bàn | Cái (0) | Không | Make | 1 | LotForLot | 0 | StandardLotSize 50 |
| BTP-CHAN01 | Chân bàn | Cái (0) | Không | Make | 1 | FixedQty 200 | 0 | |
| KIT-PKG01 | Bộ đóng gói | Bộ (0) | Không | Make | 0 | — | 0 | Phantom |
| NVL-GO01 | Gỗ thông xẻ | m3 (3) | Có | Buy | 10 | LotForLot | 0 | Giá chuẩn 8.000.000 |
| NVL-VIT01 | Ốc vít M6 | Cái (0) | Không | Buy | 7 | MinMultiple 1000/500 | 500 | Backflush |
| NVL-SON01 | Sơn PU | Lít (2) | Có, có HSD | Buy | 5 | LotForLot | 0 | |
| NVL-KEO01 | Keo dán gỗ A | Kg (2) | Không | Buy | 5 | LotForLot | 0 | |
| NVL-KEO02 | Keo dán gỗ B | Kg (2) | Không | Buy | 5 | LotForLot | 0 | |
| NVL-THUNG01 | Thùng carton | Cái (0) | Không | Buy | 3 | LotForLot | 0 | Backflush |
| NVL-XOP01 | Xốp chèn | Cái (0) | Không | Buy | 3 | LotForLot | 0 | Backflush |
| PP-MUN01 | Mùn cưa | Kg (2) | Không | Buy | 0 | — | 0 | Giá chuẩn 2.000 (phụ phẩm) |

### 1.5 BOM (Approved, hiệu lực 2026-01-01 → không thời hạn)
**TP-BAN01 v1** (Base 1, Yield 100)
| Line | Thành phần | SL | Hao hụt | Công đoạn | Xuất | Ghi chú |
|---|---|---|---|---|---|---|
| 10 | BTP-MAT01 | 1 | 0 | 10 | Manual | |
| 20 | BTP-CHAN01 | 4 | 0 | 10 | Manual | |
| 30 | NVL-VIT01 | 16 | 0 | 10 | Backflush | |
| 40 | NVL-SON01 | 0.5 | 5% | 20 | Manual | |
| 50 | NVL-KEO01 | 0.1 | 0 | 10 | Manual | Nhóm G1, ưu tiên 1 |
| 51 | NVL-KEO02 | 0.12 | 0 | 10 | Manual | Nhóm G1, ưu tiên 2 |
| 60 | KIT-PKG01 | 1 | 0 | 30 | — | Phantom |

**KIT-PKG01 v1:** NVL-THUNG01 × 1 (Backflush), NVL-XOP01 × 2 (Backflush).
**BTP-MAT01 v1:** NVL-GO01 × 0.02 m3, hao hụt 10%, công đoạn 10, Manual; Phụ phẩm PP-MUN01 × 0.5 kg.
**BTP-CHAN01 v1:** NVL-GO01 × 0.005 m3, hao hụt 10%.

### 1.6 Routing (Approved)
**TP-BAN01 v1:** 10 Lắp ráp — WC-ASM, setup 15, run 10/sp · 20 Sơn — WC-PNT, setup 30, run 6/sp · 30 Đóng gói — WC-PKG, setup 0, run 2/sp.
**BTP-MAT01 v1:** 10 Cắt — WC-CUT, setup 20, run 3/sp.

### 1.7 Tồn kho ban đầu (WH-RM, trừ khi ghi khác)
| Item | Lô | SL | HSD |
|---|---|---|---|
| BTP-MAT01 | — | 120 | |
| BTP-CHAN01 | — | 300 | |
| NVL-VIT01 | — | 2.000 | |
| NVL-SON01 | L-SON-A | 30.00 | 2026-11-30 |
| NVL-SON01 | L-SON-B | 50.00 | 2027-03-31 |
| NVL-SON01 | L-SON-X | 20.00 | 2026-10-31 (đã hết hạn) |
| NVL-KEO01 | — | 50.00 | |
| NVL-KEO02 | — | 20.00 | |
| NVL-THUNG01 | — | 500 | |
| NVL-XOP01 | — | 1.000 | |
| NVL-GO01 | L-GO-01 | 5.000 | |
| TP-BAN01 (WH-FG) | L-TP-OLD | 20 | |

### 1.8 Kế hoạch kiểm tra QP-BAN01 (FQC, Approved)
Mẫu Fixed n = 5; AllowedMinorDefects = 1.
C1 Chiều cao (Measurement, 748–752 mm, **critical**) · C2 Độ bóng sơn (Attribute, **critical**) · C3 Vết xước nhỏ (Attribute, non-critical) · C4 Nhãn mác (Attribute, non-critical).

### 1.9 Số liệu tham chiếu tính sẵn
**Lệnh chuẩn MO-A:** TP-BAN01 × **100**, DueDate **2026-11-13** (Thứ Sáu).
| Vật tư snapshot | Required | Cách tính |
|---|---|---|
| BTP-MAT01 | 100 | 100×1 |
| BTP-CHAN01 | 400 | 100×4 |
| NVL-VIT01 | 1.600 | 100×16 (Backflush, op 10) |
| NVL-SON01 | 52.50 | 100×0.5×1.05 |
| NVL-KEO01 | 10.00 | chỉ ưu tiên 1 |
| NVL-THUNG01 | 100 | phantom × 1 (op 30) |
| NVL-XOP01 | 200 | phantom × 2 (op 30) |
Không có dòng KIT-PKG01, NVL-KEO02 → **7 dòng vật tư**.

| Công đoạn MO-A | Phút chuẩn | Phút lịch |
|---|---|---|
| 10 WC-ASM | 15 + 1.000 = 1.015 | 1.015 |
| 20 WC-PNT | 30 + 600 = 630 | 630/0.8 = 787,5 → **788** |
| 30 WC-PKG | 200 | 200 |

Lập lịch lùi: op30 = Thứ Sáu 13/11 (200'); op20 = 700' Thứ Sáu + 88' Thứ Năm; op10 = 812' Thứ Năm + 203' Thứ Tư → **PlannedStart 2026-11-11, PlannedEnd 2026-11-13**.
Trần nhập kho MO-A = 100 × 1.05 = **105**. Ngưỡng hoàn thành không lý do = 100 × 0.98 = **98**.

---

## 2. TEST CASE

> Cột: **ID · Tiêu đề · Điều kiện & Bước · Kết quả mong đợi · P · Loại · Trace**

### 2.1 Quy tắc chung (TC-GEN) & Bảo mật (TC-SEC)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-GEN-001 | Audit khi tạo | Tạo Work center WC-T01 bằng user `planner01` | `CreatedBy=planner01`, `CreatedAt` UTC ≈ now | P2 | IT | BR-GEN-01 |
| TC-GEN-002 | Không xóa cứng chứng từ | Gọi DELETE trên phiếu xuất MI đã ghi | 405/không có endpoint; bản ghi còn nguyên | P1 | API | BR-GEN-01 |
| TC-GEN-003 | Xóa danh mục đã dùng | Xóa WC-ASM (đang dùng trong routing) | `PRD_GEN_005`; WC-ASM không đổi | P1 | API | BR-GEN-02 |
| TC-GEN-004 | Xóa mềm danh mục chưa dùng | Xóa WC-T01 chưa tham chiếu | `IsDeleted=true`; không còn trong danh sách | P2 | API | BR-GEN-02 |
| TC-GEN-005 | Xung đột đồng thời | 2 phiên đọc MO-A; phiên 1 cập nhật Priority; phiên 2 cập nhật với RowVersion cũ | Phiên 2: HTTP 409, `PRD_GEN_001` | P1 | API | BR-GEN-03 |
| TC-GEN-006 | Số chứng từ đồng thời | 50 request tạo lệnh song song | 50 số `MO-2611-xxxxx` khác nhau | P1 | IT | BR-GEN-04 |
| TC-GEN-007 | Định dạng số chứng từ | Tạo lệnh đầu tiên tháng 11/2026 | `MO-2611-00001` | P2 | IT | BR-GEN-04, BR-PO-01 |
| TC-GEN-008 | Ngày chứng từ tương lai | Xuất vật tư với PostingDate 2026-11-05 | `PRD_GEN_004` | P1 | API | BR-GEN-08 |
| TC-GEN-009 | Thông điệp tiếng Việt | Gây lỗi `PRD_MAT_003` | `ApiResponse.Success=false`, `ErrorCode=PRD_MAT_003`, message tiếng Việt đúng Phụ lục A | P2 | API | BR-GEN-07 |
| TC-GEN-011 | Ngày nghiệp vụ theo múi giờ nhà máy | Xuất vật tư lúc 2026-11-04 23:30 giờ VN (= 16:30 UTC) | PostingDate = 2026-11-04; thời điểm lưu UTC 16:30 | P2 | IT | BR-GEN-05 |
| TC-GEN-010 | Idempotency giao dịch | Gửi 2 lần POST xuất vật tư cùng Idempotency-Key | Chỉ 1 phiếu MI, lần 2 trả kết quả lần 1 | P1 | API | NFR-05 |
| TC-SEC-001 | Cách ly nhà máy | User chỉ được gán PL02 gọi GET lệnh của PL01 | 403 hoặc 404; danh sách không chứa lệnh PL01 | P1 | SEC | BR-GEN-09 |
| TC-SEC-002 | Không quyền phê duyệt BOM | R-ENG gọi approve BOM | 403, `PRD_GEN_002` | P1 | SEC | BR-BOM-02, §9 |
| TC-SEC-003 | Công nhân không xuất kho | R-OPR gọi POST material-issues | 403 | P1 | SEC | §9 |
| TC-SEC-004 | Thủ kho không short-close | R-WHK gọi complete với lý do short-close | 403 | P2 | SEC | BR-PO-14 |
| TC-SEC-005 | Ma trận quyền | Chạy bộ test tham số hóa mọi endpoint × 10 vai trò theo BA §9 | Kết quả khớp ma trận 100% | P2 | SEC | §9 |

### 2.2 Tích hợp (TC-INT)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-INT-001 | Quy đổi UoM | Dòng BOM NVL-VIT01 nhập 1 Hộp (=100 Cái) cho đơn 2 | Required = 200 Cái | P2 | IT | BR-BOM-07, §8 |
| TC-INT-002 | Rollback khi Inventory lỗi | Fake Inventory ném lỗi ở dòng 2/3 khi xuất 3 dòng | Không phiếu MI, không giao dịch tồn, reservation giữ nguyên | P1 | IT | BR-MAT-06 |
| TC-INT-003 | UnitCost từ Inventory | Xuất NVL-GO01 khi giá vốn bình quân 8.200.000 | `MaterialTransactionLine.UnitCost = 8.200.000` | P2 | IT | §8, BR-CST-02 |
| TC-INT-004 | Không truy cập bảng module khác | Phân tích static (ArchUnitNET): Production.Infrastructure không tham chiếu entity Inventory/Sales | Test kiến trúc pass | P2 | UT | §8 |
| TC-INT-005 | Sự kiện sau commit | Xuất vật tư thành công | Event `MaterialIssued` phát đúng 1 lần, sau commit; khi rollback không phát | P2 | IT | §8.1 |

### 2.3 Tham số, Nhà máy, Hồ sơ mặt hàng (TC-SET, TC-PLT, TC-ITM)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-SET-001 | Tự tạo tham số | Tạo Plant PL02 | Có 1 bản ghi ProductionSetting giá trị mặc định | P1 | IT | BR-SET-01 |
| TC-SET-002 | Biên dung sai | Cập nhật OverProductionTolerancePct = 100 / 100.01 / −1 | 100 OK; 100.01 và −1 lỗi validate | P2 | API | §5.1 |
| TC-SET-003 | Không hồi tố | MO-A đã Release; đổi OverIssue 3→0; xuất KEO01 10.30 | Lỗi `PRD_MAT_002` (áp dụng giá trị mới tại thời điểm giao dịch) | P2 | IT | BR-SET-02 |
| TC-SET-004 | Audit tham số | Đổi OverProduction 5→7 | Audit log có 5 → 7, người sửa | P3 | IT | BR-SET-02 |
| TC-SET-005 | Đổi phương pháp giá khi còn lệnh mở | Có MO-A InProgress; đổi CostingMethod → Standard | `PRD_SET_001` | P1 | API | BR-SET-03 |
| TC-SET-006 | Đổi phương pháp giá hợp lệ | Mọi lệnh Closed/Cancelled/Draft; đổi → Standard | Thành công | P2 | API | BR-SET-03 |
| TC-PLT-001 | Mã nhà máy trùng | Tạo plant mã `pl01` | `PRD_GEN_006` (chuẩn hóa chữ hoa trùng PL01) | P2 | API | BR-PLT-01 |
| TC-PLT-002 | Mã sai định dạng | Mã `P` và `PL 01` | Lỗi validate | P3 | API | BR-PLT-01 |
| TC-PLT-003 | Thiếu kho bắt buộc | Tạo plant không có QuarantineWarehouseId | Lỗi validate | P2 | API | BR-PLT-02 |
| TC-PLT-004 | Kho khác công ty | Chọn WH của công ty khác | Lỗi validate | P2 | API | BR-PLT-02 |
| TC-PLT-005 | Lịch mặc định | Plant không có DefaultCalendarId; Release lệnh không routing | Lỗi yêu cầu lịch mặc định | P3 | IT | BR-PLT-03 |
| TC-ITM-001 | Item Buy không được tạo lệnh | Tạo lệnh cho NVL-GO01 | `PRD_ITM_001` | P1 | API | BR-ITM-02 |
| TC-ITM-002 | Item ngừng hoạt động | Inactive BTP-CHAN01; tạo hồ sơ | Lỗi BR-ITM-01 | P2 | API | BR-ITM-01 |
| TC-ITM-003 | FixedQty thiếu số lượng | LotSizing FixedQty, FixedLotQty = 0 | `PRD_ITM_002` | P2 | UT | BR-ITM-03 |
| TC-ITM-004 | MinMultiple thiếu bội số | OrderMultiple = 0 | `PRD_ITM_002` | P2 | UT | BR-ITM-03 |
| TC-ITM-005 | PeriodOrder biên | PeriodDays = 0 / 1 / 90 / 91 | 1, 90 OK; 0, 91 lỗi | P3 | UT | BR-ITM-03 |
| TC-ITM-006 | LowLevelCode chỉ đọc | PUT hồ sơ với LowLevelCode = 5 | Giá trị không đổi (bỏ qua hoặc lỗi) | P2 | API | BR-ITM-04 |

### 2.4 Ca & Lịch (TC-CAL)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-CAL-001 | Thời lượng ca thường | CA1 06:00–14:00, nghỉ 30 | 450 | P1 | UT | BR-CAL-01 |
| TC-CAL-002 | Ca qua đêm | CA3 22:00–06:00, nghỉ 30 | 450 | P1 | UT | BR-CAL-01 |
| TC-CAL-003 | Ca 24h | 06:00–06:00, nghỉ 0 | 1.440, hợp lệ | P3 | UT | BR-CAL-01 |
| TC-CAL-004 | Nghỉ ≥ thời lượng | 06:00–14:00, nghỉ 480 | `PRD_CAL_001` | P2 | UT | BR-CAL-01 |
| TC-CAL-005 | Chồng lấn ca | Thứ 2 gán CA1 và ca 13:00–15:00 | `PRD_CAL_002` | P1 | UT | BR-CAL-02 |
| TC-CAL-006 | Chồng lấn với ca qua đêm | Thứ 2 gán CA2 (14–22) và ca 21:00–05:00 | `PRD_CAL_002` | P2 | UT | BR-CAL-02 |
| TC-CAL-007 | Ca tiếp giáp | Thứ 2 gán CA1 + CA2 (14:00 tiếp giáp) | Hợp lệ | P2 | UT | BR-CAL-02 |
| TC-CAL-008 | Phút làm việc theo ngày | CAL-5D: 2026-11-09 (T2), 2026-11-14 (T7), 2026-11-15 (CN), 2027-01-01 (lễ) | 900 / 450 / 0 / 0 | P1 | UT | BR-CAL-03 |
| TC-CAL-009 | Ngoại lệ trùng ngày | Thêm 2 ngoại lệ cùng 2027-01-01 | Lỗi `PRD_GEN_006` | P3 | API | BR-CAL-04 |
| TC-CAL-010 | Lùi phút qua cuối tuần | `SubtractWorkingMinutes(CAL-5D, cuối ngày 2026-11-16, 1000)` | Kết quả rơi vào Thứ Bảy 2026-11-14 tại phút làm việc thứ 350 | P1 | UT | BR-CAL-05 |
| TC-CAL-011 | Cộng ngày làm việc | `AddWorkingDays(2026-11-13, 1)`; `AddWorkingDays(2026-11-14, 1)` | 2026-11-14; 2026-11-16 | P1 | UT | BR-CAL-05 |
| TC-CAL-012 | Trừ ngày qua ngày lễ | `SubtractWorkingDays(2027-01-04, 1)` | 2027-01-02 (bỏ qua lễ 01/01, CN 03/01) | P2 | UT | BR-CAL-05 |

### 2.5 Work center, Máy, Công đoạn chuẩn, Lý do, Nhân công
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-WC-001 | Tạo WC hợp lệ | Tạo WC-T01 đủ trường | Thành công, mã chữ hoa | P1 | API | BR-WC-01 |
| TC-WC-002 | Mã trùng trong nhà máy | Tạo WC-CUT lần 2 tại PL01 | `PRD_GEN_006` | P2 | API | BR-WC-01 |
| TC-WC-003 | Mã sai định dạng | `W`, `WC_CUT`, 21 ký tự | `PRD_WC_001` | P3 | API | BR-WC-01 |
| TC-WC-004 | Năng lực ngày | WC-CUT ngày 2026-11-09 / 2026-11-14 / 2027-01-01 | 1.620 / 810 / 0 | P1 | UT | BR-WC-02 |
| TC-WC-005 | Năng lực có hiệu suất | WC-PNT ngày 2026-11-09 | 900×1×0.8×1.0 = 720 | P1 | UT | BR-WC-02 |
| TC-WC-006 | Biên hiệu suất | Efficiency 0/1/200/201; Utilization 0/100/101; Resources 0/999/1000 | Biên trong OK, biên ngoài `PRD_WC_002` | P2 | UT | BR-WC-04 |
| TC-WC-007 | Ngừng hoạt động khi có lệnh mở | MO-A Released; deactivate WC-ASM | `PRD_WC_003` | P1 | IT | BR-WC-03 |
| TC-WC-008 | Ngừng hoạt động hợp lệ | Không lệnh mở dùng WC-T01 | Thành công | P2 | IT | BR-WC-03 |
| TC-WC-009 | Gia công thiếu vendor | Type Subcontract, VendorId null | `PRD_WC_004` | P2 | API | BR-WC-05 |
| TC-MCH-001 | Tạo máy hợp lệ | M-CUT-03 thuộc WC-CUT, ideal 60s | Thành công | P2 | API | BR-MCH-01 |
| TC-MCH-002 | Chu kỳ lý tưởng = 0 | IdealCycleTimeSeconds = 0 | Lỗi validate | P3 | API | BR-MCH-01 |
| TC-MCH-003 | Máy Retired khi báo cáo | Báo cáo công đoạn chọn M-OLD-01 | `PRD_OP_010` | P2 | API | BR-MCH-02 |
| TC-MCH-004 | Máy bảo trì | Đổi M-CUT-02 sang UnderMaintenance; báo cáo chọn M-CUT-02 | Thành công kèm cảnh báo | P3 | API | BR-MCH-02 |
| TC-OPS-001 | Copy công đoạn chuẩn | Thêm vào routing từ OPS "CAT-01"; sau đó sửa OPS gốc run 3→5 | Công đoạn routing vẫn run 3 | P2 | IT | BR-OPS-01 |
| TC-OPS-002 | Mã trùng | Tạo OPS mã đã có | `PRD_GEN_006` | P3 | API | BR-OPS-01 |
| TC-RSN-001 | Seed lý do | Sau migration/seed | Có đủ mã ở T1.5.1 với Type/Category đúng | P2 | IT | BR-RSN-01 |
| TC-RSN-002 | Trùng mã theo Type | Tạo Scrap `SCRAP-01` lần 2; tạo Rework `SCRAP-01` | Lần 1 lỗi; lần 2 hợp lệ (khác Type) | P3 | API | BR-RSN-01 |
| TC-RSN-003 | Downtime thiếu Category | Tạo lý do Downtime không Category | Lỗi validate | P3 | API | BR-RSN-02 |
| TC-LBR-001 | Nhân viên 2 tổ | Thêm NV001 vào tổ T1 và T2 cùng hiệu lực | Lỗi | P3 | API | BR-LBR-01 |
| TC-LBR-002 | Đơn giá riêng ưu tiên | NV001 rate riêng 70.000 tại WC-ASM (50.000) | Resolver trả 70.000 | P2 | UT | BR-LBR-02 |
| TC-LBR-003 | Không có đơn giá riêng | NV002 không rate riêng | Resolver trả 50.000 | P2 | UT | BR-LBR-02 |

### 2.6 BOM (TC-BOM)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-BOM-001 | Tạo BOM Draft | Tạo BOM TP-BAN01 v2 copy dòng từ v1 | Draft, Version = 2 | P1 | API | BR-BOM-15 |
| TC-BOM-002 | BOM cho item Buy | Tạo BOM cho NVL-GO01 | `PRD_BOM_001` | P1 | API | BR-BOM-01 |
| TC-BOM-003 | Thành phần không hoạt động | Thêm dòng item Inactive | Lỗi BR-BOM-01 | P2 | API | BR-BOM-01 |
| TC-BOM-004 | Sửa BOM Approved | PUT dòng BOM TP-BAN01 v1 | `PRD_BOM_002` | P1 | API | BR-BOM-02 |
| TC-BOM-005 | Phê duyệt cần quyền | R-PMG approve v2 | Approved, ApprovedBy/At ghi nhận | P1 | API | BR-BOM-02 |
| TC-BOM-006 | Tự đóng hiệu lực bản trước | Approve v2 EffectiveFrom 2026-12-01 | v1.EffectiveTo = 2026-11-30 | P1 | IT | BR-BOM-03 |
| TC-BOM-007 | Chồng lấn hiệu lực | v1 hiệu lực 2026-01-01→2026-12-31; approve v3 EffectiveFrom 2026-06-01 | `PRD_BOM_003` | P1 | IT | BR-BOM-03 |
| TC-BOM-008 | BOM áp dụng theo ngày | Sau TC-BOM-006: resolve ngày 2026-11-30 và 2026-12-01 | v1 và v2 | P1 | UT | BR-BOM-03 |
| TC-BOM-009 | Tự tham chiếu | Thêm TP-BAN01 làm thành phần của chính nó | `PRD_BOM_004` | P1 | UT | BR-BOM-04 |
| TC-BOM-010 | Vòng lặp đa cấp | Draft BOM NVL… cho BTP-MAT01 v2 chứa TP-BAN01; approve | `PRD_BOM_004`, đường dẫn `BTP-MAT01 > TP-BAN01 > BTP-MAT01` | P1 | IT | BR-BOM-04 |
| TC-BOM-011 | Số lượng không hợp lệ | Quantity 0; −1; 1.1234567 | `PRD_BOM_005` | P2 | UT | BR-BOM-05 |
| TC-BOM-012 | Biên hao hụt/yield | Scrap 99.99 / 100; Yield 0 / 100 / 100.01 | 99.99 OK, 100 lỗi; Yield 0 và 100.01 lỗi | P3 | UT | BR-BOM-05 |
| TC-BOM-013 | UoM không quy đổi | Dòng NVL-SON01 với UoM "Cái" | `PRD_BOM_006` | P2 | IT | BR-BOM-07 |
| TC-BOM-014 | Tổng phân bổ đồng phẩm | 2 CoProduct 60% + 40% | `PRD_BOM_007` | P2 | UT | BR-BOM-09 |
| TC-BOM-015 | Nhóm thay thế thiếu ưu tiên 1 | G2: hai dòng ưu tiên 2 và 3 | `PRD_BOM_009` | P2 | UT | BR-BOM-11 |
| TC-BOM-016 | Nhóm thay thế 2 ưu tiên 1 | G2: hai dòng ưu tiên 1 | `PRD_BOM_009` | P2 | UT | BR-BOM-11 |
| TC-BOM-017 | Trùng thành phần cùng công đoạn | Thêm NVL-VIT01 lần 2 op 10 | `PRD_BOM_010` | P2 | UT | BR-BOM-12 |
| TC-BOM-018 | Cùng thành phần khác công đoạn | Thêm NVL-VIT01 op 30 | Hợp lệ | P3 | UT | BR-BOM-12 |
| TC-BOM-019 | LowLevelCode | Sau seed DS-01 | TP-BAN01=0; BTP-MAT01, BTP-CHAN01, NVL-VIT01, NVL-SON01, NVL-KEO01, NVL-KEO02, KIT-PKG01=1; NVL-GO01, NVL-THUNG01, NVL-XOP01=2 | P1 | IT | BR-BOM-13 |
| TC-BOM-020 | Obsolete không ảnh hưởng lệnh | MO-A tạo từ v1; obsolete v1 | Snapshot MO-A giữ nguyên 7 dòng | P2 | IT | BR-BOM-14 |
| TC-BOM-021 | Cảnh báo công đoạn không tồn tại | Draft BOM dòng op 40 (routing chỉ có 10–30); approve | Approve thành công kèm cảnh báo | P3 | IT | BR-RT-08 |
| TC-BOM-030 | Nhu cầu chuẩn MO-A | Explode TP-BAN01 × 100 | Đúng bảng §1.9 (7 dòng, SON 52.50) | P1 | UT | BR-BOM-06 |
| TC-BOM-031 | Làm tròn lên | Explode TP-BAN01 × 7 | NVL-SON01 = 3.675 → **3.68**; NVL-KEO01 = 0.70 | P1 | UT | BR-BOM-06, BR-GEN-06 |
| TC-BOM-032 | Phantom xuyên qua | Explode TP-BAN01 × 100 | Có NVL-THUNG01 100, NVL-XOP01 200 gắn op 30; không có KIT-PKG01 | P1 | UT | BR-BOM-08 |
| TC-BOM-033 | Phantom thiếu BOM | Biến thể: KIT-PKG01 không có BOM hiệu lực | `PRD_BOM_011` | P2 | UT | BR-BOM-08 |
| TC-BOM-034 | Chỉ lấy ưu tiên 1 | Explode TP-BAN01 × 100 | Có NVL-KEO01 10.00; không có NVL-KEO02 | P1 | UT | BR-BOM-11 |
| TC-BOM-035 | Số lượng cố định | Biến thể: thêm dòng NVL-SON01 0.2 L IsFixedQuantity, hao hụt 0 | Đơn 1 và đơn 500 đều cần 0.20 cho dòng này | P2 | UT | BR-BOM-06 |
| TC-BOM-036 | BaseQuantity ≠ 1 | Biến thể: BOM base 10, VIT 160 | Đơn 100 → VIT 1.600 | P2 | UT | BR-BOM-06 |
| TC-BOM-037 | Yield < 100 | Biến thể: Yield 80, VIT 16 | Đơn 100 → VIT 2.000 | P2 | UT | BR-BOM-06 |
| TC-BOM-038 | Đầu ra phụ phẩm | Explode BTP-MAT01 × 50 | NVL-GO01 = 50×0.02×1.1 = **1.100** m3; Output PP-MUN01 PlannedQty 25.00 | P1 | UT | BR-BOM-06, BR-BOM-09 |
| TC-BOM-039 | Đa cấp indented | ExplodeMultiLevel TP-BAN01 × 1 | Cây 3 cấp; NVL-GO01 dưới BTP-MAT01 0.022 và dưới BTP-CHAN01 4×0.0055 = 0.022 | P2 | UT | BR-BOM-06 |
| TC-BOM-040 | Vượt số cấp tối đa | Biến thể MaxBomLevels = 1; explode đa cấp TP-BAN01 | `PRD_BOM_008` | P3 | UT | BR-BOM-10 |
| TC-BOM-050 | Where-used | Where-used NVL-GO01 | BTP-MAT01, BTP-CHAN01 (cấp 1); TP-BAN01 (cấp 2) | P2 | API | T2.4 |
| TC-BOM-051 | So sánh phiên bản | Compare v1 và v2 (v2 đổi VIT 16→18, thêm dòng keo) | Diff: 1 thay đổi SL, 1 thêm | P3 | API | T2.4 |
| TC-BOM-052 | UI cây BOM | Mở S08 TP-BAN01 v1 | Cây hiển thị phantom mở rộng, nhóm G1 đánh dấu | P2 | UI | S08 |
| TC-BOM-053 | Import Excel lỗi dòng | File 10 dòng, dòng 4 item không tồn tại | Không ghi gì; báo lỗi dòng 4 | P3 | API | T2.9 |

### 2.7 Routing (TC-RT)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-RT-001 | Trùng số công đoạn | Thêm công đoạn 20 lần 2 | `PRD_RT_001` | P1 | UT | BR-RT-01 |
| TC-RT-002 | Số công đoạn ≤ 0 | Seq 0 | `PRD_RT_001` | P3 | UT | BR-RT-01 |
| TC-RT-003 | Thời gian không hợp lệ | Run 0; Setup −1; PerBatch BatchSize 0 | `PRD_RT_002` | P2 | UT | BR-RT-02 |
| TC-RT-004 | WC khác nhà máy / ngừng | Chọn WC của PL02; chọn WC Inactive | `PRD_RT_003` | P2 | API | BR-RT-02 |
| TC-RT-005 | Phiên bản routing | Approve v2 hiệu lực 2026-12-01 | v1.EffectiveTo = 2026-11-30 | P2 | IT | BR-RT-03 |
| TC-RT-006 | Thời lượng PerUnit + hiệu suất | Op 20 TP-BAN01, Q=100 | Chuẩn 630; lịch 788 | P1 | UT | BR-RT-04 |
| TC-RT-007 | Thời lượng PerBatch | Biến thể: run 30/lô, BatchSize 40, Q=100 | Chuẩn = setup + 3×30 | P2 | UT | BR-RT-04 |
| TC-RT-008 | Lead time routing | TP-BAN01, Q=100, kết thúc cuối ngày 2026-11-13 | Tổng phút lịch 2.003; bắt đầu 2026-11-11 | P1 | UT | BR-RT-05 |
| TC-RT-009 | Công đoạn gia công sai WC | IsSubcontract với WC-ASM | `PRD_RT_004` | P3 | UT | BR-RT-06 |
| TC-RT-010 | Lệnh không routing | Item BTP-CHAN01 (không routing); tạo lệnh 200 | Lệnh không có công đoạn; PlannedStart = 1 ngày làm việc trước DueDate | P2 | IT | BR-RT-07, BR-PO-06 |

### 2.8 Lệnh sản xuất (TC-PO)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-PO-001 | Tạo lệnh chuẩn | Tạo MO-A | Draft; số MO-2611-xxxxx; BOM v1, Routing v1; 7 vật tư; 3 công đoạn; Output PP… không có | P1 | API | BR-PO-01, 03, 04, 05 |
| TC-PO-002 | Số lượng 0/âm | Quantity 0; −5 | `PRD_PO_001` | P1 | API | BR-PO-03 |
| TC-PO-003 | Số lượng lẻ với UoM 0 số lẻ | Quantity 10.5 TP-BAN01 | Lỗi validate (hoặc làm tròn theo quy ước) — chốt: **lỗi** | P3 | API | BR-PO-03 |
| TC-PO-004 | Ngày yêu cầu quá khứ | DueDate 2026-11-03 | `PRD_PO_003` | P1 | API | BR-PO-03 |
| TC-PO-005 | Biên ngày yêu cầu hôm nay | DueDate 2026-11-04 | Tạo thành công, IsLate = true | P2 | API | BR-PO-03, BR-PO-06 |
| TC-PO-006 | Không có BOM hiệu lực | Biến thể: BOM TP-BAN01 hiệu lực từ 2027-01-01; tạo lệnh due 2026-11-13 | `PRD_PO_004` | P1 | API | BR-PO-04 |
| TC-PO-007 | Chọn BOM không hiệu lực | Chọn v2 (hiệu lực 2026-12-01) cho lệnh bắt đầu 11/2026 | `PRD_PO_005` | P2 | API | BR-PO-04 |
| TC-PO-008 | Regenerate khi đổi SL ở Draft | MO-A Draft đổi 100 → 120 | SON 63.00; CHAN 480; thời lượng op10 = 1.215 | P1 | IT | BR-PO-05 |
| TC-PO-009 | Snapshot độc lập BOM gốc | Tạo MO-A; sau đó sửa (copy v2 approve) BOM | MO-A không đổi | P1 | IT | BR-PO-05 |
| TC-PO-010 | Ưu tiên ngoài khoảng | Priority 0; 6 | Lỗi validate | P3 | API | BR-PO-03 |
| TC-PO-011 | Kiểm tra khả dụng | CheckAvailability MO-A | Thiếu duy nhất BTP-CHAN01: cần 400, có 300, thiếu 100 | P1 | API | BR-PO-07 |
| TC-PO-012 | Release có thiếu – không chặn | MO-A Release (Block=false) | Released + cảnh báo; reservation CHAN 300, các dòng khác = Required; op10 Ready, op20/30 Pending | P1 | IT | BR-PO-07, 08 |
| TC-PO-013 | Release có thiếu – chặn | Biến thể Block=true; Release MO-A | `PRD_PO_007`; vẫn Draft; không reservation | P1 | IT | BR-PO-07 |
| TC-PO-014 | Release khi thành phần ngừng | Inactive NVL-KEO01 sau khi tạo MO-A; Release | `PRD_PO_006` | P2 | IT | BR-PO-07 |
| TC-PO-015 | Không tuần tự | Biến thể RequireSequential=false; Release | Cả 3 công đoạn Ready | P2 | IT | BR-PO-08 |
| TC-PO-016 | Unrelease hợp lệ | MO-A Released chưa giao dịch; Unrelease | Draft; reservation = 0 | P1 | IT | BR-PO-09 |
| TC-PO-017 | Unrelease khi đã xuất | Đã xuất KEO01 5.00; Unrelease | `PRD_PO_008` | P1 | IT | BR-PO-09 |
| TC-PO-018 | Tự chuyển InProgress | MO-A Released; xuất KEO01 5.00 | Status InProgress, ActualStart ghi nhận | P1 | IT | BR-PO-10 |
| TC-PO-019 | Hold thiếu lý do | Hold MO-A không lý do | `PRD_PO_009` | P2 | API | BR-PO-11 |
| TC-PO-020 | Giao dịch khi OnHold | Hold MO-A (HLD-01); xuất vật tư; báo cáo; nhập kho | Cả 3: `PRD_PO_011` | P1 | IT | BR-PO-11 |
| TC-PO-021 | Hold tự Pause công đoạn | Op10 đang Running (NV001); Hold | Op10 Paused; time log dừng tính | P2 | IT | BR-PO-11 |
| TC-PO-022 | Resume về trạng thái trước | Hold từ InProgress; Resume | InProgress (không phải Released) | P1 | UT | BR-PO-11 |
| TC-PO-023 | Đổi SL nhỏ hơn đã hoàn thành | CompletedQty 40; đổi 39 | `PRD_PO_010` | P1 | IT | BR-PO-12 |
| TC-PO-024 | Đổi SL sau Release | MO-A InProgress (đã xuất SON 30); đổi 100 → 120 | SON Required 63.00, Issued vẫn 30; reservation SON = 33.00; lịch tính lại; history ghi | P1 | IT | BR-PO-12 |
| TC-PO-025 | Đổi SL không quyền | R-PLN đổi SL lệnh Released | 403 | P2 | SEC | BR-PO-12 |
| TC-PO-026 | Hủy Draft | Cancel MO Draft lý do CAN-01 | Cancelled | P1 | API | BR-PO-13 |
| TC-PO-027 | Hủy khi đã giao dịch | Cancel MO-A InProgress | `PRD_PO_002` | P1 | API | BR-PO-13 |
| TC-PO-028 | Hủy Released giải phóng giữ chỗ | Cancel MO-A Released | Reservation = 0; Cancelled | P2 | IT | BR-PO-13 |
| TC-PO-029 | Tự Complete | Nhập kho đủ 100 | Completed; ActualEnd; reservation còn lại = 0; công đoạn chưa xong → Completed | P1 | IT | BR-PO-14 |
| TC-PO-030 | Complete trong dung sai | CompletedQty 98; Complete không lý do | Completed | P1 | IT | BR-PO-14 |
| TC-PO-031 | Short-close thiếu lý do | CompletedQty 97; Complete không lý do | `PRD_PO_012` | P1 | IT | BR-PO-14 |
| TC-PO-032 | Short-close hợp lệ | CompletedQty 97; R-PMG + SC-01 | Completed; history lý do SC-01 | P1 | IT | BR-PO-14 |
| TC-PO-033 | Complete khi công đoạn Running | Op30 Running; Complete | `PRD_PO_013` | P2 | IT | BR-PO-14 |
| TC-PO-034 | Reopen | MO-A Completed; Reopen (R-PMG) | InProgress | P2 | IT | BR-PO-15 |
| TC-PO-035 | Reopen khi kỳ khóa | Kỳ 11/2026 khóa; Reopen | `PRD_GEN_003` | P3 | IT | BR-PO-15 |
| TC-PO-036 | Close thiếu điều kiện | Còn FQC Pending, còn MQ mở | `PRD_PO_013` liệt kê 2 điều kiện | P1 | IT | BR-PO-16 |
| TC-PO-037 | Close với vật tư chưa quyết toán | NetIssued SON 52.50, không trả, Close không xác nhận | `PRD_PO_013` (điều kiện 4) | P1 | IT | BR-PO-16 |
| TC-PO-038 | Close hợp lệ | Đủ điều kiện, xác nhận kết chuyển tiêu hao | Closed; ConsumedQty cập nhật | P1 | IT | BR-PO-16 |
| TC-PO-039 | Sửa lệnh Closed | PUT/hành động bất kỳ trên lệnh Closed | `PRD_PO_014` | P1 | API | BR-PO-16 |
| TC-PO-040 | Chuyển trạng thái sai | Close lệnh Released | `PRD_PO_002` | P2 | UT | BR-PO-02 |
| TC-PO-041 | Bảng chuyển trạng thái | Test tham số hóa mọi cặp (from,to) theo BA §4.2 | Chỉ cặp hợp lệ thành công | P1 | UT | BR-PO-02 |
| TC-PO-042 | Lịch sử trạng thái | Draft→Released→InProgress→OnHold→InProgress→Completed→Closed | 6 bản ghi history đúng thứ tự, người, lý do | P2 | IT | BR-PO-02 |
| TC-PO-043 | Lệnh con | MO-A Released thiếu CHAN 100; "Tạo lệnh con" | Lệnh BTP-CHAN01 × 100, ParentOrderId = MO-A, DueDate = 2026-11-10 (ngày làm việc trước 11-11) | P2 | IT | BR-PO-18 |
| TC-PO-044 | CompletedQty/ScrappedQty | Op10 phế 2, op20 phế 1, nhập 95 | CompletedQty 95; ScrappedQty 3 | P2 | IT | BR-PO-19 |

### 2.9 Lập lịch (TC-SCH)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-SCH-001 | Lập lịch lùi chuẩn | Tạo MO-A | PlannedStart ngày 2026-11-11, PlannedEnd 2026-11-13; op30 trong ngày 13; op20 bắt đầu 12; op10 bắt đầu 11 | P1 | UT | BR-PO-06, BR-SCH-01 |
| TC-SCH-002 | Trễ → lập lịch tiến | MO-B: TP-BAN01 × 100, Due 2026-11-05 | Lịch lùi cho start 2026-11-03 < now → lập lịch tiến từ now; IsLate = true; PlannedEnd > Due | P1 | UT | BR-PO-06 |
| TC-SCH-003 | Bỏ qua Chủ nhật | MO TP-BAN01 × 100 Due 2026-11-16 (Thứ Hai) | Không công đoạn nào xếp vào 2026-11-15; Thứ Bảy chỉ dùng 450' | P2 | UT | BR-PO-06, BR-CAL-03 |
| TC-SCH-004 | Lệnh không routing | BTP-CHAN01 × 200 Due 2026-11-16 | PlannedStart 2026-11-14 (LT 1 ngày làm việc) | P2 | UT | BR-PO-06 |
| TC-SCH-005 | Đổi DueDate tính lại lịch | MO-A Draft đổi Due 2026-11-20 | PlannedStart 2026-11-18 | P2 | IT | BR-PO-06 |
| TC-SCH-006 | Gantt kéo thả sai thứ tự | (Phase 5 P3) Kéo op20 trước op10 | Từ chối, giữ thứ tự | P3 | UI | BR-SCH-02 |

### 2.10 Vật tư: xuất, trả, thay thế (TC-MAT) & Yêu cầu cấp (TC-MQ)
> Tiền điều kiện chung: MO-A Released (TC-PO-012).

| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-MAT-001 | Xuất hợp lệ | Xuất KEO01 10.00 | Phiếu MI; IssuedQty 10.00; tồn KEO01 40.00; ReservedQty 0 | P1 | IT | BR-MAT-02, 06, 07 |
| TC-MAT-002 | Xuất khi Draft | MO Draft; xuất | `PRD_MAT_001` | P1 | API | BR-MAT-01 |
| TC-MAT-003 | Xuất khi Completed | MO Completed; xuất | `PRD_MAT_001` | P2 | API | BR-MAT-01 |
| TC-MAT-004 | Biên dung sai xuất | Xuất KEO01 10.30 | Thành công (10 × 1.03) | P1 | IT | BR-MAT-02 |
| TC-MAT-005 | Vượt dung sai | Xuất KEO01 10.31 (R-WHK) | `PRD_MAT_002` | P1 | IT | BR-MAT-02 |
| TC-MAT-006 | Vượt dung sai có quyền | R-PMG xuất KEO01 12.00 với lý do OVI-01 | Thành công, phiếu ghi lý do | P2 | IT | BR-MAT-02 |
| TC-MAT-007 | Vượt tồn | Xuất BTP-CHAN01 350 (tồn 300) | `PRD_MAT_003` | P1 | IT | BR-MAT-03 |
| TC-MAT-008 | Thiếu lô | Xuất SON không chọn lô | `PRD_MAT_004` | P1 | API | BR-MAT-04 |
| TC-MAT-009 | Lô hết hạn | Xuất SON từ L-SON-X | `PRD_MAT_005` | P1 | IT | BR-MAT-04 |
| TC-MAT-010 | Gợi ý FEFO | Suggest lots SON 52.50 | L-SON-A 30.00 + L-SON-B 22.50; không có L-SON-X | P1 | IT | BR-MAT-04 |
| TC-MAT-011 | Lô chờ kiểm | Lô SON L-SON-Q trạng thái QC-Hold; xuất | `PRD_MAT_005` | P2 | IT | BR-MAT-04 |
| TC-MAT-012 | Serial không khớp | Biến thể item serial; SL 3, nhập 2 serial | `PRD_MAT_007` | P3 | API | BR-MAT-04 |
| TC-MAT-013 | Xuất nhiều dòng nguyên tử | Phiếu gồm KEO01 10, SON 52.50 (2 lô), MAT01 100 | 1 phiếu, 4 dòng, 4 giao dịch tồn, cùng transaction | P1 | IT | BR-MAT-06 |
| TC-MAT-014 | Trả hợp lệ | Đã xuất SON 52.50; trả 2.50 về L-SON-B | Phiếu MR; ReturnedQty 2.50; NetIssued 50.00; tồn L-SON-B tăng 2.50 | P1 | IT | BR-MAT-05 |
| TC-MAT-015 | Trả vượt theo lô | Trả 31.00 về L-SON-A (xuất từ lô này 30.00) | `PRD_MAT_006` | P1 | IT | BR-MAT-05 |
| TC-MAT-016 | Trả vượt theo dòng | KEO01 NetIssued 10; trả 10.01 | `PRD_MAT_006` | P2 | IT | BR-MAT-05 |
| TC-MAT-017 | Trả không tạo lại giữ chỗ | Trả KEO01 2.00 | ReservedQty KEO01 vẫn 0 | P3 | IT | BR-MAT-07 |
| TC-MAT-018 | Thay thế hợp lệ | Thay KEO01 bằng KEO02 cho 50 sp | Dòng mới KEO02 Required = 5.00 × 0.12/0.1 = **6.00**, IsSubstitute; KEO01 Required 5.00 | P2 | IT | BR-MAT-09 |
| TC-MAT-019 | Thay thế ngoài nhóm | Thay KEO01 bằng NVL-SON01 | `PRD_MAT_009` | P2 | UT | BR-MAT-09 |
| TC-MAT-020 | Vật tư ngoài định mức | R-PMG thêm NVL-KEO02 2.00 lý do UPM-01 | Dòng IsUnplanned, Required 0; xuất cần quyền OverIssue | P3 | IT | BR-MAT-11 |
| TC-MQ-001 | Tạo yêu cầu cấp | R-SUP yêu cầu SON 52.50 | MQ Draft → Submitted | P2 | API | BR-MAT-10 |
| TC-MQ-002 | Yêu cầu vượt còn lại | Đã có MQ mở SON 52.50; tạo MQ SON 1.00 | `PRD_MAT_010` | P2 | API | BR-MAT-10 |
| TC-MQ-003 | Cấp một phần | Xuất tham chiếu MQ 30.00 | MQ PartiallyIssued | P2 | IT | BR-MAT-10 |
| TC-MQ-004 | Cấp đủ | Xuất thêm 22.50 | MQ Issued | P2 | IT | BR-MAT-10 |
| TC-MQ-005 | Hủy yêu cầu đã cấp | Cancel MQ PartiallyIssued | Lỗi chuyển trạng thái | P3 | API | BR-MAT-10 |

### 2.11 Công đoạn (TC-OP) & Downtime (TC-DT)
> Tiền điều kiện chung: MO-A Released.

| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-OP-001 | Start công đoạn Ready | NV001 Start op10 | Op10 Running; time log mở; lệnh InProgress | P1 | IT | BR-OP-02, BR-PO-10 |
| TC-OP-002 | Start công đoạn Pending | Start op20 | `PRD_OP_001` | P1 | API | BR-OP-02 |
| TC-OP-003 | Nhiều công nhân | NV001, NV002 cùng Start op10 | 2 time log mở | P2 | IT | BR-OP-02 |
| TC-OP-004 | Chồng lấn time log | NV001 đang Running op10 của MO-A; Start op10 MO khác | `PRD_OP_006` | P2 | IT | BR-OP-05 |
| TC-OP-005 | Thời lượng có Pause | Start 08:00, Pause 09:00, Resume 09:15, Stop 10:00 | Duration 105'; Paused 15' | P1 | UT | BR-OP-05 |
| TC-OP-006 | Nhập tay thời gian sai | Start 10:00, End 09:00; Start tương lai | `PRD_OP_005` | P2 | API | BR-OP-05 |
| TC-OP-007 | Báo cáo hợp lệ | Op10 Good 60, Scrap 2 (SCRAP-01) | OR ghi; Good 60, Scrap 2; op20 → Ready | P1 | IT | BR-OP-01, 03 |
| TC-OP-008 | Báo cáo toàn 0 | Good 0, Scrap 0, Rework 0 | `PRD_OP_002` | P1 | UT | BR-OP-03 |
| TC-OP-009 | Số âm | Good −1 | `PRD_OP_002` | P2 | UT | BR-OP-03 |
| TC-OP-010 | Phế thiếu lý do | Scrap 2 không lý do | `PRD_OP_003` | P1 | UT | BR-OP-03 |
| TC-OP-011 | Làm lại thiếu lý do | Rework 1 không lý do | `PRD_OP_003` | P2 | UT | BR-OP-03 |
| TC-OP-012 | Vượt đầu vào công đoạn sau | Op10 Good 60; op20 báo cáo Good 61 | `PRD_OP_004` (khả dụng 60) | P1 | IT | BR-OP-04 |
| TC-OP-013 | Biên đầu vào công đoạn đầu | Op10 lũy kế 105 OK; thêm 1 | 105 thành công; 106 `PRD_OP_004` | P1 | IT | BR-OP-04 |
| TC-OP-014 | Tự hoàn thành công đoạn đầu | Op10 lũy kế Good 98 + Scrap 2 = 100 | Op10 Completed | P1 | IT | BR-OP-06 |
| TC-OP-015 | Không tự hoàn thành khi công đoạn trước chưa xong | Op10 Good 60 (chưa xong); op20 Good 60 | Op20 vẫn Ready/Running (không Completed) | P2 | IT | BR-OP-06 |
| TC-OP-016 | Báo cáo vào công đoạn đã xong | Op10 Completed; báo cáo thêm | `PRD_OP_009` | P2 | API | BR-OP-06 |
| TC-OP-017 | Hoàn thành thủ công & Reopen | R-SUP complete op10 với Good 90; sau đó Reopen | Completed rồi Ready | P2 | IT | BR-OP-06 |
| TC-OP-018 | Báo cáo khi OnHold | MO-A OnHold; báo cáo | `PRD_PO_011` | P1 | API | BR-PO-11 |
| TC-DT-001 | Ghi downtime | M-CUT-01 DT-01 08:00–09:00 | Duration 60' | P1 | API | BR-OP-08 |
| TC-DT-002 | Chồng lấn downtime | Thêm M-CUT-01 DT-03 08:30–09:30 | `PRD_OP_008` | P1 | API | BR-OP-08 |
| TC-DT-003 | Khác máy không chồng lấn | M-CUT-02 08:30–09:30 | Thành công | P3 | API | BR-OP-08 |
| TC-DT-004 | Lý do sai loại | Downtime chọn lý do Type Scrap | Lỗi validate | P3 | API | BR-OP-08 |

### 2.12 Backflush (TC-BF)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-BF-001 | Backflush theo công đoạn | Op10 Good 60, Scrap 2 | VIT ConsumedQty = 62 × 16 = **992**; tồn VIT 1.008 | P1 | IT | BR-MAT-08 |
| TC-BF-002 | Backflush lũy kế | Tiếp TC-BF-001: op10 Good 38 | Lần này 608; tổng 1.600 | P1 | IT | BR-MAT-08 |
| TC-BF-003 | Phantom backflush ở op30 | Op30 Good 50 | THUNG 50; XOP 100 | P1 | IT | BR-MAT-08, BR-BOM-08 |
| TC-BF-004 | Làm tròn lũy kế | Biến thể: dòng SON Backflush (0.525 L/sp, 2 số lẻ); báo cáo 1, 1, 1 | 0.53, 0.52, 0.53 → tổng 1.58 (không phải 1.59) | P1 | UT | BR-MAT-08 |
| TC-BF-005 | Thiếu tồn – chặn | Biến thể tồn VIT 900; op10 Good 60 (cần 960) | `PRD_MAT_008`; op10 Good vẫn 0; không giao dịch tồn | P1 | IT | BR-MAT-08 |
| TC-BF-006 | Thiếu tồn – cho âm | Biến thể AllowNegative=true; như TC-BF-005 | Thành công; tồn VIT −60; dòng giao dịch IsNegativeStock | P2 | IT | BR-MAT-08 |
| TC-BF-007 | Backflush khi nhập kho | Biến thể: VIT không gắn công đoạn; nhập kho 10 | VIT tiêu hao 160 khi nhập | P2 | IT | BR-MAT-08, BR-FG-05 |
| TC-BF-008 | Backflush theo FEFO | Biến thể: SON Backflush, báo cáo op20 Good 40 | Tiêu hao 21.00 từ L-SON-A (không dùng L-SON-X) | P2 | IT | BR-MAT-08 |

### 2.13 Nhập kho thành phẩm (TC-FG)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-FG-001 | Nhập kho cơ bản | Op30 Good 100; nhập 100 ngày 2026-11-04 | Phiếu FR; lô **TP-BAN01-261104-01**; vào **WH-QC**, QC-Hold; FQC tạo; CompletedQty 100; lệnh Completed | P1 | IT | BR-FG-01, 02, 04, 05, BR-QC-02 |
| TC-FG-002 | Vượt Good công đoạn cuối | Op30 Good 50; nhập 51 | `PRD_FG_001` | P1 | IT | BR-FG-01, BR-OP-09 |
| TC-FG-003 | Vượt dung sai lệnh | Unit trên aggregate: giả lập Good công đoạn cuối 110 (dữ liệu lỗi/nhập tay), gọi nhận 106 | `PRD_FG_001` (trần 105 — chặn độc lập với giới hạn công đoạn) | P1 | UT | BR-FG-01, BR-PO-17 |
| TC-FG-004 | Biên dung sai | Nhập lũy kế 105 | Thành công | P2 | IT | BR-PO-17 |
| TC-FG-005 | Nhập nhiều lần cùng ngày | Nhập 40 rồi 60 | Lô …-01 và …-02 | P2 | IT | BR-FG-02 |
| TC-FG-006 | Lô nhập tay trùng | Nhập tay lô L-TP-OLD | `PRD_FG_002` | P2 | API | BR-FG-02 |
| TC-FG-007 | Hạn dùng | Biến thể ShelfLife 180; nhập 2026-11-04 | ExpiryDate 2027-05-03 | P2 | UT | BR-FG-02 |
| TC-FG-008 | Không FQC | BTP-MAT01 (không FQC) nhập 48 | Vào WH-FG khả dụng, không phiếu kiểm | P1 | IT | BR-FG-04 |
| TC-FG-009 | Lệnh không routing | BTP-CHAN01 × 200; nhập 210 | Thành công (trần 210); nhập thêm 1 → `PRD_FG_001` | P2 | IT | BR-FG-01, BR-RT-07 |
| TC-FG-010 | Phụ phẩm | Lệnh BTP-MAT01 × 50; nhập chính 48 + PP-MUN01 24 | 2 dòng nhập; Output.ReceivedQty 24 | P2 | IT | BR-FG-06 |
| TC-FG-011 | Phụ phẩm vượt dung sai | PP-MUN01 planned 25; nhập 26.26 | Lỗi (trần 26.25) | P3 | IT | BR-FG-06 |
| TC-FG-012 | Serial thành phẩm | Biến thể item serial; nhập 3 với 2 serial trùng | `PRD_FG_003` | P3 | API | BR-FG-03 |
| TC-FG-013 | Không tự Complete | Biến thể AutoComplete=false; nhập 100 | Vẫn InProgress | P3 | IT | BR-PO-14 |

### 2.14 Đảo chứng từ (TC-REV)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-REV-001 | Đảo báo cáo hợp lệ | Op10 Good 60 (op20 chưa dùng); đảo | Bản đối ứng −60; op10 Good 0; VIT trả lại 992 (đảo backflush) | P1 | IT | BR-OP-07 |
| TC-REV-002 | Đảo khi hạ nguồn đã dùng | Op10 Good 60; op20 Good 50; đảo op10 60 | `PRD_OP_007` | P1 | IT | BR-OP-07 |
| TC-REV-003 | Đảo công đoạn cuối đã nhập kho | Op30 Good 100, đã nhập 100; đảo op30 | `PRD_OP_007` | P2 | IT | BR-OP-07 |
| TC-REV-004 | Đảo nhập kho hợp lệ | Nhập 40 (chưa FQC kết quả); đảo | CompletedQty 0; tồn lô về 0; đảo backflush liên quan | P1 | IT | BR-FG-07 |
| TC-REV-005 | Đảo nhập kho khi FQC đã có kết quả | FQC Passed; đảo | `PRD_FG_004` | P1 | IT | BR-FG-07 |
| TC-REV-006 | Đảo nhập khi lô đã xuất bán | Lô đã giao 10/40; đảo | `PRD_FG_004` | P2 | IT | BR-FG-07 |
| TC-REV-007 | Đảo làm lệnh tự Reopen | Lệnh Completed (100); đảo phiếu 40 (không FQC) | CompletedQty 60; lệnh InProgress | P2 | IT | BR-FG-07 |

### 2.15 Truy xuất nguồn gốc (TC-TR)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-TR-001 | Ghi genealogy | MO-A xuất SON L-SON-A, L-SON-B; nhập lô TP-BAN01-261104-01 | Genealogy: lô TP → L-SON-A, L-SON-B | P1 | IT | BR-TR-01 |
| TC-TR-002 | Bỏ lô trả hết | Trả toàn bộ L-SON-A trước khi nhập | Genealogy chỉ có L-SON-B | P2 | IT | BR-TR-01 |
| TC-TR-003 | Truy ngược đa cấp | Lệnh BTP-MAT01 dùng L-GO-01 → nhập MAT01 (không lô) dùng cho MO-A | Truy ngược lô TP hiển thị cây tới L-GO-01 (qua lệnh BTP-MAT01) | P2 | IT | BR-TR-02 |
| TC-TR-004 | Truy xuôi | Truy xuôi L-SON-B | Ra MO-A → lô TP-BAN01-261104-01 → phiếu giao hàng (nếu có) | P1 | IT | BR-TR-02 |
| TC-TR-005 | Đảo nhập cập nhật genealogy | Đảo phiếu nhập | Liên kết genealogy của lô bị gỡ | P3 | IT | BR-TR-01 |

### 2.16 Chất lượng (TC-QC), NCR (TC-NCR), Rework (TC-RW)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-QC-001 | Kế hoạch: Min > Max | Tiêu chí Measurement Min 752 Max 748 | Lỗi validate | P2 | UT | BR-QC-01 |
| TC-QC-002 | Kế hoạch Draft không dùng | Item gắn plan Draft; nhập kho | Không tạo FQC từ plan Draft (cảnh báo cấu hình) | P2 | IT | BR-QC-01 |
| TC-QC-015 | Tự tạo IPQC | Biến thể op10 có IpqcInspectionPlanId; báo cáo op10 Good 60 | 1 phiếu IPQC Pending, LotQty 60 | P2 | IT | BR-QC-02 |
| TC-QC-003 | Cỡ mẫu Fixed | Lô 100, n=5 | SampleQty 5 | P1 | UT | BR-QC-03 |
| TC-QC-004 | Cỡ mẫu khi lô nhỏ | Lô 3, n=5 | SampleQty 3 | P2 | UT | BR-QC-03 |
| TC-QC-005 | Cỡ mẫu Percent | Lô 101, 10% | 11 (CEILING 10.1) | P2 | UT | BR-QC-03 |
| TC-QC-006 | Sai cỡ mẫu | Nhập SampleQty 4 cho FQC lô 100 | `PRD_QC_001` | P3 | API | BR-QC-03 |
| TC-QC-007 | FQC Pass | C1: 749, 750, 751, 752, 750.5; C2–C4 Pass | Passed; tồn chuyển WH-QC → WH-FG; lô Available | P1 | IT | BR-QC-04, 05 |
| TC-QC-008 | FQC Fail critical | C1 có giá trị 753 | Failed; NCR tự tạo; hàng ở WH-QC | P1 | IT | BR-QC-04, 05 |
| TC-QC-009 | Lỗi nhẹ trong ngưỡng | Chỉ C3 Fail | Passed (1 ≤ 1) | P1 | UT | BR-QC-04 |
| TC-QC-010 | Lỗi nhẹ vượt ngưỡng | C3 và C4 Fail | Failed (2 > 1) | P1 | UT | BR-QC-04 |
| TC-QC-011 | Thiếu kết quả | Bỏ trống C4 | `PRD_QC_003` | P2 | API | BR-QC-04 |
| TC-QC-012 | Sửa phiếu đã submit | PUT kết quả phiếu Passed | `PRD_QC_002` | P2 | API | BR-QC-06 |
| TC-QC-013 | Kiểm tra lại | R-QAM re-inspect phiếu Failed → Pass | Phiếu mới liên kết phiếu cũ; tồn chuyển khả dụng | P2 | IT | BR-QC-06 |
| TC-QC-014 | IPQC giới hạn đầu vào | Biến thể op10 có IPQC; op10 Good 60, IPQC Pass 50 | Op20 tối đa 50 (`PRD_OP_004` khi 51) | P2 | IT | BR-QC-05, BR-OP-04 |
| TC-NCR-001 | NCR tự tạo | Sau TC-QC-008 | NCR Open, NonconformingQty 100, lô đúng | P1 | IT | BR-NCR-01 |
| TC-NCR-002 | Tổng disposition sai | Rework 10 + Scrap 5 + UseAsIs 80 | `PRD_NCR_001` | P1 | UT | BR-NCR-02 |
| TC-NCR-003 | Disposition hợp lệ | Rework 10 + Scrap 5 + UseAsIs 85 (R-QAM) | Chấp nhận | P1 | IT | BR-NCR-02 |
| TC-NCR-004 | UseAsIs không quyền | R-QCI chọn UseAsIs | `PRD_NCR_002` | P1 | SEC | BR-NCR-03 |
| TC-NCR-005 | Thực hiện Scrap | Thực hiện dòng Scrap 5 | Giao dịch hủy 5 từ WH-QC, lý do Scrap | P1 | IT | BR-NCR-03 |
| TC-NCR-006 | Thực hiện UseAsIs | Thực hiện UseAsIs 85 | 85 chuyển WH-FG khả dụng | P2 | IT | BR-NCR-03 |
| TC-NCR-007 | Đóng NCR | Thực hiện đủ 3 dòng | NCR Closed | P2 | IT | BR-NCR-04 |
| TC-RW-001 | Tạo lệnh Rework | Từ dòng Rework 10 | Lệnh OrderType Rework, TP-BAN01 × 10, SourceType Ncr | P2 | IT | BR-RW-01 |
| TC-RW-002 | Vật tư lệnh Rework | Release lệnh Rework | Vật tư = TP-BAN01 lô gốc 10, kho WH-QC | P2 | IT | BR-RW-02 |
| TC-RW-003 | Nhập lại sau Rework | Nhập 10 | Lô TP-BAN01-261104-01-R1; vào WH-QC; FQC tạo | P2 | IT | BR-RW-02 |
| TC-RW-004 | Chi phí Rework riêng | Báo cáo giá thành | Lệnh rework gắn cờ Rework | P3 | IT | BR-RW-03 |

### 2.17 Gia công ngoài (TC-SC)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-SC-001 | Tạo đề nghị mua khi Release | Routing có op Sơn gia công (WC-SUB, vendor V01); Release lệnh 100 | 1 PR dịch vụ V01 SL 100 | P3 | IT | BR-SC-01 |
| TC-SC-002 | Chuyển vật tư gia công | Chuyển SON 52.50 sang kho V01 | Giao dịch chuyển kho; chưa tính tiêu hao | P3 | IT | BR-SC-02 |
| TC-SC-003 | Nhận về | Nhận 98 đạt, 2 lỗi | Op gia công Good 98, Scrap 2; vật tư gia công ghi tiêu hao | P3 | IT | BR-SC-03 |

### 2.18 Kế hoạch: Dự báo & MRP (TC-MRP), Năng lực (TC-CAP)
> Dữ liệu **DS-MRP** = DS-01 nhưng **không** có lệnh SX/đơn mua mở. Dự báo TP-BAN01 tuần 2026-11-16: 100. Đơn bán SO-001: TP-BAN01 × 120 giao 2026-11-18. Ngày chạy 2026-11-04.

| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-MRP-001 | Chuẩn hóa kỳ dự báo | Nhập dự báo PeriodStart 2026-11-18 (Thứ Tư) | Lưu PeriodStart 2026-11-16 | P2 | API | BR-MRP-01 |
| TC-MRP-002 | Dự báo trùng kỳ | Nhập 2 lần cùng item, kỳ | Lỗi trùng (hoặc cập nhật) — chốt: **cập nhật** | P3 | API | BR-MRP-01 |
| TC-MRP-003 | Horizon không hợp lệ | Chạy với HorizonDays 6 / 366 | `PRD_MRP_002` | P2 | API | BR-MRP-09 |
| TC-MRP-004 | Chạy đồng thời | Chạy MRP PL01 khi lần trước Running | `PRD_MRP_001` | P1 | IT | BR-MRP-09 |
| TC-MRP-005 | Lịch sử chạy | Chạy thành công | MrpRun Succeeded, đếm planned order & ngoại lệ đúng | P2 | IT | BR-MRP-09 |
| TC-MRP-010 | Tiêu dùng dự báo | Chạy DS-MRP | Nhu cầu TP-BAN01: 120 tại 2026-11-18; 0 tại 2026-11-16 | P1 | UT | BR-MRP-01 |
| TC-MRP-011 | Netting + tồn an toàn | Chạy DS-MRP | Planned TP-BAN01 **110** due 2026-11-18, start **2026-11-16** | P1 | UT | BR-MRP-04, 06 |
| TC-MRP-012 | Lô cố định | Chạy DS-MRP | BTP-CHAN01: cầu 440 tại 11-16, tồn 300 → ròng 140 → planned **200** due 11-16, start **2026-11-14** | P1 | UT | BR-MRP-02, 05 |
| TC-MRP-013 | Đủ tồn không đặt | Chạy DS-MRP | BTP-MAT01 (cầu 110, tồn 120), NVL-GO01 (cầu 1.100, tồn 5.000), NVL-SON01 (cầu 57.75, tồn khả dụng 80.00), NVL-KEO01 (11.00/50.00): không planned order | P1 | UT | BR-MRP-04 |
| TC-MRP-014 | Min/Multiple + SS | Chạy DS-MRP | NVL-VIT01: cầu 1.760, tồn 2.000, SS 500 → ròng 260 → planned mua **1.000** due 11-16, start **2026-11-07** | P1 | UT | BR-MRP-04, 05, 06 |
| TC-MRP-015 | Loại tồn hết hạn | Chạy DS-MRP | Tồn khả dụng SON không tính L-SON-X (80.00 thay vì 100.00) | P1 | UT | BR-MRP-03 |
| TC-MRP-016 | Dự báo lớn hơn đơn | Biến thể dự báo 150 | Planned 20 due 11-16 (start 11-13) và 120 due 11-18 (start 11-16) | P1 | UT | BR-MRP-01, 04 |
| TC-MRP-017 | Định cỡ FixedQty biên | Unit LotSizer FixedQty 200: ròng 140 / 400 / 401 | 200 / 400 / 600 | P2 | UT | BR-MRP-05 |
| TC-MRP-018 | Định cỡ MinMultiple | Min 1000, bội 500: ròng 260 / 1200 | 1000 / 1500 | P2 | UT | BR-MRP-05 |
| TC-MRP-019 | Định cỡ PeriodOrder | PeriodDays 7; ròng 50 @11-16, 30 @11-18, 40 @11-23 | 80 @11-16; 40 @11-23 | P2 | UT | BR-MRP-05 |
| TC-MRP-020 | Nguồn cung lệnh SX mở | Biến thể: MO TP-BAN01 × 100 Released, PlannedEnd 11-13 | Planned TP-BAN01 giảm còn 10 (= 120 + 10 − 20 − 100); vật tư phụ thuộc tính từ phần còn lại của MO | P1 | UT | BR-MRP-02, 03 |
| TC-MRP-021 | Ngoại lệ PastDueStart | Biến thể: SO-002 TP-BAN01 × 50 giao 2026-11-05 | Planned 40 due 11-05, start = 2026-11-04; ngoại lệ PastDueStart | P1 | UT | BR-MRP-06, 10 |
| TC-MRP-022 | Ngoại lệ Expedite | Đơn mua VIT 1.000 dự kiến về 11-20 trong khi cần 11-16 | Ngoại lệ Expedite | P2 | UT | BR-MRP-10 |
| TC-MRP-023 | Ngoại lệ MissingBom | Item Make không có BOM hiệu lực có nhu cầu | Ngoại lệ MissingBom, không phân rã | P2 | UT | BR-MRP-10 |
| TC-MRP-024 | Giữ planned order firm | Firm planned BTP-CHAN01 200; chạy lại | Planned firm giữ nguyên, được tính cung; không sinh trùng | P1 | IT | BR-MRP-07 |
| TC-MRP-025 | Xóa planned chưa firm | Chạy lại lần 2 | Planned chưa firm cũ bị thay; số lượng không nhân đôi | P1 | IT | BR-MRP-07 |
| TC-MRP-026 | Chuyển Make → lệnh SX | Convert planned TP-BAN01 110 | MO Draft × 110, SourceType Mrp; planned Converted | P1 | IT | BR-MRP-08 |
| TC-MRP-027 | Chuyển Buy → đề nghị mua | Convert planned VIT 1.000 | PR tạo qua `IPurchaseRequestService` | P1 | IT | BR-MRP-08 |
| TC-MRP-028 | Pegging | Xem planned BTP-CHAN01 | Pegging tới planned TP-BAN01 (440) và SafetyStock 0 | P2 | IT | BR-MRP-11 |
| TC-CAP-001 | Tải theo ngày | MO-A Released; tải WC-PNT | 2026-11-13: 559,64 phút chuẩn (77,73%); 2026-11-12: 70,36 (9,77%) | P1 | UT | BR-CAP-01, 02 |
| TC-CAP-002 | Quá tải | 2 lệnh BTP-MAT01 × 300 due 2026-11-20, Released | WC-CUT 2026-11-20: tải 1.800 / năng lực 1.620 = **111,11%** (quá tải); 2026-11-19: 40 (2,47%) | P1 | UT | BR-CAP-01, 02 |
| TC-CAP-003 | Năng lực 0 có tải | Ngoại lệ ngày lễ trùng ngày có công đoạn (dữ liệu cũ) | Hiển thị quá tải | P3 | UT | BR-CAP-02 |
| TC-CAP-004 | Loại lệnh Draft | MO Draft không tính tải | Tải không đổi | P2 | UT | BR-CAP-01 |
| TC-CAP-005 | Heatmap UI | Mở S13 tuần 16–21/11 | Ô quá tải tô đỏ, drill-down ra 2 công đoạn | P3 | UI | S13 |

### 2.19 Giá thành (TC-CST)
> Biến thể giá: giá vốn bình quân NVL-GO01 tại thời điểm xuất = **8.200.000**/m3. Lệnh **MO-D**: BTP-MAT01 × 50.

| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-CST-001 | Roll-up giá chuẩn | Roll-up BTP-MAT01 | NVL 176.000 − phụ phẩm 1.000 = 175.000; Nhân công 3.400; SXC 6.800; **Tổng 185.200** | P1 | UT | BR-CST-01 |
| TC-CST-002 | Thiếu giá chuẩn | Roll-up TP-BAN01 khi NVL-KEO01 chưa có giá chuẩn | Cảnh báo `PRD_CST_003` liệt kê NVL-KEO01 | P2 | UT | BR-CST-01 |
| TC-CST-003 | Sửa phiên bản giá đã duyệt | Sửa StandardCostItem version Approved | `PRD_CST_002` | P2 | API | BR-CST-01 |
| TC-CST-004 | Chi phí thực tế lệnh | MO-D: xuất GO01 1.150 (9.430.000), trả 0.050 (410.000), giờ công 3.0 × 60.000, giờ máy 2.8 × 120.000, phụ phẩm 24 kg, nhập 48 (short-close SC-01) | NVL 9.020.000; NC 180.000; SXC 336.000; phụ phẩm −48.000; **Tổng 9.488.000** | P1 | IT | BR-CST-02 |
| TC-CST-005 | Giá thành đơn vị | Tiếp TC-CST-004 | 9.488.000 / 48 = **197.666,6667** | P1 | UT | BR-CST-03 |
| TC-CST-006 | Phân bổ đồng phẩm | Biến thể: đồng phẩm 30%; tổng chi phí 1.000.000; nhập chính 70, đồng phẩm 30 | Chính 700.000/70 = 10.000; đồng phẩm 300.000/30 = 10.000 | P2 | UT | BR-CST-03 |
| TC-CST-007 | Chênh lệch (Standard) | Biến thể CostingMethod Standard; dữ liệu TC-CST-004 | Lượng NVL **352.000** (bất lợi); Giá NVL **220.000** (bất lợi); Hiệu suất NC **16.000** (bất lợi) | P1 | UT | BR-CST-05 |
| TC-CST-008 | Điều chỉnh giá nhập (Actual) | MO-D nhập tạm theo giá chuẩn 185.200; Close; còn tồn 48 | Điều chỉnh giá trị tồn +(197.666,6667 − 185.200) × 48 = **+598.400** | P1 | IT | BR-CST-04 |
| TC-CST-009 | Dở dang | Lệnh mới đã xuất 9.430.000, chưa nhập | WIP = 9.430.000 | P1 | IT | BR-CST-06 |
| TC-CST-010 | Khóa kỳ chặn giao dịch | Khóa kỳ 10/2026; xuất vật tư PostingDate 2026-10-31 | `PRD_GEN_003` | P1 | IT | BR-CST-07 |
| TC-CST-011 | Mở lại kỳ cần quyền | R-PLN mở kỳ 10/2026 | 403 | P2 | SEC | BR-CST-07 |
| TC-CST-012 | Hạch toán xuất NVL | Xuất GO01 9.430.000 | Bút toán Nợ 621 / Có 152 9.430.000 (mapping mặc định) | P1 | IT | BR-CST-08 |
| TC-CST-013 | Hạch toán nhập kho | Nhập 48 theo giá 185.200 | Nợ 155 / Có 154 8.889.600 | P1 | IT | BR-CST-08 |
| TC-CST-014 | Idempotent hạch toán | Gửi lại event xuất cùng DocNo | Không sinh bút toán trùng | P1 | IT | BR-CST-08 |

### 2.20 KPI & Báo cáo (TC-KPI, TC-RPT)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-KPI-001 | OEE chuẩn | M-CUT-01 ngày 2026-11-09: lịch 900', downtime Planned 60' (DT-02), Unplanned 84' (DT-01), ideal 1', Total 700, Good 665 | A 90,00%; P 92,59%; Q 95,00%; **OEE 79,17%** | P1 | UT | BR-KPI-01 |
| TC-KPI-002 | Performance > 100% | Như trên nhưng Total 800, Good 760 | P 105,82% kèm cờ cảnh báo | P2 | UT | BR-KPI-01 |
| TC-KPI-003 | Yield lệnh | CompletedQty 95, ScrappedQty 5 | 95,00% | P2 | UT | BR-KPI-02 |
| TC-KPI-004 | Đúng hạn | 4 lệnh Completed trong 11/2026, 3 có ActualEnd ≤ DueDate | 75,00% | P2 | UT | BR-KPI-03 |
| TC-KPI-005 | Tỷ lệ phế | Good 190, Scrap 10 | 5,00% | P2 | UT | BR-KPI-04 |
| TC-KPI-006 | Chênh lệch tiêu hao | GO01 thực tế 1.100, chuẩn cho 48 = 1.056 | +4,17% | P3 | UT | BR-KPI-05 |
| TC-RPT-001 | Báo cáo tình trạng lệnh | Lọc trễ hạn | Chỉ lệnh IsLate hoặc quá DueDate chưa Completed | P2 | API | H01 |
| TC-RPT-002 | Báo cáo WIP | Kỳ 11/2026 | Khớp TC-CST-009 | P2 | IT | H02, BR-CST-06 |
| TC-RPT-003 | Tiêu hao vs định mức | MO-A sau khi hoàn tất | Mỗi vật tư: chuẩn, thực tế, chênh lệch | P2 | IT | H03 |
| TC-RPT-004 | Export Excel | Export H04 | File mở được, số liệu khớp màn hình | P3 | UI | H04 |
| TC-RPT-005 | Phân quyền báo cáo | R-OPR xem báo cáo giá thành | 403 | P2 | SEC | §9 |

### 2.21 Giao diện (TC-UI)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-UI-001 | Nút hành động theo trạng thái | Mở S16 MO Draft / Released / Closed | Draft: Release, Cancel, Sửa; Released: Unrelease, Hold, Xuất VT; Closed: chỉ xem | P1 | UI | BR-PO-02 |
| TC-UI-002 | Terminal: quét QR | Quét QR MO-A trên S20 | Hiện công đoạn Ready của WC đã chọn | P2 | UI | NFR-07 |
| TC-UI-003 | Terminal: ≤ 3 chạm báo cáo | Sau khi quét, báo cáo Good 10 | Hoàn tất trong ≤ 3 lần chạm | P2 | UI | NFR-07 |
| TC-UI-004 | Hiển thị lỗi tiếng Việt | Gây `PRD_OP_004` trên terminal | Thông báo tiếng Việt rõ SL khả dụng | P2 | UI | BR-GEN-07 |
| TC-UI-005 | Mất kết nối | Ngắt mạng khi báo cáo | Báo mất kết nối, không báo thành công giả | P2 | UI | T4.13.3 |

### 2.22 Hiệu năng (TC-PERF)
| ID | Tiêu đề | Điều kiện & Bước | Kết quả mong đợi | P | Loại | Trace |
|---|---|---|---|---|---|---|
| TC-PERF-001 | Danh sách lệnh | 10.000 lệnh, trang 50, 20 VU | P95 < 1s | P2 | PERF | NFR-01 |
| TC-PERF-002 | Phân rã BOM | 5 cấp × 200 thành phần | < 2s | P2 | PERF | NFR-02 |
| TC-PERF-003 | Báo cáo + backflush | 30 vật tư backflush | < 1s | P2 | PERF | NFR-03 |
| TC-PERF-004 | MRP | 5.000 item, 90 ngày | < 10 phút; giao dịch song song không bị block > 2s | P2 | PERF | NFR-04 |
| TC-PERF-005 | Đồng thời xuất kho | 20 phiếu xuất song song cùng vật tư tồn 100, mỗi phiếu 10 | Đúng 10 thành công, 10 lỗi `PRD_MAT_003`; tồn = 0, không âm | P1 | PERF | BR-MAT-03, 06 |

### 2.23 End-to-End (TC-E2E)
| ID | Kịch bản | Bước chính | Kết quả mong đợi | P |
|---|---|---|---|---|
| TC-E2E-001 | **MVP: lệnh thủ công trọn vòng** | 1) Tạo MO-A → 2) Release (cảnh báo thiếu CHAN) → 3) Tạo lệnh con CHAN 100, sản xuất & nhập kho → 4) Xuất MAT01 100, CHAN 400, KEO01 10, SON 52.50 (FEFO) → 5) Op10: Good 98, Scrap 2 → 6) Op20: Good 98 → 7) Op30: Good 98 → 8) Nhập kho 98 (WH-QC) → 9) FQC Pass → 10) Complete (98 ≥ 98) → 11) Trả SON 1.00 → 12) Close (xác nhận kết chuyển) | Lệnh Closed; VIT tiêu hao 1.600 (lũy kế 98+2 = 100 × 16); THUNG 98, XOP 196; tồn TP WH-FG +98 (sau FQC); mọi phiếu có số; history ≥ 4 bản ghi | P1 |
| TC-E2E-002 | Chất lượng & làm lại | Biến thể E2E-001 tại bước 9: FQC Fail → NCR 98: Rework 8, Scrap 2, UseAsIs 88 → lệnh Rework 8 → nhập lại -R1 → FQC Pass | NCR Closed; WH-FG nhận 88 + 8; hủy 2; genealogy đầy đủ | P1 |
| TC-E2E-003 | Kế hoạch → thực thi | DS-MRP: chạy MRP → firm & convert TP-BAN01 110 và BTP-CHAN01 200 → Release → thực hiện → chạy MRP lại | Lần chạy 2 không đề xuất lại phần đã có lệnh; nguồn cung tính đúng | P1 |
| TC-E2E-004 | Giá thành trọn kỳ | MO-D trọn vòng → tính giá → hạch toán → khóa kỳ | Số liệu khớp TC-CST-004/005/008/012; khóa kỳ chặn giao dịch tiếp theo | P1 |
| TC-E2E-005 | Hold & đổi số lượng giữa chừng | MO-A InProgress → Hold → Resume → đổi SL 120 → hoàn tất 120 | Required, reservation, lịch cập nhật đúng; Completed ở 120 | P2 |

---

## 3. BỘ SMOKE & REGRESSION

**Smoke (chạy mỗi build, ~15 phút):** TC-GEN-005, TC-GEN-006, TC-CAL-008, TC-WC-004, TC-BOM-030, TC-BOM-032, TC-PO-001, TC-PO-012, TC-SCH-001, TC-MAT-001, TC-MAT-010, TC-OP-007, TC-BF-001, TC-FG-001, TC-PO-038, TC-E2E-001.

**Regression theo milestone:**
| Milestone | Nhóm bắt buộc |
|---|---|
| M1 (Phase 0–3) | GEN, SEC, INT, SET, PLT, ITM, CAL, WC, MCH, RSN, BOM, RT, PO, SCH, MAT, OP, FG, E2E-001 |
| Phase 4 | + BF, REV, MQ, DT, TR, QC, NCR, RW, UI, E2E-002 |
| M2 (Phase 5) | + MRP, CAP, E2E-003 |
| M3 (Phase 6) | + CST, E2E-004 |
| Phase 7 | + KPI, RPT, PERF, E2E-005 |

## 4. MA TRẬN BAO PHỦ (tóm tắt)
| Khu vực BR | Số BR | Nhóm TC | Ghi chú |
|---|---|---|---|
| GEN | 9 | TC-GEN, TC-SEC | |
| SET / PLT / ITM | 3 / 3 / 4 | TC-SET, TC-PLT, TC-ITM | |
| CAL / WC / MCH / OPS / RSN / LBR | 5 / 5 / 2 / 1 / 2 / 2 | TC-CAL, TC-WC, TC-MCH, TC-OPS, TC-RSN, TC-LBR | |
| BOM / RT | 15 / 8 | TC-BOM, TC-RT | |
| PO / SCH | 19 / 2 | TC-PO, TC-SCH, TC-UI | |
| MAT | 11 | TC-MAT, TC-MQ, TC-BF | |
| OP | 9 | TC-OP, TC-DT, TC-REV | |
| FG / TR | 7 / 2 | TC-FG, TC-REV, TC-TR | |
| QC / NCR / RW / SC | 6 / 4 / 3 / 3 | TC-QC, TC-NCR, TC-RW, TC-SC | |
| MRP / CAP | 11 / 2 | TC-MRP, TC-CAP | |
| CST / KPI | 8 / 5 | TC-CST, TC-KPI, TC-RPT | |

Quy tắc duy trì: mỗi BR mới/đổi trong BA phải có ít nhất 1 TC P1 hoặc P2; mỗi bug Critical/High phải bổ sung TC hồi quy.
