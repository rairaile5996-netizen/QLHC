using DocumentManagement.Models;

namespace DocumentManagement.Services;

/// <summary>
/// Pure PDVT approval rules shared by the sheet editor and the BoQ summary.
/// A BoQ material counts as approved once any round approved it; otherwise its state
/// follows the most recent round that contains it.
/// </summary>
public static class PdvtProgress
{
    private static readonly char[] OptionSeparators = ['/', ';', '|', ',', '\n', '\r'];

    public static bool HasResult(PdvtApprovalStatus status) =>
        status is PdvtApprovalStatus.YeuCauSua or PdvtApprovalStatus.DaDuyet or PdvtApprovalStatus.KhongDuyet;

    public static string StatusLabel(PdvtApprovalStatus status, bool submitted) => !submitted ? "Nháp" : status switch
    {
        PdvtApprovalStatus.DaDuyet => "Đã duyệt",
        PdvtApprovalStatus.YeuCauSua => "Yêu cầu sửa",
        PdvtApprovalStatus.KhongDuyet => "Không duyệt",
        _ => "Chờ kết quả"
    };

    /// <summary>Style key used by the view: draft, sub, fix, ok, no.</summary>
    public static string StatusKind(PdvtApprovalStatus status, bool submitted) => !submitted ? "draft" : status switch
    {
        PdvtApprovalStatus.DaDuyet => "ok",
        PdvtApprovalStatus.YeuCauSua => "fix",
        PdvtApprovalStatus.KhongDuyet => "no",
        _ => "sub"
    };

    public static PdvtApprovalStatus RecordStatus(PdvtRecord record)
    {
        if (!record.IsSubmitted) return PdvtApprovalStatus.ChuaTrinh;
        var rows = record.Materials;
        if (rows.Count == 0 || rows.Any(x => !HasResult(x.Status))) return PdvtApprovalStatus.DaTrinh;
        if (rows.All(x => x.Status == PdvtApprovalStatus.DaDuyet)) return PdvtApprovalStatus.DaDuyet;
        if (rows.All(x => x.Status == PdvtApprovalStatus.KhongDuyet)) return PdvtApprovalStatus.KhongDuyet;
        return PdvtApprovalStatus.YeuCauSua;
    }

    public static string RecordStatusLabel(PdvtRecord record) => RecordStatus(record) switch
    {
        PdvtApprovalStatus.ChuaTrinh => "Nháp",
        PdvtApprovalStatus.DaTrinh => "Đã trình",
        PdvtApprovalStatus.YeuCauSua => "Yêu cầu sửa",
        PdvtApprovalStatus.DaDuyet => "Đã duyệt",
        _ => "Không duyệt"
    };

    public static string RecordProgressText(PdvtRecord record)
    {
        if (!record.IsSubmitted) return $"Tạo {record.Date:dd/MM/yyyy}";
        var total = record.Materials.Count;
        var approved = record.ApprovedCount;
        var rework = record.ReworkCount + record.RejectedCount;
        var submitted = $"trình {record.SubmittedDate:dd/MM/yyyy}";
        if (record.PendingCount == total) return $"Chờ kết quả · {submitted}";
        if (record.PendingCount > 0) return $"{approved}/{total} duyệt · {submitted}";
        if (rework > 0 && !string.IsNullOrEmpty(record.NextRoundNumber)) return $"{approved}/{total} duyệt · đã trình lại ở {record.NextRoundNumber}";
        return rework > 0 ? $"{approved}/{total} duyệt · {rework} cần trình lại" : $"{approved}/{total} duyệt";
    }

    /// <summary>
    /// True when the submitted value is neither the contract value nor one of its listed alternatives
    /// ("Cadisun / Cadivi" accepts either brand). Blank values are never flagged.
    /// </summary>
    public static bool IsDifferent(string? contract, string? submitted, IEnumerable<string> options)
    {
        var target = Normalize(submitted);
        var source = Normalize(contract);
        if (target.Length == 0 || source.Length == 0 || target == source) return false;
        var accepted = Split(contract).Concat(options.SelectMany(Split)).Select(Normalize);
        return !accepted.Contains(target);
    }

    public static bool Matches(PdvtMaterialRow material, PdvtBoqItem item) =>
        material.BoqItemId == item.Id ||
        (string.IsNullOrEmpty(material.BoqItemId) &&
         string.Equals(material.NoiDung, item.NoiDung, StringComparison.CurrentCultureIgnoreCase) &&
         string.Equals(material.ThongSoKyThuat, item.ThongSoKyThuat, StringComparison.CurrentCultureIgnoreCase));

    public static bool IsInSection(PdvtBoqItem item, PdvtBoqItem heading)
    {
        var code = heading.SttBoq;
        if (string.IsNullOrEmpty(code) || ReferenceEquals(item, heading)) return false;
        return item.IsHeading
            ? item.SttBoq.StartsWith(code + ".", StringComparison.OrdinalIgnoreCase)
            : string.Equals(item.SectionCode, code, StringComparison.OrdinalIgnoreCase) ||
              item.SectionCode.StartsWith(code + ".", StringComparison.OrdinalIgnoreCase);
    }

