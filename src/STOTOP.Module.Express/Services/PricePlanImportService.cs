using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using System.Text.RegularExpressions;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 报价方案导入服务
/// </summary>
public class PricePlanImportService : IPricePlanImportService
{
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly IQuotationService _pricePlanService;

    public PricePlanImportService(
        IRepository<ExpProvince> provinceRepo,
        IQuotationService pricePlanService)
    {
        _provinceRepo = provinceRepo;
        _pricePlanService = pricePlanService;
    }

    /// <summary>
    /// 生成报价方案Excel模板（行=目的地，列=重量段）
    /// </summary>
    public async Task<byte[]> GenerateTemplateAsync()
    {
        var provinces = await _provinceRepo.Query().OrderBy(p => p.FCode).ToListAsync();
        var workbook = new XSSFWorkbook();

        // ===== Sheet1 报价矩阵模板 =====
        var sheet = workbook.CreateSheet("报价矩阵模板");
        var headerStyle = CreateHeaderStyle(workbook);
        var dataStyle = CreateDataStyle(workbook);
        var specialStyle = CreateSpecialStyle(workbook);
        var noteStyle = CreateNoteStyle(workbook);

        // 列宽：A=目的地, B-G=固定单价段, H-I=6-10面单续重, J-K=10-999面单续重
        sheet.SetColumnWidth(0, 18 * 256);
        for (int i = 1; i <= 6; i++) sheet.SetColumnWidth(i, 11 * 256);
        for (int i = 7; i <= 10; i++) sheet.SetColumnWidth(i, 10 * 256);

        // --- 双行表头 ---
        var row0 = sheet.CreateRow(0);
        var row1 = sheet.CreateRow(1);
        row0.HeightInPoints = 22;
        row1.HeightInPoints = 22;

        // A列：目的地（合并两行）
        SetHeaderCell(row0, 0, "目的地", headerStyle);
        SetHeaderCell(row1, 0, "", headerStyle);
        sheet.AddMergedRegion(new CellRangeAddress(0, 1, 0, 0));

        // B-G列：固定单价重量段（各自合并两行）
        var fixedHeaders = new[] { "0<W≤1", "1<W≤2", "2<W≤3", "3<W≤4", "4<W≤5", "5<W≤6" };
        for (int i = 0; i < fixedHeaders.Length; i++)
        {
            SetHeaderCell(row0, i + 1, fixedHeaders[i], headerStyle);
            SetHeaderCell(row1, i + 1, "", headerStyle);
            sheet.AddMergedRegion(new CellRangeAddress(0, 1, i + 1, i + 1));
        }

        // H-I列：6<W≤10（合并H0:I0，子列：面单/续重）
        SetHeaderCell(row0, 7, "6<W≤10", headerStyle);
        SetHeaderCell(row0, 8, "", headerStyle);
        sheet.AddMergedRegion(new CellRangeAddress(0, 0, 7, 8));
        SetHeaderCell(row1, 7, "面单", headerStyle);
        SetHeaderCell(row1, 8, "续重", headerStyle);

        // J-K列：10<W≤999（合并J0:K0，子列：面单/续重）
        SetHeaderCell(row0, 9, "10<W≤999", headerStyle);
        SetHeaderCell(row0, 10, "", headerStyle);
        sheet.AddMergedRegion(new CellRangeAddress(0, 0, 9, 10));
        SetHeaderCell(row1, 9, "面单", headerStyle);
        SetHeaderCell(row1, 10, "续重", headerStyle);

        // --- 数据行：普通省份 ---
        var specialShortNames = new HashSet<string> { "海南", "青海", "新疆", "西藏" };
        var normalProvinces = provinces.Where(p => !specialShortNames.Contains(p.FShortName)).ToList();
        int dataRowIdx = 2;
        foreach (var p in normalProvinces)
        {
            var row = sheet.CreateRow(dataRowIdx);
            var displayName = p.FName.EndsWith("市") ? p.FShortName : p.FName;
            SetDataCell(row, 0, displayName, dataStyle);
            for (int col = 1; col <= 10; col++)
                row.CreateCell(col).CellStyle = dataStyle;
            dataRowIdx++;
        }

        // --- 统一单价行 ---
        CreateSpecialRow(sheet, ref dataRowIdx, "海南省、青海", "8元/公斤", dataStyle, specialStyle);
        CreateSpecialRow(sheet, ref dataRowIdx, "新疆维吾尔自治区", "20元/公斤", dataStyle, specialStyle);
        CreateSpecialRow(sheet, ref dataRowIdx, "西藏自治区", "20元/公斤", dataStyle, specialStyle);

        // --- 备注 ---
        dataRowIdx++;
        var notes = new[]
        {
            "备注：1、以上价格含税，预充按2.5元/单，后期按货物均重进行预充值；",
            "\u3000\u3000\u30002、价格表中KG为结算重量，取值以转运称重为准；计泡重量=长(cm)*宽(cm)*高(cm)/10000",
            "\u3000\u3000\u30003、不可抗力加收：由于疫情、灾害、以及当地重要会议等特殊情况导致对应区域派费加收，具体加收费用及生效时间以中通快递挂网文件通知为准",
            "\u3000\u3000\u30004、大促加收规则：淘宝活动日；双十一、双十二、6.18促销、9.9促销、年货节等特殊节日加收费用及生效时间以中通快递挂网文件通知为准",
            "\u3000\u3000\u30005、窗口退回件产生的退回中转费跟邮寄费一样。",
            "\u3000\u3000\u30006、向上取整：续重重量不足1公斤按1公斤结算",
            "\u3000\u3000\u30007、因未按总部及市场部相关限定要求、操作不规范、售后未达标等产生的罚款、加收等，由客户自行承担；售后保丢不保损。"
        };
        foreach (var note in notes)
        {
            var noteRow = sheet.CreateRow(dataRowIdx);
            var noteCell = noteRow.CreateCell(0);
            noteCell.SetCellValue(note);
            noteCell.CellStyle = noteStyle;
            sheet.AddMergedRegion(new CellRangeAddress(dataRowIdx, dataRowIdx, 0, 10));
            dataRowIdx++;
        }

        // 冻结表头
        sheet.CreateFreezePane(1, 2);

        // ===== Sheet2 省份对照表 =====
        var sheet2 = workbook.CreateSheet("省份对照表");
        var refHeaders = new[] { "ID", "编码", "简称", "全称", "大区", "是否偏远" };
        var refHeaderRow = sheet2.CreateRow(0);
        for (int i = 0; i < refHeaders.Length; i++)
        {
            var c = refHeaderRow.CreateCell(i);
            c.SetCellValue(refHeaders[i]);
            c.CellStyle = headerStyle;
        }
        sheet2.SetColumnWidth(0, 8 * 256);
        sheet2.SetColumnWidth(1, 12 * 256);
        sheet2.SetColumnWidth(2, 10 * 256);
        sheet2.SetColumnWidth(3, 18 * 256);
        sheet2.SetColumnWidth(4, 12 * 256);
        sheet2.SetColumnWidth(5, 10 * 256);

        for (int i = 0; i < provinces.Count; i++)
        {
            var p = provinces[i];
            var row = sheet2.CreateRow(i + 1);
            row.CreateCell(0).SetCellValue(p.FID);
            row.CreateCell(1).SetCellValue(p.FCode);
            row.CreateCell(2).SetCellValue(p.FShortName);
            row.CreateCell(3).SetCellValue(p.FName);
            row.CreateCell(4).SetCellValue(p.FRegion ?? "");
            row.CreateCell(5).SetCellValue(p.FIsRemote ? "是" : "否");
        }
        sheet2.CreateFreezePane(0, 1);

        // 输出
        using var ms = new MemoryStream();
        workbook.Write(ms, true);
        workbook.Close();
        return ms.ToArray();
    }

