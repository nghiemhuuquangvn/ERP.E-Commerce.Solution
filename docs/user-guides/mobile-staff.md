# Hướng dẫn sử dụng: Ứng dụng nhân viên trên điện thoại

Dành cho thu ngân, nhân viên kho và nhân viên giao hàng. Nút bấm lớn (52 điểm) để dùng một tay. Giao diện có tiếng Anh và tiếng Việt (theo ngôn ngữ đã lưu của máy).

> Ghi chú cho người bảo trì: nhãn trích từ `i18n/locales/vi.json`. Đã duyệt thử bản web ở khổ 375 điểm ảnh bằng tài khoản quản trị của tenant `glcheck`, **chỉ đọc**: không hoàn tất giao dịch, không sửa tồn kho, không đổi trạng thái vận đơn. **Chưa thử trên điện thoại thật.**

## 1. Đăng nhập
Nhập **Email** và **Mật khẩu** (bắt buộc). Để trống: mỗi ô hiện "Vui lòng nhập mục này.". Sai: khung đỏ "Email hoặc mật khẩu không đúng.".

## 2. Bốn tab
**Tổng quan**, **Bán hàng** (POS), **Tồn kho**, **Giao hàng**.
- **Tổng quan:** lời chào và bốn thẻ số liệu (hiện đang là 0, chưa nối dữ liệu).

## 3. Bán hàng (POS)
1. **Chọn máy POS** (lần đầu): thẻ ở đầu màn hình ghi "Chưa chọn máy POS"; bấm **Chọn** và chạm vào máy POS bạn đang đứng bán (chỉ hiện các máy đang hoạt động). Ứng dụng nhớ lựa chọn này cho tới khi bạn đăng xuất; bấm **Đổi** để chọn máy khác. Chưa có máy POS nào thì nhờ quản trị viên tạo trong trang quản trị.
2. **Ca thu ngân:** dưới tên máy có ghi "Đang có ca thu ngân mở: giao dịch được gắn vào ca đó." hoặc "Chưa mở ca thu ngân: giao dịch không gắn vào ca nào." Không có ca vẫn bán được; mở và đóng ca làm trên trang quản trị.
3. **Thêm hàng:** gõ tên hoặc SKU vào ô **Tìm sản phẩm** (ít nhất 2 ký tự) rồi chạm vào kết quả để thêm 1 sản phẩm; hàng hết sẽ mờ và không chọn được. Hoặc bấm **Quét mã vạch**: cho phép dùng máy ảnh, hướng vào mã vạch (mã vạch là SKU của sản phẩm). Quét được thì thêm 1 sản phẩm và hiện thông báo; không thấy sản phẩm thì báo "Không có sản phẩm nào với mã vạch …" và máy ảnh vẫn mở để quét món tiếp. Cùng một mã quét lại trong 2 giây chỉ tính một lần. Nếu đã tắt quyền máy ảnh, khung quét sẽ hướng dẫn mở cài đặt.
4. **Sửa số lượng:** mỗi dòng có nút **Giảm** / **Tăng** (về 0 thì bỏ dòng).
5. Phần dưới màn hình hiện **Tạm tính**, **Thuế (10 %)** và **Tổng**; chọn phương thức **Tiền mặt**, **Thẻ tín dụng** hoặc **Ví điện tử**.
6. Bấm **Hoàn tất giao dịch** (khóa khi chưa có dòng nào hoặc chưa chọn máy POS). Thành công: thông báo xanh "Hoàn tất giao dịch. Tổng: …" và đơn bán được xóa để làm đơn mới (máy POS vẫn giữ). Thất bại: thông báo đỏ, **các dòng vẫn còn** để thử lại; "Bạn không có quyền bán hàng." nghĩa là tài khoản thiếu quyền `pos:transactions`.
- **Máy chủ tính giá:** giá mỗi dòng do máy chủ quyết định (không dùng giá app gửi). Nếu bấm **Hoàn tất giao dịch** mà có giá khác với giá đang hiện, giao dịch **chưa hoàn tất**: giỏ được cập nhật theo giá của máy chủ và hiện khung vàng **"Giá đã thay đổi"** (từng dòng "Sản phẩm: giá cũ → giá mới"); kiểm tra tổng rồi bấm **Hoàn tất giao dịch** lần nữa để xác nhận. Thất bại giữa chừng thì bấm lại an toàn: dòng hàng không bị thêm hai lần. Lý do máy chủ từ chối (ví dụ "… chưa có giá nên không bán được") hiện nguyên văn.
- Thuế 10 % đang cố định trong ứng dụng (máy chủ chưa gửi thuế suất theo sản phẩm).
- Chưa thử quét bằng điện thoại thật: cần người phụ trách thử theo mục UAT trong tài liệu BA.

## 4. Tồn kho
- Ô **Tìm sản phẩm**; mỗi dòng có tên, SKU và số **có thể bán** (đỏ khi 0, vàng dưới 5, xanh còn lại).
- Nút **Điều chỉnh tồn kho của …** mở khung **Yêu cầu điều chỉnh tồn kho**: hiện số hiện có, nhập **Số lượng mới** (số nguyên), chọn **Lý do** (Hư hỏng, Thất lạc, Kiểm đếm lại - chọn sẵn, Khác), có thể ghi **Ghi chú**, rồi **Gửi yêu cầu** hoặc **Hủy**. Nhập chữ: "Hãy nhập một số nguyên."; nhập đúng số hiện có: "Không có gì để đổi" và chưa gửi gì.
- **Đây là yêu cầu, không phải thay đổi ngay:** gửi xong có thông báo xanh "Đã gửi yêu cầu điều chỉnh. Tồn kho chỉ đổi khi quản lý phê duyệt." Quản lý duyệt hoặc từ chối trong trang quản trị (Tồn kho → Điều chỉnh). Tài khoản không có quyền tạo yêu cầu sẽ thấy "Bạn không có quyền yêu cầu điều chỉnh tồn kho."

## 5. Giao hàng
Mỗi thẻ: mã vận đơn, nhãn trạng thái (Chờ lấy hàng, Đã lấy hàng, Đang giao, Đã giao, Thất bại), mã đơn. Nút **Đánh dấu … đang giao** cho vận đơn chưa giao xong.
- Chưa có vận đơn nào thì màn hình hiện "No active shipments". Lỗi tải sẽ có khung đỏ kèm nút **Thử lại**.

## 6. Dùng với cỡ chữ lớn và trợ năng
Mọi nút, tab và lựa chọn có tên để trình đọc màn hình đọc; thông báo xanh/đỏ được đọc ngay; cỡ chữ tăng đến 130 % vẫn không che mất nút.
