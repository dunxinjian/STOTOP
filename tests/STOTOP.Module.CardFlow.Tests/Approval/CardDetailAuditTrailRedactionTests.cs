using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CardDetailAuditTrailRedactionTests
{
    private const string SensitiveValue = "6222021234567890123";

    [Fact]
    public async global::System.Threading.Tasks.Task RouteDecisionAudit_DropsSnapshotPayloads_NoRawValueLeak()
    {
        using var db = TestDbContextFactory.Create(nameof(RouteDecisionAudit_DropsSnapshotPayloads_NoRawValueLeak));

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 200,
            FFlowDefinitionId = 1,
            FFlowVersionId = 1,
            FTitle = "报销单",
            FStatus = "completed",
            FInitiatorId = 9,
            FInitiatorName = "发起人",
            FDataJson = "{}",
            FCurrentRound = 1,
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });

        // 决策快照与候选结果都内嵌了原始敏感值：
        // - decisionSnapshot.fields 把 schema 声明敏感、但字段名不命中写入期启发式（store policy）的值原文留存
        // - candidateResults[].explanation 自由文本内嵌值，无法可靠打码
        db.Set<CfRouteDecisionSnapshot>().Add(new CfRouteDecisionSnapshot
        {
            FID = 210,
            FCardId = 200,
            FSourceStageInstanceId = 0,
            FFromStageKey = "finance_review",
            FSelectedEdgeKey = "manager_to_gm",
            FToStageKey = "gm_approve",
            FCandidateResultsJson =
                """[{"edgeKey":"manager_to_gm","matched":true,"explanation":"账号 6222021234567890123 命中"}]""",
            FDecisionSnapshotJson =
                """{"fields":{"payeeAccountNo":{"present":true,"policy":"store","value":"6222021234567890123"}}}""",
            FReason = "命中条件：流转到总经理审批。",
            FOperatorId = 2,
            FDecisionTime = DateTime.Now.AddMinutes(-4),
            FRound = 1
        });
        await db.SaveChangesAsync();

        var service = new CardService(
            db,
            NullLogger<CardService>.Instance,
            new StageConfigParser(),
            new StageViewProfileResolver(new CardPresentationResolver()),
            new CardFlowSourceContextVerifier(db),
            new CardRedactionService());

        // 发起人查看（通过访问门）
        var result = await service.GetByIdAsync(200, userId: 9);

        Assert.NotNull(result);
        var audit = Assert.Single(result!.AuditTrail);
        Assert.Equal("routeDecision", audit.SnapshotType);

        // 路由结构元数据保留（前端 CardTimeline 消费）
        Assert.Equal("manager_to_gm", audit.EdgeKey);
        Assert.True(audit.Metadata.ContainsKey("edgeKey"));
        Assert.True(audit.Metadata.ContainsKey("fromStageKey"));

        // 值载荷被移除
        Assert.False(audit.Metadata.ContainsKey("decisionSnapshot"));
        Assert.False(audit.Metadata.ContainsKey("candidateResults"));

        // 整条 AuditTrail 序列化后不得出现原始敏感值
        var serialized = JsonSerializer.Serialize(result.AuditTrail);
        Assert.DoesNotContain(SensitiveValue, serialized);
    }
}
