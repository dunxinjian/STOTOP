using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionPublishGateTests
{
    private static STOTOP.Infrastructure.Data.STOTOPDbContext Seed(string name, params CfStageDefinition[] stages)
    {
        var db = TestDbContextFactory.Create(name);
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "测试流程", FFlowCode = "T1",
            FStatus = "published", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 201, FFlowDefinitionId = 100, FVersionNumber = 1,
            FStatus = "draft", FCreatedTime = DateTime.Now
        });
        foreach (var s in stages) db.Set<CfStageDefinition>().Add(s);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 发布_零节点草稿被拒()
    {
        using var db = Seed(nameof(发布_零节点草稿被拒));
        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(100, 1));
        Assert.Contains("没有任何节点", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 发布_空节点键被拒()
    {
        using var db = Seed(nameof(发布_空节点键被拒), new CfStageDefinition
        {
            FID = 301, FFlowVersionId = 201, FStageKey = "",
            FStageName = "导入", FSortOrder = 1, FType = "human"
        });
        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(100, 1));
        Assert.Contains("StageKey", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 发布_凭证节点缺规则被拒()
    {
        using var db = Seed(nameof(发布_凭证节点缺规则被拒), new CfStageDefinition
        {
            FID = 301, FFlowVersionId = 201, FStageKey = "s1",
            FStageName = "自动凭证", FSortOrder = 1, FType = "auto",
            F插件注册ID = 5 // AutoVoucher,但 F插件规则ID 为空
        });
        db.Set<CfAutoPluginRegistry>().Add(new CfAutoPluginRegistry
        {
            FID = 5, F插件编码 = "AutoVoucher", F插件名称 = "自动凭证",
            F插件类型 = "processing", F处理粒度 = "batch", F状态 = 1
        });
        db.SaveChanges();
        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(100, 1));
        Assert.Contains("未配置凭证规则", ex.Message);
    }
}
