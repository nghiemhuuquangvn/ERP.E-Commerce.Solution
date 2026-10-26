> **2026-10-26 (P1-8):** RFQs and supplier quotations exist (§5 gap 2 closed). See `ProcureToPay.md`.

# Supplier — Nghiệp vụ

> Mô tả hành vi thực tế trong code (`src/services/PurchaseOrderService`).

## 1. Entity `Supplier`

`ERP.PO.Domain/Entities/Supplier.cs`: `Code`, `Name` (bắt buộc), `ContactPerson`,
`Email`, `Phone`, `Address` (value object), `IsActive` (mặc định `true`).
Vòng đời: `Create` → `Update` (đổi thông tin liên hệ) → `Activate`/`Deactivate`
(cờ boolean, không xoá dữ liệu).

**Không có** trên entity này: điều khoản thanh toán (payment terms), mã số
thuế, thông tin ngân hàng, hạn mức công nợ, hoặc loại tiền tệ giao dịch riêng
theo supplier.

## 2. Entity `SupplierRating`

`ERP.PO.Domain/Entities/SupplierRating.cs` — đánh giá nhà cung cấp: `Rating`
(1-5, validate ở `Create`), `Comment` tuỳ chọn, `RatedBy`. Đây là tính năng
được bổ sung 2026-07-03 ("trước đó hoàn toàn chưa có").

## 3. API Endpoints (`SuppliersController`, quyền `supplier:*`)

| Method | Route | Quyền | Ghi chú |
|---|---|---|---|
| POST | `/api/suppliers` | `Create` | |
| GET | `/{id}` | `Read` | |
| GET | `/api/suppliers` | `Read` | Lọc `isActive`, phân trang |
| PUT | `/{id}` | `Update` | Dùng `_userContextService.GetCurrentUser().UserId` làm `UpdatedBy` — **đúng chuẩn** (khác với PurchaseOrdersController hard-code "system", xem `PurchaseOrder.md` mục 4) |
| POST | `/{id}/deactivate` | `Deactivate` | Tương tự, dùng user thật |
| POST | `/{id}/ratings` | `Rate` | Tạo `SupplierRating`, trả `201 Created` |
| GET | `/{id}/ratings` | `Read` | Danh sách rating + điểm trung bình |

## 4. Liên quan tới so sánh giá

`ComparePricesForProductQuery` (`GET /api/purchase-orders/compare-prices`) so
sánh đơn giá một `productCode` **giữa các đơn mua hàng lịch sử** (không phải
giữa các nhà cung cấp qua một quy trình RFQ chủ động).

## 5. Khoảng trống & Đề xuất

1. **Không có điều khoản thanh toán, mã số thuế, thông tin ngân hàng, hạn mức
   công nợ** trên Supplier — cần thiết cho AP/kế toán khi tích hợp sâu hơn với
   AccountingService.
2. **Không có RFQ (Request for Quotation)** thực sự — mời nhiều nhà cung cấp
   báo giá cho cùng một nhu cầu mua hàng rồi so sánh, chọn nhà thắng thầu.
   Hiện chỉ có so giá theo lịch sử đơn hàng cũ.
3. **Supplier không liên kết với SharedMasterDataService** — toàn bộ dữ liệu
   supplier nằm riêng trong `PurchasingDB`, không có làm giàu dữ liệu
   (enrichment) hay đối chiếu với master data dùng chung của hệ thống (vd. mã
   số thuế công ty, ngành nghề theo chuẩn chung).
4. **Không có hợp đồng/thoả thuận khung (contract/agreement)** giữa công ty và
   supplier (thời hạn, điều khoản giá cố định, SLA giao hàng).
