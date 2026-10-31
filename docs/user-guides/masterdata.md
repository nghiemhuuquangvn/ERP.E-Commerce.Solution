# Hướng dẫn sử dụng: nhóm màn hình Dữ liệu chủ (Master data)

Dành cho người quản trị dữ liệu nền của công ty: cơ cấu tổ chức, nhân sự cơ bản, đơn vị, địa lý, tiền tệ. Mô tả kỹ thuật: [masterdata-admin-guide.md](../frontend/masterdata-admin-guide.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: các tên màn hình, hộp thoại và ô nhập trong bảng dưới được trích tự động từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết; nếu đổi nhãn nhớ cập nhật. Đã duyệt thử cả 17 màn hình trên trình duyệt với tài khoản quản trị nền tảng; chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
Mỗi màn hình có hai quyền: **xem** (`masterdata:<tên>:view`) và **quản lý** (`masterdata:<tên>:manage`). Chỉ người có quyền quản lý mới thấy nút **Thêm…**, **Sửa**, **Ngưng dùng**. Không thấy nút Thêm nghĩa là tài khoản của bạn chưa được cấp quyền quản lý màn hình đó — nhờ quản trị viên phân quyền.

## 2. Các màn hình
| Màn hình | Nằm ở | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|---|
| Phòng ban | Dữ liệu chủ → Phòng ban | Cơ cấu tổ chức: phòng ban, có thể có phòng ban cha và thuộc một bộ phận. | **Thêm phòng ban**: Tên*; Mã*; Quản lý; Phòng ban cha; Bộ phận |
| Chức vụ | Dữ liệu chủ → Chức vụ | Danh sách chức danh dùng khi tuyển dụng và phân công. | **Thêm chức vụ**: Chức danh*; Mã*; Mô tả; Mã phòng ban (tùy chọn) |
| Nhân viên | Dữ liệu chủ → Nhân viên | Hồ sơ nhân viên: tuyển, cập nhật hồ sơ, điều chuyển phòng ban, cho thôi việc. | **Tuyển nhân viên**: Tên*; Họ*; Email; Điện thoại; Phòng ban*; Mã chức vụ (tùy chọn); Ngày vào làm*; Loại hợp đồng<br>**Cập nhật hồ sơ — {{name}}**: Email; Điện thoại<br>**Điều chuyển — {{name}}**: Phòng ban mới*<br>**Cho thôi việc — {{name}}**: Lý do* |
| Đơn vị tính | Dữ liệu chủ → Đơn vị tính | Đơn vị đo (cái, kg, hộp…) và hệ số quy đổi sang đơn vị gốc. | **Thêm đơn vị tính**: Mã*; Tên*; Ký hiệu*; Loại đơn vị; Hệ số quy đổi*; Mã đơn vị gốc (tùy chọn) |
| Địa lý | Dữ liệu chủ → Địa lý | Quốc gia → tỉnh/thành → quận/huyện → phường/xã (bốn thẻ). | **Thêm quốc gia**: Mã*; Tên*; Mã điện thoại<br>**Thêm tỉnh/thành**: Quốc gia*; Mã*; Tên*<br>**Thêm quận/huyện**: Tỉnh/Thành phố*; Mã*; Tên*<br>**Thêm phường/xã**: Quận/Huyện*; Mã*; Tên* |
| Ngôn ngữ | Dữ liệu chủ → Ngôn ngữ | Các ngôn ngữ hệ thống hỗ trợ và ngôn ngữ mặc định. | **Thêm ngôn ngữ**: Mã*; Tên*; Tên bản ngữ*; Thứ tự; Ngôn ngữ mặc định (ô chọn) |
| Công ty | Dữ liệu chủ → Công ty | Pháp nhân của doanh nghiệp (mã số thuế, địa chỉ pháp lý, tiền tệ). | **Thêm công ty**: Tên*; Mã*; Mã số thuế; Địa chỉ pháp lý; Mã tiền tệ |
| Chi nhánh | Dữ liệu chủ → Chi nhánh | Chi nhánh của một công ty. | **Thêm chi nhánh**: Công ty*; Tên*; Mã*; Địa chỉ |
| Bộ phận | Dữ liệu chủ → Bộ phận | Bộ phận thuộc một chi nhánh. | **Thêm bộ phận**: Chi nhánh*; Tên*; Mã* |
| Nhóm | Dữ liệu chủ → Nhóm | Nhóm làm việc trong một phòng ban. | **Thêm nhóm**: Phòng ban*; Tên*; Mã* |
| Đơn vị kinh doanh | Dữ liệu chủ → Đơn vị kinh doanh | Đơn vị kinh doanh của một công ty. | **Thêm đơn vị kinh doanh**: Công ty*; Tên*; Mã* |
| Trung tâm chi phí | Dữ liệu chủ → Trung tâm chi phí | Nơi tập hợp chi phí và ngân sách. | **Thêm trung tâm chi phí**: Công ty*; Tên*; Mã*; Ngân sách*; Phòng ban (tùy chọn) |
| Trung tâm lợi nhuận | Dữ liệu chủ → Trung tâm lợi nhuận | Nơi tập hợp doanh thu và lợi nhuận. | **Thêm trung tâm lợi nhuận**: Công ty*; Tên*; Mã*; Đơn vị kinh doanh (tùy chọn) |
| Lịch tổ chức | Dữ liệu chủ → Lịch tổ chức | Lịch làm việc và ngày lễ của tổ chức (ngày lễ có thể lặp lại hàng năm). | **Thêm ngày lễ**: Ngày*; Tên*; Lặp lại hàng năm (ô chọn) |
| Phân công nhóm | Dữ liệu chủ → Phân công nhóm | Nhân viên thuộc nhóm nào trong khoảng thời gian nào; có thể kết thúc phân công. | **Thêm phân công nhóm**: Nhân viên*; Nhóm*; Mã chức vụ (tùy chọn); Hiệu lực từ*<br>**Kết thúc phân công nhóm**: Hiệu lực đến* |
| Loại tiền tệ | Dữ liệu chủ → Loại tiền tệ | Danh sách tiền tệ (thẻ "Loại tiền tệ") và tỷ giá (thẻ "Tỷ giá"). | **Thêm loại tiền tệ**: Mã*; Tên*; Ký hiệu*; Số chữ số thập phân* |
| Điều khoản thanh toán | Dữ liệu chủ → Điều khoản thanh toán | Số ngày thanh toán (net) và phần trăm đặt cọc. | **Thêm điều khoản thanh toán**: Mã*; Tên*; Số ngày net*; Phần trăm đặt cọc |

## 3. Việc thường làm

### 3.1 Thêm một bản ghi
1. Mở màn hình ở menu bên trái: **Dữ liệu chủ**, chọn mục.
2. Bấm nút **Thêm …** ở góc trên bên phải. Hộp thoại hiện ra, con trỏ nằm sẵn ở ô đầu tiên.
3. Điền các ô. Ô có dấu **\*** đỏ là bắt buộc. Với ô chọn (công ty, phòng ban…), chọn từ danh sách.
4. Bấm **Lưu**. Nếu để trống ô bắt buộc, chữ **"Bắt buộc"** hiện ngay dưới ô đó và hộp thoại ở lại.
5. Lưu thành công: hộp thoại đóng, thẻ xanh **"Đã lưu."** hiện ở góc trên bên phải trong khoảng 4 giây và dòng mới xuất hiện trong bảng.

### 3.2 Sửa
Bấm **Sửa** ở cuối dòng, đổi nội dung, **Lưu**. *Các màn hình chỉ có nút Thêm (ví dụ Địa lý, Ngôn ngữ) không có nút Sửa; ô nào bị khóa xám thì không đổi được.*

### 3.3 Ngưng dùng / kích hoạt lại, kết thúc phân công, cho thôi việc
- **Ngưng dùng** / **Kích hoạt**: hộp thoại xác nhận hỏi lại; bấm **Xác nhận** hoặc **Hủy** (hoặc Esc). Bản ghi ngưng dùng vẫn còn trong danh sách, không bị xóa.
- **Phân công nhóm → Kết thúc**: chọn ngày hiệu lực đến, bản ghi chuyển sang "Đã kết thúc".
- **Nhân viên → Cho thôi việc**: nhập **lý do** (bắt buộc), bấm nút, rồi xác nhận lần nữa ở hộp thoại thứ hai. Việc này không hoàn tác từ màn hình.

### 3.4 Lọc và danh sách trống
Một số màn hình có ô lọc phía trên bảng (công ty, phòng ban, thẻ…). Danh sách chưa có dữ liệu hiện khung "Không tìm thấy dữ liệu."; Phân công nhóm yêu cầu chọn nhân viên trước.

## 4. Khi máy chủ từ chối
Nếu dữ liệu không hợp lệ theo quy tắc của hệ thống, một **khung đỏ** hiện trong hộp thoại, ngay trên các nút, ghi lý do của máy chủ (ví dụ mã đã tồn tại). Sửa rồi bấm **Lưu** lại, hoặc **Hủy**.
- **Lưu ý đã biết (R76):** với một số màn hình, nhập **mã trùng** khiến máy chủ trả lỗi chung "Yêu cầu không thể hoàn tất. Lỗi đã được ghi lại." thay vì nói rõ "mã đã tồn tại". Kiểm tra mã trước khi lưu.

## 5. Giới hạn cần biết
- Danh sách có lọc mà không có kết quả chưa hiện nút **Xóa lọc**: hãy tự đặt ô lọc về "Tất cả" (R75).
- Các thẻ con (Địa lý, Tiền tệ, Lịch tổ chức) mở hộp thoại riêng theo thẻ đang chọn.
