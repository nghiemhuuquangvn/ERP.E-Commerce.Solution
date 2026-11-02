# Hướng dẫn sử dụng: Kế toán — Bút toán, Hóa đơn mua, Hóa đơn bán

Ba màn hình trong nhóm **Kế toán** ghi chứng từ vào sổ cái: **Bút toán** (Journal Entries), **Hóa đơn mua** (Bills) và **Hóa đơn bán** (Invoices). Đóng thêm: thanh báo chứng từ chưa có bút toán và khung **đánh giá lại ngoại tệ** ở trang Kỳ kế toán.

> Ghi chú cho người bảo trì: nhãn trích từ mã nguồn và `vi.json`. Đã duyệt thử trên trình duyệt (tenant `glcheck`) với dữ liệu thử: ghi một bút toán, hủy nó, mở các hộp xác nhận của duyệt hóa đơn mua, thanh toán, hóa đơn điện tử, ghi bút toán thiếu và đánh giá lại tới bước xác nhận (không bấm xác nhận trên dữ liệu thật); dữ liệu thử đã xóa.

**Nguyên tắc chung:** mọi bước ghi sổ hoặc khóa dữ liệu đều **hỏi lại trước khi làm**, nói rõ hậu quả; bấm **Esc** hoặc **Hủy** thì không gửi gì. Ô có dấu **\*** là bắt buộc; để trống thì hiện **"Bắt buộc"** dưới ô. Nếu máy chủ từ chối (kỳ đã đóng, tài khoản không ghi sổ được …) lý do hiện nguyên văn trong hộp thoại hoặc thông báo.

## 1. Bút toán
**Ai dùng được:** quyền xem bút toán thấy danh sách; quyền quản lý bút toán (`accounting:journals:manage`) thấy các nút ghi, đảo, hủy.
- **Lọc theo kỳ:** ô **Kỳ kế toán** phía trên bảng. Nút **Xem** hiện các dòng của bút toán.
- **Bút toán mới:** nhập **Số bút toán**, chọn **Kỳ kế toán**, **Diễn giải**, rồi các dòng (tài khoản, **Nợ** hoặc **Có**, diễn giải dòng; **Thêm dòng** / **Xóa**). Khung màu bên dưới cho biết tổng nợ, tổng có và **"Cân đối" / "Không cân đối … (chênh lệch n)"**. Bấm **Hạch toán**: bút toán được ghi ngay, **không có bản nháp**. Chưa cân mà bấm thì hiện lỗi và không gửi gì.
- Không ghi được vào tài khoản cha hoặc tài khoản ngoài bảng (loại 0): máy chủ báo lý do ngay trong hộp thoại.
- **Đảo** (mọi bút toán đã ghi, trừ bút toán kết chuyển cuối kỳ): nhập lý do và ngày đảo; sổ có thêm một bút toán ngược, bản gốc ở trạng thái "Đã đảo".
- **Hủy** (chỉ bút toán tự nhập, kỳ còn mở): nhập lý do; bút toán ra khỏi sổ, hiện "Đã hủy". Bút toán sinh từ hóa đơn thì chỉ đảo được.

## 2. Hóa đơn mua
**Ai dùng được:** quyền quản lý hóa đơn mua (`accounting:bills:manage`) thấy các nút thêm, duyệt, thanh toán.
- Hai thẻ: **Hóa đơn mua** và **Tuổi nợ phải trả** (mũi tên trái / phải đổi thẻ; địa chỉ trang nhớ thẻ đang mở).
- **Hóa đơn mua mới:** mã hóa đơn, nhà cung cấp, đơn mua, ngày đến hạn; số tiền một lần, hoặc nhiều dòng thuế (thuế suất / không chịu thuế), hoặc điền **Tiền tệ** (3 chữ cái) để nhập số tiền ngoại tệ và tỷ giá (**Gợi ý tỷ giá** lấy từ dữ liệu gốc).
- **Duyệt:** hộp xác nhận nói duyệt sẽ ghi hóa đơn vào sổ như khoản phải trả và không sửa được nữa. Ô **Thuế suất (%) trên hóa đơn của nhà cung cấp** để sẵn thuế suất hiện tại; đổi nếu hóa đơn thật khác, để nguyên thì giữ cách tách thuế lúc tạo.
- **Đánh dấu đã thanh toán:** hộp xác nhận ghi nhận thanh toán và ghi vào sổ. Hóa đơn ngoại tệ có thêm ô **Tỷ giá lúc thanh toán** (bắt buộc, lớn hơn 0).
- Biểu tượng cảnh báo cạnh số hóa đơn: **chưa ghi vào sổ cái** (chưa có kỳ mở hoặc thiếu hồ sơ định khoản).

## 3. Hóa đơn bán
**Ai dùng được:** quyền quản lý hóa đơn bán (`accounting:invoices:manage`).
- Cùng cách dùng như hóa đơn mua: thẻ **Hóa đơn bán** / **Tuổi nợ phải thu**, **Hóa đơn mới**, **Đánh dấu đã thanh toán** (hộp xác nhận ghi nhận khoản thu; hóa đơn ngoại tệ hỏi tỷ giá).
- **Điều chỉnh VAT:** đổi một thuế suất, hoặc **Nhiều dòng thuế** với tổng các dòng bằng đúng số tiền hóa đơn (nút **Lưu** chỉ bật khi đủ). Số tiền phải thu không đổi, chỉ cách tách thuế.
- **Hóa đơn điện tử:** nhập thông tin người mua → **Phát hành hóa đơn điện tử** → xác nhận (hóa đơn được gửi tới nhà cung cấp và thuế VAT của hóa đơn bị khóa). Khi đã phát hành: **Hủy** (bắt buộc lý do), **Thay thế** (lý do, thuế suất mới nếu cần) và **Điều chỉnh** (chênh lệch chưa thuế / VAT, lý do) — mỗi bước hỏi xác nhận riêng. Nút chỉ bật khi đã nhập lý do.

## 4. Chứng từ chưa có bút toán
Khi có hóa đơn mua hoặc bán chưa được ghi vào sổ, phía trên bảng hiện thanh vàng "n chứng từ chưa có bút toán". **Hạch toán các bút toán còn thiếu** hỏi xác nhận (ghi vào kỳ đang mở theo hồ sơ định khoản), rồi báo "Đã ghi hết" hoặc "n chứng từ vẫn chưa ghi được".

## 5. Đánh giá lại ngoại tệ (trang Kỳ kế toán)
Chọn **Kỳ**, nhập tỷ giá cuối kỳ nếu dữ liệu gốc chưa có, xem bảng chênh lệch (phải thu, phải trả, ròng). **Ghi bút toán đánh giá lại** hỏi xác nhận: nói rõ kỳ, chênh lệch ròng, bút toán ghi vào ngày cuối kỳ và **tự đảo vào ngày hôm sau**. Sau khi ghi hiện số bút toán.

## 6. Mẹo
- Mỗi nút trên dòng có tên đầy đủ cho trình đọc màn hình ("Duyệt B-1").
- Số tiền hiển thị theo ngôn ngữ trình duyệt (`1.200` hoặc `1,200`).
- Thiếu quyền thì chỉ thấy danh sách, không có nút thao tác.
