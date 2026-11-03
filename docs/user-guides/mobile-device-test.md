# Hướng dẫn: chạy thử hai ứng dụng di động trên điện thoại Android thật (R68)

Mục tiêu: cài **ERP Shop** (khách) và **ERP Staff** (nhân viên) lên điện thoại Android của anh, thử **xin quyền máy ảnh**, **quét mã vạch** và **một giao dịch bán thật** trên shop thử `glcheck`. Anh làm trên điện thoại, tôi chuẩn bị dữ liệu trước và kiểm tra/dọn dữ liệu sau.

> Tôi **không** tự bấm bán, tự build hay đăng nhập Expo thay anh. Mọi thứ dưới đây tôi đã chuẩn bị và kiểm tra ở phía máy tính; phần còn lại chỉ chạy được khi có điện thoại và tài khoản Expo của anh.

## 0. Đã chuẩn bị sẵn (không cần làm)
- Hai app đã có mã định danh (`com.erp.shop`, `com.erp.staff`), icon, màn hình khởi động, cấu hình EAS (`eas.json`) và cho phép HTTP không mã hóa **chỉ trong bản `preview`**.
- Tên miền thử **`shop.glcheck.lan`** đã được gắn vào shop glcheck (cả hai app dùng chung một địa chỉ: `http://shop.glcheck.lan/api`).
- Dữ liệu thử trên shop glcheck (sẽ xóa sau khi xong):
  - Máy POS **`Phone test till`** (đang hoạt động).
  - Sản phẩm **`R68 test item`**, SKU **`R68-TEST-001`**, giá **10.000** (đặt trong bộ nhớ đệm POS; xem "Lưu ý về giá" ở cuối).
- Công cụ trong `deployment/docker/lan-test/`: `allow-lan-http.ps1`, `check-from-lan.js`, `make-barcode-page.js`, `db-snapshot.js`.
- Ảnh chụp "trước" số dòng của các database đã được lưu để so với "sau" lượt bán.

## 1. Mạng: để điện thoại gọi được máy tính này
Điện thoại và máy tính phải **cùng Wi-Fi**.
1. **Địa chỉ IP của máy tính** (hiện là `192.168.1.7`, chạy `node deployment/docker/lan-test/check-from-lan.js` để xem lại). Vào router, **đặt cố định (DHCP reservation)** địa chỉ này cho máy tính để nó không đổi.
2. **Bản ghi DNS trên router:** `shop.glcheck.lan` → IP ở bước 1. (Không dùng đuôi `.local`: Android xử lý `.local` kiểu khác nên sẽ không gọi được.) Nếu router không cho thêm bản ghi DNS, báo tôi — có cách dự phòng bằng đường hầm công khai nhưng cần anh đồng ý riêng vì nó đưa hệ thống thử ra Internet.
3. **Tường lửa Windows:** mở PowerShell **Run as administrator**, vào thư mục `deployment\docker\lan-test`, chạy `.\allow-lan-http.ps1` (mở cổng 80 chỉ cho mạng *Private*). Nếu Wi-Fi đang là *Public*, script sẽ nhắc anh đổi sang Private. **Xong việc nhớ chạy `.\allow-lan-http.ps1 -Remove`** để đóng lại. Trong lúc mở, mọi máy cùng Wi-Fi vào được hệ thống thử: hãy dùng mạng tin cậy.
4. **Kiểm tra:** `node deployment/docker/lan-test/check-from-lan.js` phải in `OK ... same shop as glcheck.shop.erp.local`. Rồi **trên điện thoại** mở trình duyệt vào `http://shop.glcheck.lan` — phải thấy trang cửa hàng. Bước này mới chứng minh cả DNS lẫn tường lửa; nếu không vào được, đừng build vội.