    /// <summary>
    /// 从Excel导入报价方案（新格式：行=目的地，列=重量段）
    /// </summary>
    public async Task<QuotationDto> ImportFromExcelAsync(
        string brandCode, string planName, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            throw new InvalidOperationException("仅支持 .xlsx 或 .xls 格式的Excel文件");

        var provinces = await _provinceRepo.Query().OrderBy(p => p.FCode).ToListAsync();
        var provinceMap = new Dictionary<string, ExpProvince>();
        foreach (var p in provinces)
        {
            provinceMap[p.FShortName] = p;
            provinceMap[p.FName] = p;
            if (p.FName.EndsWith("市")) provinceMap[p.FName.TrimEnd('市')] = p;
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        IWorkbook workbook = WorkbookFactory.Create(ms);

        try
        {
            var sheet = workbook.GetSheetAt(0);
            if (sheet == null)
                throw new InvalidOperationException("Excel文件中没有工作表");

            // 固定8个重量段：0-1,1-2,...,5-6(固定单价), 6-10,10-999(首续重)
            var segments = new List<WeightSegmentInput>
            {
                new() { SegmentIndex = 1, WeightFrom = 0, WeightTo = 1, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 2, WeightFrom = 1, WeightTo = 2, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 3, WeightFrom = 2, WeightTo = 3, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 4, WeightFrom = 3, WeightTo = 4, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 5, WeightFrom = 4, WeightTo = 5, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 6, WeightFrom = 5, WeightTo = 6, PricingMethod = 1, Cells = new() },
                new() { SegmentIndex = 7, WeightFrom = 6, WeightTo = 10, PricingMethod = 3, FirstWeight = 1, ContinueWeight = 1, Cells = new() },
                new() { SegmentIndex = 8, WeightFrom = 10, WeightTo = 999, PricingMethod = 3, FirstWeight = 1, ContinueWeight = 1, Cells = new() },
            };

            var errors = new List<string>();
            var flatRatePattern = new Regex(@"^(\d+(?:\.\d+)?)\s*元/公斤$");

            // 从第3行（索引2）开始逐行解析
            for (int rowIdx = 2; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                var nameCell = row.GetCell(0);
                if (nameCell == null) continue;
                var destName = GetCellString(nameCell).Trim();
                if (string.IsNullOrEmpty(destName)) continue;
                if (destName.StartsWith("备注")) break; // 备注行以下忽略

                // 检测统一单价行（如 "8元/公斤"）
                var cell1Str = GetCellString(row.GetCell(1)).Trim();
                var flatMatch = flatRatePattern.Match(cell1Str);
                if (flatMatch.Success)
                {
                    var unitPrice = decimal.Parse(flatMatch.Groups[1].Value);
                    var names = destName.Split(new[] { '、', '，', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var name in names)
                    {
                        if (!TryMatchProvince(provinceMap, name.Trim(), out var prov))
                        {
                            errors.Add($"第{rowIdx + 1}行: 目的地\"{name.Trim()}\"无法匹配省份");
                            continue;
                        }
                        foreach (var seg in segments)
                        {
                            // "元/公斤" = 按公斤单价计费，所有重量段统一用 首续重(首重1kg/步进1kg, 首价=续价=单价)
                            // 计算结果为 单价 × 进位后公斤数，避免在固定段(1-6kg)上退化为按段固定价而少算
                            var cell = new PriceCellInput
                            {
                                ProvinceId = prov.FID,
                                BasePrice = unitPrice,
                                ContinuePrice = unitPrice,
                                FirstWeight = 1m,
                                ContinueStep = 1m
                            };
                            seg.Cells.Add(cell);
                        }
                    }
                    continue;
                }

                // 普通省份行
                if (!TryMatchProvince(provinceMap, destName, out var province))
                {
                    errors.Add($"第{rowIdx + 1}行: 目的地\"{destName}\"无法匹配省份");
                    continue;
                }

                // B-G列（索引1-6）: 固定单价
                var segHeaders = new[] { "0<W≤1", "1<W≤2", "2<W≤3", "3<W≤4", "4<W≤5", "5<W≤6" };
                for (int col = 1; col <= 6; col++)
                {
                    var val = GetCellString(row.GetCell(col)).Trim();
                    if (string.IsNullOrEmpty(val))
                    {
                        errors.Add($"第{rowIdx + 1}行: {province.FShortName} {segHeaders[col - 1]}列缺少价格");
                        continue;
                    }
                    if (!decimal.TryParse(val, out var price) || price <= 0)
                    {
                        errors.Add($"第{rowIdx + 1}行: {province.FShortName} {segHeaders[col - 1]}列价格无效\"{val}\"");
                        continue;
                    }
                    segments[col - 1].Cells.Add(new PriceCellInput { ProvinceId = province.FID, BasePrice = price });
                }

                // H-I列：6<W≤10 面单/续重
                ParseFirstContinuePair(row, 7, 8, province, segments[6], errors, rowIdx, "6<W≤10");
                // J-K列：10<W≤999 面单/续重
                ParseFirstContinuePair(row, 9, 10, province, segments[7], errors, rowIdx, "10<W≤999");
            }

            if (segments.All(s => s.Cells.Count == 0))
                throw new InvalidOperationException("未解析到任何有效的报价数据");

            if (errors.Count > 0)
                throw new InvalidOperationException($"数据校验失败({errors.Count}个错误):\n" + string.Join("\n", errors.Take(20)));

            var request = new CreateQuotationRequest
            {
                BrandCode = brandCode,
                PlanName = planName,
                Segments = segments
            };

            return await _pricePlanService.CreateAsync(request);
        }
        finally
        {
            workbook.Close();
        }
    }

    #region 辅助方法

    private static void ParseFirstContinuePair(IRow row, int firstCol, int contCol,
        ExpProvince province, WeightSegmentInput segment, List<string> errors, int rowIdx, string rangeName)
    {
        var firstVal = GetCellString(row.GetCell(firstCol)).Trim();
        var contVal = GetCellString(row.GetCell(contCol)).Trim();
        if (string.IsNullOrEmpty(firstVal) || string.IsNullOrEmpty(contVal))
        {
            errors.Add($"第{rowIdx + 1}行: {province.FShortName} {rangeName} 面单/续重缺少数据");
            return;
        }
        if (!decimal.TryParse(firstVal, out var firstPrice) || firstPrice <= 0)
        {
            errors.Add($"第{rowIdx + 1}行: {province.FShortName} {rangeName} 面单价格无效\"{firstVal}\"");
            return;
        }
        if (!decimal.TryParse(contVal, out var contPrice) || contPrice <= 0)
        {
            errors.Add($"第{rowIdx + 1}行: {province.FShortName} {rangeName} 续重价格无效\"{contVal}\"");
            return;
        }
        segment.Cells.Add(new PriceCellInput
        {
            ProvinceId = province.FID,
            BasePrice = firstPrice,
            ContinuePrice = contPrice
        });
    }

    private static bool TryMatchProvince(Dictionary<string, ExpProvince> map, string name, out ExpProvince province)
    {
        if (map.TryGetValue(name, out province!)) return true;
        if (name.EndsWith("省") && map.TryGetValue(name.TrimEnd('省'), out province!)) return true;
        if (!name.EndsWith("省") && !name.Contains("自治") && !name.EndsWith("市") && map.TryGetValue(name + "省", out province!)) return true;
        province = null!;
        return false;
    }

    private static ICellStyle CreateHeaderStyle(XSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        font.FontHeightInPoints = 11;
        style.SetFont(font);
        style.Alignment = HorizontalAlignment.Center;
        style.VerticalAlignment = VerticalAlignment.Center;
        style.FillForegroundColor = IndexedColors.PaleBlue.Index;
        style.FillPattern = FillPattern.SolidForeground;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderTop = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;
        return style;
    }

    private static ICellStyle CreateDataStyle(XSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        style.Alignment = HorizontalAlignment.Center;
        style.VerticalAlignment = VerticalAlignment.Center;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderTop = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;
        return style;
    }

    private static ICellStyle CreateSpecialStyle(XSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        style.Alignment = HorizontalAlignment.Center;
        style.VerticalAlignment = VerticalAlignment.Center;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderTop = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;
        var font = workbook.CreateFont();
        font.IsBold = true;
        style.SetFont(font);
        return style;
    }

    private static ICellStyle CreateNoteStyle(XSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        style.WrapText = true;
        style.VerticalAlignment = VerticalAlignment.Top;
        var font = workbook.CreateFont();
        font.FontHeightInPoints = 10;
        font.Color = IndexedColors.Blue.Index;
        style.SetFont(font);
        return style;
    }

    private static void SetHeaderCell(IRow row, int col, string value, ICellStyle style)
    {
        var cell = row.CreateCell(col);
        cell.SetCellValue(value);
        cell.CellStyle = style;
    }

    private static void SetDataCell(IRow row, int col, string value, ICellStyle style)
    {
        var cell = row.CreateCell(col);
        cell.SetCellValue(value);
        cell.CellStyle = style;
    }

    private static void CreateSpecialRow(ISheet sheet, ref int rowIdx, string name, string priceText,
        ICellStyle nameStyle, ICellStyle valStyle)
    {
        var row = sheet.CreateRow(rowIdx);
        SetDataCell(row, 0, name, nameStyle);
        var valCell = row.CreateCell(1);
        valCell.SetCellValue(priceText);
        valCell.CellStyle = valStyle;
        for (int col = 2; col <= 10; col++)
            row.CreateCell(col).CellStyle = valStyle;
        sheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 1, 10));
        rowIdx++;
    }

    private static string GetCellString(ICell? cell)
    {
        if (cell == null) return string.Empty;
        return cell.CellType switch
        {
            CellType.Numeric => cell.NumericCellValue.ToString("G"),
            CellType.String => cell.StringCellValue ?? string.Empty,
            CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric
                ? cell.NumericCellValue.ToString("G")
                : cell.StringCellValue ?? string.Empty,
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            _ => string.Empty
        };
    }

    private static string GetColumnLetter(int colIndex)
    {
        string result = "";
        while (colIndex >= 0)
        {
            result = (char)('A' + colIndex % 26) + result;
            colIndex = colIndex / 26 - 1;
        }
        return result;
    }

    #endregion
}
