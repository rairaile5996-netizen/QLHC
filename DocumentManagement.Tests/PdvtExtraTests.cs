using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.Tests;

public sealed class PdvtExtraTests
{
    private static PdvtMaterialRow Extra(string name, PdvtApprovalStatus status, string? key = null) =>
        new() { Source = "Phát sinh", NoiDung = name, Status = status, ExtraKey = key ?? string.Empty };

    private static PdvtRecord Sheet(string number, DateTime date, params PdvtMaterialRow[] rows) =>
        new() { Number = number, Date = date, SubmittedDate = date, Materials = rows.ToList() };

    [Fact]
    public void ExtrasAppearUnderOwnSectionAndKeepIdentityAcrossRounds()
    {
        var boq = new List<PdvtBoqItem> { new() { SttBoq = "1", NoiDung = "Máng cáp" } };
        var first = Extra("Máng cáp", PdvtApprovalStatus.YeuCauSua); // same name as BoQ line, must not match it
        var round2 = Extra("Máng cáp", PdvtApprovalStatus.DaDuyet, key: first.Id);
        var records = new List<PdvtRecord>
        {
            Sheet("PDVT 01", new DateTime(2026, 10, 1), first),
            Sheet("PDVT 01_v2", new DateTime(2026, 10, 9), round2)
        };

        var extras = PdvtProgress.BuildExtraItems(boq, records);
        var all = boq.Concat(extras).ToList();
        PdvtProgress.Apply(all, records);

        Assert.Equal(2, extras.Count);
        Assert.True(extras[0].IsHeading);
        var line = extras[1];
        Assert.Equal("PS.1", line.SttBoq);
        Assert.True(PdvtProgress.IsInSection(line, extras[0]));
        Assert.Equal(PdvtBoqProgress.DaDuyet, line.Progress);
        Assert.Equal(2, line.History.Count);
        Assert.Equal(PdvtBoqProgress.ChuaLap, boq[0].Progress);
    }

    [Fact]
    public void LinkingMovesHistoryToBoqLineAndUnlinkingRestoresIt()
    {
        var boq = new List<PdvtBoqItem> { new() { SttBoq = "1", NoiDung = "Đèn exit" } };
        var row = Extra("Đèn exit chỉ hướng", PdvtApprovalStatus.DaDuyet);
        var records = new List<PdvtRecord> { Sheet("PDVT 03", new DateTime(2026, 10, 1), row) };
        var extra = PdvtProgress.BuildExtraItems(boq, records)[1];

        Assert.Equal(1, PdvtProgress.LinkExtra(records, extra.ExtraKey, boq[0].Id));
        Assert.Empty(PdvtProgress.BuildExtraItems(boq, records));
        PdvtProgress.Apply(boq, records);
        Assert.Equal(PdvtBoqProgress.DaDuyet, boq[0].Progress);
        Assert.True(boq[0].HasLinkedExtra);
        Assert.Contains("phát sinh", boq[0].History[0].Submitted);

        Assert.Equal(1, PdvtProgress.UnlinkExtras(records, boq[0].Id));
        Assert.Equal(2, PdvtProgress.BuildExtraItems(boq, records).Count);
    }

    [Fact]
    public void AppendixIsPrefixedAndAutoLinksUniqueNameMatches()
    {
        var boq = new List<PdvtBoqItem>
        {
            new() { SttBoq = "I", IsHeading = true, HeadingLevel = 1, NoiDung = "Điện" },
            new() { SttBoq = "1", SectionCode = "I", NoiDung = "Cáp" }
        };
        var records = new List<PdvtRecord>
        {
            Sheet("PDVT 05", new DateTime(2026, 10, 1), Extra("Đèn exit", PdvtApprovalStatus.DaDuyet), Extra("Quạt hút", PdvtApprovalStatus.DaTrinh))
        };
        var extras = PdvtProgress.BuildExtraItems(boq, records);
        var appendix = new List<PdvtBoqItem>
        {
            new() { SttBoq = "I", IsHeading = true, HeadingLevel = 1, NoiDung = "Bổ sung" },
            new() { SttBoq = "1", SectionCode = "I", NoiDung = "đèn  EXIT" },
            new() { SttBoq = "2", SectionCode = "I", NoiDung = "Ống gió" }
        };

        var added = PdvtProgress.AppendAppendix(boq, appendix, "PL bổ sung");
        Assert.Equal(2, added.Count);
        Assert.Equal("PL1", boq[2].SttBoq);
        Assert.Equal("PL1.I", boq[3].SttBoq);
        Assert.Equal("PL1.I", added[0].SectionCode);
        Assert.True(PdvtProgress.IsAppendixItem(added[0]));
        Assert.False(PdvtProgress.IsInSection(added[0], boq[0])); // appendix "I" must not merge into contract "I"

        Assert.Equal(1, PdvtProgress.AutoLinkExtras(records, extras, added));
        PdvtProgress.Apply(boq, records);
        Assert.Equal(PdvtBoqProgress.DaDuyet, added[0].Progress);
        var remaining = PdvtProgress.BuildExtraItems(boq, records);
        Assert.Equal("Quạt hút", Assert.Single(remaining, x => !x.IsHeading).NoiDung);

        PdvtProgress.AppendAppendix(boq, [new PdvtBoqItem { NoiDung = "Van" }], "PL2");
        Assert.Contains(boq, x => x.SttBoq == "PL2" && x.IsTopHeading);
        Assert.Equal("PL2", boq[^1].SectionCode);
    }
}
