# Module 20 — Promotion
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (20) được gán mới trong đợt rà soát 2026-07-29 — trước đó chưa có quy ước đánh số toàn cục cho toàn bộ 15 service, chỉ một số module (01, 02, 03, 05, 07, 08, 12, 18) đã có số. Tài liệu này viết theo hướng **reconciliation** (đối chiếu với code thật đã tồn tại), không phải đề xuất thiết kế mới — PromotionService đã được code đầy đủ. Xem thêm `docs/business/Promotion/LoyaltyAndCouponSecurity.md` (tài liệu hẹp, chỉ về lỗ hổng bảo mật Coupon/Loyalty đã được vá) cho bối cảnh lịch sử của phần Loyalty.

---

## 1. Mục tiêu và phạm vi

PromotionService (Port 5012, Code: PRM, database `PromotionDB`) quản lý toàn bộ vòng đời khuyến mãi: chương trình giảm giá (Promotion), mã giảm giá (Coupon), Flash Sale theo khung giờ, và chương trình khách hàng thân thiết (Loyalty). Đây là module duy nhất trong hệ thống có logic tính toán discount thực thi ngay tại domain layer (không phải chỉ là dữ liệu cấu hình) — `Promotion.CalculateDiscount()` chạy công thức Percentage/FixedAmount/BuyXGetY ngay trong aggregate.

Phạm vi **không bao gồm**: xử lý thanh toán (PaymentService), áp giá theo Price List theo kênh bán (ProductService) — Promotion chỉ tính discount *thêm vào* trên giá đã có sẵn từ Product/Sales, không thay thế cơ chế định giá gốc.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Marketing/Promotions Manager | Tạo/kích hoạt/tạm dừng/huỷ Promotion, tạo Flash Sale, sinh mã Coupon hàng loạt |
| Customer (qua Storefront) | Nhập mã Coupon ở giỏ hàng (kiểm tra hợp lệ trước khi đăng nhập), redeem Coupon khi checkout (yêu cầu đăng nhập) |
| Sales/POS | Áp dụng Promotion tự động (GetApplicablePromotions) khi tạo Order |
| Customer Service | Điều chỉnh điểm Loyalty thủ công cho khách (Earn/Redeem) |

## 3. Quy trình nghiệp vụ chính

1. **Promotion**: Marketing tạo `Promotion` ở trạng thái `Draft` (loại giảm giá: Percentage/FixedAmount/BuyXGetY/FreeShipping; phạm vi: Order/Product/Category) → `Activate()` → hệ thống tự động áp dụng khi đơn hàng thoả điều kiện (`MinOrderAmount`, còn hạn `MaxUsageTotal`/`MaxUsagePerUser`, trong khoảng `StartDate`–`EndDate`).
2. **Coupon**: sinh từ một `Promotion` (đơn lẻ qua `GenerateCoupon(code)` hoặc hàng loạt qua `GenerateBulkCoupons(count, prefix)` — mã hàng loạt tự sinh ngẫu nhiên 8 ký tự hex, không trùng lặp trong phạm vi Promotion đó). Khách nhập mã ở giỏ hàng → `validate-coupon` (không cần đăng nhập) → khi thanh toán → `redeem-coupon` (bắt buộc đăng nhập, tiêu huỷ mã, phát `CouponRedeemedEvent`).
3. **Flash Sale**: tạo ở trạng thái `Scheduled`, thêm sản phẩm kèm giá gốc/giá sale/số lượng phân bổ (`AddItem` — chỉ cho phép khi còn `Scheduled`, giá sale phải < giá gốc) → `Start()` (yêu cầu có ít nhất 1 item, phát `FlashSaleStartedEvent`) → khách đặt hàng gọi `TryReserve(productId, quantity)` để giữ số lượng giới hạn (kiểm tra còn hàng theo `RemainingQuantity = AllocatedQuantity − SoldQuantity`) → `Complete()` khi kết thúc.
4. **Loyalty**: mỗi User có một `LoyaltyAccount` (tạo lười — `Create(userId)` khi lần đầu cần). `EarnPoints(points, reason)` cộng điểm và tự động cập nhật hạng (`Tier`) theo ngưỡng luỹ kế (`TotalPoints`, không phải điểm khả dụng): Bronze (0) → Silver (≥1,000) → Gold (≥5,000) → Platinum (≥10,000). Tốc độ tích điểm theo hạng: Bronze=1đ/đơn vị tiền, Silver=2, Gold=3, Platinum=5. `RedeemPoints(points)` trừ vào `AvailablePoints` (không đổi `TotalPoints`, nên không hạ hạng khi tiêu điểm).

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Promotion (Aggregate Root)
 ├─ DiscountType: Percentage | FixedAmount | BuyXGetY | FreeShipping
 ├─ Scope: Order | Product | Category
 ├─ Status: Draft → Active → Paused | Expired | Cancelled
 ├─ ApplicableProductIds / ApplicableCategoryIds (JSON column, EF Core)
 └─ Coupons (owned collection)
     Coupon (Entity) — Code, IsUsed, UsedAt, UsedByUserId

