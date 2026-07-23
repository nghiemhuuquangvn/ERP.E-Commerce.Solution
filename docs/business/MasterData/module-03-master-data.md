# Module 3 — Master Data
## Technical Design Specification | ERP.E-Commerce.Solution

> ✅ **Tin tốt**: đây là module đã build nhiều nhất trong 20 module — Customer, geographic hierarchy (Country/Province/District), Warehouse, Shipping Method, UOM, Tax Category, Department, Position đều đã có. Phần còn thiếu tập trung vào **Product Catalog** (Item/Item Group/Brand) và **Pricing/Currency** — đúng ra là phần quan trọng nhất cho một hệ Ecommerce.

---

## 1. Mục tiêu và phạm vi

Master Data là tập dữ liệu dùng chung, ít thay đổi, được tham chiếu bởi hầu hết module nghiệp vụ (Sales, Purchasing, Warehouse, Finance). Nguyên tắc thiết kế: **mỗi entity chỉ có một nguồn sự thật (single source of truth)**, các module khác chỉ FK tham chiếu, không copy dữ liệu.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Master Data Admin | CRUD toàn bộ master data |
| Purchasing | Quản lý Supplier, Price List mua |
| Sales | Quản lý Price List bán, xem Item/Customer |
| Warehouse | Xem Item/UOM/Warehouse (đã có) |
| Finance | Quản lý Currency/Exchange Rate, Tax |

## 3. Quy trình nghiệp vụ chính

1. Tạo Item Group (cây phân cấp) → tạo Brand → tạo Item, gán Item Group + Brand + UOM cơ bản.
2. Item có thể có nhiều UOM quy đổi (VD: 1 Thùng = 24 Lon) → cần bảng `ItemUomConversion`.
3. Tạo Supplier, gán Item mà Supplier đó cung cấp (`SupplierItem` — kèm giá mua tham khảo, lead time).
4. Price List: tạo theo Customer Group/Channel (Retail/Wholesale/Ecommerce), có hiệu lực theo khoảng thời gian.
5. Exchange Rate: cập nhật tỷ giá hàng ngày (thủ công hoặc tích hợp API tỷ giá ngân hàng), Sales/Purchasing dùng để quy đổi khi giao dịch đa tiền tệ.

## 4. Domain Model (Aggregate Roots — DDD)

```
ItemGroup (Aggregate Root, self-referencing tree)
 └─ Item (Aggregate Root)
     ├─ ItemUomConversion (Entity)
     ├─ ItemAttribute (Entity — màu/size cho Ecommerce)
     ├─ ItemVariant (Entity — SKU theo attribute, quan trọng cho Ecommerce)
     ├─ ItemImage (Entity)
     └─ SupplierItem (Entity, join sang Supplier)

Brand (Aggregate Root, flat)
Supplier (Aggregate Root) — cấu trúc tương tự Customer đã có, có thể tái dùng chung base class "BusinessPartner"
Currency (Aggregate Root, flat)
 └─ ExchangeRate (Entity, time-series theo Currency + ngày)

PriceList (Aggregate Root)
 └─ PriceListItem (Entity — Item + giá + hiệu lực)

PaymentTerm (Aggregate Root, flat: Net 30, COD, 50% deposit...)
```

**Bounded Context**: `MasterData` — đã tồn tại, chỉ mở rộng thêm sub-domain `Catalog` (Item/Brand/ItemGroup/Variant) vì đây thực chất là một bounded context riêng biệt trong DDD chuẩn (Product Catalog thường tách khỏi Master Data thuần vì thay đổi nhanh hơn nhiều — cân nhắc tách khi hệ thống lớn).

## 5. Database Design — chỉ liệt kê phần **CHƯA CÓ**, cần build mới

**Nhóm Product Catalog (ưu tiên cao nhất — core của Ecommerce)**
| # | Bảng | Cột chính |
|---|---|---|
| 1 | `ItemGroups` | Id, ParentItemGroupId, Code, Name, SortOrder |
| 2 | `Brands` | Id, Code, Name, LogoUrl |
| 3 | `Items` | Id, ItemGroupId, BrandId, Code, Name, BaseUomId (FK→UOM đã có), Description, IsActive, IsSellable, IsPurchasable |
| 4 | `ItemUomConversions` | Id, ItemId, FromUomId, ToUomId, ConversionFactor |
| 5 | `ItemAttributes` | Id, Code, Name (Color, Size...) |
| 6 | `ItemAttributeValues` | Id, ItemAttributeId, Value (Đỏ, XL...) |
| 7 | `ItemVariants` | Id, ItemId, SKU, Barcode, AttributeValueIds (JSON hoặc bảng join) |
| 8 | `ItemImages` | Id, ItemId, VariantId (nullable), Url, SortOrder, IsPrimary |

**Nhóm Supplier & Purchasing tham chiếu**
| # | Bảng | Cột chính |
|---|---|---|
| 9 | `Suppliers` | Id, Code, Name, TaxCode, AddressId (FK→geographic đã có), PaymentTermId | Nên kế thừa cấu trúc chung với Customer nếu code hiện tại cho phép (base class `BusinessPartner`) |
| 10 | `SupplierItems` | Id, SupplierId, ItemId, SupplierItemCode, LastPurchasePrice, LeadTimeDays |

