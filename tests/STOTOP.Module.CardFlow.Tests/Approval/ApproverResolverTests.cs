using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ApproverResolverTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task FixedUsers_AcceptsSpecifiedAlias()
    {
        using var db = TestDbContextFactory.Create(nameof(FixedUsers_AcceptsSpecifiedAlias));
        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "specified",
            FAssigneeConfigJson = """{"users":[{"userId":7,"userName":"Alice"}]}"""
        };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 1, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Single(result.Approvers);
        Assert.Equal(7, result.Approvers[0].UserId);
        Assert.Equal("Alice", result.Approvers[0].UserName);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task RoleStrategy_UsesActiveRoleAndActiveUsersOnly()
    {
        using var db = TestDbContextFactory.Create(nameof(RoleStrategy_UsesActiveRoleAndActiveUsersOnly));
        db.Set<SysRole>().Add(new SysRole { FID = 10, FCode = "FIN", FName = "财务", FStatus = 1 });
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "Active", FStatus = 1 },
            new SysUser { FID = 2, FName = "Inactive", FStatus = 0 });
        db.Set<SysUserRole>().AddRange(
            new SysUserRole { FUserId = 1, FRoleId = 10, FOrgId = 100 },
            new SysUserRole { FUserId = 2, FRoleId = 10, FOrgId = 100 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "role",
            FAssigneeConfigJson = """{"roleCode":"FIN","orgScoped":true}"""
        };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Single(result.Approvers);
        Assert.Equal(1, result.Approvers[0].UserId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task FieldUsers_NormalizesUserFieldShapesInOrder()
    {
        using var db = TestDbContextFactory.Create(nameof(FieldUsers_NormalizesUserFieldShapesInOrder));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 4, FName = "U4", FStatus = 1 },
            new SysUser { FID = 5, FName = "U5", FStatus = 1 },
            new SysUser { FID = 6, FName = "U6", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "fieldUsers",
            FAssigneeConfigJson = """{"fieldKey":"reviewers"}"""
        };
        var cardData = new Dictionary<string, object?>
        {
            ["reviewers"] = new object?[] { new Dictionary<string, object?> { ["id"] = 4 }, "5", 6L }
        };

        var result = await resolver.ResolveAsync(stage, new CfCard(), cardData, flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 4, 5, 6 }, result.Approvers.Select(a => a.UserId));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task FlowAdminFallback_UsesApprovalAdminUserIds()
    {
        using var db = TestDbContextFactory.Create(nameof(FlowAdminFallback_UsesApprovalAdminUserIds));
        db.Set<SysUser>().Add(new SysUser { FID = 1, FName = "管理员", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[], "fallback":{"type":"flowAdmin"}}"""
        };

        var result = await resolver.ResolveAsync(
            stage,
            new CfCard(),
            new Dictionary<string, object?>(),
            flowOrgId: 100,
            initiatorId: 99,
            flowSettingsJson: """{"approvalAdminUserIds":[1]}""");

        Assert.True(result.Success);
        Assert.Single(result.Approvers);
        Assert.Equal(1, result.Approvers[0].UserId);
        Assert.Contains("flowAdmin", result.FallbackReason);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task OrgChain_ResolvesManagersFromCurrentOrgToStopOrg()
    {
        using var db = TestDbContextFactory.Create(nameof(OrgChain_ResolvesManagersFromCurrentOrgToStopOrg));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 11, FName = "部门负责人", FStatus = 1 },
            new SysUser { FID = 22, FName = "区域负责人", FStatus = 1 });
        db.Set<SysOrganization>().AddRange(
            new SysOrganization { FID = 100, FName = "部门", FParentId = 200, FManagerId = 11, FStatus = 1 },
            new SysOrganization { FID = 200, FName = "区域", FParentId = 300, FManagerId = 22, FStatus = 1 },
            new SysOrganization { FID = 300, FName = "总部", FParentId = 0, FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "orgChain",
            FAssigneeConfigJson = """{"start":"currentOrg","stopOrgId":200}"""
        };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 11, 22 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, approver => Assert.Equal("orgChain", approver.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task AmountMatrix_UsesFirstMatchingAmountRange()
    {
        using var db = TestDbContextFactory.Create(nameof(AmountMatrix_UsesFirstMatchingAmountRange));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 11, FName = "主管", FStatus = 1 },
            new SysUser { FID = 22, FName = "总经理", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "amountMatrix",
            FAssigneeConfigJson = """
            {
              "amountField":"amount",
              "ranges":[
                {"min":0,"max":4999,"users":[{"userId":11}]},
                {"min":5000,"users":[{"userId":22}]}
              ]
            }
            """
        };
        var cardData = new Dictionary<string, object?> { ["amount"] = 6800m };

        var result = await resolver.ResolveAsync(stage, new CfCard(), cardData, flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Single(result.Approvers);
        Assert.Equal(22, result.Approvers[0].UserId);
        Assert.Equal("amountMatrix", result.Approvers[0].Source);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task FeeTypeBp_MapsFeeTypeToFinanceBpUser()
    {
        using var db = TestDbContextFactory.Create(nameof(FeeTypeBp_MapsFeeTypeToFinanceBpUser));
        db.Set<SysUser>().Add(new SysUser { FID = 33, FName = "差旅财务BP", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition
        {
            FAssigneeStrategy = "feeTypeBp",
            FAssigneeConfigJson = """
            {
              "fieldKey":"feeType",
              "mapping":{
                "travel":{"users":[{"userId":33}]}
              }
            }
            """
        };
        var cardData = new Dictionary<string, object?> { ["feeType"] = "travel" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), cardData, flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Single(result.Approvers);
        Assert.Equal(33, result.Approvers[0].UserId);
        Assert.Equal("feeTypeBp", result.Approvers[0].Source);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_WalksDirectSuperiorsInLevelOrder()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_WalksDirectSuperiorsInLevelOrder));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 2, FName = "一级主管", FStatus = 1 },
            new SysUser { FID = 3, FName = "二级主管", FStatus = 1 },
            new SysUser { FID = 4, FName = "三级主管", FStatus = 1 });
        db.Set<SysUserOrganization>().AddRange(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = 2, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 2, FOrgId = 100, FDirectSuperiorId = 3, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 3, FOrgId = 100, FDirectSuperiorId = 4, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 4, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":2}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 1);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 2, 3 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("superiorChain", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_SkipsInactiveSuperiorButPenetratesUpward()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_SkipsInactiveSuperiorButPenetratesUpward));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 2, FName = "已离职主管", FStatus = 0 },
            new SysUser { FID = 3, FName = "上级主管", FStatus = 1 });
        db.Set<SysUserOrganization>().AddRange(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = 2, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 2, FOrgId = 100, FDirectSuperiorId = 3, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 3, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":5}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 1);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 3 }, result.Approvers.Select(a => a.UserId));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_EmptyChainFallsBackToFlowAdmin()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_EmptyChainFallsBackToFlowAdmin));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 9, FName = "流程管理员", FStatus = 1 });
        db.Set<SysUserOrganization>().Add(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":3,"fallback":{"type":"flowAdmin"}}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(),
            flowOrgId: 100, initiatorId: 1, flowSettingsJson: """{"approvalAdminUserIds":[9]}""");

        Assert.True(result.Success);
        Assert.Equal(9, result.Approvers[0].UserId);
        Assert.Contains("flowAdmin", result.FallbackReason);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_ExplicitSourceStageKey_TakesApprovedAssignees()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_ExplicitSourceStageKey_TakesApprovedAssignees));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 11, FName = "初审人", FStatus = 1 },
            new SysUser { FID = 12, FName = "驳回人", FStatus = 1 });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = 500, FFlowVersionId = 900, FStageKey = "first_review", FStageName = "初审", FType = "human" });
        db.Set<CfStageInstance>().Add(new CfStageInstance { FID = 600, FCardId = 700, FStageDefinitionId = 500, FStageName = "初审", FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-10) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 600, FUserId = 11, FUserName = "初审人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 600, FUserId = 12, FUserName = "驳回人", FStatus = "rejected" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 700, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 501, FFlowVersionId = 900, FStageKey = "second", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = """{"sourceStageKey":"first_review"}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 11 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("prevStage", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_Default_TakesMostRecentCompletedHumanStage()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_Default_TakesMostRecentCompletedHumanStage));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 21, FName = "早节点人", FStatus = 1 },
            new SysUser { FID = 22, FName = "近节点人", FStatus = 1 });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 510, FFlowVersionId = 900, FStageKey = "early", FType = "human" },
            new CfStageDefinition { FID = 511, FFlowVersionId = 900, FStageKey = "recent", FType = "human" },
            new CfStageDefinition { FID = 512, FFlowVersionId = 900, FStageKey = "auto_node", FType = "auto" });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance { FID = 610, FCardId = 700, FStageDefinitionId = 510, FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-30) },
            new CfStageInstance { FID = 611, FCardId = 700, FStageDefinitionId = 511, FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-5) },
            new CfStageInstance { FID = 612, FCardId = 700, FStageDefinitionId = 512, FType = "auto", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-1) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 610, FUserId = 21, FUserName = "早节点人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 611, FUserId = 22, FUserName = "近节点人", FStatus = "approved" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 700, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 513, FFlowVersionId = 900, FStageKey = "current", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = null };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 22 }, result.Approvers.Select(a => a.UserId)); // 排除 auto_node(612) 与更早的 early(610)
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_ExplicitSource_ExcludesCancelledRound_TakesNonCancelled()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_ExplicitSource_ExcludesCancelledRound_TakesNonCancelled));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 41, FName = "被撤销轮次人", FStatus = 1 },
            new SysUser { FID = 42, FName = "有效轮次人", FStatus = 1 });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = 520, FFlowVersionId = 900, FStageKey = "review_source", FStageName = "来源节点", FType = "human" });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance { FID = 630, FCardId = 700, FStageDefinitionId = 520, FStageName = "来源节点", FType = "human", FRound = 2, FStatus = "cancelled", FCompletedTime = DateTime.Now.AddMinutes(-5) },
            new CfStageInstance { FID = 631, FCardId = 700, FStageDefinitionId = 520, FStageName = "来源节点", FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-10) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 630, FUserId = 41, FUserName = "被撤销轮次人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 631, FUserId = 42, FUserName = "有效轮次人", FStatus = "approved" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 700, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 521, FFlowVersionId = 900, FStageKey = "current", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = """{"sourceStageKey":"review_source"}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 42 }, result.Approvers.Select(a => a.UserId)); // 高轮次(2)已 cancelled 被排除，取非撤销的轮次1
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_ExplicitSource_MultipleRounds_TakesLatestRound()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_ExplicitSource_MultipleRounds_TakesLatestRound));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 51, FName = "第一轮人", FStatus = 1 },
            new SysUser { FID = 52, FName = "第二轮人", FStatus = 1 });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = 522, FFlowVersionId = 900, FStageKey = "review_source2", FStageName = "来源节点2", FType = "human" });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance { FID = 640, FCardId = 701, FStageDefinitionId = 522, FStageName = "来源节点2", FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-20) },
            new CfStageInstance { FID = 641, FCardId = 701, FStageDefinitionId = 522, FStageName = "来源节点2", FType = "human", FRound = 2, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-5) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 640, FUserId = 51, FUserName = "第一轮人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 641, FUserId = 52, FUserName = "第二轮人", FStatus = "approved" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 701, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 523, FFlowVersionId = 900, FStageKey = "current2", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = """{"sourceStageKey":"review_source2"}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 52 }, result.Approvers.Select(a => a.UserId)); // OrderByDescending(FRound) 取最新轮次2
    }

    [Fact]
    public async global::System.Threading.Tasks.Task InitiatorSelect_ReadsAssignmentsByStageKey()
    {
        using var db = TestDbContextFactory.Create(nameof(InitiatorSelect_ReadsAssignmentsByStageKey));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 31, FName = "发起人指定甲", FStatus = 1 },
            new SysUser { FID = 32, FName = "发起人指定乙", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 800, FInitiatorAssignmentsJson = """{"review":[{"userId":31,"userName":"发起人指定甲"},{"userId":32,"userName":"发起人指定乙"}]}""" };
        var stage = new CfStageDefinition { FStageKey = "review", FAssigneeStrategy = "initiatorSelect" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 31, 32 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("initiatorSelect", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task InitiatorSelect_NoSelectionFallsBackToFlowAdmin()
    {
        using var db = TestDbContextFactory.Create(nameof(InitiatorSelect_NoSelectionFallsBackToFlowAdmin));
        db.Set<SysUser>().Add(new SysUser { FID = 9, FName = "流程管理员", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 801, FInitiatorAssignmentsJson = """{"other":[{"userId":5}]}""" };
        var stage = new CfStageDefinition { FStageKey = "review", FAssigneeStrategy = "initiatorSelect", FAssigneeConfigJson = """{"fallback":{"type":"flowAdmin"}}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(),
            flowOrgId: 100, initiatorId: 99, flowSettingsJson: """{"approvalAdminUserIds":[9]}""");

        Assert.True(result.Success);
        Assert.Equal(9, result.Approvers[0].UserId);
        Assert.Contains("flowAdmin", result.FallbackReason);
    }
}
