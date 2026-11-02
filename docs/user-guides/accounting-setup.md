# Hướng dẫn sử dụng: Kế toán — Thuế suất, Kỳ kế toán, Hệ thống tài khoản, Tài khoản ngân hàng và đối chiếu

Bốn màn hình nằm trong nhóm **Kế toán** của thanh menu bên trái: **Thuế suất**, **Kỳ kế toán**, **Hệ thống tài khoản**, **Tài khoản ngân hàng** (mỗi mục hiện khi tài khoản có quyền tương ứng). Phân tích nghiệp vụ: [AdminSetup.md](../business/Accounting/AdminSetup.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `vi.json`. Đã duyệt thử trên trình duyệt (tenant `glcheck`): tạo thuế suất, tài khoản, tài khoản ngân hàng, sao kê và nhập một dòng CSV thử; mở các hộp xác nhận rồi đóng bằng Esc. **Không** chạy thật: Áp mẫu hệ thống tài khoản, Đóng / Mở lại kỳ, Hạch toán phí / lãi và Đối chiếu trên sổ thật (chỉ mở hộp xác nhận rồi đóng). Dữ liệu thử đã xóa bằng SQL. Chưa có ảnh chụp trong tài liệu.

Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô; **Esc** hoặc **Hủy** đóng mà không lưu; hộp xác nhận chồng lên hộp thoại: **Esc chỉ đóng hộp trên cùng**. Lưu xong có thông báo **"Đã lưu."**; hệ thống từ chối thì lý do hiện ngay (thông báo đỏ hoặc trong hộp thoại), không còn lỗi im lặng.

## 1. Thuế suất
**Ai dùng được:** quyền quản lý thuế (`accounting:tax:manage`) thấy các nút thêm, sửa, ngừng hoạt động.
- **Thêm thuế suất**: **Tên**, **Mã** (không trùng trong cùng cửa hàng), **Thuế suất (%)** (từ 0 đến 100; ô gợi ý "Nhập 10 nghĩa là 10 % giá trị chưa thuế"), **Mô tả** (không bắt buộc) → **Lưu**. Nhập 150 thì báo "Nhập thuế suất từ 0 đến 100." và chưa gửi gì.
- **Ngừng hoạt động**: hỏi xác nhận có nêu tên thuế suất; xác nhận xong, thuế suất không còn chọn được cho chứng từ mới, chứng từ cũ không đổi. Ô **Chỉ hiện đang hoạt động** lọc danh sách.

## 2. Hệ thống tài khoản
**Ai dùng được:** quyền quản lý tài khoản (`accounting:accounts:manage`).
- **Thêm tài khoản**: **Số tài khoản**, **Tên**, **Loại tài khoản**, **Số dư bình thường**, **Tài khoản cha** (chỉ hiện tài khoản đang dùng, cùng loại), ô **Tài khoản tiền**, **Nhóm dòng tiền**.
- **Sửa**: chỉ đổi tên, tài khoản cha và hai mục tiền; **số, loại và số dư bình thường bị khóa** và có dòng giải thích "Không đổi được sau khi đã tạo tài khoản."
- **Ngừng dùng**: hỏi xác nhận ("Tài khoản không nhận bút toán mới, lịch sử trong sổ vẫn giữ"). Hệ thống từ chối (ví dụ còn số dư) thì lý do hiện thành thông báo đỏ. **Kích hoạt** lại là một lần bấm, có thông báo "Đã lưu.".
- **Áp mẫu hệ thống tài khoản**: chọn mẫu (mẫu Vas99 có khung vàng nhắc nhờ kế toán rà soát) → **Áp mẫu hệ thống tài khoản** → hộp xác nhận ghi rõ tên mẫu và số tài khoản sẽ thêm ("Chỉ thực hiện được khi chưa có bút toán nào"). Esc ở hộp xác nhận quay lại hộp chọn mẫu, Esc lần nữa đóng hẳn; chưa áp gì.

## 3. Kỳ kế toán
**Ai dùng được:** quyền quản lý kỳ (`accounting:periods:manage`).
- **Mở kỳ kế toán**: **Tên kỳ**, **Ngày bắt đầu**, **Ngày kết thúc** (ngày kết thúc trước ngày bắt đầu thì báo "Ngày kết thúc không được trước ngày bắt đầu.").
- **Đóng kỳ**: hộp xác nhận nêu hậu quả — ghi bút toán kết chuyển cuối kỳ và **khóa kỳ**, mở lại được sau. Hệ thống từ chối (còn kỳ trước chưa đóng, còn bút toán nháp, bảng cân đối thử không cân) thì lý do hiện thành thông báo đỏ.
- **Mở lại**: nhập lý do bắt buộc; bút toán kết chuyển của kỳ bị hủy.
- **Chính sách khóa sổ**: chế độ (hàng tháng / cuối năm), tháng kết thúc năm tài chính, tự trích lãi vay → **Lưu** ("Đã lưu."). Chỉ đổi chế độ giữa hai năm tài chính.
- Khung **Đánh giá lại ngoại tệ** ở cuối trang chưa thay đổi (giữ giao diện cũ, xem DEFERRED R96).

## 4. Tài khoản ngân hàng và đối chiếu
**Ai dùng được:** `accounting:bank:view` xem; `accounting:bank:manage` thêm, nhập dòng, ghép, hạch toán, đối chiếu.
1. **Thêm tài khoản ngân hàng**: tên, ngân hàng, số tài khoản, số dư đầu kỳ, loại tiền (mặc định VND).
2. **Xem sao kê** → hộp **Sao kê ngân hàng**; **Thêm sao kê** (ngày, số dư đầu kỳ, số dư cuối kỳ — đủ cả ba mới lưu được).
3. **Dòng sao kê** (nút theo từng sao kê, đọc là "Dòng sao kê ngày …") mở hộp **Đối chiếu ngân hàng** chồng lên:
   - **File CSV của ngân hàng**: cột ngày, nội dung, tham chiếu, số tiền (hoặc hai cột ghi nợ / ghi có). **Có dòng sai thì không nhập dòng nào** và hộp liệt kê từng dòng sai kèm lý do.
   - **Ghép tự động** rồi ghép tay: **Ghép** mở danh sách dòng sổ cái cùng số tiền; **Bỏ ghép** hoàn lại.
   - **Hạch toán phí / lãi**: hộp xác nhận ghi rõ "Hệ thống ghi một bút toán". **Xóa**: hộp xác nhận nêu dòng, có thể nhập lại CSV.
   - Phần tóm tắt cho biết số dư cuối sao kê, số dư sổ cái, khoản chưa lên sao kê, dòng sao kê chưa có trong sổ và **Chênh lệch** viết thành lời ("Chênh lệch 0 - sẵn sàng đối chiếu" hoặc "Chênh lệch … - chưa đối chiếu được").
   - **Đối chiếu** (chân hộp) chỉ bật khi chênh lệch bằng 0; hộp xác nhận nói rõ sao kê sẽ **bị khóa**, không sửa được các dòng nữa.
4. Esc đóng lần lượt: hộp xác nhận → hộp đối chiếu → hộp sao kê.

## 5. Giới hạn đã biết
- Chưa sửa được tài khoản ngân hàng, sao kê hay từng dòng sao kê (xóa rồi nhập lại).
- Khung đánh giá lại ngoại tệ (trang Kỳ kế toán) vẫn là giao diện cũ và gửi ngay khi bấm (R96).
- Số tiền hiển thị theo định dạng của trình duyệt (ví dụ `1.200` hoặc `1,200`).
