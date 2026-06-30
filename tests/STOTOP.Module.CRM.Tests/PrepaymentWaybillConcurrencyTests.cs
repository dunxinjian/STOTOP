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

namespace STOTOP.Module.CRM.Tests;

/// <summary>
/// 号段池并发分配/回收回归测试。必须跑在 SQLite（而非 EF InMemory）上：
/// InMemory 不支持 ExecuteUpdateAsync、也不模拟事务与行级并发，验不出原子扣减。
/// 多连接打同一临时文件库，PRAGMA busy_timeout 让并发写串行化等待（而非 SQLITE_BUSY）。
/// </summary>
public class PrepaymentWaybillConcurrencyTests
{
    private const long Org = 100;

    private sealed class SqliteOrgAccessor(long orgId) : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; } = orgId;
        public long? CurrentTenantId { get; set; } = 1;
        public bool IsPlatformScope { get; set; }
    }

    // 同一临时文件库 + 多连接，模拟多请求并发。Dispose 时统一关连接、清池、删文件。
    private sealed class SharedSqliteDb : IDisposable
    {
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"crm_waybill_{Guid.NewGuid():N}.db");
        private readonly List<SqliteConnection> _conns = new();

        public STOTOPDbContext NewContext()
        {
            STOTOPDbContext.RegisterModuleAssembly(typeof(CrmWaybillPool).Assembly);

            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                // 关外键校验：本测试只验号段池计数的并发原子性，与引用完整性无关，避免播种 Customer/预付款行。
                // busy_timeout 让并发写在锁竞争时等待而非立刻 SQLITE_BUSY 报错。
                cmd.CommandText = "PRAGMA foreign_keys=OFF; PRAGMA busy_timeout=15000;";
                cmd.ExecuteNonQuery();
            }
            lock (_conns) _conns.Add(conn);

            var options = new DbContextOptionsBuilder<STOTOPDbContext>()
                .UseSqlite(conn)
                .ReplaceService<IModelCustomizer, SqliteCompatModelCustomizer>()
                .Options;
            return new STOTOPDbContext(options, new SqliteOrgAccessor(Org));
        }

        public void Dispose()
        {
            lock (_conns)
            {
                foreach (var c in _conns)
                {
                    try { c.Close(); c.Dispose(); } catch { /* 尽力清理 */ }
                }
                _conns.Clear();
            }
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* 尽力清理 */ }
        }
    }

    private static PrepaymentWaybillService BuildService(STOTOPDbContext db) =>
        new(new Repository<CrmWaybillPool>(db),
            new Repository<CrmCustomerAccount>(db),
            new Repository<CrmPrepayment>(db),
            new Repository<CrmWaybillAllocation>(db),
            db);

    private static CrmWaybillPool NewPool(long id, int total, string startNo, string endNo) => new()
    {
        FID = id,
        FOrgId = Org,
        FBrandCode = "ZT",
        FPrefix = null, // 空前缀：发放起止号即纯数字，便于断言号段不重叠
        FStartNo = startNo,
        FEndNo = endNo,
        FTotalCount = total,
        FAllocatedCount = 0,
        FRemainingCount = total,
        FVersion = 0,
        FStatus = 0,
        FCreatedTime = DateTime.Now
    };

    private static AllocateWaybillRequest Req(int count) => new()
    {
        PoolId = 1,
        Count = count,
        CustomerId = "C1",
        OperatorId = 1,
        PrepaymentId = 0
    };

    [Fact]
    public async Task AllocateWaybill_concurrent_over_capacity_does_not_oversell()
    {
        using var shared = new SharedSqliteDb();

        await using (var setup = shared.NewContext())
        {
            setup.Database.EnsureCreated();
            setup.Set<CrmWaybillPool>().Add(NewPool(id: 1, total: 10, startNo: "1000", endNo: "1009"));
            await setup.SaveChangesAsync();
        }

        // 两笔并发分配，各请求 6（合计 12 > 容量 10）：恰好一成一败
        async Task<(bool ok, string? err)> Allocate(int count)
        {
            await using var db = shared.NewContext();
            var svc = BuildService(db);
            try
            {
                await svc.AllocateWaybillAsync(Req(count));
                return (true, null);
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message);
            }
        }

        var results = await Task.WhenAll(Allocate(6), Allocate(6));

        Assert.Equal(1, results.Count(r => r.ok));
        Assert.Contains(results, r => !r.ok && r.err!.Contains("剩余数量不足"));

        await using var verify = shared.NewContext();
        var pool = await verify.Set<CrmWaybillPool>().AsNoTracking().FirstAsync(p => p.FID == 1);
        Assert.Equal(6, pool.FAllocatedCount);          // 只发放了一笔
        Assert.Equal(4, pool.FRemainingCount);          // 未被并发扣成 -2
        Assert.True(pool.FRemainingCount >= 0, "剩余数量被扣成负数");
        Assert.True(pool.FAllocatedCount <= pool.FTotalCount, "发放量超过池容量");
        Assert.Equal(1, await verify.Set<CrmWaybillAllocation>().CountAsync(a => a.FPoolId == 1));
    }

    [Fact]
    public async Task AllocateWaybill_concurrent_fanout_keeps_invariants_and_no_overlap()
    {
        using var shared = new SharedSqliteDb();
        const int total = 20, perReq = 3, taskCount = 10; // 需求 30 > 容量 20，必有失败

        await using (var setup = shared.NewContext())
        {
            setup.Database.EnsureCreated();
            setup.Set<CrmWaybillPool>().Add(NewPool(id: 1, total: total, startNo: "1000", endNo: "1019"));
            await setup.SaveChangesAsync();
        }

        async Task<bool> Allocate()
        {
            await using var db = shared.NewContext();
            var svc = BuildService(db);
            try
            {
                await svc.AllocateWaybillAsync(Req(perReq));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var oks = await Task.WhenAll(Enumerable.Range(0, taskCount).Select(_ => Allocate()));
        int successes = oks.Count(x => x);

        await using var verify = shared.NewContext();
        var pool = await verify.Set<CrmWaybillPool>().AsNoTracking().FirstAsync(p => p.FID == 1);
        var allocs = await verify.Set<CrmWaybillAllocation>().AsNoTracking()
            .Where(a => a.FPoolId == 1).ToListAsync();

        // 不超发、剩余不为负、计数与分配记录自洽
        Assert.True(pool.FRemainingCount >= 0, $"剩余被扣成负数: {pool.FRemainingCount}");
        Assert.True(pool.FAllocatedCount <= total, $"超发: 已发放 {pool.FAllocatedCount} > 容量 {total}");
        Assert.Equal(successes, allocs.Count);
        Assert.Equal(successes * perReq, pool.FAllocatedCount);
        Assert.Equal(total - pool.FAllocatedCount, pool.FRemainingCount);

        // 展开所有已发放号：必须互异（无两笔分到同一号）且落在池区间内
        long poolStart = long.Parse(pool.FStartNo);
        long poolEnd = poolStart + total - 1;
        var numbers = new List<long>();
        foreach (var a in allocs)
        {
            long s = long.Parse(a.FStartNo);
            long e = long.Parse(a.FEndNo);
            for (long n = s; n <= e; n++) numbers.Add(n);
        }
        Assert.Equal(pool.FAllocatedCount, numbers.Count);
        Assert.Equal(numbers.Count, numbers.Distinct().Count()); // 无重复号
        Assert.All(numbers, n => Assert.InRange(n, poolStart, poolEnd));
    }

    [Fact]
    public async Task RecycleWaybill_restores_pool_and_blocks_double_recycle()
    {
        using var shared = new SharedSqliteDb();

        await using (var setup = shared.NewContext())
        {
            setup.Database.EnsureCreated();
            setup.Set<CrmWaybillPool>().Add(NewPool(id: 1, total: 10, startNo: "1000", endNo: "1009"));
            await setup.SaveChangesAsync();
        }

        long allocId;
        await using (var db = shared.NewContext())
        {
            var dto = await BuildService(db).AllocateWaybillAsync(Req(4));
            allocId = dto.Id;
        }

        await using (var db = shared.NewContext())
        {
            Assert.True(await BuildService(db).RecycleWaybillAsync(allocId));
        }

        await using (var verify = shared.NewContext())
        {
            var pool = await verify.Set<CrmWaybillPool>().AsNoTracking().FirstAsync(p => p.FID == 1);
            Assert.Equal(0, pool.FAllocatedCount);
            Assert.Equal(10, pool.FRemainingCount);
            var alloc = await verify.Set<CrmWaybillAllocation>().AsNoTracking().FirstAsync(a => a.FID == allocId);
            Assert.Equal(2, alloc.FStatus); // 已回收
        }

        // 重复回收应被拒，且不得二次加回号段池
        await using (var db = shared.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => BuildService(db).RecycleWaybillAsync(allocId));
            var pool = await db.Set<CrmWaybillPool>().AsNoTracking().FirstAsync(p => p.FID == 1);
            Assert.Equal(10, pool.FRemainingCount); // 仍为 10，未被重复恢复
        }
    }
}
