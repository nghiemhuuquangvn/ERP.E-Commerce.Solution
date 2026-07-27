# Module 18 — Ecommerce + POS
## Technical Design Specification | ERP.E-Commerce.Solution

> ⚠️ **Module này không tự chứa nghiệp vụ bán hàng** — nó là **kênh** (channel) tạo ra SalesOrder (Module 5). Nguyên tắc cốt lõi: Ecommerce/POS chỉ sở hữu trải nghiệm mua hàng (Cart, Checkout, Payment Gateway, POS Terminal), còn Order/Invoice/Inventory/Finance vẫn dùng chung logic đã phân tích ở các module trước — **không tạo bảng SalesOrder song song riêng cho Ecommerce**.

---

## 1. Mục tiêu và phạm vi

Ecommerce + POS là hai kênh bán hàng (Sales Channel) khác nhau nhưng dùng chung backend Sales/Inventory/Finance đã thiết kế. Website phục vụ khách hàng tự mua online; POS phục vụ nhân viên bán hàng tại quầy/cửa hàng, có yêu cầu **hoạt động offline** khi mất mạng (khác biệt lớn nhất so với Website).

Phạm vi **không bao gồm**: xử lý Order/Invoice/Inventory (thuộc Module 5/7/8 — Ecommerce chỉ gọi API của các module đó), kế toán thanh toán (thuộc Module 12 — Ecommerce chỉ ghi nhận giao dịch cổng thanh toán, Finance mới là sổ sách).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Website Visitor/Customer | Duyệt sản phẩm, thêm giỏ hàng, checkout |
| POS Cashier | Bán hàng tại quầy, mở/đóng ca |
| Store Manager | Xem báo cáo POS theo ca, xử lý đổi trả tại quầy |
| Payment Gateway (bên thứ 3) | VNPay/Momo/Stripe... callback kết quả thanh toán |

## 3. Quy trình nghiệp vụ chính

**Website:**
1. Duyệt sản phẩm (query Module 3 Catalog + Module 8 Available Quantity) → thêm vào `Cart` (session-based, chưa login vẫn dùng được qua `SessionId`).
2. Checkout → nếu chưa có tài khoản, tạo `Customer` (Module 3) + `User` (Module 2, nếu yêu cầu đăng nhập) → chọn địa chỉ giao hàng, `ShippingMethod` (đã có), `PaymentMethod`.
3. Xác nhận Checkout → gọi API Module 5 tạo `SalesOrder` (OrderSource = Ecommerce) → chuyển sang `PaymentGatewayTransaction` chờ thanh toán.
4. Payment Gateway callback (thành công/thất bại) → nếu thành công, publish event xác nhận Order → Sales tiếp tục luồng như đã thiết kế (Delivery Request sang Warehouse).
5. Khách theo dõi đơn hàng qua `OrderTrackingView` (build từ SalesOrder + Delivery status, không phải bảng riêng).

**POS:**
1. Cashier mở ca (`PosSession`) → quét Barcode (Module 7) → thêm vào giỏ hàng tại quầy (`PosCart`, tương tự Cart nhưng không cần session web).
2. Thanh toán tại quầy (tiền mặt/thẻ/QR) → tạo `SalesOrder` (OrderSource = POS) ngay lập tức, `Status = Confirmed` luôn (không qua bước chờ thanh toán online) → xuất kho ngay nếu bán trực tiếp tại kho tại chỗ.
3. Nếu mất mạng: POS ghi giao dịch local (`PosOfflineQueue`) → khi có mạng lại, đồng bộ lên server theo thứ tự, dùng `ApiKeys` (đã thiết kế ở Module 2) để xác thực máy POS.
4. Đóng ca (`PosSession.Status = Closed`) → đối chiếu tiền mặt thực tế vs hệ thống → chênh lệch ghi vào `PosSessionVariance`.

## 4. Domain Model (Aggregate Roots — DDD)

```
Cart (Aggregate Root — Website, session hoặc customer-based)
 └─ CartItem (Entity)

Checkout (Aggregate Root — quá trình chuyển Cart → SalesOrder, tồn tại ngắn)
 └─ tham chiếu CartId, kết quả là SalesOrderId (Module 5)

PaymentGatewayTransaction (Aggregate Root)
 └─ trạng thái: Initiated → Pending → Succeeded/Failed → (nếu Succeeded) publish event cho Sales

PosSession (Aggregate Root — 1 ca làm việc của 1 Cashier tại 1 Terminal)
 └─ PosTransaction (Entity, tham chiếu SalesOrderId đã tạo trong ca)
 └─ PosSessionVariance (Entity)

PosTerminal (Aggregate Root — thiết bị vật lý, gắn WarehouseId/StoreId)
PosOfflineQueue (Aggregate Root — hàng đợi đồng bộ khi offline)

Wishlist (Aggregate Root, phụ — không bắt buộc giai đoạn đầu)
ProductReview (Aggregate Root, phụ — không bắt buộc giai đoạn đầu)
```

