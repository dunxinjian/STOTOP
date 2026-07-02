using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段2A(M4) 组织模型地基自检：OrgTreeMaterializer 的合法父子规则、范围根解析(4级)、
/// 物化字段(F父类别/F所属网点公司ID/F范围根/F路径) 与闭包表重建。
/// (DB CHECK 约束无法在 InMemory 复现，靠真实库迁移 V8 验证；此处覆盖等价的应用层规则。)
/// </summary>
public class OrgModelTests
{
    private static void AddNode(STOTOPDbContext ctx, long id, long parentId, OrgKind kind, string name)
    {
        ctx.Set<SysOrganization>().Add(new SysOrganization
        {
            FID = id,
            FUID = $"u{id}",
            FName = name,
            FCode = $"C{id}",
            FParentId = parentId,
            FKind = (int)kind,
            FTypeId = 5,
            FStatus = 1,
        });
    }

    /// <summary>建一棵覆盖全部 4 个范围层(集团/区域公司/中心/网点公司)的代表树并物化。</summary>
    private static STOTOPDbContext BuildAndRebuild(string db)
    {
        var ctx = TestDbContextFactory.Create(db);
        // 1 MDSTO(集团)
        AddNode(ctx, 1, 0, OrgKind.Group, "MDSTO");
        //   2 石家庄申通(区域公司) → 3 业务一中心(部门) → 4 栾城网点(部门) → 5 战虎团(部门, 深部门链)
        AddNode(ctx, 2, 1, OrgKind.Region, "石家庄申通");
        AddNode(ctx, 3, 2, OrgKind.Dept, "业务一中心");
        AddNode(ctx, 4, 3, OrgKind.Dept, "栾城网点");
        AddNode(ctx, 5, 4, OrgKind.Dept, "战虎团");
        //   192 太仓美申(区域公司) → 193 经营支持部(部门) ; 194 城区公司(网点公司) → 197 洋沙承包区(部门)
        AddNode(ctx, 192, 1, OrgKind.Region, "太仓美申");
        AddNode(ctx, 193, 192, OrgKind.Dept, "经营支持部");
        AddNode(ctx, 194, 192, OrgKind.Company, "城区公司");
        AddNode(ctx, 197, 194, OrgKind.Dept, "洋沙承包区");
        //   251 集团大市场中心(部门, 集团直属)
        AddNode(ctx, 251, 1, OrgKind.Dept, "集团大市场中心");
        //   运营中心(有网点公司子树) 260 → 261 中心网点公司(网点公司) → 262 组(部门)
        AddNode(ctx, 260, 2, OrgKind.Center, "运营中心");
        AddNode(ctx, 261, 260, OrgKind.Company, "中心网点公司");
        AddNode(ctx, 262, 261, OrgKind.Dept, "组");
        //   管理中心(无网点公司子树) 263 → 264 管理部(部门)
        AddNode(ctx, 263, 2, OrgKind.Center, "管理中心");
        AddNode(ctx, 264, 263, OrgKind.Dept, "管理部");
        ctx.SaveChanges();

        OrgTreeMaterializer.RebuildAll(ctx);
        return ctx;
    }

    [Fact]
    public void 范围根_四级解析正确()
    {
        using var ctx = BuildAndRebuild("orgmodel_scope");
        var byId = ctx.Set<SysOrganization>().IgnoreQueryFilters().ToDictionary(o => o.FID);

        // 集团级：集团根自身 + 集团直属部门
        Assert.Equal(((int)OrgScopeType.Group, 1L), (byId[1].FScopeRootType, byId[1].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Group, 1L), (byId[251].FScopeRootType, byId[251].FScopeRootId));

        // 区域公司级：区域公司自身 + 其直属部门 + 深部门链末端
        Assert.Equal(((int)OrgScopeType.Region, 2L), (byId[2].FScopeRootType, byId[2].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Region, 2L), (byId[3].FScopeRootType, byId[3].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Region, 2L), (byId[5].FScopeRootType, byId[5].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Region, 192L), (byId[193].FScopeRootType, byId[193].FScopeRootId));

        // 网点公司级：网点公司自身 + 其下部门(承包区)
        Assert.Equal(((int)OrgScopeType.Company, 194L), (byId[194].FScopeRootType, byId[194].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Company, 194L), (byId[197].FScopeRootType, byId[197].FScopeRootId));

        // 中心级：运营中心(子树含网点公司)自身落中心级；其下网点公司仍落网点公司级
        Assert.Equal(((int)OrgScopeType.Center, 260L), (byId[260].FScopeRootType, byId[260].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Company, 261L), (byId[261].FScopeRootType, byId[261].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Company, 261L), (byId[262].FScopeRootType, byId[262].FScopeRootId));

        // 管理中心(子树无网点公司)及其下部门落区域公司级(非中心级)
        Assert.Equal(((int)OrgScopeType.Region, 2L), (byId[263].FScopeRootType, byId[263].FScopeRootId));
        Assert.Equal(((int)OrgScopeType.Region, 2L), (byId[264].FScopeRootType, byId[264].FScopeRootId));
    }

