# QLHS — mã nguồn 1.5.6 (PDVT)

Ứng dụng WPF .NET 8 cho quản lý hợp đồng và hồ sơ công trình. Đây là gói mã nguồn để lưu trên GitHub; không chứa EXE, bin/obj, dữ liệu hợp đồng hay dữ liệu người dùng.

## Mở và kiểm tra

- Windows, .NET 8 SDK.
- `dotnet restore DocumentManagement.sln`
- `dotnet test DocumentManagement.Tests/DocumentManagement.Tests.csproj`
- `dotnet build DocumentManagement/DocumentManagement.csproj -c Release`

## PDVT

- BoQ giữ các dòng đầu mục và thứ tự gốc từ Excel; chỉ dòng vật tư được chọn để lập phiếu.
- Giao diện có tab BoQ và tab Phiếu PDVT.
- File Excel PDVT nằm trong `3.HSCL/PDVT` của từng hợp đồng và được tìm trong “Hồ sơ đã tạo”.
- BoQ đã nạp trước thay đổi này cần bấm “Nạp BoQ Excel” lại để lấy các đầu mục.

Mẫu Excel PDVT ở `DocumentManagement/Templates/Pdvt/PDVT_template.xlsx`.
