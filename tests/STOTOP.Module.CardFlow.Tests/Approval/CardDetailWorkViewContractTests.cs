using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CardDetailWorkViewContractTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ActiveCard_ReturnsCurrentStageWorkViewAndRedactedData()
    {
        using var db = TestDbContextFactory.Create(nameof(ActiveCard_ReturnsCurrentStageWorkViewAndRedactedData));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 10,
            FFlowName = "费用报销",
            FFlowCode = "EXP",
            FStatus = "published",
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 20,
            FFlowDefinitionId = 10,
            FVersionNumber = 1,
            FStatus = "published",
            FIsCurrentVersion = true,
            FCardSchemaJson = """
            {
              "version": 2,
              "fields": [
                { "key": "amount", "label": "金额", "type": "money" },
                { "key": "secretNote", "label": "敏感说明", "type": "text" },
                { "key": "title", "label": "标题", "type": "text" }
              ],
              "components": [
                {
                  "id": "amount-card",
                  "type": "amountSummary",
                  "title": "金额摘要",
                  "binding": { "source": "cardField", "fieldKey": "amount" }
                },
                {
                  "id": "expense-lines",
                  "type": "detailTable",
                  "title": "费用明细",
                  "binding": { "source": "detailTable", "detailTableKey": "expense" },
                  "aggregation": {
                    "sum": [
                      { "fieldKey": "amount", "targetKey": "detailSum.amount" }
                    ]
                  }
                },
                {
                  "id": "route-explain",
                  "type": "routeDecision",
                  "title": "流转说明",
                  "binding": { "source": "snapshot", "snapshotType": "routeDecision" }
                },
                {
                  "id": "dynamic-explain",
                  "type": "dynamicApprover",
                  "title": "动态审批说明",
                  "binding": { "source": "snapshot", "snapshotType": "dynamicApprover" }
                },
                {
                  "id": "loan-relations",
                  "type": "relationCards",
                  "title": "冲抵借款",
                  "binding": { "source": "relation", "relationType": "loanOffset" }
                }
              ]
            }
            """,
            FDetailSchemaJson = """
            {
              "version": 2,
              "tables": [
                {
                  "detailTableKey": "expense",
                  "columns": [
                    { "key": "amount", "label": "金额", "type": "money" },
                    { "key": "accountCode", "label": "科目", "type": "account" },
                    { "key": "internalAuditNote", "label": "内部审计备注", "type": "text" }
                  ]
                }
              ]
            }
            """,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 30,
            FFlowVersionId = 20,
            FStageName = "财务审核",
            FType = "human",
            FInputFieldsJson = """
            {
              "version": 2,
              "inputFields": [],
              "viewProfile": {
                "fieldAccess": {
                  "amount": { "access": "masked" },
                  "secretNote": { "access": "hidden" },
                  "title": { "access": "readonly" }
                },
                "detailAccess": {
                  "expense.amount": { "access": "readonly" },
                  "expense.accountCode": { "access": "editable" },
                  "expense.internalAuditNote": { "access": "hidden" }
                },
                "componentAccess": {
                  "amount-card": { "access": "masked" },
                  "expense-lines": { "access": "readonly" }
                },
                "actions": ["approve"]
              },
              "actionPolicy": { "allowedActions": ["approve", "reject"] }
            }
            """
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 40,
            FFlowDefinitionId = 10,
            FFlowVersionId = 20,
            FTitle = "报销单",
            FStatus = "active",
            FInitiatorId = 1,
            FInitiatorName = "发起人",
            FDataJson = """{"amount":"123456","secretNote":"raw-secret","title":"报销单"}""",
            FCurrentStageInstanceId = 50,
            FCurrentRound = 1,
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 41,
            FFlowDefinitionId = 10,
            FFlowVersionId = 20,
            FTitle = "备用金借款",
            FCardNumber = "JK-001",
            FStatus = "completed",
            FInitiatorId = 1,
            FInitiatorName = "发起人",
            FDataJson = """{"amount":500}""",
            FCurrentRound = 1,
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 50,
            FCardId = 40,
            FStageDefinitionId = 30,
            FStageName = "财务审核",
            FType = "human",
            FStatus = "active",
            FRound = 1,
            FActivatedTime = DateTime.Now
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 51,
            FCardId = 40,
            FStageDefinitionId = 30,
            FStageName = "总经理加批",
            FType = "human",
            FStatus = "completed",
            FRound = 1,
            FIsDynamicInsert = true,
            FInsertSourceStageId = 50,
            FInsertContextJson = """
            {
              "insertMode": "policy",
              "sourceStageInstanceId": 50,
              "policyKey": "gm_policy",
              "policyName": "大额报销总经理加批",
              "strategyType": "amountMatrix",
              "triggerTiming": "afterRouteBeforeTarget",
              "selectedRouteEdgeKey": "manager_to_gm",
              "reason": "命中动态审批策略：大额报销总经理加批"
            }
            """,
            FActivatedTime = DateTime.Now.AddMinutes(-5),
            FCompletedTime = DateTime.Now.AddMinutes(-3)
        });
        db.Set<CfRouteDecisionSnapshot>().Add(new CfRouteDecisionSnapshot
        {
            FID = 70,
            FCardId = 40,
            FSourceStageInstanceId = 50,
            FFromStageDefinitionId = 30,
            FFromStageKey = "finance_review",
            FSelectedRouteRuleId = 80,
            FSelectedEdgeKey = "manager_to_gm",
            FToStageDefinitionId = 90,
            FToStageKey = "gm_approve",
            FCandidateResultsJson = """[{"edgeKey":"manager_to_gm","matched":true}]""",
            FDecisionSnapshotJson = """{"amount":123456,"orgId":1}""",
            FReason = "报销金额大于等于 5000，流转到总经理审批。",
            FOperatorId = 2,
            FDecisionTime = DateTime.Now.AddMinutes(-4),
            FRound = 1
        });
        db.Set<CfCardRelation>().Add(new CfCardRelation
        {
            FID = 75,
            FSourceCardId = 40,
            FTargetCardId = 41,
            FRelationType = "loanOffset",
            FDescription = "冲抵备用金借款",
            FOffsetAmount = 300m,
            FSnapshotDataJson = """{"remainingAmount":200}""",
            FOrgId = 1,
            FCreatedTime = DateTime.Now.AddMinutes(-2)
        });
        db.Set<CfCardDetail>().Add(new CfCardDetail
        {
            FID = 60,
            FCardId = 40,
            FDetailTableKey = "expense",
            FSortOrder = 1,
            FDataJson = """{"amount":321,"accountCode":"6602","internalAuditNote":"hide-me"}""",
            FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 100,
            FStageInstanceId = 50,
            FUserId = 2,
            FUserName = "财务审核员",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new CardService(
            db,
            NullLogger<CardService>.Instance,
            new StageConfigParser(),
            new StageViewProfileResolver(new CardPresentationResolver()),
            new CardFlowSourceContextVerifier(db),
            new CardRedactionService());

        var result = await service.GetByIdAsync(40, userId: 2);

        Assert.NotNull(result);
        Assert.NotNull(result!.CurrentStageWorkView);
        Assert.Contains("amount", result.CurrentStageWorkView!.FieldAccess.Keys);
        Assert.Equal(new[] { "approve" }, result.CurrentStageWorkView.ActionPolicy.AllowedActions);
        Assert.DoesNotContain("secretNote", result.DataJson);
        // FieldMasker 收紧阈值后 ≤7 字符整体打码，原始值"123456"被替换为"****"，不再出现部分可见片段
        Assert.DoesNotContain("123456", result.DataJson);
        Assert.Contains("****", result.DataJson);
        Assert.Equal("expense", result.Details.Single().DetailTableKey);
        Assert.DoesNotContain("internalAuditNote", result.Details.Single().DataJson);
        Assert.Contains(result.CurrentStageWorkView.Components, component => component.Id == "amount-card");
        var detailComponent = Assert.Single(result.CurrentStageWorkView.Components, component => component.Id == "expense-lines");
        Assert.Equal("readonly", detailComponent.Access);
        Assert.Equal(321m, result.CurrentStageWorkView.DetailSummary["detailSum.amount"]);
        var routeComponent = Assert.Single(result.CurrentStageWorkView.Components, component => component.Id == "route-explain");
        Assert.Single(routeComponent.Snapshots);
        Assert.Equal("manager_to_gm", routeComponent.Snapshots[0].Metadata["edgeKey"]);
        Assert.Contains("总经理审批", routeComponent.Snapshots[0].Reason);
        var dynamicComponent = Assert.Single(result.CurrentStageWorkView.Components, component => component.Id == "dynamic-explain");
        Assert.Single(dynamicComponent.Snapshots);
        Assert.Equal("gm_policy", dynamicComponent.Snapshots[0].Metadata["policyKey"]);
        Assert.Contains("大额报销总经理加批", dynamicComponent.Snapshots[0].Reason);
        var relationComponent = Assert.Single(result.CurrentStageWorkView.Components, component => component.Id == "loan-relations");
        var relations = Assert.IsType<List<CardPresentationRelation>>(relationComponent.Value);
        var relation = Assert.Single(relations);
        Assert.Equal("JK-001", relation.TargetCardNumber);
        Assert.Equal(300m, relation.OffsetAmount);
        Assert.Equal(200L, relation.SnapshotData["remainingAmount"]);
        Assert.Equal(30, result.StageInstances.Single(stage => stage.Id == 50).StageDefinitionId);
    }
}
