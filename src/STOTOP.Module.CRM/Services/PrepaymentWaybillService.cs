using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class PrepaymentWaybillService : IPrepaymentWaybillService
{
    private readonly IRepository<CrmWaybillPool> _poolRepo;
    private readonly IRepository<CrmCustomerAccount> _accountRepo;
    private readonly IRepository<CrmPrepayment> _prepaymentRepo;
    private readonly IRepository<CrmWaybillAllocation> _allocationRepo;
    private readonly STOTOPDbContext _db;

    public PrepaymentWaybillService(
        IRepository<CrmWaybillPool> poolRepo,
        IRepository<CrmCustomerAccount> accountRepo,
        IRepository<CrmPrepayment> prepaymentRepo,
        IRepository<CrmWaybillAllocation> allocationRepo,
        STOTOPDbContext db)
    {
        _poolRepo = poolRepo;
        _accountRepo = accountRepo;
        _prepaymentRepo = prepaymentRepo;
        _allocationRepo = allocationRepo;
        _db = db;
    }

    #region Waybill Pool

    public async Task<PagedResult<WaybillPoolDto>> GetWaybillPoolsAsync(WaybillPoolQueryRequest request)
    {
        var query = _poolRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(p => p.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(p => p.FStatus == request.Status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<WaybillPoolDto>
        {
            Items = items.Select(MapPoolToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<WaybillPoolDto?> GetWaybillPoolByIdAsync(long id)
    {
        var entity = await _poolRepo.Query().FirstOrDefaultAsync(p => p.FID == id);
        return entity == null ? null : MapPoolToDto(entity);
    }

    public async Task<WaybillPoolDto> CreateWaybillPoolAsync(CreateWaybillPoolRequest request)
    {
        var entity = new CrmWaybillPool
        {
            FBrandCode = request.BrandCode,
            FPrefix = request.Prefix,
            FStartNo = request.StartNo,
            FEndNo = request.EndNo,
            FTotalCount = request.TotalCount,
            FAllocatedCount = 0,
            FRemainingCount = request.TotalCount,
            FPurchaseDate = request.PurchaseDate,
            FUnitPrice = request.UnitPrice,
            FVersion = 0,
            FStatus = 0,
            FCreatedTime = DateTime.Now
        };

        await _poolRepo.AddAsync(entity);
        return MapPoolToDto(entity);
    }

    public async Task<bool> DeleteWaybillPoolAsync(long id)
    {
        var entity = await _poolRepo.GetByIdAsync(id);
        if (entity == null) return false;
        await _poolRepo.DeleteAsync(id);
        return true;
    }

    #endregion

    #region Customer Account

    public async Task<CustomerAccountDto?> GetCustomerAccountAsync(string customerId, string brandCode)
    {
        var entity = await _accountRepo.Query()
            .FirstOrDefaultAsync(a => a.FCustomerId == customerId && a.FBrandCode == brandCode);
        return entity == null ? null : MapAccountToDto(entity);
    }

    public async Task<CustomerAccountDto> RechargeAccountAsync(long accountId, decimal amount)
    {
        // 原子 UPDATE，避免并发充值时的读-改-写丢失更新
        var affected = await _accountRepo.Query()
            .Where(a => a.FID == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.FBalance, a => a.FBalance + amount)
                .SetProperty(a => a.FTotalRecharge, a => a.FTotalRecharge + amount)
                .SetProperty(a => a.FUpdatedTime, DateTime.Now));

        if (affected == 0)
            throw new InvalidOperationException("客户账户不存在");

        var entity = await _accountRepo.Query().FirstOrDefaultAsync(a => a.FID == accountId);
        return MapAccountToDto(entity!);
    }

    public async Task<CustomerAccountDto> DeductAccountAsync(long accountId, decimal amount)
    {
        // 原子 check-then-debit：余额充足判断由数据库 WHERE 保证，杜绝并发超扣/丢失更新
        var affected = await _accountRepo.Query()
            .Where(a => a.FID == accountId && a.FBalance >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.FBalance, a => a.FBalance - amount)
                .SetProperty(a => a.FTotalConsumption, a => a.FTotalConsumption + amount)
                .SetProperty(a => a.FUpdatedTime, DateTime.Now));

        if (affected == 0)
        {
            var exists = await _accountRepo.Query().AnyAsync(a => a.FID == accountId);
            throw new InvalidOperationException(exists ? "账户余额不足" : "客户账户不存在");
        }

        var entity = await _accountRepo.Query().FirstOrDefaultAsync(a => a.FID == accountId);
        return MapAccountToDto(entity!);
    }

    #endregion

    #region Prepayment

    public async Task<PagedResult<PrepaymentDto>> GetPrepaymentsAsync(PrepaymentQueryRequest request)
    {
        var query = _prepaymentRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(p => p.FCustomerId == request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(p => p.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(p => p.FStatus == request.Status.Value);
        if (request.StartDate.HasValue)
            query = query.Where(p => p.FCreatedTime >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(p => p.FCreatedTime <= request.EndDate.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<PrepaymentDto>
        {
            Items = items.Select(MapPrepaymentToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<PrepaymentDto?> GetPrepaymentByIdAsync(long id)
    {
        var entity = await _prepaymentRepo.Query().FirstOrDefaultAsync(p => p.FID == id);
        return entity == null ? null : MapPrepaymentToDto(entity);
    }

    public async Task<PrepaymentDto> CreatePrepaymentAsync(CreatePrepaymentRequest request)
    {
        var entity = new CrmPrepayment
        {
            FCustomerId = request.CustomerId,
            FCustomerAccountId = request.CustomerAccountId,
            FOrgId = request.OrgId ?? 0,
            FBrandCode = request.BrandCode,
            FPrepayAmount = request.PrepayAmount,
            FReceivedAmount = 0,
            FExpectedWaybillCount = request.ExpectedWaybillCount,
            FAllocatedWaybillCount = 0,
            FStatus = 0, // 待到账
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now
        };

        await _prepaymentRepo.AddAsync(entity);
        return MapPrepaymentToDto(entity);
    }

    public async Task<bool> ConfirmPrepaymentReceivedAsync(long id, decimal receivedAmount, long? bankTransactionId)
    {
        var entity = await _prepaymentRepo.Query().AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);
        if (entity == null) return false;

        entity.FReceivedAmount = receivedAmount;
        entity.FBankTransactionId = bankTransactionId;
        entity.FStatus = 1; // 已到账
        entity.FUpdatedTime = DateTime.Now;
        await _prepaymentRepo.UpdateAsync(entity);

        // 到账时充值账户余额（原子 UPDATE，避免并发丢失更新）
        await _accountRepo.Query()
            .Where(a => a.FID == entity.FCustomerAccountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.FBalance, a => a.FBalance + receivedAmount)
                .SetProperty(a => a.FTotalRecharge, a => a.FTotalRecharge + receivedAmount)
                .SetProperty(a => a.FUpdatedTime, DateTime.Now));

        return true;
    }

    #endregion

    #region Waybill Allocation

    /// <summary>
    /// 把一组写操作包进事务：关系型 provider 且当前无外层事务时自开事务、失败整体回滚；
    /// 已存在外层事务时直接复用、由外层统一提交/回滚（避免 EF 不支持的嵌套 BeginTransaction）；
    /// 非关系型(InMemory) provider 不支持事务，退化为直接执行。
    /// </summary>
    private async Task WithTransactionAsync(Func<Task> writes)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
        {
            await writes();
            return;
        }
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await writes();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<WaybillAllocationDto> AllocateWaybillAsync(AllocateWaybillRequest request)
    {
        WaybillAllocationDto? result = null;
        await WithTransactionAsync(async () =>
        {
            // 原子扣减号段池：把"剩余是否充足"的判断与扣减合并进单条带 WHERE 守卫的 UPDATE，
            // 命中 0 行即剩余不足/池不存在。杜绝并发下两笔分配读到相同剩余而双双扣减导致的超发。
            // 同步递增 F版本号，兼容其他基于乐观锁(IsConcurrencyToken)的写路径。
            var affected = await _db.Set<CrmWaybillPool>()
                .Where(p => p.FID == request.PoolId && p.FRemainingCount >= request.Count)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.FAllocatedCount, p => p.FAllocatedCount + request.Count)
                    .SetProperty(p => p.FRemainingCount, p => p.FRemainingCount - request.Count)
                    .SetProperty(p => p.FVersion, p => p.FVersion + 1)
                    .SetProperty(p => p.FUpdatedTime, DateTime.Now));

            if (affected == 0)
            {
                var exists = await _db.Set<CrmWaybillPool>().AnyAsync(p => p.FID == request.PoolId);
                throw new InvalidOperationException(exists
                    ? $"号段池剩余数量不足，请求 {request.Count}"
                    : "号段池不存在");
            }

            // 扣减成功后回读：此时 F已发放 已含本次增量，借此推出本笔独占的起止号区间。
            // 该回读与上面的 UPDATE 同处一个事务/连接，受行级排它锁串行化保护，区间不会与并发笔重叠。
            var pool = await _db.Set<CrmWaybillPool>()
                .Where(p => p.FID == request.PoolId)
                .Select(p => new { p.FStartNo, p.FPrefix, p.FAllocatedCount })
                .FirstAsync();

            long poolStart = long.Parse(pool.FStartNo);
            long allocStart = poolStart + pool.FAllocatedCount - request.Count;
            long allocEnd = poolStart + pool.FAllocatedCount - 1;

            var allocation = new CrmWaybillAllocation
            {
                FPrepaymentId = request.PrepaymentId,
                FCustomerId = request.CustomerId,
                FPoolId = request.PoolId,
                FStartNo = (pool.FPrefix ?? "") + allocStart.ToString(),
                FEndNo = (pool.FPrefix ?? "") + allocEnd.ToString(),
                FAllocatedCount = request.Count,
                FAllocationDate = DateOnly.FromDateTime(DateTime.Now),
                FOperatorId = request.OperatorId,
                FStatus = 1, // 已分配
                FCreatedTime = DateTime.Now
            };
            _db.Set<CrmWaybillAllocation>().Add(allocation);

            // 更新预付款的已分配运单数
            var prepayment = await _db.Set<CrmPrepayment>().AsTracking()
                .FirstOrDefaultAsync(p => p.FID == request.PrepaymentId);
            if (prepayment != null)
            {
                prepayment.FAllocatedWaybillCount += request.Count;
                prepayment.FUpdatedTime = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            result = MapAllocationToDto(allocation);
        });

        return result!;
    }

    public async Task<bool> RecycleWaybillAsync(long allocationId)
    {
        bool result = false;
        await WithTransactionAsync(async () =>
        {
            var allocation = await _db.Set<CrmWaybillAllocation>()
                .Where(a => a.FID == allocationId)
                .Select(a => new { a.FStatus, a.FPoolId, a.FAllocatedCount })
                .FirstOrDefaultAsync();
            if (allocation == null) { result = false; return; }

            if (allocation.FStatus != 1)
                throw new InvalidOperationException("只能回收已分配状态的运单号");

            // 原子地把分配记录从"已分配(1)"翻为"已回收(2)"：命中 0 行说明已被并发回收，
            // 据此中止本次回收，避免并发双回收把同一段号重复加回（剩余超额、已发放为负）。
            var flipped = await _db.Set<CrmWaybillAllocation>()
                .Where(a => a.FID == allocationId && a.FStatus == 1)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.FStatus, 2)
                    .SetProperty(a => a.FUpdatedTime, DateTime.Now));
            if (flipped == 0)
                throw new InvalidOperationException("只能回收已分配状态的运单号");

            // 原子恢复号段池数量（池不存在则更新 0 行，无副作用）
            await _db.Set<CrmWaybillPool>()
                .Where(p => p.FID == allocation.FPoolId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.FAllocatedCount, p => p.FAllocatedCount - allocation.FAllocatedCount)
                    .SetProperty(p => p.FRemainingCount, p => p.FRemainingCount + allocation.FAllocatedCount)
                    .SetProperty(p => p.FVersion, p => p.FVersion + 1)
                    .SetProperty(p => p.FUpdatedTime, DateTime.Now));

            result = true;
        });
        return result;
    }

    public async Task<List<WaybillAllocationDto>> GetAllocationsByCustomerAsync(string customerId)
    {
        var items = await _allocationRepo.Query()
            .Where(a => a.FCustomerId == customerId)
            .OrderByDescending(a => a.FCreatedTime)
            .ToListAsync();
        return items.Select(MapAllocationToDto).ToList();
    }

    public async Task<List<WaybillAllocationDto>> GetAllocationsByPoolAsync(long poolId)
    {
        var items = await _allocationRepo.Query()
            .Where(a => a.FPoolId == poolId)
            .OrderByDescending(a => a.FCreatedTime)
            .ToListAsync();
        return items.Select(MapAllocationToDto).ToList();
    }

    #endregion

    #region Mapping

    private static WaybillPoolDto MapPoolToDto(CrmWaybillPool e) => new()
    {
        Id = e.FID,
        BrandCode = e.FBrandCode,
        Prefix = e.FPrefix,
        StartNo = e.FStartNo,
        EndNo = e.FEndNo,
        TotalCount = e.FTotalCount,
        AllocatedCount = e.FAllocatedCount,
        RemainingCount = e.FRemainingCount,
        PurchaseDate = e.FPurchaseDate,
        UnitPrice = e.FUnitPrice,
        Status = e.FStatus,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    private static CustomerAccountDto MapAccountToDto(CrmCustomerAccount e) => new()
    {
        Id = e.FID,
        CustomerId = e.FCustomerId,
        BrandCode = e.FBrandCode,
        Balance = e.FBalance,
        TotalRecharge = e.FTotalRecharge,
        TotalConsumption = e.FTotalConsumption,
        FrozenAmount = e.FFrozenAmount,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    private static PrepaymentDto MapPrepaymentToDto(CrmPrepayment e) => new()
    {
        Id = e.FID,
        CustomerId = e.FCustomerId,
        CustomerAccountId = e.FCustomerAccountId,
        OrgId = e.FOrgId,
        BrandCode = e.FBrandCode,
        PrepayAmount = e.FPrepayAmount,
        ReceivedAmount = e.FReceivedAmount,
        ExpectedWaybillCount = e.FExpectedWaybillCount,
        AllocatedWaybillCount = e.FAllocatedWaybillCount,
        Status = e.FStatus,
        BankTransactionId = e.FBankTransactionId,
        Remark = e.FRemark,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    private static WaybillAllocationDto MapAllocationToDto(CrmWaybillAllocation e) => new()
    {
        Id = e.FID,
        PrepaymentId = e.FPrepaymentId,
        CustomerId = e.FCustomerId,
        PoolId = e.FPoolId,
        StartNo = e.FStartNo,
        EndNo = e.FEndNo,
        AllocatedCount = e.FAllocatedCount,
        AllocationDate = e.FAllocationDate,
        OperatorId = e.FOperatorId,
        Status = e.FStatus,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    #endregion
}
