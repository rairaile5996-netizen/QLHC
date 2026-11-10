using DocumentManagement.Models;
using DocumentManagement.Utilities;

namespace DocumentManagement.Tests.Properties;

public class PlaceholderRegistryProperties
{
    [Fact]
    public void BuildCvxd_includes_reflected_model_placeholders_and_custom_values()
    {
        var doc = new Document
        {
            ContractNumber = "01/HD",
            ProjectName = "Project from document",
            SignDate = new DateTime(2026, 5, 13)
        };
        var info = new CvxdInfo
        {
            TenDuAn = "Du an A",
            TenCongTrinh = "Cong trinh A",
            NgayKyHD = "12/05/2026",
            KyHieuNhaThau = "KH-MEP",
            KyHieuChuDauTu = "OLP",
            KyHieuTVGS = "TVGS-01",
            CustomPlaceholders = [new CvxdCustomPlaceholder { Key = "{TenRutGon}", Value = "TenNt", Scope = "CVXD" }]
        };
        var item = new CvxdItem { TenNt = "Lap ong", NgayNt = "13/05/2026" };

        var result = PlaceholderRegistry.BuildCvxd(doc, info, new CvxdGlobalConfig(), item);

        Assert.Equal("Du an A", result["{TenDuAn}"]);
        Assert.Equal("01/HD", result["{ContractNumber}"]);
        Assert.Equal("LAP ONG", result["{NoiDung}"]);
        Assert.Equal("Lap ong", result["{TenRutGon}"]);
        Assert.Equal("KH-MEP", result["{KY_HIEU_NT}"]);
        Assert.Equal("OLP", result["{KY_HIEU_GT}"]);
        Assert.Equal("TVGS-01", result["{KY_HIEU_TVGS}"]);
    }

    [Fact]
    public void BuildVtdv_includes_session_and_material_aliases()
    {
        var doc = new Document { ContractNumber = "02/HD", SignDate = new DateTime(2026, 5, 13) };
        var info = new CvxdInfo { TenDuAn = "Du an B", SoHopDongDauTu = "PDVT-01", DiaDiem = "Da Nang", KyHieuNhaThau = "KH-MEP", KyHieuChuDauTu = "OLP", KyHieuTVGS = "TVGS-01" };
        var session = new VtdvSession
        {
            SoBienBan = "03/VT-02",
            NgayNghiệmThu = "13/05/2026",
            DoiTuongNghiệmThu = "Vat tu dau vao",
            BatDau = "08h00"
        };
        var material = new VtdvItem { DoiTuong = "Ong dong", DonVi = "m", SoLuong = "10" };

        var config = new CvxdGlobalConfig
        {
            GlobalPlaceholders = [new CvxdCustomPlaceholder { Key = "{ChuoiCoDinh}", Value = "ABC", Scope = "VTĐV" }]
        };

        var sessionResult = PlaceholderRegistry.BuildVtdv(doc, info, config, session);
        var itemResult = PlaceholderRegistry.BuildVtdvItem(material, info, session, 4);

        Assert.Equal("03", sessionResult["{SoBB}"]);
        Assert.Equal("Vat tu dau vao", sessionResult["{DoiTuongNT}"]);
        Assert.Equal("08h00", sessionResult["{Gio_NT}"]);
        Assert.Equal("ABC", sessionResult["{ChuoiCoDinh}"]);
        Assert.Equal("KH-MEP", sessionResult["{KY_HIEU_NT}"]);
        Assert.Equal("OLP", sessionResult["{KY_HIEU_GT}"]);
        Assert.Equal("TVGS-01", sessionResult["{KY_HIEU_TVGS}"]);
        Assert.Equal("Ong dong", itemResult["{TenVT}"]);
        Assert.Equal("10,00", itemResult["{SoLuong}"]);
        Assert.Equal("Da Nang", itemResult["{DiaDiem}"]);
        Assert.Equal("4", itemResult["{STT}"]);
    }
}
