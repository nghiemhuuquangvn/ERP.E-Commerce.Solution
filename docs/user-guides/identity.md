# Hướng dẫn sử dụng: Người dùng, Vai trò và Đơn vị

Dành cho quản trị viên cửa hàng (người dùng, vai trò) và quản trị nền tảng (đơn vị). Ba màn hình là ba mục của thanh menu bên trái: **Người dùng**, **Vai trò**, **Đơn vị** (mỗi mục hiện khi tài khoản có quyền xem tương ứng). Phân tích nghiệp vụ đầy đủ (trạng thái, quy tắc, kiểm thử chấp nhận): [docs/business/Identity/AdminBackOffice.md](../business/Identity/AdminBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: tên màn hình, hộp thoại và ô nhập trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết. Đã duyệt thử trên trình duyệt bằng tài khoản quản trị nền tảng (người dùng và đơn vị thử, đã xóa): mở 3 trang, hộp thoại Mời / Thêm vai trò / Thêm đơn vị, sửa hồ sơ, khóa và mở khóa, gán và gỡ vai trò, vô hiệu hóa, tick quyền của vai trò, đổi tên và xóa vai trò, thêm / vô hiệu hóa / kích hoạt đơn vị. **Không** chạy: khóa hoặc đổi vai trò của người dùng thật, vô hiệu hóa đơn vị thật. **Mời một tài khoản đang ở đơn vị khác hiện báo lỗi máy chủ** (R89, xem mục 5). Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
- **Người dùng**: xem danh sách cần quyền xem; **Mời, Khóa / Mở khóa, Quản lý vai trò**: `identity:users:manage`; **Sửa hồ sơ**: `identity:users:edit`; **Vô hiệu hóa**: `identity:users:delete`.
- **Vai trò**: **Thêm** `identity:roles:create`, **Sửa** `:edit`, **Xóa** `:delete`, **Quyền hạn** `:manage`.
- **Đơn vị**: **Thêm** `identity:tenants:create`, **Sửa** `:edit`, **Kích hoạt / Vô hiệu hóa** `:manage`. Ô chọn **Đơn vị** trên trang Người dùng và Vai trò chỉ hiện với quyền xem đơn vị (quản trị nền tảng).
Không thấy nút nghĩa là tài khoản chưa được cấp quyền.

## 2. Các màn hình
| Màn hình | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|
| Người dùng | Danh sách người dùng của đơn vị: email, họ tên, vai trò (nhãn), trạng thái (Đang hoạt động / Ngừng hoạt động / Đã khóa) | **Mời người dùng**: Email*; Vai trò (không bắt buộc). **Chỉnh sửa hồ sơ**: Họ tên; Tên đăng nhập. **Quản lý vai trò**: danh sách vai trò, ô chọn **Vai trò cần gán** + **Gán**, nút **Gỡ** từng vai trò |
| Vai trò | Danh sách vai trò; danh mục quyền hạn (chỉ đọc) ở dưới | **Thêm / Sửa vai trò**: Mã* (chỉ khi thêm, không đổi được sau này); Tên*. **Quản lý quyền hạn**: ô **Lọc quyền hạn**, các nhóm có tiêu đề "đã cấp n/m", từng quyền là một ô tick |
| Đơn vị | Danh sách đơn vị (cửa hàng) và trạng thái | **Thêm / Sửa đơn vị**: Mã* (chỉ khi thêm); Tên* |

## 3. Việc thường làm
Mọi hộp thoại: con trỏ nằm sẵn ở ô đầu; ô có dấu **\*** đỏ là bắt buộc — để trống thì **"Bắt buộc"** hiện dưới ô; **Esc** hoặc **Hủy** đóng mà không lưu; lưu xong hiện thẻ xanh **"Đã lưu."**. Khi hệ thống từ chối, một khung đỏ trong hộp thoại (hoặc thẻ đỏ góc trên bên phải với nút ngoài hộp thoại) ghi lý do.

### 3.1 Mời một người vào cửa hàng
1. **Người dùng → Mời người dùng**. Hộp thoại ghi rõ: người này **phải đã có tài khoản**; họ chỉ được thêm vào đơn vị này, hệ thống **không tạo tài khoản mới và không gửi email**.
2. Nhập Email, chọn vai trò nếu muốn, bấm **Mời người dùng**. Kết quả hiện ngay trong hộp thoại: thành công, "đã là thành viên", hoặc lý do bị từ chối (không có tài khoản với email này; email trùng ở nhiều đơn vị).
3. Hộp thoại vẫn mở để mời người khác; bấm **Đóng** khi xong.

### 3.2 Khóa, mở khóa, vô hiệu hóa
- **Khóa**: hộp xác nhận nói rõ người đó **bị đăng xuất khỏi mọi nơi ngay** và không đăng nhập được cho đến khi mở khóa. **Mở khóa** cho họ đăng nhập lại.
- **Vô hiệu hóa**: người đó bị đăng xuất ngay và mất quyền truy cập đơn vị này. Người bị vô hiệu hóa có nút **Kích hoạt lại** trên dòng của họ (cần quyền quản lý người dùng; không có ở dòng của chính bạn): hộp xác nhận nói họ đăng nhập lại được và vai trò giữ nguyên.
- Trên **dòng của chính bạn** không có nút Khóa và Vô hiệu hóa, để bạn không tự đăng xuất mình. Máy chủ cũng từ chối việc đó.
- **Hệ thống không cho cửa hàng tự khóa mình ra ngoài.** Quản trị viên là người đang hoạt động, không bị khóa và có vai trò kèm quyền *quản lý người dùng*. Máy chủ từ chối (lý do hiện trong thẻ đỏ) khi: bạn khóa / vô hiệu hóa chính mình; khóa, vô hiệu hóa hoặc gỡ vai trò của **quản trị viên cuối cùng**; xóa vai trò (hoặc gỡ quyền quản lý người dùng khỏi vai trò) mà mọi quản trị viên chỉ có nhờ vai trò đó; xóa vai trò mặc định **CUSTOMER / STAFF / USER**; vô hiệu hóa đơn vị nền tảng. Muốn đổi quản trị viên: cấp quyền cho người mới trước, rồi mới khóa người cũ.

### 3.3 Gán vai trò
**Quản lý vai trò** → chọn vai trò ở ô **Vai trò cần gán** → **Gán**. Mỗi vai trò đã có hiện một nút **Gỡ**. Chỉ những vai trò chưa có mới nằm trong ô chọn. Thay đổi có hiệu lực ngay với phiên của người đó.

### 3.4 Tạo vai trò và cấp quyền
1. **Vai trò → Thêm vai trò**: nhập Mã (ví dụ SALES) và Tên.
2. Bấm **Quyền hạn** ở dòng vai trò: tick từng quyền. **Mỗi lần tick được lưu ngay** và hiện "Đã lưu."; nếu hệ thống từ chối (ví dụ quyền chỉ dành cho nền tảng, hoặc bạn không có quyền đó để cấp) thì hiện thẻ đỏ ghi lý do và ô tick trở về như cũ.
3. Gõ vào **Lọc quyền hạn** để thu hẹp danh sách; không khớp thì hiện "Không có kết quả" và nút **Xóa lọc**.
4. **Xóa vai trò**: hộp xác nhận nói rõ **mọi người đang giữ vai trò này mất quyền của nó ngay lập tức**.

### 3.5 Đơn vị (quản trị nền tảng)
**Đơn vị → Thêm đơn vị** (Mã, Tên). **Vô hiệu hóa** hỏi xác nhận kèm tên đơn vị: mọi người dùng của đơn vị bị chặn ngay ở mọi dịch vụ; **Kích hoạt** khôi phục. Mã đơn vị không đổi được sau khi tạo.

## 4. Khi hệ thống từ chối
Hộp thoại ghi lý do thật của máy chủ (ví dụ "Role code 'X' is reserved", "No account found for email …"). Với các nút ngoài hộp thoại (Khóa, Xóa vai trò, Vô hiệu hóa đơn vị…) lý do hiện bằng thẻ đỏ và dữ liệu không đổi.

## 5. Giới hạn đã biết
- Mời một tài khoản đang thuộc đơn vị khác nay đã dùng được (trước đây báo lỗi máy chủ, R89). Người được mời vào danh sách người dùng của đơn vị với vai trò đã chọn.
- Khi quản trị nền tảng chọn **đơn vị khác** ở ô Đơn vị, danh sách người dùng và vai trò của đơn vị đó **chỉ để xem**: có dòng thông báo màu xanh và không có nút thao tác nào (Mời, Thêm, Sửa, Khóa, Vô hiệu hóa, Kích hoạt lại, Quản lý vai trò). Muốn thay đổi hãy chọn **Đơn vị của tôi**.
- Không tạo được tài khoản người dùng từ màn hình này (tài khoản do người dùng tự đăng ký hoặc đăng nhập bằng dịch vụ ngoài).