    public static string Reference(PdvtRecord record) => $"{record.Number} · L{record.Version}";

    /// <summary>Recomputes every display/progress field of the BoQ from the saved sheets.</summary>
    public static void Apply(IReadOnlyList<PdvtBoqItem> boq, IReadOnlyList<PdvtRecord> records)
    {
        // Oldest first: submitted rounds by submission date, drafts last.
        var ordered = records
            .OrderBy(r => r.IsSubmitted ? 0 : 1)
            .ThenBy(r => r.SubmittedDate ?? r.Date)
            .ThenBy(r => r.Version)
            .ToList();

        foreach (var item in boq)
        {
            if (item.IsHeading) { ResetMaterialFields(item); continue; }

            var hits = ordered
                .SelectMany(r => r.Materials.Where(m => Matches(m, item)).Take(1).Select(m => (Record: r, Row: m)))
                .ToList();

            item.PdvtNumbers = string.Join(", ", hits.Select(h => h.Record.Number).Distinct()
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
            item.History = hits.AsEnumerable().Reverse().Select(h => new PdvtHistoryEntry(
                h.Record.Id,
                $"{h.Record.Number} · Lần {h.Record.Version}",
                h.Record.IsSubmitted ? $"Trình {h.Record.SubmittedDate:dd/MM/yyyy}" : $"Nháp {h.Record.Date:dd/MM/yyyy}",
                BrandWithOrigin(h.Row.NhanHieuPheDuyet, h.Row.XuatXuDeTrinh),
                h.Row.Status, h.Record.IsSubmitted, h.Row.ReviewNote)).ToList();

            var approved = hits.LastOrDefault(h => h.Record.IsSubmitted && h.Row.Status == PdvtApprovalStatus.DaDuyet);
            item.ApprovedPdvtNumbers = string.Join(", ", hits
                .Where(h => h.Record.IsSubmitted && h.Row.Status == PdvtApprovalStatus.DaDuyet)
                .Select(h => h.Record.Number).Distinct());

            if (hits.Count == 0)
            {
                ResetMaterialFields(item);
            }
            else if (approved.Record != null)
            {
                Set(item, PdvtBoqProgress.DaDuyet, "Đã duyệt", "ok", approved.Record);
                item.ApprovedBrand = approved.Row.NhanHieuPheDuyet;
                item.ApprovedOrigin = approved.Row.XuatXuDeTrinh;
                item.ApprovedDate = approved.Row.ApprovedDate ?? approved.Record.SubmittedDate;
            }
            else
            {
                var latest = hits[^1];
                var submitted = latest.Record.IsSubmitted;
                var progress = submitted && HasResult(latest.Row.Status) ? PdvtBoqProgress.CanTrinhLai : PdvtBoqProgress.ChoKetQua;
                Set(item, progress, StatusLabel(latest.Row.Status, submitted), StatusKind(latest.Row.Status, submitted), latest.Record);
                item.ApprovedBrand = string.Empty;
                item.ApprovedOrigin = string.Empty;
                item.ApprovedDate = null;
            }
            item.ApprovalSummary = item.StatusLabel;
        }

        foreach (var heading in boq.Where(x => x.IsHeading))
        {
            var children = boq.Where(x => !x.IsHeading && IsInSection(x, heading)).ToList();
            heading.ChildTotal = children.Count;
            heading.ChildApproved = children.Count(x => x.Progress == PdvtBoqProgress.DaDuyet);
            heading.ChildPending = children.Count(x => x.Progress == PdvtBoqProgress.ChoKetQua);
            heading.ChildRework = children.Count(x => x.Progress == PdvtBoqProgress.CanTrinhLai);
            heading.ChildUnassigned = children.Count(x => x.Progress == PdvtBoqProgress.ChuaLap);
        }
    }

    private static void Set(PdvtBoqItem item, PdvtBoqProgress progress, string label, string kind, PdvtRecord record)
    {
        item.Progress = progress;
        item.StatusLabel = label;
        item.StatusKind = kind;
        item.LatestReference = Reference(record);
        item.LatestRecordId = record.Id;
    }

    private static void ResetMaterialFields(PdvtBoqItem item)
    {
        item.Progress = PdvtBoqProgress.ChuaLap;
        item.StatusLabel = item.IsHeading ? string.Empty : "Chưa lập";
        item.StatusKind = "none";
        item.LatestReference = string.Empty;
        item.LatestRecordId = string.Empty;
        item.ApprovedBrand = string.Empty;
        item.ApprovedOrigin = string.Empty;
        item.ApprovedDate = null;
        item.ApprovedPdvtNumbers = string.Empty;
        item.ApprovalSummary = item.StatusLabel;
        if (item.IsHeading) item.PdvtNumbers = string.Empty;
        item.History = [];
    }

    public static string BrandWithOrigin(string? brand, string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return brand?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(brand) ? origin.Trim() : $"{brand.Trim()} ({origin.Trim()})";
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split(OptionSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
