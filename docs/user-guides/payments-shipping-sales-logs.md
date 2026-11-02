# Hướng dẫn sử dụng: Thanh toán (hoàn tiền), Vận đơn, Chính sách hoa hồng và Nhật ký hệ thống

Bốn màn hình là bốn mục của thanh menu bên trái: **Thanh toán**, **Vận đơn**, **Chính sách hoa hồng**, **Nhật ký hệ thống** (mỗi mục hiện khi tài khoản có quyền tương ứng). Phân tích nghiệp vụ: [Payment](../business/Payment/AdminBackOffice.md), [Shipping](../business/Shipping/ShipmentsBackOffice.md), [Sales](../business/Sales/CommissionPlansBackOffice.md), [Logs](../business/Reporting/LogsBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `vi.json`. Đã duyệt thử trên trình duyệt: chính sách hoa hồng (tạo, từ chối tỷ lệ > 100, ngừng kích hoạt có xác nhận; dữ liệu thử đã xóa), vận đơn (mở chi tiết, bảng chia kiện, xác nhận Hủy tại hãng rồi Esc), nhật ký (mở chi tiết, Esc). **Không** chạy: hoàn tiền (tenant thử không có khoản thanh toán hoàn được; chỉ có kiểm thử tự động), đặt lại / hủy tại hãng / chia kiện thật (đổi dữ liệu ở hãng vận chuyển). Chưa có ảnh chụp trong tài liệu.

Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô; **Esc** hoặc **Hủy** đóng mà không lưu; hộp xác nhận chồng lên hộp thoại: **Esc chỉ đóng hộp trên cùng**.

## 1. Thanh toán — hoàn tiền
**Ai dùng được:** quyền hoàn tiền (`payments:refund`) mới thấy nút **Hoàn tiền**.
- Bộ lọc: nhóm nút trạng thái (Tất cả, Đang chờ, …; nút đang chọn được đánh dấu), ô **Provider**, ô **Cần xử lý (đã gắn cờ…)**. Lọc không ra kết quả thì hiện "Không có kết quả" và **Xóa lọc**. Khoản bị gắn cờ có nhãn vàng và lý do ngay cạnh.
- **Hoàn tiền** (chỉ với khoản Đã thu / Thu một phần / Đã hoàn một phần): nhập **Số tiền hoàn** (từ 1 đến số tiền của khoản thanh toán; ô gợi ý "Hoàn tối đa") và **Lý do** (bắt buộc) → **Hoàn tiền**.
- **Hộp xác nhận mới:** "Hoàn … cho khách qua VNPay? Tiền được gửi trả lại và không hoàn tác được." → **Gửi hoàn tiền**. Với **COD**: "Ghi nhận đã hoàn …? Không gửi gì tới cổng thanh toán." Esc ở hộp xác nhận quay lại form, Esc lần nữa đóng form; chưa gửi gì.
- Cổng thanh toán xác nhận xong khoản mới đổi trạng thái. Nếu hệ thống từ chối, lý do hiện ngay trong hộp thoại hoàn tiền.

## 2. Vận đơn
**Ai dùng được:** quyền cập nhật vận chuyển (`shipping:update`) mới thấy **Đặt lại với hãng**, **Chia thành nhiều kiện**, **Hủy tại hãng**.
- Danh sách: ô tìm (mã vận đơn, mã của hãng hoặc người nhận), lọc trạng thái, ô **Cần xử lý**; nút **Xem** theo từng vận đơn.
- **Xem**: hộp thoại ghi hãng, mã vận đơn, trạng thái, lưu ý cần xử lý (khung vàng), lỗi đặt hãng (khung đỏ), thông tin người nhận / thu hộ / phí, lý do chọn hãng và dòng thời gian theo dõi.
- **Đặt lại với hãng** (chỉ khi chưa có mã vận đơn): hỏi xác nhận, có thể phát sinh phí; hãng từ chối thì hiện thẻ đỏ kèm lý do.
- **Hủy tại hãng** (chỉ khi Chờ / Sẵn sàng lấy hàng): hỏi xác nhận, nêu tên hãng; **chỉ hủy kiện tại hãng, không hủy đơn hàng**.
- **Chia thành nhiều kiện** (chỉ khi chưa lấy hàng và có hơn một đơn vị): hộp thoại thứ hai chồng lên. Chọn **Số kiện** (2–10); bảng cho mỗi mặt hàng một ô cho từng kiện (kiện 1 ban đầu giữ tất cả). Dòng dưới bảng nói bằng chữ: "Còn n đơn vị chưa gán", "bị gán quá số lượng", "Mỗi kiện cần ít nhất một đơn vị", "Tiền thu hộ phải cộng đủ …" hoặc "Mọi mặt hàng đã được gán". Nút **Chia và đặt hãng** chỉ bật khi hết lỗi, rồi hỏi xác nhận: mỗi kiện được đặt riêng, đặt chỗ ban đầu bị hủy, **không hoàn tác được**. Hàng chỉ coi là đã giao khi giao kiện cuối cùng.

## 3. Chính sách hoa hồng
**Ai dùng được:** quyền duyệt đơn bán (`sales_order:approve`) thấy nút thêm và **Ngừng kích hoạt**.
- **+ Thêm chính sách**: **Tên** (bắt buộc) và **Tỷ lệ (%)** (lớn hơn 0 và tối đa 100; "5 nghĩa là 5 % giá trị đơn") → **Lưu** ("Đã lưu.").
- **Ngừng kích hoạt**: hỏi xác nhận; chính sách không còn được dùng cho đơn mới, hoa hồng đã tính không đổi. Chưa có nút kích hoạt lại và chưa sửa được chính sách.

## 4. Nhật ký hệ thống (chỉ quản trị nền tảng)
Tài khoản không có quyền xem nhật ký bị chuyển về Bảng điều khiển.
- Bộ lọc có nhãn: **Dịch vụ**, **Mức độ**, **Loại**, **Từ**, **Đến**, **Từ khóa** (Enter để tìm). Bộ lọc nằm trên địa chỉ trang nên gửi được đường dẫn cho người khác. Không có kết quả → "Không có kết quả" và **Xóa lọc** (xóa mọi bộ lọc).
- Bấm **Xem** ở một dòng: hộp thoại **Chi tiết nhật ký** (thay cho ngăn kéo bên phải) ghi mức độ, thời gian, dịch vụ, loại exception, mã tương quan, nội dung, stack trace và thuộc tính. **Esc** hoặc **Đóng** để thoát. Bấm vào dòng không còn mở chi tiết, hãy dùng nút **Xem**.

## 5. Giới hạn đã biết
- Hoàn tiền: giới hạn trên là số tiền của khoản thanh toán, máy chủ mới là nơi quyết định; chưa có lịch sử hoàn tiền (R95).
- Chia kiện không hoàn tác được; ngày hiển thị theo định dạng tiếng Anh.
- Quyền thêm / ngừng chính sách hoa hồng đang dùng quyền "duyệt đơn bán" (chưa có quyền riêng).
