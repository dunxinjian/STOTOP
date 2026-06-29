using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.OA.Entities;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 验证多文件内容路由上传入口（Task A3）：
/// 1) ExcelParserService.ReadHeadersAsync 复用 ParseAsync 取首行列名（真文件，无 DB）。
/// 2) BatchTriggerService.ClassifyFilesAsync 复用 MatchFlowDefinitionsAsync 按命中数分
///    routed(=1) / unmatched(0) / ambiguous(>1)（InMemory 种子流程，不触发导入）。
/// </summary>
public class AutoRouteUploadTests
{
    // 真 OLE2(.xls)，列含「运单号」「问题类型」
    private const string 未到件Xls =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细\excel (未到件).xls";

    // ─────────────────────────────────────────────
    // Step 1：ReadHeadersAsync 返回首行列名（真文件，无 DB）
    // ─────────────────────────────────────────────
    [Fact]
    public async Task ReadHeadersAsync_ReturnsHeaderColumns()
    {
        var svc = new ExcelParserService();
        await using var fs = File.OpenRead(未到件Xls);
        var cols = await svc.ReadHeadersAsync(fs, "excel (未到件).xls", headerRow: 1);
        Assert.Contains("运单号", cols);
        Assert.Contains("问题类型", cols);
    }

    // ─────────────────────────────────────────────
    // Step 5：ClassifyFilesAsync 按命中数分类（InMemory 种子流程）
    // ─────────────────────────────────────────────

    /// <summary>
    /// 构建一个真实 DI 容器：STOTOPDbContext 用固定 InMemory 库名，
    /// 这样种子用的 DbContext 与 BatchTriggerService 内部 CreateScope() 取到的是同一个库。
    /// </summary>
    private static (ServiceProvider Provider, string DbName) BuildProvider(string testName)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(OaExpenseRequest).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);

        var dbName = $"{testName}_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<STOTOPDbContext>(opt => opt
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging());
        return (services.BuildServiceProvider(), dbName);
    }

    private static BatchTriggerService CreateTriggerService(ServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var channel = Channel.CreateUnbounded<BatchJob>();
        return new BatchTriggerService(scopeFactory, channel, NullLogger<BatchTriggerService>.Instance);
    }

    /// <summary>
    /// 种子一条「申通承运商质量事件」流程：已发布流程定义 + 当前版本 + 首节点(excelInput 插件规则)，
    /// 规则的 columnIdentifier = "运单号,问题类型"（第二轮包含匹配）。
    /// </summary>
    private static async Task SeedFlowAsync(ServiceProvider provider, long orgId)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        db.Set<CfPluginRule>().Add(new CfPluginRule
        {
            FID = 7001,
            FOrgId = orgId,
            F类型编码 = "excelInput",
            F规则名称 = "申通承运商质量事件-导入",
            F规则配置JSON = """{"columnIdentifier":"运单号,问题类型"}""",
            F状态 = 1,
        });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 8001,
            FFlowName = "申通承运商质量事件",
            FFlowCode = "STO_QUALITY_EVENT",
            FStatus = "published",
            FOrgId = orgId,
            FCreatedTime = DateTime.Now,
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 9001,
            FFlowDefinitionId = 8001,
            FVersionNumber = 1,
            FStatus = "published",
            FIsCurrentVersion = true,
            FCreatedTime = DateTime.Now,
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 10001,
            FFlowVersionId = 9001,
            FStageKey = "import",
            FSortOrder = 1,
            FStageName = "导入暂存",
            FType = "auto",
            F处理粒度 = "batch",
            F插件规则ID = 7001,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ClassifyFilesAsync_MatchingColumns_Routed()
    {
        const long orgId = 100;
        var (provider, _) = BuildProvider(nameof(ClassifyFilesAsync_MatchingColumns_Routed));
        await SeedFlowAsync(provider, orgId);
        var svc = CreateTriggerService(provider);

        var files = new List<FileColumnHeader>
        {
            new("未到件明细.xls", new[] { "运单号", "问题类型", "网点名称" }),
        };

        var result = await svc.ClassifyFilesAsync(files, orgId);

        Assert.Single(result.Routed);
        Assert.Empty(result.Unmatched);
        Assert.Empty(result.Ambiguous);
        Assert.Equal("未到件明细.xls", result.Routed[0].FileName);
        Assert.Equal(8001, result.Routed[0].FlowDefinitionId);
        Assert.Equal(7001, result.Routed[0].PluginRuleId);
    }

    [Fact]
    public async Task ClassifyFilesAsync_UnrelatedColumns_Unmatched()
    {
        const long orgId = 100;
        var (provider, _) = BuildProvider(nameof(ClassifyFilesAsync_UnrelatedColumns_Unmatched));
        await SeedFlowAsync(provider, orgId);
        var svc = CreateTriggerService(provider);

        var files = new List<FileColumnHeader>
        {
            new("无关表.xls", new[] { "甲", "乙", "丙" }),
        };

        var result = await svc.ClassifyFilesAsync(files, orgId);

        Assert.Empty(result.Routed);
        Assert.Single(result.Unmatched);
        Assert.Empty(result.Ambiguous);
        Assert.Equal("无关表.xls", result.Unmatched[0].FileName);
        Assert.Contains("甲", result.Unmatched[0].Columns);
    }

    [Fact]
    public async Task ClassifyFilesAsync_MixedFiles_SplitsByHitCount()
    {
        const long orgId = 100;
        var (provider, _) = BuildProvider(nameof(ClassifyFilesAsync_MixedFiles_SplitsByHitCount));
        await SeedFlowAsync(provider, orgId);
        var svc = CreateTriggerService(provider);

        var files = new List<FileColumnHeader>
        {
            new("命中.xls", new[] { "运单号", "问题类型" }),
            new("未命中.xls", new[] { "无关列" }),
        };

        var result = await svc.ClassifyFilesAsync(files, orgId);

        Assert.Single(result.Routed);
        Assert.Single(result.Unmatched);
        Assert.Empty(result.Ambiguous);
        Assert.Equal("命中.xls", result.Routed[0].FileName);
        Assert.Equal("未命中.xls", result.Unmatched[0].FileName);
    }
}
