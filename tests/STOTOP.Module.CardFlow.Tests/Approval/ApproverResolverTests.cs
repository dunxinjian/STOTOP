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
}
