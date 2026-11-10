using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentManagement.Services;
using DocumentManagement.Models;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace DocumentManagement.Tests;

public sealed class PdvtServiceTests
{
    [Fact]
    public void ExportExcelUsesPdvtSampleAndKeepsContractAndSubmittedValuesSeparate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pdvt-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var document = new Document { FolderPath = root, ContractNumber = "304/2026/HĐ/OCC-KHC", ProjectName = "Dự án thử" };
            var record = new PdvtRecord { Number = "PDVT 01", HạngMục = "Cáp điện", Materials = [
                new PdvtMaterialRow { NoiDungTheoHd = "Dây điện", NoiDung = "Dây điện", NhanHieuTheoHd = "Cadisun/Cadivi", XuatXuTheoHd = "Việt Nam", NhanHieuPheDuyet = "Cadisun", XuatXuDeTrinh = "Việt Nam" },
                new PdvtMaterialRow { NoiDungTheoHd = "Ống thép", NoiDung = "Ống thép", NhanHieuTheoHd = "Hòa Phát/Seah", NhanHieuPheDuyet = "Hòa Phát" }
            ] };
            var info = new CvxdInfo { DaiDienNhaThau = "Người ký thử", ChucVuNT = "Chỉ huy trưởng" };
            var path = new PdvtService().ExportExcel(record, document, info);
            using var workbook = new XLWorkbook(path);
            var sheet = Assert.Single(workbook.Worksheets);
            Assert.Equal("PDVT 01", sheet.Name);
            Assert.Equal("Cadisun/Cadivi", sheet.Cell(15, 4).GetString());
            Assert.Equal("Cadisun", sheet.Cell(15, 8).GetString());
            Assert.Equal("Việt Nam", sheet.Cell(15, 9).GetString());
            Assert.Equal("Ống thép", sheet.Cell(16, 2).GetString());
            Assert.Contains("Phê duyệt liên quan", sheet.Cell(17, 1).GetString());
            Assert.Equal("Dự án thử", sheet.Cell("G1").GetString());
            Assert.Equal("Kính gửi: ", sheet.Cell("A8").GetString());
            Assert.DoesNotContain(sheet.CellsUsed(), cell => cell.GetString().Contains("Hoa Sen") || cell.GetString().Contains("Phạm Phương Vũ"));
            Assert.Contains("Người ký thử", sheet.Cell("F7").GetString());
            Assert.Contains("Người ký thử", sheet.Cell(27, 5).GetString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExportSummaryWritesOneRowPerBoqLineWithApprovalState()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pdvt-summary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var heading = new PdvtBoqItem { SttBoq = "I", IsHeading = true, HeadingLevel = 1, NoiDung = "Hệ thống điện" };
            var cable = new PdvtBoqItem { SttBoq = "1", SectionCode = "I", NoiDung = "Cáp điện", NhanHieuTheoHd = "Cadivi", KhoiLuong = 1250 };
            var socket = new PdvtBoqItem { SttBoq = "2", SectionCode = "I", NoiDung = "Ổ cắm" };
            var sheet = new PdvtRecord { Number = "PDVT 01", SubmittedDate = new DateTime(2026, 11, 5), Materials =
                [new PdvtMaterialRow { BoqItemId = cable.Id, Status = PdvtApprovalStatus.DaDuyet, NhanHieuPheDuyet = "Cadivi", XuatXuDeTrinh = "Việt Nam", ApprovedDate = new DateTime(2026, 11, 8) }] };
            var items = new List<PdvtBoqItem> { heading, cable, socket };
            PdvtProgress.Apply(items, [sheet]);

            var path = new PdvtService().ExportSummary(items, new Document { FolderPath = root, ContractNumber = "HĐ-01" });
            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheet(1);
            Assert.Equal("1/2 đã duyệt", ws.Cell(5, 7).GetString());
            Assert.Equal("Cáp điện", ws.Cell(6, 2).GetString());
            Assert.Equal("Đã duyệt", ws.Cell(6, 7).GetString());
            Assert.Equal("Cadivi", ws.Cell(6, 8).GetString());
            Assert.Equal("08/11/2026", ws.Cell(6, 11).GetString());
            Assert.Equal("Chưa lập", ws.Cell(7, 7).GetString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SplitBrandOriginKeepsContractAlternativesSeparateFromOrigin()
    {
        var result = PdvtService.SplitBrandOrigin("Cadisun/Cadivi/Việt Nam", "");
        Assert.Equal(["Cadisun", "Cadivi"], result.Options);
        Assert.Equal("Việt Nam", result.Origin);
        Assert.Equal(["Cadisun", "Cadivi"], PdvtService.ParseOptions("Cadisun/Cadivi"));
    }

    [Fact]
    public void ReadBoqMapsReorderedColumnsAndCommonAlternativeHeaders()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-reordered-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Vật tư HĐ");
                sheet.Cell(4, 1).Value = "SL";
                sheet.Cell(4, 2).Value = "Thương hiệu";
                sheet.Cell(4, 3).Value = "Quy cách kỹ thuật";
                sheet.Cell(4, 4).Value = "ĐVT";
                sheet.Cell(4, 5).Value = "Tên vật liệu";
                sheet.Cell(4, 6).Value = "Nước xuất xứ";
                sheet.Cell(5, 1).Value = 10;
                sheet.Cell(5, 2).Value = "Hãng A";
                sheet.Cell(5, 3).Value = "D25";
                sheet.Cell(5, 4).Value = "m";
                sheet.Cell(5, 5).Value = "Ống thép";
                sheet.Cell(5, 6).Value = "Việt Nam";
                workbook.SaveAs(path);
            }
            var item = Assert.Single(PdvtService.ReadBoq(path));
            Assert.Equal("Ống thép", item.NoiDung);
            Assert.Equal("D25", item.ThongSoKyThuat);
            Assert.Equal("Hãng A / Việt Nam", item.NhanHieuXuatXu);
            Assert.Equal(10m, item.KhoiLuong);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ReadBoqPreservesSectionRowsInOriginalOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-sections-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("boq");
                sheet.Cell(1, 1).Value = "STT";
                sheet.Cell(1, 2).Value = "Tên công việc";
                sheet.Cell(1, 3).Value = "Đơn vị";
                sheet.Cell(1, 4).Value = "Khối lượng";
                sheet.Cell(2, 1).Value = "I"; sheet.Cell(2, 2).Value = "Hệ thống điện";
                sheet.Cell(3, 1).Value = "I.1"; sheet.Cell(3, 2).Value = "Dây cáp điện";
                sheet.Cell(4, 1).Value = 1; sheet.Cell(4, 2).Value = "Cáp điện"; sheet.Cell(4, 3).Value = "m"; sheet.Cell(4, 4).Value = 20;
                sheet.Cell(5, 1).Value = "II"; sheet.Cell(5, 2).Value = "Ống điện";
                sheet.Cell(6, 1).Value = 1; sheet.Cell(6, 2).Value = "Ống SP"; sheet.Cell(6, 3).Value = "m"; sheet.Cell(6, 4).Value = 10;
                workbook.SaveAs(path);
            }
            var items = PdvtService.ReadBoq(path);
            Assert.Equal(["Hệ thống điện", "Dây cáp điện", "Cáp điện", "Ống điện", "Ống SP"], items.Select(item => item.NoiDung));
            Assert.Equal([true, true, false, true, false], items.Select(item => item.IsHeading));
            Assert.Equal("I.1", items[2].SectionCode);
            Assert.Equal("II", items[4].SectionCode);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ReadBoqIgnoresMalformedExternalRelationshipInWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-malformed-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("boq");
                sheet.Cell(1, 1).Value = "Tên công việc";
                sheet.Cell(1, 2).Value = "Đơn vị";
                sheet.Cell(1, 3).Value = "Khối lượng";
                sheet.Cell(1, 4).Value = "Nhãn hiệu";
                sheet.Cell(1, 5).Value = "Xuất xứ";
                sheet.Cell(2, 1).Value = "Van chặn lửa";
                sheet.Cell(2, 2).Value = "cái";
                sheet.Cell(2, 3).Value = 2;
                sheet.Cell(2, 4).Value = "Hãng A; Hãng B";
                sheet.Cell(2, 5).Value = "Việt Nam";
                workbook.SaveAs(path);
            }
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry("xl/_rels/workbook.xml.rels")!;
                XDocument document;
                using (var stream = entry.Open()) document = XDocument.Load(stream);
                var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
                document.Root!.Add(new XElement(ns + "Relationship",
                    new XAttribute("Id", "rIdMalformedExternal"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                    new XAttribute("Target", "file:///\\\\https:\\invalid.example\\bad file.xlsx"),
                    new XAttribute("TargetMode", "External")));
                entry.Delete();
                using var output = archive.CreateEntry("xl/_rels/workbook.xml.rels").Open();
                document.Save(output);
            }

            var item = Assert.Single(PdvtService.ReadBoq(path));
            Assert.Equal("Van chặn lửa", item.NoiDung);
            Assert.Equal(["Hãng A", "Hãng B"], item.NhanHieuOptions);
            Assert.Equal("Việt Nam", item.XuatXuTheoHd);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void NextNumberCountsLegacyNewAndVersionedReceipts()
    {
        var records = new[] {
            new PdvtRecord { Number = "PDVT-001" },
            new PdvtRecord { Number = "PDVT 02" },
            new PdvtRecord { Number = "PDVT 02_v2", Version = 2 }
        };
        Assert.Equal(3, PdvtService.NextNumber(records));
    }

    [Fact]
    public void BoqIsStoredSeparatelyForEachContract()
    {
        var service = new PdvtService();
        var firstId = -Random.Shared.Next(100_000, 1_000_000);
        var secondId = firstId - 1;
        try
        {
            service.SaveBoq(firstId, [new PdvtBoqItem { Id = "row-1", NoiDung = "Ống đồng" }]);
            service.SaveBoq(secondId, [new PdvtBoqItem { Id = "row-2", NoiDung = "Ống gió" }]);
            Assert.Equal("row-1", Assert.Single(service.LoadBoq(firstId)).Id);
            Assert.Equal("Ống gió", Assert.Single(service.LoadBoq(secondId)).NoiDung);
        }
        finally
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "PDVT");
            File.Delete(Path.Combine(folder, $"{firstId}.boq.json"));
            File.Delete(Path.Combine(folder, $"{secondId}.boq.json"));
        }
    }

    [Fact]
    public void ReadBoqAcceptsRepeatedAndBlankHeadersAndUsesFirstNonEmptyMatchingColumn()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-boq-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("BoQ");
                sheet.Cell(1, 1).Value = "Nội dung công việc";
                sheet.Cell(1, 2).Value = "Noi dung cong viec"; // Same normalized header.
                sheet.Cell(1, 3).Value = "Thông số kỹ thuật";
                sheet.Cell(1, 4).Value = "Nhãn hiệu - xuất xứ";
                sheet.Cell(1, 5).Value = "Đơn vị tính";
                sheet.Cell(1, 6).Value = "Đơn giá";
                sheet.Cell(1, 7).Value = "Khối lượng";
                sheet.Cell(1, 8).Value = " "; // Blank headings must not become dictionary keys.
                sheet.Cell(2, 1).Value = " ";
                sheet.Cell(2, 2).Value = "Máy bơm cấp nước";
                sheet.Cell(2, 3).Value = "Q=10 m3/h";
                sheet.Cell(2, 4).Value = "Hãng A; Hãng B";
                sheet.Cell(2, 5).Value = "Cái";
                sheet.Cell(2, 6).Value = 1250000;
                sheet.Cell(2, 7).Value = 2;
                workbook.SaveAs(path);
            }

            var items = PdvtService.ReadBoq(path);

            var item = Assert.Single(items);
            Assert.Equal("Máy bơm cấp nước", item.NoiDung);
            Assert.Equal("Q=10 m3/h", item.ThongSoKyThuat);
            Assert.Equal(["Hãng A", "Hãng B"], item.NhanHieuOptions);
            Assert.Equal("Cái", item.DonViTinh);
            Assert.Equal(1250000m, item.DonGia);
            Assert.Equal(2m, item.KhoiLuong);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ReadBoqFindsHeaderBelowTitleAndOnAnotherWorksheet()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-boq-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var cover = workbook.AddWorksheet("Bìa");
                cover.Cell(1, 1).Value = "BẢNG TỔNG HỢP GIÁ TRỊ HỢP ĐỒNG";
                var boq = workbook.AddWorksheet("Chi tiết vật tư");
                boq.Cell(1, 1).Value = "Dự án: Công trình mẫu";
                boq.Cell(3, 1).Value = "STT";
                boq.Cell(3, 2).Value = "Tên hàng hóa, thiết bị";
                boq.Cell(3, 3).Value = "Thông số kỹ thuật";
                boq.Cell(3, 4).Value = "Nhãn hiệu / Xuất xứ";
                boq.Cell(3, 5).Value = "ĐVT";
                boq.Cell(3, 6).Value = "Đơn giá";
                boq.Cell(3, 7).Value = "Số lượng";
                boq.Cell(4, 1).Value = 1;
                boq.Cell(4, 2).Value = "Tủ điện phân phối";
                boq.Cell(4, 3).Value = "IP54";
                boq.Cell(4, 4).Value = "Hãng X";
                boq.Cell(4, 5).Value = "Bộ";
                boq.Cell(4, 6).Value = 5000000;
                boq.Cell(4, 7).Value = 1;
                workbook.SaveAs(path);
            }

            var item = Assert.Single(PdvtService.ReadBoq(path));
            Assert.Equal("Tủ điện phân phối", item.NoiDung);
            Assert.Equal("IP54", item.ThongSoKyThuat);
            Assert.Equal("Hãng X", item.NhanHieuXuatXu);
            Assert.Equal("Bộ", item.DonViTinh);
            Assert.Equal(1m, item.KhoiLuong);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ReadBoqUsesCachedValueForFormulaLinkedToAnotherWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdvt-boq-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("BoQ");
                sheet.Cell(1, 1).Value = "Nội dung công việc";
                sheet.Cell(1, 2).Value = "Đơn vị tính";
                sheet.Cell(2, 1).Value = "Tên tạm";
                sheet.Cell(2, 2).Value = "Bộ";
                workbook.SaveAs(path);
            }

            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var cell = document.WorkbookPart!.WorksheetParts.Single().Worksheet
                    .Descendants<Spreadsheet.Cell>().Single(x => x.CellReference == "A2");
                cell.CellFormula = new Spreadsheet.CellFormula("'[Nguon.xlsx]VatTu'!A2");
                cell.CellValue = new Spreadsheet.CellValue("Máy bơm đã liên kết");
                cell.DataType = Spreadsheet.CellValues.String;
                document.WorkbookPart.WorksheetParts.Single().Worksheet.Save();
            }

            var item = Assert.Single(PdvtService.ReadBoq(path));
            Assert.Equal("Máy bơm đã liên kết", item.NoiDung);
            Assert.Equal("Bộ", item.DonViTinh);
            Assert.True(File.Exists(path)); // Import preprocessing must leave the selected workbook untouched.
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
