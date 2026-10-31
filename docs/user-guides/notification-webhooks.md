# Hướng dẫn sử dụng: màn hình Webhook thông báo

Dành cho quản trị viên của cửa hàng và người phụ trách kỹ thuật phía hệ thống nhận. Mô tả nghiệp vụ đầy đủ: [WebhooksAndTemplates.md](../business/Notification/WebhooksAndTemplates.md).

> Ghi chú cho người bảo trì: hướng dẫn viết từ mã nguồn, nhãn tiếng Việt và tài liệu nghiệp vụ; chưa có ảnh chụp màn hình vì chưa duyệt thử trên trình duyệt. Chưa thử gửi tới một địa chỉ trả 2xx thật.

Cách hộp thoại, thông báo kết quả và danh sách trống hoạt động chung: xem [common-screen-behaviour.md](common-screen-behaviour.md).

## 1. Webhook là gì, dùng khi nào
Webhook là một **địa chỉ web của hệ thống bạn** (ví dụ phần mềm chăm sóc khách hàng, Zapier, hệ thống nội bộ). Mỗi khi ERP **gửi** một thông báo (email, SMS…) hoặc **gửi lỗi**, ERP gửi một tin JSON tới địa chỉ đó để hệ thống của bạn biết.

Các sự kiện hiện có:
| Sự kiện | Khi nào |
|---|---|
| `notification.sent` | Một thông báo đã được gửi |
| `notification.failed` | Một lần gửi thông báo bị lỗi (có thể được thử lại) |
| `webhook.test` | Chỉ khi bạn bấm **Gửi thử** |

## 2. Ai dùng được
Quyền **Quản lý webhook thông báo** (`notifications:webhooks:manage`). Không có quyền thì mục không hiện trong menu.

## 3. Mở màn hình
Menu bên trái → **Cài đặt** → **Webhook**.

## 4. Đọc màn hình
Bảng gồm: Tên · Địa chỉ · Sự kiện (hoặc "Tất cả sự kiện") · **Có ký** · **Lần gửi gần nhất** (Đã gửi / Lỗi / Chưa gửi) · công tắc bật/tắt. Mỗi dòng có **Gửi thử**, **Nhật ký gửi**, Sửa và Xóa.

## 5. Việc thường làm

### 5.1 Thêm webhook
1. Bấm **Thêm webhook**.
2. Nhập **Tên** để nhớ.
3. Nhập **Địa chỉ (HTTPS)**. Phải là địa chỉ https **công khai**. Các địa chỉ `http://`, `localhost`, địa chỉ nội bộ (10.x, 192.168.x, 172.16–31.x), địa chỉ máy chủ tên đơn (kiểu `sqlserver`) hoặc có tài khoản trong địa chỉ đều bị từ chối.
4. (Khuyên dùng) nhập **Khóa bí mật để ký**: chuỗi bất kỳ đủ dài. Hệ thống dùng nó ký mỗi tin để bạn kiểm tra tin đúng là của ERP.
5. Chọn **Tất cả sự kiện** hoặc tick từng sự kiện.
6. Bấm **Lưu**.

### 5.2 Gửi thử
1. Bấm **Gửi thử** ở dòng đó (dùng được cả khi webhook đang tắt).
2. Hộp kết quả hiện mã HTTP, thời gian (ms) và lỗi nếu có: "Đầu nhận đã chấp nhận tin thử." hoặc "Đầu nhận không chấp nhận tin thử."
3. Lần thử cũng được ghi vào nhật ký.

Ví dụ: kết quả 405 nghĩa là địa chỉ đó không nhận lệnh POST — chỉnh hệ thống nhận cho nhận POST.

### 5.3 Xem nhật ký gửi
Bấm **Nhật ký gửi**: danh sách mới nhất ở trên, mỗi lần gửi có sự kiện, kết quả, mã HTTP, thời gian, lỗi. **Chỉ giữ 30 ngày gần nhất.**

### 5.4 Đổi hoặc xóa khóa bí mật
- Sửa webhook, **để trống ô khóa** → giữ nguyên khóa cũ (ô ghi "Đã lưu - để trống để giữ nguyên"). Khóa không bao giờ hiện lại.
- Nhập khóa mới → thay khóa cũ. Nhớ cập nhật khóa ở hệ thống nhận.
- Tick **Xóa khóa bí mật đã lưu** → bỏ ký (cột "Có ký" trống).

### 5.5 Tạm ngừng / xóa
Gạt công tắc để tắt hoặc bật. **Xóa** sẽ xóa luôn nhật ký gửi của webhook đó (có hỏi xác nhận).

## 6. Hệ thống nhận cần làm gì
Mỗi tin là POST JSON, có các header:
- `X-Event-Type` — tên sự kiện;
- `X-Timestamp` — thời điểm gửi (giây Unix);
- `X-Hub-Signature: sha256=<hex>` — nếu có khóa: HMAC-SHA256 của chuỗi `"<X-Timestamp>.<nội dung JSON>"` bằng khóa bí mật.

Hãy kiểm tra chữ ký, rồi **trả mã 2xx** càng nhanh càng tốt. Hệ thống không đi theo chuyển hướng (301/302).

## 7. Thông báo lỗi thường gặp
| Thấy gì | Nghĩa | Cần làm |
|---|---|---|
| "Phải là địa chỉ https công khai…" | Địa chỉ bị chặn vì an toàn | Dùng địa chỉ https công khai |
| "Không lưu được webhook." kèm lý do | Tên/địa chỉ/sự kiện không hợp lệ | Sửa theo lý do |
| Nhật ký ghi Lỗi, mã 4xx/5xx | Hệ thống nhận từ chối hoặc lỗi | Kiểm tra hệ thống nhận |
| Nhật ký ghi Lỗi không có mã | Không kết nối được (hết thời gian, tên miền sai) | Kiểm tra địa chỉ và chứng chỉ https |

## 8. Giới hạn cần biết
- Tin lỗi **không được thử lại** và webhook lỗi **không tự tắt** (R62): hãy theo dõi cột "Lần gửi gần nhất".
- Khóa bí mật của webhook tạo trước tháng 10/2026 chỉ được mã hóa sau khi bạn **Lưu** lại webhook đó (R61).
- Chỉ có 2 sự kiện thông báo (đã gửi / gửi lỗi); chưa có sự kiện đơn hàng, thanh toán…
