# Hướng dẫn sử dụng: nhóm màn hình Nhân sự

Dành cho người phụ trách chấm công, nghỉ phép và lương của cửa hàng / công ty. Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md). Mô tả nghiệp vụ và cách tính lương: [docs/business/HRM](../business/HRM/) (đặc biệt `Payroll.md`).

> Ghi chú cho người bảo trì: tên màn hình, hộp thoại và ô nhập trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết; nếu đổi nhãn nhớ cập nhật. Đã duyệt thử trên trình duyệt (tài khoản quản trị cửa hàng thử): mở hộp thoại đầu tiên của 5 màn hình, kiểm tra lỗi bắt buộc, Esc, và lưu thật một ca làm việc. **Không** thử tính lương, tạo / trả / hủy phiếu lương (các thao tác này đổi dữ liệu tiền). Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
Mỗi màn hình có nút thao tác chỉ khi tài khoản có quyền **quản lý** tương ứng: chấm công (`hrm:attendance:manage`), nghỉ phép (`hrm:leave:manage`), lương — gồm Bảng lương, Hồ sơ lương, Thiết lập lương (`hrm:payroll:manage`), ca làm việc (`hrm:shifts:manage`). Không thấy nút nghĩa là tài khoản chưa được cấp quyền — nhờ quản trị viên phân quyền.

## 2. Các màn hình
| Màn hình | Nằm ở | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|---|
| Chấm công | Nhân sự → Chấm công | Xem chấm công theo nhân viên và trạng thái (Có mặt, Vắng mặt, Đi trễ, Nửa ngày, Nghỉ phép, Ngày lễ). | **Ghi nhận Chấm công vào**: Nhân viên*; Giờ vào*; Ca làm việc (tùy chọn); Ghi chú<br>**Ghi nhận Chấm công ra**: Giờ ra*; Ghi chú<br>**Đánh dấu nhân viên vắng mặt**: Nhân viên*; ngày*; Ghi chú |
| Đơn nghỉ phép | Nhân sự → Đơn nghỉ phép | Gửi, duyệt, từ chối, hủy đơn nghỉ; lọc theo nhân viên và trạng thái (Đang chờ, Đã duyệt, Đã từ chối, Đã hủy). | **Gửi đơn nghỉ phép**: Nhân viên*; Loại nghỉ phép (Nghỉ phép năm, Nghỉ ốm, Nghỉ không lương, Nghỉ thai sản, Nghỉ chăm con); Ngày bắt đầu*; Ngày kết thúc*; Lý do<br>**Từ chối đơn nghỉ phép**: Lý do* |
| Bảng lương & Phiếu lương | Nhân sự → Bảng lương | Danh sách phiếu lương, lọc theo nhân viên, năm, tháng; xem chi tiết một phiếu. | **Xử lý bảng lương**: Năm*; Tháng*<br>**Tạo phiếu lương** (nhập tay): Nhân viên*; Năm*; Tháng*; Lương gộp*; Khoản khấu trừ; Giờ tăng ca; Lương tăng ca<br>**Chi tiết phiếu lương** (xem; **Đánh dấu đã trả**; **Hủy phiếu lương** kèm **Lý do hủy***) |
| Hồ sơ lương | Nhân sự → Hồ sơ lương | Lương và người phụ thuộc của từng nhân viên; tìm theo tên / mã. | **Điều khoản lương** (nút **Lương** ở cuối dòng): Hiệu lực từ*; Lương cơ bản*; Phụ cấp chịu thuế; Phụ cấp miễn thuế; Lương đóng bảo hiểm; ô chọn Đóng BHXH / BHYT / BHTN; Mã số thuế; Số sổ BHXH<br>**Người phụ thuộc** (nút **Người phụ thuộc**): bảng hiện có + mẫu **Thêm người phụ thuộc**: Họ tên*; Quan hệ*; Mã số thuế; Từ tháng*; Đến tháng |
| Thiết lập lương | Nhân sự → Thiết lập lương | Các bộ tỷ lệ bảo hiểm, mức giảm trừ và biểu thuế TNCN, mỗi bộ có hiệu lực từ một tháng. | **Bộ tỷ lệ mới / Sửa bộ tỷ lệ**: Tên*; Hiệu lực từ*; tỷ lệ BHXH / BHYT / BHTN của người lao động và doanh nghiệp (%); mức lương đóng tối đa; giảm trừ bản thân và mỗi người phụ thuộc; số ngày phép năm; các bậc thuế (Đến, Thuế suất %); ô chọn **Kế toán đã xác nhận** |
| Ca làm việc | Nhân sự → Ca làm việc | Ca làm việc (giờ bắt đầu / kết thúc, số giờ ca). | **Thêm ca làm việc**: Tên*; Giờ bắt đầu*; Giờ kết thúc* |

