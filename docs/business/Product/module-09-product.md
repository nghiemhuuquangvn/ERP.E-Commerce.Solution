# Module 9 — Product
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (9) được gán mới trong đợt rà soát 2026-07-29 — ProductService trước đó chưa có tài liệu BA riêng dù đây là module lớn nhất về số lượng entity trong toàn hệ thống (16 entity, 10 migration). Viết theo hướng **reconciliation** hoàn toàn.

---

## 1. Mục tiêu và phạm vi

ProductService (Port 5008, Code: PRD, database `ProductDB`) là **nguồn dữ liệu gốc duy nhất** của toàn bộ danh mục sản phẩm: Product/Variant, Category, Brand, Bảng giá (PriceList), Thuộc tính động (EAV Attribute), Combo (Bundle), Sản phẩm liên quan (Cross-sell/Up-sell), Đánh giá (Review), và chuyển đổi đơn vị tính (UoM Conversion). Mọi service khác (Sales, Warehouse, POS, Promotion) chỉ tham chiếu Product/Variant theo `Id`, không lưu bản sao.

Phạm vi **không bao gồm**: tồn kho vật lý (Warehouse sở hữu — Product chỉ giữ `StockQuantity` như read-model đồng bộ một chiều qua event, không phải nguồn sự thật), giá bán thực tế hiển thị cho khách (xem ghi chú quan trọng ở §8 — đây là điểm cần chú ý nhất của module).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Merchandiser/Quản lý sản phẩm | Tạo/sửa Product/Variant, quản lý Category/Brand, thiết lập Bảng giá |
| Kho vận (gián tiếp) | Không thao tác trực tiếp — chỉ publish `InventoryUpdatedEvent` để đồng bộ `StockQuantity` |
| Khách hàng (qua Storefront) | Xem/tìm sản phẩm (nhiều endpoint GET là `[AllowAnonymous]`), viết Đánh giá |
| Các service khác (Sales, Warehouse, POS, Promotion) | Tiêu thụ `ProductCreated/Updated/Published/Discontinued/PriceChanged` để đồng bộ read-model riêng |

## 3. Quy trình nghiệp vụ chính

1. Tạo Product ở trạng thái `Draft` → (tuỳ chọn) `SubmitForReview()` chuyển `PendingReview` → `Publish()` chuyển `Active`, **bắt buộc phải có ít nhất 1 Variant, nếu không sẽ throw exception** — đây là ràng buộc nghiệp vụ cứng duy nhất chặn publish.
2. `Discontinue()` có thể gọi từ **bất kỳ trạng thái nào kể cả Draft**, không có guard, và **không cascade** — không tự động ảnh hưởng tới PriceListItem, đơn hàng đang mở, hay Bundle đang tham chiếu sản phẩm đó.
3. Giá bán: có **2 cơ chế song song, chưa hợp nhất rõ ràng** (xem ghi chú quan trọng ở §8) — `PriceList`/`PriceListItem` (theo kênh bán Retail/Wholesale/Ecommerce, hỗ trợ bậc giá theo số lượng tối thiểu) là cơ chế "đúng" dùng cho Elasticsearch/storefront, trong khi `SetProductPriceCommand` lại ghi thẳng vào `ProductVariant.Cost`.
4. Thuộc tính động (EAV): định nghĩa `ProductAttribute` (kiểu dữ liệu Text/Number/Boolean/Date/SingleSelect/MultiSelect, có thể giới hạn theo Category hoặc áp dụng toàn hệ thống) → gán giá trị cho từng Product/Variant qua `ProductAttributeValue` — hệ thống ép buộc đúng loại field được điền theo `DataType` đã khai báo.
5. Combo (Bundle): một Product bình thường có thể chứa các `ProductBundleItem` trỏ tới Product khác làm thành phần — **không có ràng buộc bundle phải đúng `ProductType=Bundle`, và không chống được vòng lặp (A chứa B, B chứa A)**.
6. Đồng bộ tồn kho: nhận `InventoryUpdatedEvent` từ Warehouse (qua RabbitMQ) → chỉ cập nhật `StockQuantity`/`LastStockSyncAt` làm read-model hiển thị, không phát lại event nào.
7. Tìm kiếm: mọi thay đổi Product tự động đưa vào `SearchIndexQueue` (bảng nội bộ, không qua Kafka) → `SearchIndexProcessor` polling mỗi 5 giây → đẩy lên Elasticsearch.

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Product (Aggregate Root)
 ├─ Status: Draft → PendingReview → Active (bắt buộc ≥1 Variant) → Discontinued (không cascade)
 │                                        └→ Reactivate() : Discontinued → Active
 ├─ Variants (owned) — Sku, Barcode, Cost, kích thước/khối lượng
 ├─ Images (owned) — có thể gắn theo Variant cụ thể, đúng 1 ảnh Primary
 └─ Translations (owned)

