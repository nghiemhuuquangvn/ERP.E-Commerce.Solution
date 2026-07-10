# Customers — Nghiệp vụ (trong phạm vi SaleOrderService)

> SaleOrderService **không có aggregate Customer riêng**. Tài liệu này ghi
> nhận thực trạng và đề xuất, không mô tả một tính năng đã tồn tại.

## Thực trạng

`SalesOrder.CustomerId/CustomerName/CustomerEmail`
(`ERP.SO.Domain/Entities/SalesOrder.cs:9-11`) chỉ là 3 field snapshot được
truyền vào lúc `Create()` — không có bảng/entity Customer nào trong
`SalesDB`, không có lịch sử tổng hợp đơn hàng theo khách hàng ngoài query
`GET /api/sales-orders/customer/{customerId}` (lọc theo `CustomerId`, không
tổng hợp/join gì thêm), không có phân khúc khách hàng, không có hạn mức tín
dụng (credit limit) dù README của service có mô tả tính năng này.

## Vì sao không nên tạo Customer aggregate mới trong SaleOrderService

CRMService (Port 5006, Code: CRM) đã sở hữu domain "khách hàng" theo đúng
ranh giới nghiệp vụ: Contact & customer management, Lead tracking, Opportunity,
Case — xem `CLAUDE.md` mục CRM Service. Tạo thêm một Customer aggregate trong
SaleOrderService sẽ:

- Vi phạm nguyên tắc "mỗi service sở hữu database của mình" (SalesDB không nên
  chứa hồ sơ khách hàng đầy đủ — đó là dữ liệu của CrmDB).
- Trùng lặp dữ liệu (CustomerName/Email đã là bản sao snapshot, hợp lý cho mục
  đích hiển thị trên đơn hàng tại thời điểm đặt hàng — không nên mở rộng
  thành nguồn sự thật thứ hai cho hồ sơ khách hàng).

## Đề xuất

Nếu nghiệp vụ cần credit-limit/phân khúc/lịch sử khách hàng đầy đủ:
1. Xây dựng các field đó ở **CRMService** (đã có Contact/Lead), không phải
   SaleOrderService.
2. SaleOrderService, khi cần kiểm tra hạn mức tín dụng lúc `Approve()`, gọi
   API CRMService (đồng bộ) hoặc tiêu thụ một sự kiện `CreditLimitUpdatedEvent`
   từ CRM để cache cục bộ (theo đúng mẫu Outbox/consumer đã dùng ở nơi khác
   trong hệ thống) — không tạo bảng Customer trùng lặp trong SalesDB.
3. `CustomerName/CustomerEmail` trên `SalesOrder` tiếp tục giữ vai trò snapshot
   hiển thị, không nên trở thành nơi cập nhật hai chiều với CRM.
