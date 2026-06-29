using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Presentation;

public class CardPresentationResolverTests
{
    [Fact]
    public void Resolve_BuildsRuntimeComponentsWithStageAccessAndDetailSummary()
    {
        var resolver = new CardPresentationResolver();
        var profile = new StageViewProfile
        {
            ComponentAccess =
            {
                ["amount-card"] = new StageComponentAccessRule { Access = "masked" },
                ["secret-card"] = new StageComponentAccessRule { Access = "hidden" },
                ["expense-lines"] = new StageComponentAccessRule { Access = "editable", Required = true }
            }
        };
        var card = new CfCard
        {
            FID = 10,
            FDataJson = """{"amount":123456,"secretNote":"raw-secret","title":"报销单"}"""
        };
        var details = new List<CfCardDetail>
        {
            new()
            {
                FID = 20,
                FDetailTableKey = "expense",
                FSortOrder = 1,
                FDataJson = """{"amount":100,"accountCode":"6602"}"""
            },
            new()
            {
                FID = 21,
                FDetailTableKey = "expense",
                FSortOrder = 2,
                FDataJson = """{"amount":200,"accountCode":"6603"}"""
            }
        };

        var result = resolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = """
            {
              "version": 2,
              "fields": [
                { "key": "amount", "label": "报销金额", "type": "money" },
                { "key": "secretNote", "label": "敏感说明", "type": "text" },
                { "key": "title", "label": "标题", "type": "text" }
              ],
              "components": [
                {
                  "id": "amount-card",
                  "type": "amountSummary",
                  "title": "金额摘要",
                  "binding": { "source": "cardField", "fieldKey": "amount" },
                  "statisticKey": "expenseAmount"
                },
                {
                  "id": "secret-card",
                  "type": "text",
                  "title": "敏感说明",
                  "binding": { "source": "cardField", "fieldKey": "secretNote" }
                },
                {
                  "id": "expense-lines",
                  "type": "detailTable",
                  "title": "费用明细",
                  "binding": { "source": "detailTable", "detailTableKey": "expense" },
                  "validation": { "minRows": 1 },
                  "aggregation": {
                    "sum": [
                      { "fieldKey": "amount", "targetKey": "detailSum.amount" }
                    ]
                  }
                }
              ]
            }
            """,
            DetailSchemaJson = """
            {
              "version": 2,
              "tables": [
                {
                  "detailTableKey": "expense",
                  "label": "费用明细",
                  "columns": [
                    { "key": "amount", "label": "金额", "type": "money" },
                    { "key": "accountCode", "label": "科目", "type": "account" }
                  ]
                }
              ]
            }
            """,
            StageProfile = profile,
            FieldAccess = new Dictionary<string, StageFieldAccessRule>
            {
                ["amount"] = new() { Access = "masked" },
                ["secretNote"] = new() { Access = "hidden" },
                ["title"] = new() { Access = "readonly" }
            },
            DetailAccess = new Dictionary<string, StageDetailAccessRule>
            {
                ["expense.amount"] = new() { Access = "editable" },
                ["expense.accountCode"] = new() { Access = "readonly" }
            },
            Card = card,
            Details = details
        });

        Assert.DoesNotContain(result.Components, component => component.Id == "secret-card");
        var amount = Assert.Single(result.Components, component => component.Id == "amount-card");
        Assert.Equal("masked", amount.Access);
        Assert.False(amount.Editable);
        Assert.Equal("****", amount.Value?.ToString()); // 6位值在 FieldMasker 收紧阈值后整体打码

        var detailTable = Assert.Single(result.Components, component => component.Id == "expense-lines");
        Assert.True(detailTable.Editable);
        Assert.True(detailTable.Required);
        Assert.Equal(2, detailTable.Rows.Count);
        Assert.Equal("readonly", detailTable.Columns.Single(column => column.Key == "accountCode").Access);
        Assert.Equal(300m, result.DetailSummary["detailSum.amount"]);
    }

    [Fact]
    public void Resolve_SynthesizesFieldAndDetailComponentsForLegacyArraySchemas()
    {
        var resolver = new CardPresentationResolver();
        var result = resolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = """[{"key":"amount","label":"金额","type":"money"},{"key":"title","label":"标题","type":"text"}]""",
            DetailSchemaJson = """[{"key":"amount","label":"金额","type":"money"}]""",
            StageProfile = new StageViewProfile(),
            FieldAccess = new Dictionary<string, StageFieldAccessRule>
            {
                ["amount"] = new() { Access = "readonly" },
                ["title"] = new() { Access = "readonly" }
            },
            DetailAccess = new Dictionary<string, StageDetailAccessRule>
            {
                ["default.amount"] = new() { Access = "readonly" }
            },
            Card = new CfCard { FID = 11, FDataJson = """{"amount":88,"title":"普通卡片"}""" },
            Details = new List<CfCardDetail>
            {
                new CfCardDetail
                {
                    FID = 30,
                    FDetailTableKey = "default",
                    FSortOrder = 1,
                    FDataJson = """{"amount":88}"""
                }
            }
        });

        Assert.Contains(result.Components, component => component.Id == "field_amount");
        Assert.Contains(result.Components, component => component.Id == "field_title");
        Assert.Contains(result.Components, component => component.Id == "detail_default");
    }

    [Fact]
    public void Resolve_ExposesSnapshotExplanationComponents()
    {
        var resolver = new CardPresentationResolver();
        var result = resolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = """
            {
              "version": 2,
              "components": [
                {
                  "id": "route-explain",
                  "type": "routeDecision",
                  "title": "流转说明",
                  "binding": { "source": "snapshot", "snapshotType": "routeDecision" }
                }
              ]
            }
            """,
            StageProfile = new StageViewProfile(),
            Card = new CfCard { FID = 12, FDataJson = "{}" },
            Snapshots =
            {
                new CardPresentationSnapshot
                {
                    SnapshotType = "routeDecision",
                    Title = "大额报销",
                    Reason = "当报销金额大于等于 5000 时，流转到总经理审批。",
                    Metadata = { ["edgeKey"] = "manager_to_gm" }
                }
            }
        });

        var routeDecision = Assert.Single(result.Components, component => component.Id == "route-explain");
        Assert.Single(routeDecision.Snapshots);
        Assert.Equal("manager_to_gm", routeDecision.Snapshots[0].Metadata["edgeKey"]);
        Assert.Contains("总经理审批", routeDecision.Snapshots[0].Reason);
    }

    [Fact]
    public void MaskedDetailColumn_ExcludedFromDefaultSummary()
    {
        // 构造一行明细，default.amount 列被标记 masked；断言结果 DetailSummary 不含 detailSum.amount
        var resolver = new CardPresentationResolver();
        var result = resolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = """
            {
              "version": 2,
              "components": [
                {
                  "id": "lines",
                  "type": "detailTable",
                  "title": "明细",
                  "binding": { "source": "detailTable", "detailTableKey": "default" }
                }
              ]
            }
            """,
            DetailSchemaJson = """[{"key":"amount","label":"金额","type":"money"}]""",
            StageProfile = new StageViewProfile(),
            FieldAccess = new Dictionary<string, StageFieldAccessRule>(),
            DetailAccess = new Dictionary<string, StageDetailAccessRule>
            {
                ["default.amount"] = new() { Access = "masked" }
            },
            Card = new CfCard { FID = 50, FDataJson = "{}" },
            Details = new List<CfCardDetail>
            {
                new CfCardDetail
                {
                    FID = 60,
                    FDetailTableKey = "default",
                    FSortOrder = 1,
                    FDataJson = """{"amount":999}"""
                }
            }
        });

        Assert.False(result.DetailSummary.ContainsKey("detailSum.amount"));
    }

    [Fact]
    public void Resolve_BindsRelationComponentsByRelationType()
    {
        var resolver = new CardPresentationResolver();
        var result = resolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = """
            {
              "version": 2,
              "components": [
                {
                  "id": "loan-relations",
                  "type": "relationCards",
                  "title": "冲抵借款",
                  "binding": { "source": "relation", "relationType": "loanOffset" }
                }
              ]
            }
            """,
            StageProfile = new StageViewProfile(),
            Card = new CfCard { FID = 12, FDataJson = "{}" },
            Relations = new List<CardPresentationRelation>
            {
                new CardPresentationRelation
                {
                    Id = 1,
                    SourceCardId = 12,
                    TargetCardId = 100,
                    TargetCardNumber = "JK-001",
                    TargetCardTitle = "备用金借款",
                    RelationType = "loanOffset",
                    OffsetAmount = 300m
                },
                new CardPresentationRelation
                {
                    Id = 2,
                    SourceCardId = 12,
                    TargetCardId = 101,
                    TargetCardNumber = "FY-001",
                    RelationType = "sourceRequest"
                }
            }
        });

        var relationComponent = Assert.Single(result.Components, component => component.Id == "loan-relations");
        var relations = Assert.IsType<List<CardPresentationRelation>>(relationComponent.Value);
        var relation = Assert.Single(relations);
        Assert.Equal("JK-001", relation.TargetCardNumber);
        Assert.Equal(300m, relation.OffsetAmount);
    }
}