Category (Aggregate Root) — cây phân cấp qua ParentCategoryId (KHÔNG chống vòng lặp)
Brand (Aggregate Root) — phẳng, không phân cấp

PriceList (Aggregate Root)
 └─ PriceListItem (Entity) — unique theo (VariantId, MinQuantity): hỗ trợ bậc giá theo số lượng

ProductAttribute (Aggregate Root, định nghĩa EAV — Code/DataType bất biến sau khi tạo)
 └─ ProductAttributeOption (Entity, cho Single/MultiSelect)
ProductAttributeValue (đứng độc lập, không phải owned collection) — OwnerType (Product/Variant) + OwnerId

ProductBundleItem — BundleProductId, ComponentProductId, Quantity (chống tự-tham-chiếu, KHÔNG chống vòng lặp gián tiếp)
ProductRelation — RelationType: CrossSell | UpSell (có chiều, chống tự-tham-chiếu)
ProductReview — Rating 1-5, IsApproved mặc định TRUE (không có hàng đợi kiểm duyệt thật)
ProductExternalMapping — tồn tại trong Domain/DB nhưng KHÔNG có Command/Query/Controller nào (chết hoàn toàn ở tầng ứng dụng)
ProductUomConversion — FromUomId/ToUomId (tham chiếu cross-service, không FK), ConversionFactor
```

## 5. Database Design (`ProductDB`, đã đối chiếu 10 migration `src/Database/Product/001-010*.sql`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Products` | Id, Sku (unique filtered), Name, Slug (unique filtered), Status, CategoryId, BrandId (**có FK thật**), BaseUomId (cross-service, không FK), StockQuantity, LastStockSyncAt |
| 2 | `ProductVariants` | Id, ProductId (FK), Sku (unique filtered), Barcode, Cost, Weight, kích thước |
| 3 | `ProductImages` | Id, ProductId, VariantId, ImageUrl, IsPrimary, SortOrder |
| 4 | `Categories` | Id, Name, Code, ParentCategoryId (self-ref, không FK constraint chống vòng lặp) |
| 5 | `Brands` | Id, Code (unique), Name, LogoUrl, IsActive |
| 6 | `PriceLists` / `PriceListItems` | Channel, EffectiveDate/ExpiryDate; Item unique (PriceListId, VariantId, MinQuantity — mở rộng ở migration 010) |
| 7 | `ProductAttributes` / `ProductAttributeOptions` / `ProductAttributeValues` | CategoryId (null=toàn cục), Code, DataType, IsVariantOption/IsRequired/IsFilterable |
| 8 | `ProductBundleItems` | unique (BundleProductId, ComponentProductId) |
| 9 | `ProductRelations` | unique (ProductId, RelatedProductId, RelationType) |
| 10 | `ProductReviews` | ProductId, CustomerId, Rating, IsApproved |
| 11 | `ProductExternalMappings` | ProductId, ExternalSystem, ExternalId (bảng chết, không code ứng dụng nào dùng) |
| 12 | `ProductUomConversions` | unique (ProductId, FromUomId, ToUomId) |
| 13 | `SystemStoreProductSettings` / `TenantStoreProductSettings` | 2 tầng System/Tenant |
| 14 | `SearchIndexQueue` | hàng đợi nội bộ đẩy lên Elasticsearch, không qua Kafka |
| 15 | `OutboxMessages` | chuẩn Outbox pattern |

