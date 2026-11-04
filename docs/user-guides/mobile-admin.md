# Hướng dẫn sử dụng: Ứng dụng quản trị ERP trên điện thoại

Dành cho người duyệt đơn mua, đơn bán, đơn nghỉ phép và người xem số liệu bán hàng. Mỗi tenant (cửa hàng/công ty) có **một bản cài riêng** với tên "ERP Admin" (tenant SYSTEM) hoặc "ERP Admin - tên tenant"; có thể cài nhiều bản trên cùng một máy. Giao diện có tiếng Anh và tiếng Việt.

> Ghi chú cho người bảo trì: nhãn trích từ `i18n/locales/vi.json` của app. Bản đầu **chỉ xem, duyệt, từ chối, hủy**; không tạo hoặc sửa đơn trên điện thoại. Xem thêm `docs/business/Mobile/AdminApp.md`. Tài khoản cần đúng quyền (bảng ở mục 7).

## 1. Đăng nhập
Màn hình ghi "Đăng nhập vào" kèm nhãn tenant của bản cài (ví dụ SYSTEM, glcheck). Nhập **Email** và **Mật khẩu** (bắt buộc), bấm **Đăng nhập**.
- Sai tài khoản: "Sai email hoặc mật khẩu." (hoặc câu trả lời của máy chủ, ví dụ tài khoản bị khóa).
- Không có mạng hoặc sai địa chỉ máy chủ: "Không kết nối được máy chủ. Kiểm tra mạng." (không phải lỗi mật khẩu).
- **Mã hai bước:** nếu tài khoản bật xác thực hai bước, màn hình đổi sang "Mã xác nhận cho …". Nhập mã 6 số (chỉ nhận chữ số), bấm **Xác nhận**. **Gửi lại mã** dùng được sau 30 giây. Mã sai: "Mã chưa đúng.".
- Lần sau mở app không phải đăng nhập lại; hết phiên thì app tự về màn đăng nhập.
- Tài khoản của tenant khác không đăng nhập được vào bản cài của tenant này.

## 2. Các tab
Thanh dưới có tối đa 3 mục (Trang chủ và hai mục tiếp theo). Mục còn lại (ví dụ **Nhân sự** khi tài khoản có đủ bốn mục) mở từ **menu ☰** ở góc trên bên trái. Chỉ hiện mục tài khoản được phép dùng (theo quyền và gói giấy phép của tenant): **Trang chủ**, **Mua hàng**, **Bán hàng**, **Nhân sự**. Góc trên bên phải có **chuông thông báo** và nhãn tenant.

### Menu ☰
Bấm ☰ để mở bảng trượt từ bên trái: tên và email, tenant, danh sách mục được phép dùng (mục đang mở được tô nền), **Thông báo**, **Ngôn ngữ**, địa chỉ máy chủ, phiên bản và **Đăng xuất** (có hỏi xác nhận). Bấm một mục để mở; bấm vùng tối bên ngoài, nút ✕ hoặc phím Back để đóng.

### Chuông thông báo
Chuông hiện khi tài khoản có quyền `notifications:view`. Số đỏ là số thông báo chưa đọc (quá 99 hiện "99+"); app cập nhật số này mỗi 60 giây khi đang mở. Bấm chuông để xem hộp thư: thông báo chưa đọc in đậm, có chấm xanh; bấm vào để đọc đủ nội dung và đánh dấu đã đọc. Kéo xuống để tải lại. Chưa có nút "đánh dấu tất cả đã đọc".

## 3. Trang chủ
- Chọn **7 / 30 / 90 ngày**: hiện **Doanh thu**, **Đơn hàng**, **Giá trị TB/đơn** và biểu đồ doanh thu theo ngày. Kéo xuống để tải lại.
- **Cần xử lý:** số đơn mua chờ duyệt, đơn bán chờ duyệt, đơn nghỉ phép chờ duyệt (chỉ hiện dòng bạn có quyền duyệt). Chạm vào dòng để mở danh sách đã lọc đúng trạng thái. Không có việc nào: "Không có việc nào đang chờ.".
- Thiếu quyền xem báo cáo bán hàng: "Tài khoản chưa có quyền xem mục này." kèm tên quyền cần xin.
- Số đơn mua chờ duyệt đếm trong 100 đơn đầu tiên.

