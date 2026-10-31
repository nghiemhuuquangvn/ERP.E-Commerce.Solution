# Hướng dẫn sử dụng: cách các màn hình hoạt động chung

Áp dụng cho mọi màn hình đã được làm mới: **Khuyến mãi, Flash sale, Tích điểm, POS (giao dịch, ca, báo cáo Z, máy POS), Hoàn hàng, Webhook, Mẫu thông báo và 17 màn hình Dữ liệu chủ** (xem [masterdata.md](masterdata.md)), và mọi hộp thoại xác nhận trong hệ thống. Các màn hình còn lại sẽ chuyển dần sang cách làm này.

> Ghi chú cho người bảo trì: viết từ mã nguồn và lần duyệt thử bằng Chrome headless trên tenant `glcheck` (14 bước đạt). Chưa có ảnh chụp trong tài liệu này.

## 1. Hộp thoại (cửa sổ nhỏ hiện lên giữa màn hình)
- Mở ra thì con trỏ nằm sẵn ở ô đầu tiên; **phím Tab** chỉ di chuyển trong hộp thoại, không nhảy ra màn hình phía sau. Màn hình phía sau không cuộn được khi hộp thoại đang mở.
- Đóng hộp thoại bằng nút **Hủy** / **Đóng**, dấu **×** ở góc trên, hoặc phím **Esc**. Đóng xong con trỏ quay lại đúng nút bạn đã bấm để mở.
- **Hộp thoại có biểu mẫu (thêm, sửa) không đóng khi bấm ra ngoài** để bạn không mất dữ liệu đã gõ. Hộp thoại chỉ để xem (chi tiết giao dịch, chi tiết ca, nhật ký gửi webhook, danh sách mã giảm giá) đóng được khi bấm ra ngoài.
- Khi đang lưu, nút **Lưu** hiện vòng xoay và không bấm lần hai được; phím Esc tạm không đóng hộp thoại cho đến khi lưu xong.
- Hộp thoại có thể mở thêm một hộp thoại khác (ví dụ chọn sản phẩm trong lúc tạo Flash sale): Esc chỉ đóng hộp thoại ở trên cùng.

## 2. Ô nhập liệu và thông báo lỗi
- Ô bắt buộc có dấu **\*** đỏ sau tên. Nếu để trống rồi bấm Lưu, thông báo lỗi hiện **ngay dưới ô đó** bằng chữ đỏ và ô được viền đỏ; trình đọc màn hình đọc lỗi này luôn.
- Lỗi do máy chủ từ chối (ví dụ địa chỉ webhook không hợp lệ, mẫu của nền tảng không được sửa) hiện trong **khung đỏ nằm trong hộp thoại**, ngay trên các nút; khung này ở lại cho đến khi bạn sửa và lưu lại hoặc đóng hộp thoại.

## 3. Thông báo kết quả (góc trên bên phải)
- Làm xong một việc (lưu, bật/tắt, xóa, gửi thử…), một thẻ nhỏ màu xanh xuất hiện ở **góc trên bên phải**: ví dụ "Đã lưu." Thẻ **tự biến mất sau khoảng 4 giây**.
- Thẻ **màu đỏ** báo việc không thực hiện được (ví dụ hãng vận chuyển từ chối hủy). Thẻ đỏ **ở lại cho đến khi bạn bấm ×**. Tối đa 3 thẻ cùng lúc, thẻ cũ nhất tự nhường chỗ.

## 4. Danh sách trống và ô lọc
- Danh sách chưa có dữ liệu hiện một khung ở giữa: biểu tượng, câu giải thích và (nếu bạn có quyền) nút để thêm bản ghi đầu tiên.
- Khi đã chọn bộ lọc mà không có kết quả, khung hiện **"Không có kết quả"** cùng nút **Xóa lọc** để bỏ hết bộ lọc và xem lại toàn bộ danh sách.
- Trong lúc tải, bảng hiện các dòng xám mờ thay cho chữ "Đang tải…"; tiêu đề cột vẫn nhìn thấy.
- Có lỗi tải dữ liệu (báo cáo Z): khung báo lỗi và nút **Thử lại**.

## 5. Trạng thái
Mọi trạng thái là một nhãn chữ có màu: **xanh lá** = hoàn tất / đang dùng, **vàng** = đang chờ người xử lý hoặc xử lý dở, **đỏ** = lỗi / đã hủy / bị từ chối, **xanh dương** = đang xử lý, **xám** = nháp / chưa bắt đầu. Màu chỉ giúp nhìn nhanh; ý nghĩa luôn có trong chữ.

## 6. Thay đổi bạn có thể nhận thấy so với trước
- Nút chính của hệ thống đậm hơn một bậc (để chữ trắng dễ đọc hơn).
- Hoàn hàng: nút **Hủy lấy hàng** thay cho chữ "Hủy" trên dòng chuyến hoàn.
- Hộp thoại xác nhận (xóa, hủy, ngưng dùng…) nay đóng được bằng **Esc**; trước đây chỉ bấm nút.
- Ô "reason" của hộp thoại nhập lý do luôn bắt đầu **trống** mỗi lần mở.
