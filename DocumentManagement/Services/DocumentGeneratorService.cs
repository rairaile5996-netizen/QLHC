using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentManagement.Converters;
using DocumentManagement.Models;
using DocumentManagement.Utilities;
using ContractDocument = DocumentManagement.Models.Document;

namespace DocumentManagement.Services;

/// <summary>
/// ThÃ´ng tin tá»•ng tháº§u lÆ°u trong contractors.json
/// </summary>
public class ContractorInfo
{
    public string Code { get; set; } = string.Empty;           // MÃ£ viáº¿t táº¯t: THD, OLP, OCC, ADC
    public string FullName { get; set; } = string.Empty;       // TÃªn Ä‘áº§y Ä‘á»§ cÃ´ng ty
    public string Director { get; set; } = string.Empty;       // TÃªn giÃ¡m Ä‘á»‘c
    public string Address { get; set; } = string.Empty;        // Äá»‹a chá»‰
    public string TaxCode { get; set; } = string.Empty;        // MÃ£ sá»‘ thuáº¿
    public string Phone { get; set; } = string.Empty;          // Sá»‘ Ä‘iá»‡n thoáº¡i
    public string BankAccount { get; set; } = string.Empty;    // Sá»‘ tÃ i khoáº£n
    public string BankName { get; set; } = string.Empty;       // TÃªn ngÃ¢n hÃ ng
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

/// <summary>
/// Service táº¡o cÃ¡c loáº¡i cÃ´ng vÄƒn tá»« template Word
/// ThÆ° má»¥c Templates náº±m CÃ™NG THÆ¯ Má»¤C Vá»šI FILE EXE Ä‘á»ƒ dá»… dÃ ng copy sang mÃ¡y khÃ¡c
/// </summary>
public partial class DocumentGeneratorService
{
    public enum DocumentType
    {
        PaymentRequest,      // CÃ´ng vÄƒn thanh toÃ¡n
        ContractLiquidation, // Thanh lÃ½ há»£p Ä‘á»“ng  
        HandoverReport,      // BiÃªn báº£n bÃ n giao Ä‘Æ°a vÃ o sá»­ dá»¥ng
        LoadTest             // LiÃªn Ä‘á»™ng cÃ³ táº£i
    }

    private readonly string _templateFolder;
    private readonly string _outputFolder;
    private readonly string _contractorsJsonPath;
    private readonly AIService? _aiService;
    private Dictionary<string, ContractorInfo> _contractors = [];

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // TÃªn file template cho má»—i loáº¡i cÃ´ng vÄƒn
    private static readonly Dictionary<DocumentType, string> TemplateFileNames = new()
    {
        { DocumentType.PaymentRequest, "CongVanThanhToan.docx" },
        { DocumentType.ContractLiquidation, "ThanhLyHopDong.docx" },
        { DocumentType.HandoverReport, "BienBanBanGiao.docx" },
        { DocumentType.LoadTest, "LienDongCoTai.docx" }
    };

    public DocumentGeneratorService(AIService? aiService = null)
    {
        // Templates náº±m CÃ™NG THÆ¯ Má»¤C Vá»šI FILE EXE
        var appFolder = AppDomain.CurrentDomain.BaseDirectory;
        _templateFolder = Path.Combine(appFolder, "Templates");
        _outputFolder = Path.Combine(appFolder, "Generated");
        _contractorsJsonPath = Path.Combine(_templateFolder, "contractors.json");
        _aiService = aiService;

        InitializeTemplateStructure();
        LoadContractorsFromJson();
    }

    #region Contractors Management
    /// <summary>
    /// Load thÃ´ng tin tá»•ng tháº§u tá»« contractors.json
    /// </summary>
    private void LoadContractorsFromJson()
    {
        try
        {
            if (File.Exists(_contractorsJsonPath))
            {
                var json = File.ReadAllText(_contractorsJsonPath);
                _contractors = JsonSerializer.Deserialize<Dictionary<string, ContractorInfo>>(json) ?? [];
            }
        }
        catch { _contractors = []; }
    }

    /// <summary>
    /// LÆ°u thÃ´ng tin tá»•ng tháº§u vÃ o contractors.json
    /// </summary>
    private void SaveContractorsToJson()
    {
        try
        {
            var json = JsonSerializer.Serialize(_contractors, _jsonOptions);
            File.WriteAllText(_contractorsJsonPath, json);
        }
        catch { }
    }

    /// <summary>
    /// TrÃ­ch mÃ£ tá»•ng tháº§u tá»« sá»‘ há»£p Ä‘á»“ng
    /// VÃ­ dá»¥: "001/2024/HÄ/THD-KHC" â†’ "THD"
    /// </summary>
    public static string ExtractContractorCode(string contractNumber)
    {
        if (string.IsNullOrEmpty(contractNumber)) return "";

        // Pattern: xxx/nÄƒm/HÄ/MÃƒ-KHC
        var match = KhcRegex().Match(contractNumber);
        if (match.Success)
            return match.Groups[1].Value.ToUpper();

        // Fallback: tÃ¬m mÃ£ viáº¿t táº¯t sau dáº¥u /
        var parts = contractNumber.Split('/');
        if (parts.Length >= 4)
        {
            var lastPart = parts[^1]; // Pháº§n cuá»‘i: THD-KHC
            var codePart = lastPart.Split('-')[0];
            if (codePart.Length <= 5 && codePart.All(char.IsLetter))
                return codePart.ToUpper();
        }

        return "";
    }

    /// <summary>
    /// Láº¥y thÃ´ng tin tá»•ng tháº§u theo mÃ£
    /// </summary>
    public ContractorInfo? GetContractorInfo(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        return _contractors.TryGetValue(code.ToUpper(), out var info) ? info : null;
    }

    /// <summary>
    /// Cáº­p nháº­t thÃ´ng tin tá»•ng tháº§u
    /// </summary>
    public void UpdateContractorInfo(ContractorInfo info)
    {
        if (string.IsNullOrEmpty(info.Code)) return;
        info.LastUpdated = DateTime.Now;
        _contractors[info.Code.ToUpper()] = info;
        SaveContractorsToJson();
    }

    /// <summary>
    /// XÃ³a thÃ´ng tin tá»•ng tháº§u
    /// </summary>
    public void DeleteContractorInfo(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        if (_contractors.Remove(code.ToUpper()))
        {
            SaveContractorsToJson();
        }
    }

    /// <summary>
    /// Láº¥y táº¥t cáº£ thÃ´ng tin tá»•ng tháº§u
    /// </summary>
    public IEnumerable<ContractorInfo> GetAllContractors()
    {
        return _contractors.Values;
    }

