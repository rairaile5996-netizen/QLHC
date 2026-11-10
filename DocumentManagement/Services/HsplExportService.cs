using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentManagement.Models;
using System.Text.Json;
using System.IO;
using System.Globalization;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WordDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using ContractDocument = DocumentManagement.Models.Document;

namespace DocumentManagement.Services;

public sealed class HsplExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly OrganizationChartRenderer OrganizationChartRenderer = new();
    // Push the role column three standard Word tab intervals farther from the name column.
    private const int RepresentativePositionTabStop = 5360;

    public string GetDataPath(ContractDocument document) =>
        Path.Combine(document.FolderPath ?? AppContext.BaseDirectory, "hspl_data.json");

    public HsplData Load(ContractDocument document)
    {
        var path = GetDataPath(document);
        if (!File.Exists(path)) return new HsplData();
        try { return JsonSerializer.Deserialize<HsplData>(File.ReadAllText(path), JsonOptions) ?? new HsplData(); }
        catch { return new HsplData(); }
    }

    public void Save(ContractDocument document, HsplData data)
    {
        var folder = document.FolderPath ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(folder);
        File.WriteAllText(GetDataPath(document), JsonSerializer.Serialize(data, JsonOptions));
    }

    public string Export(ContractDocument document, HsplData data, CvxdInfo? projectInfo = null)
    {
        if (string.IsNullOrWhiteSpace(data.TemplatePath) || !File.Exists(data.TemplatePath))
            throw new FileNotFoundException("Chưa chọn template HSPL hợp lệ.");

        var outputDirectory = Path.Combine(document.FolderPath ?? AppContext.BaseDirectory, "2.HSPL");
        Directory.CreateDirectory(outputDirectory);
        var safeName = string.Join("_", (document.ContractNumber ?? "HSPL").Split(Path.GetInvalidFileNameChars()));
        var outputPath = Path.Combine(outputDirectory, $"HSPL_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
        File.Copy(data.TemplatePath, outputPath, true);

        var replacements = BuildReplacements(document, data, projectInfo);

        using var word = WordprocessingDocument.Open(outputPath, true);
        ExpandPersonnelTable(word.MainDocumentPart?.Document?.Body, data.Personnel, projectInfo?.TenCongTrinh ?? document.ConstructionName ?? string.Empty);
        ExpandWorkerCommitments(word.MainDocumentPart?.Document?.Body, data.Workers);
        ExpandDecisionLists(word.MainDocumentPart?.Document?.Body, data.Personnel);
        InsertOrganizationCharts(word.MainDocumentPart, data.Personnel);
        ReplacePart(word.MainDocumentPart?.Document?.Body, replacements);
        foreach (var header in word.MainDocumentPart?.HeaderParts ?? []) ReplacePart(header.Header, replacements);
        foreach (var footer in word.MainDocumentPart?.FooterParts ?? []) ReplacePart(footer.Footer, replacements);
        word.MainDocumentPart?.Document.Save();
        word.Dispose();
        ExportedFileRegistry.Record(document.FolderPath, outputPath, "HSPL");
        return outputPath;
    }

    public IReadOnlyList<string> ExportSelected(
        ContractDocument document,
        HsplData data,
        IEnumerable<HsplDocumentTypeOption> selectedTypes,
        IEnumerable<HsplTemplateOption> templates,
        CvxdInfo? projectInfo = null)
    {
        var outputs = new List<string>();
        foreach (var type in selectedTypes)
        {
            var template = templates.FirstOrDefault(t => IsTemplateForDocumentType(t.FilePath, type.Code));
            if (template == null)
                throw new FileNotFoundException($"Không tìm thấy template {type.Code} - {type.Name} trong thư mục HSPL đã cấu hình.");
            outputs.Add(ExportWithTemplate(document, data, template.FilePath, type.Code, type.Name, projectInfo));
        }
        return outputs;
    }

    private static bool IsTemplateForDocumentType(string templatePath, string code)
    {
        var fileName = Path.GetFileNameWithoutExtension(templatePath);
        var aliases = code.ToUpperInvariant() switch
        {
            "PLNT" => new[] { "PLNT", "HSPL", "PHÁP LÝ NHÀ THẦU" },
            "CCAT" => new[] { "CCAT", "CAM KẾT ATLĐ", "CAM KẾT AN TOÀN LAO ĐỘNG" },
            "DKC" => new[] { "DKC", "ĐƠN KHỞI CÔNG", "LỆNH KHỞI CÔNG" },
            "BGMB" => new[] { "BGMB", "BÀN GIAO MẶT BẰNG" },
            _ => new[] { code }
        };

        return aliases.Any(alias => fileName.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }

    private string ExportWithTemplate(
        ContractDocument document,
        HsplData data,
        string templatePath,
        string code,
        string displayName,
        CvxdInfo? projectInfo)
    {
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            throw new FileNotFoundException($"Template {code} không tồn tại.");

        var outputDirectory = Path.Combine(document.FolderPath ?? AppContext.BaseDirectory, "2.HSPL");
        Directory.CreateDirectory(outputDirectory);
        var safeName = string.Join("_", (document.ContractNumber ?? "HSPL").Split(Path.GetInvalidFileNameChars()));
        var outputPath = Path.Combine(outputDirectory, $"{code}_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
        File.Copy(templatePath, outputPath, true);
        ExportDocumentContent(outputPath, document, data, projectInfo);
        ExportedFileRegistry.Record(document.FolderPath, outputPath, "HSPL");
        return outputPath;
    }

    private static void ExportDocumentContent(
        string outputPath,
        ContractDocument document,
        HsplData data,
        CvxdInfo? projectInfo)
    {
        var replacements = BuildReplacements(document, data, projectInfo);
        using var word = WordprocessingDocument.Open(outputPath, true);
        ExpandPersonnelTable(word.MainDocumentPart?.Document?.Body, data.Personnel, projectInfo?.TenCongTrinh ?? document.ConstructionName ?? string.Empty);
        ExpandWorkerCommitments(word.MainDocumentPart?.Document?.Body, data.Workers);
        ExpandDecisionLists(word.MainDocumentPart?.Document?.Body, data.Personnel);
        InsertOrganizationCharts(word.MainDocumentPart, data.Personnel);
        ReplacePart(word.MainDocumentPart?.Document?.Body, replacements);
        foreach (var header in word.MainDocumentPart?.HeaderParts ?? []) ReplacePart(header.Header, replacements);
        foreach (var footer in word.MainDocumentPart?.FooterParts ?? []) ReplacePart(footer.Footer, replacements);
        word.MainDocumentPart?.Document.Save();
    }

    private static Dictionary<string, string> BuildReplacements(
        ContractDocument document,
        HsplData data,
        CvxdInfo? projectInfo)
    {
        var commander = data.Personnel.FirstOrDefault(p => p.Id == data.ChosenCommanderId);
        var atldHead = data.Personnel.FirstOrDefault(p => p.Id == data.ChosenAtldHeadId);
        var contractNumber = document.ContractNumber ?? string.Empty;
        var contractSignDate = ResolveContractSignDate(document, projectInfo);
        var signDate = !string.IsNullOrWhiteSpace(projectInfo?.NgayKyHD)
            ? projectInfo.NgayKyHD
            : contractSignDate.ToString("dd/MM/yyyy");
        var documentDate = FormatVietnameseDate(data.DocumentDate ?? DateTime.Today);
        var tenderDate = contractSignDate.AddDays(-7);
        if (tenderDate.DayOfWeek == DayOfWeek.Sunday) tenderDate = tenderDate.AddDays(-1);
        var constructionStartDate = data.ConstructionStartDate;
        var applicationDate = constructionStartDate.HasValue
            ? (constructionStartDate.Value.AddDays(-1) < contractSignDate
                ? contractSignDate
                : constructionStartDate.Value.AddDays(-1))
            : (DateTime?)null;
        var emptyVietnameseDate = "ngày ...... tháng ...... năm ......";
        var constructionStartDateText = constructionStartDate.HasValue
            ? FormatVietnameseDate(constructionStartDate.Value)
            : emptyVietnameseDate;
        var applicationDateText = applicationDate.HasValue
            ? FormatVietnameseDate(applicationDate.Value)
            : emptyVietnameseDate;
        var employerName = FirstNonEmpty(projectInfo?.TenChuDauTu, document.EmployerName);
        var constructionName = !string.IsNullOrWhiteSpace(projectInfo?.TenCongTrinh)
            ? projectInfo.TenCongTrinh
            : document.ConstructionName ?? string.Empty;
        var projectName = FirstNonEmpty(projectInfo?.TenDuAn, document.ProjectName);
        var location = projectInfo?.DiaDiem?.Trim() ?? string.Empty;
        var itemName = !string.IsNullOrWhiteSpace(projectInfo?.TenHangMuc)
            ? projectInfo.TenHangMuc
            : document.ItemName ?? string.Empty;
        var subcontractorName = FirstNonEmpty(
            projectInfo?.TenNhaThauPhu,
            projectInfo?.TenNhaThau,
            document.ContractorName);
        var contractorCode = FirstNonEmpty(projectInfo?.KyHieuNhaThau, document.ContractorCode);
        var workerList = string.Join(Environment.NewLine, data.Workers.Select((w, i) =>
            $"{i + 1}. {w.FullName}    {w.Position}    {w.DateOfBirth:dd/MM/yyyy}"));

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{SO_HOP_DONG}}"] = contractNumber,
            ["{{DU_AN}}"] = document.ProjectName,
            ["{{CONG_TRINH}}"] = constructionName,
            ["{{NGAY_KY}}"] = signDate,
            ["{SOHD}"] = contractNumber,
            ["{NgayKy}"] = signDate,
            ["{{NGAY_LAP_HSPL}}"] = documentDate,
            ["{NgayLapHSPL}"] = documentDate,
            ["{NgayNhanThau}"] = FormatVietnameseDate(tenderDate),
            ["{{NGAY_NHAN_THAU}}"] = FormatVietnameseDate(tenderDate),
            ["{NgayKC}"] = constructionStartDateText,
            ["{NgaylapKC}"] = applicationDateText,
            ["{NgayLapKC}"] = applicationDateText,
            ["{{NGAY_KHOI_CONG}}"] = constructionStartDateText,
            ["{{NGAY_LAP_KHOI_CONG}}"] = applicationDateText,
            ["{TenNTC}"] = employerName,
            ["{TENNTC}"] = employerName.ToUpperInvariant(),
            ["{TenNTP}"] = subcontractorName,
            ["{TENNTP}"] = subcontractorName.ToUpperInvariant(),
            ["{KY_HIEU_NT}"] = contractorCode,
            ["{TenDuAn}"] = projectName,
            ["{TenDuAn_VietHoa}"] = projectName.ToUpperInvariant(),
            ["{DiaDiem}"] = location,
            ["{Diadiem}"] = location,
            ["{HangMuc}"] = itemName,
            ["{TenCongTrinh}"] = constructionName,
            ["{SoHD}"] = contractNumber,
            ["{TenTVGS}"] = projectInfo?.TenTuVanGiamSat?.Trim() ?? string.Empty,
            ["{DaiDienTVGS}"] = projectInfo?.DaiDienTVGS?.Trim() ?? string.Empty,
            ["{ChucVuTVGS}"] = projectInfo?.ChucVuTVGS?.Trim() ?? string.Empty,
            ["{ChucvuTVGS}"] = projectInfo?.ChucVuTVGS?.Trim() ?? string.Empty,
            ["{DS_TVGS}"] = BuildRepresentativeList(projectInfo?.TvgsRepresentatives, projectInfo?.DaiDienTVGS, projectInfo?.ChucVuTVGS),
            ["{DaiDienNTC}"] = projectInfo?.DaiDienChuDauTu?.Trim() ?? string.Empty,
            ["{ChucVuNTC}"] = projectInfo?.ChucVuCDT?.Trim() ?? string.Empty,
            ["{ChucvuNTC}"] = projectInfo?.ChucVuCDT?.Trim() ?? string.Empty,
            ["{DS_NTC}"] = BuildRepresentativeList(projectInfo?.EmployerRepresentatives, projectInfo?.DaiDienChuDauTu, projectInfo?.ChucVuCDT),
            ["{DaiDienNTP}"] = projectInfo?.DaiDienNhaThau?.Trim() ?? string.Empty,
            ["{ChucVuNTP}"] = projectInfo?.ChucVuNT?.Trim() ?? string.Empty,
            ["{ChucvuNTP}"] = projectInfo?.ChucVuNT?.Trim() ?? string.Empty,
            ["{DS_NTP}"] = BuildRepresentativeList(projectInfo?.ContractorRepresentatives, projectInfo?.DaiDienNhaThau, projectInfo?.ChucVuNT),
            ["{{CHI_HUY_TRUONG_HO_TEN}}"] = commander?.FullName ?? string.Empty,
            ["{{CHI_HUY_TRUONG_CHUC_VU}}"] = commander?.Position ?? string.Empty,
            ["{{CHT_TEN}}"] = commander?.FullName ?? string.Empty,
            ["{{CHT_CV}}"] = commander?.Position ?? string.Empty,
            ["{{TRUONG_BAN_ATLD_HO_TEN}}"] = atldHead?.FullName ?? string.Empty,
            ["{{TRUONG_BAN_ATLD_CHUC_VU}}"] = atldHead?.Position ?? string.Empty,
            ["{{TBATLD_TEN}}"] = atldHead?.FullName ?? string.Empty,
            ["{{TBATLD_CV}}"] = atldHead?.Position ?? string.Empty,
            ["{{DANH_SACH_BAN_CHI_HUY}}"] = BuildList(data.Personnel),
            ["{{DANH_SACH_BAN_ATLD}}"] = BuildAtldList(data.Personnel),
            ["{{DS_BCH}}"] = BuildList(data.Personnel),
            ["{{DS_BCH_QD}}"] = BuildDecisionList(data.Personnel),
            ["{{DS_ATLD}}"] = BuildAtldList(data.Personnel),
            ["{{DANH_SACH_CONG_NHAN}}"] = workerList,
            ["{{DS_CN}}"] = workerList
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static DateTime ResolveContractSignDate(ContractDocument document, CvxdInfo? projectInfo)
    {
        if (DateTime.TryParseExact(projectInfo?.NgayKyHD, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var configuredDate))
            return configuredDate;

        return document.SignDate;
    }

    private static string FormatVietnameseDate(DateTime value) =>
        $"Ngày {value:dd} tháng {value:MM} năm {value:yyyy}";

    private static string BuildList(IEnumerable<HsplPerson> people)
    {
        return string.Join(Environment.NewLine, people.Select((p, i) =>
            $"{i + 1}. {p.FullName}    {p.Position}"));
    }

    private static string BuildAtldList(IEnumerable<HsplPerson> people)
    {
        var selected = people.Where(IsAtldRole).ToList();
        return string.Join(Environment.NewLine, selected.Select((p, i) =>
            $"{i + 1}. {p.FullName}    {p.Position}"));
    }

    private static string BuildDecisionList(IEnumerable<HsplPerson> people)
    {
        return string.Join(Environment.NewLine, people.Select(person =>
            $"Ông: {person.FullName}\tChức vụ: {person.Position}"));
    }

    private static string BuildRepresentativeList(
        IEnumerable<ContractRepresentative>? representatives,
        string? fallbackName,
        string? fallbackPosition)
    {
        var rows = representatives?
            .Where(person => !string.IsNullOrWhiteSpace(person.FullName))
            .Select(person => FormatRepresentative(person.FullName, person.Position))
            .ToList() ?? [];

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(fallbackName))
            rows.Add(FormatRepresentative(fallbackName, fallbackPosition));

        return string.Join(Environment.NewLine, rows);
    }

    private static string FormatRepresentative(string name, string? position) =>
        string.IsNullOrWhiteSpace(position)
            ? $"Ông: {name.Trim()}"
            : $"Ông: {name.Trim()}\tChức vụ: {position.Trim()}";

    private static void ExpandPersonnelTable(OpenXmlElement? root, IEnumerable<HsplPerson> people, string constructionName)
    {
        if (root == null) return;

        foreach (var markerRow in root.Descendants<TableRow>()
                     .Where(row => row.InnerText.Contains("{{BCH_TABLE_ROWS}}", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            LockSequenceColumnWidth(markerRow.Ancestors<Table>().FirstOrDefault());
            var personnel = people.ToList();
            foreach (var pair in personnel.Select((person, index) => (person, index)))
            {
                var row = (TableRow)markerRow.CloneNode(true);
                var cells = row.Elements<TableCell>().ToList();
                if (cells.Count > 0) SetTableCellText(cells[0], (pair.index + 1).ToString());
                if (cells.Count > 1) SetTableCellText(cells[1], pair.person.FullName);
                if (cells.Count > 2) SetTableCellText(cells[2], pair.person.Position);
                if (cells.Count > 3) SetTableCellText(cells[3], constructionName);
                markerRow.InsertBeforeSelf(row);
            }

            markerRow.Remove();
        }
    }

    private static void ExpandWorkerCommitments(Body? body, IEnumerable<HsplWorker> workers)
    {
        if (body == null) return;
        var workerList = workers.ToList();
        ExpandWorkerTable(body, workerList);

        var beginMarker = body.Elements<Paragraph>().FirstOrDefault(paragraph =>
            paragraph.InnerText.Contains("{{CCAT_WORKER_BEGIN}}", StringComparison.OrdinalIgnoreCase));
        var endMarker = body.Elements<Paragraph>().FirstOrDefault(paragraph =>
            paragraph.InnerText.Contains("{{CCAT_WORKER_END}}", StringComparison.OrdinalIgnoreCase));
        if (beginMarker == null || endMarker == null) return;

        var bodyElements = body.ChildElements.ToList();
        var beginIndex = bodyElements.IndexOf(beginMarker);
        var endIndex = bodyElements.IndexOf(endMarker);
        if (beginIndex < 0 || endIndex <= beginIndex) return;

        var workerPageTemplate = bodyElements.Skip(beginIndex + 1).Take(endIndex - beginIndex - 1).ToList();
        foreach (var worker in workerList)
        {
            beginMarker.InsertBeforeSelf(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            foreach (var templateElement in workerPageTemplate)
            {
                var copy = templateElement.CloneNode(true);
                ReplaceWorkerPlaceholders(copy, worker);
                beginMarker.InsertBeforeSelf(copy);
            }
        }

        foreach (var element in bodyElements.Skip(beginIndex).Take(endIndex - beginIndex + 1))
            element.Remove();
    }

    private static void ExpandWorkerTable(OpenXmlElement root, IReadOnlyList<HsplWorker> workers)
    {
        foreach (var markerRow in root.Descendants<TableRow>()
                     .Where(row => row.InnerText.Contains("{{CN_TABLE_ROWS}}", StringComparison.OrdinalIgnoreCase)
                                || row.InnerText.Contains("{{CCAT_WORKER_ROWS}}", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            foreach (var pair in workers.Select((worker, index) => (worker, index)))
            {
                var row = (TableRow)markerRow.CloneNode(true);
                var cells = row.Elements<TableCell>().ToList();
                if (cells.Count > 0) SetTableCellText(cells[0], (pair.index + 1).ToString());
                if (cells.Count > 1) SetTableCellText(cells[1], pair.worker.FullName);
                if (cells.Count > 2) SetTableCellText(cells[2], pair.worker.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty);
                if (cells.Count > 3) SetTableCellText(cells[3], pair.worker.Position);
                markerRow.InsertBeforeSelf(row);
            }

            markerRow.Remove();
        }
    }

    private static void ReplaceWorkerPlaceholders(OpenXmlElement root, HsplWorker worker)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{CN_HO_TEN}}"] = worker.FullName,
            ["{{CN_CHUC_VU}}"] = worker.Position,
            ["{{CN_NGAY_SINH}}"] = worker.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty,
            ["{CN_HO_TEN}"] = worker.FullName,
            ["{CN_CHUC_VU}"] = worker.Position,
            ["{CN_NGAY_SINH}"] = worker.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty
        };
        var paragraphs = root is Paragraph rootParagraph
            ? new[] { rootParagraph }.Concat(root.Descendants<Paragraph>()).Distinct()
            : root.Descendants<Paragraph>();
        foreach (var targetParagraph in paragraphs)
        {
            foreach (var replacement in replacements)
                ReplaceTextRuns(targetParagraph, replacement.Key, replacement.Value, StringComparison.Ordinal);
        }
    }

    // The long marker would otherwise make Word auto-fit the STT column before rows are expanded.
    private static void LockSequenceColumnWidth(Table? table)
    {
        if (table == null) return;

        const string sequenceColumnWidth = "600";
        var tableProperties = table.GetFirstChild<TableProperties>();
        if (tableProperties == null)
        {
            tableProperties = new TableProperties();
            table.PrependChild(tableProperties);
        }
        var layout = tableProperties.GetFirstChild<TableLayout>();
        if (layout == null)
            tableProperties.Append(new TableLayout { Type = TableLayoutValues.Fixed });
        else
            layout.Type = TableLayoutValues.Fixed;

        var firstGridColumn = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>().FirstOrDefault();
        if (firstGridColumn != null) firstGridColumn.Width = sequenceColumnWidth;

        foreach (var row in table.Elements<TableRow>())
        {
            var firstCell = row.Elements<TableCell>().FirstOrDefault();
            if (firstCell == null) continue;

            var cellProperties = firstCell.GetFirstChild<TableCellProperties>();
            if (cellProperties == null)
            {
                cellProperties = new TableCellProperties();
                firstCell.PrependChild(cellProperties);
            }

            var cellWidth = cellProperties.GetFirstChild<TableCellWidth>();
            if (cellWidth == null)
            {
                cellWidth = new TableCellWidth();
                cellProperties.Append(cellWidth);
            }

            cellWidth.Type = TableWidthUnitValues.Dxa;
            cellWidth.Width = sequenceColumnWidth;
        }
    }

    private static void SetTableCellText(TableCell cell, string value)
    {
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault() ?? new Paragraph();
        if (paragraph.Parent == null) cell.Append(paragraph);

        var paragraphProperties = paragraph.ParagraphProperties?.CloneNode(true);
        var runProperties = paragraph.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        cell.RemoveAllChildren<Paragraph>();

        var replacement = new Paragraph();
        if (paragraphProperties != null) replacement.Append(paragraphProperties);
        var run = new Run();
        runProperties ??= new RunProperties();
        runProperties.RunFonts = new RunFonts
        {
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = "Times New Roman",
            ComplexScript = "Times New Roman"
        };
        run.Append(runProperties);
        AppendTextWithTabs(run, value);
        replacement.Append(run);
        cell.Append(replacement);
    }

    private static void ExpandDecisionLists(OpenXmlElement? root, IEnumerable<HsplPerson> people)
    {
        var personnel = people.ToList();
        ExpandDecisionList(root, "{{DS_BCH_QD}}", personnel);
        ExpandDecisionList(root, "{{DS_ATLD_QD}}", personnel.Where(IsAtldRole));
    }

    private static void ExpandDecisionList(OpenXmlElement? root, string marker, IEnumerable<HsplPerson> people)
    {
        if (root == null) return;

        foreach (var markerParagraph in root.Descendants<Paragraph>()
                     .Where(paragraph => paragraph.InnerText.Contains(marker, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            foreach (var person in people)
            {
                var paragraph = (Paragraph)markerParagraph.CloneNode(true);
                SetDecisionParagraphText(paragraph, person);
                markerParagraph.InsertBeforeSelf(paragraph);
            }

            markerParagraph.Remove();
        }
    }

    private static void SetDecisionParagraphText(Paragraph paragraph, HsplPerson person)
    {
        var paragraphProperties = paragraph.ParagraphProperties?.CloneNode(true) as ParagraphProperties ?? new ParagraphProperties();
        paragraphProperties.RemoveAllChildren<Tabs>();
        paragraphProperties.Append(new Tabs(new TabStop { Val = TabStopValues.Left, Position = 5000 }));
        paragraphProperties.Justification = new Justification { Val = JustificationValues.Left };
        var runProperties = paragraph.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;

        foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();
        if (paragraph.ParagraphProperties != null)
        {
            paragraph.ReplaceChild(paragraphProperties, paragraph.ParagraphProperties);
        }
        else
        {
            paragraph.PrependChild(paragraphProperties);
        }

        var nameRun = new Run();
        nameRun.Append(CreateTimesNewRomanProperties(runProperties));
        nameRun.Append(new Text($"Ông: {person.FullName}") { Space = SpaceProcessingModeValues.Preserve });
        nameRun.Append(new TabChar());
        paragraph.Append(nameRun);

        var positionRun = new Run();
        positionRun.Append(CreateTimesNewRomanProperties(runProperties));
        positionRun.Append(new Text($"Chức vụ: {person.Position}") { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(positionRun);
    }

    private static RunProperties CreateTimesNewRomanProperties(OpenXmlElement? source)
    {
        var properties = source?.CloneNode(true) as RunProperties ?? new RunProperties();
        properties.RunFonts = new RunFonts
        {
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = "Times New Roman",
            ComplexScript = "Times New Roman"
        };
        return properties;
    }

    private static void InsertOrganizationCharts(MainDocumentPart? mainPart, IEnumerable<HsplPerson> people)
    {
        var personnel = people.ToList();
        InsertOrganizationChart(mainPart, "{{SO_DO_TO_CHUC}}", personnel);
        InsertOrganizationChart(mainPart, "{{SO_DO_ATLD}}", personnel.Where(IsAtldRole));
    }

    private static void InsertOrganizationChart(MainDocumentPart? mainPart, string marker, IEnumerable<HsplPerson> people)
    {
        var body = mainPart?.Document?.Body;
        if (mainPart == null || body == null) return;

        var chartBytes = OrganizationChartRenderer.Render(people);
        if (chartBytes.Length == 0) return;

        foreach (var markerParagraph in body.Descendants<Paragraph>()
                     .Where(paragraph => paragraph.InnerText.Contains(marker, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = new MemoryStream(chartBytes)) imagePart.FeedData(imageStream);
            var relationshipId = mainPart.GetIdOfPart(imagePart);

            markerParagraph.RemoveAllChildren<Run>();
            markerParagraph.Append(new Run(CreateOrganizationChartDrawing(relationshipId, 680, 420)));
        }
    }

    private static WordDrawing CreateOrganizationChartDrawing(string relationshipId, int widthPixels, int heightPixels)
    {
        const long emusPerPixel = 9525;
        var width = widthPixels * emusPerPixel;
        var height = heightPixels * emusPerPixel;

        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "So do to chuc.png" },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip { Embed = relationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var graphicData = new A.GraphicData(picture)
        {
            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
        };
        var inline = new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "So do to chuc" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(graphicData))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };
        return new WordDrawing(inline);
    }

    private static bool IsAtldRole(HsplPerson person)
    {
        var position = person.Position.Trim();
        return position.Contains("ATLĐ", StringComparison.OrdinalIgnoreCase)
            || position.Contains("ATLD", StringComparison.OrdinalIgnoreCase)
            || position.Contains("an toàn lao động", StringComparison.OrdinalIgnoreCase)
            || position.Contains("vệ sinh môi trường", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplacePart(OpenXmlElement? root, IReadOnlyDictionary<string, string> replacements)
    {
        if (root == null) return;
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
            if (!replacements.Keys.Any(key => text.Contains(key, StringComparison.OrdinalIgnoreCase))) continue;

            // Replace inside each Word run so text before and after a placeholder preserves its own formatting.
            // Exact matches preserve intentional casing, e.g. {TenNTP} versus {TENNTP}.
            foreach (var item in replacements)
                ReplaceTextRuns(paragraph, item.Key, item.Value, StringComparison.Ordinal);

            // Retain tolerant matching only for placeholders that do not have a case-specific variant.
            foreach (var item in replacements.Where(item => replacements.Keys.Count(key => string.Equals(key, item.Key, StringComparison.OrdinalIgnoreCase)) == 1))
                ReplaceTextRuns(paragraph, item.Key, item.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ReplaceTextRuns(Paragraph paragraph, string placeholder, string value, StringComparison comparison)
    {
        while (true)
        {
            var texts = paragraph.Descendants<Text>().ToList();
            var combinedText = string.Concat(texts.Select(text => text.Text));
            var matchIndex = combinedText.IndexOf(placeholder, comparison);
            if (matchIndex < 0) return;

            var matchEnd = matchIndex + placeholder.Length;
            var currentOffset = 0;
            var startTextIndex = -1;
            var endTextIndex = -1;
            var startOffset = 0;
            var endOffset = 0;

            for (var index = 0; index < texts.Count; index++)
            {
                var textLength = texts[index].Text.Length;
                if (startTextIndex < 0 && matchIndex >= currentOffset && matchIndex < currentOffset + textLength)
                {
                    startTextIndex = index;
                    startOffset = matchIndex - currentOffset;
                }
                if (matchEnd > currentOffset && matchEnd <= currentOffset + textLength)
                {
                    endTextIndex = index;
                    endOffset = matchEnd - currentOffset;
                    break;
                }
                currentOffset += textLength;
            }

            if (startTextIndex < 0 || endTextIndex < 0) return;

            if (value.Contains('\n') || value.Contains('\t'))
            {
                ReplaceMultilineText(paragraph, texts, startTextIndex, endTextIndex, startOffset, endOffset, value);
                return;
            }

            var startText = texts[startTextIndex];
            if (startTextIndex == endTextIndex)
            {
                startText.Text = startText.Text.Remove(startOffset, placeholder.Length).Insert(startOffset, value);
                startText.Space = SpaceProcessingModeValues.Preserve;
                continue;
            }

            // Preserve the style before/after the token; the replacement takes the first token run's style.
            startText.Text = startText.Text[..startOffset] + value;
            startText.Space = SpaceProcessingModeValues.Preserve;
            for (var index = startTextIndex + 1; index < endTextIndex; index++)
                texts[index].Text = string.Empty;

            var endText = texts[endTextIndex];
            endText.Text = endText.Text[endOffset..];
            endText.Space = SpaceProcessingModeValues.Preserve;
        }
    }

    // A representative list must become real Word paragraphs so each person keeps a readable line.
    private static void ReplaceMultilineText(
        Paragraph paragraph,
        IReadOnlyList<Text> texts,
        int startTextIndex,
        int endTextIndex,
        int startOffset,
        int endOffset,
        string value)
    {
        var lines = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        var prefix = texts[startTextIndex].Text[..startOffset];
        var suffix = texts[endTextIndex].Text[endOffset..];
        var insertedAfter = paragraph;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var targetParagraph = lineIndex == 0
                ? paragraph
                : (Paragraph)paragraph.CloneNode(true);

            var targetTexts = targetParagraph.Descendants<Text>().ToList();
            foreach (var targetText in targetTexts)
            {
                targetText.Text = string.Empty;
                targetText.Space = SpaceProcessingModeValues.Preserve;
            }

            SetTextWithTabs(
                targetTexts[startTextIndex],
                (lineIndex == 0 ? prefix : string.Empty) +
                lines[lineIndex] +
                (lineIndex == lines.Length - 1 ? suffix : string.Empty));

            if (lineIndex > 0)
            {
                insertedAfter.InsertAfterSelf(targetParagraph);
                insertedAfter = targetParagraph;
            }
        }
    }

    private static void SetTextWithTabs(Text text, string value)
    {
        if (text.Parent is not Run run)
        {
            text.Text = value.Replace("\t", "    ");
            text.Space = SpaceProcessingModeValues.Preserve;
            return;
        }

        var runProperties = run.RunProperties?.CloneNode(true) as RunProperties;
        run.RemoveAllChildren();
        if (runProperties != null) run.Append(runProperties);
        if (value.Contains('\t'))
            EnsureRepresentativeTabStop(run.Ancestors<Paragraph>().FirstOrDefault());
        AppendTextWithTabs(run, value);
    }

    private static void EnsureRepresentativeTabStop(Paragraph? paragraph)
    {
        if (paragraph == null) return;

        var properties = paragraph.ParagraphProperties;
        if (properties == null)
        {
            properties = new ParagraphProperties();
            paragraph.PrependChild(properties);
        }

        var tabs = properties.GetFirstChild<Tabs>();
        if (tabs == null)
        {
            tabs = new Tabs();
            properties.Append(tabs);
        }

        if (!tabs.Elements<TabStop>().Any(tab => tab.Position?.Value == RepresentativePositionTabStop))
        {
            tabs.Append(new TabStop
            {
                Val = TabStopValues.Left,
                Position = RepresentativePositionTabStop
            });
        }
    }

    private static void AppendTextWithTabs(Run run, string value)
    {
        var segments = value.Split('\t');
        for (var index = 0; index < segments.Length; index++)
        {
            if (index > 0) run.Append(new TabChar());
            run.Append(new Text(segments[index]) { Space = SpaceProcessingModeValues.Preserve });
        }
    }
}
