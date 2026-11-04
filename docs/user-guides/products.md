# Hướng dẫn sử dụng: nhóm màn hình Sản phẩm

Dành cho người quản lý danh mục hàng của cửa hàng: thương hiệu, danh mục, thuộc tính, bảng giá và từng sản phẩm. Cách hộp thoại, lỗi và thông báo hoạt động chung: [common-screen-behaviour.md](common-screen-behaviour.md). Mô tả nghiệp vụ: [docs/business/Product](../business/Product/).

> Ghi chú cho người bảo trì: tên màn hình, hộp thoại và ô nhập trong bảng dưới trích từ mã nguồn và bản dịch tiếng Việt (`vi.json`) lúc viết; nếu đổi nhãn nhớ cập nhật. Đã duyệt thử trên trình duyệt (tài khoản quản trị của cửa hàng thử) tất cả màn hình dưới đây, trừ tải ảnh lên (xem mục 6). Chưa có ảnh chụp trong tài liệu.

## 1. Ai dùng được
- **Thương hiệu** cần quyền `product:brands:manage` để thấy nút Thêm / Sửa / Ngừng dùng.
- **Danh mục, Thuộc tính, Bảng giá, Sản phẩm** (thêm, sửa, tải ảnh, biến thể, thông số) cần quyền `products:edit`; tạo sản phẩm mới cần `products:create`.
- **Quy đổi đơn vị** cần `product:uom-conversions:view` để xem và `product:uom-conversions:manage` để thêm / sửa.
Không thấy nút nghĩa là tài khoản chưa được cấp quyền — nhờ quản trị viên phân quyền.

## 2. Các màn hình
| Màn hình | Nằm ở | Dùng để | Hộp thoại và ô nhập (* = bắt buộc) |
|---|---|---|---|
| Sản phẩm | Sản phẩm → Sản phẩm | Danh sách sản phẩm; tìm theo tên / SKU; lọc theo trạng thái (Tất cả, Nháp, Chờ duyệt, Đang bán, Ngừng bán). | Trang riêng (mục 3) |
| Thương hiệu | Sản phẩm → Thương hiệu | Danh sách thương hiệu, lọc Tất cả / Đang dùng / Ngừng dùng. | **Thêm thương hiệu**: Mã* (chỉ khi thêm mới); Tên*; Địa chỉ logo |
| Danh mục | Sản phẩm → Danh mục | Cây danh mục hàng (hiện dạng danh sách phẳng). | **Thêm danh mục**: Tên*; Mã; Danh mục cha |
| Thuộc tính | Sản phẩm → Thuộc tính | Định nghĩa thuộc tính (Chất liệu, Màu sắc…) để gán cho sản phẩm và biến thể. | **Thêm thuộc tính**: Mã* (chữ thường, số, gạch dưới); Tên*; Kiểu dữ liệu; Danh mục; Đơn vị; ô chọn: dùng để phân biệt biến thể, Bắt buộc, Có thể lọc; Giá trị lựa chọn (với kiểu Chọn một / Chọn nhiều) |
| Bảng giá | Sản phẩm → Bảng giá | Tạo bảng giá theo kênh bán, mở một bảng giá theo mã để xem. | **Thêm bảng giá**: Tên*; Kênh bán; Mô tả; Ngày hiệu lực*; Ngày hết hạn; Tiền tệ; các dòng **Mặt hàng** (Mã biến thể*, Giá gốc*, Giá khuyến mãi, Số lượng tối thiểu) |
| Biến thể (thẻ trong trang sản phẩm) | Sản phẩm → mở sản phẩm → Biến thể | SKU con của sản phẩm (màu, cỡ…). | **Thêm biến thể**: SKU* (chỉ khi thêm mới); Mã vạch; Giá vốn; Khối lượng + đơn vị; Chiều dài; Chiều rộng; Chiều cao + đơn vị; các thông số dùng để phân biệt biến thể |
| Quy đổi đơn vị (thẻ) | … → Quy đổi đơn vị | Hệ số quy đổi giữa hai đơn vị của sản phẩm. | **Thêm quy đổi đơn vị**: Từ đơn vị*; Sang đơn vị*; Hệ số quy đổi* |

## 3. Trang sản phẩm
### 3.1 Danh sách
Gõ vào ô tìm để tìm theo tên hoặc SKU (tìm tối đa 50 kết quả, không phân trang); bấm một nút trạng thái để lọc. Khi tìm hoặc lọc mà không có sản phẩm nào, khung **"Không có kết quả"** hiện cùng nút **Xóa lọc** đưa danh sách về như ban đầu.

