# Hướng dẫn sử dụng: Cài đặt và Đăng nhập

Nhóm **Cài đặt** ở thanh menu bên trái gồm các màn hình: Cài đặt chung, Công ty, Ngôn ngữ, Tiền tệ, Thuế, Cửa hàng / sản phẩm, Email, SMS, Thông báo, Tích hợp, API, Lưu trữ tệp, Thanh toán, Vận chuyển, Giỏ hàng / thanh toán, Đơn hàng, Tồn kho, Quy trình duyệt, Lập lịch, Nhật ký kiểm toán, Nâng cao, Xác thực, Cờ tính năng, Giấy phép (cùng Lịch sử / Mẫu / Webhook thông báo và Vận chuyển đã có hướng dẫn riêng: [notification-history.md](notification-history.md), [notification-templates.md](notification-templates.md), [notification-webhooks.md](notification-webhooks.md), [shipping-settings.md](shipping-settings.md)).

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `en.json` / `vi.json`. Đã duyệt thử trên trình duyệt (tenant `glcheck` và quản trị nền tảng): mở cả 27 màn hình Cài đặt, đổi thẻ bằng phím mũi tên, đo độ tương phản chữ, lưu thử một giá trị (Cài đặt chung › Tên cửa hàng) rồi khôi phục. Các thay đổi khác chỉ kiểm thử tự động.

## 1. Cách dùng chung của mọi màn hình Cài đặt
- Phần lớn màn hình có hai thẻ: **Hệ thống** (chỉ người quản trị nền tảng: công tắc áp dụng cho mọi cửa hàng) và **Cửa hàng** (giá trị của riêng cửa hàng; chỉ có hiệu lực khi nền tảng cho phép). Đổi thẻ bằng chuột hoặc phím mũi tên trái / phải.
- Sửa các ô rồi bấm **Lưu**: nút hiện tiến trình, xong có thông báo **"Đã lưu."**. Nếu máy chủ từ chối, lý do hiện nguyên văn trong khung đỏ phía trên nút Lưu và những gì đã nhập vẫn còn.
- Không có quyền quản lý: mọi ô bị khóa và không có nút Lưu. Không có quyền xem: màn hình báo "Bạn không có quyền".
- **Ô bí mật** (mật khẩu SMTP, khóa SMS, khóa tích hợp, khóa cổng thanh toán, bí mật hãng vận chuyển): không bao giờ hiện lại. Ô ghi "Chưa đặt" hoặc "đã đặt - để trống nếu giữ nguyên". **Để trống rồi Lưu thì giá trị cũ được giữ**; chỉ gõ khi muốn thay.

