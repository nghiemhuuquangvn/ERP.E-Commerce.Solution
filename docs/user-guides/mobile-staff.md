# Hướng dẫn sử dụng: Ứng dụng nhân viên trên điện thoại

Dành cho thu ngân, nhân viên kho và nhân viên giao hàng. Nút bấm lớn (52 điểm) để dùng một tay. Giao diện có tiếng Anh và tiếng Việt (theo ngôn ngữ đã lưu của máy).

> Ghi chú cho người bảo trì: nhãn trích từ `i18n/locales/vi.json`. Đã duyệt thử bản web ở khổ 375 điểm ảnh bằng tài khoản quản trị của tenant `glcheck`, **chỉ đọc**: không hoàn tất giao dịch, không sửa tồn kho, không đổi trạng thái vận đơn. **Chưa thử trên điện thoại thật.**

## 1. Đăng nhập
Nhập **Email** và **Mật khẩu** (bắt buộc). Để trống: mỗi ô hiện "Vui lòng nhập mục này.". Sai: khung đỏ "Email hoặc mật khẩu không đúng.".

## 2. Bốn tab
**Tổng quan**, **Bán hàng** (POS), **Tồn kho**, **Giao hàng**.
- **Tổng quan:** lời chào và bốn thẻ số liệu (hiện đang là 0, chưa nối dữ liệu).

## 3. Bán hàng (POS)
1. Nhập **Mã sản phẩm**, **Tên**, **Đơn giá** rồi bấm **+ Thêm**. Lặp lại cho từng mặt hàng.
2. Phần dưới màn hình hiện **Tạm tính**, **Thuế (10 %)** và **Tổng**.
3. Chọn phương thức: **Tiền mặt**, **Thẻ tín dụng**, **Ví điện tử**.
4. Bấm **Hoàn tất giao dịch** (khóa khi chưa có dòng nào). Thành công: thông báo xanh "Hoàn tất giao dịch. Tổng: …" và đơn bán được xóa để làm đơn mới. Thất bại: thông báo đỏ, **các dòng vẫn còn** để thử lại.
- Mã máy POS (`TERM-001`) và thuế 10 % đang cố định trong ứng dụng.

## 4. Tồn kho
- Ô **Tìm sản phẩm**; mỗi dòng có tên, SKU và số **có thể bán** (đỏ khi 0, vàng dưới 5, xanh còn lại).
- Nút **Điều chỉnh tồn kho của …** mở khung dưới màn hình: hiện số hiện có, nhập **Số lượng mới** (số nguyên), **Lưu** hoặc **Hủy**. Nhập chữ: "Hãy nhập một số nguyên." và chưa gửi gì.
- **Lưu ý quan trọng:** chức năng lưu điều chỉnh hiện **chưa hoạt động**, vì máy chủ đã chuyển sang quy trình *yêu cầu điều chỉnh có phê duyệt*; bấm Lưu sẽ ra thông báo đỏ. Điều chỉnh tồn kho hãy dùng trang quản trị cho tới khi màn hình này được nối lại (DEFERRED R107).

## 5. Giao hàng
Mỗi thẻ: mã vận đơn, nhãn trạng thái (Chờ lấy hàng, Đã lấy hàng, Đang giao, Đã giao, Thất bại), mã đơn. Nút **Đánh dấu … đang giao** cho vận đơn chưa giao xong.
- **Hiện tại danh sách có thể báo lỗi** "Không tải được nội dung" kèm nút **Thử lại**, vì đường dẫn danh sách vận đơn chưa khớp với Gateway (R107). Dùng trang quản trị Giao hàng trong lúc chờ sửa.

## 6. Dùng với cỡ chữ lớn và trợ năng
Mọi nút, tab và lựa chọn có tên để trình đọc màn hình đọc; thông báo xanh/đỏ được đọc ngay; cỡ chữ tăng đến 130 % vẫn không che mất nút.
