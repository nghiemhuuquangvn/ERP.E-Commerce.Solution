# Hướng dẫn: build file APK trên máy tính và cài lên máy ảo Android (R68, cách không cần EAS)

Hai file APK đã được build sẵn trên máy này (không cần tài khoản Expo, không cần điện thoại):

| App | Tên gói | File | Dung lượng |
|---|---|---|---|
| ERP Staff (nhân viên) | `com.erp.staff` | `src\frontend\mobile\apk\staff-preview.apk` | khoảng 82 MB |
| ERP Shop (khách) | `com.erp.shop` | `src\frontend\mobile\apk\customer-preview.apk` | khoảng 63 MB |

Thư mục `apk\` không đưa lên git. Hai file chạy được trên **máy ảo hoặc điện thoại Android bất kỳ** (Android 6.0 trở lên, x86_64 hoặc arm64), ký bằng khóa debug: chỉ dùng để thử nội bộ.

## 1. Hai app kết nối API như thế nào
```
App  ->  http://shop.glcheck.lan/api/...
      ->  nginx (cổng 80 của máy tính chạy Docker)
      ->  container Storefront (mọi tên miền lạ không phải *.erp.local đều vào đây), proxy /api sang Gateway giữ nguyên Host
      ->  Gateway :5000 đổi Host thành shop (bảng TenantDomains) -> dịch vụ
```
- **Shop được quyết định chỉ bằng tên máy (Host) mà app gọi**. Cả hai app dùng chung một địa chỉ: `http://shop.glcheck.lan/api`, nên đăng nhập luôn vào shop **glcheck**.
- Địa chỉ này được "nướng" vào file APK lúc build qua biến `EXPO_PUBLIC_API_URL` (xem `eas.json` và `-ApiUrl` của script build). Muốn đổi địa chỉ thì build lại.
- Android chặn `http://` thường; bản preview bật cho phép (`usesCleartextTraffic`), bản production thì không.
- Từ **máy ảo**, máy tính chủ có địa chỉ `10.0.2.2`. Tên `shop.glcheck.lan` phải trỏ về `10.0.2.2`.

| Thông tin | Giá trị |
|---|---|
| API base URL | `http://shop.glcheck.lan/api` |
| Tên máy (tenant) | `shop.glcheck.lan` → shop `glcheck` |
| Máy ảo thấy máy tính chủ là | `10.0.2.2` (cổng 80) |
| Điện thoại thật | tên phải phân giải ra IP của máy tính (DNS router) + mở cổng 80, xem [mobile-device-test.md](mobile-device-test.md) |
| Nhân viên đăng nhập | `admin@glcheck.test` / `Gl-Check-Passw0rd!` |
| Khách | tự đăng ký (ví dụ `device@glcheck.test`) |
| Web admin của shop | `http://glcheck.erp.local` (cùng tài khoản nhân viên) |
| Quản trị nền tảng | `http://admin.erp.local`, `superadmin@system.local` / `SuperAdmin@123` |

## 2. Cài lên máy ảo bất kỳ
1. Cài file: kéo thả file `.apk` vào cửa sổ máy ảo, hoặc `adb install -r staff-preview.apk`.
2. Cho máy ảo biết `shop.glcheck.lan` là máy tính chủ. Có hai cách:
   - **Máy ảo dùng ảnh "Google APIs" (không có Play Store)** như máy `erp-pixel` do script tạo: chạy emulator với `-writable-system`, rồi
     ```
     adb root
     adb remount
     adb shell "echo '10.0.2.2 shop.glcheck.lan' >> /system/etc/hosts"
     ```
     (nếu `remount` đòi, chạy `adb disable-verity`, `adb reboot` rồi làm lại). Không sửa file hosts của Windows.
   - **Máy ảo có Play Store (không root được)**: build lại APK với địa chỉ khác, `-ApiUrl http://10.0.2.2/api`, và nhờ tôi đăng ký thêm tên miền `10.0.2.2` cho shop glcheck (nếu không, Gateway coi đó là shop nền tảng, không phải glcheck).
3. Kiểm tra: mở app nhân viên, đăng nhập bằng tài khoản ở trên → vào Dashboard.

## 3. Chạy lại từ đầu (script trong `src\frontend\mobile\tools\`)
Cần `pwsh` (PowerShell 7). Mọi thứ nặng nằm ở `D:\Android` (JDK 17, Android SDK, Gradle, máy ảo), không đụng biến môi trường máy.
```
pwsh -File src\frontend\mobile\tools\setup-android-sdk.ps1        # một lần, khoảng 10 GB
pwsh -File src\frontend\mobile\tools\build-apk-local.ps1 -App staff      # 11-23 phút
pwsh -File src\frontend\mobile\tools\build-apk-local.ps1 -App customer
pwsh -File src\frontend\mobile\tools\start-emulator.ps1           # tạo + mở máy ảo erp-pixel, thêm dòng hosts
```
`build-apk-local.ps1` sao app sang `D:\b\<app>` (đường dẫn ngắn, vì Windows giới hạn 260 ký tự), chạy `npm install`, `expo prebuild`, `gradlew assembleRelease`, rồi chép APK về `apk\`. Cây mã trong repo không bị sửa.

## 4. Đã kiểm tra trên máy ảo (Pixel 6, Android 14)
- Quyền trong file APK (đọc bằng `aapt`): app nhân viên có **CAMERA**, INTERNET, VIBRATE, ACCESS_NETWORK_STATE; **không** có ghi âm, lưu trữ, vẽ đè (đã chặn bằng `blockedPermissions`). App khách có INTERNET và các quyền thông báo do `expo-notifications` (chưa dùng).
- Cài cả hai app thành công; mở được; màn đăng nhập hiện đúng.
- App nhân viên: đăng nhập `admin@glcheck.test` → Dashboard với 4 tab; nginx ghi `POST /api/v1/auth/login 200` từ `okhttp` (chính là app).
- Tab POS → **Scan a barcode** → nút **Allow the camera** → hộp thoại hệ thống "Allow ERP Staff to take pictures and record video?" (While using the app / Only this time / Don't allow) → chọn cho phép thì quyền CAMERA là `granted=true` và khung quét hiện hình camera của máy ảo.

## 5. Chưa kiểm tra (cần điện thoại thật hoặc anh làm tiếp)
- **Quét mã vạch thật**: máy ảo chỉ có camera ảo (một căn phòng giả), chưa đưa được mã `R68-TEST-001` vào cảnh đó. Chưa có kết quả quét.
- **Một giao dịch bán thật**: tôi dừng trước bước chọn máy POS và "Complete Transaction" (quy tắc: tôi không tự bấm bán). Anh có thể chọn máy `Phone test till`, tìm `R68 test item` (gõ SKU) rồi bán ngay trên máy ảo; báo "đã bán xong" để tôi đối chiếu dữ liệu và dọn.
- TalkBack, wifi/DNS thật, chạy trên điện thoại thật.
- Bản **production** (HTTPS, bundle `.aab`, khóa ký thật) chưa làm.

## 6. Lưu ý
- Lần đầu build mất 11-23 phút; build lại nhanh hơn nhờ bộ nhớ đệm Gradle.
- Nếu `npm ci` báo lock lệch: script dùng `npm install` trên bản sao, lock trong repo không đổi.
- Máy ảo không có hosts hợp lệ thì app đăng nhập thất bại hoặc vào nhầm shop nền tảng (R112): luôn kiểm tra bước 2.
