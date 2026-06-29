namespace STOTOP.Infrastructure.Services;

/// <summary>
/// Excel 解析抽象（按列映射输出 Schema 字段 JSON 行）
/// </summary>
public interface IExcelParserService
{
    /// <summary>
    /// 解析 Excel 文件（.xlsx / .xls / .csv），按 columnMapping 将原始表头映射为 Schema 字段名
    /// </summary>
    /// <param name="filePath">磁盘文件绝对路径</param>
    /// <param name="columnMapping">key=Excel 表头, value=Schema 字段名（如 "金额"→"amount"）</param>
    Task<List<ExcelRowData>> ParseAsync(string filePath, Dictionary<string, string> columnMapping);
}

/// <summary>
/// 单行解析结果
/// </summary>
public class ExcelRowData
{
    /// <summary>行号（1-based，含表头偏移后的物理行号）</summary>
    public int RowNo { get; set; }
    /// <summary>映射后的字段-值 JSON</summary>
    public string DataJson { get; set; } = string.Empty;
}