### 3.2 Thêm sản phẩm
Bấm **+ Thêm sản phẩm**. Điền **Tên sản phẩm*** (tối thiểu 3 ký tự), **SKU***, loại, danh mục, thương hiệu, đơn vị tính cơ bản, mô tả và phần SEO (slug, meta title, meta description). Bấm **Lưu**: nếu thiếu tên hoặc SKU, câu **"Bắt buộc"** hiện dưới ô đó (tên ngắn hơn 3 ký tự: **"Tối thiểu 3 ký tự."**). Lưu xong, thẻ xanh **"Đã lưu."** hiện và trang chuyển sang sản phẩm vừa tạo.

### 3.3 Sửa sản phẩm
Sau khi tạo, **tên, SKU, loại và danh mục không đổi được** (ô bị khóa xám, ghi "Không thể thay đổi sau khi tạo"). Có thể sửa thương hiệu, đơn vị tính cơ bản, mô tả và SEO rồi **Lưu**.

### 3.4 Hành động trạng thái
Khung **Hành động trạng thái** có: **Gửi duyệt** (từ Nháp), **Xuất bản** (từ Nháp hoặc Chờ duyệt, **cần ít nhất một biến thể**), **Ngừng bán** (hỏi xác nhận; sản phẩm bị gỡ khỏi cửa hàng). **Kích hoạt lại** chưa dùng được vì hệ thống chưa hỗ trợ. Thành công thì hiện thẻ **"Đã lưu."**; bị từ chối thì khung đỏ ghi lý do của hệ thống.

### 3.5 Bốn thẻ bên dưới
Chỉ dùng được sau khi sản phẩm đã được lưu.
- **Hình ảnh**: bảng ảnh (Ảnh xem trước, Văn bản thay thế, Thứ tự, Ảnh chính). Thêm ảnh bằng cách **kéo thả** vào khung nét đứt, **nhấn vào khung** (hoặc Tab tới khung rồi Enter) để chọn tệp, hoặc **dán URL ảnh ngoài**. Chấp nhận JPEG, PNG, WEBP, GIF, tối đa 5 MB; sai loại hoặc quá lớn thì khung đỏ báo ngay. Mỗi dòng có **Đặt làm ảnh chính**, **Thay thế**, **Xóa**.
- **Biến thể**: bảng biến thể; **+ Thêm biến thể**, **Sửa**, **Xóa** (hỏi xác nhận). Sản phẩm đã xuất bản phải còn ít nhất một biến thể, nếu không xóa được hệ thống sẽ báo.
- **Thông số**: nhập giá trị cho các thuộc tính mô tả sản phẩm (không dùng để phân biệt biến thể), bấm **Lưu**. Mỗi ô có nhãn theo tên thuộc tính (kèm đơn vị).
- **Quy đổi đơn vị**: thêm / sửa hệ số quy đổi (phải lớn hơn 0).

## 4. Thương hiệu, danh mục, thuộc tính, bảng giá
- **Thêm / sửa**: bấm nút **+ Thêm …** ở góc trên bên phải (hoặc **Sửa** ở cuối dòng), điền, **Lưu**. Ô bắt buộc có dấu **\*** đỏ; để trống thì **"Bắt buộc"** hiện dưới ô. Lưu xong hiện thẻ xanh **"Đã lưu."**.
- **Thương hiệu**: **Ngừng dùng** hỏi xác nhận; thương hiệu ngừng dùng vẫn nằm trong danh sách, bấm **Kích hoạt** để dùng lại. Mã không đổi được sau khi tạo.
- **Danh mục**: có thể đổi danh mục cha nhưng **không thể bỏ** danh mục cha đã chọn; thứ tự chỉ để xem.
- **Thuộc tính**: mã chỉ gồm chữ thường, chữ số và dấu gạch dưới (sai thì ô báo **"Chỉ gồm chữ thường, chữ số và dấu gạch dưới."**). Thuộc tính kiểu **Chọn một / Chọn nhiều** cần ít nhất một giá trị lựa chọn: gõ giá trị, bấm **Thêm**, các giá trị đã thêm hiện thành thẻ nhỏ. **Xóa** thất bại nếu đã có sản phẩm dùng thuộc tính.
- **Bảng giá**: hệ thống **không có danh sách tất cả bảng giá**; màn hình chỉ nhớ các bảng giá bạn vừa tạo hoặc vừa mở trong phiên làm việc này. Để xem lại bảng giá cũ, dán **mã bảng giá** vào ô và bấm **Mở**. Trên thẻ **Bảng giá** có thể đổi kênh bán và tiền tệ; thẻ **Mặt hàng** chỉ để xem — muốn đổi giá hãy tạo bảng giá mới. Trong hộp thoại thêm, dùng **Thêm mặt hàng** để thêm dòng và **Xóa** để bỏ dòng (luôn còn ít nhất một dòng); mã biến thể và giá gốc của mỗi dòng là bắt buộc.

