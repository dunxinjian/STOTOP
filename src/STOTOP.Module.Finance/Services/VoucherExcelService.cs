using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

public class VoucherImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public List<VoucherImportError> Errors { get; set; } = new();
}

public class VoucherImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class VoucherExcelService
{
    private readonly STOTOPDbContext _context;

    private static readonly string[] Headers = new[]
    {
        "凭证字", "凭证号", "日期", "附件数", "行号",
        "摘要", "科目编码", "科目名称", "借方金额", "贷方金额", "辅助核算"
    };

    public VoucherExcelService(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>导出凭证到Excel</summary>
    public async Task<byte[]> ExportToExcel(List<long> voucherIds, long accountSetId)
    {
        var vouchers = await _context.Set<FinVoucher>()
            .Include(v => v.Entries)
            .Where(v => voucherIds.Contains(v.FID))
            .OrderBy(v => v.FVoucherNo)
            .AsNoTracking()
            .ToListAsync();

        // 构建科目ID->编码/名称映射
        var accountIds = vouchers
            .SelectMany(v => v.Entries)
            .Select(e => e.FAccountId)
            .Distinct()
            .ToList();

        var accountDict = new Dictionary<long, (string Code, string Name)>();
        if (accountIds.Count > 0)
        {
            var accounts = await _context.Set<FinAccount>()
                .Where(a => accountIds.Contains(a.FID))
                .AsNoTracking()
                .Select(a => new { a.FID, a.FCode, a.FName })
                .ToListAsync();
            foreach (var a in accounts)
                accountDict[a.FID] = (a.FCode, a.FName);
        }

        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("凭证数据");

        // 表头样式：粗体 + 居中
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        // 写表头
        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < Headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(Headers[i]);
            cell.CellStyle = headerStyle;
        }

        // 冻结首行
        sheet.CreateFreezePane(0, 1);

        // 写数据行
        int rowIndex = 1;
        foreach (var voucher in vouchers)
        {
            var entries = voucher.Entries.OrderBy(e => e.FLineNo).ToList();
            bool isFirstEntry = true;

            foreach (var entry in entries)
            {
                var row = sheet.CreateRow(rowIndex++);

                // 凭证级字段仅第一行写入
                if (isFirstEntry)
                {
                    row.CreateCell(0).SetCellValue(voucher.FVoucherWord);
                    row.CreateCell(1).SetCellValue(voucher.FVoucherNo);
                    row.CreateCell(2).SetCellValue(voucher.FDate.ToString("yyyy-MM-dd"));
                    row.CreateCell(3).SetCellValue(voucher.FAttachmentCount);
                    isFirstEntry = false;
                }
                else
                {
                    row.CreateCell(0).SetCellValue("");
                    row.CreateCell(1).SetCellValue("");
                    row.CreateCell(2).SetCellValue("");
                    row.CreateCell(3).SetCellValue("");
                }

                row.CreateCell(4).SetCellValue(entry.FLineNo);
                row.CreateCell(5).SetCellValue(entry.FSummary);

                // 科目编码和名称
                if (accountDict.TryGetValue(entry.FAccountId, out var acct))
                {
                    row.CreateCell(6).SetCellValue(acct.Code);
                    row.CreateCell(7).SetCellValue(acct.Name);
                }
                else
                {
                    row.CreateCell(6).SetCellValue(entry.FAccountCode);
                    row.CreateCell(7).SetCellValue(entry.FAccountName);
                }

                row.CreateCell(8).SetCellValue((double)entry.FDebitAmount);
                row.CreateCell(9).SetCellValue((double)entry.FCreditAmount);
                row.CreateCell(10).SetCellValue(FormatAuxiliary(entry.FAuxiliaryJson));
            }
        }

        // 自动列宽
        for (int i = 0; i < Headers.Length; i++)
        {
            sheet.AutoSizeColumn(i);
            if (sheet.GetColumnWidth(i) < 3000)
                sheet.SetColumnWidth(i, 3000);
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, true);
        return ms.ToArray();
    }

