# Template phiếu PDVT

App dùng `PDVT_template.xlsx`, sheet `pdvt 01`. Muốn đổi mẫu thì sửa file này, hoặc chép một bản vào thư mục `Templates/Pdvt/` cạnh file chạy của app (bản đó được ưu tiên dùng).

## Phần đầu phiếu
Đặt ở ô bất kỳ, có thể viết lẫn với chữ (vd. `Số: {SoPDVT}`):

`{SoPDVT}` `{NgayLap}` `{SoHD}` `{HangMuc}` `{TenDuAn}` `{ChuDauTu}` `{TenCongTrinh}` `{DiaDiem}` `{TenNTC}` `{DaiDienNTC}` `{ChucVuNTC}` `{TenNTP}` `{DaiDienNTP}` `{ChucVuNTP}` `{TenTVGS}` `{DaiDienTVGS}` `{ChucVuTVGS}`

## Dòng vật tư
Điền mã vào **một dòng** của bảng. App nhân dòng đó lên thành một dòng cho mỗi vật tư, giữ nguyên định dạng, ô gộp và công thức trong dòng. Các dòng phía dưới (ký tên, tổng…) được đẩy xuống tương ứng.

| Mã | Theo hợp đồng | Mã | Đệ trình |
|---|---|---|---|
| `{NoiDungHD}` | Nội dung | `{NoiDung}` | Nội dung |
| `{ModelThongSoHD}` | Model + thông số (2 dòng) | `{ModelThongSo}` | Model + thông số |
| `{ModelHD}` | Model | `{Model}` | Model |
| `{ThongSoHD}` | Thông số kỹ thuật | `{ThongSo}` | Thông số kỹ thuật |
| `{NhanHieuHD}` | Nhãn hiệu | `{NhanHieu}` | Nhãn hiệu |
| `{XuatXuHD}` | Xuất xứ | `{XuatXu}` | Xuất xứ |

Mã dùng chung: `{STT}` `{DVT}` `{KhoiLuong}` `{DonGia}` `{ThanhTien}` `{GhiChu}` `{KetQua}` (kết quả duyệt) `{YKienTVGS}`.

- Một ô có thể ghép nhiều mã, vd. `{NhanHieu} ({XuatXu})`.
- Ô chỉ chứa đúng một mã số (`{STT}`, `{KhoiLuong}`, `{DonGia}`, `{ThanhTien}`) được ghi dưới dạng số, nên dùng được trong công thức.
- Công thức tổng phía dưới tham chiếu riêng dòng mẫu, vd. `=SUM(G15:G15)`, sẽ tự giãn ra theo toàn bộ danh sách vật tư.
- Template không có mã nào thì app ghi theo bố cục cũ: dòng 15, cột A–J.
