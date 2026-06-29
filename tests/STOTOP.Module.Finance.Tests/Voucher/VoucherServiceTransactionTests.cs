using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

public class VoucherServiceTransactionTests
{
    private const long Org = 100;
    private const long AcctSet = 7;

    // 包装真实分录仓储，在第 N 次 AddAsync 抛错，模拟"分录写入中途失败"
    private sealed class ThrowOnNthEntryRepository : IRepository<FinVoucherEntry>
    {
        private readonly IRepository<FinVoucherEntry> _inner;
        private readonly int _throwOn;
        private int _count;
        public ThrowOnNthEntryRepository(IRepository<FinVoucherEntry> inner, int throwOn) { _inner = inner; _throwOn = throwOn; }
        public Task<FinVoucherEntry> AddAsync(FinVoucherEntry entity)
        {
            if (++_count == _throwOn) throw new InvalidOperationException("模拟分录写入失败");
            return _inner.AddAsync(entity);
        }
        public Task<FinVoucherEntry?> GetByIdAsync(long id) => _inner.GetByIdAsync(id);
        public Task<List<FinVoucherEntry>> GetAllAsync() => _inner.GetAllAsync();
        public Task UpdateAsync(FinVoucherEntry entity) => _inner.UpdateAsync(entity);
        public Task DeleteAsync(long id) => _inner.DeleteAsync(id);
        public IQueryable<FinVoucherEntry> Query() => _inner.Query();
    }

    private sealed class SqliteOrgAccessor(long orgId) : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; } = orgId;
    }

    private static STOTOPDbContext CreateSqliteDb(SqliteConnection conn)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinAmoebaPLTemplate).Assembly);
        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlite(conn)
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, SqliteCompatModelCustomizer>()
            .Options;
        var db = new STOTOPDbContext(options, new SqliteOrgAccessor(Org));
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Create_rolls_back_header_when_entry_write_fails()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        await using var db = CreateSqliteDb(conn);

        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        await db.SaveChangesAsync();

        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var opLog = new OperationLogService(
            new Repository<FinOperationLog>(db), http, Microsoft.Extensions.Logging.Abstractions.NullLogger<OperationLogService>.Instance);
        var changeTracking = new ChangeTrackingService(new Repository<FinChangeHistory>(db), http);
        var service = new VoucherService(
            new Repository<FinVoucher>(db),
            new ThrowOnNthEntryRepository(new Repository<FinVoucherEntry>(db), throwOn: 2),
            new Repository<FinAccount>(db),
            new Repository<FinAccountPeriod>(db),
            opLog, changeTracking, http,
            new VoucherServiceTestHarness.NoOpEventDispatcher(), db);

        var request = new CreateVoucherRequest
        {
            VoucherWord = "记", Date = new DateTime(2026, 6, 15), PeriodId = 0,
            Entries =
            {
                new CreateVoucherEntryRequest { LineNo = 1, Summary = "t", AccountId = 1, DebitAmount = 100m },
                new CreateVoucherEntryRequest { LineNo = 2, Summary = "t", AccountId = 2, CreditAmount = 100m },
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, "tester", AcctSet));

        // 关键断言：分录写入失败后，凭证头不得残留（事务整体回滚）
        Assert.Empty(db.Set<FinVoucher>());
        Assert.Empty(db.Set<FinVoucherEntry>());
    }

    // UpdateAsync 是"先删旧分录、再插新分录"的最复杂写路径：插新分录中途失败时，
    // 事务必须整体回滚——旧分录不能丢、凭证内容不被部分改写。
    [Fact]
    public async Task Update_rolls_back_and_keeps_old_entries_when_new_entry_write_fails()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        await using var db = CreateSqliteDb(conn);

        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        await db.SaveChangesAsync();

        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);

        // 1) 先正常创建一张含 2 条分录(金额100)的凭证
        var created = await VoucherServiceTestHarness.Build(db, http).CreateAsync(new CreateVoucherRequest
        {
            VoucherWord = "记", Date = new DateTime(2026, 6, 15), PeriodId = 0,
            Entries =
            {
                new CreateVoucherEntryRequest { LineNo = 1, Summary = "原始", AccountId = 1, DebitAmount = 100m },
                new CreateVoucherEntryRequest { LineNo = 2, Summary = "原始", AccountId = 2, CreditAmount = 100m },
            }
        }, "tester", AcctSet);

        // 2) 用"插新分录第 1 次即抛错"的 service 更新（UpdateAsync 先删旧2条→插新第1条即抛）
        var opLog = new OperationLogService(
            new Repository<FinOperationLog>(db), http, Microsoft.Extensions.Logging.Abstractions.NullLogger<OperationLogService>.Instance);
        var changeTracking = new ChangeTrackingService(new Repository<FinChangeHistory>(db), http);
        var faulty = new VoucherService(
            new Repository<FinVoucher>(db),
            new ThrowOnNthEntryRepository(new Repository<FinVoucherEntry>(db), throwOn: 1),
            new Repository<FinAccount>(db),
            new Repository<FinAccountPeriod>(db),
            opLog, changeTracking, http,
            new VoucherServiceTestHarness.NoOpEventDispatcher(), db);

        var updateReq = new CreateVoucherRequest
        {
            VoucherWord = "记", Date = new DateTime(2026, 6, 16), PeriodId = 0,
            Entries =
            {
                new CreateVoucherEntryRequest { LineNo = 1, Summary = "篡改", AccountId = 1, DebitAmount = 999m },
                new CreateVoucherEntryRequest { LineNo = 2, Summary = "篡改", AccountId = 2, CreditAmount = 999m },
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => faulty.UpdateAsync(created.Id, updateReq, "attacker"));

        // 3) 用同一连接的全新上下文读 DB 真实状态（绕开 ChangeTracker 残留）：旧2条分录仍在、金额仍为100、无999
        await using var verify = CreateSqliteDb(conn);
        var entries = verify.Set<FinVoucherEntry>().Where(e => e.FVoucherId == created.Id).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.True(e.FDebitAmount == 100m || e.FCreditAmount == 100m));
        Assert.DoesNotContain(entries, e => e.FDebitAmount == 999m || e.FCreditAmount == 999m);
    }

    // CardFlow 审批流(FlowEngineService)在自己的事务内经 Bridge→CreateAsync 生成凭证：
    // WithTransactionAsync 必须复用外层事务、不得嵌套 BeginTransactionAsync（EF 不支持→否则抛错）。
    [Fact]
    public async Task Create_joins_existing_outer_transaction_without_nesting()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        await using var db = CreateSqliteDb(conn);

        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        await db.SaveChangesAsync();

        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        // 模拟外层事务（如 FlowEngineService）：在其中创建凭证不应因嵌套 BeginTransaction 抛错
        await using var outer = await db.Database.BeginTransactionAsync();
        var dto = await service.CreateAsync(new CreateVoucherRequest
        {
            VoucherWord = "记", Date = new DateTime(2026, 6, 15), PeriodId = 0,
            Entries =
            {
                new CreateVoucherEntryRequest { LineNo = 1, Summary = "t", AccountId = 1, DebitAmount = 100m },
                new CreateVoucherEntryRequest { LineNo = 2, Summary = "t", AccountId = 2, CreditAmount = 100m },
            }
        }, "tester", AcctSet);
        await outer.CommitAsync(); // 由外层提交

        Assert.True(dto.Id > 0);
        await using var verify = CreateSqliteDb(conn);
        Assert.Single(verify.Set<FinVoucher>()); // 外层提交后凭证落库
    }
}