**Nhóm Currency & Pricing**
| # | Bảng | Cột chính |
|---|---|---|
| 11 | `Currencies` | Id, Code (VND/USD), Name, Symbol, DecimalPlaces |
| 12 | `ExchangeRates` | Id, FromCurrencyId, ToCurrencyId, Rate, EffectiveDate |
| 13 | `PaymentTerms` | Id, Code, Name, NetDays, DepositPercent |
| 14 | `PriceLists` | Id, Code, Name, Channel (Retail/Wholesale/Ecommerce), CurrencyId, EffectiveFrom, EffectiveTo |
| 15 | `PriceListItems` | Id, PriceListId, ItemId, VariantId (nullable), UnitPrice, MinQuantity (bậc giá theo số lượng) |

→ **15 bảng mới**, đúng bằng phần "thực sự thiếu" trong tổng ~50 bảng đề xuất ban đầu (phần còn lại — Customer, Warehouse, UOM, Tax, geographic — đã có sẵn).

## 6. API Design (REST)

```
GET    /api/v1/catalog/items?itemGroupId=&brandId=&search=    → PaginatedResult<ItemDto>
GET    /api/v1/catalog/items/{id}/variants
POST   /api/v1/catalog/items
GET    /api/v1/catalog/items/{id}/price?priceListCode=&quantity=   → tính giá theo bậc số lượng
GET    /api/v1/suppliers/{id}/items
POST   /api/v1/currencies/{code}/exchange-rates
GET    /api/v1/price-lists/{code}/effective-price?itemId=&date=
```

## 7. Event Design

| Event | Trigger | Consumer |
|---|---|---|
| `ItemCreated` / `ItemPriceChanged` | CRUD Item/PriceListItem | Ecommerce (đồng bộ hiển thị website), Search index (nếu dùng Elasticsearch) |
| `ExchangeRateUpdated` | Cập nhật tỷ giá hàng ngày | Sales, Purchasing, Finance (quy đổi lại giao dịch mở) |
| `SupplierItemPriceChanged` | Nhà cung cấp báo giá mới | Purchasing (cảnh báo lập PO) |

## 8. Business Rules & Validation

- `Item.Code` unique toàn hệ thống (không theo Company, vì Item là danh mục dùng chung nhiều công ty nếu multi-company).
- `PriceListItem` không được overlap thời gian hiệu lực cho cùng Item + PriceList (check constraint hoặc validate ở Application layer).
- `ExchangeRate`: luôn lấy bản ghi có `EffectiveDate` gần nhất ≤ ngày giao dịch — cần index theo `(FromCurrencyId, ToCurrencyId, EffectiveDate DESC)`.
- `ItemVariant.SKU` unique toàn hệ thống, `Barcode` unique nếu không rỗng (cho phép null nhưng không cho trùng khi có giá trị — filtered unique index).
- Item không cho xoá cứng nếu đã phát sinh giao dịch (Sales Order/PO/Stock) — chỉ `IsActive = false`.

## 9. So sánh & Reconciliation với project hiện tại

| Entity đề xuất ban đầu | Trạng thái hiện tại | Khuyến nghị |
|---|---|---|
| Customer | ✅ Đã có | Không đổi. Cân nhắc trích base class `BusinessPartner` dùng chung với Supplier mới |
| Warehouse | ✅ Đã có | Không đổi |
| UOM | ✅ Đã có | Tái sử dụng làm `BaseUomId` cho Item |
| Tax | ✅ Đã có (tax categories) | Gán vào Item qua `TaxCategoryId` |
| Country/Province/District | ✅ Đã có (geographic hierarchy) | Dùng cho `Supplier.AddressId` |
| Shipping Method | ✅ Đã có | Dùng ở Sales/Ecommerce sau này |
| **Item, Item Group, Brand** | ❌ Chưa có | **Ưu tiên cao nhất** — không có Product Catalog thì Ecommerce (Module 18) không thể build |
| **Supplier** | ❌ Chưa có | Cần cho Module 6 Purchasing |
| **Currency, Exchange Rate** | ❌ Chưa có | Cần nếu có giao dịch đa tiền tệ; nếu chỉ VND, có thể hoãn |
| **Payment Term, Price List** | ❌ Chưa có | Price List đặc biệt quan trọng — Sales/Ecommerce không thể tính giá nếu thiếu |

**Kết luận effort**: 15 bảng mới thay vì 50 — vì phần lớn "Master Data" cổ điển đã có, phần còn thiếu chính là **Product Catalog + Pricing**, đây cũng là phần đáng làm trước nhất trong toàn bộ 3 module đã phân tích, vì nó mở khoá được Module 5 (Sales) và Module 18 (Ecommerce).

## 10. Khuyến nghị triển khai .NET

- Tách `Catalog` thành sub-namespace riêng trong `ERP.E_Commerce.Domain/MasterData/Catalog/` — vì đây là phần sẽ thay đổi/mở rộng nhiều nhất (thêm attribute, variant, image... theo yêu cầu Ecommerce UI đã thiết kế mockup trước đó).
- `ItemVariant` nên có index phủ (covering index) trên `(ItemId, SKU)` vì đây là truy vấn nóng nhất khi hiển thị sản phẩm trên Frontend/Mobile/POS.
- Cân nhắc dùng `AttributeValueIds` dạng bảng join `ItemVariantAttributeValues` thay vì JSON column — dễ query/filter theo attribute hơn (VD: lọc "màu đỏ, size L") dù tốn thêm 1 bảng.
- Giá (`UnitPrice`) dùng kiểu `decimal(18,4)`, không dùng `float`/`double` — tránh sai số khi tính toán tài chính, nhất quán với convention Finance sau này.
- Nếu chưa cần đa tiền tệ ngay, có thể **hoãn Currency/ExchangeRate**, tập trung Item/Brand/PriceList trước để mở khoá Sales sớm hơn.
