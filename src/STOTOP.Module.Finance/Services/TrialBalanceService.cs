using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class TrialBalanceService : ITrialBalanceService
{
    private readonly IRepository<FinTrialBalance> _trialBalanceRepository;
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly IRepository<FinAccount> _accountRepository;

    public TrialBalanceService(
        IRepository<FinTrialBalance> trialBalanceRepository,
        IRepository<FinAccountBalance> balanceRepository,
        IRepository<FinAccount> accountRepository)
    {
        _trialBalanceRepository = trialBalanceRepository;
        _balanceRepository = balanceRepository;
        _accountRepository = accountRepository;
    }

    public async Task<TrialBalanceDto> GenerateTrialBalanceAsync(long periodId, long accountSetId)
    {
        // 查询该期间所有科目余额
        var balances = await _balanceRepository.Query()
            .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId)
            .ToListAsync();

        // 查询科目信息
        var accountIds = balances.Select(b => b.FAccountId).Distinct().ToList();
        var accounts = await _accountRepository.Query()
            .Where(a => accountIds.Contains(a.FID))
            .ToListAsync();

        var details = new List<TrialBalanceDetailDto>();
        decimal totalDebit = 0;
        decimal totalCredit = 0;

        foreach (var balance in balances)
        {
            var account = accounts.FirstOrDefault(a => a.FID == balance.FAccountId);
            if (account == null) continue;

            var debitBalance = balance.FEndDebit;
            var creditBalance = balance.FEndCredit;

            details.Add(new TrialBalanceDetailDto
            {
                AccountId = balance.FAccountId,
                AccountCode = account.FCode,
                AccountName = account.FName,
                DebitBalance = debitBalance,
                CreditBalance = creditBalance
            });

            totalDebit += debitBalance;
            totalCredit += creditBalance;
        }

        var isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m;

        // 序列化明细为 JSON
        var detailsJson = JsonSerializer.Serialize(details);

        // 写入数据库
        var trialBalance = new FinTrialBalance
        {
            FPeriodId = periodId,
            FAccountSetId = accountSetId,
            FTotalDebit = totalDebit,
            FTotalCredit = totalCredit,
            FIsBalanced = isBalanced,
            FDetails = detailsJson,
            FGeneratedTime = DateTime.Now,
            FOperatorId = 0
        };

        await _trialBalanceRepository.AddAsync(trialBalance);

        return new TrialBalanceDto
        {
            Id = trialBalance.FID,
            PeriodId = periodId,
            AccountSetId = accountSetId,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = isBalanced,
            Details = details.OrderBy(d => d.AccountCode).ToList(),
            GeneratedTime = trialBalance.FGeneratedTime
        };
    }

    public async Task<TrialBalanceDto?> GetLatestTrialBalanceAsync(long periodId, long accountSetId)
    {
        var trialBalance = await _trialBalanceRepository.Query()
            .Where(t => t.FPeriodId == periodId && t.FAccountSetId == accountSetId)
            .OrderByDescending(t => t.FGeneratedTime)
            .FirstOrDefaultAsync();

        if (trialBalance == null) return null;

        var details = new List<TrialBalanceDetailDto>();
        if (!string.IsNullOrEmpty(trialBalance.FDetails))
        {
            details = JsonSerializer.Deserialize<List<TrialBalanceDetailDto>>(trialBalance.FDetails) ?? new();
        }

        return new TrialBalanceDto
        {
            Id = trialBalance.FID,
            PeriodId = trialBalance.FPeriodId,
            AccountSetId = trialBalance.FAccountSetId,
            TotalDebit = trialBalance.FTotalDebit,
            TotalCredit = trialBalance.FTotalCredit,
            IsBalanced = trialBalance.FIsBalanced,
            Details = details.OrderBy(d => d.AccountCode).ToList(),
            GeneratedTime = trialBalance.FGeneratedTime
        };
    }
}
