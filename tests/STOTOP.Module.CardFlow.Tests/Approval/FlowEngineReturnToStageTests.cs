using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class FlowEngineReturnToStageTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task RejectAsync_ToPrevious_ReopensPreviousHumanStage()
    {
        using var db = TestDbContextFactory.Create(nameof(RejectAsync_ToPrevious_ReopensPreviousHumanStage));
        var card = new CfCard
        {
            FID = 1000,
            FFlowDefinitionId = 1,
            FFlowVersionId = 200,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 302,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = "{}"
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 200,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 201,
                FFlowVersionId = 200,
                FSortOrder = 1,
                FStageName = "部门审批",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":21,"userName":"部门经理"}]}"""
            },
            new CfStageDefinition
            {
                FID = 202,
                FFlowVersionId = 200,
                FSortOrder = 2,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
            });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance
            {
                FID = 301,
                FCardId = card.FID,
                FStageDefinitionId = 201,
                FStageName = "部门审批",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "completed"
            },
            new CfStageInstance
            {
                FID = 302,
                FCardId = card.FID,
                FStageDefinitionId = 202,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "active"
            });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 401,
            FStageInstanceId = 302,
            FUserId = 31,
            FUserName = "财务",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 501,
            FCardId = card.FID,
            FStageInstanceId = 302,
            FHandlerId = 31,
            FHandlerName = "财务",
            FStatus = "pending",
            FOrgId = 1
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var result = await engine.RejectAsync(card.FID, 31, new RejectRequest
        {
            ReturnMode = "toPrevious",
            Opinion = "请部门补充说明"
        });

        Assert.True(result.Success);
        Assert.Equal("active", result.NewStatus);
        Assert.DoesNotContain("暂未实现", result.Message);

        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == card.FID);
        var currentStage = db.Set<CfStageInstance>().Single(s => s.FID == refreshedCard.FCurrentStageInstanceId);
        Assert.Equal(201, currentStage.FStageDefinitionId);
        Assert.Equal("部门审批", currentStage.FStageName);
        Assert.Equal("active", currentStage.FStatus);

        var returnedStage = db.Set<CfStageInstance>().Single(s => s.FID == 302);
        Assert.Equal("returned", returnedStage.FStatus);
        Assert.Equal("returnToStage", returnedStage.FFinalAction);

        var newAssignee = db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == currentStage.FID);
        Assert.Equal(21, newAssignee.FUserId);
        Assert.Equal("pending", newAssignee.FStatus);

        var actionLog = db.Set<CfActionLog>().Single(l => l.FActionType == "returnToStage");
        Assert.Contains("\"targetStageDefinitionId\":201", actionLog.FDetailJson);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task RejectAsync_ToPrevious_UsesApproverResolverForTargetStage()
    {
        using var db = TestDbContextFactory.Create(nameof(RejectAsync_ToPrevious_UsesApproverResolverForTargetStage));
        db.Set<SysUser>().Add(new SysUser { FID = 41, FName = "字段审批人", FStatus = 1 });
        var card = new CfCard
        {
            FID = 1050,
            FFlowDefinitionId = 1,
            FFlowVersionId = 205,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 307,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = """{"reviewers":[41]}"""
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 205,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 206,
                FFlowVersionId = 205,
                FSortOrder = 1,
                FStageName = "字段取人审批",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fieldUsers",
                FAssigneeConfigJson = """{"fieldKey":"reviewers"}"""
            },
            new CfStageDefinition
            {
                FID = 207,
                FFlowVersionId = 205,
                FSortOrder = 2,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
            });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance
            {
                FID = 306,
                FCardId = card.FID,
                FStageDefinitionId = 206,
                FStageName = "字段取人审批",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "completed"
            },
            new CfStageInstance
            {
                FID = 307,
                FCardId = card.FID,
                FStageDefinitionId = 207,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "active"
            });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 407,
            FStageInstanceId = 307,
            FUserId = 31,
            FUserName = "财务",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var result = await engine.RejectAsync(card.FID, 31, new RejectRequest
        {
            ReturnMode = "toPrevious",
            Opinion = "退回字段审批"
        });

        Assert.True(result.Success);
        var currentStageId = db.Set<CfCard>().Single(c => c.FID == card.FID).FCurrentStageInstanceId!.Value;
        var newAssignee = db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == currentStageId);
        Assert.Equal(41, newAssignee.FUserId);
        Assert.Equal("字段审批人", newAssignee.FUserName);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CountersignAsync_Before_ParksSourceHandlerAndMovesCardToDynamicStage()
    {
        using var db = TestDbContextFactory.Create(nameof(CountersignAsync_Before_ParksSourceHandlerAndMovesCardToDynamicStage));
        var card = new CfCard
        {
            FID = 1100,
            FFlowDefinitionId = 1,
            FFlowVersionId = 210,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 312,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = "{}"
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 210,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 212,
            FFlowVersionId = 210,
            FSortOrder = 1,
            FStageName = "财务复核",
            FType = "human",
            FApprovalMode = "single",
            FAssigneeStrategy = "fixed",
            FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 312,
            FCardId = card.FID,
            FStageDefinitionId = 212,
            FStageName = "财务复核",
            FType = "human",
            FApprovalMode = "single",
            FRound = 1,
            FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 412,
            FStageInstanceId = 312,
            FUserId = 31,
            FUserName = "财务",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 512,
            FCardId = card.FID,
            FStageInstanceId = 312,
            FHandlerId = 31,
            FHandlerName = "财务",
            FStatus = "pending",
            FOrgId = 1
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var result = await engine.CountersignAsync(card.FID, 31, new CountersignRequest
        {
            UserId = 41,
            InsertMode = "before",
            Opinion = "请法务先看"
        });

        Assert.True(result.Success);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == card.FID);
        var dynamicStage = db.Set<CfStageInstance>().Single(s => s.FCardId == card.FID && s.FIsDynamicInsert);
        Assert.Equal(dynamicStage.FID, refreshedCard.FCurrentStageInstanceId);
        Assert.Equal("active", dynamicStage.FStatus);
        Assert.Equal("before", ReadJsonString(dynamicStage.FInsertContextJson, "insertMode"));
        Assert.Equal(412, ReadJsonLong(dynamicStage.FInsertContextJson, "requesterAssigneeId"));

        var sourceStage = db.Set<CfStageInstance>().Single(s => s.FID == 312);
        Assert.Equal("suspended", sourceStage.FStatus);

        var originalAssignee = db.Set<CfStageAssignee>().Single(a => a.FID == 412);
        Assert.Equal("waiting", originalAssignee.FStatus);
        Assert.Null(originalAssignee.FCompletedTime);

        var originalTodo = db.Set<CfTodoItem>().Single(t => t.FID == 512);
        Assert.Equal("cancelled", originalTodo.FStatus);

        var dynamicAssignee = db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == dynamicStage.FID);
        Assert.Equal(41, dynamicAssignee.FUserId);
        Assert.Equal("pending", dynamicAssignee.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ApproveAsync_DynamicBefore_RestoresSourceStageAndRequester()
    {
        using var db = TestDbContextFactory.Create(nameof(ApproveAsync_DynamicBefore_RestoresSourceStageAndRequester));
        var card = new CfCard
        {
            FID = 1200,
            FFlowDefinitionId = 1,
            FFlowVersionId = 220,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 322,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = "{}"
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 220,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 222,
            FFlowVersionId = 220,
            FSortOrder = 1,
            FStageName = "财务复核",
            FType = "human",
            FApprovalMode = "single",
            FAssigneeStrategy = "fixed",
            FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
        });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance
            {
                FID = 321,
                FCardId = card.FID,
                FStageDefinitionId = 222,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "suspended"
            },
            new CfStageInstance
            {
                FID = 322,
                FCardId = card.FID,
                FStageDefinitionId = 222,
                FStageName = "财务复核(加签)",
                FType = "human",
                FApprovalMode = "single",
                FRound = 1,
                FStatus = "active",
                FIsDynamicInsert = true,
                FInsertSourceStageId = 321,
                FInsertContextJson = """{"insertMode":"before","sourceStageInstanceId":321,"requesterAssigneeId":421,"sourceWasComplete":false,"suspendedAssigneeIds":[421]}"""
            });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee
            {
                FID = 421,
                FStageInstanceId = 321,
                FUserId = 31,
                FUserName = "财务",
                FStatus = "waiting",
                FAssignedTime = DateTime.Now
            },
            new CfStageAssignee
            {
                FID = 422,
                FStageInstanceId = 322,
                FUserId = 41,
                FUserName = "法务",
                FStatus = "pending",
                FAssignedTime = DateTime.Now
            });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 522,
            FCardId = card.FID,
            FStageInstanceId = 322,
            FHandlerId = 41,
            FHandlerName = "法务",
            FStatus = "pending",
            FOrgId = 1
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(card.FID, 41, new ApproveRequest
        {
            Opinion = "同意"
        });

        Assert.True(result.Success);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == card.FID);
        Assert.Equal(321, refreshedCard.FCurrentStageInstanceId);
        Assert.Equal("active", refreshedCard.FStatus);

        var sourceStage = db.Set<CfStageInstance>().Single(s => s.FID == 321);
        Assert.Equal("active", sourceStage.FStatus);

        var requester = db.Set<CfStageAssignee>().Single(a => a.FID == 421);
        Assert.Equal("pending", requester.FStatus);

        var dynamicStage = db.Set<CfStageInstance>().Single(s => s.FID == 322);
        Assert.Equal("completed", dynamicStage.FStatus);

        Assert.Contains(db.Set<CfTodoItem>(), t =>
            t.FStageInstanceId == 321 &&
            t.FHandlerId == 31 &&
            t.FStatus == "pending");
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ApproveAsync_DynamicAfter_WhenSourceCompleted_AdvancesToNextStage()
    {
        using var db = TestDbContextFactory.Create(nameof(ApproveAsync_DynamicAfter_WhenSourceCompleted_AdvancesToNextStage));
        var card = new CfCard
        {
            FID = 1300,
            FFlowDefinitionId = 1,
            FFlowVersionId = 230,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 331,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = "{}"
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 230,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 231,
                FFlowVersionId = 230,
                FSortOrder = 1,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
            },
            new CfStageDefinition
            {
                FID = 232,
                FFlowVersionId = 230,
                FSortOrder = 2,
                FStageName = "总经理审批",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"总经理"}]}"""
            });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 331,
            FCardId = card.FID,
            FStageDefinitionId = 231,
            FStageName = "财务复核",
            FType = "human",
            FApprovalMode = "single",
            FRound = 1,
            FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 431,
            FStageInstanceId = 331,
            FUserId = 31,
            FUserName = "财务",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 531,
            FCardId = card.FID,
            FStageInstanceId = 331,
            FHandlerId = 31,
            FHandlerName = "财务",
            FStatus = "pending",
            FOrgId = 1
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var countersign = await engine.CountersignAsync(card.FID, 31, new CountersignRequest
        {
            UserId = 41,
            InsertMode = "after",
            Opinion = "补充法务确认"
        });
        Assert.True(countersign.Success);

        var dynamicStage = db.Set<CfStageInstance>().Single(s => s.FCardId == card.FID && s.FIsDynamicInsert);
        Assert.Equal(dynamicStage.FID, db.Set<CfCard>().Single(c => c.FID == card.FID).FCurrentStageInstanceId);
        Assert.Equal("completed", db.Set<CfStageInstance>().Single(s => s.FID == 331).FStatus);
        Assert.True(ReadJsonBool(dynamicStage.FInsertContextJson, "sourceWasComplete"));

        var approve = await engine.ApproveAsync(card.FID, 41, new ApproveRequest
        {
            Opinion = "同意"
        });

        Assert.True(approve.Success);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == card.FID);
        var nextStage = db.Set<CfStageInstance>().Single(s => s.FID == refreshedCard.FCurrentStageInstanceId);
        Assert.Equal(232, nextStage.FStageDefinitionId);
        Assert.Equal("总经理审批", nextStage.FStageName);
        Assert.Equal("active", nextStage.FStatus);

        var nextAssignee = db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == nextStage.FID);
        Assert.Equal(51, nextAssignee.FUserId);
        Assert.Equal("pending", nextAssignee.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ApproveAsync_DynamicAfter_WhenSourceIncomplete_RestoresRemainingHandlers()
    {
        using var db = TestDbContextFactory.Create(nameof(ApproveAsync_DynamicAfter_WhenSourceIncomplete_RestoresRemainingHandlers));
        var card = new CfCard
        {
            FID = 1400,
            FFlowDefinitionId = 1,
            FFlowVersionId = 240,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 341,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = "{}"
        };
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 240,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 241,
            FFlowVersionId = 240,
            FSortOrder = 1,
            FStageName = "联合审批",
            FType = "human",
            FApprovalMode = "countersign",
            FAssigneeStrategy = "fixed",
            FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"},{"userId":32,"userName":"运营"}]}"""
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 341,
            FCardId = card.FID,
            FStageDefinitionId = 241,
            FStageName = "联合审批",
            FType = "human",
            FApprovalMode = "countersign",
            FRound = 1,
            FStatus = "active"
        });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee
            {
                FID = 441,
                FStageInstanceId = 341,
                FUserId = 31,
                FUserName = "财务",
                FStatus = "pending",
                FAssignedTime = DateTime.Now
            },
            new CfStageAssignee
            {
                FID = 442,
                FStageInstanceId = 341,
                FUserId = 32,
                FUserName = "运营",
                FStatus = "pending",
                FAssignedTime = DateTime.Now
            });
        db.Set<CfTodoItem>().AddRange(
            new CfTodoItem
            {
                FID = 541,
                FCardId = card.FID,
                FStageInstanceId = 341,
                FHandlerId = 31,
                FHandlerName = "财务",
                FStatus = "pending",
                FOrgId = 1
            },
            new CfTodoItem
            {
                FID = 542,
                FCardId = card.FID,
                FStageInstanceId = 341,
                FHandlerId = 32,
                FHandlerName = "运营",
                FStatus = "pending",
                FOrgId = 1
            });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var countersign = await engine.CountersignAsync(card.FID, 31, new CountersignRequest
        {
            UserId = 41,
            InsertMode = "after",
            Opinion = "请法务确认"
        });

        Assert.True(countersign.Success);
        Assert.Equal("approved", db.Set<CfStageAssignee>().Single(a => a.FID == 441).FStatus);
        Assert.Equal("waiting", db.Set<CfStageAssignee>().Single(a => a.FID == 442).FStatus);
        Assert.Equal("suspended", db.Set<CfStageInstance>().Single(s => s.FID == 341).FStatus);

        var dynamicStage = db.Set<CfStageInstance>().Single(s => s.FCardId == card.FID && s.FIsDynamicInsert);
        Assert.False(ReadJsonBool(dynamicStage.FInsertContextJson, "sourceWasComplete"));

        var approve = await engine.ApproveAsync(card.FID, 41, new ApproveRequest
        {
            Opinion = "同意"
        });

        Assert.True(approve.Success);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == card.FID);
        Assert.Equal(341, refreshedCard.FCurrentStageInstanceId);
        Assert.Equal("active", db.Set<CfStageInstance>().Single(s => s.FID == 341).FStatus);
        Assert.Equal("pending", db.Set<CfStageAssignee>().Single(a => a.FID == 442).FStatus);
        Assert.Contains(db.Set<CfTodoItem>(), t =>
            t.FStageInstanceId == 341 &&
            t.FHandlerId == 32 &&
            t.FStatus == "pending");
    }

    [Fact]
    public async global::System.Threading.Tasks.Task DynamicPolicy_AfterSourceBeforeRoute_InsertsBeforeRouteAndContinuesFromOriginalSource()
    {
        using var db = TestDbContextFactory.Create(nameof(DynamicPolicy_AfterSourceBeforeRoute_InsertsBeforeRouteAndContinuesFromOriginalSource));
        SeedDynamicPolicyFlow(db, policyTiming: "afterSourceBeforeRoute", sourceStageKey: "manager");
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var approveSource = await engine.ApproveAsync(1000, 21, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveSource.Success, approveSource.Message);
        var inserted = db.Set<CfStageInstance>().Single(s => s.FCardId == 1000 && s.FIsDynamicInsert);
        Assert.Equal("大额追加总经理", inserted.FStageName);
        Assert.Equal("afterSourceBeforeRoute", ReadJsonString(inserted.FInsertContextJson, "triggerTiming"));
        Assert.Equal(7, db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == inserted.FID).FUserId);
        Assert.Empty(db.Set<CfRouteDecisionSnapshot>().Where(s => s.FCardId == 1000));

        var approveDynamic = await engine.ApproveAsync(1000, 7, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveDynamic.Success, approveDynamic.Message);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == 1000);
        var current = db.Set<CfStageInstance>().Single(s => s.FID == refreshedCard.FCurrentStageInstanceId);
        Assert.False(current.FIsDynamicInsert);
        Assert.Equal("财务复核", current.FStageName);
        Assert.Single(db.Set<CfStageInstance>().Where(s => s.FCardId == 1000 && s.FIsDynamicInsert));
        Assert.Single(db.Set<CfRouteDecisionSnapshot>().Where(s => s.FCardId == 1000 && s.FSelectedEdgeKey == "manager_to_finance"));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task DynamicPolicy_ReplaceTargetHandlers_UsesPolicyApproversWithoutDynamicStage()
    {
        using var db = TestDbContextFactory.Create(nameof(DynamicPolicy_ReplaceTargetHandlers_UsesPolicyApproversWithoutDynamicStage));
        SeedDynamicPolicyFlow(db, policyTiming: "replaceTargetHandlers", sourceStageKey: "manager");
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var approveSource = await engine.ApproveAsync(1000, 21, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveSource.Success, approveSource.Message);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == 1000);
        var current = db.Set<CfStageInstance>().Single(s => s.FID == refreshedCard.FCurrentStageInstanceId);
        Assert.False(current.FIsDynamicInsert);
        Assert.Equal("财务复核", current.FStageName);
        Assert.Empty(db.Set<CfStageInstance>().Where(s => s.FCardId == 1000 && s.FIsDynamicInsert));
        var assignees = db.Set<CfStageAssignee>().Where(a => a.FStageInstanceId == current.FID).ToList();
        Assert.Single(assignees);
        Assert.Equal(7, assignees[0].FUserId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task DynamicPolicy_AfterTarget_InsertsAfterSelectedTargetCompletesAndContinuesFromTarget()
    {
        using var db = TestDbContextFactory.Create(nameof(DynamicPolicy_AfterTarget_InsertsAfterSelectedTargetCompletesAndContinuesFromTarget));
        SeedDynamicPolicyFlow(db, policyTiming: "afterTarget", sourceStageKey: "finance");
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var approveManager = await engine.ApproveAsync(1000, 21, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveManager.Success, approveManager.Message);
        var finance = db.Set<CfStageInstance>().Single(s => s.FCardId == 1000 && s.FStageDefinitionId == 202 && s.FStatus == "active");
        Assert.Equal(31, db.Set<CfStageAssignee>().Single(a => a.FStageInstanceId == finance.FID).FUserId);

        var approveFinance = await engine.ApproveAsync(1000, 31, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveFinance.Success, approveFinance.Message);
        var inserted = db.Set<CfStageInstance>().Single(s => s.FCardId == 1000 && s.FIsDynamicInsert);
        Assert.Equal("大额追加总经理", inserted.FStageName);
        Assert.Equal("afterTarget", ReadJsonString(inserted.FInsertContextJson, "triggerTiming"));

        var approveDynamic = await engine.ApproveAsync(1000, 7, new ApproveRequest { Opinion = "同意" });

        Assert.True(approveDynamic.Success, approveDynamic.Message);
        var refreshedCard = db.Set<CfCard>().Single(c => c.FID == 1000);
        var payment = db.Set<CfStageInstance>().Single(s => s.FID == refreshedCard.FCurrentStageInstanceId);
        Assert.Equal("付款确认", payment.FStageName);
        Assert.False(payment.FIsDynamicInsert);
    }

    private static void SeedDynamicPolicyFlow(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        string policyTiming,
        string sourceStageKey)
    {
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 7, FName = "总经理", FStatus = 1 },
            new SysUser { FID = 21, FName = "部门主管", FStatus = 1 },
            new SysUser { FID = 31, FName = "财务", FStatus = 1 },
            new SysUser { FID = 41, FName = "出纳", FStatus = 1 });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 200,
            FFlowDefinitionId = 1,
            FStatus = "published",
            FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 201,
                FFlowVersionId = 200,
                FStageKey = "manager",
                FSortOrder = 1,
                FStageName = "部门审批",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":21,"userName":"部门主管"}]}"""
            },
            new CfStageDefinition
            {
                FID = 202,
                FFlowVersionId = 200,
                FStageKey = "finance",
                FSortOrder = 2,
                FStageName = "财务复核",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
            },
            new CfStageDefinition
            {
                FID = 203,
                FFlowVersionId = 200,
                FStageKey = "payment",
                FSortOrder = 3,
                FStageName = "付款确认",
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = "fixed",
                FAssigneeConfigJson = """{"users":[{"userId":41,"userName":"出纳"}]}"""
            });
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 200,
                FEdgeKey = "manager_to_finance",
                FFromStageDefinitionId = 201,
                FFromStageKey = "manager",
                FToStageDefinitionId = 202,
                FToStageKey = "finance",
                FRouteName = "主管后财务",
                FPriority = 1,
                FIsDefault = true,
                FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 200,
                FEdgeKey = "finance_to_payment",
                FFromStageDefinitionId = 202,
                FFromStageKey = "finance",
                FToStageDefinitionId = 203,
                FToStageKey = "payment",
                FRouteName = "财务后付款",
                FPriority = 1,
                FIsDefault = true,
                FStatus = "active"
            });
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FFlowVersionId = 200,
            FPolicyKey = $"gm_{policyTiming}",
            FSourceStageDefinitionId = sourceStageKey == "finance" ? 202 : 201,
            FSourceStageKey = sourceStageKey,
            FPolicyName = "大额追加总经理",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
            FTriggerTiming = policyTiming,
            FInsertPosition = "beforeTarget",
            FPriority = 1,
            FMaxInsertCount = 20,
            FStatus = "active"
        });
        var card = new CfCard
        {
            FID = 1000,
            FFlowDefinitionId = 1,
            FFlowVersionId = 200,
            FTitle = "费用报销",
            FStatus = "active",
            FInitiatorId = 10,
            FInitiatorName = "发起人",
            FCurrentStageInstanceId = 301,
            FCurrentRound = 1,
            FOrgId = 1,
            FDataJson = """{"amount":6800}"""
        };
        db.Set<CfCard>().Add(card);
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 301,
            FCardId = card.FID,
            FStageDefinitionId = 201,
            FStageName = "部门审批",
            FType = "human",
            FApprovalMode = "single",
            FRound = 1,
            FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 401,
            FStageInstanceId = 301,
            FUserId = 21,
            FUserName = "部门主管",
            FStatus = "pending",
            FAssignedTime = DateTime.Now
        });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 501,
            FCardId = card.FID,
            FStageInstanceId = 301,
            FHandlerId = 21,
            FHandlerName = "部门主管",
            FStatus = "pending",
            FOrgId = 1
        });
    }

    private static string? ReadJsonString(string? json, string propertyName)
    {
        using var document = global::System.Text.Json.JsonDocument.Parse(json ?? "{}");
        return document.RootElement.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    private static long? ReadJsonLong(string? json, string propertyName)
    {
        using var document = global::System.Text.Json.JsonDocument.Parse(json ?? "{}");
        return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == global::System.Text.Json.JsonValueKind.Number
            ? property.GetInt64()
            : null;
    }

    private static bool ReadJsonBool(string? json, string propertyName)
    {
        using var document = global::System.Text.Json.JsonDocument.Parse(json ?? "{}");
        return document.RootElement.TryGetProperty(propertyName, out var property)
            && property.ValueKind == global::System.Text.Json.JsonValueKind.True;
    }

    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var orchestration = new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance);

        return new FlowEngineService(
            db,
            new FakeNumberSequenceService(),
            new FakeCardSchemaService(),
            new ApprovalModeHandler(),
            new SequentialApprovalRuntime(),
            new ReturnToStageRuntime(),
            new StageConfigParser(),
            new StageFieldAccessService(),
            new StageActionPolicyService(),
            new ConditionEvaluator(),
            new ApproverResolver(db),
            new FakeBudgetOccupationService(),
            new DbTodoService(db),
            new FakeNotificationDispatcher(),
            new AutoPluginFactory(provider),
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            orchestration,
            new FakeBatchNotifier(),
            new FakeBatchLifecycleService(),
            NullLogger<FlowEngineService>.Instance);
    }

}
