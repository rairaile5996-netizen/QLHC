using DocumentManagement.Models;

namespace DocumentManagement.Utilities;

public static class DefaultCvxdStandards
{
    public const int SeedVersion = 1;

    public static IReadOnlyList<CvxdStandard> Create()
    {
        return
        [
            new CvxdStandard
            {
                Key = "ĐIỆN",
                Value = "+ QCVN-12-2014/BXD: Quy chuẩn kỹ thuật Quốc gia về hệ thống điện của Nhà ở và công trình công cộng;\n+ QCVN 09:2017/BXD: Quy chuẩn kỹ thuật quốc gia về các công trình xây dựng sử dụng năng lượng hiệu quả;\n+ 11TCN 20-2006, 11TCN 19-2006, 11TCN 21-2006: Quy phạm trang bị điện;\n+ TCVN 9207:2012 Đặt đường dây dẫn điện trong nhà ở nhà công trình công cộng;\n+ TCVN 7114-2008: Chiếu sáng nơi làm việc;\n+ TCVN 13456:2022 Phòng cháy chữa cháy – Phương tiện chiếu sáng sự cố và chỉ dẫn thoát nạn – Yêu cầu thiết kế lắp đặt."
            },
            new CvxdStandard
            {
                Key = "NƯỚC",
                Value = "+ TCVN 4474:1987 - Thoát nước bên trong – Tiêu chuẩn thiết kế\n+ TCVN 7957:2023 - Thoát nước – Mạng lưới và công trình bên ngoài – Yêu cầu thiết kế\n+ TCVN 4513:1988: Cấp nước bên trong - Tiêu chuẩn thiết kế"
            },
            new CvxdStandard
            {
                Key = "CHỮA CHÁY",
                Value = "+ QCVN 06:2022/BXD + Sửa đổi 1:2023 Quy chuẩn kỹ thuật quốc gia về an toàn cháy cho nhà và công trình.\n+ TCVN 5738:2021: Phòng cháy chữa cháy - Hệ thống báo cháy tự động - Yêu cầu kỹ thuật\n+ TCVN 3890:2023: Phòng cháy chữa cháy - Phương tiện phòng cháy chữa cháy cho nhà và công trình - Trang bị, bố trí\n+ TCVN 13456:2022: Phòng cháy chữa cháy - Đèn chiếu sáng sự cố và chỉ dẫn thoát nạn - Yêu cầu thiết kế, lắp đặt"
            },
            new CvxdStandard
            {
                Key = "ĐHKK",
                Value = "+ TCVN 5697:2024 Thông gió và điều hòa không khí – Yêu cầu thiết kế;\n+ TCVN 13581:2023 Thông gió và điều hòa không khí – Yêu cầu lắp đặt đường ống và nghiệm thu hệ thống\n+ TCVN 13580:2023 Thông gió và điều hòa không khí – Yêu cầu chế tạo đường ống;\n+ QCVN 06:2022/BXD Quy chuẩn kỹ thuật quốc gia về an toàn cháy cho nhà và công trình;"
            }
        ];
    }
}
