# Hướng dẫn sử dụng: Cửa hàng trực tuyến (Storefront)

Dành cho khách mua hàng trên web (máy tính và điện thoại). Giao diện có tiếng Anh và tiếng Việt; chọn **EN / VI** ở góc trên bên phải.

> Ghi chú cho người bảo trì: nhãn trích từ `vi.json` / `en.json`. Đã duyệt thử trên trình duyệt (tenant `glcheck`, cửa hàng `glcheck.shop.erp.local`) ở khổ 1280 và 375 điểm ảnh: trang chủ, danh sách, chi tiết, giỏ hàng, đăng ký, thanh toán đến bước xem lại (không đặt đơn), tài khoản, menu điện thoại, đăng nhập sai và đúng. Khách thử đã xóa.

## 1. Duyệt và tìm sản phẩm
- **Trang chủ:** banner và hàng sản phẩm nổi bật. Bấm **Mua ngay** để vào danh sách.
- **Sản phẩm:** ô **Tìm kiếm** (gõ rồi chờ kết quả) và ô **Sắp xếp theo** (Mới nhất, Giá thấp → cao, Giá cao → thấp, Tên A–Z). Lưới 2 / 3 / 4 cột tùy màn hình; dưới lưới có **Trước / Sau** và "Trang n / N".
- Không có kết quả: có thông báo và nút **Xóa tìm kiếm**. Lỗi tải: khung đỏ có nút **Thử lại**.
- **Chi tiết sản phẩm:** tên, giá (số đậm), "Còn hàng" hoặc "Hết hàng", mô tả, chọn số lượng bằng nút **−** / **+**, bấm **Thêm vào giỏ**. Thông báo "Đã thêm vào giỏ hàng" hiện ở góc màn hình và biểu tượng giỏ ở đầu trang tăng số. Hết hàng thì nút bị khóa. Trên điện thoại, số lượng và nút Thêm vào giỏ nằm cố định ở cuối màn hình.

## 2. Giỏ hàng
Mỗi dòng có nút giảm / tăng số lượng (đọc là "Số lượng …") và **Xóa** (đọc tên sản phẩm). Bên phải (hoặc bên dưới trên điện thoại) là **Tóm tắt đơn hàng**: tạm tính, giảm giá từ mã nếu có, ghi chú **phí giao hàng được tính ở bước thanh toán**, tổng cộng. Bấm **Tiến hành thanh toán**. Giỏ trống: có nút **Xem sản phẩm**.

## 3. Đăng ký và đăng nhập
- **Đăng ký:** Tên, Họ, Email, Mật khẩu (ít nhất 8 ký tự) → vào trang chủ trong trạng thái đã đăng nhập. Lỗi: khung đỏ "Đăng ký không thành công…".
- **Đăng nhập:** Email, Mật khẩu. Sai: khung đỏ "Email hoặc mật khẩu không đúng." Đăng nhập xong quay lại đúng trang đang làm dở (ví dụ thanh toán).
- Đăng xuất: nút **Đăng xuất** trên đầu trang (máy tính), trong menu (điện thoại) hoặc trong **Hồ sơ**.

## 4. Thanh toán (3 bước)
Phải đăng nhập. Thanh bước ở đầu trang cho biết bạn đang ở bước nào.
1. **Giao hàng:** Địa chỉ, Phường/xã, **Số điện thoại** (ít nhất 8 chữ số; nhân viên giao hàng sẽ gọi số này), Thành phố, Tỉnh/bang, Mã bưu chính, Quốc gia. Ô có dấu **\*** là bắt buộc. Bấm **Tiếp tục →** khi còn thiếu: lời nhắc hiện ngay dưới từng ô và bạn ở lại bước này.
2. **Phương thức thanh toán:** chỉ hiện những gì cửa hàng đang nhận: Thanh toán khi nhận hàng, VNPay, MoMo, ZaloPay. Cửa hàng chưa bật phương thức nào thì có cảnh báo.
3. **Xem lại:** địa chỉ, phương thức, từng dòng hàng, **Tiền hàng**, **Phí vận chuyển** (đang tính / chưa tính được - sẽ cộng khi đặt / Miễn phí / số tiền), **Tổng**. Nút đặt hàng ghi sẵn số tiền, ví dụ "Đặt hàng · 150.000 ₫". Phí hiển thị là phí tạm tính; hệ thống tính lại khi đặt đơn.
- **Thanh toán khi nhận hàng:** sang trang **Đặt hàng thành công** với mã đơn, tổng tiền và dòng "Bạn sẽ trả tiền cho nhân viên giao hàng".
- **Thanh toán online:** sang cổng thanh toán, sau đó trang **Thanh toán** cho biết kết quả (đã nhận tiền / chưa hoàn tất / đã hủy / đang chờ). Nếu chưa vào được cổng: bấm **Thanh toán ngay** hoặc **Thanh toán lại**.

## 5. Tài khoản
Menu bên trái (trên điện thoại nằm ngang phía trên): **Đơn hàng của tôi** (mã đơn, ngày, nhãn trạng thái, tổng tiền) và **Hồ sơ của tôi** (tên, email, đăng xuất).

## 6. Dùng trên điện thoại
Nút **☰ Menu** (có tên "Mở menu") mở ngăn bên trái gồm Sản phẩm, Giỏ hàng, đơn hàng, hồ sơ, đăng nhập / đăng xuất. Nhấn **Esc** hoặc nút đóng để thoát; con trỏ trở về nút menu. Nút chat (trợ lý) ở góc dưới bên phải, nằm trên thanh Thêm vào giỏ.

## 7. Trợ năng
- Phím **Tab** đầu tiên đến liên kết **Bỏ qua, tới nội dung**.
- Mọi ô nhập có nhãn; lỗi được đọc lên và gắn với ô.
- Chữ đạt độ tương phản tối thiểu 4.5:1; thông báo trạng thái dùng vùng "trực tiếp" để trình đọc màn hình đọc ngay.

## 8. Lưu ý
- Giá ở giỏ hàng và trang sản phẩm hiện bằng đô la; từ bước thanh toán (khi đã chọn thanh toán khi nhận hàng hoặc online) hiện bằng đồng. Đây là cách hiển thị cũ, sẽ thống nhất ở một bước sau.
- Chưa có: quên mật khẩu, xác nhận email, nhập mã giảm giá, theo dõi vận đơn, sổ địa chỉ, danh sách yêu thích.
