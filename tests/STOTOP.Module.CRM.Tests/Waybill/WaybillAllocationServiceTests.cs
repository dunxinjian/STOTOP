using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services;
using Xunit;

namespace STOTOP.Module.CRM.Tests.Waybill;

// STOTOP.Module 下有 Task/System 子命名空间会与 System.Threading.Tasks.Task 撞名；此别名必须紧随命名空间声明。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// PrepaymentWaybillService 号段分配/回收路径单元测试。
/// 分配/回收已改为关系型原子实现（ExecuteUpdateAsync + 事务），EF InMemory 不支持，
/// 故每个用例跑在独立的 SQLite :memory: 库上（连接随用例存活，类销毁时统一关闭）。
/// 并发原子性的回归见 PrepaymentWaybillConcurrencyTests；本类聚焦单笔的号段拼接/计数回写/异常路径。
/// </summary>
public sealed class WaybillAllocationServiceTests : IDisposable
{
    private const long Org = 1;
    private readonly List<SqliteConnection> _conns = new();

    private sealed class OrgAccessor(long orgId) : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; } = orgId;
    }

    /// <summary>新建一个 SQLite :memory: 上下文（已建表）。连接保持打开以维持库存活，登记后由类销毁统一关闭。</summary>
    private STOTOPDbContext NewDb()
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CrmWaybillPool).Assembly);

        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            // 关外键校验：本类只验号段拼接/计数回写/异常路径，与引用完整性无关，不播种 Customer/预付款行。
            cmd.CommandText = "PRAGMA foreign_keys=OFF;";
            cmd.ExecuteNonQuery();
        }
        _conns.Add(conn);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlite(conn)
            .ReplaceService<IModelCustomizer, SqliteCompatModelCustomizer>()
            .EnableSensitiveDataLogging()
            .Options;

        var db = new STOTOPDbContext(options, new OrgAccessor(Org));
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose()
    {
        foreach (var c in _conns)
        {
            try { c.Close(); c.Dispose(); } catch { /* 尽力清理 */ }
        }
        _conns.Clear();
        SqliteConnection.ClearAllPools();
    }

    private static PrepaymentWaybillService Build(STOTOPDbContext db) => new(
        new Repository<CrmWaybillPool>(db),
        new Repository<CrmCustomerAccount>(db),
        new Repository<CrmPrepayment>(db),
        new Repository<CrmWaybillAllocation>(db),
        db);

    [Fact]
    public async Task 分配成功_返回DTO起止号按前缀加起号拼接()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 0, FRemainingCount = 1000, FStatus = 0
        });

        var svc = Build(db);
        var dto = await svc.AllocateWaybillAsync(new AllocateWaybillRequest
        {
            PrepaymentId = 999, CustomerId = "C1", PoolId = pool.FID, Count = 10, OperatorId = 7
        });

        // allocStart = parse(1000) + 0 = 1000；allocEnd = 1000 + 10 - 1 = 1009
        Assert.Equal("SF1000", dto.StartNo);
        Assert.Equal("SF1009", dto.EndNo);
        Assert.Equal(10, dto.AllocatedCount);
        Assert.Equal(1, dto.Status); // 已分配
        Assert.Equal("C1", dto.CustomerId);
    }

    [Fact]
    public async Task 分配成功_起号按池已分配数量偏移()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 30, FRemainingCount = 970, FStatus = 0
        });

        var svc = Build(db);
        var dto = await svc.AllocateWaybillAsync(new AllocateWaybillRequest
        {
            PrepaymentId = 1, CustomerId = "C1", PoolId = pool.FID, Count = 5, OperatorId = 1
        });

        // allocStart = 1000 + 30 = 1030；allocEnd = 1030 + 5 - 1 = 1034
        Assert.Equal("SF1030", dto.StartNo);
        Assert.Equal("SF1034", dto.EndNo);
    }

    [Fact]
    public async Task 分配成功_空前缀时号段不带前缀()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "X", FPrefix = null, FStartNo = "2000", FEndNo = "2999",
            FTotalCount = 1000, FAllocatedCount = 0, FRemainingCount = 1000, FStatus = 0
        });

        var svc = Build(db);
        var dto = await svc.AllocateWaybillAsync(new AllocateWaybillRequest
        {
            PrepaymentId = 1, CustomerId = "C1", PoolId = pool.FID, Count = 3, OperatorId = 1
        });

        Assert.Equal("2000", dto.StartNo);
        Assert.Equal("2002", dto.EndNo);
    }

    [Fact]
    public async Task 分配成功_回写号段池已分配与剩余数量()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 0, FRemainingCount = 1000, FStatus = 0
        });

        var svc = Build(db);
        await svc.AllocateWaybillAsync(new AllocateWaybillRequest
        {
            PrepaymentId = 1, CustomerId = "C1", PoolId = pool.FID, Count = 40, OperatorId = 1
        });

        // ExecuteUpdateAsync 走原生 SQL 绕过变更追踪器；同一 context 回读须先清缓存才能见到 DB 新值。
        db.ChangeTracker.Clear();
        var saved = await poolRepo.GetByIdAsync(pool.FID);
        Assert.NotNull(saved);
        Assert.Equal(40, saved!.FAllocatedCount);
        Assert.Equal(960, saved.FRemainingCount);
    }

    [Fact]
    public async Task 分配成功_回写预付款已分配运单数()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var prepayRepo = new Repository<CrmPrepayment>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 0, FRemainingCount = 1000, FStatus = 0
        });
        var prepay = await prepayRepo.AddAsync(new CrmPrepayment
        {
            FCustomerId = "C1", FCustomerAccountId = 1, FBrandCode = "SF",
            FPrepayAmount = 10000, FExpectedWaybillCount = 100, FAllocatedWaybillCount = 0, FStatus = 1
        });

        var svc = Build(db);
        await svc.AllocateWaybillAsync(new AllocateWaybillRequest
        {
            PrepaymentId = prepay.FID, CustomerId = "C1", PoolId = pool.FID, Count = 25, OperatorId = 1
        });

        var savedPrepay = await prepayRepo.GetByIdAsync(prepay.FID);
        Assert.NotNull(savedPrepay);
        Assert.Equal(25, savedPrepay!.FAllocatedWaybillCount);
    }

    [Fact]
    public async Task 分配失败_剩余不足抛业务异常()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 995, FRemainingCount = 5, FStatus = 0
        });

        var svc = Build(db);
        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() =>
            svc.AllocateWaybillAsync(new AllocateWaybillRequest
            {
                PrepaymentId = 1, CustomerId = "C1", PoolId = pool.FID, Count = 10, OperatorId = 1
            }));
        Assert.Contains("剩余数量不足", ex.Message);
    }

    [Fact]
    public async Task 回收成功_分配状态置为已回收且号段池数量回滚()
    {
        await using var db = NewDb();
        var poolRepo = new Repository<CrmWaybillPool>(db);
        var allocRepo = new Repository<CrmWaybillAllocation>(db);
        var pool = await poolRepo.AddAsync(new CrmWaybillPool
        {
            FBrandCode = "SF", FPrefix = "SF", FStartNo = "1000", FEndNo = "1999",
            FTotalCount = 1000, FAllocatedCount = 20, FRemainingCount = 980, FStatus = 0
        });
        var alloc = await allocRepo.AddAsync(new CrmWaybillAllocation
        {
            FPrepaymentId = 1, FCustomerId = "C1", FPoolId = pool.FID,
            FStartNo = "SF1000", FEndNo = "SF1019", FAllocatedCount = 20,
            FAllocationDate = global::System.DateOnly.FromDateTime(global::System.DateTime.Now),
            FOperatorId = 1, FStatus = 1
        });

        var svc = Build(db);
        var ok = await svc.RecycleWaybillAsync(alloc.FID);

        Assert.True(ok);
        // 回收经 ExecuteUpdateAsync 改状态/池计数（绕过追踪器），回读须先清缓存。
        db.ChangeTracker.Clear();
        var savedAlloc = await allocRepo.GetByIdAsync(alloc.FID);
        Assert.Equal(2, savedAlloc!.FStatus); // 已回收

        var savedPool = await poolRepo.GetByIdAsync(pool.FID);
        Assert.Equal(0, savedPool!.FAllocatedCount);   // 20 - 20
        Assert.Equal(1000, savedPool.FRemainingCount); // 980 + 20
    }

    [Fact]
    public async Task 回收失败_非已分配状态抛业务异常()
    {
        await using var db = NewDb();
        var allocRepo = new Repository<CrmWaybillAllocation>(db);
        var alloc = await allocRepo.AddAsync(new CrmWaybillAllocation
        {
            FPrepaymentId = 1, FCustomerId = "C1", FPoolId = 1,
            FStartNo = "SF1000", FEndNo = "SF1019", FAllocatedCount = 20,
            FAllocationDate = global::System.DateOnly.FromDateTime(global::System.DateTime.Now),
            FOperatorId = 1, FStatus = 2 // 已回收，非「已分配(1)」
        });

        var svc = Build(db);
        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() =>
            svc.RecycleWaybillAsync(alloc.FID));
        Assert.Contains("只能回收已分配状态的运单号", ex.Message);
    }

    [Fact]
    public async Task 回收_分配不存在返回false()
    {
        await using var db = NewDb();
        var svc = Build(db);
        var ok = await svc.RecycleWaybillAsync(999999);
        Assert.False(ok);
    }
}
