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

    public static bool Matches(PdvtMaterialRow material, PdvtBoqItem item)
    {
        if (item.IsExtra) return material.IsExtra && material.LineKey == item.ExtraKey;
        if (material.BoqItemId == item.Id) return true;
        // Legacy BoQ rows saved before BoqItemId existed; "phát sinh" rows only match once linked.
        return string.IsNullOrEmpty(material.BoqItemId) && !material.IsExtra &&
               string.Equals(material.NoiDung, item.NoiDung, StringComparison.CurrentCultureIgnoreCase) &&
               string.Equals(material.ThongSoKyThuat, item.ThongSoKyThuat, StringComparison.CurrentCultureIgnoreCase);
    }

    public const string ExtraSectionCode = "PS";
    private static readonly System.Text.RegularExpressions.Regex AppendixCode = new(@"^PL\d+([.>]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static bool IsAppendixItem(PdvtBoqItem item) =>
        AppendixCode.IsMatch(item.IsHeading ? (item.Path.Length > 0 ? item.Path : item.SttBoq) : item.SectionCode);

    /// <summary>
    /// Builds the summary lines for "phát sinh" materials that are not linked to a BoQ line, grouped under a "PS" heading.
    /// Existing objects in <paramref name="cache"/> are reused so ticks survive a recompute.
    /// </summary>
    public static List<PdvtBoqItem> BuildExtraItems(IReadOnlyList<PdvtBoqItem> boq, IReadOnlyList<PdvtRecord> records, IDictionary<string, PdvtBoqItem>? cache = null)
    {
        var boqIds = boq.Where(x => !x.IsHeading).Select(x => x.Id).ToHashSet();
        var rows = records
            .OrderBy(r => r.Date).ThenBy(r => r.Version)
            .SelectMany(r => r.Materials)
            .Where(m => m.IsExtra && (string.IsNullOrEmpty(m.BoqItemId) || !boqIds.Contains(m.BoqItemId)))
            .GroupBy(m => m.LineKey)
            .ToList();
        if (rows.Count == 0) return [];

        PdvtBoqItem Get(string id, Func<PdvtBoqItem> create)
        {
            if (cache == null) return create();
            if (!cache.TryGetValue(id, out var existing)) cache[id] = existing = create();
            return existing;
        }

        var result = new List<PdvtBoqItem>
        {
            Get("ps", () => new PdvtBoqItem { Id = "ps", SttBoq = ExtraSectionCode, IsHeading = true, HeadingLevel = 1, IsExtra = true, NoiDung = "PHÁT SINH NGOÀI BOQ" })
        };
        var index = 0;
        foreach (var group in rows)
        {
            var latest = group.Last();
            var item = Get("ps:" + group.Key, () => new PdvtBoqItem { Id = "ps:" + group.Key, IsExtra = true, ExtraKey = group.Key });
            item.SttBoq = $"{ExtraSectionCode}.{++index}";
            item.SectionCode = ExtraSectionCode;
            item.NoiDung = latest.NoiDung;
            item.ThongSoKyThuat = latest.ThongSoKyThuat;
            item.DonViTinh = latest.DonViTinh;
            item.KhoiLuong = latest.KhoiLuong;
            result.Add(item);
        }
        return result;
    }

    /// <summary>Points every round of a "phát sinh" material at a BoQ line (e.g. after it became a contract appendix).</summary>
    public static int LinkExtra(IEnumerable<PdvtRecord> records, string extraKey, string boqItemId)
    {
        var changed = 0;
        foreach (var row in records.SelectMany(r => r.Materials).Where(m => m.IsExtra && m.LineKey == extraKey))
        {
            row.ExtraKey = extraKey;
            row.BoqItemId = boqItemId;
            changed++;
        }
        return changed;
    }

    public static int UnlinkExtras(IEnumerable<PdvtRecord> records, string boqItemId)
    {
        var changed = 0;
        foreach (var row in records.SelectMany(r => r.Materials).Where(m => m.IsExtra && m.BoqItemId == boqItemId))
        {
            row.BoqItemId = string.Empty;
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// Appends an appendix BoQ under a new top heading "PLn". Its headings get the path "PLn>…", so its
    /// "I", "1.1"… cannot collide with the main BoQ. Returns the appended material lines.
    /// </summary>
    public static List<PdvtBoqItem> AppendAppendix(IList<PdvtBoqItem> boq, IEnumerable<PdvtBoqItem> imported, string label)
    {
        var number = boq.Where(x => x.IsTopHeading && AppendixCode.IsMatch(x.SttBoq)).Count() + 1;
        var code = $"PL{number}";
        while (boq.Any(x => x.IsHeading && string.Equals(x.SttBoq, code, StringComparison.OrdinalIgnoreCase))) code = $"PL{++number}";

        boq.Add(new PdvtBoqItem { SttBoq = code, Path = code, IsHeading = true, HeadingLevel = 1, NoiDung = $"PHỤ LỤC {number} · {label}" });
        var added = new List<PdvtBoqItem>();
        foreach (var item in imported)
        {
            if (item.IsHeading)
            {
                // Items without a path come from older flat parses: their code is their path.
                var path = item.Path.Length > 0 ? item.Path : item.SttBoq;
                item.Path = $"{code}>{path}";
                item.SectionCode = item.SectionCode.Length > 0 ? $"{code}>{item.SectionCode}" : code;
                item.SttBoq = $"{code}.{item.SttBoq}";
                item.HeadingLevel += 1;
            }
            else
            {
                item.SectionCode = string.IsNullOrEmpty(item.SectionCode) ? code : $"{code}>{item.SectionCode}";
                added.Add(item);
            }
            boq.Add(item);
        }
        return added;
    }

    /// <summary>Links each unlinked "phát sinh" material to the single candidate line with the same name. Returns links made.</summary>
    public static int AutoLinkExtras(IReadOnlyList<PdvtRecord> records, IEnumerable<PdvtBoqItem> extras, IReadOnlyList<PdvtBoqItem> candidates)
    {
        var linked = 0;
        var taken = new HashSet<string>();
        foreach (var extra in extras.Where(x => x.IsExtra && !x.IsHeading).ToList())
        {
            var matches = candidates.Where(c => !c.IsHeading && !taken.Contains(c.Id) && Normalize(c.NoiDung) == Normalize(extra.NoiDung)).ToList();
            if (matches.Count != 1) continue;
            LinkExtra(records, extra.ExtraKey, matches[0].Id);
            taken.Add(matches[0].Id);
            linked++;
        }
        return linked;
    }

    public static bool IsInSection(PdvtBoqItem item, PdvtBoqItem heading)
    {
        if (ReferenceEquals(item, heading)) return false;
        if (heading.Path.Length > 0)
        {
            // Path-based BoQ: "C>I>I.1" lies inside "C" and "C>I", never inside another "I".
            var own = item.IsHeading ? item.Path : item.SectionCode;
            return (!item.IsHeading && string.Equals(own, heading.Path, StringComparison.OrdinalIgnoreCase)) ||
                   own.StartsWith(heading.Path + ">", StringComparison.OrdinalIgnoreCase);
        }
        var code = heading.SttBoq;
        if (string.IsNullOrEmpty(code) || item.Path.Length > 0) return false;
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
                (h.Record.IsSubmitted ? $"Trình {h.Record.SubmittedDate:dd/MM/yyyy}" : $"Nháp {h.Record.Date:dd/MM/yyyy}") +
                    (h.Row.IsExtra && !item.IsExtra ? " · trình dạng phát sinh" : string.Empty),
                BrandWithOrigin(h.Row.NhanHieuPheDuyet, h.Row.XuatXuDeTrinh),
                h.Row.Status, h.Record.IsSubmitted, h.Row.ReviewNote)).ToList();
            item.HasLinkedExtra = !item.IsExtra && hits.Any(h => h.Row.IsExtra);

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
        item.HasLinkedExtra = false;
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