    [Fact]
    public void 物化_父类别_网点公司归属_路径_租户()
    {
        using var ctx = BuildAndRebuild("orgmodel_materialize");
        var byId = ctx.Set<SysOrganization>().IgnoreQueryFilters().ToDictionary(o => o.FID);

        // F父类别：根为 null；子取父 FKind
        Assert.Null(byId[1].FParentKind);
        Assert.Equal((int)OrgKind.Group, byId[2].FParentKind);
        Assert.Equal((int)OrgKind.Company, byId[197].FParentKind); // 承包区 父=网点公司
        Assert.Equal((int)OrgKind.Dept, byId[5].FParentKind);      // 深部门链

        // F所属网点公司ID：网点公司自身及其后代=该公司；其余=null
        Assert.Equal(194L, byId[194].FCompanyId);
        Assert.Equal(194L, byId[197].FCompanyId);
        Assert.Equal(261L, byId[262].FCompanyId);
        Assert.Null(byId[1].FCompanyId);
        Assert.Null(byId[3].FCompanyId);   // 石家庄申通下部门链, 无网点公司祖先
        Assert.Null(byId[193].FCompanyId);

        // F路径：/根/.../自身/
        Assert.Equal("/1/", byId[1].FPath);
        Assert.Equal("/1/192/194/197/", byId[197].FPath);
        Assert.Equal("/1/2/260/261/262/", byId[262].FPath);

        // F租户ID：单树全部 = 根 FID
        Assert.All(byId.Values, o => Assert.Equal(1L, o.FTenantId));
    }

    [Fact]
    public void 闭包_含自反且祖先链完整()
    {
        using var ctx = BuildAndRebuild("orgmodel_closure");
        var closure = ctx.Set<SysOrgClosure>().ToList();

        // 自反行：每节点一条 (自身,自身,0)
        var nodeCount = ctx.Set<SysOrganization>().IgnoreQueryFilters().Count();
        Assert.Equal(nodeCount, closure.Count(c => c.FDepth == 0 && c.FAncestorId == c.FDescendantId));

        // 承包区(197) 的祖先链: 197(0),194(1),192(2),1(3)
        var anc197 = closure.Where(c => c.FDescendantId == 197)
            .ToDictionary(c => c.FAncestorId, c => c.FDepth);
        Assert.Equal(0, anc197[197]);
        Assert.Equal(1, anc197[194]);
        Assert.Equal(2, anc197[192]);
        Assert.Equal(3, anc197[1]);
        Assert.Equal(4, anc197.Count); // 恰好 4 个祖先(含自身)

        // 根(1) 是所有节点的祖先(后代计数 = 节点总数)
        Assert.Equal(nodeCount, closure.Count(c => c.FAncestorId == 1));

        // 闭包行携带后代的租户
        Assert.All(closure, c => Assert.Equal(1L, c.FTenantId));
    }

    [Fact]
    public void 闭包_删除子树后重建收敛()
    {
        using var ctx = BuildAndRebuild("orgmodel_closure_rebuild");
        // 删除承包区 197 后重建，闭包不应再引用 197
        var n197 = ctx.Set<SysOrganization>().IgnoreQueryFilters().First(o => o.FID == 197);
        ctx.Set<SysOrganization>().Remove(n197);
        ctx.SaveChanges();
        OrgTreeMaterializer.RebuildAll(ctx);

        var closure = ctx.Set<SysOrgClosure>().ToList();
        Assert.DoesNotContain(closure, c => c.FDescendantId == 197 || c.FAncestorId == 197);
    }

    [Fact]
    public void 合法根类别()
    {
        Assert.True(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Group));
        Assert.True(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Region));
        Assert.True(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Company));
        Assert.False(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Center));
        Assert.False(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Dept));
        Assert.False(OrgTreeMaterializer.IsLegalRootKind((int)OrgKind.Team));
    }

    [Fact]
    public void 合法父子对_含放宽的部门嵌套()
    {
        // 合法
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Group, (int)OrgKind.Region));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Group, (int)OrgKind.Dept));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Region, (int)OrgKind.Company));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Region, (int)OrgKind.Dept));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Center, (int)OrgKind.Company));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Company, (int)OrgKind.Dept));
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Dept, (int)OrgKind.Dept));   // 放宽
        Assert.True(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Dept, (int)OrgKind.Team));

        // 非法
        Assert.False(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Group, (int)OrgKind.Company));
        Assert.False(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Company, (int)OrgKind.Region));
        Assert.False(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Company, (int)OrgKind.Company));
        Assert.False(OrgTreeMaterializer.IsLegalChild((int)OrgKind.Team, (int)OrgKind.Dept));  // 班组是叶
    }
}
