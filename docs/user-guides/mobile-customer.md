# Hướng dẫn sử dụng: Ứng dụng mua hàng trên điện thoại (khách hàng)

Dành cho khách mua hàng dùng ứng dụng di động của cửa hàng. Giao diện có tiếng Anh và tiếng Việt, đổi trong **Hồ sơ**.

> Ghi chú cho người bảo trì: nhãn trích từ `i18n/locales/vi.json`. Đã duyệt thử bản web của ứng dụng ở khổ 375 điểm ảnh (tenant `glcheck`): đăng nhập sai, đăng ký, các tab, tìm kiếm không có kết quả, giỏ hàng, chuyển ngôn ngữ. **Chưa thử trên điện thoại thật** (xem mục 7).

## 1. Đăng nhập và đăng ký
- **Đăng nhập:** nhập **Email** và **Mật khẩu** (hai ô đều bắt buộc, có dấu *). Để trống rồi bấm **Đăng nhập** thì mỗi ô hiện dòng "Vui lòng nhập mục này." và chưa gửi gì. Sai thông tin: khung đỏ "Email hoặc mật khẩu không đúng."
- **Đăng ký:** bấm "Chưa có tài khoản? Đăng ký →". Nhập Tên, Họ (không bắt buộc), Email, Mật khẩu (ít nhất 8 ký tự). Xong là vào trang chủ.
- Phím **Tiếp** / **Đi** trên bàn phím chuyển ô hoặc gửi biểu mẫu.

## 2. Thanh tab ở cuối màn hình
Năm tab: **Trang chủ**, **Tìm kiếm**, **Giỏ hàng** (hiện số mặt hàng), **Đơn hàng**, **Hồ sơ**. Mỗi tab đủ lớn để chạm (ít nhất 44 điểm).

## 3. Trang chủ và tìm kiếm
- **Trang chủ:** banner chào mừng và hàng sản phẩm nổi bật. Lỗi tải: khung đỏ kèm nút **Thử lại**.
- **Tìm kiếm:** ô **Tìm sản phẩm**; kết quả hiện dạng lưới hai cột, mỗi ô đọc là "tên, giá" (kèm "Hết hàng" nếu hết). Không có kết quả: có thông báo và nút **Xóa tìm kiếm**.
- **Trang sản phẩm:** bấm một ô sản phẩm để mở. Có ảnh (hoặc biểu tượng thay thế), danh mục, tên, giá, "Còn hàng" / "Hết hàng" và mô tả. Nút **Quay lại** ở đầu màn hình.
- **Thêm vào giỏ:** thanh cố định ở cuối màn hình có bộ chọn **Số lượng** (từ 1 đến số hàng còn) và nút **Thêm vào giỏ**. Bấm xong hiện thông báo xanh "Đã thêm vào giỏ hàng" và số lượng quay về 1. Sản phẩm hết hàng thì nút bị khóa. Muốn xem giỏ: bấm **Quay lại**, rồi mở tab **Giỏ hàng** (có số mặt hàng).

## 4. Giỏ hàng và đặt hàng
- Mỗi dòng có nút **Giảm** / **Tăng** (đọc kèm tên sản phẩm) và nút **Xóa**.
- **Tóm tắt đơn hàng:** tạm tính, tổng cộng. **Thanh toán bằng:** chỉ hiện những gì cửa hàng đang nhận (thanh toán khi nhận hàng, VNPay, MoMo, ZaloPay).
- Nút đặt hàng ghi sẵn số tiền, ví dụ "Đặt hàng · $30.00". Chưa đăng nhập thì ứng dụng hỏi đăng nhập trước.
- Đặt không được: thông báo đỏ ở cuối màn hình và **giỏ hàng được giữ nguyên**.
- Thanh toán trực tuyến: ứng dụng mở trang của cổng thanh toán; quay lại ứng dụng, màn hình đơn hàng tự cập nhật kết quả. Nếu chưa vào được cổng, bấm **Thanh toán ngay**.

## 5. Đơn hàng
Danh sách thẻ đơn hàng: mã đơn, nhãn trạng thái (Đã xác nhận, Đang xử lý, Đang giao, Đã giao, Đã hủy), ngày, tổng tiền. Bấm thẻ để xem chi tiết các dòng hàng và trạng thái thanh toán. Chưa có đơn: có nút **Xem sản phẩm**.

## 6. Hồ sơ
Tên và email, **Đơn hàng của tôi**, chọn ngôn ngữ (**English** / **Tiếng Việt**, đổi ngay và được nhớ), **Đăng xuất** (có hỏi xác nhận).

## 7. Lưu ý
- Giá hiện bằng đô la; bản web của cửa hàng hiện bằng đồng ở bước thanh toán. Sẽ thống nhất ở một bước sau.
- Ứng dụng chưa có quên mật khẩu, mã giảm giá, theo dõi vận đơn, thông báo đẩy.
- Cỡ chữ lớn của điện thoại (đến 130 %) vẫn dùng được: chữ không bị cắt, nút vẫn chạm được.
- Chưa chạy thử trên máy Android / iPhone thật: cần người phụ trách chạy `npx expo start` và thử theo mục UAT trong tài liệu BA.
