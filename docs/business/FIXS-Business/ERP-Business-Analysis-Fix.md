# Root file
`ERP-Business-Analysis.md`
### 3.1 Identity Service — Xác Thực & Phân Quyền
#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~Xác thực 2 bước (2FA/MFA)~~ | ~~Cao~~ | **Đã triển khai** — SMS/Email OTP, xem `docs/business/Identity/ExternalAuthAndTwoFactor.md` |
| ~~Khóa tài khoản sau N lần sai mật khẩu~~ | ~~Cao~~ | **Đã có sẵn từ trước** khi viết tài liệu này — `User.RecordLoginFail()` khoá sau 5 lần sai (mục này trước đó ghi nhầm là thiếu) |
| Audit log đăng nhập | Cao | **SQL đã có sẵn nhưng chưa dùng**: bảng `SecurityAuditLogs`/`LoginAttempts`/`DeviceSessions` tồn tại trong `001_CreateAuthDB.sql` nhưng không có DbSet/entity C# nào tương ứng trong `AuthDbContext` — schema mồ côi, cần viết Domain entity + Application write-path mới thực sự hoạt động |
| Session management | Trung | Bảng `DeviceSessions`/`TokenBlacklist` cũng đã có sẵn trong SQL nhưng chưa được dùng — cùng tình trạng schema mồ côi như trên |
| API Key management | Trung | Cho service-to-service calls |
| Password policy configuration | Thấp | Bảng `PasswordHistories` cũng đã có sẵn trong SQL nhưng chưa được dùng |
| LDAP/Active Directory integration | Thấp | Cho doanh nghiệp lớn |
| Published: `UserUpdated`, `RoleAssigned`, `PermissionGranted` | Cao | Các service khác cần đồng bộ |

### 3.2 Product Service — Quản Lý Sản Phẩm

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~Tìm kiếm & lọc sản phẩm (full-text search)~~ | ~~Cao~~ | **Đã triển khai** — Elasticsearch, xem mục ✅ ở trên. Storefront UI lọc/tìm kiếm theo facet vẫn CHƯA làm (phạm vi backend only) |
| Bulk import/export (Excel/CSV) | Cao | Nhập hàng nghìn sản phẩm |
| Trạng thái sản phẩm workflow | Cao | Draft → Review → Published → Discontinued |
| SEO fields (slug, meta title, meta description) | Trung | Cho storefront |
| Product bundles / combo | Trung | Bán kèm, bộ sản phẩm |
| Cross-sell / upsell relationships | Trung | "Khách hàng cũng mua..." |
| Stock quantity sync từ Warehouse | Cao | Real-time tồn kho trên storefront |
| `ProductUpdated` integration event | Cao | Thông báo SalesOrder, POS, Promotion |
| `ProductPriceChanged` integration event | Cao | Thông báo Promotion tính lại |
| Application layer: command handlers đầy đủ | Cao | Features folder chưa complete |
| ~~Attribute system (custom attributes)~~ | ~~Thấp~~ | **Đã triển khai** — xem `docs/architecture/extensible-attributes-pattern.md` và `ProductAttributes`/`ProductAttributeValues` trong ProductDB |
| Product reviews & ratings | Thấp | Khách hàng đánh giá |

### 3.3 Sale Order Service — Đơn Hàng Bán

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `ApproveOrderCommand` | Cao | Duyệt đơn hàng (thủ công hoặc auto) |
| `CancelOrderCommand` | Cao | Hủy đơn với lý do |
| `UpdateOrderCommand` | Cao | Cập nhật địa chỉ, ghi chú trước khi xử lý |
| `ListOrdersQuery` với filter/sort/page | Cao | Xem danh sách đơn hàng |
| `GetOrdersByCustomerQuery` | Cao | Lịch sử đơn hàng của khách |
| Controller endpoints đầy đủ | Cao | Hiện chỉ có 1 controller rất hạn chế |
| Apply promotion/coupon khi tạo đơn | Cao | Gọi PromotionService |
| Order notes / internal comments | Trung | Ghi chú nội bộ cho team |
| Partial fulfillment | Trung | Xuất một phần đơn hàng |
| Order timeline / audit trail | Trung | Lịch sử thay đổi trạng thái |
| Consume `ProductPriceChanged` event | Cao | Cảnh báo giá thay đổi sau khi đặt |
| Return/Refund order flow | Cao | Khách trả hàng → hoàn tiền |
| `SalesOrderDelivered` event | Cao | Trigger loyalty points, CRM update |

