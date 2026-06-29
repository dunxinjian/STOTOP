using STOTOP.Module.CardFlow.Services.Import;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 验证 Excel 解析按文件头魔数（而非扩展名）判别 xls/xlsx，
/// 兼容这批申通网点数据里普遍存在的「扩展名与真实格式不符」文件。
/// </summary>
public class ExcelParserServiceFormatTests
{
    // 内容实为 xlsx（PK\x03\x04 头），但扩展名是 .xls
    private const string MisnamedXlsxAsXls =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细\b20606c963a5471eaf1e443c1aa42564抖音照片质检.xls";

    // 内容实为 OLE2/BIFF（D0CF 头）的真 .xls 文件
    private const string RealXls =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细\excel (未到件).xls";

    private static async Task<List<string>> ReadHeadersViaParseAsync(string path, string declaredFileName)
    {
        var svc = new ExcelParserService();
        var headers = new List<string>();
        await using var fs = File.OpenRead(path);
        await svc.ParseAsync(fs, declaredFileName, headerRow: 1, dataStartRow: 2, batchSize: 50,
            (rows, _) => { if (headers.Count == 0 && rows.Count > 0) headers.AddRange(rows[0].Keys); return Task.CompletedTask; });
        return headers;
    }

    [Fact]
    public async Task ParseAsync_ContentIsXlsxButExtensionIsXls_ReadsHeaders()
    {
        var headers = await ReadHeadersViaParseAsync(MisnamedXlsxAsXls, "x.xls");
        Assert.Contains("单号", headers);
        Assert.Contains("是否质检合格", headers);
    }

    [Fact]
    public async Task ParseAsync_ContentIsXlsButExtensionIsXlsx_ReadsHeaders()
    {
        // 反向场景：真 OLE2 .xls 内容，却被声明为 .xlsx。
        // 按扩展名会走 MiniExcel（不支持二进制 BIFF）而失败；按魔数应改走 NPOI。
        var headers = await ReadHeadersViaParseAsync(RealXls, "x.xlsx");
        Assert.Contains("运单号", headers);
        Assert.Contains("问题类型", headers);
    }
}
