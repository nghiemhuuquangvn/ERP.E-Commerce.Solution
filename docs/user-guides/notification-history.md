# Hướng dẫn sử dụng: Cài đặt → Thông báo đã gửi

Dành cho nhân viên hỗ trợ và vận hành. Màn hình liệt kê **mọi thông báo hệ thống đã gửi hoặc cố gửi** cho khách hàng và nhân viên của cửa hàng (email, SMS, push, trong ứng dụng). Nghiệp vụ: [docs/business/Notification/WebhooksAndTemplates.md](../business/Notification/WebhooksAndTemplates.md) và mục 4.5 của [docs/business/Identity/AdminBackOffice.md](../business/Identity/AdminBackOffice.md). Cách hộp thoại và danh sách trống hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `vi.json`. Đã duyệt thử trên trình duyệt (tài khoản quản trị cửa hàng thử): mở trang, mở hộp thoại chi tiết một thông báo, lọc không có kết quả → Xóa lọc. Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
Quyền xem lịch sử thông báo của đơn vị. Màn hình chỉ đọc: không có nút gửi lại hay xóa.

## 2. Màn hình
- **Bộ lọc**: Trạng thái (Queued, Sending, Sent, Delivered, Failed, Bounced), Kênh (Email, Sms, Push, InApp) và ô tìm **email hoặc tiêu đề**. Đổi bộ lọc đưa về trang 1. Không có kết quả thì hiện "Không có kết quả" và nút **Xóa lọc**; chưa từng gửi gì thì hiện "Chưa có thông báo nào được gửi."
- **Bảng**: thời gian, người nhận, kênh, tiêu đề, trạng thái, lỗi (nếu có) và nút **Xem** của từng dòng.
- **Xem**: hộp thoại ghi tiêu đề, người nhận · kênh, trạng thái, **nội dung đã gửi**, khung đỏ **Lỗi** kèm số lần thử lại (nếu thất bại) và **Nhật ký gửi** (thời điểm — loại sự kiện: chi tiết). Esc hoặc **Đóng** để thoát.

## 3. Đọc trạng thái
Queued (đang chờ) → Sending (đang gửi) → Sent (đã gửi đi) → Delivered (đã tới nơi); **Failed** (thất bại, hệ thống thử lại tối đa 5 lần rồi dừng) và **Bounced** (bị trả lại, ví dụ email không tồn tại). Mở **Xem** để biết lý do.

## 4. Giới hạn đã biết
Không có nút gửi lại thủ công và không xuất danh sách; việc gửi lại do hệ thống tự thực hiện.