## 4. Mua hàng (đơn mua)
- Chip lọc: **Tất cả**, **Đã gửi**, **Chờ tài chính**, **Đã duyệt**. Ô tìm kiếm lọc theo số đơn hoặc nhà cung cấp **trong các dòng đã tải** (cuộn xuống để tải thêm).
- Mỗi thẻ: số đơn, trạng thái, nhà cung cấp, tổng tiền, ngày. Chạm để mở chi tiết: nhà cung cấp, ngày, ngày giao, các dòng hàng, tổng, ghi chú.
- **Thanh nút dưới màn hình** chỉ hiện nút hợp lệ:
  - Đơn **Đã gửi**: **Duyệt**, **Từ chối** (cần quyền duyệt đơn mua); **Hủy đơn** (cần quyền hủy).
  - Đơn **Chờ tài chính**: **Duyệt tài chính** (quyền duyệt tài chính), **Từ chối**.
  - Đơn **Đã duyệt**: **Hủy đơn**.
- Bấm nút không chạy ngay: hiện bảng xác nhận ("Duyệt PO-… ?"). **Từ chối** bắt buộc nhập **Lý do** (để trống: "Vui lòng nhập lý do."). Xong có thông báo xanh và trạng thái đổi.
- Máy chủ từ chối (ví dụ đơn đã được người khác duyệt): câu của máy chủ hiện ngay trong bảng, bảng không đóng, không có gì thay đổi. Thiếu quyền: "Tài khoản không được phép thực hiện thao tác này.".

## 5. Bán hàng (đơn bán)
- Chip: **Tất cả**, **Đã gửi**, **Đã duyệt**, **Đã giao vận**. Thẻ có số đơn, khách, tổng, ngày; đơn bị hệ thống cảnh báo giá trị bất thường có dòng vàng "Cảnh báo: …".
- Chi tiết: khách, ngày, địa chỉ giao, mã vận đơn, các dòng, giảm giá, phí giao hàng, tổng.
- Nút: đơn **Đã gửi** có **Duyệt**, **Từ chối** (lý do bắt buộc), **Hủy đơn** (lý do bắt buộc); đơn **Đã duyệt** hoặc **Đã lấy hàng** có **Hủy đơn**. Lấy hàng, giao hàng, trả hàng vẫn làm trên trang quản trị.

## 6. Nhân sự
Ba phần: **Nhân viên | Nghỉ phép | Chấm công** (chỉ hiện phần có quyền).
- **Nhân viên:** tìm theo tên/mã; chạm để xem mã số, email, trạng thái (chỉ xem).
- **Nghỉ phép:** mặc định lọc **Chờ duyệt**; chip **Đã duyệt**, **Tất cả**. Chi tiết có loại nghỉ, từ ngày, đến ngày, số ngày, lý do. Đơn chờ duyệt: **Duyệt**, **Từ chối** (lý do bắt buộc); đơn đã duyệt: **Hủy nghỉ phép**. Tên người nghỉ hiện khi tài khoản có quyền xem nhân viên, nếu không thì hiện số đơn.
- **Chấm công:** danh sách theo ngày: tên, ngày, số giờ, trạng thái (chỉ xem).

## 7. Quyền cần có
| Việc | Quyền |
|---|---|
| Xem số liệu Trang chủ | xem báo cáo bán hàng (`reports:sales:view`) |
| Xem đơn mua / duyệt / duyệt tài chính / hủy | `purchase_order:read` / `:approve` / `:finance_approve` / `:cancel` |
| Xem đơn bán / duyệt, từ chối / hủy | `sales_order:read` / `:approve` / `:cancel` |
| Xem nhân viên / duyệt nghỉ phép / xem chấm công | `hrm:employees:view` / `hrm:leave:manage` / `hrm:attendance:view` |
Quyền cấp ở **Cài đặt → Vai trò** trên trang quản trị. Quyền theo vai trò, không theo tên vai trò.

## 8. Cài đặt cá nhân (trong menu ☰)
Tên và email, **Ngôn ngữ** (English / Tiếng Việt), tenant, địa chỉ máy chủ, phiên bản, **Đăng xuất** (có hỏi xác nhận).

## 9. Mất mạng
Danh sách đã tải vẫn hiện với dải vàng "Đang ngoại tuyến. Hiển thị dữ liệu đã lưu."; bấm duyệt/từ chối khi không có mạng sẽ báo lỗi, không có thao tác nào bị bỏ dở.

## 10. Giới hạn đã biết
Tìm kiếm đơn mua/đơn bán chỉ lọc trong các dòng đã tải; số chờ duyệt đơn mua tính trong 100 đơn đầu; chưa thử bằng trình đọc màn hình (TalkBack) và chưa thử xác thực hai bước với tài khoản thật (rủi ro R123-R127 trong DEFERRED-ISSUES).