## 2. Build hai app bằng EAS (đám mây của Expo)
Cần tài khoản Expo miễn phí (https://expo.dev). Làm lần lượt cho **từng app**, trong thư mục `src\frontend\mobile\staff` rồi `src\frontend\mobile\customer`:
```
npm install
npx eas-cli login          # đăng nhập tài khoản Expo của anh
npx eas-cli init           # lần đầu: tạo dự án trên Expo, ghi projectId vào app.json
npm run build:android:preview
```
- Lần build đầu EAS hỏi về khóa ký (keystore): chọn để EAS tự tạo và giữ trong tài khoản của anh.
- Build mất vài phút đến vài chục phút (hàng đợi gói miễn phí). Khi xong có đường dẫn và mã QR để **tải file APK**.
- Cài APK vào điện thoại (cho phép "cài ứng dụng không rõ nguồn" cho trình duyệt/tệp khi được hỏi).
- `eas init` sửa `app.json` (thêm `extra.eas.projectId`): giữ nguyên thay đổi đó, báo tôi để tôi đưa vào commit.
- Tên gói `com.erp.shop` / `com.erp.staff` chỉ là tạm. Nếu muốn đổi, **đổi trước khi build lần đầu** (sau khi đưa lên cửa hàng ứng dụng thì không đổi được).

## 3. Kịch bản thử (đánh dấu từng dòng, chụp màn hình khi có lỗi)
### 3A. App nhân viên (ERP Staff) — đăng nhập `admin@glcheck.test` / `Gl-Check-Passw0rd!`
| # | Làm gì | Kết quả mong đợi |
|---|---|---|
| 1 | Mở app lần đầu | Màn hình khởi động xanh có biểu tượng cửa hàng, rồi màn đăng nhập tiếng Anh |
| 2 | Bấm **Sign In** khi để trống | Hai dòng "This field is required." dưới hai ô |
| 3 | Nhập đúng email/mật khẩu | Vào **Dashboard**; có bốn tab: Dashboard, POS, Inventory, Deliveries |
| 4 | Tab **POS** → **Choose** | Khung "Choose a POS terminal" liệt kê **Phone test till** |
| 5 | Chọn máy POS | Thẻ hiện tên máy và dòng "No open cash shift: the sale is not attached to one." |
| 6 | Bấm **Scan a barcode** | **Hộp thoại xin quyền máy ảnh của Android.** Ghi lại chữ hiển thị |
| 7 | Chọn **Từ chối** | Khung quét hiện giải thích; nếu chọn "Không hỏi lại" thì có nút **Open settings** |
| 8 | Cho phép (hoặc vào Cài đặt để bật) | Hiện hình máy ảnh trong khung quét |
| 9 | Mở trang mã vạch trên màn hình máy tính (xem 3C) và **quét mã vạch**, rồi thử **mã QR** | Có thông báo xanh "Added R68 test item", dòng hàng giá **$10,000.00**; quét liên tục cùng mã chỉ tăng **1 lần** trong 2 giây |
| 10 | Quét một mã không có trong hệ thống (ví dụ mã vạch trên một món đồ bất kỳ) | Thông báo đỏ "No product with the barcode …", camera vẫn mở |
| 11 | Đóng khung quét; nút **−/+** đổi số lượng | Tổng cập nhật: tạm tính, thuế 10 %, tổng |
| 12 | Chọn **Cash** → **Complete Transaction** | **Một giao dịch bán thật** — thông báo xanh "Transaction complete. Total: …", các dòng được xóa, máy POS vẫn giữ |
| 13 | Bật TalkBack (Cài đặt → Hỗ trợ tiếp cận) và duyệt các tab, nút, ô nhập | Mỗi mục được đọc tên (ví dụ "More R68 test item", "Complete Transaction") |
| 14 | Xoay máy / đổi cỡ chữ hệ thống lên lớn nhất | Không mất nút, chữ không bị cắt |

### 3B. App khách (ERP Shop) — tự đăng ký tài khoản thử `device@glcheck.test`
| # | Làm gì | Kết quả mong đợi |
|---|---|---|
| 1 | Mở app | Màn hình khởi động hồng, rồi màn đăng nhập |
| 2 | **Register** một tài khoản thử | Vào Trang chủ có banner và sản phẩm nổi bật |
| 3 | Tab **Search** → tìm "R68" → mở sản phẩm | Màn sản phẩm; giá hiện **$0.00** (xem "Lưu ý về giá") |
| 4 | **Add to cart** → quay lại → tab **Cart** | Dòng hàng, nút Giảm/Tăng/Xóa; nhóm phương thức thanh toán |
| 5 | **Không bấm Place Order** (trừ khi anh muốn thử; đơn hàng cũng ghi nhiều dữ liệu) | — |
| 6 | **Profile** → Tiếng Việt | Mọi màn đổi sang tiếng Việt |

### 3C. Trang mã vạch để quét
Trên máy tính: `node deployment/docker/lan-test/make-barcode-page.js` rồi mở file in ra trong trình duyệt (cần Internet để tải thư viện vẽ mã). Có **mã vạch Code 128** và **mã QR** của `R68-TEST-001`. Tăng độ sáng màn hình, đưa camera cách khoảng 20–30 cm. Hoặc in ra giấy.

## 4. Báo lại cho tôi
Khi xong, nhắn "đã bán xong" kèm: các dòng nào **không** đúng mong đợi (ghi số dòng), ảnh chụp màn hình lỗi, chữ trong hộp thoại xin quyền máy ảnh, và giờ bán (để tôi tìm giao dịch). Tôi sẽ:
1. Chạy `db-snapshot.js diff` và liệt kê **mọi dòng dữ liệu** mà đúng một giao dịch đã ghi (POS, kế toán, báo cáo, kho, thông báo…).
2. Đối chiếu số tiền, thuế, bút toán với giao dịch.
3. **Chỉ khi anh ra lệnh** mới xóa dữ liệu thử (giao dịch và các dòng phát sinh, sản phẩm, máy POS, tên miền, tài khoản khách thử) bằng SQL và báo số đếm về 0.

## 5. Dọn dẹp phía máy tính khi xong
- `.\allow-lan-http.ps1 -Remove` (đóng tường lửa).
- Xóa bản ghi DNS trên router nếu không dùng nữa.
- Báo tôi gỡ tên miền `shop.glcheck.lan` khỏi shop glcheck.

## Lưu ý về giá (phát hiện khi chuẩn bị — R111)
Hệ thống hiện **chưa có đường đi cho giá bán**: API sản phẩm luôn trả giá 0 (giá nằm ở biến thể/bảng giá nhưng không được đọc ra), và đặt giá cho biến thể không làm POS cập nhật. Vì vậy cửa hàng web và app khách hiện giá 0, còn app nhân viên **không cho thêm sản phẩm không có giá** vào đơn (đơn 0 đồng không hoàn tất được). Để kiểm được luồng bán, tôi đặt giá 10.000 trực tiếp cho sản phẩm thử trong bộ nhớ đệm POS — đó là cách làm riêng cho lượt thử này, không phải luồng thật. Cách sửa thật cần anh quyết định (mục DEFERRED R111).
