using System.Text.Json;
using MiniExcelLibs;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace STOTOP.Infrastructure.Services;

/// <summary>
/// Excel 解析服务实现：
/// - .xlsx 走 MiniExcel 流式读取（避免大文件内存溢出）
/// - .xls 走 NPOI HSSF（二进制格式，MiniExcel 不支持）
/// - .csv 走简单文本解析
/// </summary>
public class ExcelParserService : IExcelParserService
{
    public Task<List<ExcelRowData>> ParseAsync(string filePath, Dictionary<string, string> columnMapping)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("filePath 不能为空", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel 文件不存在", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => Task.FromResult(ParseXlsx(filePath, columnMapping)),
            ".xls"  => Task.FromResult(ParseXls(filePath, columnMapping)),
            ".csv"  => Task.FromResult(ParseCsv(filePath, columnMapping)),
            _ => throw new NotSupportedException($"不支持的文件类型：{ext}")
        };
    }

    private static List<ExcelRowData> ParseXlsx(string filePath, Dictionary<string, string> columnMapping)
    {
        var result = new List<ExcelRowData>();
        // MiniExcel 第一行为表头（按字典展开）
        var rows = MiniExcel.Query(filePath, useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();
        int rowNo = 1; // 表头为第 1 行；数据从第 2 行起
        foreach (var row in rows)
        {
            rowNo++;
            var mapped = MapRow(row, columnMapping);
            result.Add(new ExcelRowData
            {
                RowNo = rowNo,
                DataJson = JsonSerializer.Serialize(mapped)
            });
        }
        return result;
    }

    private static List<ExcelRowData> ParseXls(string filePath, Dictionary<string, string> columnMapping)
    {
        var result = new List<ExcelRowData>();
        using var fs = File.OpenRead(filePath);
        IWorkbook wb = new HSSFWorkbook(fs);
        var sheet = wb.GetSheetAt(0);
        if (sheet == null || sheet.LastRowNum < 0) return result;

        // 表头：第 0 行
        var headerRow = sheet.GetRow(0);
        if (headerRow == null) return result;
        var headers = new List<string>();
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            headers.Add(headerRow.GetCell(c)?.ToString()?.Trim() ?? string.Empty);
        }

        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;
            var dict = new Dictionary<string, object?>();
            for (int c = 0; c < headers.Count; c++)
            {
                if (string.IsNullOrEmpty(headers[c])) continue;
                dict[headers[c]] = row.GetCell(c)?.ToString();
            }
            var mapped = MapRow(dict, columnMapping);
            result.Add(new ExcelRowData
            {
                RowNo = r + 1,
                DataJson = JsonSerializer.Serialize(mapped)
            });
        }
        return result;
    }

    private static List<ExcelRowData> ParseCsv(string filePath, Dictionary<string, string> columnMapping)
    {
        var result = new List<ExcelRowData>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0) return result;

        var headers = SplitCsvLine(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i]);
            var dict = new Dictionary<string, object?>();
            for (int c = 0; c < headers.Length && c < cells.Length; c++)
            {
                if (string.IsNullOrEmpty(headers[c])) continue;
                dict[headers[c]] = cells[c];
            }
            var mapped = MapRow(dict, columnMapping);
            result.Add(new ExcelRowData
            {
                RowNo = i + 1,
                DataJson = JsonSerializer.Serialize(mapped)
            });
        }
        return result;
    }

    private static Dictionary<string, object?> MapRow<TVal>(IDictionary<string, TVal> row, Dictionary<string, string> columnMapping)
    {
        var mapped = new Dictionary<string, object?>();
        if (columnMapping == null || columnMapping.Count == 0)
        {
            foreach (var kv in row) mapped[kv.Key] = kv.Value;
            return mapped;
        }
        foreach (var (excelHeader, schemaField) in columnMapping)
        {
            if (row.TryGetValue(excelHeader, out var value))
            {
                mapped[schemaField] = value;
            }
            else
            {
                mapped[schemaField] = null;
            }
        }
        return mapped;
    }

    private static string[] SplitCsvLine(string line)
    {
        // 简化处理：不支持嵌入逗号的引号字段。CardFlow 批量场景默认 Excel
        return line.Split(',').Select(s => s.Trim().Trim('"')).ToArray();
    }
}
