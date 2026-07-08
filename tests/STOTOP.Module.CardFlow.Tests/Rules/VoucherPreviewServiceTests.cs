using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

/// <summary>
/// M5-3 凭证试算：诚实降级。凭证引擎 AutoVoucherHandler 强依赖 STG 暂存表 + 账套科目上下文，
/// 单张卡片 dataJson 无法无失真喂入，故端点做静态完整性预判 + 降级说明（不伪造分录）。
/// </summary>
public class VoucherPreviewServiceTests
{
    private static void SeedFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db, CfStageDefinition stage, CfPluginRule? rule = null)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 800, FFlowName = "凭证流程", FFlowCode = "VCH", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 801, FFlowDefinitionId = 800, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().Add(stage);
        if (rule != null)
            db.Set<CfPluginRule>().Add(rule);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewVoucher_规则完整_降级返回success假且结构完整()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewVoucher_规则完整_降级返回success假且结构完整));
        SeedFlow(db,
            new CfStageDefinition { FID = 90, FFlowVersionId = 801, FStageKey = "voucher", FStageName = "自动凭证", FType = "auto", FSortOrder = 1, F插件规则ID = 5000 },
            new CfPluginRule
            {
                FID = 5000, FOrgId = 1, F类型编码 = "AutoVoucher", F规则名称 = "费用凭证", F状态 = 1,
                F规则配置JSON = """{"ruleConfig":{"ruleGroups":[{"id":"g1","name":"差旅","lines":[{"lineNo":1,"direction":"借","accountId":1001,"amountField":"amount"}]}]}}"""
            });
        await db.SaveChangesAsync();

        var service = new VoucherPreviewService(db);
        var result = await service.PreviewVoucherAsync(800, new VoucherPreviewRequest
        {
            StageKey = "voucher",
            CardDataJson = """{"amount":800}"""
        });

        // 诚实降级：结构完整、success=false、message 非空、entries 空
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Empty(result.Entries);
        Assert.Contains("运行时", result.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewVoucher_规则组为空_静态预判不生成凭证()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewVoucher_规则组为空_静态预判不生成凭证));
        SeedFlow(db,
            new CfStageDefinition { FID = 91, FFlowVersionId = 801, FStageKey = "voucher", FStageName = "自动凭证", FType = "auto", FSortOrder = 1, F插件规则ID = 5001 },
            new CfPluginRule
            {
                FID = 5001, FOrgId = 1, F类型编码 = "AutoVoucher", F规则名称 = "空规则", F状态 = 1,
                F规则配置JSON = """{"ruleConfig":{"ruleGroups":[]}}"""
            });
        await db.SaveChangesAsync();

        var service = new VoucherPreviewService(db);
        var result = await service.PreviewVoucherAsync(800, new VoucherPreviewRequest { StageKey = "voucher", CardDataJson = "{}" });

        Assert.False(result.Success);
        Assert.Contains("规则组", result.Message);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewVoucher_非自动节点_返回失败说明()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewVoucher_非自动节点_返回失败说明));
        SeedFlow(db,
            new CfStageDefinition { FID = 92, FFlowVersionId = 801, FStageKey = "approve", FStageName = "人工审批", FType = "human", FSortOrder = 1 });
        await db.SaveChangesAsync();

        var service = new VoucherPreviewService(db);
        var result = await service.PreviewVoucherAsync(800, new VoucherPreviewRequest { StageKey = "approve", CardDataJson = "{}" });

        Assert.False(result.Success);
        Assert.Contains("不是自动节点", result.Message);
    }
}
