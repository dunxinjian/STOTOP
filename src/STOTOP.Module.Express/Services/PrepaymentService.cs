using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class PrepaymentService : IPrepaymentService
{
    private readonly IRepository<ExpPrepayment> _prepaymentRepo;
    private readonly IRepository<ExpPrepaymentBalance> _balanceRepo;
    private readonly IRepository<ExpPrepaymentTransaction> _transactionRepo;
    private readonly STOTOPDbContext _context;

    public PrepaymentService(
        IRepository<ExpPrepayment> prepaymentRepo,
        IRepository<ExpPrepaymentBalance> balanceRepo,
        IRepository<ExpPrepaymentTransaction> transactionRepo,
        STOTOPDbContext context)
    {
        _prepaymentRepo = prepaymentRepo;
        _balanceRepo = balanceRepo;
        _transactionRepo = transactionRepo;
        _context = context;
    }

    public async Task<PrepaymentDto> RechargeAsync(string clientId, decimal amount, DateTime? paymentDate, string? paymentMethod, string? remark)
    {
        if (amount <= 0) throw new InvalidOperationException("充值金额必须大于0");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. 创建预付款记录
            var prepayment = await _prepaymentRepo.AddAsync(new ExpPrepayment
            {
                FBusinessObjectId = clientId,
                FAmount = amount,
                FPaymentDate = paymentDate ?? DateTime.Today,
                FPaymentMethod = paymentMethod,
                FRemark = remark,
                FCreatedTime = DateTime.Now
            });

            // 2. 更新余额（使用 AsTracking 确保实体被跟踪，全局 NoTracking 环境）
            var balance = await _balanceRepo.Query().AsTracking()
                .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId);

            if (balance == null)
            {
                balance = await _balanceRepo.AddAsync(new ExpPrepaymentBalance
                {
                    FBusinessObjectId = clientId,
                    FBalance = amount,
                    FTotalRecharge = amount,
                    FTotalConsume = 0,
                    FUpdatedTime = DateTime.Now
                });
            }
            else
            {
                balance.FBalance += amount;
                balance.FTotalRecharge += amount;
                balance.FUpdatedTime = DateTime.Now;
                await _balanceRepo.UpdateAsync(balance);
            }

            // 3. 创建流水
            await _transactionRepo.AddAsync(new ExpPrepaymentTransaction
            {
                FBusinessObjectId = clientId,
                FTransactionType = 1, // 充值
                FAmount = amount,
                FBalanceAfter = balance.FBalance,
                FRemark = remark,
                FCreatedTime = DateTime.Now
            });

            await transaction.CommitAsync();

            return new PrepaymentDto
            {
                Id = prepayment.FID,
                ClientId = prepayment.FBusinessObjectId,
                Amount = prepayment.FAmount,
                PaymentDate = prepayment.FPaymentDate,
                PaymentMethod = prepayment.FPaymentMethod,
                Remark = prepayment.FRemark,
                CreatedTime = prepayment.FCreatedTime
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("充值操作发生并发冲突，请重试");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PrepaymentBalanceDto?> GetBalanceAsync(string clientId)
    {
        var balance = await _balanceRepo.Query()
            .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId);

        if (balance == null) return null;

        return new PrepaymentBalanceDto
        {
            Id = balance.FID,
            ClientId = balance.FBusinessObjectId,
            Balance = balance.FBalance,
            TotalRecharge = balance.FTotalRecharge,
            TotalConsume = balance.FTotalConsume,
            UpdatedTime = balance.FUpdatedTime
        };
    }

    public async Task<PagedResult<PrepaymentTransactionDto>> GetTransactionsAsync(TransactionQueryRequest request)
    {
        var query = _transactionRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(t => t.FBusinessObjectId == request.ClientId);
        if (request.TransactionType.HasValue)
            query = query.Where(t => t.FTransactionType == request.TransactionType.Value);
        if (request.StartDate.HasValue)
            query = query.Where(t => t.FCreatedTime >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(t => t.FCreatedTime <= request.EndDate.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<PrepaymentTransactionDto>
        {
            Items = items.Select(t => new PrepaymentTransactionDto
            {
                Id = t.FID,
                ClientId = t.FBusinessObjectId,
                TransactionType = t.FTransactionType,
                Amount = t.FAmount,
                InvoiceId = t.FInvoiceId,
                BalanceAfter = t.FBalanceAfter,
                Remark = t.FRemark,
                CreatedTime = t.FCreatedTime
            }).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task WriteOffAsync(string clientId, long invoiceId, decimal totalAmount, int paymentMode, decimal? prepayRatio, decimal? prepayPerTicket, int totalWaybills)
    {
        // PaymentMode: 1预付 2后付 3混合
        if (paymentMode == 2) return; // 后付不核销

        decimal deductAmount;
        if (paymentMode == 1)
        {
            // 全额扣除
            deductAmount = totalAmount;
        }
        else // paymentMode == 3 混合
        {
            if (prepayRatio.HasValue && prepayRatio.Value > 0)
            {
                deductAmount = totalAmount * prepayRatio.Value / 100m;
            }
            else if (prepayPerTicket.HasValue && prepayPerTicket.Value > 0)
            {
                deductAmount = prepayPerTicket.Value * totalWaybills;
            }
            else
            {
                return; // 无法计算预付金额
            }
        }

        if (deductAmount <= 0) return;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 使用 AsTracking 确保实体被跟踪（全局 NoTracking 环境）
            var balance = await _balanceRepo.Query().AsTracking()
                .FirstOrDefaultAsync(b => b.FBusinessObjectId == clientId);

            if (balance == null)
                throw new InvalidOperationException("客户无预付款余额");

            if (balance.FBalance < deductAmount)
                throw new InvalidOperationException($"预付款余额不足，当前余额: {balance.FBalance}，需核销: {deductAmount}");

            balance.FBalance -= deductAmount;
            balance.FTotalConsume += deductAmount;
            balance.FUpdatedTime = DateTime.Now;
            await _balanceRepo.UpdateAsync(balance);

            await _transactionRepo.AddAsync(new ExpPrepaymentTransaction
            {
                FBusinessObjectId = clientId,
                FTransactionType = 2, // 核销
                FAmount = deductAmount,
                FInvoiceId = invoiceId,
                FBalanceAfter = balance.FBalance,
                FRemark = $"账单核销，账单ID: {invoiceId}",
                FCreatedTime = DateTime.Now
            });

            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("预付款核销发生并发冲突，请重试");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
