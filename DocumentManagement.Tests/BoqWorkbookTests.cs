using System.IO;
using ClosedXML.Excel;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.Tests;

/// <summary>BoQ layouts seen on real contracts: columns vary in order, naming and grouping.</summary>
public sealed class BoqWorkbookTests
{
    private static string Save(Action<IXLWorksheet> fill, string sheetName = "BoQ")
    {
        var path = Path.Combine(Path.GetTempPath(), $"boq-layout-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Bìa").Cell(1, 1).Value = "Trang bìa";
        fill(workbook.AddWorksheet(sheetName));
        workbook.SaveAs(path);
        return path;
    }

    /// <summary>Layout 1: Stt | Nội dung | ĐVT | KL | "Nhãn Hiệu /Xuất xứ" | Thông số | Đơn giá (2-row header).</summary>
    private static string CombinedBrandLayout() => Save(sheet =>
    {
        sheet.Cell(2, 1).Value = "BẢNG 1 : CHI TIẾT CÔNG VIỆC";
        string[] top = ["Stt", "Nội dung công việc", "Đơn vị tính", "Khối lượng", "Nhãn Hiệu /Xuất xứ", "Thông số kỹ thuật", "Đơn giá (VNĐ)", "", "Thành tiền (VNĐ)"];
        for (var i = 0; i < top.Length; i++) sheet.Cell(5, i + 1).Value = top[i];
        sheet.Range(5, 7, 5, 8).Merge();
        sheet.Cell(6, 7).Value = "Thiết bị & Vật tư";
        sheet.Cell(6, 8).Value = "Nhân công & Máy thi công";
        object[][] rows =
        [
            ["A", "ĐIỆN"],
            ["I", "Tủ điện"],
            [1, "Tủ điện điều khiển bơm", "Tủ", 1, "Schneider/VN", "", 1500000],
            ["C", "ĐIỀU HÒA KHÔNG KHÍ & THÔNG GIÓ"],
            ["I", "Ống điện và Dây điều khiển"],
            ["I.1", "Ống điện"],
            [1, "Ống điện SP D20 đi nổi", "Mét", 10, "SP/VN", "", 11865],
            [2, "Phụ kiện, hộp chia ngã", "Lô", 1, "VN/ Không COCQ", "", 44962],
            ["I.2", "Cáp điện"],
            [1, "Cáp điện CV 2.5mm2", "Mét", 30, "Cadisun/cadivi", "", 9821],
            ["II", "Thiết bị"],
            [1, "Quạt hút âm trần", "Cái", 4, "Panasonic/Thái Lan", "Lưu lượng 300m3/h", 850000]
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(7 + r, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
    });

    /// <summary>Layout 2: Stt | Tên công việc | THÔNG SỐ (holds model codes) | Mã hiệu (holds brands) | Nhãn hiệu | Xuất xứ | Đơn vị | KL.</summary>
    private static string SplitBrandLayout() => Save(sheet =>
    {
        string[] header = ["Stt", "Tên công việc", "THÔNG SỐ KỸ THUẬT", "Mã hiệu", "Nhãn hiệu", "Xuất xứ", "Đơn vị", "Khối lượng", "Đơn giá"];
        for (var i = 0; i < header.Length; i++) sheet.Cell(4, i + 2).Value = header[i]; // starts at column B
        object[][] rows =
        [
            ["**", "KHỐI LƯỢNG CHO TẦNG 1 VÀ 2"],
            ["A", "HỆ THỐNG ĐIỆN"],
            ["I", "TỦ ĐIỆN"],
            [1, "TĐ-T", "", "VN", "Pentatech", "Việt Nam", "Tủ", 1, "402.301.200"],
            ["", "MCCB 3P 1000A 50kA", "C100N320FM", "Schneider/ Asia", "", "", "Cái", 1],
            ["", "Đèn báo pha đỏ", "", "Ilec", "", "", "Cái", 1],
            ["", "Đồng hồ đo đa chức năng", "MFM383-A", "SELEC", "", "", "Cái", "1,00"]
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(5 + r, c + 2).Value = XLCellValue.FromObject(rows[r][c]);
    }, "Chi tiet");

    [Fact]
    public void DetectsCombinedBrandColumnTwoRowHeaderAndNestedSections()
    {
        var path = CombinedBrandLayout();
        try
        {
            using var workbook = BoqWorkbook.Open(path);
            var map = workbook.Detect();
            Assert.Equal("BoQ", map.SheetName);
            Assert.Equal(5, map.HeaderRow);
            Assert.Equal(1, map.Get(BoqField.Stt));
            Assert.Equal(5, map.Get(BoqField.NhanHieu));
            Assert.Equal(6, map.Get(BoqField.ThongSo));
            Assert.Equal(7, map.Get(BoqField.DonGia)); // "Đơn giá (VNĐ)" → material price column
            Assert.Equal(0, map.Get(BoqField.XuatXu));

            var items = workbook.Read(map);
            var pipe = items.Single(x => x.NoiDung == "Ống điện SP D20 đi nổi");
            Assert.Equal("C>I>I.1", pipe.SectionCode);
            Assert.Equal("SP", pipe.NhanHieuTheoHd.Split('/')[0]);
            Assert.Equal(10m, pipe.KhoiLuong);
            Assert.Equal("Thái Lan", items.Single(x => x.NoiDung == "Quạt hút âm trần").XuatXuTheoHd);

            // "I" under A and "I" under C are different sections.
            var electricalI = items.First(x => x.IsHeading && x.Path == "A>I");
            var hvacI = items.First(x => x.IsHeading && x.Path == "C>I");
            var hvac = items.First(x => x.IsHeading && x.Path == "C");
            Assert.Equal(3, items.Count(x => !x.IsHeading && PdvtProgress.IsInSection(x, hvacI)));
            Assert.Equal(1, items.Count(x => !x.IsHeading && PdvtProgress.IsInSection(x, electricalI)));
            Assert.Equal(4, items.Count(x => !x.IsHeading && PdvtProgress.IsInSection(x, hvac)));
            Assert.Equal(3, items.Single(x => x.Path == "C>I>I.1").HeadingLevel);
            Assert.Equal("C>II", items.Single(x => x.NoiDung == "Thiết bị").Path); // II closes I and I.x
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ManualMappingFixesMislabelledColumnsAndKeepsComponentRows()
    {
        var path = SplitBrandLayout();
        try
        {
            using var workbook = BoqWorkbook.Open(path);
            var detected = workbook.Detect();
            Assert.Equal("Chi tiet", detected.SheetName);
            Assert.Equal(2, detected.Get(BoqField.Stt));
            Assert.Equal(5, detected.Get(BoqField.Model));    // header says "Mã hiệu"…
            Assert.Equal(6, detected.Get(BoqField.NhanHieu));
            Assert.Equal(7, detected.Get(BoqField.XuatXu));

            // …but the values are brands, and "THÔNG SỐ" holds model codes: the user swaps them in the dialog.
            var map = detected;
            map.Set(BoqField.NhanHieu, 5);
            map.Set(BoqField.Model, 4);
            map.Set(BoqField.ThongSo, 0);
            var items = workbook.Read(map);

            var mccb = items.Single(x => x.NoiDung == "MCCB 3P 1000A 50kA");
            Assert.Equal("C100N320FM", mccb.Model);
            Assert.Equal(["Schneider", "Asia"], mccb.NhanHieuOptions);
            Assert.Equal("**>A>I", mccb.SectionCode);
            Assert.Equal(1m, items.Single(x => x.NoiDung.StartsWith("Đồng hồ")).KhoiLuong);
            Assert.Equal(402301200m, items.Single(x => x.NoiDung == "TĐ-T").DonGia);
            Assert.Equal(4, items.Count(x => !x.IsHeading));
            Assert.Equal("MFM383-A", items.Single(x => x.NoiDung.StartsWith("Đồng hồ")).SpecWithModel);

            var columns = workbook.DescribeColumns(map.SheetName, map.HeaderRow);
            Assert.Contains(columns, c => c.Letter == "E" && c.Header == "Mã hiệu" && c.Sample.Contains("Schneider"));
            Assert.Equal(workbook.Signature(map.SheetName, map.HeaderRow), workbook.Signature("Chi tiet", 4));

            // Brand split over two columns: "Nhãn hiệu" for the cabinet, "Mã hiệu" for its components.
            var split = workbook.Detect();
            split.Set(BoqField.Model, 4);
            split.Set(BoqField.ThongSo, 0);
            split.SetFallback(BoqField.NhanHieu, 5);
            var mixed = workbook.Read(split);
            Assert.Equal("Pentatech", mixed.Single(x => x.NoiDung == "TĐ-T").NhanHieuTheoHd);
            Assert.Equal("Ilec", mixed.Single(x => x.NoiDung == "Đèn báo pha đỏ").NhanHieuTheoHd);

            var layout = split.ToLayout();
            var restored = workbook.Detect();
            restored.Apply(layout);
            Assert.Equal(5, restored.GetFallback(BoqField.NhanHieu));
            Assert.Equal(4, restored.Get(BoqField.Model));
            Assert.Equal(0, restored.Get(BoqField.ThongSo));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("1.250,50", 1250.5)]
    [InlineData("1,250.50", 1250.5)]
    [InlineData("10,00", 10)]
    [InlineData("402.301.200", 402301200)]
    [InlineData("1.250", 1250)]
    [InlineData("2,5", 2.5)]
    [InlineData("-", 0)]
    public void ParsesVietnameseAndEnglishNumbers(string text, double expected) =>
        Assert.Equal((decimal)expected, BoqWorkbook.ParseNumber(text));

    [Theory]
    [InlineData("A", "letter")]
    [InlineData("C", "letter")]
    [InlineData("I", "roman")]
    [InlineData("IV", "roman")]
    [InlineData("I.1", "dot1")]
    [InlineData("1.2.3", "dot2")]
    [InlineData("12", "number")]
    [InlineData("**", "other")]
    public void ClassifiesHeadingCodes(string stt, string kind) => Assert.Equal(kind, BoqWorkbook.SttKind(stt));
}
