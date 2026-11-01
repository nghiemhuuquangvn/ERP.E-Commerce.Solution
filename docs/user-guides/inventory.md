# Hướng dẫn sử dụng: nhóm màn hình Tồn kho

Dành cho thủ kho, quản lý kho và người mua hàng. Các màn hình nằm ở menu **Tồn kho**: **Tồn kho**, **Vị trí kho**, **Kho hàng**, **Điều chỉnh tồn kho**, **Kiểm kê**, **Chuyển kho**, **Mã vạch**, **Chốt định giá tồn kho**. Phân tích nghiệp vụ đầy đủ (quy trình, trạng thái, quy tắc, kiểm thử chấp nhận): [docs/business/Warehouse/AdminBackOffice.md](../business/Warehouse/AdminBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: tên màn hình, hộp thoại và ô nhập trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết. Đã duyệt thử trên trình duyệt (tài khoản quản trị cửa hàng thử): cả 9 trang, tạo kho và 2 vị trí, phiếu chuyển kho (nháp → hộp xác nhận xuất → hủy), yêu cầu điều chỉnh và từ chối, bắt đầu kiểm kê → hộp xác nhận hoàn tất → hủy kiểm kê, hộp xác nhận chốt kỳ (không chốt). **Không** chạy: duyệt điều chỉnh, duyệt chênh lệch kiểm kê, xuất / nhận hàng chuyển kho, chốt kỳ (các thao tác này đổi tồn kho hoặc khóa kỳ). Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
- **Xem** các màn hình: `inventory:view`.
- **Quản lý** vị trí, kiểm kê (bắt đầu / hoàn tất / hủy), chuyển kho, mã vạch, chính sách tồn: `inventory:manage`.
- **Yêu cầu điều chỉnh**: `inventory:adjustment:create`; **duyệt / từ chối**: `inventory:adjustment:approve`.
- **Duyệt chênh lệch kiểm kê**: `inventory:stocktake:approve`. **Chốt kỳ định giá**: `inventory:valuation:close`.
- **Kho hàng** (thêm, sửa, ngừng): `inventory:warehouse:manage`.
Không thấy nút nghĩa là tài khoản chưa được cấp quyền.

## 2. Các màn hình
| Màn hình | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|
| Tồn kho | Danh sách sản phẩm: tồn thực tế, khả dụng, tối thiểu, tối đa, trạng thái; tìm theo sản phẩm / SKU; "Chỉ hàng sắp hết" | — |
| Chi tiết tồn kho (bấm tên sản phẩm) | 4 ô số (Tồn thực tế, Khả dụng, Đã giữ, Điểm đặt hàng lại), **Chính sách tồn kho**, các thẻ Vị trí / Lô hàng / Serial / Lịch sử giá vốn | **Yêu cầu điều chỉnh tồn kho**: Số lượng thay đổi* (khác 0); Lý do* (Hư hỏng, Thất lạc, Điều chỉnh sai lệch, Khác); Ghi chú |
| Kho hàng | Danh sách kho; Sửa; Ngừng kích hoạt / Kích hoạt | **Thêm / Sửa kho hàng**: Mã* (chỉ khi thêm); Tên*; Địa chỉ |
| Vị trí kho | Danh sách vị trí, lọc theo kho, thanh mức sử dụng sức chứa | **Thêm / Sửa vị trí**: Mã* (chỉ khi thêm); Kho; Tên; Khu vực; Dãy*; Kệ*; Tầng*; Sức chứa* |
| Điều chỉnh tồn kho | Các yêu cầu điều chỉnh, lọc Tất cả / Chờ duyệt (mặc định) / Đã duyệt / Đã từ chối | **Từ chối điều chỉnh**: Lý do* |
| Kiểm kê | Danh sách phiếu kiểm kê; **Bắt đầu kiểm kê** (đếm toàn bộ kho) | Trang chi tiết (mục 3.2) |
| Chuyển kho | Danh sách phiếu chuyển; **Chuyển kho mới** | **Phiếu mới**: Vị trí nguồn*; Vị trí đích*; Ghi chú; các dòng (Sản phẩm, Số lượng) |
| Mã vạch | Tra mã vạch quét được ra sản phẩm; thêm ánh xạ mã vạch ↔ sản phẩm | Mã vạch*; Mã sản phẩm* (để thêm ánh xạ) |
| Chốt định giá tồn kho | Chốt giá trị tồn kho cuối kỳ; xem kỳ đã chốt | **Chốt kỳ**: Mã kỳ* (ví dụ 2026-07); Mã kho (tùy chọn) |

## 3. Việc thường làm
Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô; **Esc** hoặc **Hủy** đóng mà không lưu; lưu xong hiện thẻ xanh **"Đã lưu."**. Khi hệ thống từ chối, một khung đỏ ghi lý do.

### 3.1 Điều chỉnh số lượng tồn
1. Mở **Tồn kho**, bấm tên sản phẩm, bấm **Yêu cầu điều chỉnh**. Nhập số lượng thay đổi (âm = giảm), chọn lý do, **Gửi yêu cầu**. Hệ thống báo "Đã gửi yêu cầu điều chỉnh - chờ duyệt." — **tồn kho chưa đổi**.
2. Quản lý kho vào **Điều chỉnh tồn kho** (mặc định hiện các yêu cầu **Chờ duyệt**): **Duyệt** (tồn kho đổi ngay) hoặc **Từ chối** (bắt buộc ghi lý do). Số âm hiện màu đỏ, số dương màu xanh.

### 3.2 Kiểm kê
1. **Kiểm kê → + Bắt đầu kiểm kê** (kiểm kê toàn bộ kho), mở phiếu vừa tạo.
2. Với từng dòng, nhập **SL đếm được** và bấm **Ghi nhận**. Cột **Chênh lệch** = đếm được − hệ thống (thiếu màu đỏ, dư màu xanh, có dấu).
3. **Hoàn tất đếm**: hộp xác nhận nói rõ việc đếm dừng lại, chênh lệch chuyển sang chờ duyệt và **tồn kho chưa thay đổi**.
4. Người có quyền duyệt bấm **Duyệt chênh lệch**: hộp xác nhận nói tồn kho sẽ được điều chỉnh theo chênh lệch và **không hoàn tác được**.
5. **Hủy kiểm kê** (còn mở thì hủy được, có hỏi xác nhận): số đã đếm bị bỏ, tồn kho không đổi. Phiếu Hoàn tất / Đã hủy không còn nút.

### 3.3 Chuyển hàng giữa hai vị trí
1. **Chuyển kho → + Chuyển kho mới**: chọn vị trí nguồn và đích (phải khác nhau), thêm dòng (sản phẩm, số lượng ≥ 1), **Lưu** → phiếu ở trạng thái **Nháp**.
2. Trên danh sách: **Xuất chuyển** (hỏi xác nhận: hàng rời khỏi vị trí nguồn) hoặc **Hủy** (chỉ phiếu nháp, hỏi xác nhận).
3. Khi hàng đến nơi, phiếu **Đang vận chuyển** có nút **Hoàn tất** (hỏi xác nhận: hàng được cộng vào vị trí đích).
Danh sách hiện **mã vị trí**, không hiện mã nội bộ.

### 3.4 Kho và vị trí
Thêm kho ở **Kho hàng**, rồi thêm vị trí ở **Vị trí kho** (chọn kho, nhập mã, dãy, kệ, tầng, sức chứa). Cột Sức chứa cho biết số đang chứa / tối đa và thanh chuyển đỏ từ 90 %. **Ngừng kích hoạt** hỏi xác nhận, không xóa dữ liệu.

### 3.5 Chính sách tồn kho của sản phẩm
Ở trang chi tiết: nhập **Tối thiểu**, **Tồn an toàn**, **Tối đa**. Điều kiện: 0 ≤ an toàn ≤ tối thiểu < tối đa (nếu sai, khung vàng báo và nút Lưu bị khóa). Khi tồn khả dụng chạm mức tối thiểu, Mua hàng đề xuất đơn tới mức tối đa.

### 3.6 Chốt kỳ định giá
Nhập **Mã kỳ**, bấm **Chốt kỳ**: hộp xác nhận nói giá trị tồn kho được lưu thành ảnh chụp và kỳ bị khóa. Mỗi kỳ chốt một lần. Xem lại bằng cách nhập mã kỳ ở khung "Xem kỳ đã chốt".

## 4. Khi hệ thống từ chối
Khung đỏ trong hộp thoại ghi lý do (ví dụ không đủ tồn ở vị trí nguồn, kỳ đã chốt). Với các nút ngoài hộp thoại (Duyệt, Xuất chuyển, Hoàn tất, Hủy…) lý do hiện bằng thẻ đỏ ở góc trên bên phải và dữ liệu không đổi.

## 5. Giới hạn đã biết
- Điều chỉnh, dòng kiểm kê, dòng chuyển kho và chốt định giá hiện **mã sản phẩm** thay vì tên (R87).
- Kiểm kê luôn đếm **toàn bộ kho**; chưa chọn được phạm vi.
- Phiếu chuyển kho đã xuất chưa hủy được từ màn hình.
- Chưa có màn hình nhập hàng / cất hàng, lịch sử biến động, danh sách giữ hàng, quản lý lô và serial; nhập hàng vào kho đi qua Mua hàng, bán hàng và POS.
- **Duyệt điều chỉnh, duyệt kiểm kê, xuất / nhận chuyển kho và chốt kỳ chưa được chạy thử trên dữ liệu thật** khi viết hướng dẫn này; mỗi bước đã có hộp xác nhận và kiểm thử tự động.