### 3.4 Purchase Order Service — Đơn Hàng Mua

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreatePurchaseOrderCommand` + Handler | Cao | Command chưa có handler |
| `ApprovePurchaseOrderCommand` | Cao | Workflow duyệt mua hàng |
| `ReceiveGoodsCommand` (GRN) | Cao | Nhận hàng → trigger Warehouse |
| `ListPurchaseOrdersQuery` | Cao | Xem danh sách PO |
| `GetPurchaseOrderQuery` | Cao | Chi tiết PO |
| `CancelPurchaseOrderCommand` | Trung | Hủy PO |
| Integration Event Consumers | Cao | Hiện không consume bất kỳ event nào |
| `PurchaseOrderReceived` integration event | Cao | Trigger WarehouseService nhập kho |
| Supplier evaluation / rating | Thấp | Đánh giá nhà cung cấp |
| Price comparison giữa các supplier | Trung | Báo giá nhiều nhà cung cấp |
| `ReceiptScheduled` event | Cao | Lên lịch nhận hàng |
| Three-way matching | Thấp | PO vs GRN vs Invoice |

### 3.5 Warehouse Service — Kho Hàng

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Nhập kho từ PO (`ReceiveStockCommand`) | Cao | Consume `PurchaseOrderReceived` event |
| Quản lý vị trí kho (Location CRUD API) | Cao | Rack, Shelf, Bin management |
| Kiểm kê vật lý (Stocktake / Physical Count) | Cao | So sánh thực tế với hệ thống |
| Transfer Order (chuyển kho) | Trung | Di chuyển hàng giữa các kho |
| Cảnh báo tồn kho tối thiểu (reorder point) | Cao | Tự động tạo PR khi stock < min |
| Lot/Batch tracking | Trung | Theo dõi lô hàng |
| Serial number tracking | Trung | Tracking thiết bị theo số serial |
| Expiry date management | Trung | Quản lý hạn sử dụng |
| Stock adjustment (điều chỉnh tồn kho) | Cao | Xử lý hàng vỡ, mất mát |
| Publish `ReceiptCompleted` event | Cao | Thông báo khi nhập kho xong |
| Publish `StockAlert` event | Trung | Cảnh báo tồn kho thấp |
| Inventory valuation (FIFO/LIFO/Average) | Thấp | Cho kế toán |

### 3.6 Payment Service — Thanh Toán

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Payment gateway integration thực | Cao | Stripe, VNPay, MoMo, ZaloPay |
| Webhook handler từ gateway | Cao | Nhận callback async từ gateway |
| `PaymentMethod` entity (saved cards) | Trung | Lưu thẻ đã dùng |
| Partial refund | Cao | Hoàn tiền một phần |
| Installment plan | Thấp | Trả góp |
| Multi-currency support | Trung | USD, VND, EUR |
| Consume `InvoiceCreated` từ Accounting | Trung | Thanh toán theo invoice |
| Payment reconciliation report | Trung | Đối soát với ngân hàng |
| Fraud detection integration | Thấp | Phát hiện giao dịch gian lận |
| `PaymentAuthorized` event | Cao | Phân biệt authorize vs capture |

### 3.7 Shipping Service — Vận Chuyển

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Carrier API integration thực | Cao | GHN, GHTK, J&T, FedEx, DHL |
| Shipping rate calculation | Cao | Tính phí theo cân nặng/khoảng cách |
| Label printing | Trung | PDF shipping label |
| Failed delivery handling | Cao | Giao thất bại → retry/return |
| Return shipment flow | Cao | Khách trả hàng → RMA |
| Bulk tracking update | Trung | Cập nhật tracking nhiều đơn cùng lúc |
| `InventoryReserved` event consumer | Cao | Lên lịch pickup sau khi kho confirm |
| Proof of delivery (POD) | Trung | Ảnh chụp, chữ ký xác nhận |
| Estimated delivery date calculation | Trung | ETA dựa theo route |
| `ShipmentFailed` event | Cao | Giao thất bại notify các service khác |

### 3.8 POS Service — Bán Lẻ Tại Quầy

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Terminal entity & management API | Cao | Quản lý máy POS, kích hoạt |
| Consume `ProductCreated` event | Cao | Sync sản phẩm mới xuống POS |
| Consume `PromotionCreated` event | Cao | Sync khuyến mãi xuống POS |
| Consume `PaymentProcessed` event | Cao | Xác nhận thanh toán từ gateway |
| Offline mode & sync queue | Cao | Giao dịch khi mất mạng |
| Barcode / QR scanner integration | Trung | Tìm sản phẩm nhanh |
| Cash denomination tracking | Trung | Đếm tiền từng mệnh giá |
| Receipt printing | Trung | In hóa đơn nhiệt |
| Daily Z-report | Cao | Báo cáo doanh thu cuối ngày |
| Shift summary | Cao | Tổng kết ca làm việc |
| Customer display integration | Thấp | Màn hình phụ cho khách |
| Split payment | Trung | Vừa tiền mặt vừa thẻ |
| Real-time inventory deduction | Cao | Giảm tồn kho ngay khi bán |

### 3.9 Promotion Service — Khuyến Mãi

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `FlashSale` entity (chưa có trong domain) | Cao | Tồn tại trong command nhưng thiếu entity |
| Consume `ProductCreated` event | Cao | Apply promotion rules cho sản phẩm mới |
| Consume `SalesOrderCreated` event | Cao | Earn loyalty sau khi đặt hàng |
| Promotion eligibility rules engine | Cao | Điều kiện áp dụng phức tạp |
| Bulk coupon code generation | Trung | Tạo hàng nghìn coupon cùng lúc |
| Promotion scheduling (auto start/end) | Trung | Background job tự động bật/tắt |
| Tier-based loyalty (Bronze/Silver/Gold) | Trung | Hạng thành viên |
| Combo promotions (Buy X Get Y) | Trung | Mua 2 tặng 1 |
| Category-level discount | Trung | Giảm giá cả danh mục |
| Promotion analytics | Thấp | Hiệu quả từng chương trình |
| `GetApplicablePromotions` API | Cao | SalesOrder/POS gọi để lấy KM áp dụng được |
| `ValidateCoupon` API | Cao | Kiểm tra coupon còn hiệu lực không |
| `GetLoyaltyBalance` API | Cao | Xem số điểm của khách |

### 3.10 CRM Service — Quản Lý Khách Hàng

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreateContactCommand` + Handler | Cao | CRUD contact |
| `CreateLeadCommand` + Handler | Cao | Tạo lead mới |
| `QualifyLeadCommand` | Cao | Chuyển lead → opportunity |
| `CreateOpportunityCommand` | Cao | Pipeline bán hàng |
| `OpenCaseCommand` / `ResolveCaseCommand` | Cao | Hỗ trợ khách hàng |
| Consume `UserCreated` event | Cao | Tạo contact từ user mới |
| Consume `SalesOrderCreated` event | Cao | Link đơn hàng vào contact |
| Consume `PaymentReceived` event | Trung | Lịch sử thanh toán |
| Published events: `LeadCreated`, `LeadQualified`, `OpportunityWon`, `CaseOpened`, `CaseResolved` | Cao | Trigger notifications, reports |
| Activity logging (call, email, meeting) | Trung | Lịch sử tương tác |
| Contact tags & segments | Trung | Phân nhóm khách hàng |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Email integration | Thấp | Gửi/nhận email trong CRM |
| Calendar / appointment | Thấp | Lịch hẹn |