    /// <summary>
    /// DÃ¹ng AI Ä‘á»c file Word há»£p Ä‘á»“ng Ä‘á»ƒ láº¥y thÃ´ng tin tá»•ng tháº§u
    /// </summary>
    public async Task<ContractorInfo?> ExtractContractorInfoFromWordAsync(string wordFilePath, string contractorCode)
    {
        if (_aiService == null || !File.Exists(wordFilePath))
            return null;

        try
        {
            var wordText = await ExtractTextFromWordAsync(wordFilePath);
            if (string.IsNullOrEmpty(wordText)) return null;

            var prompt = $@"Äá»c ná»™i dung há»£p Ä‘á»“ng sau vÃ  trÃ­ch xuáº¥t thÃ´ng tin cá»§a BÃŠN A (tá»•ng tháº§u/chá»§ Ä‘áº§u tÆ°).
Tráº£ vá» JSON vá»›i format:
{{
  ""FullName"": ""TÃªn cÃ´ng ty Ä‘áº§y Ä‘á»§"",
  ""Director"": ""TÃªn giÃ¡m Ä‘á»‘c/ngÆ°á»i Ä‘áº¡i diá»‡n"",
  ""Address"": ""Äá»‹a chá»‰"",
  ""TaxCode"": ""MÃ£ sá»‘ thuáº¿"",
  ""Phone"": ""Sá»‘ Ä‘iá»‡n thoáº¡i"",
  ""BankAccount"": ""Sá»‘ tÃ i khoáº£n ngÃ¢n hÃ ng"",
  ""BankName"": ""TÃªn ngÃ¢n hÃ ng""
}}

Náº¿u khÃ´ng tÃ¬m tháº¥y thÃ´ng tin nÃ o, Ä‘á»ƒ trá»‘ng """".
CHá»ˆ TRáº¢ Vá»€ JSON, KHÃ”NG GIáº¢I THÃCH.

Ná»™i dung há»£p Ä‘á»“ng:
{wordText[..Math.Min(4000, wordText.Length)]}";

            var response = await _aiService.SendChatAsync(prompt);

            // Parse JSON tá»« response
            var jsonMatch = JsonBracketRegex().Match(response);
            if (jsonMatch.Success)
            {
                var info = JsonSerializer.Deserialize<ContractorInfo>(jsonMatch.Value);
                if (info != null)
                {
                    info.Code = contractorCode.ToUpper();
                    info.LastUpdated = DateTime.Now;

                    // LÆ°u vÃ o cache
                    _contractors[info.Code] = info;
                    SaveContractorsToJson();

                    return info;
                }
            }
        }
        catch { }

        return null;
    }
    #endregion

    #region Excel Reading
    /// <summary>
    /// Äá»c giÃ¡ trá»‹ Ä‘á» nghá»‹ thanh toÃ¡n tá»« file Excel
    /// Sheet: GTQT hoáº·c "GiÃ¡ trá»‹ QT"
    /// Cá»™t B: tÃ¬m dÃ²ng "giÃ¡ trá»‹ Ä‘á» nghá»‹ thanh toÃ¡n"
    /// Cá»™t C: láº¥y giÃ¡ trá»‹ tÆ°Æ¡ng á»©ng
    /// Returns: (Success, Value, Description)
    /// </summary>
    public static (bool Success, decimal Value, string? Description) ReadPaymentValueFromExcel(string excelFilePath)
    {
        if (!File.Exists(excelFilePath)) return (false, 0, null);

        string? tempFile = null;
        try
        {
            // Copy file táº¡m Ä‘á»ƒ trÃ¡nh lá»—i khi file Ä‘ang má»Ÿ bá»Ÿi Excel
            tempFile = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}.xlsx");
            File.Copy(excelFilePath, tempFile, true);

            using var workbook = new XLWorkbook(tempFile);

            // TÃ¬m sheet GTQT, GTTT, "GiÃ¡ trá»‹ QT", "GiÃ¡ trá»‹ TT"
            IXLWorksheet? worksheet = null;
            foreach (var ws in workbook.Worksheets)
            {
                var name = ws.Name.ToLower().Trim();
                if (name == "gtqt" || name == "gttt" ||
                    name.Contains("giÃ¡ trá»‹ qt") || name.Contains("gia tri qt") ||
                    name.Contains("giÃ¡ trá»‹ tt") || name.Contains("gia tri tt"))
                {
                    worksheet = ws;
                    break;
                }
            }

            // Náº¿u khÃ´ng tÃ¬m tháº¥y, dÃ¹ng sheet Ä‘áº§u tiÃªn
            worksheet ??= workbook.Worksheets.First();

            // Duyá»‡t cá»™t B tÃ¬m giÃ¡ trá»‹ thanh toÃ¡n
            foreach (var row in worksheet.RowsUsed())
            {
                var cellB = row.Cell("B").GetString().ToLower().Trim();

                // TÃ¬m cÃ¡c keyword liÃªn quan Ä‘áº¿n thanh toÃ¡n
                if (cellB.Contains("thanh toÃ¡n láº§n nÃ y") ||
                    cellB.Contains("thanh toan lan nay") ||
                    cellB.Contains("thanh toÃ¡n Ä‘á»£t nÃ y") ||
                    cellB.Contains("thanh toan dot nay") ||
                    cellB.Contains("giÃ¡ trá»‹ Ä‘á» nghá»‹ thanh toÃ¡n") ||
                    cellB.Contains("gia tri de nghi thanh toan") ||
                    cellB.Contains("Ä‘á» nghá»‹ thanh toÃ¡n") ||
                    cellB.Contains("de nghi thanh toan") ||
                    (cellB.Contains("thanh toÃ¡n") && cellB.Contains("láº§n")) ||
                    (cellB.Contains("thanh toan") && cellB.Contains("lan")))
                {
                    var cellC = row.Cell("C");
                    decimal extractedValue = 0;

                    // Thá»­ láº¥y giÃ¡ trá»‹ sá»‘
                    if (cellC.TryGetValue<decimal>(out var value) && value > 0)
                        extractedValue = value;

                    // Thá»­ láº¥y giÃ¡ trá»‹ double
                    else if (cellC.TryGetValue<double>(out var dValue) && dValue > 0)
                        extractedValue = (decimal)dValue;

                    // Thá»­ parse string (loáº¡i bá» dáº¥u cháº¥m phÃ¢n cÃ¡ch hÃ ng nghÃ¬n)
                    else
                    {
                        var strValue = cellC.GetString();
                        if (VietnameseFormat.TryParseMoney(strValue, out var parsedValue) && parsedValue > 0)
                            extractedValue = parsedValue;
                    }

                    if (extractedValue > 0)
                    {
                        return (true, extractedValue, row.Cell("B").GetString().Trim());
                    }
                }
            }
        }
        catch { }
        finally
        {
            // XÃ³a file táº¡m
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        return (false, 0, null);
    }
    #endregion

    #region Payment Request Generation
    /// <summary>
    /// Táº¡o cÃ´ng vÄƒn thanh toÃ¡n tá»« file Excel (sync version Ä‘á»ƒ trÃ¡nh deadlock)
    /// </summary>
    public (bool Success, string FilePath, string Message) GeneratePaymentRequest(
        ContractDocument contract,
        string excelFilePath)
    {
        try
        {
            // 1. Äá»c giÃ¡ trá»‹ tá»« Excel
            var (success, paymentValue, _) = ReadPaymentValueFromExcel(excelFilePath);
            if (!success || paymentValue <= 0)
                return (false, "", "KhÃ´ng tÃ¬m tháº¥y giÃ¡ trá»‹ Ä‘á» nghá»‹ thanh toÃ¡n trong file Excel.\nKiá»ƒm tra sheet GTQT hoáº·c 'GiÃ¡ trá»‹ QT' cÃ³ dÃ²ng 'giÃ¡ trá»‹ Ä‘á» nghá»‹ thanh toÃ¡n' á»Ÿ cá»™t B khÃ´ng.");

            return GeneratePaymentRequestInternal(contract, excelFilePath, paymentValue, null);
        }
        catch (Exception ex)
        {
            return (false, "", $"Lá»—i: {ex.Message}");
        }
    }

    /// <summary>
    /// Táº¡o cÃ´ng vÄƒn thanh toÃ¡n vá»›i giÃ¡ trá»‹ vÃ  mÃ´ táº£ Ä‘Ã£ chá»‰nh sá»­a tá»« dialog preview
    /// </summary>
    public (bool Success, string FilePath, string Message) GeneratePaymentRequest(
        ContractDocument contract,
        string excelFilePath,
        decimal paymentValue,
        string? description)
    {
        try
        {
            return GeneratePaymentRequestInternal(contract, excelFilePath, paymentValue, description);
        }
        catch (Exception ex)
        {
            return (false, "", $"Lá»—i: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method Ä‘á»ƒ táº¡o cÃ´ng vÄƒn thanh toÃ¡n
    /// </summary>
    private (bool Success, string FilePath, string Message) GeneratePaymentRequestInternal(
        ContractDocument contract,
        string excelFilePath,
        decimal paymentValue,
        string? description)
    {
        // 1. TrÃ­ch mÃ£ tá»•ng tháº§u tá»« sá»‘ HÄ
        var contractorCode = ExtractContractorCode(contract.ContractNumber);

        // 2. Láº¥y thÃ´ng tin tá»•ng tháº§u tá»« cache
        var contractorInfo = GetContractorInfo(contractorCode);

        // Náº¿u chÆ°a cÃ³ thÃ´ng tin, táº¡o má»›i vá»›i tÃªn = mÃ£
        if (contractorInfo == null && !string.IsNullOrEmpty(contractorCode))
        {
            contractorInfo = new ContractorInfo
            {
                Code = contractorCode,
                FullName = contractorCode
            };
        }

        // 3. Chuáº©n bá»‹ placeholder data
        var placeholders = new Dictionary<string, string>
        {
            ["{{SoHopDong}}"] = contract.ContractNumber,
            ["{{NgayKy}}"] = contract.SignDate.ToString("dd/MM/yyyy"),
            ["{{TenDuAn}}"] = contract.ProjectName,
            ["{{HangMuc}}"] = contract.ItemName,
            ["{{GiaTriThanhToan}}"] = paymentValue.ToString("N0"),
            ["{{BangChu}}"] = NumberToVietnameseWords.Convert(paymentValue),
            ["{{MaTongThau}}"] = contractorCode,
            ["{{TenTongThau}}"] = contractorInfo?.FullName ?? contractorCode,
            ["{{GiamDoc}}"] = contractorInfo?.Director ?? "",
            ["{{DiaChi}}"] = contractorInfo?.Address ?? "",
            ["{{MaSoThue}}"] = contractorInfo?.TaxCode ?? "",
            ["{{SoDienThoai}}"] = contractorInfo?.Phone ?? "",
            ["{{SoTaiKhoan}}"] = contractorInfo?.BankAccount ?? "",
            ["{{NganHang}}"] = contractorInfo?.BankName ?? "",
            ["{{Ngay}}"] = DateTime.Now.Day.ToString(),
            ["{{Thang}}"] = DateTime.Now.Month.ToString(),
            ["{{Nam}}"] = DateTime.Now.Year.ToString(),
            ["{{NgayHienTai}}"] = DateTime.Now.ToString("dd/MM/yyyy"),
            ["{{NoiDung}}"] = description ?? $"Thanh toÃ¡n - {contract.ItemName}"
        };

        // 4. TÃ¬m template
        var templatePath = GetTemplatePath(DocumentType.PaymentRequest, contractorCode);

        // 5. Táº¡o file output (cÃ¹ng thÆ° má»¥c vá»›i file Excel)
        var outputFolder = Path.GetDirectoryName(excelFilePath) ?? _outputFolder;
        var outputFileName = $"CV_ThanhToan_{contract.ContractNumber.Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
        var outputPath = Path.Combine(outputFolder, outputFileName);

        // 6. Táº¡o file
        if (File.Exists(templatePath))
        {
            GenerateFromTemplate(templatePath, outputPath, placeholders);
        }
        else
        {
            GenerateDefaultPaymentRequest(outputPath, placeholders);
        }

        return (true, outputPath, $"ÄÃ£ táº¡o cÃ´ng vÄƒn thanh toÃ¡n: {outputFileName}");
    }

    /// <summary>
    /// Táº¡o cÃ´ng vÄƒn thanh toÃ¡n tá»« file Excel (async version)
    /// </summary>
    public async Task<(bool Success, string FilePath, string Message)> GeneratePaymentRequestAsync(
        ContractDocument contract,
        string excelFilePath)
    {
        return await Task.Run(() => GeneratePaymentRequest(contract, excelFilePath)).ConfigureAwait(false);
    }

    /// <summary>
    /// Táº¡o file tá»« template (sync)
    /// </summary>
    private static void GenerateFromTemplate(string templatePath, string outputPath, Dictionary<string, string> placeholders)
    {
        File.Copy(templatePath, outputPath, true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return;

        // HÃ m thay tháº¿ placeholder trong má»™t element (xá»­ lÃ½ trÆ°á»ng há»£p placeholder bá»‹ tÃ¡ch thÃ nh nhiá»u Run)
        void ReplaceInElement(OpenXmlElement element)
        {
            // Láº¥y táº¥t cáº£ paragraph
            foreach (var para in element.Descendants<Paragraph>())
            {
                // Gá»™p táº¥t cáº£ text trong paragraph
                var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));

                // Kiá»ƒm tra cÃ³ placeholder nÃ o khÃ´ng
                var hasPlaceholder = placeholders.Keys.Any(k => fullText.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (!hasPlaceholder) continue;

                // Thay tháº¿ táº¥t cáº£ placeholder
                foreach (var kv in placeholders)
                {
                    fullText = fullText.Replace(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
                }

                // XÃ³a táº¥t cáº£ text cÅ© vÃ  thay báº±ng text má»›i
                var texts = para.Descendants<Text>().ToList();
                if (texts.Count > 0)
                {
                    texts[0].Text = fullText;
                    for (int i = 1; i < texts.Count; i++)
                    {
                        texts[i].Text = "";
                    }
                }
            }
        }

        // Thay tháº¿ trong body
        ReplaceInElement(body);

        // Thay tháº¿ trong header
        foreach (var headerPart in doc.MainDocumentPart?.HeaderParts ?? [])
        {
            ReplaceInElement(headerPart.Header);
        }

        // Thay tháº¿ trong footer
        foreach (var footerPart in doc.MainDocumentPart?.FooterParts ?? [])
        {
            ReplaceInElement(footerPart.Footer);
        }

        doc.MainDocumentPart?.Document.Save();
    }

    /// <summary>
    /// Táº¡o cÃ´ng vÄƒn thanh toÃ¡n máº«u (sync - khi khÃ´ng cÃ³ template)
    /// </summary>
    private static void GenerateDefaultPaymentRequest(string outputPath, Dictionary<string, string> data)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddParagraph(body, "Cá»˜NG HÃ’A XÃƒ Há»˜I CHá»¦ NGHÄ¨A VIá»†T NAM", true, JustificationValues.Center, "14");
        AddParagraph(body, "Äá»™c láº­p - Tá»± do - Háº¡nh phÃºc", false, JustificationValues.Center, "13", true);
        AddParagraph(body, "---o0o---", false, JustificationValues.Center);
        AddEmptyParagraph(body);

        AddParagraph(body, "CÃ”NG VÄ‚N Äá»€ NGHá»Š THANH TOÃN", true, JustificationValues.Center, "16");
        AddEmptyParagraph(body);

        AddParagraph(body, $"KÃ­nh gá»­i: {data.GetValueOrDefault("{{TenTongThau}}", "[TÃªn tá»•ng tháº§u]")}");
        AddEmptyParagraph(body);

        AddParagraph(body, $"CÄƒn cá»© Há»£p Ä‘á»“ng sá»‘: {data.GetValueOrDefault("{{SoHopDong}}", "")} kÃ½ ngÃ y {data.GetValueOrDefault("{{NgayKy}}", "")}");
        AddParagraph(body, $"Dá»± Ã¡n: {data.GetValueOrDefault("{{TenDuAn}}", "")}");
        AddParagraph(body, $"Háº¡ng má»¥c: {data.GetValueOrDefault("{{HangMuc}}", "")}");
        AddEmptyParagraph(body);

        AddParagraph(body, "ChÃºng tÃ´i Ä‘á» nghá»‹ thanh toÃ¡n sá»‘ tiá»n:");
        AddParagraph(body, $"Sá»‘ tiá»n: {data.GetValueOrDefault("{{GiaTriThanhToan}}", "0")} VNÄ", true);
        AddParagraph(body, $"Báº±ng chá»¯: {data.GetValueOrDefault("{{BangChu}}", "")}", false, null, "13", true);
        AddEmptyParagraph(body);

        AddParagraph(body, "KÃ­nh Ä‘á» nghá»‹ QuÃ½ cÃ´ng ty xem xÃ©t vÃ  thanh toÃ¡n.");
        AddEmptyParagraph(body);
        AddEmptyParagraph(body);

        // Chá»¯ kÃ½
        var table = new Table();
        var tableRow = new TableRow();

        var cell1 = new TableCell(new Paragraph(new Run(new Text(""))));
        cell1.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "50" }));

        var cell2 = new TableCell();
        cell2.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "50" }));
        AddParagraphToCell(cell2, $"NgÃ y {data.GetValueOrDefault("{{Ngay}}", "")} thÃ¡ng {data.GetValueOrDefault("{{Thang}}", "")} nÄƒm {data.GetValueOrDefault("{{Nam}}", "")}", false, JustificationValues.Center);
        AddParagraphToCell(cell2, "GIÃM Äá»C", true, JustificationValues.Center);

        tableRow.Append(cell1, cell2);
        table.Append(tableRow);
        body.Append(table);

        mainPart.Document.Save();
    }

    private static void AddParagraphToCell(TableCell cell, string text, bool bold = false, JustificationValues? align = null)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        if (align.HasValue) pPr.Append(new Justification { Val = align.Value });
        para.Append(pPr);

        var run = new Run();
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
        rPr.Append(new FontSize { Val = "26" });
        if (bold) rPr.Append(new Bold());
        run.Append(rPr);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        para.Append(run);
        cell.Append(para);
    }
    #endregion

    /// <summary>
    /// Khá»Ÿi táº¡o cáº¥u trÃºc thÆ° má»¥c template
    /// </summary>
    private void InitializeTemplateStructure()
    {
        if (!Directory.Exists(_templateFolder))
            Directory.CreateDirectory(_templateFolder);
        if (!Directory.Exists(_outputFolder))
            Directory.CreateDirectory(_outputFolder);

        // Táº¡o thÆ° má»¥c Default
        var defaultFolder = Path.Combine(_templateFolder, "Default");
        if (!Directory.Exists(defaultFolder))
            Directory.CreateDirectory(defaultFolder);

        // Táº¡o file hÆ°á»›ng dáº«n náº¿u chÆ°a cÃ³
        var readmePath = Path.Combine(_templateFolder, "README.txt");
        if (!File.Exists(readmePath))
        {
            File.WriteAllText(readmePath, @"HÆ¯á»šNG DáºªN Sá»¬ Dá»¤NG THÆ¯ Má»¤C TEMPLATES
=====================================

1. Cáº¤U TRÃšC THÆ¯ Má»¤C:
   Templates/
   â”œâ”€â”€ CongVanThanhToan.docx   <- TEMPLATE CHUNG cho táº¥t cáº£ tá»•ng tháº§u
   â”œâ”€â”€ EVN/                    <- Tá»•ng tháº§u EVN
   â”‚   â”œâ”€â”€ ThanhLyHopDong.docx
   â”‚   â”œâ”€â”€ BienBanBanGiao.docx
   â”‚   â””â”€â”€ LienDongCoTai.docx
   â”œâ”€â”€ PECC/                   <- Tá»•ng tháº§u PECC
   â”‚   â”œâ”€â”€ ThanhLyHopDong.docx
   â”‚   â””â”€â”€ ...
   â”œâ”€â”€ Default/                <- Template máº·c Ä‘á»‹nh (fallback)
   â”‚   â”œâ”€â”€ ThanhLyHopDong.docx
   â”‚   â””â”€â”€ ...
   â””â”€â”€ contractors.txt         <- File cáº¥u hÃ¬nh nháº­n diá»‡n tá»•ng tháº§u

2. QUY Táº®C TEMPLATE:
   - CÃ”NG VÄ‚N THANH TOÃN: DÃ¹ng 1 template CHUNG á»Ÿ thÆ° má»¥c gá»‘c (Templates/CongVanThanhToan.docx)
     â†’ Táº¥t cáº£ tá»•ng tháº§u dÃ¹ng chung, chá»‰ thay Ä‘á»•i thÃ´ng tin
   - THANH LÃ Há»¢P Äá»’NG: Má»—i tá»•ng tháº§u cÃ³ template RIÃŠNG (Templates/[Tá»•ngTháº§u]/ThanhLyHopDong.docx)
   - BIÃŠN Báº¢N BÃ€N GIAO: Má»—i tá»•ng tháº§u cÃ³ template RIÃŠNG
   - LIÃŠN Äá»˜NG CÃ“ Táº¢I: Má»—i tá»•ng tháº§u cÃ³ template RIÃŠNG

3. FILE contractors.txt:
   Má»—i dÃ²ng gá»“m: TÃŠN_THÆ¯_Má»¤C|tá»« khÃ³a 1|tá»« khÃ³a 2|...
   VÃ­ dá»¥:
   EVN|Ä‘iá»‡n lá»±c|evn|táº­p Ä‘oÃ n Ä‘iá»‡n lá»±c
   PECC|pecc|tÆ° váº¥n Ä‘iá»‡n
   LILAMA|lilama|láº¯p mÃ¡y

4. PLACEHOLDER TRONG TEMPLATE:
   {{SoHopDong}}      - Sá»‘ há»£p Ä‘á»“ng
   {{NgayKy}}         - NgÃ y kÃ½ (dd/MM/yyyy)
   {{TenDuAn}}        - TÃªn dá»± Ã¡n
   {{HangMuc}}        - Háº¡ng má»¥c
   {{GiaTriHopDong}}  - GiÃ¡ trá»‹ há»£p Ä‘á»“ng
   {{TongGiaTri}}     - Tá»•ng giÃ¡ trá»‹ (HÄ + Phá»¥ lá»¥c)
   {{DaTamUng}}       - ÄÃ£ táº¡m á»©ng
   {{DaThanhToan}}    - ÄÃ£ thanh toÃ¡n
   {{ConLai}}         - CÃ²n láº¡i
   {{BenA}}           - TÃªn bÃªn A (tá»« file Word há»£p Ä‘á»“ng)
   {{BenB}}           - TÃªn bÃªn B
   {{TongThau}}       - TÃªn tá»•ng tháº§u (tá»± nháº­n diá»‡n)
   {{Ngay}}, {{Thang}}, {{Nam}} - NgÃ y thÃ¡ng nÄƒm hiá»‡n táº¡i
   ... vÃ  nhiá»u placeholder khÃ¡c

5. CÃCH Láº¤Y THÃ”NG TIN:
   - Tá»« DATABASE: Sá»‘ HÄ, ngÃ y kÃ½, giÃ¡ trá»‹, táº¡m á»©ng, thanh toÃ¡n...
   - Tá»« FILE WORD Há»¢P Äá»’NG: BÃªn A, BÃªn B, Ä‘á»‹a chá»‰, MST, STK, ngÃ¢n hÃ ng...
   - Tá»« FILE EXCEL: GiÃ¡ trá»‹ chi tiáº¿t, tá»•ng cá»™ng, thuáº¿...
   - AI sáº½ Ä‘á»c file Word trong thÆ° má»¥c 1.INPUT Ä‘á»ƒ nháº­n diá»‡n tá»•ng tháº§u

6. CÃCH HOáº T Äá»˜NG:
   - AI Ä‘á»c file Word há»£p Ä‘á»“ng Ä‘á»ƒ nháº­n diá»‡n tá»•ng tháº§u (theo tá»« khÃ³a hoáº·c AI)
   - Vá»›i CÃ´ng vÄƒn thanh toÃ¡n: DÃ¹ng template chung
   - Vá»›i cÃ¡c loáº¡i khÃ¡c: Chá»n template tá»« thÆ° má»¥c tá»•ng tháº§u tÆ°Æ¡ng á»©ng
   - Náº¿u khÃ´ng tÃ¬m tháº¥y, dÃ¹ng template tá»« thÆ° má»¥c Default
");
        }

        // Táº¡o file contractors.txt máº«u náº¿u chÆ°a cÃ³
        var contractorsPath = Path.Combine(_templateFolder, "contractors.txt");
        if (!File.Exists(contractorsPath))
        {
            File.WriteAllText(contractorsPath, @"# Danh sÃ¡ch tá»•ng tháº§u vÃ  tá»« khÃ³a nháº­n diá»‡n
# Format: TÃŠN_THÆ¯_Má»¤C|tá»« khÃ³a 1|tá»« khÃ³a 2|...
# DÃ²ng báº¯t Ä‘áº§u báº±ng # sáº½ bá»‹ bá» qua

THD|thÃ¡i dÆ°Æ¡ng|thai duong
OLP|olp
OCC|occ
ADC|adc
");
        }

        // Táº¡o file contractors.json máº«u náº¿u chÆ°a cÃ³
        if (!File.Exists(_contractorsJsonPath))
        {
            var sampleContractors = new Dictionary<string, ContractorInfo>
            {
                ["THD"] = new ContractorInfo { Code = "THD", FullName = "CÃ´ng ty TNHH XÃ¢y Dá»±ng DÃ¢n Dá»¥ng ThÃ¡i DÆ°Æ¡ng" },
                ["OLP"] = new ContractorInfo { Code = "OLP", FullName = "OLP" },
                ["BNC"] = new ContractorInfo { Code = "BNC", FullName = "BNC" },
                ["OCC"] = new ContractorInfo { Code = "OCC", FullName = "OCC" },
                ["ADC"] = new ContractorInfo { Code = "ADC", FullName = "ADC" }
            };
            File.WriteAllText(_contractorsJsonPath, JsonSerializer.Serialize(sampleContractors, _jsonOptions));
        }
    }

    #region Public Methods
    /// <summary>
    /// Táº¡o cÃ´ng vÄƒn tá»« template - tá»± Ä‘á»™ng nháº­n diá»‡n tá»•ng tháº§u
    /// </summary>
    public async Task<(string FilePath, string Contractor)> GenerateDocumentAsync(
        ContractDocument contract,
        DocumentType type,
        Dictionary<string, string>? extraData = null,
        string? sourceWordFile = null,
        string? sourceExcelFile = null,
        string? forceContractor = null)
    {
        // 1. XÃ¡c Ä‘á»‹nh file Word nguá»“n
        var wordFile = sourceWordFile;
        if (string.IsNullOrEmpty(wordFile) && !string.IsNullOrEmpty(contract.FolderPath))
        {
            var inputFolder = Path.Combine(contract.FolderPath, "1.INPUT");
            if (Directory.Exists(inputFolder))
            {
                var wordFiles = Directory.GetFiles(inputFolder, "*.docx", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.Contains("~$")).ToArray();
                if (wordFiles.Length > 0)
                    wordFile = wordFiles[0];
            }
        }

        // 2. Nháº­n diá»‡n tá»•ng tháº§u
        var contractor = forceContractor;
        if (string.IsNullOrEmpty(contractor) && !string.IsNullOrEmpty(wordFile))
        {
            contractor = await DetectContractorAsync(wordFile);
        }
        contractor ??= "Default";

        // 3. Láº¥y táº¥t cáº£ placeholder data
        var placeholders = await BuildPlaceholderDataAsync(contract, extraData, wordFile, sourceExcelFile);
        placeholders["{{TongThau}}"] = contractor;

        // 4. TÃ¬m template theo tá»•ng tháº§u
        var templatePath = GetTemplatePath(type, contractor);

        // 5. Táº¡o file output
        var outputFileName = GetOutputFileName(type, contract.ContractNumber);
        var outputPath = GetOutputPath(contract, outputFileName);

        // 6. Copy template vÃ  thay tháº¿ placeholder
        if (File.Exists(templatePath))
        {
            await GenerateFromTemplateAsync(templatePath, outputPath, placeholders);
        }
        else
        {
            // Náº¿u khÃ´ng cÃ³ template, táº¡o file máº«u cÆ¡ báº£n
            await GenerateDefaultDocumentAsync(type, outputPath, placeholders, contract);
        }

        return (outputPath, contractor);
    }

    /// <summary>
    /// Nháº­n diá»‡n tá»•ng tháº§u tá»« file Word báº±ng AI hoáº·c tá»« khÃ³a
    /// </summary>
    public async Task<string> DetectContractorAsync(string wordFilePath)
    {
        var contractText = await ExtractTextFromWordAsync(wordFilePath);
        if (string.IsNullOrEmpty(contractText))
            return "Default";

        // 1. Thá»­ nháº­n diá»‡n báº±ng tá»« khÃ³a trÆ°á»›c (nhanh hÆ¡n)
        var contractor = DetectContractorByKeywords(contractText);
        if (!string.IsNullOrEmpty(contractor))
            return contractor;

        // 2. Náº¿u khÃ´ng tÃ¬m tháº¥y vÃ  cÃ³ AI, dÃ¹ng AI
        if (_aiService != null)
        {
            contractor = await DetectContractorByAIAsync(contractText);
            if (!string.IsNullOrEmpty(contractor))
                return contractor;
        }

        return "Default";
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch tá»•ng tháº§u Ä‘Ã£ cáº¥u hÃ¬nh
    /// </summary>
    public List<string> GetAvailableContractors()
    {
        var contractors = new List<string> { "Default" };

        // Tá»« thÆ° má»¥c
        if (Directory.Exists(_templateFolder))
        {
            var dirs = Directory.GetDirectories(_templateFolder)
                .Select(Path.GetFileName)
                .Where(d => d != null && d != "Default")
                .Cast<string>();
            contractors.AddRange(dirs);
        }

        // Tá»« file contractors.txt
        var contractorsFile = Path.Combine(_templateFolder, "contractors.txt");
        if (File.Exists(contractorsFile))
        {
            var lines = File.ReadAllLines(contractorsFile)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'));
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length > 0 && !contractors.Contains(parts[0].Trim()))
                    contractors.Add(parts[0].Trim());
            }
        }

        return [.. contractors.Distinct()];
    }

    /// <summary>
    /// Kiá»ƒm tra template cÃ³ tá»“n táº¡i khÃ´ng
    /// </summary>
    public bool HasTemplate(DocumentType type, string? contractor = null)
    {
        return File.Exists(GetTemplatePath(type, contractor ?? "Default"));
    }

    /// <summary>
    /// Láº¥y Ä‘Æ°á»ng dáº«n thÆ° má»¥c template
    /// </summary>
    public string GetTemplateFolder() => _templateFolder;

    /// <summary>
    /// Láº¥y Ä‘Æ°á»ng dáº«n thÆ° má»¥c template cá»§a tá»•ng tháº§u
    /// </summary>
    public string GetContractorTemplateFolder(string contractor)
    {
        return Path.Combine(_templateFolder, contractor);
    }
    #endregion

    #region Contractor Detection
    /// <summary>
    /// Nháº­n diá»‡n tá»•ng tháº§u báº±ng tá»« khÃ³a
    /// </summary>
    private string? DetectContractorByKeywords(string text)
    {
        var contractorsFile = Path.Combine(_templateFolder, "contractors.txt");
        if (!File.Exists(contractorsFile))
            return null;

        var textLower = text.ToLower();
        var lines = File.ReadAllLines(contractorsFile)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'));

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 2) continue;

            var contractorName = parts[0].Trim();
            var keywords = parts.Skip(1).Select(k => k.Trim().ToLower());

            // Kiá»ƒm tra tá»«ng tá»« khÃ³a
            foreach (var keyword in keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && textLower.Contains(keyword))
                {
                    // Kiá»ƒm tra thÆ° má»¥c template tá»“n táº¡i
                    var templateDir = Path.Combine(_templateFolder, contractorName);
                    if (Directory.Exists(templateDir))
                        return contractorName;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Nháº­n diá»‡n tá»•ng tháº§u báº±ng AI
    /// </summary>
    private async Task<string?> DetectContractorByAIAsync(string contractText)
    {
        if (_aiService == null)
            return null;

        try
        {
            var contractors = GetAvailableContractors();
            var contractorList = string.Join(", ", contractors);

            var prompt = $@"Äá»c ná»™i dung há»£p Ä‘á»“ng sau vÃ  xÃ¡c Ä‘á»‹nh tá»•ng tháº§u/chá»§ Ä‘áº§u tÆ° thuá»™c Ä‘Æ¡n vá»‹ nÃ o.

Danh sÃ¡ch tá»•ng tháº§u cÃ³ sáºµn: {contractorList}

Ná»™i dung há»£p Ä‘á»“ng (trÃ­ch):
{contractText[..Math.Min(2000, contractText.Length)]}

Tráº£ lá»i CHá»ˆ tÃªn tá»•ng tháº§u (má»™t trong danh sÃ¡ch trÃªn), khÃ´ng giáº£i thÃ­ch. Náº¿u khÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c, tráº£ lá»i 'Default'.";

            var response = await _aiService.SendChatAsync(prompt);
            var result = response.Trim();

            // Kiá»ƒm tra káº¿t quáº£ cÃ³ trong danh sÃ¡ch khÃ´ng
            if (contractors.Any(c => c.Equals(result, StringComparison.OrdinalIgnoreCase)))
                return contractors.First(c => c.Equals(result, StringComparison.OrdinalIgnoreCase));
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Äá»c text tá»« file Word
    /// </summary>
    private static async Task<string> ExtractTextFromWordAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                return body?.InnerText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });
    }
    #endregion

    #region Data Extraction
    /// <summary>
    /// XÃ¢y dá»±ng dictionary chá»©a táº¥t cáº£ placeholder vÃ  giÃ¡ trá»‹
    /// </summary>
    private static async Task<Dictionary<string, string>> BuildPlaceholderDataAsync(
        ContractDocument contract,
        Dictionary<string, string>? extraData,
        string? sourceWordFile,
        string? sourceExcelFile)
    {
        Dictionary<string, string> data = [];

        // 1. ThÃ´ng tin cÆ¡ báº£n tá»« contract
        data["{{SoHopDong}}"] = contract.ContractNumber;
        data["{{NgayKy}}"] = contract.SignDate.ToString("dd/MM/yyyy");
        data["{{TenDuAn}}"] = contract.ProjectName;
        data["{{HangMuc}}"] = contract.ItemName;

        // 2. GiÃ¡ trá»‹ tá»« contract
        data["{{GiaTriHopDong}}"] = contract.ContractValue.ToString("N0");
        data["{{GiaTriPhuLuc}}"] = contract.TotalAppendixValue.ToString("N0");
        data["{{TongGiaTri}}"] = contract.TotalValue.ToString("N0");
        data["{{DaTamUng}}"] = contract.TotalAdvance.ToString("N0");
        data["{{DaThanhToan}}"] = contract.TotalPayment.ToString("N0");
        data["{{ConLai}}"] = contract.RemainingAmount.ToString("N0");
        data["{{GiaTriQuyetToan}}"] = contract.SettlementValue.ToString("N0");
        data["{{GiaTriBaoHanh}}"] = contract.WarrantyValue.ToString("N0");
        data["{{ConLaiThanhToan}}"] = contract.RemainingPaymentValue.ToString("N0");

        // 3. NgÃ y thÃ¡ng hiá»‡n táº¡i
        data["{{Ngay}}"] = DateTime.Now.Day.ToString();
        data["{{Thang}}"] = DateTime.Now.Month.ToString();
        data["{{Nam}}"] = DateTime.Now.Year.ToString();
        data["{{NgayHienTai}}"] = DateTime.Now.ToString("dd/MM/yyyy");

        // 4. Äá»c thÃ´ng tin tá»« file Word há»£p Ä‘á»“ng gá»‘c
        if (!string.IsNullOrEmpty(sourceWordFile) && File.Exists(sourceWordFile))
        {
            var wordData = await ExtractDataFromWordAsync(sourceWordFile);
            foreach (var kv in wordData)
                data[kv.Key] = kv.Value;
        }
        else if (!string.IsNullOrEmpty(contract.FolderPath))
        {
            var inputFolder = Path.Combine(contract.FolderPath, "1.INPUT");
            if (Directory.Exists(inputFolder))
            {
                var wordFiles = Directory.GetFiles(inputFolder, "*.docx", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.Contains("~$")).ToArray();
                if (wordFiles.Length > 0)
                {
                    var wordData = await ExtractDataFromWordAsync(wordFiles[0]);
                    foreach (var kv in wordData)
                        data[kv.Key] = kv.Value;
                }
            }
        }

        // 5. Äá»c giÃ¡ trá»‹ tá»« file Excel
        if (!string.IsNullOrEmpty(sourceExcelFile) && File.Exists(sourceExcelFile))
        {
            var excelData = await ExtractDataFromExcelAsync(sourceExcelFile);
            foreach (var kv in excelData)
                data[kv.Key] = kv.Value;
        }
        else if (!string.IsNullOrEmpty(contract.FolderPath))
        {
            var excelFiles = Directory.GetFiles(contract.FolderPath, "*.xlsx", SearchOption.AllDirectories)
                .Where(f => !f.Contains("~$")).ToArray();
            if (excelFiles.Length > 0)
            {
                var excelData = await ExtractDataFromExcelAsync(excelFiles[0]);
                foreach (var kv in excelData)
                    data[kv.Key] = kv.Value;
            }
        }

        // 6. ThÃªm extra data tá»« user input (override náº¿u trÃ¹ng)
        if (extraData != null)
        {
            foreach (var kv in extraData)
            {
                var key = kv.Key.StartsWith("{{") ? kv.Key : $"{{{{{kv.Key}}}}}";
                data[key] = kv.Value;
            }
        }

        return data;
    }

    /// <summary>
    /// Äá»c thÃ´ng tin tá»« file Word há»£p Ä‘á»“ng
    /// </summary>
    private static async Task<Dictionary<string, string>> ExtractDataFromWordAsync(string filePath)
    {
        var data = new Dictionary<string, string>();

        await Task.Run(() =>
        {
            try
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return;

                var fullText = body.InnerText;

                // BÃªn A
                var benAMatch = BenARegex().Match(fullText);
                if (benAMatch.Success)
                    data["{{BenA}}"] = CleanText(benAMatch.Groups[1].Value);

                // BÃªn B
                var benBMatch = BenBRegex().Match(fullText);
                if (benBMatch.Success)
                    data["{{BenB}}"] = CleanText(benBMatch.Groups[1].Value);

                // Äáº¡i diá»‡n
                var daiDienMatch = DaiDienRegex().Match(fullText);
                if (daiDienMatch.Success)
                    data["{{DaiDienBenA}}"] = CleanText(daiDienMatch.Groups[1].Value);

                // Äá»‹a chá»‰
                var diaChiMatch = DiaChiRegex().Match(fullText);
                if (diaChiMatch.Success)
                    data["{{DiaChi}}"] = CleanText(diaChiMatch.Groups[1].Value);

                // MST
                var mstMatch = MstRegex().Match(fullText);
                if (mstMatch.Success)
                    data["{{MaSoThue}}"] = CleanText(mstMatch.Groups[1].Value);

                // STK
                var stkMatch = StkRegex().Match(fullText);
                if (stkMatch.Success)
                    data["{{SoTaiKhoan}}"] = CleanText(stkMatch.Groups[1].Value);

                // NgÃ¢n hÃ ng
                var nhMatch = NganHangRegex().Match(fullText);
                if (nhMatch.Success)
                    data["{{NganHang}}"] = CleanText(nhMatch.Groups[1].Value);
            }
            catch { }
        });

        return data;
    }

    /// <summary>
    /// Äá»c giÃ¡ trá»‹ tá»« file Excel
    /// </summary>
    private static async Task<Dictionary<string, string>> ExtractDataFromExcelAsync(string filePath)
    {
        var data = new Dictionary<string, string>();

        await Task.Run(() =>
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.First();

                foreach (var row in worksheet.RowsUsed())
                {
                    foreach (var cell in row.CellsUsed())
                    {
                        var cellText = cell.GetString().Trim().ToLower();
                        var nextCell = cell.CellRight();

                        if (cellText.Contains("giÃ¡ trá»‹") || cellText.Contains("gia tri"))
                        {
                            if (nextCell != null && nextCell.TryGetValue<decimal>(out var value))
                                data["{{GiaTriExcel}}"] = value.ToString("N0");
                        }

                        if (cellText.Contains("tá»•ng") || cellText.Contains("tong"))
                        {
                            if (nextCell != null && nextCell.TryGetValue<decimal>(out var value))
                                data["{{TongExcel}}"] = value.ToString("N0");
                        }

                        if (cellText.Contains("thuáº¿") || cellText.Contains("vat"))
                        {
                            if (nextCell != null && nextCell.TryGetValue<decimal>(out var value))
                                data["{{ThueExcel}}"] = value.ToString("N0");
                        }
                    }
                }

                // Láº¥y tá»•ng tá»« SUM
                foreach (var cell in worksheet.CellsUsed())
                {
                    if (cell.HasFormula && cell.FormulaA1.Contains("SUM", StringComparison.OrdinalIgnoreCase))
                    {
                        if (cell.TryGetValue<decimal>(out var sumValue))
                        {
                            data["{{TongCongExcel}}"] = sumValue.ToString("N0");
                            break;
                        }
                    }
                }
            }
            catch { }
        });

        return data;
    }

    private static string CleanText(string text) => CleanRegex().Replace(text.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex CleanRegex();
    #endregion

    #region Template Processing
    private static async Task GenerateFromTemplateAsync(string templatePath, string outputPath, Dictionary<string, string> placeholders)
    {
        await Task.Run(() =>
        {
            File.Copy(templatePath, outputPath, true);

            using var doc = WordprocessingDocument.Open(outputPath, true);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return;

            // Thay tháº¿ trong body
            foreach (var text in body.Descendants<Text>())
            {
                foreach (var kv in placeholders)
                {
                    if (text.Text.Contains(kv.Key))
                        text.Text = text.Text.Replace(kv.Key, kv.Value);
                }
            }

            // Thay tháº¿ trong header
            foreach (var headerPart in doc.MainDocumentPart?.HeaderParts ?? [])
            {
                foreach (var text in headerPart.Header.Descendants<Text>())
                {
                    foreach (var kv in placeholders)
                    {
                        if (text.Text.Contains(kv.Key))
                            text.Text = text.Text.Replace(kv.Key, kv.Value);
                    }
                }
            }

            // Thay tháº¿ trong footer
            foreach (var footerPart in doc.MainDocumentPart?.FooterParts ?? [])
            {
                foreach (var text in footerPart.Footer.Descendants<Text>())
                {
                    foreach (var kv in placeholders)
                    {
                        if (text.Text.Contains(kv.Key))
                            text.Text = text.Text.Replace(kv.Key, kv.Value);
                    }
                }
            }

            doc.MainDocumentPart?.Document.Save();
        });
    }

    private string GetTemplatePath(DocumentType type, string contractor)
    {
        // CÃ”NG VÄ‚N THANH TOÃN: DÃ¹ng 1 template chung cho Táº¤T Cáº¢ tá»•ng tháº§u
        // (chá»‰ Ä‘á»•i thÃ´ng tin tá»•ng tháº§u, khÃ´ng cáº§n template riÃªng)
        if (type == DocumentType.PaymentRequest)
        {
            // Æ¯u tiÃªn template á»Ÿ root: Templates/CongVanThanhToan.docx
            var rootPath = Path.Combine(_templateFolder, TemplateFileNames[type]);
            if (File.Exists(rootPath))
                return rootPath;

            // Fallback vá» Default
            var defaultPath = Path.Combine(_templateFolder, "Default", TemplateFileNames[type]);
            if (File.Exists(defaultPath))
                return defaultPath;

            return rootPath; // Tráº£ vá» root path dÃ¹ khÃ´ng tá»“n táº¡i (Ä‘á»ƒ táº¡o file máº«u)
        }

        // CÃC LOáº I KHÃC (Thanh lÃ½, BÃ n giao, LiÃªn Ä‘á»™ng): Má»—i tá»•ng tháº§u cÃ³ template riÃªng
        // Thá»­ tÃ¬m trong thÆ° má»¥c tá»•ng tháº§u trÆ°á»›c
        var contractorPath = Path.Combine(_templateFolder, contractor, TemplateFileNames[type]);
        if (File.Exists(contractorPath))
            return contractorPath;

        // Fallback vá» Default
        var defaultPath2 = Path.Combine(_templateFolder, "Default", TemplateFileNames[type]);
        if (File.Exists(defaultPath2))
            return defaultPath2;

        // Fallback vá» root
        return Path.Combine(_templateFolder, TemplateFileNames[type]);
    }

    private static string GetOutputFileName(DocumentType type, string contractNumber)
    {
        var prefix = type switch
        {
            DocumentType.PaymentRequest => "CongVan_ThanhToan",
            DocumentType.ContractLiquidation => "BienBan_ThanhLy",
            DocumentType.HandoverReport => "BienBan_BanGiao",
            DocumentType.LoadTest => "BienBan_LienDongCoTai",
            _ => "Document"
        };
        return $"{prefix}_{contractNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
    }

    private string GetOutputPath(ContractDocument contract, string fileName)
    {
        if (!string.IsNullOrEmpty(contract.FolderPath) && Directory.Exists(contract.FolderPath))
        {
            var outputFolder = Path.Combine(contract.FolderPath, "3.OUTPUT");
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            return Path.Combine(outputFolder, fileName);
        }
        return Path.Combine(_outputFolder, fileName);
    }
    #endregion

    #region Default Document Generation
    private static async Task GenerateDefaultDocumentAsync(DocumentType type, string outputPath,
        Dictionary<string, string> data, ContractDocument contract)
    {
        await Task.Run(() =>
        {
            using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddParagraph(body, "Cá»˜NG HÃ’A XÃƒ Há»˜I CHá»¦ NGHÄ¨A VIá»†T NAM", true, JustificationValues.Center, "14");
            AddParagraph(body, "Äá»™c láº­p - Tá»± do - Háº¡nh phÃºc", false, JustificationValues.Center, "13", true);
            AddParagraph(body, "---o0o---", false, JustificationValues.Center);
            AddEmptyParagraph(body);

            switch (type)
            {
                case DocumentType.PaymentRequest:
                    AddParagraph(body, "CÃ”NG VÄ‚N Äá»€ NGHá»Š THANH TOÃN", true, JustificationValues.Center, "16");
                    AddEmptyParagraph(body);
                    AddParagraph(body, $"KÃ­nh gá»­i: {data.GetValueOrDefault("{{BenA}}", "[TÃªn Ä‘Æ¡n vá»‹]")}");
                    AddParagraph(body, $"CÄƒn cá»© HÄ sá»‘: {contract.ContractNumber} ngÃ y {contract.SignDate:dd/MM/yyyy}");
                    AddParagraph(body, $"Dá»± Ã¡n: {contract.ProjectName}");
                    AddParagraph(body, $"GiÃ¡ trá»‹ HÄ: {data.GetValueOrDefault("{{GiaTriHopDong}}", "0")} Ä‘á»“ng");
                    AddParagraph(body, $"Äá» nghá»‹ thanh toÃ¡n: {data.GetValueOrDefault("{{ConLai}}", "0")} Ä‘á»“ng");
                    break;

                case DocumentType.ContractLiquidation:
                    AddParagraph(body, "BIÃŠN Báº¢N THANH LÃ Há»¢P Äá»’NG", true, JustificationValues.Center, "16");
                    AddEmptyParagraph(body);
                    AddParagraph(body, $"HÄ sá»‘: {contract.ContractNumber}");
                    AddParagraph(body, $"Dá»± Ã¡n: {contract.ProjectName}");
                    AddParagraph(body, $"GiÃ¡ trá»‹ quyáº¿t toÃ¡n: {data.GetValueOrDefault("{{GiaTriQuyetToan}}", "0")} Ä‘á»“ng");
                    break;

                case DocumentType.HandoverReport:
                    AddParagraph(body, "BIÃŠN Báº¢N BÃ€N GIAO CÃ”NG TRÃŒNH", true, JustificationValues.Center, "16");
                    AddEmptyParagraph(body);
                    AddParagraph(body, $"CÃ´ng trÃ¬nh: {contract.ProjectName}");
                    AddParagraph(body, $"Háº¡ng má»¥c: {contract.ItemName}");
                    break;

                case DocumentType.LoadTest:
                    AddParagraph(body, "BIÃŠN Báº¢N THá»¬ NGHIá»†M LIÃŠN Äá»˜NG CÃ“ Táº¢I", true, JustificationValues.Center, "16");
                    AddEmptyParagraph(body);
                    AddParagraph(body, $"CÃ´ng trÃ¬nh: {contract.ProjectName}");
                    AddParagraph(body, "Káº¿t quáº£: â–¡ Äáº¡t  â–¡ KhÃ´ng Ä‘áº¡t");
                    break;
            }

            mainPart.Document.Save();
        });
    }

    private static void AddParagraph(Body body, string text, bool bold = false, JustificationValues? align = null,
        string fontSize = "13", bool underline = false)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        if (align.HasValue) pPr.Append(new Justification { Val = align.Value });
        para.Append(pPr);

        var run = new Run();
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
        rPr.Append(new FontSize { Val = (int.Parse(fontSize) * 2).ToString() });
        if (bold) rPr.Append(new Bold());
        if (underline) rPr.Append(new Underline { Val = UnderlineValues.Single });
        run.Append(rPr);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        para.Append(run);
        body.Append(para);
    }

    private static void AddEmptyParagraph(Body body) => body.Append(new Paragraph(new Run(new Text(""))));
    #endregion

    #region Static Methods
    public static List<(string Placeholder, string Description)> GetAvailablePlaceholders() => [
        ("{{SoHopDong}}", "Sá»‘ há»£p Ä‘á»“ng"),
        ("{{NgayKy}}", "NgÃ y kÃ½ (dd/MM/yyyy)"),
        ("{{TenDuAn}}", "TÃªn dá»± Ã¡n"),
        ("{{HangMuc}}", "Háº¡ng má»¥c"),
        ("{{GiaTriHopDong}}", "GiÃ¡ trá»‹ há»£p Ä‘á»“ng"),
        ("{{GiaTriPhuLuc}}", "GiÃ¡ trá»‹ phá»¥ lá»¥c"),
        ("{{TongGiaTri}}", "Tá»•ng giÃ¡ trá»‹"),
        ("{{DaTamUng}}", "ÄÃ£ táº¡m á»©ng"),
        ("{{DaThanhToan}}", "ÄÃ£ thanh toÃ¡n"),
        ("{{ConLai}}", "CÃ²n láº¡i"),
        ("{{GiaTriQuyetToan}}", "GiÃ¡ trá»‹ quyáº¿t toÃ¡n"),
        ("{{BenA}}", "TÃªn bÃªn A"),
        ("{{BenB}}", "TÃªn bÃªn B"),
        ("{{TongThau}}", "TÃªn tá»•ng tháº§u (tá»± nháº­n diá»‡n)"),
        ("{{Ngay}}", "NgÃ y hiá»‡n táº¡i"),
        ("{{Thang}}", "ThÃ¡ng hiá»‡n táº¡i"),
        ("{{Nam}}", "NÄƒm hiá»‡n táº¡i")
    ];

    public static List<(DocumentType Type, string Name, string Description, string TemplateFile)> GetAvailableDocumentTypes() => [
        (DocumentType.PaymentRequest, "CÃ´ng vÄƒn thanh toÃ¡n", "Äá» nghá»‹ thanh toÃ¡n", TemplateFileNames[DocumentType.PaymentRequest]),
        (DocumentType.ContractLiquidation, "Thanh lÃ½ há»£p Ä‘á»“ng", "BiÃªn báº£n thanh lÃ½", TemplateFileNames[DocumentType.ContractLiquidation]),
        (DocumentType.HandoverReport, "BiÃªn báº£n bÃ n giao", "BÃ n giao cÃ´ng trÃ¬nh", TemplateFileNames[DocumentType.HandoverReport]),
        (DocumentType.LoadTest, "LiÃªn Ä‘á»™ng cÃ³ táº£i", "Thá»­ nghiá»‡m liÃªn Ä‘á»™ng", TemplateFileNames[DocumentType.LoadTest])
    ];

    [GeneratedRegex(@"/([A-Z]+)-KHC", RegexOptions.IgnoreCase)]
    private static partial Regex KhcRegex();

    [GeneratedRegex(@"\{[\s\S]*\}")]
    private static partial Regex JsonBracketRegex();

    [GeneratedRegex(@"BÃŠN\s*A[:\s]*([^\n]+?)(?=BÃŠN\s*B|Äá»‹a chá»‰|Äiá»‡n thoáº¡i|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BenARegex();

    [GeneratedRegex(@"BÃŠN\s*B[:\s]*([^\n]+?)(?=Äá»‹a chá»‰|Äiá»‡n thoáº¡i|Äiá»u|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BenBRegex();

    [GeneratedRegex(@"(?:Äáº¡i diá»‡n|Ã”ng|BÃ )[:\s]*([^\n,]+?)(?=Chá»©c vá»¥|Äá»‹a chá»‰|,|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DaiDienRegex();

    [GeneratedRegex(@"Äá»‹a chá»‰[:\s]*([^\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DiaChiRegex();

    [GeneratedRegex(@"(?:MÃ£ sá»‘ thuáº¿|MST)[:\s]*([0-9\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MstRegex();

    [GeneratedRegex(@"(?:Sá»‘ tÃ i khoáº£n|STK)[:\s]*([0-9\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex StkRegex();

    [GeneratedRegex(@"(?:NgÃ¢n hÃ ng|NH|táº¡i)[:\s]*([^\n,]+?)(?=Chi nhÃ¡nh|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NganHangRegex();
    #endregion
}