**Bounded Context**: `Sales Channel` (gộp Ecommerce + POS vì cùng bản chất "kênh tạo Order", chỉ khác UI/luồng thanh toán) — gọi API của `Sales`, `MasterData`, `Inventory` qua interface đã thiết kế, không truy cập DB trực tiếp.

## 5. Database Design (đề xuất bảng)

**Nhóm Website — Cart & Checkout**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Carts` | Id, CustomerId (nullable), SessionId, Status (Active/Abandoned/Converted), ExpiresAt |
| 2 | `CartItems` | Id, CartId, ItemId, VariantId, Quantity, UnitPriceSnapshot |
| 3 | `Checkouts` | Id, CartId, ShippingAddressId, ShippingMethodId (FK→đã có), PaymentMethod, SalesOrderId (nullable đến khi tạo xong) |
| 4 | `Wishlists` | Id, CustomerId, ItemId | Phụ, có thể hoãn |
| 5 | `ProductReviews` | Id, ItemId, CustomerId, Rating, Comment, Status (Pending/Approved) | Phụ, có thể hoãn |

**Nhóm Payment Gateway**
| # | Bảng | Cột chính |
|---|---|---|
| 6 | `PaymentGatewayTransactions` | Id, SalesOrderId (nullable đến khi Order tạo), Gateway (VNPay/Momo/Stripe), Amount, Status, GatewayTransactionRef, CallbackPayload (JSON) |
| 7 | `PaymentGatewayConfigs` | Id, Gateway, MerchantId, ApiKeyEncrypted (Key Vault), IsSandbox, IsActive |

**Nhóm POS**
| # | Bảng | Cột chính |
|---|---|---|
| 8 | `PosTerminals` | Id, TerminalCode, WarehouseId (FK→đã có), Name, IsActive |
| 9 | `PosSessions` | Id, TerminalId, CashierId (FK→User), OpenedAt, ClosedAt, OpeningCashAmount, ClosingCashAmount, Status |
| 10 | `PosTransactions` | Id, SessionId, SalesOrderId (FK→Module 5), PaymentMethod, Amount |
| 11 | `PosSessionVariances` | Id, SessionId, ExpectedAmount, ActualAmount, VarianceAmount, Note |
| 12 | `PosOfflineQueues` | Id, TerminalId, PayloadJson, Status (Pending/Synced/Failed), CreatedAtLocal, SyncedAt |

→ **12 bảng cốt lõi** (bỏ Wishlist/Review nếu muốn tối giản hơn nữa còn 10) — so với ~80 đề xuất ban đầu. Con số gốc rất có thể tính cả bảng CMS nội dung website (Banner, Blog, SEO...) chưa cần thiết cho giai đoạn xây nền tảng giao dịch.

## 6. API Design (REST)

```
POST   /api/v1/shop/cart/items                              → thêm sản phẩm vào giỏ (session hoặc customer)
GET    /api/v1/shop/cart                                     → xem giỏ hiện tại
POST   /api/v1/shop/checkout                                  → tạo Checkout, gọi Module 5 tạo SalesOrder
POST   /api/v1/shop/payment/{gateway}/create-transaction
POST   /api/v1/shop/payment/{gateway}/callback                → webhook từ cổng thanh toán (không cần auth JWT, verify signature riêng)
GET    /api/v1/shop/orders/{orderId}/tracking

