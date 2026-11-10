using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.Tests;

public sealed class PdvtProgressTests
{
    private static PdvtRecord Sheet(string number, DateTime? submitted, int version = 1, params PdvtMaterialRow[] rows) =>
        new() { Number = number, Version = version, Date = new DateTime(2026, 10, 1), SubmittedDate = submitted, Materials = rows.ToList() };

    private static PdvtMaterialRow Row(PdvtBoqItem item, PdvtApprovalStatus status, string brand = "", string note = "") =>
        new() { BoqItemId = item.Id, NoiDung = item.NoiDung, Status = status, NhanHieuPheDuyet = brand, ReviewNote = note };

    [Fact]
    public void RecordStatusFollowsSubmissionAndRowResults()
    {
        var item = new PdvtBoqItem { NoiDung = "Cáp" };
        Assert.Equal(PdvtApprovalStatus.ChuaTrinh, Sheet("PDVT 01", null, 1, Row(item, PdvtApprovalStatus.ChuaTrinh)).DerivedStatus);
        var submitted = new DateTime(2026, 11, 5);
        Assert.Equal(PdvtApprovalStatus.DaTrinh, Sheet("PDVT 01", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.DaTrinh)).DerivedStatus);
        Assert.Equal(PdvtApprovalStatus.DaDuyet, Sheet("PDVT 01", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet)).DerivedStatus);
        Assert.Equal(PdvtApprovalStatus.YeuCauSua, Sheet("PDVT 01", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.KhongDuyet)).DerivedStatus);
        Assert.Equal(PdvtApprovalStatus.KhongDuyet, Sheet("PDVT 01", submitted, 1, Row(item, PdvtApprovalStatus.KhongDuyet)).DerivedStatus);
    }

    [Fact]
    public void ProgressTextSummarisesTheSheet()
    {
        var item = new PdvtBoqItem();
        var submitted = new DateTime(2026, 11, 5);
        Assert.Equal("Tạo 01/10/2026", Sheet("A", null, 1, Row(item, PdvtApprovalStatus.ChuaTrinh)).ProgressText);
        Assert.Equal("Chờ kết quả · trình 05/11/2026", Sheet("A", submitted, 1, Row(item, PdvtApprovalStatus.DaTrinh)).ProgressText);
        Assert.Equal("1/2 duyệt · trình 05/11/2026", Sheet("A", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.DaTrinh)).ProgressText);
        Assert.Equal("1/2 duyệt · 1 cần trình lại", Sheet("A", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.YeuCauSua)).ProgressText);
        Assert.Equal("2/2 duyệt", Sheet("A", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.DaDuyet)).ProgressText);
        var resubmitted = Sheet("A", submitted, 1, Row(item, PdvtApprovalStatus.DaDuyet), Row(item, PdvtApprovalStatus.KhongDuyet));
        resubmitted.NextRoundNumber = "A_v2";
        Assert.Equal("1/2 duyệt · đã trình lại ở A_v2", resubmitted.ProgressText);
    }

    [Theory]
    [InlineData("Cadisun / Cadivi", "Cadivi", false)]
    [InlineData("Schneider", "schneider ", false)]
    [InlineData("Schneider", "LS Electric", true)]
    [InlineData("", "LS Electric", false)]
    [InlineData("Panasonic", "", false)]
    public void IsDifferentAcceptsContractAlternatives(string contract, string submitted, bool expected) =>
        Assert.Equal(expected, PdvtProgress.IsDifferent(contract, submitted, []));

    [Fact]
    public void MaterialRowFlagsSubmittedBrandOutsideContractOptions()
    {
        var row = new PdvtMaterialRow { NhanHieuTheoHd = "Hòa Phát/Seah", NhanHieuOptions = ["Hòa Phát", "Seah"], NhanHieuPheDuyet = "Seah" };
        Assert.False(row.IsDiff);
        row.NhanHieuPheDuyet = "Vinapipe";
        Assert.True(row.IsBrandDiff);
        Assert.False(new PdvtMaterialRow { Source = "Phát sinh", NhanHieuTheoHd = "A", NhanHieuPheDuyet = "B" }.IsDiff);
    }

    [Fact]
    public void ApplyComputesBoqProgressAcrossRounds()
    {
        var heading = new PdvtBoqItem { SttBoq = "I", IsHeading = true, HeadingLevel = 1, NoiDung = "Hệ thống điện" };
        var approved = new PdvtBoqItem { SttBoq = "1", SectionCode = "I", NoiDung = "Cáp điện" };
        var rework = new PdvtBoqItem { SttBoq = "2", SectionCode = "I", NoiDung = "Tủ điện" };
        var waiting = new PdvtBoqItem { SttBoq = "3", SectionCode = "I", NoiDung = "Đèn LED" };
        var untouched = new PdvtBoqItem { SttBoq = "4", SectionCode = "I", NoiDung = "Ổ cắm" };
        var boq = new List<PdvtBoqItem> { heading, approved, rework, waiting, untouched };

        var round1 = Sheet("PDVT 05", new DateTime(2026, 10, 20), 1,
            Row(approved, PdvtApprovalStatus.DaDuyet, "Cadivi"),
            Row(rework, PdvtApprovalStatus.YeuCauSua, "Hưng Phát", "Bổ sung catalogue"),
            Row(waiting, PdvtApprovalStatus.DaTrinh));
        var records = new List<PdvtRecord> { round1 };
        PdvtProgress.Apply(boq, records);

        Assert.Equal(PdvtBoqProgress.DaDuyet, approved.Progress);
        Assert.Equal("Cadivi", approved.ApprovedBrand);
        Assert.Equal("PDVT 05 · L1", approved.LatestReference);
        Assert.Equal(PdvtBoqProgress.CanTrinhLai, rework.Progress);
        Assert.Equal("Yêu cầu sửa", rework.StatusLabel);
        Assert.Equal(PdvtBoqProgress.ChoKetQua, waiting.Progress);
        Assert.Equal("Chờ kết quả", waiting.StatusLabel);
        Assert.Equal(PdvtBoqProgress.ChuaLap, untouched.Progress);
        Assert.Equal((4, 1, 1, 1, 1), (heading.ChildTotal, heading.ChildApproved, heading.ChildPending, heading.ChildRework, heading.ChildUnassigned));

        // A draft round 2 moves the reworked line back to "waiting", shown as a draft.
        var round2 = Sheet("PDVT 05_v2", null, 2, Row(rework, PdvtApprovalStatus.ChuaTrinh, "Hưng Phát"));
        records.Add(round2);
        PdvtProgress.Apply(boq, records);
        Assert.Equal(PdvtBoqProgress.ChoKetQua, rework.Progress);
        Assert.Equal("Nháp", rework.StatusLabel);
        Assert.Equal(["PDVT 05_v2 · Lần 2", "PDVT 05 · Lần 1"], rework.History.Select(h => h.Reference));
        Assert.Equal("Bổ sung catalogue", rework.History[1].Note);

        // Approval in any round wins over a later pending round.
        records.Add(Sheet("PDVT 07", new DateTime(2026, 11, 2), 1, Row(approved, PdvtApprovalStatus.DaTrinh)));
        PdvtProgress.Apply(boq, records);
        Assert.Equal(PdvtBoqProgress.DaDuyet, approved.Progress);
        Assert.Equal("PDVT 05, PDVT 07", approved.PdvtNumbers);
    }

    [Fact]
    public void ApplyMatchesLegacyRowsWithoutBoqIdByNameAndSpec()
    {
        var item = new PdvtBoqItem { NoiDung = "Van cổng", ThongSoKyThuat = "DN25" };
        var legacy = new PdvtMaterialRow { NoiDung = "van cổng", ThongSoKyThuat = "dn25", Status = PdvtApprovalStatus.DaDuyet };
        PdvtProgress.Apply([item], [Sheet("PDVT 01", new DateTime(2026, 9, 1), 1, legacy)]);
        Assert.Equal(PdvtBoqProgress.DaDuyet, item.Progress);
    }
}