**Cảnh báo migration nghiêm trọng nhất module này**: migration `007_RebuildProductsAndMissingTables.sql` (bước 8/9/10) định nghĩa lại `ProductAttributes`, `ProductAttributeOptions`, `ProductAttributeValues`, `ProductExternalMappings`, `ProductReviews` với **cột hoàn toàn khác** so với schema chuẩn ở migration 003/004/006 và entity C# thật đang dùng (ví dụ: `ProductAttributes` ở 007 chỉ có `ProductId, Name`, trong khi schema thật có `Code, DataType, IsVariantOption, IsRequired, IsFilterable, SortOrder, CategoryId`). Migration 007 chỉ an toàn nếu 003/004/006 luôn chạy trước (khi đó guard `IF NOT EXISTS` sẽ tự bỏ qua) — đây là rủi ro thứ tự migration tiềm ẩn, không phải lỗi B-Tree nhưng cần ghi nhận cùng nhóm rủi ro migration.

**Không có `IEntityTypeConfiguration<T>` nào trong project** — `ApplyConfigurationsFromAssembly` không tìm thấy class nào; phần lớn entity (Product, Variant, Category, PriceList, Image, Translation, Attribute*) dựa hoàn toàn vào EF convention, chỉ 9 entity có cấu hình Fluent API thủ công trong `ProductDbContext`.

## 6. API Design (REST) — 13 Controller, phần lớn chỉ `[Authorize]` (không phân quyền chi tiết)

```
POST/GET   /products, /{id}, /sku/{sku}, /search       (GET phần lớn [AllowAnonymous])
PUT        /products/{id}/description, /seo, /brand, /base-uom, /translations
POST       /products/{id}/submit-for-review, /publish, /discontinue
POST/GET   /products/bulk-import, /export

/products/{productId}/variants, /images, /bundle-items, /relations, /reviews, /uom-conversions
/categories, /brands, /pricing (variants + lists), /product-attributes

GET        /products/search/advanced (Elasticsearch, [AllowAnonymous])
POST       /products/search/advanced/reindex-all

/settings/store-product   (settings:store-product:{system|tenant}:{view|manage})
```

**Phát hiện quan trọng về phân quyền**: chỉ 3 nhóm tài nguyên có permission string chi tiết thật sự (`Product_BrandsView/Manage`, `Product_UomConversionsView/Manage`, `settings:store-product:*`) — **toàn bộ các tài nguyên còn lại (Products, Categories, Pricing, Variants, Images, Attributes, Bundles, Relations, Search) chỉ yêu cầu `[Authorize]` (bất kỳ ai đã đăng nhập)** — bất kỳ user nào cũng có thể publish/discontinue sản phẩm hay đổi giá, không phân biệt vai trò. `ProductReviewsController` còn thiếu cả `[Authorize]` ở cấp class (chỉ action `POST` mới yêu cầu).

## 7. Event Design

**Published** (Outbox → Kafka topic `product-topic`):
- `ProductCreatedDomainEvent`, `ProductUpdatedDomainEvent`, `ProductPublishedDomainEvent`, `ProductDiscontinuedDomainEvent`, `ProductPriceChangedDomainEvent`
- Các sự kiện này **được xác nhận từng thiếu hoàn toàn** trước khi được thêm vào (comment trong code) — vì SaleOrderService/POS/Promotion cần chúng để đồng bộ read-model giá/tồn kho, trước đó các consumer này mặc định giá = 0.
- `ProductSearchIndexRequestedDomainEvent` — **không** đi qua Outbox/Kafka, được xử lý đặc biệt ngay trong `SaveChangesAsync` để ghi vào bảng `SearchIndexQueue` nội bộ.

**Consumed**:
- RabbitMQ `warehouse_exchange` / `warehouse.InventoryUpdatedEvent` → cập nhật `StockQuantity` (chỉ đọc, không phát lại).
- Kafka `mst-topic` / `SettingsChangedIntegrationEvent` (Currency) → chỉ invalidate cache, **hiện chưa có tác dụng thấy được** vì Product chưa cache Currency Settings ở đâu.

## 8. Business Rules & Validation — bao gồm các điểm cần chú ý nhất của module