### 3.11 HRM Service — Nhân Sự

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `HireEmployeeCommand` + Handler | Cao | Onboarding nhân viên mới |
| `TerminateEmployeeCommand` | Cao | Offboarding |
| `CreateDepartmentCommand` | Cao | Quản lý cơ cấu tổ chức |
| `SubmitLeaveRequestCommand` | Cao | Nhân viên nộp đơn xin nghỉ |
| `ApproveLeaveCommand` | Cao | Manager duyệt nghỉ phép |
| `ProcessPayrollCommand` | Cao | Tính lương hàng tháng |
| `GeneratePaySlipCommand` | Cao | Tạo phiếu lương |
| Attendance tracking | Cao | Chấm công vào/ra |
| Consume `UserCreated` event | Cao | Link user account với employee |
| Published events: `EmployeeHired`, `EmployeeTerminated`, `PaySlipGenerated`, `LeaveApproved` | Cao | Trigger notifications |
| Org chart | Trung | Sơ đồ tổ chức |
| Performance review | Thấp | Đánh giá nhân viên |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Shift management | Trung | Ca làm việc |
| Overtime calculation | Trung | Tính lương OT |

### 3.12 Accounting Service — Kế Toán

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreateInvoiceCommand` | Cao | Khi PaymentProcessed |
| `ApproveBillCommand` | Cao | Khi PurchaseOrderReceived |
| `PostJournalEntryCommand` | Cao | Ghi bút toán vào GL |
| `CloseFiscalPeriodCommand` | Cao | Đóng kỳ kế toán |
| Consume `SalesOrderCreated` | Cao | Tạo hóa đơn AR |
| Consume `PurchaseOrderCreated` | Cao | Tạo bill AP |
| Consume `PaymentProcessed` | Cao | Ghi nhận tiền thu |
| Consume `WarehouseMovement` | Trung | Điều chỉnh giá trị tồn kho |
| Published: `InvoiceCreated`, `BillApproved`, `JournalEntryPosted`, `FiscalPeriodClosed` | Cao | Trigger reports |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Chart of Accounts management | Cao | Danh mục tài khoản |
| AR Aging report | Cao | Công nợ phải thu |
| AP Aging report | Cao | Công nợ phải trả |
| Bank reconciliation | Trung | Đối soát ngân hàng |
| Tax management (VAT) | Cao | Khai báo thuế |
| Trial balance | Cao | Bảng cân đối thử |
| P&L Statement | Cao | Báo cáo lãi lỗ |
| Balance Sheet | Cao | Bảng cân đối kế toán |

### 3.13 Notification Service — Thông Báo

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Email provider implementation | Cao | SendGrid / AWS SES / SMTP thực sự |
| SMS provider implementation | Cao | Twilio / VIETTEL / VNPT SMS |
| FCM/APNS push implementation | Cao | Firebase + Apple Push |
| Template variable substitution engine | Cao | Replace {{customerName}}, {{orderId}} |
| Retry mechanism (exponential backoff) | Cao | Khi gửi thất bại |
| Notification scheduling | Trung | Gửi theo lịch |
| Digest emails | Thấp | Tổng hợp nhiều thông báo vào 1 email |
| Unsubscribe management | Trung | Tuân thủ GDPR |
| Webhook delivery (outbound) | Thấp | Notify external systems |
| Bulk notification | Trung | Gửi campaign email marketing |

### 3.14 Reporting Service — Báo Cáo & Phân Tích

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Kafka consumers cho TẤT CẢ events | Cao | Hiện không có event consumer nào |
| Fact tables schema & EF models | Cao | FactSalesOrders, FactPayments, FactInventory, FactPOS |
| Dimension tables schema & EF models | Cao | DimCustomers, DimProducts, DimTime, DimEmployees |
| ETL handlers (event → OLAP) | Cao | Transform và load vào fact/dim tables |
| Sales Dashboard data query | Cao | Revenue, orders, AOV theo thời gian |
| Inventory Report query | Cao | Stock levels, turnover rate |
| Payment Summary query | Cao | Revenue by payment method |
| HR Dashboard query | Cao | Headcount, attendance, leave |
| Pre-aggregated materialized views | Trung | Performance cho large dataset |
| Export to Excel/CSV | Trung | Download báo cáo |
| Real-time KPI calculation | Cao | Số liệu gần real-time |