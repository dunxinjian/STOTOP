using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services;
using Xunit;

namespace STOTOP.Module.CRM.Tests.BonusAndOrg;

// STOTOP.Module 下同时有 Task/System 子命名空间，会与 System.Threading.Tasks.Task 撞名；
// 在文件作用域命名空间「之后」用 global:: 声明别名消除非泛型 Task 歧义（泛型 Task<T> 不受影响）。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// BonusAndOrg 簇首批单测：奖金方案/明细的状态校验 + 角色映射唯一性 + 组织隔离。
/// 全部使用真实 Repository + InMemory STOTOPDbContext（零 fake 数据访问），断言服务返回 DTO/bool 或回查实体字段。
/// </summary>
public class BonusAndOrgServiceTests
{
    // ---------- BonusService：方案/明细状态校验 ----------

    [Fact]
    public async Task 创建草稿方案带明细_状态为0且明细数正确()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建草稿方案带明细_状态为0且明细数正确), orgId: 1);
        var svc = new BonusService(new Repository<CrmBonusPlan>(db), new Repository<CrmBonusDetail>(db));

        var dto = await svc.CreateBonusPlanAsync(new CreateBonusPlanRequest
        {
            OrgId = 1,
            Period = "2026-06",
            TotalAmount = 10000m,
            Details = new List<CreateBonusDetailRequest>
            {
                new() { EmployeeId = 101, Amount = 6000m, BonusType = 1 },
                new() { EmployeeId = 102, Amount = 4000m, BonusType = 1 }
            }
        });

        Assert.Equal(0, dto.Status); // 新建即草稿
        Assert.Equal("2026-06", dto.Period);
        Assert.Equal(2, dto.Details.Count);
        Assert.Equal(10000m, dto.TotalAmount);
    }

    [Fact]
    public async Task 编辑非草稿方案_抛只有草稿可编辑()
    {
        await using var db = TestDbContextFactory.Create(nameof(编辑非草稿方案_抛只有草稿可编辑), orgId: 1);
        var planRepo = new Repository<CrmBonusPlan>(db);
        var svc = new BonusService(planRepo, new Repository<CrmBonusDetail>(db));

        var plan = await planRepo.AddAsync(new CrmBonusPlan { FPeriod = "2026-06", FTotalAmount = 100m, FStatus = 1 }); // 非草稿（已提交）

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateBonusPlanAsync(plan.FID, new UpdateBonusPlanRequest { OrgId = 1, Period = "2026-07", TotalAmount = 200m }));
        Assert.Contains("只有草稿状态的方案可以编辑", ex.Message);
    }

    [Fact]
    public async Task 删除非草稿方案_抛只有草稿可删除()
    {
        await using var db = TestDbContextFactory.Create(nameof(删除非草稿方案_抛只有草稿可删除), orgId: 1);
        var planRepo = new Repository<CrmBonusPlan>(db);
        var svc = new BonusService(planRepo, new Repository<CrmBonusDetail>(db));

        var plan = await planRepo.AddAsync(new CrmBonusPlan { FPeriod = "2026-06", FTotalAmount = 100m, FStatus = 2 }); // 非草稿

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteBonusPlanAsync(plan.FID));
        Assert.Contains("只有草稿状态的方案可以删除", ex.Message);
    }

    [Fact]
    public async Task 给不存在的方案加明细_抛方案不存在()
    {
        await using var db = TestDbContextFactory.Create(nameof(给不存在的方案加明细_抛方案不存在), orgId: 1);
        var svc = new BonusService(new Repository<CrmBonusPlan>(db), new Repository<CrmBonusDetail>(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddBonusDetailAsync(999999, new CreateBonusDetailRequest { EmployeeId = 1, Amount = 100m, BonusType = 1 }));
        Assert.Contains("奖金方案不存在", ex.Message);
    }

    [Fact]
    public async Task 给非草稿方案加明细_抛只有草稿可添加明细()
    {
        await using var db = TestDbContextFactory.Create(nameof(给非草稿方案加明细_抛只有草稿可添加明细), orgId: 1);
        var planRepo = new Repository<CrmBonusPlan>(db);
        var svc = new BonusService(planRepo, new Repository<CrmBonusDetail>(db));

        var plan = await planRepo.AddAsync(new CrmBonusPlan { FPeriod = "2026-06", FTotalAmount = 100m, FStatus = 1 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddBonusDetailAsync(plan.FID, new CreateBonusDetailRequest { EmployeeId = 1, Amount = 100m, BonusType = 1 }));
        Assert.Contains("只有草稿状态的方案可以添加明细", ex.Message);
    }

    [Fact]
    public async Task 删除非草稿方案下的明细_抛只有草稿可删除明细()
    {
        await using var db = TestDbContextFactory.Create(nameof(删除非草稿方案下的明细_抛只有草稿可删除明细), orgId: 1);
        var planRepo = new Repository<CrmBonusPlan>(db);
        var detailRepo = new Repository<CrmBonusDetail>(db);
        var svc = new BonusService(planRepo, detailRepo);

        var plan = await planRepo.AddAsync(new CrmBonusPlan { FPeriod = "2026-06", FTotalAmount = 100m, FStatus = 1 });
        var detail = await detailRepo.AddAsync(new CrmBonusDetail { FPlanId = plan.FID, FEmployeeId = 1, FAmount = 100m, FBonusType = 1 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteBonusDetailAsync(detail.FID));
        Assert.Contains("只有草稿状态的方案可以删除明细", ex.Message);
    }

    // ---------- CrmOrgService：角色映射唯一性 + 角色筛选 ----------

    [Fact]
    public async Task 创建重复角色映射_首次成功二次抛已存在()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建重复角色映射_首次成功二次抛已存在), orgId: 1);
        var svc = new CrmOrgService(new Repository<CrmRoleMapping>(db));

        var first = await svc.CreateRoleMappingAsync(new CreateRoleMappingRequest { OrgId = 1, EmployeeId = 200, Role = 1 });
        Assert.Equal(200, first.EmployeeId);
        Assert.Equal(1, first.Role);
        Assert.Equal(1, first.OrgId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateRoleMappingAsync(new CreateRoleMappingRequest { OrgId = 1, EmployeeId = 200, Role = 1 }));
        Assert.Contains("该员工在此组织下已存在相同角色映射", ex.Message);
    }

    [Fact]
    public async Task BD列表与运维列表_仅按角色筛选()
    {
        await using var db = TestDbContextFactory.Create(nameof(BD列表与运维列表_仅按角色筛选), orgId: 1);
        var roleRepo = new Repository<CrmRoleMapping>(db);
        var svc = new CrmOrgService(roleRepo);

        await roleRepo.AddAsync(new CrmRoleMapping { FOrgId = 1, FEmployeeId = 301, FRole = 1 }); // BD
        await roleRepo.AddAsync(new CrmRoleMapping { FOrgId = 1, FEmployeeId = 302, FRole = 1 }); // BD
        await roleRepo.AddAsync(new CrmRoleMapping { FOrgId = 1, FEmployeeId = 303, FRole = 2 }); // 运维

        var bds = await svc.GetBdListAsync(1);
        var maintenance = await svc.GetMaintenanceListAsync(1);

        Assert.Equal(2, bds.Count);
        Assert.All(bds, r => Assert.Equal(1, r.Role));
        Assert.Single(maintenance);
        Assert.All(maintenance, r => Assert.Equal(2, r.Role));
    }

    // ---------- 组织隔离：全局查询过滤器 ----------

    [Fact]
    public async Task 角色映射查询_只返回当前组织数据()
    {
        const string dbName = nameof(角色映射查询_只返回当前组织数据);

        // org1 与 org2 共享同一 InMemory 库，各自上下文 seed 一条记录
        await using (var dbOrg1Seed = TestDbContextFactory.CreateShared(dbName, orgId: 1))
        {
            await new Repository<CrmRoleMapping>(dbOrg1Seed).AddAsync(new CrmRoleMapping { FEmployeeId = 401, FRole = 1 });
        }
        await using (var dbOrg2Seed = TestDbContextFactory.CreateShared(dbName, orgId: 2))
        {
            await new Repository<CrmRoleMapping>(dbOrg2Seed).AddAsync(new CrmRoleMapping { FEmployeeId = 402, FRole = 1 });
        }

        await using var dbOrg1 = TestDbContextFactory.CreateShared(dbName, orgId: 1);
        var svc = new CrmOrgService(new Repository<CrmRoleMapping>(dbOrg1));

        var page = await svc.GetRoleMappingsAsync(new RoleMappingQueryRequest { PageIndex = 1, PageSize = 50 });

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(401, page.Items[0].EmployeeId);
        Assert.Equal(1, page.Items[0].OrgId);
    }
}
