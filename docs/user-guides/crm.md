# Hướng dẫn sử dụng: Khách hàng (CRM)

Dành cho nhân viên bán hàng và chăm sóc khách hàng của cửa hàng. Màn hình **Khách hàng (CRM)** nằm ở menu **Khách hàng** và có năm thẻ: **Liên hệ**, **Khách hàng tiềm năng**, **Cơ hội bán hàng**, **Yêu cầu hỗ trợ**, **Hoạt động**. Phân tích nghiệp vụ đầy đủ (quy trình, trạng thái, quy tắc, kiểm thử chấp nhận): [docs/business/CRM/AdminBackOffice.md](../business/CRM/AdminBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: tên thẻ, hộp thoại và ô nhập trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết. Đã duyệt thử trên trình duyệt (tài khoản quản trị cửa hàng thử): cả năm thẻ, tạo liên hệ, khách hàng tiềm năng → đã liên hệ → chuyển thành cơ hội, cơ hội mới, chuyển giai đoạn, thất bại, yêu cầu hỗ trợ mở → giải quyết → đóng, hủy hoạt động. **Chưa** thử: thẻ (tag), phân công yêu cầu, thắng cơ hội, hoàn thành hoạt động. Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
Mỗi thẻ chỉ hiện với người có quyền **xem** (`crm:<tên thẻ>:view`); các nút Thêm, Sửa, Chuyển… chỉ hiện với người có quyền **quản lý** (`crm:<tên thẻ>:manage`). Không có quyền nào thì màn hình báo "không có quyền". Thẻ đang mở nằm trên địa chỉ trang (`?tab=leads`), nên tải lại trang hoặc gửi đường dẫn cho đồng nghiệp vẫn mở đúng thẻ. Dùng phím mũi tên trái / phải để đổi thẻ.

## 2. Quy trình tổng quát
**Liên hệ** (người / công ty) → **Khách hàng tiềm năng** → *Chuyển thành cơ hội* → **Cơ hội bán hàng** → *Thắng* hoặc *Thất bại*. Song song: **Yêu cầu hỗ trợ** (khiếu nại, hỏi đáp) và **Hoạt động** (cuộc gọi, họp, ghi chú… gắn với một liên hệ).

## 3. Các thẻ
| Thẻ | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|
| Liên hệ | Danh bạ khách hàng; tìm theo tên | **Thêm / Sửa liên hệ**: Tên*; Email; Điện thoại; Công ty. **Thẻ — <tên>**: danh sách thẻ, ô **Thẻ mới** + **Thêm**, dấu × để xóa thẻ |
| Khách hàng tiềm năng | Người quan tâm chưa chắc mua; lọc theo trạng thái (Mới, Đã liên hệ, Đã đánh giá, Thất bại) | **Thêm khách hàng tiềm năng**: Tên khách hàng tiềm năng*; Nguồn (Website, Giới thiệu, Mạng xã hội, Email, Điện thoại, Khác); Giá trị ước tính; Liên hệ (tùy chọn). **Đánh dấu thất bại**: Lý do* |
| Cơ hội bán hàng | Các thương vụ đang theo đuổi; 6 ô đếm theo giai đoạn (Tìm kiếm, Đánh giá, Đề xuất, Đàm phán, Thắng, Thất bại) | **Thêm cơ hội**: Tên*; Liên hệ*; Giá trị*; Ngày dự kiến chốt*; Người phụ trách (tùy chọn). **Đánh dấu thất bại**: Lý do* |
| Yêu cầu hỗ trợ | Khiếu nại / hỗ trợ; lọc theo trạng thái (Đang mở, Đang xử lý, Đã giải quyết, Đã đóng) | **Mở yêu cầu hỗ trợ**: Tiêu đề*; Liên hệ*; Mô tả*; Mức độ ưu tiên (Thấp, Trung bình, Cao, Khẩn cấp). **Phân công / Giao lại**: Người phụ trách*. **Giải quyết**: Kết quả giải quyết* |
| Hoạt động | Nhật ký công việc với khách; lọc theo loại (Gọi điện, Email, Họp, Ghi chú, Công việc) | **Ghi nhận hoạt động**: Liên hệ*; Loại; Tiêu đề*; Ghi chú; Thời gian dự kiến; Mã cơ hội; Mã yêu cầu hỗ trợ. **Hoàn thành**: Ghi chú |

## 4. Việc thường làm
Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô và hộp thoại ở lại; **Esc** hoặc **Hủy** đóng mà không lưu; lưu xong hiện thẻ xanh **"Đã lưu."** vài giây. Khi hệ thống từ chối, một **khung đỏ** trong hộp thoại ghi lý do; sửa rồi lưu lại.

### 4.1 Theo một khách hàng tiềm năng
1. Thẻ **Khách hàng tiềm năng** → **+ Thêm khách hàng tiềm năng**; **nên chọn Liên hệ** (xem mục 6). **Lưu** → trạng thái **Mới**.
2. Sau khi đã gọi / nhắn: **Đánh dấu đã liên hệ** (chỉ với khách ở trạng thái Mới).
3. Khi khách thật sự có nhu cầu: **Chuyển thành cơ hội**. Hộp thoại xác nhận nói rõ: *hệ thống tạo một cơ hội với cùng liên hệ và giá trị dự kiến, dự kiến chốt sau 3 tháng, và khách hàng tiềm năng này không sửa được nữa*. Bấm **Chuyển thành cơ hội** để đồng ý, **Hủy** để thôi.
4. Nếu không thành: **Đánh dấu thất bại** và ghi lý do (bắt buộc).
Khách đã "Đã đánh giá" hoặc "Thất bại" không còn nút nào.

### 4.2 Theo một cơ hội
- Cơ hội mới có **Ngày dự kiến chốt** tự điền **30 ngày sau hôm nay**; phải là ngày sau hôm nay, nếu không ô báo "Chọn ngày sau hôm nay.".
- **Chuyển giai đoạn**: sang đúng một bậc (Tìm kiếm 10 % → Đánh giá 20 % → Đề xuất 40 % → Đàm phán 60 %); không nhảy thẳng tới Thắng.
- **Thắng**: chốt thương vụ (100 %). **Đánh dấu thất bại**: cần lý do. Cơ hội Thắng / Thất bại không còn nút.
- Bấm một ô đếm giai đoạn để chỉ xem cơ hội ở giai đoạn đó; bấm lần nữa để bỏ lọc.

### 4.3 Theo một yêu cầu hỗ trợ
**Mở yêu cầu hỗ trợ** (mã tự sinh dạng `CASE-20261101-…`) → **Phân công** người xử lý (làm lại được: nút đó thành "Giao lại") → **Giải quyết** (ghi kết quả) → **Đóng**. **Đóng** hỏi xác nhận vì yêu cầu đã đóng không thể thay đổi. Mức độ ưu tiên hiện bằng nhãn màu (Khẩn cấp đỏ, Cao vàng, Trung bình xanh, Thấp xám).

### 4.4 Hoạt động
Ghi nhận cuộc gọi, họp… với một liên hệ. Nếu **Thời gian dự kiến** ở tương lai, hoạt động ở trạng thái "Đã lên lịch" và có hai nút: **Hoàn thành** (kèm ghi chú) và **Hủy** (hỏi xác nhận). Mã cơ hội / yêu cầu hỗ trợ là ô dán tay: sao chép mã từ màn hình tương ứng.

### 4.5 Tìm và lọc
Ô tìm ở thẻ Liên hệ, ô lọc ở các thẻ còn lại. Không có kết quả thì hiện **"Không có kết quả"** kèm nút **Xóa lọc**.

## 5. Khi hệ thống từ chối
Khung đỏ trong hộp thoại ghi lý do của hệ thống (ví dụ ngày chốt không hợp lệ). Với nút ngoài hộp thoại (Chuyển giai đoạn, Thắng, Đóng, Hủy) lý do hiện bằng thẻ đỏ ở góc trên bên phải và dữ liệu không đổi.

## 6. Giới hạn đã biết
- **Ghi nhận hoạt động đang báo lỗi chung** "Yêu cầu không thể hoàn tất. Lỗi đã được ghi lại." trên hệ thống hiện tại (lỗi phía máy chủ, R82); hoàn thành / hủy hoạt động đã có sẵn vẫn dùng được.
- **Chuyển thành cơ hội cần khách hàng tiềm năng đã có Liên hệ**; nếu thiếu, máy chủ báo lỗi chung (R83). Khi thêm khách, hãy chọn Liên hệ.
- **Danh sách liên hệ được máy chủ nhớ tạm tối đa 5 phút**: liên hệ vừa tạo có thể chưa hiện trong ô chọn liên hệ của hộp thoại khác ngay lập tức (R84).
- Ô chọn liên hệ chỉ liệt kê 100 liên hệ đầu; chưa có kiểm tra trùng liên hệ; chưa có SLA cho yêu cầu hỗ trợ; ghi chú hoạt động chưa sửa được sau khi lưu.