    /// <summary>导出凭证模板（表头+1行示例数据）</summary>
    public byte[] ExportTemplate()
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("凭证数据");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < Headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(Headers[i]);
            cell.CellStyle = headerStyle;
        }

        // 示例数据行
        var sampleRow = sheet.CreateRow(1);
        sampleRow.CreateCell(0).SetCellValue(VoucherWord.Ji);
        sampleRow.CreateCell(1).SetCellValue(1);
        sampleRow.CreateCell(2).SetCellValue("2026-01-01");
        sampleRow.CreateCell(3).SetCellValue(0);
        sampleRow.CreateCell(4).SetCellValue(1);
        sampleRow.CreateCell(5).SetCellValue("示例摘要");
        sampleRow.CreateCell(6).SetCellValue("1001");
        sampleRow.CreateCell(7).SetCellValue("库存现金");
        sampleRow.CreateCell(8).SetCellValue(100.00);
        sampleRow.CreateCell(9).SetCellValue("");
        sampleRow.CreateCell(10).SetCellValue("");

        sheet.CreateFreezePane(0, 1);

        for (int i = 0; i < Headers.Length; i++)
        {
            sheet.AutoSizeColumn(i);
            if (sheet.GetColumnWidth(i) < 3000)
                sheet.SetColumnWidth(i, 3000);
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, true);
        return ms.ToArray();
    }

    /// <summary>从Excel导入凭证</summary>
    public async Task<VoucherImportResult> ImportFromExcel(Stream stream, string fileName, long accountSetId, string currentUser)
    {
        var result = new VoucherImportResult();
        var errors = new List<VoucherImportError>();

        // 1. 打开Excel
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        IWorkbook workbook = extension == ".xls"
            ? new HSSFWorkbook(ms)
            : new XSSFWorkbook(ms);

        try
        {
            var sheet = workbook.GetSheetAt(0);
            if (sheet == null)
            {
                result.Errors.Add(new VoucherImportError { RowNumber = 0, Message = "Excel文件中没有工作表" });
                return result;
            }

            // 2. 验证表头
            var headerRow = sheet.GetRow(0);
            if (headerRow == null)
            {
                result.Errors.Add(new VoucherImportError { RowNumber = 1, Message = "缺少表头行" });
                return result;
            }

            for (int i = 0; i < Headers.Length; i++)
            {
                var cellValue = GetCellString(headerRow.GetCell(i));
                if (cellValue != Headers[i])
                {
                    result.Errors.Add(new VoucherImportError { RowNumber = 1, Message = $"第{i + 1}列表头应为\"{Headers[i]}\"，实际为\"{cellValue}\"" });
                }
            }
            if (result.Errors.Count > 0)
                return result;

            // 3. 构建科目编码->ID映射
            var accounts = await _context.Set<FinAccount>()
                .Where(a => a.FAccountSetId == accountSetId)
                .AsNoTracking()
                .Select(a => new { a.FID, a.FCode, a.FName })
                .ToListAsync();
            var codeToAccountDict = accounts.ToDictionary(a => a.FCode, a => (a.FID, a.FName));

            // 4. 查询期间列表（用于日期→期间匹配）
            var periods = await _context.Set<FinAccountPeriod>()
                .Where(p => p.FAccountSetId == accountSetId)
                .AsNoTracking()
                .ToListAsync();

            // 5. 逐行解析，按凭证分组
            var voucherGroups = new List<VoucherParseGroup>();
            VoucherParseGroup? currentGroup = null;

            for (int rowIdx = 1; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                // 检测全空行
                bool hasData = false;
                for (int c = 0; c < Headers.Length; c++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCellString(row.GetCell(c))))
                    {
                        hasData = true;
                        break;
                    }
                }
                if (!hasData) continue;

                var excelRow = rowIdx + 1;
                var voucherWord = GetCellString(row.GetCell(0)).Trim();
                var voucherNoStr = GetCellString(row.GetCell(1)).Trim();
                var dateStr = GetCellString(row.GetCell(2)).Trim();
                var attachStr = GetCellString(row.GetCell(3)).Trim();
                var lineNoStr = GetCellString(row.GetCell(4)).Trim();
                var summary = GetCellString(row.GetCell(5)).Trim();
                var accountCode = GetCellString(row.GetCell(6)).Trim();
                // 列7（科目名称）导入时忽略
                var debitStr = GetCellString(row.GetCell(8)).Trim();
                var creditStr = GetCellString(row.GetCell(9)).Trim();
                var auxiliaryStr = GetCellString(row.GetCell(10)).Trim();

                // 凭证字非空 → 新凭证
                if (!string.IsNullOrEmpty(voucherWord))
                {
                    // 验证凭证字
                    if (voucherWord != "记" && voucherWord != "收" && voucherWord != "付" && voucherWord != "转")
                        errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"凭证字只能是 记/收/付/转，当前值: \"{voucherWord}\"" });

                    // 解析凭证号
                    int voucherNo = 0;
                    if (!string.IsNullOrEmpty(voucherNoStr))
                    {
                        if (!int.TryParse(voucherNoStr.Replace(".0", ""), out voucherNo) || voucherNo <= 0)
                            errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"凭证号格式错误: \"{voucherNoStr}\"" });
                    }

                    // 解析日期
                    DateTime voucherDate = DateTime.MinValue;
                    if (!string.IsNullOrEmpty(dateStr))
                    {
                        if (!DateTime.TryParse(dateStr, out voucherDate))
                            errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"日期格式错误: \"{dateStr}\"，请使用YYYY-MM-DD格式" });
                    }
                    else
                    {
                        errors.Add(new VoucherImportError { RowNumber = excelRow, Message = "凭证日期不能为空" });
                    }

                    // 解析附件数
                    int attachCount = 0;
                    if (!string.IsNullOrEmpty(attachStr))
                    {
                        int.TryParse(attachStr.Replace(".0", ""), out attachCount);
                    }

                    // 匹配期间
                    long periodId = 0;
                    if (voucherDate != DateTime.MinValue)
                    {
                        var period = periods.FirstOrDefault(p => voucherDate >= p.FStartDate && voucherDate <= p.FEndDate);
                        if (period == null)
                            errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"日期 {dateStr} 找不到对应的会计期间" });
                        else
                            periodId = period.FID;
                    }

                    currentGroup = new VoucherParseGroup
                    {
                        StartRow = excelRow,
                        VoucherWord = voucherWord,
                        VoucherNo = voucherNo,
                        VoucherDate = voucherDate,
                        AttachmentCount = attachCount,
                        PeriodId = periodId
                    };
                    voucherGroups.Add(currentGroup);
                }

                if (currentGroup == null)
                {
                    errors.Add(new VoucherImportError { RowNumber = excelRow, Message = "分录行缺少所属凭证（第一行需填写凭证字）" });
                    continue;
                }

                // 解析分录行
                int lineNo = 0;
                if (!string.IsNullOrEmpty(lineNoStr))
                {
                    if (!int.TryParse(lineNoStr.Replace(".0", ""), out lineNo))
                        errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"行号格式错误: \"{lineNoStr}\"" });
                }

                // 科目编码验证
                long accountId = 0;
                string accountName = "";
                if (string.IsNullOrEmpty(accountCode))
                {
                    errors.Add(new VoucherImportError { RowNumber = excelRow, Message = "科目编码不能为空" });
                }
                else if (!codeToAccountDict.TryGetValue(accountCode, out var acctInfo))
                {
                    errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"科目编码\"{accountCode}\"在账套中不存在" });
                }
                else
                {
                    accountId = acctInfo.FID;
                    accountName = acctInfo.FName;
                }

                // 借贷金额
                decimal debitAmount = 0;
                decimal creditAmount = 0;
                if (!string.IsNullOrEmpty(debitStr))
                {
                    if (!decimal.TryParse(debitStr, out debitAmount))
                        errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"借方金额格式错误: \"{debitStr}\"" });
                }
                if (!string.IsNullOrEmpty(creditStr))
                {
                    if (!decimal.TryParse(creditStr, out creditAmount))
                        errors.Add(new VoucherImportError { RowNumber = excelRow, Message = $"贷方金额格式错误: \"{creditStr}\"" });
                }
                if (debitAmount == 0 && creditAmount == 0)
                    errors.Add(new VoucherImportError { RowNumber = excelRow, Message = "借方金额和贷方金额至少填写一个" });

                // 辅助核算解析
                string? auxiliaryJson = null;
                if (!string.IsNullOrEmpty(auxiliaryStr))
                {
                    auxiliaryJson = ParseAuxiliary(auxiliaryStr, excelRow, errors);
                }

                currentGroup.Entries.Add(new EntryParseData
                {
                    RowNumber = excelRow,
                    LineNo = lineNo,
                    Summary = summary,
                    AccountId = accountId,
                    AccountCode = accountCode,
                    AccountName = accountName,
                    DebitAmount = debitAmount,
                    CreditAmount = creditAmount,
                    AuxiliaryJson = auxiliaryJson
                });
            }

            // 6. 验证每张凭证借贷平衡
            foreach (var group in voucherGroups)
            {
                var totalDebit = group.Entries.Sum(e => e.DebitAmount);
                var totalCredit = group.Entries.Sum(e => e.CreditAmount);
                if (totalDebit != totalCredit)
                {
                    errors.Add(new VoucherImportError
                    {
                        RowNumber = group.StartRow,
                        Message = $"凭证(凭证字:{group.VoucherWord} 凭证号:{group.VoucherNo})借贷不平衡，借方合计:{totalDebit}，贷方合计:{totalCredit}"
                    });
                }
            }

            // 7. 如有错误返回
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }

            // 8. 开启事务插入
            var now = DateTime.Now;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var group in voucherGroups)
                    {
                        var voucher = new FinVoucher
                        {
                            FVoucherWord = group.VoucherWord,
                            FVoucherNo = group.VoucherNo,
                            FDate = group.VoucherDate,
                            FPeriodId = group.PeriodId,
                            FAttachmentCount = group.AttachmentCount,
                            FCreator = currentUser,
                            FStatus = 0,
                            FSource = "Excel导入",
                            FAccountSetId = accountSetId,
                            FOrgId = 0,
                            FCreatedTime = now,
                            FUpdatedTime = now
                        };

                        _context.Set<FinVoucher>().Add(voucher);
                        await _context.SaveChangesAsync();

                        var entries = group.Entries.Select((e, idx) => new FinVoucherEntry
                        {
                            FVoucherId = voucher.FID,
                            FLineNo = e.LineNo > 0 ? e.LineNo : idx + 1,
                            FSummary = e.Summary,
                            FAccountId = e.AccountId,
                            FAccountCode = e.AccountCode,
                            FAccountName = e.AccountName,
                            FAuxiliaryJson = e.AuxiliaryJson,
                            FDebitAmount = e.DebitAmount,
                            FCreditAmount = e.CreditAmount,
                            FCreatedTime = now,
                            FUpdatedTime = now
                        }).ToList();

                        await _context.Set<FinVoucherEntry>().AddRangeAsync(entries);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            result.Success = true;
            result.ImportedCount = voucherGroups.Count;
            return result;
        }
        finally
        {
            workbook.Close();
        }
    }

    #region 辅助方法

    /// <summary>辅助核算JSON → 人类可读格式 (客户:A公司,部门:市场部)</summary>
    private static string FormatAuxiliary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    parts.Add($"{prop.Name}:{prop.Value.GetString() ?? prop.Value.ToString()}");
                }
                return string.Join(",", parts);
            }
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var value = item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(name))
                        parts.Add($"{name}:{value}");
                }
                return string.Join(",", parts);
            }
            return json;
        }
        catch
        {
            return json;
        }
    }

    /// <summary>人类可读辅助核算 → JSON (客户:A公司,部门:市场部 → {"客户":"A公司","部门":"市场部"})</summary>
    private static string? ParseAuxiliary(string text, int rowNumber, List<VoucherImportError> errors)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 如果已经是JSON格式，直接返回
        if (text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("["))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                return text;
            }
            catch { /* 非法JSON，按简化格式解析 */ }
        }

        var pairs = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dict = new Dictionary<string, string>();
        foreach (var pair in pairs)
        {
            var colonIdx = pair.IndexOf(':');
            if (colonIdx <= 0)
            {
                errors.Add(new VoucherImportError { RowNumber = rowNumber, Message = $"辅助核算格式错误: \"{pair}\"，应为\"类别:值\"格式（英文冒号分隔）" });
                continue;
            }
            var key = pair[..colonIdx].Trim();
            var value = pair[(colonIdx + 1)..].Trim();
            dict[key] = value;
        }
        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }

    /// <summary>安全获取单元格字符串值</summary>
    private static string GetCellString(ICell? cell)
    {
        if (cell == null) return "";
        return cell.CellType switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue?.ToString("yyyy-MM-dd") ?? ""
                : cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CachedFormulaResultType switch
            {
                CellType.Numeric => cell.NumericCellValue.ToString(),
                CellType.String => cell.StringCellValue ?? "",
                _ => cell.ToString() ?? ""
            },
            _ => cell.ToString()?.Trim() ?? ""
        };
    }

    #endregion

    #region 内部数据结构

    private class VoucherParseGroup
    {
        public int StartRow { get; set; }
        public string VoucherWord { get; set; } = "";
        public int VoucherNo { get; set; }
        public DateTime VoucherDate { get; set; }
        public int AttachmentCount { get; set; }
        public long PeriodId { get; set; }
        public List<EntryParseData> Entries { get; set; } = new();
    }

    private class EntryParseData
    {
        public int RowNumber { get; set; }
        public int LineNo { get; set; }
        public string Summary { get; set; } = "";
        public long AccountId { get; set; }
        public string AccountCode { get; set; } = "";
        public string AccountName { get; set; } = "";
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string? AuxiliaryJson { get; set; }
    }

    #endregion
}