## 2. Các màn hình
| Màn hình | Điều khiển |
|---|---|
| Cài đặt chung | Chế độ bảo trì, mã ngôn ngữ mặc định, tên cửa hàng, múi giờ, định dạng ngày |
| Công ty | Tên công ty, tên pháp lý, mã số thuế, địa chỉ, điện thoại, email, logo, website |
| Ngôn ngữ | Nhiều ngôn ngữ, ngôn ngữ mặc định, ngôn ngữ hỗ trợ, định dạng số / tiền, bố cục phải sang trái |
| Tiền tệ | Nhiều loại tiền, tiền tệ mặc định, nguồn tỷ giá, số chữ số thập phân |
| Thuế | Thuế theo vùng, mã số thuế, thuế suất mặc định, giá đã gồm thuế, nhà cung cấp hóa đơn điện tử và ký hiệu |
| Cửa hàng / sản phẩm | Giá đã gồm thuế, đánh giá sản phẩm, tên / mô tả / logo cửa hàng, hiện hàng hết |
| Email | SMTP riêng: máy chủ, cổng, tài khoản, mật khẩu (bí mật), địa chỉ và tên người gửi |
| SMS | Cổng SMS riêng: nhà cung cấp, khóa API (bí mật), số gửi |
| Thông báo | Kênh nào có trên nền tảng; kênh nào bật mặc định cho cửa hàng |
| Tích hợp | Tích hợp ngoài: địa chỉ gốc, khóa API (bí mật), đồng bộ |
| API | Khóa API: giới hạn gọi mỗi phút, số khóa hoạt động tối đa |
| Lưu trữ tệp | Nhà cung cấp lưu trữ, dung lượng tải lên tối đa, địa chỉ CDN |
| Thanh toán | Cổng có trên nền tảng; công tắc, khóa, chế độ thử, thu tiền khi nhận hàng của cửa hàng; thẻ **tài khoản thanh toán trực tuyến** VNPay / MoMo / ZaloPay |
| Vận chuyển | Địa chỉ lấy hàng, phí giao cố định và ngưỡng miễn phí, tài khoản hãng, quy tắc chọn hãng, bảng phí giao hàng |
| Giỏ hàng / thanh toán | Mua không cần đăng nhập, thời gian giữ giỏ, bắt đăng ký, số bước |
| Đơn hàng | Định dạng và tiền tố số đơn, tự hủy đơn chưa thanh toán, sửa đơn sau khi gửi |
| Tồn kho | Cho phép âm kho, ngưỡng tồn thấp, đặt trước, số ngày giữ hàng |
| Quy trình duyệt | Duyệt nhiều cấp, số cấp mặc định, ngưỡng tự duyệt |
| Lập lịch | Công việc nền, chu kỳ đọc hàng đợi, số lần thử lại |
| Nhật ký kiểm toán | Bật ghi nhật ký, thời gian giữ, ghi đăng nhập thành công / thất bại, thay đổi quyền |
| Nâng cao | Chế độ gỡ lỗi, CSS tùy chỉnh, xuất dữ liệu |
| Xác thực | Đăng nhập Google / Microsoft / Facebook / Twitter, xác thực hai bước, độ dài mật khẩu tối thiểu, khóa tài khoản, thời gian phiên |
| Cờ tính năng | Cờ của nền tảng (thẻ Hệ thống) và ghi đè của cửa hàng (thẻ Cửa hàng) |
| Giấy phép | Mô-đun, gói, giấy phép của từng cửa hàng (cấp, gia hạn, tạm ngừng, thu hồi) |

## 3. Cờ tính năng
Bật / tắt một cờ **của nền tảng** (thẻ Hệ thống) hỏi xác nhận: nói rõ tên cờ và rằng mọi cửa hàng không ghi đè sẽ có / mất tính năng ngay. **Esc** hoặc **Hủy** giữ nguyên. Ghi đè của riêng cửa hàng chỉ ảnh hưởng cửa hàng đó và đổi ngay.

## 4. Đăng nhập
- Nhập **Email** và **Mật khẩu** (hai ô bắt buộc; để trống thì trình duyệt báo ô nào thiếu), bấm **Đăng nhập**. Sai thì hiện khung đỏ "Email hoặc mật khẩu không đúng."
- Nếu cửa hàng bật xác thực hai bước, trang tiếp theo xin **mã xác thực 6 số** gửi qua SMS hoặc email. Sai mã: khung đỏ báo; không nhận được mã: **Gửi lại** (chờ 60 giây giữa hai lần, nút đếm ngược).
- Các nút **Google / Microsoft / Facebook / Twitter** chỉ hiện khi cửa hàng bật tương ứng. Với Twitter, trang trung gian hiện "đang hoàn tất đăng nhập"; nếu lỗi thì có khung đỏ và nút **Đăng nhập** để quay lại.
- Nếu máy chủ chưa được cấu hình đủ cho một nhà cung cấp (ví dụ thiếu khóa Twitter), người dùng chỉ thấy "Đã xảy ra lỗi"; người quản trị xem chi tiết trong **Nhật ký hệ thống**.

## 5. Mẹo
- Mỗi ô có nhãn riêng; trình đọc màn hình đọc đúng tên ô. Chữ phụ đã được làm đậm hơn để đọc rõ trên mọi nền.
- Màu sắc, nút và thông báo đồng nhất với các màn hình khác của hệ thống.
