# Hướng dẫn sử dụng: Mua hàng (Đơn hàng mua, Yêu cầu báo giá, Hóa đơn nhà cung cấp, Trả hàng, Dung sai đối chiếu)

Dành cho nhân viên mua hàng, thủ kho nhận hàng và kế toán công nợ phải trả. Các màn hình nằm ở menu **Mua hàng**: **Đơn hàng mua**, **Yêu cầu báo giá**, **Hóa đơn nhà cung cấp**, **Trả hàng nhà cung cấp**, **Dung sai đối chiếu**. Phân tích nghiệp vụ đầy đủ (trạng thái, quy tắc, kiểm thử chấp nhận): [docs/business/Purchasing/AdminBackOffice.md](../business/Purchasing/AdminBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: tên màn hình, hộp thoại và ô nhập trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết. Đã duyệt thử trên trình duyệt (tài khoản quản trị nền tảng, dữ liệu thử đã xóa): mở cả 5 trang, hộp thoại Yêu cầu báo giá mới / Phiếu trả hàng / Ghi hóa đơn (để trống rồi lưu, Esc), dung sai lớn hơn 100 bị từ chối, một yêu cầu báo giá đi trọn vòng (tạo → gửi, có xác nhận → nhập báo giá → xem so sánh → mở xác nhận Chọn nhà cung cấp rồi Esc → hủy). **Không** chạy trên dữ liệu thật: tạo đơn mua từ báo giá, duyệt đơn, nhận hàng (đổi tồn kho), duyệt hoặc từ chối hóa đơn (ghi sổ công nợ), xuất trả hàng; các bước này chỉ có hộp xác nhận và kiểm thử tự động. Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
- **Yêu cầu báo giá**: xem `rfq:read`; tạo, gửi, hủy, nhập báo giá, chọn nhà cung cấp `rfq:manage`.
- **Đơn hàng mua**: duyệt / từ chối `purchase_order:approve`; **Tài chính duyệt** `purchase_order:finance_approve`; hủy `purchase_order:cancel`; **Nhận hàng** `purchase_order:receive`.
- **Hóa đơn nhà cung cấp**: xem `supplier_invoice:read`; ghi, đối chiếu lại, từ chối `supplier_invoice:manage`; **Duyệt thanh toán** `supplier_invoice:approve`; **Duyệt dù chênh lệch** (hóa đơn đang giữ) `supplier_invoice:approve_variance`.
- **Trả hàng nhà cung cấp**: xem `vendor_return:read`; tạo, gửi trả, hủy, ghi giấy báo có `vendor_return:manage`.
- **Dung sai đối chiếu**: sửa `purchasing_settings:manage`.
Không thấy nút nghĩa là tài khoản chưa được cấp quyền.

## 2. Các màn hình
| Màn hình | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|
| Đơn hàng mua | Danh sách đơn, lọc theo trạng thái (nhóm nút Tất cả, Nháp, Đã gửi…); **Duyệt**, **Tài chính duyệt**, **Từ chối**, **Hủy** theo trạng thái | **Từ chối đơn**: Lý do từ chối* |
| Chi tiết đơn mua | Thông tin đơn, các dòng hàng, nút **Nhận hàng** và liên kết tới hóa đơn / phiếu trả hàng của đơn | **Nhận hàng**: mỗi dòng có ô **Đạt** và **Không đạt** |
| Yêu cầu báo giá | Danh sách yêu cầu; lọc trạng thái | **Yêu cầu báo giá mới**: Tiêu đề*; Cần trước ngày (không ở quá khứ); các dòng hàng (Mã hàng*, Hàng hóa, Số lượng ≥ 1, ĐVT); chọn nhà cung cấp được hỏi |
| Chi tiết yêu cầu báo giá | Dòng hàng, nhà cung cấp và trạng thái từng nhà cung cấp, bảng **So sánh báo giá**; nút **Gửi nhà cung cấp**, **Hủy yêu cầu**, **Nhập báo giá / Sửa báo giá**, **Từ chối**, **Chọn nhà cung cấp** | **Báo giá của nhà cung cấp**: Số báo giá; Hiệu lực đến*; Tiền tệ; Thời gian giao; **Đơn giá của từng dòng*** |
| Hóa đơn nhà cung cấp | Danh sách hóa đơn; lọc trạng thái | **Ghi hóa đơn nhà cung cấp**: Đơn mua*; Số hóa đơn của NCC*; Ngày hóa đơn; Hạn thanh toán; Thuế; các dòng (nút **Điền theo phần còn được lập hóa đơn**) |
| Chi tiết hóa đơn | Số liệu hóa đơn, lý do bị giữ, bảng **đối chiếu ba bên** (đặt – nhận – trả – đã lập – lần này); nút **Đối chiếu lại**, **Duyệt thanh toán**, **Duyệt dù chênh lệch**, **Từ chối** | Ô **Lý do** ngay trên trang |
| Trả hàng nhà cung cấp | Danh sách phiếu trả; **Gửi trả**, **Hủy**, **Ghi giấy báo có** | **Phiếu trả hàng**: Đơn mua* (đã nhận hàng); số lượng trả từng dòng (≤ "Còn trả được"); Lý do*. **Giấy báo có**: Số giấy báo có*; Số tiền được trừ* (> 0) |
| Dung sai đối chiếu | Mức lệch cho phép trước khi hóa đơn bị giữ | Dung sai số lượng (%), Dung sai giá (%) (0–100), công tắc **Tự duyệt hóa đơn đã khớp** |

## 3. Việc thường làm
Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô; **Esc** hoặc **Hủy** đóng mà không lưu; lưu xong hiện thẻ xanh **"Đã lưu."**. Khi hệ thống từ chối, một khung đỏ (hoặc thẻ đỏ góc trên bên phải với nút ngoài hộp thoại) ghi lý do; nếu máy chủ không nêu lý do sẽ hiện **"Đã xảy ra lỗi. Vui lòng thử lại."**.

### 3.1 Hỏi giá nhiều nhà cung cấp rồi chọn một
1. **Yêu cầu báo giá → + Yêu cầu báo giá mới**: nhập tiêu đề, ít nhất một dòng hàng có mã, tick các nhà cung cấp cần hỏi → **Lưu**. Yêu cầu ở trạng thái **Nháp**.
2. **Gửi nhà cung cấp**: hộp xác nhận nói rõ các dòng hàng không sửa được nữa. Cần có ít nhất một nhà cung cấp được mời, nếu không hệ thống từ chối và ghi lý do.
3. Với từng nhà cung cấp, bấm **Nhập báo giá**: điền đơn giá **cho mọi dòng**, hiệu lực (mặc định 14 ngày) → **Lưu**. Nhà cung cấp không báo giá thì bấm **Từ chối** (có hỏi xác nhận).
4. Bảng **So sánh báo giá** hiện mỗi báo giá một cột, đơn giá thấp nhất có nhãn xanh, báo giá hết hạn bị mờ và ghi "hết hạn" (không chọn được); nhà cung cấp có tổng thấp nhất có dấu ★.
5. **Chọn nhà cung cấp**: hộp xác nhận ghi tên nhà cung cấp, nói rõ hệ thống **tạo đơn mua nháp theo giá đã báo và đóng yêu cầu**. Sau đó đơn mua nháp được mở ra.
6. **Hủy yêu cầu** (có hỏi xác nhận) đóng yêu cầu; các báo giá vẫn được giữ để tham khảo.

### 3.2 Duyệt đơn mua
Ở **Đơn hàng mua**, đơn **Đã gửi** có **Duyệt** và **Từ chối**; đơn **Chờ Tài chính duyệt** có **Tài chính duyệt**. **Duyệt** và **Tài chính duyệt** đều hỏi xác nhận kèm số đơn. **Từ chối** bắt buộc nhập lý do. **Hủy** hỏi xác nhận và không hoàn tác được. Nếu bị từ chối, lý do của hệ thống hiện bằng thẻ đỏ (trước đây im lặng).

### 3.3 Nhận hàng
1. Mở đơn (đã duyệt / đã đặt / nhận một phần) → **Nhận hàng**.
2. Mỗi dòng cho biết **Còn lại**; nhập **Đạt** (nhập kho) và **Không đạt** (bị loại). Đạt + Không đạt của một dòng **không được vượt quá số còn lại**: nếu vượt, hiện cảnh báo "Vượt quá số lượng còn lại." và nút khóa. Dòng cuối ghi tổng "Đạt … · loại …".
3. Bấm **Ghi nhận phiếu nhập** → hộp xác nhận: "Nhập n đơn vị đạt vào kho (m bị loại)? **Phiếu nhập không hoàn tác được.**" → **Nhập vào kho**. Tồn kho tăng đúng số đạt.

### 3.4 Kiểm tra hóa đơn nhà cung cấp
1. **Hóa đơn nhà cung cấp → + Ghi hóa đơn**: chọn đơn mua, nhập số hóa đơn của nhà cung cấp, bấm **Điền theo phần còn được lập hóa đơn** rồi chỉnh số lượng / đơn giá nếu hóa đơn khác → **Ghi và đối chiếu**.
2. Hệ thống đối chiếu với đơn mua, phiếu nhập, hàng trả và các hóa đơn trước. Kết quả từng dòng: **Khớp**, lệch số lượng, lệch giá, hoặc không có trong đơn. Có chênh lệch quá dung sai thì hóa đơn **Đang giữ** và nêu lý do.
3. Hóa đơn **Đã khớp**: **Duyệt thanh toán** → hộp xác nhận "Công nợ được ghi sổ ở Kế toán và hóa đơn không đổi được nữa" → xác nhận.
4. Hóa đơn **Đang giữ**: người có quyền duyệt chênh lệch nhập **Lý do** rồi **Duyệt dù chênh lệch** (cùng hộp xác nhận). Người không có quyền chỉ thấy dòng nhắc cần quyền.
5. **Từ chối**: nút chỉ bật khi đã gõ **Lý do**; hộp xác nhận nói nhà cung cấp phải gửi hóa đơn mới và không ghi sổ gì.
6. **Đối chiếu lại** dùng khi sau đó có phiếu nhập hoặc phiếu trả mới.

### 3.5 Trả hàng nhà cung cấp
**+ Phiếu trả hàng**: chọn đơn đã nhận hàng, nhập số lượng trả từng dòng (không quá "Còn trả được", ít nhất một dòng > 0) và lý do → **Lưu** (phiếu **Nháp**). **Gửi trả** hỏi xác nhận: hàng rời kho **ngay**; **Hủy** (chỉ phiếu nháp) hỏi xác nhận, tồn kho không đổi. Khi nhà cung cấp gửi giấy báo có: **Ghi giấy báo có** (số và số tiền > 0) — Kế toán ghi giảm khoản phải trả.

### 3.6 Dung sai đối chiếu
Nhập % lệch số lượng và % lệch giá cho phép (0–100) rồi **Lưu**; giá trị ngoài khoảng bị từ chối ngay trong form. Công tắc **Tự duyệt hóa đơn đã khớp** làm hóa đơn khớp được duyệt (và ghi sổ) **không cần người**; hóa đơn đang giữ không bao giờ tự duyệt.

## 4. Giới hạn đã biết
- **Phiếu nhập không hoàn tác được** và chưa có màn hình lịch sử nhập; nhập nhầm thì sửa bằng điều chỉnh tồn kho (R93).
- Tiền và ngày của đơn mua luôn theo định dạng tiếng Anh (R94).
- Không thể gửi yêu cầu báo giá nếu cửa hàng chưa có nhà cung cấp đang hoạt động.
- Chưa sửa được yêu cầu báo giá đã gửi và chưa chọn nhiều nhà cung cấp cho từng dòng.