- **Publish bắt buộc ≥1 Variant** — ràng buộc nghiệp vụ cứng duy nhất; `Discontinue()` hoàn toàn không có ràng buộc/cascade.
- **⚠️ Điểm quan trọng nhất — nhầm lẫn khái niệm "Cost" và "Price"**: `SetProductPriceCommand` ghi giá trị người dùng nhập thẳng vào `ProductVariant.Cost`, và `GetProductPricesQuery` cũng trả về đúng field `Cost` đó dưới tên `VariantPriceDto.Cost`. Trong khi đó, cơ chế giá "đúng" dùng cho tìm kiếm/storefront (`PriceListItem.BasePrice`/`EffectivePrice`, hỗ trợ bậc giá) lại là một đường dữ liệu khác. Hệ quả: `ProductDto.Price` ở các endpoint đọc thông thường (`GetProduct`, `ListProducts`, tìm kiếm SQL LIKE) **luôn trả về cứng giá trị 0** — chỉ endpoint tìm kiếm Elasticsearch nâng cao hoặc endpoint pricing riêng mới trả giá thật. Đây là rủi ro tích hợp lớn nhất của module, cần làm rõ và hợp nhất trước khi mở rộng thêm tính năng giá.
- Không kiểm tra trùng SKU khi tạo Product qua API thông thường (`CreateProductCommand`) — chỉ `BulkImportProductsCommand` (import CSV) mới kiểm tra; tạo trùng SKU qua API sẽ lỗi ở tầng DB (unique index), không phải lỗi validate thân thiện.
- Không chống vòng lặp phân cấp Category (`ParentCategoryId`) và không chống vòng lặp gián tiếp giữa các Bundle.
- Đánh giá sản phẩm (`ProductReview`) mặc định `IsApproved=true` — không có hàng đợi kiểm duyệt thật dù có method `Hide()` (không endpoint nào gọi).
- `ProductRepository.GetExportRowsAsync` là stub chết xác nhận — luôn trả về danh sách rỗng; export thật dùng đường khác (`GetPagedAsync`).

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 5 (Sales) | Tiêu thụ `ProductCreated`/`ProductPriceChanged` để đánh dấu đơn hàng mở khi giá đổi |
| Module 7 (Warehouse) | Publish `InventoryUpdatedEvent` → Product đồng bộ `StockQuantity` một chiều |
| Module 11 (POS), Module 20 (Promotion) | Tiêu thụ `ProductCreated`/`ProductPriceChanged` để đồng bộ read-model riêng |
| Elasticsearch (hạ tầng tìm kiếm) | Nhận cập nhật qua `SearchIndexQueue` nội bộ, không qua Kafka |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính cần ưu tiên xử lý:
1. **Nhầm lẫn "Cost" vs "Price"** (§8) — rủi ro nghiệp vụ lớn nhất, ảnh hưởng trực tiếp tới trải nghiệm khách hàng nếu API "giá thường" luôn trả 0.
2. **Phân quyền không nhất quán** — chỉ 3/10+ nhóm tài nguyên có permission chi tiết, phần còn lại chỉ cần đăng nhập là đủ quyền publish/discontinue/đổi giá.
3. **Rủi ro thứ tự migration ở bước 007** — schema định nghĩa lại khác hẳn 003/004/006, chỉ an toàn nếu chạy đúng thứ tự.
4. **`ProductExternalMapping` chết hoàn toàn** ở tầng ứng dụng dù có đủ Domain/DB.
5. Không chống vòng lặp Category/Bundle — rủi ro dữ liệu tiềm ẩn khi mở rộng danh mục lớn.

## Variant creation (R38, 2026-10-28)
`POST /api/v1/products/{id}/variants` creates a variant (SKU unique per product, uppercase letters, digits and hyphens; cost; optional weight with its unit kg / g / lb / oz, dimensions). The variant belongs to the caller's tenant. A SKU already used by the product is refused with HTTP 400. The weight is what ShippingService reads (`internal/v1/product-weights`, see `docs/business/Shipping/ShippingFee.md`) to weigh a parcel; a product without a weight is parcelled at the shop's default item weight.
**UAT:** create a 0.8 kg variant → 201 and listed with `0.8 kg`; the same SKU again → 400; another tenant does not see it.
