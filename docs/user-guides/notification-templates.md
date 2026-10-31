# Hướng dẫn sử dụng: màn hình Mẫu thông báo

Dành cho nhân viên phụ trách nội dung / chăm sóc khách hàng của cửa hàng. Mô tả nghiệp vụ đầy đủ: [WebhooksAndTemplates.md](../business/Notification/WebhooksAndTemplates.md).

> Ghi chú cho người bảo trì: hướng dẫn viết từ mã nguồn, nhãn tiếng Việt và tài liệu nghiệp vụ; chưa có ảnh chụp màn hình vì chưa duyệt thử trên trình duyệt.

## 1. Mẫu thông báo là gì
Mỗi tin nhắn tự động (xác nhận đơn, đơn đã giao, mã xác thực…) có một **mẫu**: tiêu đề + nội dung có các chỗ trống `{tên}` mà hệ thống điền vào (ví dụ `{customerName}`, `{orderNumber}`, `{trackingNumber}`).

Có hai loại mẫu:
- **Mặc định nền tảng** — dùng chung cho mọi cửa hàng, **không sửa trực tiếp**.
- **Của công ty** — bản riêng của bạn, cùng mã với bản mặc định; khi có bản này, hệ thống dùng bản của bạn.

## 2. Ai dùng được
| Việc | Quyền |
|---|---|
| Xem mẫu | Xem thông báo (`notifications:view`) |
| Ghi đè, sửa, bật/tắt, xóa, thêm | Quản lý mẫu thông báo (`notifications:templates:manage`) |

## 3. Mở màn hình
Menu bên trái → **Cài đặt** → **Mẫu thông báo**.

## 4. Đọc danh sách
Cột Mã · Tên · Kênh · Nguồn (**Mặc định nền tảng** hoặc **Của công ty**) · trạng thái. Nhãn phụ:
- **Đã được thay bằng mẫu của bạn** — mẫu mặc định này đã có bản riêng của công ty.
- **Đã tắt** — mẫu đang tắt.

## 5. Việc thường làm

### 5.1 Đổi nội dung một tin có sẵn (ghi đè)
1. Tìm mẫu mặc định theo mã (ví dụ `ORDER_SHIPPED`, tên hiển thị "Order shipped": tên và nội dung mặc định hiện bằng tiếng Anh).
2. Bấm **Ghi đè**. Hệ thống tạo bản riêng của công ty: "Đã tạo bản riêng của bạn. Hãy sửa và lưu."
3. Sửa tiêu đề và nội dung. Muốn chèn giá trị tự động, **bấm vào tên biến** (dưới ô nội dung) để chèn `{tên}` đúng chỗ con trỏ.
4. Nhìn khung **Xem trước** (cập nhật sau chừng nửa giây): hệ thống điền giá trị mẫu để bạn thấy tin trông thế nào.
5. Bấm **Lưu**. Từ tin kế tiếp, khách nhận nội dung mới.

### 5.2 Quay về nội dung mặc định
Ở bản của bạn bấm **Xóa / về mặc định** rồi xác nhận. Bản riêng bị xóa, hệ thống dùng lại mẫu mặc định.

### 5.3 Thêm mẫu mới
1. Bấm **Thêm mẫu**.
2. **Mã**: 2–100 ký tự gồm chữ in hoa, số và dấu gạch dưới (gõ chữ thường sẽ tự đổi sang in hoa), ví dụ `GIFT_THANKS`; tên biến viết liền kiểu `orderNumber`. Mã và kênh **không đổi được** sau khi lưu.
3. Chọn **kênh**, nhập tên, tiêu đề (nếu kênh cần), nội dung.
4. Khai báo **Biến trong mẫu** (cách nhau bằng dấu phẩy), rồi viết nội dung có `{tên}`.
5. **Lưu**. Mã đã có của công ty thì bị từ chối.

### 5.4 Tắt / bật một mẫu của công ty
Bấm **Ngừng kích hoạt** / **Kích hoạt**. Mẫu tắt không được dùng để gửi: tin được **bỏ qua** (trạng thái "Skipped"), không coi là lỗi, và nếu cửa hàng có mẫu mặc định cùng mã thì mẫu mặc định được dùng.

## 6. Lưu ý khi viết
- Chỗ trống viết `{tên}` — tên phải trùng với danh sách **Biến trong mẫu**. Tên chưa khai báo hiện cảnh báo "Các chỗ này chưa khai báo là biến và sẽ giữ nguyên như đã viết" — tin gửi đi sẽ hiện nguyên `{tên}` cho khách.
- Xem trước dùng giá trị mẫu, không phải dữ liệu đơn thật.

## 7. Thông báo lỗi thường gặp
| Thấy gì | Nghĩa | Cần làm |
|---|---|---|
| "Đây là mẫu mặc định của nền tảng. Hãy ghi đè…" | Cửa hàng không sửa trực tiếp mẫu nền tảng | Bấm **Ghi đè** |
| "Không lưu được mẫu." kèm lý do | Mã sai định dạng hoặc đã tồn tại, thiếu nội dung | Sửa theo lý do |
| "Thao tác không thành công." | Yêu cầu bị từ chối | Đọc lý do |

## 8. Giới hạn cần biết
- Chưa có nút gửi thử một mẫu bằng tin thật và chưa có mẫu theo từng ngôn ngữ (R63).
- Chỉ **tenant nền tảng** mới sửa được mẫu mặc định; nó ảnh hưởng mọi cửa hàng chưa ghi đè và chưa có lịch sử sửa (R64).
- Cửa hàng chưa tắt hẳn được một tin: tắt bản riêng chỉ làm hệ thống dùng lại mẫu mặc định (R66).
