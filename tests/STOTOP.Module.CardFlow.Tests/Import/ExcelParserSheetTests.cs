using STOTOP.Module.CardFlow.Services.Import;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 验证 Excel 解析支持按 sheet 名选择，以便把多 sheet 源（如物流信息指数）
/// 的各 sheet 各导入一张表。
/// </summary>
public class ExcelParserSheetTests
{
    // 真 OLE2(.xls)，含 3 个 sheet：及时性汇总（按天）/完整性汇总（按天）/准确性汇总（按天）
    // 其中「完整性」sheet 独有列「揽收缺失量」
    private const string 物流信息指数Xls =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页\excel（物流信息指数）.xls";

    private static async Task<List<string>> ReadHeadersViaParseAsync(string path, string declaredFileName, string? sheetName)
    {
        var svc = new ExcelParserService();
        var headers = new List<string>();
        await using var fs = File.OpenRead(path);
        await svc.ParseAsync(fs, declaredFileName, headerRow: 1, dataStartRow: 2, batchSize: 50,
            (rows, _) => { if (headers.Count == 0 && rows.Count > 0) headers.AddRange(rows[0].Keys); return Task.CompletedTask; },
            sheetName: sheetName);
        return headers;
    }

    [Fact]
    public async Task ParseAsync_WithSheetName_ReadsThatSheet()
    {
        var headers = await ReadHeadersViaParseAsync(物流信息指数Xls, "x.xls", "完整性汇总（按天）");
        Assert.Contains("揽收缺失量", headers); // 完整性 sheet 独有列
    }

    [Fact]
    public async Task ParseAsync_WithoutSheetName_ReadsFirstSheet()
    {
        // 不传 sheetName 时行为不变：读首个「及时性」sheet，不含「揽收缺失量」
        var headers = await ReadHeadersViaParseAsync(物流信息指数Xls, "x.xls", sheetName: null);
        Assert.DoesNotContain("揽收缺失量", headers);
    }

    [Fact]
    public async Task ParseAsync_WithUnknownSheetName_FallsBackToFirstSheet()
    {
        // sheetName 不存在时安全退回首表
        var headers = await ReadHeadersViaParseAsync(物流信息指数Xls, "x.xls", sheetName: "不存在的表");
        Assert.NotEmpty(headers);
        Assert.DoesNotContain("揽收缺失量", headers);
    }
}