## 5. Khi hệ thống từ chối
Nếu dữ liệu không hợp lệ theo quy tắc của hệ thống, một **khung đỏ** hiện trong hộp thoại (hoặc ngay trên nút của trang sản phẩm) ghi lý do; sửa rồi **Lưu** lại, hoặc **Hủy** / Esc. Với **Xóa** (biến thể, thuộc tính) và **Ngừng dùng** (thương hiệu), lý do hiện dưới dạng thẻ đỏ góc trên bên phải.
- **Lưu ý đã biết (R76):** nhập **mã trùng** (ví dụ thương hiệu) khiến máy chủ trả lỗi chung "Yêu cầu không thể hoàn tất. Lỗi đã được ghi lại." thay vì nói rõ mã đã tồn tại. Kiểm tra mã trước khi lưu.

## 6. Giới hạn cần biết
- Chưa thử trên trình duyệt: tải ảnh lên bằng kéo thả / chọn tệp, lưu biến thể kèm thông số, lưu bảng giá có nhiều mặt hàng — các luồng này chỉ được kiểm bằng kiểm thử tự động và mở hộp thoại.
- Danh sách thương hiệu hiện chưa có khung "thêm thương hiệu đầu tiên" khi trống; danh mục và thuộc tính cũng vậy.
- Bảng giá: không có màn hình liệt kê, không sửa được mặt hàng sau khi tạo (giới hạn của hệ thống phía sau).

## 7. Đặt giá bán lẻ cho sản phẩm
Giá bán lẻ là giá khách trả; **giá vốn** (cột "Giá vốn") là chi phí và không dùng để bán.
1. Vào **Sản phẩm → Sản phẩm**, mở sản phẩm, chọn thẻ **Biến thể**.
2. Bảng biến thể có cột **Giá bán lẻ** (hiện **Chưa đặt** nếu biến thể chưa có giá). Bấm **Đặt giá** ở dòng biến thể.
3. Nhập **Giá bán** và, nếu muốn, **Giá khuyến mãi (không bắt buộc)**. Giá khuyến mãi phải **thấp hơn** giá bán, nếu không hộp thoại báo "Giá khuyến mãi phải thấp hơn giá bán." và không gửi gì. Bấm **Lưu**: thẻ xanh **"Đã lưu."** hiện.
4. Giá được lưu trong bảng giá **"Default retail"** của cửa hàng (tự tạo ở lần đặt giá đầu tiên), không đổi giá vốn. Danh sách sản phẩm, cửa hàng web, app khách và app nhân viên (POS) đều hiện giá mới; quầy POS nhận giá mới trong vài giây.

Quy tắc chọn giá khi có nhiều bảng giá: chỉ tính bảng giá đang bật, đã đến ngày hiệu lực và chưa hết hạn; ưu tiên kênh **Retail**, nếu không có thì kênh **Ecommerce** (bảng giá **Wholesale** không bao giờ là giá bán lẻ); trong cùng kênh, bảng giá có ngày hiệu lực mới nhất thắng; giá khuyến mãi (nếu có) thắng giá gốc. Sản phẩm nhiều biến thể hiện giá của biến thể **rẻ nhất**. Sản phẩm chưa có giá hiện 0.

Cần quyền `products:edit` để thấy nút **Đặt giá**. Nếu quầy POS của một sản phẩm đặt giá từ trước vẫn hiện giá cũ, quản trị viên gọi `POST /prd/api/v1/pricing/republish` một lần để gửi lại giá hiện tại cho mọi sản phẩm (gọi lại nhiều lần vẫn an toàn). Mô tả nghiệp vụ: [Pricing.md](../business/Product/Pricing.md).

## 8. Đơn hàng và báo giá luôn dùng giá hệ thống
Từ 2026-11-04, **máy chủ tự tính giá** mọi dòng của đơn hàng, báo giá và giao dịch POS: không còn ai gõ được giá tùy ý.
- Trang **Đơn hàng mới** và **Báo giá mới** (Admin): ô **Đơn giá** điền sẵn giá hệ thống và **bị khóa** ("Giá lấy từ bảng giá của hệ thống, không sửa được ở đây."). Sản phẩm chưa có giá hiện "Sản phẩm này chưa có giá bán: hãy đặt giá ở Sản phẩm > Biến thể trước." và không lưu được.
- Người có quyền **`sales_order:price_override`** (Cài đặt > Vai trò) sửa được ô giá; giá nhập tay thay giá hệ thống và được ghi vào ghi chú nội bộ của đơn ("Manual price for …"). Quyền xét theo danh sách quyền, **không theo tên vai trò**: vai trò Admin mà chưa được cấp quyền này vẫn bị khóa. Mặc định chỉ quản trị nền tảng có quyền.
- Báo giá chuyển thành đơn giữ nguyên giá đã báo.
- Lỗi từ máy chủ hiện nguyên văn dưới form (trước đây chỉ ghi "Lưu thất bại").
Mô tả nghiệp vụ: [ServerPricing.md](../business/Sales/ServerPricing.md).