FlashSale (Aggregate Root)
 ├─ Status: Scheduled → Active → Completed | Cancelled
 └─ Items (owned collection)
     FlashSaleItem (Entity) — ProductId, OriginalPrice, SalePrice,
     AllocatedQuantity, SoldQuantity, computed RemainingQuantity/DiscountPercentage

LoyaltyAccount (Aggregate Root)
 ├─ Tier: Bronze | Silver | Gold | Platinum (tự tính lại mỗi lần EarnPoints)
 └─ Transactions (owned collection)
     LoyaltyTransaction (Entity) — Earn/Redeem log
```

**Ghi chú quan trọng**: `Promotion.CalculateDiscount()` chứa toàn bộ logic tính toán (percentage trên `MinOrderAmount`/scope-filtered amount, fixed-amount capped bởi `orderAmount`, BuyXGetY sắp xếp item theo giá tăng dần rồi tính số lượng "tặng" theo bội số `BuyQuantity+GetQuantity`, và luôn áp trần `MaxDiscountAmount` nếu có) — đây là business logic thật đang chạy trong production, không phải placeholder.

## 5. Database Design (`PromotionDB`, đã đối chiếu `src/Database/Promotion/*.sql` + `PromotionService/*.sql`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Promotions` | Id, Name, DiscountType, Scope, DiscountValue, MaxDiscountAmount, MinOrderAmount, MaxUsageTotal, MaxUsagePerUser, CurrentUsage, BuyQuantity, GetQuantity, StartDate, EndDate, Status, ApplicableProductIds (JSON), ApplicableCategoryIds (JSON) |
| 2 | `Coupons` | Id, PromotionId (FK), Code (unique cùng TenantId), IsUsed, UsedAt, UsedByUserId |
| 3 | `LoyaltyAccounts` | Id, UserId (unique cùng TenantId), TotalPoints, AvailablePoints, Tier |
| 4 | `LoyaltyTransactions` | Id, AccountId (FK), UserId, Points, Type (Earn/Redeem), Reason |
| 5 | `FlashSaleItems` | Id, FlashSaleId (FK), ProductId, unique (FlashSaleId, ProductId), OriginalPrice, SalePrice, AllocatedQuantity, SoldQuantity |
| 6 | `PromotionProductCache` | Category (index) — bảng cache tra cứu nhanh, không phải nguồn sự thật |
| 7 | `OutboxMessages` | chuẩn Outbox pattern dùng chung toàn hệ thống |

**Lưu ý cấu trúc thư mục migration**: 2 folder `src/Database/Promotion/` và `src/Database/PromotionService/` cùng trỏ vào một database thật (`PromotionDB`) — đây là bất nhất về đặt tên thư mục, không phải lỗi B-Tree, nhưng nên gộp lại một folder duy nhất trong lần dọn dẹp kế tiếp (xem `docs/architecture/database-performance-indexing-review.md` §7).

**Index cần lưu ý** (chi tiết đầy đủ ở tài liệu Index Review): `IX_Promotions_Status (Status, StartDate, EndDate)` dẫn đầu bằng cột trạng thái thay đổi thường xuyên (Draft→Active→Paused/Expired/Cancelled) — ứng viên chuyển sang filtered index nếu query thực tế chỉ quan tâm `Status = Active`. `Coupons` có 3 index đơn lẻ (`Code+TenantId`, `PromotionId`, `TenantId`) — ứng viên gộp lại.

## 6. API Design (REST) — đã xác nhận đúng route/permission trong code

```
GET    /api/v1/promotions                        → chỉ Promotion đang Active (permission: promotions:view)
GET    /api/v1/promotions/all                     → tất cả (promotions:manage)
GET    /api/v1/promotions/{id}
POST   /api/v1/promotions                         → tạo (promotions:manage)
POST   /api/v1/promotions/{id}/activate
POST   /api/v1/promotions/{id}/pause
POST   /api/v1/promotions/{id}/cancel
POST   /api/v1/promotions/{id}/generate-coupons
POST   /api/v1/promotions/applicable              → GetApplicablePromotions cho Sales/POS
POST   /api/v1/promotions/validate-coupon         → [AllowAnonymous] — Storefront gọi được trước khi đăng nhập (giỏ hàng)
POST   /api/v1/promotions/redeem-coupon           → yêu cầu đăng nhập, KHÔNG yêu cầu permission staff (khách tự redeem)

GET    /api/v1/flash-sales?activeOnly=            (promotions:view)
GET    /api/v1/flash-sales/{id}
POST   /api/v1/flash-sales                        (promotions:manage)
POST   /api/v1/flash-sales/{id}/start
POST   /api/v1/flash-sales/{id}/cancel
POST   /api/v1/flash-sales/{id}/reserve           (promotions:manage — xem ghi chú bảo mật bên dưới)

GET    /api/v1/loyalty/{userId}                    (promotions:view)
POST   /api/v1/loyalty/{userId}/earn               (promotions:manage — staff-only, xem §8)
POST   /api/v1/loyalty/{userId}/redeem             (promotions:manage — staff-only, xem §8)
```

## 7. Event Design

| Event | Trigger | Publisher |
|---|---|---|
| `PromotionCreatedEvent` | `Promotion.Create()` | PromotionService |
| `CouponRedeemedEvent` | `Coupon.Redeem()` | PromotionService |
| `LoyaltyPointsEarnedEvent` | `LoyaltyAccount.EarnPoints()` | PromotionService |
| `FlashSaleStartedEvent` | `FlashSale.Start()` | PromotionService |

**Ghi chú**: `RedeemPoints()` (trừ điểm) **không** phát domain event nào — chỉ `EarnPoints()` có. Đây là điểm bất đối xứng đáng lưu ý nếu sau này cần audit trail đầy đủ hai chiều qua event.

## 8. Business Rules & Validation (đã xác nhận trong code, bao gồm 1 lỗ hổng bảo mật đã vá)

- `EndDate` phải sau `StartDate` khi tạo Promotion; `BuyXGetY` bắt buộc phải có cả `BuyQuantity` và `GetQuantity`.
- `IsActive()` kiểm tra đồng thời: `Status == Active`, đang trong khoảng `StartDate`–`EndDate`, và chưa vượt `MaxUsageTotal` — thiếu bất kỳ điều kiện nào thì `CalculateDiscount()` trả về 0.
- Coupon không thể redeem hai lần (`IsUsed` chặn).
- FlashSale: không thể thêm item sau khi đã `Start()`; không thể `Start()` nếu chưa có item nào; giá sale bắt buộc nhỏ hơn giá gốc; một sản phẩm không được thêm 2 lần vào cùng FlashSale.
- **Lỗ hổng bảo mật đã phát hiện và vá (xem code comment tại `LoyaltyController`)**: trước đây endpoint Earn/Redeem tin tưởng hoàn toàn `userId` do caller truyền vào route, không kiểm tra permission — bất kỳ ai (kể cả chưa đăng nhập) đều có thể tự cộng/trừ điểm cho **bất kỳ user nào khác**. Đã xác nhận không có Storefront/mobile app nào gọi các endpoint Loyalty này (dead/unreachable từ frontend hiện tại) nên không có luồng "khách tự đổi điểm" cần giữ lại — quyết định xử lý: gate cả hai endpoint bằng `promotions:manage` (staff-only, ví dụ CSKH điều chỉnh điểm thủ công). Nếu sau này có luồng khách tự đổi điểm thật, cần thêm cơ chế xác thực "chỉ tự thao tác trên chính mình" riêng (giống `PreferencesController` của NotificationService), không phải nới lỏng permission hiện tại.
- Tương tự, `POST /flash-sales/{id}/reserve` cũng được gate `promotions:manage` (staff-only) vì hiện chưa có luồng khách đặt hàng Flash Sale nào gọi trực tiếp endpoint này từ frontend — cùng nguyên tắc thận trọng như Loyalty.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 5 (Sales) | Sales gọi `GetApplicablePromotionsQuery` khi tạo Order để tự động áp discount |
| Module 18 (POS) | POS cũng gọi API Promotion để áp coupon/discount tại quầy |
| Module 9 (Product) | Promotion tham chiếu `ProductId`/Category theo Id only (không FK DB) |
| Storefront (frontend) | `validate-coupon` (ẩn danh, giỏ hàng) và `redeem-coupon` (đăng nhập, checkout) là 2 điểm chạm duy nhất hiện có từ customer-facing app |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Tài liệu hẹp trước đó (`LoyaltyAndCouponSecurity.md`) chỉ ghi lại phần vá lỗ hổng bảo mật Loyalty/Coupon — tài liệu này (module-20) là bản đầy đủ đầu tiên bao phủ toàn bộ nghiệp vụ Promotion/Coupon/FlashSale/Loyalty. Không phát hiện sai lệch lớn giữa domain code và các endpoint — toàn bộ business logic tính discount, sinh coupon, quản lý flash sale, và tích điểm loyalty đều là code thật đang chạy, không phải scaffold. Điểm cần theo dõi: `RedeemPoints()` thiếu domain event (bất đối xứng với `EarnPoints()`), và cấu trúc thư mục migration `Promotion`/`PromotionService` nên được gộp.
