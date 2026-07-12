using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionCloneSelfHealTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task 克隆_源节点空键时自愈生成确定性键而非抛错()
    {
        using var db = TestDbContextFactory.Create(nameof(克隆_源节点空键时自愈生成确定性键而非抛错));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "极兔导入", FFlowCode = "JT",
            FStatus = "published", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 201, FFlowDefinitionId = 100, FVersionNumber = 1,
            FStatus = "published", FIsCurrentVersion = true,
            FPublishTime = DateTime.Now, FCreatedTime = DateTime.Now
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 301, FFlowVersionId = 201, FStageKey = "",   // seeder 直插的空键节点(极兔5140同款)
            FStageName = "Excel导入解析", FSortOrder = 1, FType = "auto"
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            // 注意: 夹具键须已归一化——EnsureStageKey 恒先过 NormalizeKeyToken(非字母数字→下划线),
            // 若用真实节点 5141 的 "jt-auto-voucher" 会被归一成 "jt_auto_voucher" 导致断言失配
            FID = 302, FFlowVersionId = 201, FStageKey = "jt_auto_voucher",
            FStageName = "自动凭证", FSortOrder = 2, FType = "auto"
        });
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var draft = await service.CreateDraftFromVersionAsync(100, 201, operatorId: 1);

        Assert.Equal(2, draft.Stages.Count);
        Assert.Equal("stage_201_1_301", draft.Stages[0].StageKey); // 自愈键: stage_{源版本}_{排序}_{源FID}
        Assert.Equal("jt_auto_voucher", draft.Stages[1].StageKey); // 非空键归一化后保留
    }
}
