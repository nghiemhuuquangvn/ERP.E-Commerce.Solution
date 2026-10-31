# Hướng dẫn sử dụng: màn hình Hoàn hàng

Dành cho nhân viên kho / vận chuyển và chăm sóc khách hàng của cửa hàng. Mô tả nghiệp vụ đầy đủ: [ReturnsBackOffice.md](../business/Shipping/ReturnsBackOffice.md).

> Ghi chú cho người bảo trì: hướng dẫn viết từ mã nguồn, nhãn trên màn hình tiếng Việt và tài liệu nghiệp vụ; chưa có ảnh chụp màn hình vì chưa duyệt thử trên trình duyệt. Thêm ảnh sau khi duyệt thử.

Cách hộp thoại, thông báo kết quả và danh sách trống hoạt động chung: xem [common-screen-behaviour.md](common-screen-behaviour.md).

## 1. Màn hình này dùng để làm gì
Khi một đơn trả hàng được duyệt, hệ thống tự đặt một chuyến **lấy hàng hoàn về** tại hãng vận chuyển (GHTK, GHN…). Màn hình **Hoàn hàng** cho bạn:
- xem mọi chuyến hoàn của cửa hàng ở một chỗ;
- thấy ngay chuyến nào **hãng từ chối nhận** (ví dụ thiếu phường/xã) để đặt lại;
- ghi nhận **đã nhận hàng hoàn** khi hàng về mà hãng chưa báo;
- **hủy** chuyến lấy hàng khi khách đổi ý.

## 2. Ai dùng được
| Việc | Quyền cần có |
|---|---|
| Xem danh sách | Xem vận chuyển (`shipping:view`) |
| Đặt lại, Đã nhận hàng, Hủy | Cập nhật vận chuyển (`shipping:update`) |

Không có quyền cập nhật thì các nút thao tác không hiện.

## 3. Mở màn hình
Menu bên trái → **Hoàn hàng** (ngay dưới **Vận đơn**).

## 4. Đọc màn hình
- **Ô tìm kiếm** — gõ mã đơn hàng, mã vận đơn, mã đơn của hãng hoặc tên hãng.
- **Lọc trạng thái** — Chờ lấy hàng · Đang hoàn về · Đã nhận · Đã hủy.
- **Hãng từ chối đặt** (ô tick) — chỉ hiện các chuyến cần người xử lý.
- **Bảng:** Đơn hàng · Lý do (khách trả hàng) · Hãng và mã vận đơn · Trạng thái · ngày tạo / ngày nhận. Chuyến bị hãng từ chối có **dòng chữ đỏ** ghi lý do của hãng và chưa có mã vận đơn.
- Danh sách mới nhất ở trên, chia trang.

### Ý nghĩa trạng thái
| Trạng thái | Nghĩa |
|---|---|
| Chờ lấy hàng | Đã yêu cầu hãng đến lấy (hoặc hãng từ chối và chờ bạn đặt lại) |
| Đang hoàn về | Hãng đã lấy hàng, đang chuyển về kho |
| Đã nhận | Hàng đã về; đơn được phép hoàn tiền |
| Đã hủy | Chuyến lấy hàng đã bị hủy |

## 5. Việc thường làm

### 5.1 Đặt lại chuyến hãng đã từ chối
1. Vào **Hoàn hàng**, tick **Hãng từ chối đặt**.
2. Đọc lý do chữ đỏ (ví dụ "thiếu phường/xã").
3. Sửa nguyên nhân ở đơn hàng (thường là địa chỉ của khách).
4. Quay lại dòng đó, bấm **Đặt lại**.
5. Thành công: màn hình báo "Đã đặt lấy hàng: *mã vận đơn*", dòng có mã vận đơn và rời khỏi bộ lọc. Bấm hai lần không đặt thêm chuyến thứ hai.
6. Hãng vẫn từ chối: lý do mới hiện ra, dòng vẫn ở bộ lọc — sửa tiếp rồi đặt lại.

### 5.2 Ghi nhận đã nhận hàng
Khi hàng hoàn đã về kho mà hãng chưa cập nhật:
1. Tìm dòng bằng mã đơn hoặc mã vận đơn.
2. Bấm **Đã nhận hàng**. Màn hình báo "Đã ghi nhận nhận hàng."
3. Trạng thái thành **Đã nhận**; việc hoàn tiền cho đơn không còn bị giữ.

### 5.3 Hủy chuyến lấy hàng
1. Bấm **Hủy lấy hàng** trên dòng đang **Chờ lấy hàng**.
2. Xác nhận trong hộp thoại ("Hủy lần lấy hàng … tại hãng và đóng yêu cầu này?").
3. Hệ thống hủy **tại hãng trước**, rồi mới đóng yêu cầu: "Đã hủy yêu cầu hoàn hàng."
4. Hãng từ chối hủy (shipper đã đi lấy): hiện lý do của hãng và yêu cầu **vẫn mở**. Chờ hàng về rồi bấm **Đã nhận hàng**.

## 6. Thông báo lỗi thường gặp
| Thấy gì | Nghĩa | Cần làm |
|---|---|---|
| Lý do chữ đỏ của hãng | Hãng không nhận đặt | Sửa dữ liệu rồi **Đặt lại** |
| "Thao tác không thành công." kèm lý do | Yêu cầu bị từ chối (đã nhận / đã hủy rồi, hoặc hãng không hủy được) | Đọc lý do; không bấm lặp lại |
| "Hãng vẫn chưa nhận lấy hàng." | Đặt lại nhưng hãng vẫn chưa nhận | Kiểm tra địa chỉ, thử lại sau |

## 7. Giới hạn cần biết
- **Hủy chuyến hoàn không báo cho đơn bán hàng**: đơn vẫn ở trạng thái "yêu cầu hoàn" và việc hoàn tiền vẫn chờ hàng. Nếu khách không trả nữa, xử lý đơn riêng (rủi ro R59).
- Chưa có thao tác hàng loạt và chưa xuất file (R60).
- Phí của chuyến hoàn chưa được ghi sổ tự động (R45) và khách chưa thấy theo dõi hoàn hàng trên cửa hàng online (R46).
- Viettel Post và J&T chưa hỗ trợ đặt chuyến hoàn.