POST   /api/v1/pos/sessions/open
POST   /api/v1/pos/sessions/{id}/transactions                 → tạo PosTransaction + gọi Module 5 tạo SalesOrder ngay
POST   /api/v1/pos/sessions/{id}/close
POST   /api/v1/pos/offline-sync                                → đồng bộ hàng loạt khi có mạng lại
```

## 7. Event Design

| Event | Trigger | Consumer |
|---|---|---|
| `PaymentGatewaySucceeded` | Callback cổng thanh toán thành công | Sales (confirm Order chính thức, publish `SalesOrderConfirmed` như đã thiết kế) |
| `PaymentGatewayFailed` | Callback thất bại | Cart quay lại trạng thái Active, Notification cho khách |
| `CartAbandoned` | Cart không checkout sau X giờ (scheduled job) | Marketing/Notification (nhắc nhở) — phụ, có thể hoãn |
| `PosSessionClosed` | Đóng ca | Finance (tổng hợp doanh thu tiền mặt theo ca) |

## 8. Business Rules & Validation

- `CartItem.UnitPriceSnapshot` chốt giá tại thời điểm thêm vào giỏ nhưng **phải re-validate giá + tồn kho tại bước Checkout** (giá/tồn có thể đổi giữa lúc thêm giỏ và lúc thanh toán) — không tin giá đã snapshot khi tạo SalesOrder thật.
- `PaymentGatewayTransaction` callback phải verify chữ ký (signature) từ cổng thanh toán — tuyệt đối không tin payload không xác thực để tránh giả mạo xác nhận thanh toán.
- POS: `SalesOrder` tạo trực tiếp `Status = Confirmed` (khác Website phải chờ Payment Gateway) vì thanh toán tại quầy coi như tức thời — nhưng vẫn phải qua đúng luồng kiểm tra Available Quantity (Module 8) trước khi xác nhận, tránh bán vượt tồn.
- `PosOfflineQueue`: khi đồng bộ lại, phải xử lý theo đúng thứ tự thời gian tạo tại local, và phải kiểm tra lại tồn kho tại thời điểm sync — nếu hết hàng trong lúc offline, cần flow xử lý ngoại lệ (thông báo Cashier, không tự động huỷ giao dịch đã bán cho khách).
- `PosSession` không cho mở ca mới nếu ca trước chưa đóng trên cùng `TerminalId`.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 2 (Identity) | Customer login trên Website; `ApiKeys` cho xác thực POS Terminal |
| Module 3 (Master Data) | Catalog (Item/Variant/PriceList) hiển thị trên Website/POS |
| Module 5 (Sales) | **Tạo SalesOrder qua API, không có bảng Order riêng ở Ecommerce** |
| Module 7 (Warehouse) | Barcode scan tại POS; `DeliveryRequested` khi Website order cần giao |
| Module 8 (Inventory) | Check Available Quantity trước Checkout và trước xác nhận POS |
| Module 12 (Finance) | `PosSessionClosed` → đối chiếu doanh thu tiền mặt theo ca |

## 10. So sánh & Reconciliation — điểm dễ làm sai nhất

| Cám dỗ thiết kế sai | Vì sao sai | Đúng nên làm |
|---|---|---|
| Tạo bảng `EcommerceOrders` riêng | Trùng lặp với `SalesOrders` (Module 5), gây 2 nguồn sự thật | Dùng chung `SalesOrders`, chỉ khác `OrderSource` |
| Tạo `EcommercePayments` ghi sổ kế toán | Trùng vai trò với `PaymentReceipts` (Module 12) | `PaymentGatewayTransactions` chỉ là **integration log** với cổng thanh toán, Finance mới ghi sổ khi nhận event thành công |
| POS tự trừ kho trực tiếp trong bảng riêng | Trùng với `GoodsIssues` (Module 7) | POS gọi cùng luồng `DeliveryRequested`/`GoodsIssue` như Website, chỉ khác là xác nhận tức thời |

## 11. Khuyến nghị triển khai .NET

- Ecommerce/POS nên là **BFF (Backend for Frontend)** layer mỏng — gọi API nội bộ của Sales/MasterData/Inventory, không chứa business logic tính giá/tồn kho riêng (tránh logic bị lệch giữa 2 kênh).
- POS cần thiết kế client offline-first thật sự (local SQLite/IndexedDB) — phần đồng bộ (`PosOfflineQueue`) nên implement idempotent để tránh tạo trùng SalesOrder khi retry sync.
- Payment Gateway integration: mỗi gateway 1 adapter riêng implement chung interface `IPaymentGatewayAdapter` (Strategy pattern) — dễ thêm gateway mới sau này.
- Webhook callback endpoint (`/payment/{gateway}/callback`) không dùng JWT auth thông thường — verify bằng signature/secret riêng của từng gateway, đặt rate-limit riêng để chống lạm dụng.
- Cân nhắc rõ: giai đoạn đầu có thể **bỏ qua CMS nội dung** (Banner/Blog/SEO trong 80 bảng gốc) hoàn toàn — dùng CMS có sẵn (Strapi/headless CMS) nếu cần, không tự build lại trong ERP.
