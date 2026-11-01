# Hướng dẫn sử dụng: Cài đặt Vận chuyển — Quy tắc chọn hãng và Bảng phí giao hàng

Dành cho quản lý vận chuyển của cửa hàng. Màn hình: **Cài đặt → Cài đặt Vận chuyển**, hai thẻ **Quy tắc chọn hãng** và **Bảng phí giao hàng** (các thẻ **Đơn vị** và **Hãng vận chuyển** là phần cấu hình khác). Nghiệp vụ: [docs/business/Shipping/CarrierIntegration.md](../business/Shipping/CarrierIntegration.md), [docs/business/Shipping/ShippingFee.md](../business/Shipping/ShippingFee.md) và mục 4.4 của [docs/business/Identity/AdminBackOffice.md](../business/Identity/AdminBackOffice.md). Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md).

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `vi.json` lúc viết. Đã duyệt thử trên trình duyệt (tài khoản quản trị cửa hàng thử): thêm, sửa, xóa một quy tắc và một dòng phí (dữ liệu thử đã xóa), kiểm tra khoảng sai, hộp xác nhận xóa. **Không** chạy nút **Thử** (gọi báo giá hãng vận chuyển thật). Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
Xem: quyền xem cài đặt vận chuyển của đơn vị. **Thêm / Sửa / Xóa**: quyền quản lý cài đặt vận chuyển; không có quyền thì không thấy các nút.

## 2. Quy tắc chọn hãng
Quyết định **hãng nào nhận kiện hàng**. Các quy tắc được xét theo **số ưu tiên nhỏ trước**; chỉ quy tắc **đang bật** được tính; quy tắc khớp đầu tiên chọn hãng. Đơn không khớp quy tắc nào đi theo **báo giá rẻ nhất**; hãng từ chối thì kiện chuyển sang hãng kế tiếp.

| Ô trong hộp thoại (* = bắt buộc) | Ý nghĩa |
|---|---|
| Tên* | Tên gợi nhớ |
| Ưu tiên (nhỏ = trước) | Số nguyên; 10 được xét trước 100 |
| Hãng | GHTK, VTP hoặc GHN |
| Tỉnh / thành | Cách nhau bằng dấu phẩy; để trống = mọi nơi |
| Cân nặng tối thiểu / tối đa (g), Giá trị đơn tối thiểu / tối đa | Để trống = không giới hạn; **tối thiểu không được lớn hơn tối đa** |
| Thanh toán | Mọi hình thức / chỉ thu hộ (COD) / chỉ trả trước |
| Đang bật | Tắt để giữ quy tắc nhưng không dùng |

- **Thêm quy tắc** → điền → **Lưu** → thẻ xanh "Đã lưu.". Ô sai hiện lý do ngay dưới ô (ví dụ khoảng tối thiểu lớn hơn tối đa, ưu tiên không phải số nguyên).
- **Sửa**: mở lại hộp thoại. **Xóa**: hiện **hộp xác nhận** "Đơn mới sẽ không dùng quy tắc này nữa; các bưu kiện đã đặt không đổi" — Esc hoặc Hủy giữ nguyên quy tắc. (Trước đây nút Xóa xóa ngay.)
- **Thử một kiện hàng** (dưới danh sách): nhập tỉnh, quận / huyện, cân nặng, giá trị, tiền thu hộ rồi bấm **Thử** — màn hình cho biết quy tắc nào thắng và giá của từng hãng. **Không đặt đơn vận chuyển nào.**

## 3. Bảng phí giao hàng
Quyết định **khách trả bao nhiêu tiền giao hàng** (giá của cửa hàng cho khách, không phải giá của hãng). Dòng đang bật khớp đầu tiên (số ưu tiên nhỏ trước, theo tỉnh và cân nặng) đặt mức phí; kiện không khớp dòng nào trả **phí cố định** ở thẻ **Đơn vị**; đơn đạt ngưỡng miễn phí thì miễn phí.

| Ô (* = bắt buộc) | Ý nghĩa |
|---|---|
| Tên* | Tên dòng |
| Ưu tiên | Số nguyên |
| Phí (₫) | Không âm |
| Tỉnh / thành | Cách nhau bằng dấu phẩy; để trống = mọi tỉnh; không phân biệt hoa thường, dấu, "Tỉnh / Thành phố" |
| Từ cân nặng (g), Đến cân nặng (g) | Để trống = không giới hạn; **"từ" không được lớn hơn "đến"** |
| Đang bật | Tắt để giữ dòng nhưng không dùng |

**Thêm / Sửa / Xóa** giống mục 2. **Xóa** nói rõ: đơn mới sẽ tính theo dòng khớp kế tiếp hoặc phí cố định; đơn đã đặt không đổi. **Thử một kiện hàng**: nhập tỉnh, cân nặng, giá trị hàng → cho biết khách trả bao nhiêu và dòng nào quyết định; không tính tiền ai.

## 4. Khi hệ thống từ chối
Hộp thoại có khung đỏ ghi lý do của máy chủ; với nút Xóa lý do hiện bằng thẻ đỏ góc trên bên phải và quy tắc vẫn còn.

## 5. Giới hạn đã biết
- Chưa có thao tác sắp xếp kéo thả; thứ tự chỉ do số ưu tiên (hai dòng cùng số ưu tiên: thứ tự không đảm bảo).
- Nút **Thử** chưa được kiểm tra trên môi trường có hãng vận chuyển thật khi viết hướng dẫn này.
