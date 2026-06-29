using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class WaybillNumberService : IWaybillNumberService
{
    private readonly IRepository<ExpWaybillNumberPool> _poolRepo;
    private readonly IRepository<ExpWaybillNumberTransaction> _transactionRepo;
    private readonly IRepository<ExpClientWaybillBalance> _balanceRepo;
    private readonly STOTOPDbContext _context;

    public WaybillNumberService(
        IRepository<ExpWaybillNumberPool> poolRepo,
        IRepository<ExpWaybillNumberTransaction> transactionRepo,
        IRepository<ExpClientWaybillBalance> balanceRepo,
        STOTOPDbContext context)
    {
        _poolRepo = poolRepo;
        _transactionRepo = transactionRepo;
        _balanceRepo = balanceRepo;
        _context = context;
    }

    public async Task<List<WaybillNumberPoolDto>> GetPoolListAsync()
    {
        var pools = await _poolRepo.Query()
            .OrderByDescending(p => p.FCreatedTime)
            .ToListAsync();

        return pools.Select(MapPoolToDto).ToList();
    }

    public async Task<WaybillNumberPoolDto> CreatePoolAsync(CreatePoolRequest request)
    {
        var entity = new ExpWaybillNumberPool
        {
            FBrandCode = request.BrandCode,
            FPrefix = request.Prefix,
            FStartNo = request.StartNo,
            FEndNo = request.EndNo,
            FTotalCount = request.TotalCount,
            FAllocated = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _poolRepo.AddAsync(entity);
        return MapPoolToDto(result);
    }

    public async Task<WaybillNumberTransactionDto> AllocateAsync(long poolId, string clientId, string brandCode, int quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("分配数量必须大于0");

        // 使用 SQL 原子更新，防止并发超发
        var affected = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE [dbo].[EXP运单号段] 
              SET [F已分配] = [F已分配] + {0}, [F更新时间] = GETDATE()
              WHERE [FID] = {1} AND ([F总数量] - [F已分配]) >= {0}",
            quantity, poolId);

        if (affected == 0)
            throw new InvalidOperationException("号段池剩余数量不足或已被其他请求占用");

        // 创建交易记录
        var transaction = await _transactionRepo.AddAsync(new ExpWaybillNumberTransaction
        {
            FBusinessObjectId = clientId,
            FBrandCode = brandCode,
            FPoolId = poolId,
            FTransactionType = 1, // 分配
            FQuantity = quantity,
            FTransactionDate = DateTime.Today,
            FCreatedTime = DateTime.Now
        });

        // 更新客户余额
        var balance = await _balanceRepo.Query()
            .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId && b.FBrandCode == brandCode);

        if (balance == null)
        {
            await _balanceRepo.AddAsync(new ExpClientWaybillBalance
            {
                FBusinessObjectId = clientId,
                FBrandCode = brandCode,
                FAvailable = quantity,
                FUsed = 0,
                FTotalAllocated = quantity,
                FTotalReturned = 0,
                FUpdatedTime = DateTime.Now
            });
        }
        else
        {
            balance.FAvailable += quantity;
            balance.FTotalAllocated += quantity;
            balance.FUpdatedTime = DateTime.Now;
            await _balanceRepo.UpdateAsync(balance);
        }

        return MapTransactionToDto(transaction);
    }

    public async Task<WaybillNumberTransactionDto> ReturnAsync(string clientId, string brandCode, int quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("回收数量必须大于0");

        var balance = await _balanceRepo.Query()
            .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId && b.FBrandCode == brandCode);

        if (balance == null)
            throw new InvalidOperationException("客户无运单号余额");
        if (balance.FAvailable < quantity)
            throw new InvalidOperationException($"可用数量不足，当前可用: {balance.FAvailable}，需回收: {quantity}");

        balance.FAvailable -= quantity;
        balance.FTotalReturned += quantity;
        balance.FUpdatedTime = DateTime.Now;
        await _balanceRepo.UpdateAsync(balance);

        var transaction = await _transactionRepo.AddAsync(new ExpWaybillNumberTransaction
        {
            FBusinessObjectId = clientId,
            FBrandCode = brandCode,
            FTransactionType = 2, // 回收
            FQuantity = quantity,
            FTransactionDate = DateTime.Today,
            FCreatedTime = DateTime.Now
        });

        return MapTransactionToDto(transaction);
    }

    public async Task<ClientWaybillBalanceDto?> GetClientBalanceAsync(string clientId, string brandCode)
    {
        var balance = await _balanceRepo.Query()
            .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId && b.FBrandCode == brandCode);

        if (balance == null) return null;

        return new ClientWaybillBalanceDto
        {
            Id = balance.FID,
            ClientId = balance.FBusinessObjectId,
            BrandCode = balance.FBrandCode,
            Available = balance.FAvailable,
            Used = balance.FUsed,
            TotalAllocated = balance.FTotalAllocated,
            TotalReturned = balance.FTotalReturned,
            UpdatedTime = balance.FUpdatedTime
        };
    }

    private static WaybillNumberPoolDto MapPoolToDto(ExpWaybillNumberPool e) => new()
    {
        Id = e.FID,
        BrandCode = e.FBrandCode,
        Prefix = e.FPrefix,
        StartNo = e.FStartNo,
        EndNo = e.FEndNo,
        TotalCount = e.FTotalCount,
        Allocated = e.FAllocated,
        Remaining = e.FRemaining,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };

    private static WaybillNumberTransactionDto MapTransactionToDto(ExpWaybillNumberTransaction e) => new()
    {
        Id = e.FID,
        ClientId = e.FBusinessObjectId,
        BrandCode = e.FBrandCode,
        PoolId = e.FPoolId,
        TransactionType = e.FTransactionType,
        Quantity = e.FQuantity,
        StartNo = e.FStartNo,
        EndNo = e.FEndNo,
        TransactionDate = e.FTransactionDate,
        CreatedTime = e.FCreatedTime
    };
}
