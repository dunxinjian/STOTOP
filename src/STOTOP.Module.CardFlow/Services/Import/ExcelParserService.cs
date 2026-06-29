using System.Text;
using MiniExcelLibs;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace STOTOP.Module.CardFlow.Services.Import;

public class ExcelParserService
{
    /// <summary>
    /// 读取表头行
    /// </summary>
    public List<string> ReadHeaders(Stream fileStream, string fileName, int headerRow = 1)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".csv")
            return ReadCsvHeaders(fileStream, headerRow);

        return ReadExcelHeaders(fileStream, extension, headerRow);
    }

    /// <summary>
    /// 异步读取表头行（首行列名）。复用 ParseAsync（batchSize=1 取首批 keys），
    /// 因此与 ParseAsync 共享魔数判别 + 多 sheet 选择能力，适合多文件内容路由场景。
    /// </summary>
    /// <param name="headerRow">表头行号（1-based）</param>
    /// <param name="sheetName">可选 sheet 名（仅 Excel 生效），见 ParseAsync 说明</param>
    public async Task<IReadOnlyList<string>> ReadHeadersAsync(
        Stream fileStream, string fileName, int headerRow, string? sheetName = null, CancellationToken ct = default)
    {
        var cols = new List<string>();
        await ParseAsync(fileStream, fileName, headerRow, headerRow + 1, batchSize: 1,
            (rows, _) => { if (cols.Count == 0 && rows.Count > 0) cols.AddRange(rows[0].Keys); return Task.CompletedTask; },
            ct, sheetName);
        return cols;
    }

    /// <summary>
    /// 分批流式读取数据行，每批 batchSize 行，通过回调处理
    /// </summary>
    /// <param name="headerRow">表头行号（1-based）</param>
    /// <param name="dataStartRow">数据起始行号（1-based），默认 = headerRow + 1</param>
    /// <param name="sheetName">
    /// 可选：指定要解析的 Excel sheet 名（仅对 Excel 生效，CSV 忽略）。
    /// 命中则取该 sheet；为 null/空或不存在时退回首个 sheet（行为不变）。
    /// 用于多 sheet 源（如物流信息指数）把各 sheet 各导入一张表。
    /// </param>
    public async Task ParseAsync(
        Stream fileStream,
        string fileName,
        int headerRow,
        int dataStartRow,
        int batchSize,
        Func<List<Dictionary<string, string>>, int, Task> batchCallback,
        CancellationToken ct = default,
        string? sheetName = null)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".csv")
        {
            await ParseCsvAsync(fileStream, headerRow, dataStartRow, batchSize, batchCallback, ct);
        }
        else
        {
            // 这批申通网点数据普遍存在「扩展名与真实格式不符」（如 xlsx 内容被命名为 .xls，
            // 或反向）。按文件头魔数判别真实格式后再选引擎，避免 MiniExcel 读 OLE2 报
            // "file type could not be inferred" 或 NPOI 读错格式。
            var realExtension = await SniffExcelFormatAsync(fileStream, extension, ct);
            await ParseExcelAsync(fileStream, realExtension, headerRow, dataStartRow, batchSize, batchCallback, ct, sheetName);
        }
    }

    /// <summary>
    /// 读取前N行预览数据
    /// </summary>
    public List<Dictionary<string, object?>> ReadPreviewRows(Stream fileStream, string fileName, int headerRow, int rowCount = 10)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".csv")
            return ReadCsvPreviewRows(fileStream, headerRow, rowCount);

        return ReadExcelPreviewRows(fileStream, extension, headerRow, rowCount);
    }

    #region Excel 解析

    private static List<string> ReadExcelHeaders(Stream fileStream, string extension, int headerRow)
    {
        if (extension == ".xls")
        {
            // .xls 是二进制格式，MiniExcel 不支持，使用 NPOI
            // .xls 文件最大 ~65536 行，不会触发 2GB MemoryStream 限制
            return ReadExcelHeadersWithNpoi(fileStream, headerRow);
        }
        else
        {
            // .xlsx 使用 MiniExcel 流式读取，避免大文件 "Stream was too long" 内存溢出
            return ReadExcelHeadersWithMiniExcel(fileStream, headerRow);
        }
    }

    private static List<string> ReadExcelHeadersWithNpoi(Stream fileStream, int headerRow)
    {
        var prepared = PrepareStream(fileStream, out var ownsStream);
        try
        {
            var workbook = CreateWorkbook(prepared, ".xls");
            try
            {
                var sheet = workbook.GetSheetAt(0);
                var headerRowObj = sheet.GetRow(headerRow - 1);
                if (headerRowObj == null) return new List<string>();

                var headers = new List<string>();
                for (int i = 0; i < headerRowObj.LastCellNum; i++)
                {
                    var cell = headerRowObj.GetCell(i);
                    var value = cell?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(value)) break;
                    headers.Add(value);
                }
                return headers;
            }
            finally
            {
                workbook.Close();
            }
        }
        finally
        {
            if (ownsStream) prepared.Dispose();
        }
    }

    private static List<string> ReadExcelHeadersWithMiniExcel(Stream fileStream, int headerRow)
    {
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        var row = MiniExcel.Query(fileStream, useHeaderRow: false, startCell: "A1")
            .Cast<IDictionary<string, object>>()
            .Skip(headerRow - 1)
            .FirstOrDefault();

        if (row == null) return new List<string>();

        return row.Values
            .Select(v => v?.ToString()?.Trim() ?? string.Empty)
            .TakeWhile(h => !string.IsNullOrEmpty(h))
            .ToList();
    }

    /// <summary>
    /// 按文件头魔数判别 Excel 真实格式，返回 ".xlsx" 或 ".xls"。
    /// - PK\x03\x04（OOXML/ZIP）→ ".xlsx"
    /// - D0CF11E0（OLE2/BIFF）→ ".xls"
    /// 流不可 Seek 或魔数无法识别时，退回传入的扩展名（行为不变）。
    /// 读取后会恢复流的原始 Position，不影响后续解析。
    /// </summary>
    private static async Task<string> SniffExcelFormatAsync(Stream stream, string fallbackExtension, CancellationToken ct)
    {
        if (!stream.CanSeek) return fallbackExtension;

        var head = new byte[8];
        var pos = stream.Position;
        var n = await stream.ReadAsync(head.AsMemory(0, 8), ct);
        stream.Position = pos;

        if (n >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04)
            return ".xlsx"; // ZIP/OOXML
        if (n >= 8 && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0)
            return ".xls";  // OLE2/BIFF

        return fallbackExtension;
    }

    private static async Task ParseExcelAsync(
        Stream fileStream, string extension, int headerRow, int dataStartRow, int batchSize,
        Func<List<Dictionary<string, string>>, int, Task> batchCallback,
        CancellationToken ct, string? sheetName = null)
    {
        if (extension?.ToLowerInvariant() == ".xls")
        {
            // .xls 是二进制格式，最大 ~65536 行，不会触发 2GB MemoryStream 限制
            await ParseExcelWithNpoiAsync(fileStream, headerRow, dataStartRow, batchSize, batchCallback, ct, sheetName);
        }
        else
        {
            // .xlsx 使用 MiniExcel 流式解析，避免 NPOI 解压 ZIP 条目时 "Stream was too long" 错误
            await ParseExcelWithMiniExcelAsync(fileStream, headerRow, dataStartRow, batchSize, batchCallback, ct, sheetName);
        }
    }

    /// <summary>
    /// 使用 NPOI 解析 .xls 文件（二进制 BIFF 格式）
    /// </summary>
    private static async Task ParseExcelWithNpoiAsync(
        Stream fileStream, int headerRow, int dataStartRow, int batchSize,
        Func<List<Dictionary<string, string>>, int, Task> batchCallback,
        CancellationToken ct, string? sheetName = null)
    {
        var prepared = PrepareStream(fileStream, out var ownsStream);
        IWorkbook workbook = CreateWorkbook(prepared, ".xls");
        try
        {
            // 指定 sheet 名命中则取该 sheet，否则（为空/不存在）退回首表，行为不变
            var sheet = (!string.IsNullOrEmpty(sheetName) ? workbook.GetSheet(sheetName) : null)
                        ?? workbook.GetSheetAt(0);
            var headerRowObj = sheet.GetRow(headerRow - 1);
            if (headerRowObj == null) return;

            var headers = new List<string>();
            for (int i = 0; i < headerRowObj.LastCellNum; i++)
            {
                var cell = headerRowObj.GetCell(i);
                headers.Add(cell?.ToString()?.Trim() ?? $"Column{i}");
            }

            var batch = new List<Dictionary<string, string>>();
            int startRow = dataStartRow - 1; // dataStartRow 是 1-based，转为 0-based 索引
            int batchStartRow = dataStartRow; // 1-based 行号

            for (int rowIdx = startRow; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                ct.ThrowIfCancellationRequested();

                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                var rowData = new Dictionary<string, string>();
                bool hasData = false;
                for (int colIdx = 0; colIdx < headers.Count; colIdx++)
                {
                    var cell = row.GetCell(colIdx);
                    var value = GetCellStringValue(cell);
                    rowData[headers[colIdx]] = value;
                    if (!string.IsNullOrWhiteSpace(value)) hasData = true;
                }

                if (!hasData) continue; // 跳过空行

                batch.Add(rowData);

                if (batch.Count >= batchSize)
                {
                    await batchCallback(batch, batchStartRow);
                    batchStartRow += batch.Count;
                    batch = new List<Dictionary<string, string>>();
                }
            }

            if (batch.Count > 0)
            {
                await batchCallback(batch, batchStartRow);
            }
        }
        finally
        {
            workbook.Close();
            if (ownsStream) prepared.Dispose();
        }
    }

    /// <summary>
    /// 使用 MiniExcel 流式解析 .xlsx 文件，避免 NPOI 的 2GB MemoryStream 限制
    /// </summary>
    private static async Task ParseExcelWithMiniExcelAsync(
        Stream fileStream, int headerRow, int dataStartRow, int batchSize,
        Func<List<Dictionary<string, string>>, int, Task> batchCallback,
        CancellationToken ct, string? sheetName = null)
    {
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        // 指定 sheet 名命中则取该 sheet，否则（为空/不存在）退回首表，行为不变
        string? targetSheet = null;
        if (!string.IsNullOrEmpty(sheetName))
        {
            var names = MiniExcel.GetSheetNames(fileStream);
            if (names.Contains(sheetName))
                targetSheet = sheetName;
            if (fileStream.CanSeek)
                fileStream.Position = 0;
        }

        // MiniExcel.Query 返回 IEnumerable，流式遍历不加载整个文件到内存
        var rows = MiniExcel.Query(fileStream, useHeaderRow: false, sheetName: targetSheet)
            .Cast<IDictionary<string, object>>();

        List<string>? headers = null;
        var batch = new List<Dictionary<string, string>>();
        int currentRow = 0; // 1-based 行号计数器
        int batchStartRow = dataStartRow;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            currentRow++;

            if (currentRow == headerRow)
            {
                // 提取表头：遇到空列名则停止（与 NPOI 版本的 ReadHeaders 行为一致）
                headers = row.Values
                    .Select(v => v?.ToString()?.Trim() ?? string.Empty)
                    .ToList();
                continue;
            }

            if (currentRow < dataStartRow) continue;
            if (headers == null) continue;

            // 构建数据行字典
            var dict = new Dictionary<string, string>();
            var values = row.Values.ToList();
            bool hasData = false;

            for (int i = 0; i < headers.Count && i < values.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrEmpty(header))
                {
                    // 空列名视为列边界，停止（与 NPOI 逻辑一致：headers 有 Column{i} 占位）
                    // 这里用 Column{i} 占位保持兼容
                    header = $"Column{i}";
                }
                var cellValue = values[i]?.ToString()?.Trim() ?? string.Empty;
                dict[header] = cellValue;
                if (!string.IsNullOrWhiteSpace(cellValue)) hasData = true;
            }

            if (!hasData) continue; // 跳过全空行

            batch.Add(dict);

            if (batch.Count >= batchSize)
            {
                await batchCallback(batch, batchStartRow);
                batchStartRow += batch.Count;
                batch = new List<Dictionary<string, string>>();
            }
        }

        // 处理剩余的数据
        if (batch.Count > 0)
        {
            await batchCallback(batch, batchStartRow);
        }
    }

    private static List<Dictionary<string, object?>> ReadExcelPreviewRows(Stream fileStream, string extension, int headerRow, int rowCount)
    {
        var prepared = PrepareStream(fileStream, out var ownsStream);
        IWorkbook workbook = CreateWorkbook(prepared, extension);
        try
        {
            var sheet = workbook.GetSheetAt(0);
            var headerRowObj = sheet.GetRow(headerRow - 1);
            if (headerRowObj == null) return new List<Dictionary<string, object?>>();

            var headers = new List<string>();
            for (int i = 0; i < headerRowObj.LastCellNum; i++)
            {
                var cell = headerRowObj.GetCell(i);
                headers.Add(cell?.ToString()?.Trim() ?? $"Column{i}");
            }

            var result = new List<Dictionary<string, object?>>();
            int startRow = headerRow; // 0-based

            for (int rowIdx = startRow; rowIdx <= sheet.LastRowNum && result.Count < rowCount; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                var rowData = new Dictionary<string, object?>();
                bool hasData = false;
                for (int colIdx = 0; colIdx < headers.Count; colIdx++)
                {
                    var cell = row.GetCell(colIdx);
                    rowData[headers[colIdx]] = GetCellValue(cell);
                    if (cell != null && cell.CellType != CellType.Blank) hasData = true;
                }

                if (hasData) result.Add(rowData);
            }

            return result;
        }
        finally
        {
            workbook.Close();
            if (ownsStream) prepared.Dispose();
        }
    }

    /// <summary>
    /// 确保流可用于 NPOI 解析。对于 FileStream / MemoryStream 等可 Seek 的流，
    /// 直接重置 Position 并返回（避免大文件复制导致 "Stream was too long" 异常）；
    /// 对于不可 Seek 的流（如网络流），复制到 MemoryStream。
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="ownsStream">如果为 true，表示返回的流是新创建的，调用方需负责 Dispose</param>
    private static Stream PrepareStream(Stream source, out bool ownsStream)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
            ownsStream = false;
            return source;
        }

        // 不可 Seek 的流（如网络流），需要复制到 MemoryStream
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        ownsStream = true;
        return ms;
    }

    private static IWorkbook CreateWorkbook(Stream stream, string extension)
    {
        return WorkbookFactory.Create(stream);
    }

    private static string GetCellStringValue(ICell? cell)
    {
        if (cell == null) return string.Empty;
        return cell.CellType switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
                : cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CachedFormulaResultType switch
            {
                CellType.Numeric => cell.NumericCellValue.ToString(),
                CellType.String => cell.StringCellValue ?? string.Empty,
                _ => cell.ToString() ?? string.Empty
            },
            _ => cell.ToString()?.Trim() ?? string.Empty
        };
    }

    private static object? GetCellValue(ICell? cell)
    {
        if (cell == null) return null;
        return cell.CellType switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue
                : cell.NumericCellValue,
            CellType.Boolean => cell.BooleanCellValue,
            CellType.Blank => null,
            CellType.Formula => cell.CachedFormulaResultType switch
            {
                CellType.Numeric => cell.NumericCellValue,
                CellType.String => cell.StringCellValue,
                _ => cell.ToString()
            },
            _ => cell.ToString()?.Trim()
        };
    }

    #endregion

    #region CSV 解析

    private static List<string> ReadCsvHeaders(Stream fileStream, int headerRow)
    {
        fileStream.Position = 0;
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        string? line = null;
        for (int i = 0; i < headerRow; i++)
        {
            line = reader.ReadLine();
            if (line == null) return new List<string>();
        }
        return ParseCsvLine(line!);
    }

    private static async Task ParseCsvAsync(
        Stream fileStream, int headerRow, int dataStartRow, int batchSize,
        Func<List<Dictionary<string, string>>, int, Task> batchCallback,
        CancellationToken ct)
    {
        fileStream.Position = 0;
        using var reader = new StreamReader(fileStream, leaveOpen: true);

        // 读到表头行
        string? headerLine = null;
        for (int i = 0; i < headerRow; i++)
        {
            headerLine = await reader.ReadLineAsync(ct);
            if (headerLine == null) return;
        }

        var headers = ParseCsvLine(headerLine!);

        // 跳过 headerRow 与 dataStartRow 之间的行
        for (int skip = headerRow + 1; skip < dataStartRow; skip++)
        {
            if (reader.Peek() == -1) return;
            await reader.ReadLineAsync(ct);
        }

        var batch = new List<Dictionary<string, string>>();
        int lineNumber = dataStartRow;
        int batchStartRow = lineNumber;

        while (reader.Peek() != -1)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
            {
                lineNumber++;
                continue;
            }

            var values = ParseCsvLine(line);
            var rowData = new Dictionary<string, string>();
            for (int i = 0; i < headers.Count; i++)
            {
                rowData[headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            batch.Add(rowData);
            lineNumber++;

            if (batch.Count >= batchSize)
            {
                await batchCallback(batch, batchStartRow);
                batchStartRow = lineNumber;
                batch = new List<Dictionary<string, string>>();
            }
        }

        if (batch.Count > 0)
        {
            await batchCallback(batch, batchStartRow);
        }
    }

    private static List<Dictionary<string, object?>> ReadCsvPreviewRows(Stream fileStream, int headerRow, int rowCount)
    {
        fileStream.Position = 0;
        using var reader = new StreamReader(fileStream, leaveOpen: true);

        string? headerLine = null;
        for (int i = 0; i < headerRow; i++)
        {
            headerLine = reader.ReadLine();
            if (headerLine == null) return new List<Dictionary<string, object?>>();
        }

        var headers = ParseCsvLine(headerLine!);
        var result = new List<Dictionary<string, object?>>();

        while (!reader.EndOfStream && result.Count < rowCount)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = ParseCsvLine(line);
            var rowData = new Dictionary<string, object?>();
            for (int i = 0; i < headers.Count; i++)
            {
                rowData[headers[i]] = i < values.Count ? values[i] : null;
            }
            result.Add(rowData);
        }

        return result;
    }

    /// <summary>
    /// CSV 行解析，支持引号转义
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 跳过转义的引号
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    #endregion
}