## 3. Việc thường làm
Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc, để trống thì **"Bắt buộc"** hiện dưới ô và hộp thoại ở lại; **Esc** hoặc **Hủy** đóng hộp thoại; lưu xong hiện thẻ xanh **"Đã lưu."** vài giây.

### 3.1 Chấm công
- **Chấm công vào**: bấm nút, chọn nhân viên, giờ vào (mặc định bây giờ), có thể chọn ca; **Lưu**.
- **Chấm công ra**: ở dòng đang mở (đã vào, chưa ra) bấm **Chấm công ra**, kiểm giờ ra, **Lưu**.
- **Vắng mặt**: chọn nhân viên và ngày, **Lưu**; dòng hiện trạng thái "Vắng mặt".
- Khi lọc mà không có dòng nào, khung **"Không có kết quả"** hiện cùng nút **Xóa lọc**.

### 3.2 Nghỉ phép
1. **+ Gửi đơn nghỉ phép**: chọn nhân viên và loại nghỉ; với **Nghỉ phép năm**, khung xanh cho biết số ngày phép còn lại trong năm ("Phép năm 2026: còn x/y ngày").
2. Điền ngày, lý do, **Lưu**: đơn ở trạng thái **Đang chờ**.
3. Đơn đang chờ có **Duyệt** (hỏi xác nhận), **Từ chối** (bắt buộc ghi lý do) và **Hủy** (hỏi xác nhận). Nếu hệ thống từ chối duyệt (ví dụ hết phép năm), một khung đỏ ghi lý do hiện phía trên bảng.

### 3.3 Tính lương và phiếu lương
Lương được hệ thống tính từ hồ sơ lương, ngày công, nghỉ không lương và bộ tỷ lệ có hiệu lực (xem tài liệu nghiệp vụ). Màn hình này không đổi cách tính.
- **Xử lý bảng lương**: chọn năm, tháng, bấm **Xử lý bảng lương**: tạo phiếu lương cho mọi nhân viên đang làm việc. Nhân viên chưa có hồ sơ lương bị **bỏ qua** và liệt kê trong hộp thoại kết quả ("Bỏ qua n nhân viên…") — nhập hồ sơ lương rồi tính lại tháng đó.
- **+ Tạo phiếu lương**: nhập tay một phiếu cho một nhân viên (không có bảng tính chi tiết).
- **Chi tiết**: xem lương cơ bản, ngày công, phụ cấp, tăng ca, các khoản bảo hiểm và thuế TNCN bị khấu trừ, lương thực nhận. Phiếu **Chưa trả** có hai nút: **Đánh dấu đã trả** (một lần bấm, không hỏi lại) và **Hủy phiếu lương** (hiện ô **Lý do hủy**, nút **Hủy phiếu lương** chỉ bấm được khi đã nhập lý do).

### 3.4 Hồ sơ lương, Thiết lập lương, Ca làm việc
- **Hồ sơ lương**: **Lương** ghi điều khoản mới theo tháng hiệu lực; **Người phụ thuộc** thêm / xóa người được giảm trừ (dùng khi tính thuế). Dòng "Chưa có hồ sơ lương" nghĩa là nhân viên sẽ bị bỏ qua khi tính lương.
- **Thiết lập lương**: luật đổi thì **+ Bộ tỷ lệ mới** (không sửa đè bộ cũ). Bộ mới mặc định là **NHÁP — chờ kế toán xác nhận**; chỉ ô **Kế toán đã xác nhận** đánh dấu đã được kế toán duyệt. Chỉ một bậc thuế không giới hạn và phải là bậc cuối.
- **Ca làm việc**: thêm / sửa ca; **Ngừng hoạt động** hỏi xác nhận, ca không bị xóa.

## 4. Khi hệ thống từ chối
Nếu dữ liệu không hợp lệ theo quy tắc của hệ thống, một **khung đỏ** hiện trong hộp thoại, ngay trên các nút, ghi lý do; sửa rồi **Lưu** lại, hoặc **Hủy**. Với thao tác ngoài hộp thoại (duyệt đơn nghỉ, trả phiếu lương) khung đỏ hiện trong hộp thoại chi tiết hoặc phía trên bảng.

## 5. Giới hạn cần biết
- Chưa thử trên trình duyệt: lưu các hộp thoại chấm công, gửi / duyệt / từ chối đơn nghỉ, **xử lý và tạo phiếu lương, trả và hủy phiếu lương**, lưu hồ sơ lương và người phụ thuộc, lưu bộ tỷ lệ (chỉ kiểm bằng kiểm thử tự động và mở hộp thoại).
- Tài khoản cửa hàng thử chưa có nhân viên trong hồ sơ lương nên hộp thoại của màn **Hồ sơ lương** chưa được mở trên trình duyệt.
- **Đánh dấu đã trả** không hỏi xác nhận (hành vi cũ, giữ nguyên).
- Chỉ **Bảng lương**, **Đơn nghỉ phép** và **Chấm công** có nút **Xóa lọc**; các màn còn lại chưa có bộ lọc.
