using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class TreasuryPlanService : ITreasuryPlanService
{
    private readonly IRepository<FinTreasuryAccountBinding> _bindingRepository;
    private readonly IRepository<FinTreasuryPlanLine> _planLineRepository;
    private readonly IRepository<FinAccountBalance> _accountBalanceRepository;
    private readonly IRepository<FinBankTransaction> _bankTransactionRepository;

    public TreasuryPlanService(
        IRepository<FinTreasuryAccountBinding> bindingRepository,
        IRepository<FinTreasuryPlanLine> planLineRepository,
        IRepository<FinAccountBalance> accountBalanceRepository,
        IRepository<FinBankTransaction> bankTransactionRepository)
    {
        _bindingRepository = bindingRepository;
        _planLineRepository = planLineRepository;
        _accountBalanceRepository = accountBalanceRepository;
        _bankTransactionRepository = bankTransactionRepository;
    }

    public async Task<List<TreasuryAccountBindingDto>> GetBindingsAsync(long accountSetId, long? orgId)
    {
        if (accountSetId <= 0) return new List<TreasuryAccountBindingDto>();

        var query = _bindingRepository.Query()
            .Where(b => b.FAccountSetId == accountSetId);

        if (orgId.HasValue)
        {
            query = query.Where(b => b.FOrgId == orgId.Value || b.FOrgId == null);
        }

        var bindings = await query
            .OrderBy(b => b.FOrgId.HasValue ? 0 : 1)
            .ThenBy(b => b.FChannelId)
            .ThenBy(b => b.FCashAccountId)
            .ToListAsync();

        return bindings.Select(MapBinding).ToList();
    }

    public async Task<TreasuryAccountBindingDto> SaveBindingAsync(TreasuryAccountBindingDto dto)
    {
        NormalizeAndValidateBinding(dto);

        FinTreasuryAccountBinding? entity = null;
        if (dto.Id > 0)
        {
            entity = await _bindingRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(b => b.FID == dto.Id);
        }

        if (entity == null)
        {
            entity = await _bindingRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(b =>
                    b.FAccountSetId == dto.AccountSetId &&
                    b.FOrgId == dto.OrgId &&
                    b.FChannelId == dto.ChannelId &&
                    b.FCashAccountId == dto.CashAccountId &&
                    b.FAccountNo == dto.AccountNo);
        }

        var now = DateTime.Now;
        if (entity == null)
        {
            entity = new FinTreasuryAccountBinding
            {
                FCreatedTime = now
            };
            ApplyBinding(entity, dto, now);
            await _bindingRepository.AddAsync(entity);
        }
        else
        {
            ApplyBinding(entity, dto, now);
            await _bindingRepository.UpdateAsync(entity);
        }

        return MapBinding(entity);
    }

    public async Task<List<TreasuryPlanLineDto>> GetPlanLinesAsync(long accountSetId, DateTime startDate, DateTime endDate, long? orgId)
    {
        if (accountSetId <= 0) return new List<TreasuryPlanLineDto>();

        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
        {
            throw new InvalidOperationException("结束日期不能早于开始日期");
        }

        var query = _planLineRepository.Query()
            .Where(l => l.FAccountSetId == accountSetId && l.FPlanDate >= start && l.FPlanDate <= end);

        if (orgId.HasValue)
        {
            query = query.Where(l => l.FOrgId == orgId.Value);
        }

        var lines = await query
            .OrderBy(l => l.FPlanDate)
            .ThenBy(l => l.FDirection)
            .ThenBy(l => l.FCashCategory)
            .ToListAsync();

        return lines.Select(MapPlanLine).ToList();
    }

    public async Task<TreasuryPlanLineDto> SavePlanLineAsync(TreasuryPlanLineDto dto)
    {
        NormalizeAndValidatePlanLine(dto);

        FinTreasuryPlanLine? entity = null;
        if (dto.Id > 0)
        {
            entity = await _planLineRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(l => l.FID == dto.Id);
        }

        var now = DateTime.Now;
        if (entity == null)
        {
            entity = new FinTreasuryPlanLine
            {
                FCreatedTime = now
            };
            ApplyPlanLine(entity, dto, now);
            await _planLineRepository.AddAsync(entity);
        }
        else
        {
            ApplyPlanLine(entity, dto, now);
            await _planLineRepository.UpdateAsync(entity);
        }

        return MapPlanLine(entity);
    }

    public async Task DeletePlanLineAsync(long id)
    {
        var exists = await _planLineRepository.Query()
            .AnyAsync(l => l.FID == id);

        if (!exists)
        {
            throw new InvalidOperationException("资金计划明细不存在");
        }

        await _planLineRepository.DeleteAsync(id);
    }

    public async Task<Rolling13WeekTreasuryDto> GetRolling13WeeksAsync(long accountSetId, DateTime startDate, long? orgId, decimal safetyCash)
    {
        if (accountSetId <= 0)
        {
            throw new InvalidOperationException("账套ID不能为空");
        }

        var start = startDate.Date;
        var end = start.AddDays(13 * 7 - 1);
        var openingCash = await CalculateOpeningCashAsync(accountSetId, orgId);
        var lines = await _planLineRepository.Query()
            .Where(l => l.FAccountSetId == accountSetId && l.FPlanDate >= start && l.FPlanDate <= end)
            .Where(l => !orgId.HasValue || l.FOrgId == orgId.Value)
            .ToListAsync();

        var weeks = new List<TreasuryWeekDto>();
        var currentOpening = openingCash;
        for (var weekIndex = 0; weekIndex < 13; weekIndex++)
        {
            var weekStart = start.AddDays(weekIndex * 7);
            var weekEnd = weekStart.AddDays(6);
            var inflow = lines
                .Where(x => x.FDirection == "inflow" && x.FPlanDate >= weekStart && x.FPlanDate <= weekEnd)
                .Sum(x => x.FAmount * x.FProbability / 100m);
            var outflow = lines
                .Where(x => x.FDirection == "outflow" && x.FPlanDate >= weekStart && x.FPlanDate <= weekEnd)
                .Sum(x => x.FAmount * x.FProbability / 100m);
            var endingCash = currentOpening + inflow - outflow;

            weeks.Add(new TreasuryWeekDto
            {
                WeekStartDate = weekStart,
                OpeningCash = currentOpening,
                Inflow = inflow,
                Outflow = outflow,
                EndingCash = endingCash,
                BelowSafetyCash = endingCash < safetyCash
            });

            currentOpening = endingCash;
        }

        return new Rolling13WeekTreasuryDto
        {
            OpeningCash = openingCash,
            SafetyCash = safetyCash,
            Weeks = weeks
        };
    }

    private async Task<decimal> CalculateOpeningCashAsync(long accountSetId, long? orgId)
    {
        var activeBindings = await _bindingRepository.Query()
            .Where(b => b.FAccountSetId == accountSetId && b.FStatus == 1)
            .Where(b => !orgId.HasValue || b.FOrgId == orgId.Value || b.FOrgId == null)
            .ToListAsync();

        if (activeBindings.Count == 0)
        {
            return 0m;
        }

        var openingCash = activeBindings
            .Where(b => b.FOpeningSource == "manual")
            .Sum(b => b.FManualOpeningAmount ?? 0m);

        foreach (var binding in activeBindings.Where(b => b.FOpeningSource == "account_balance" && b.FCashAccountId.HasValue))
        {
            var balanceQuery = _accountBalanceRepository.Query()
                .Where(b => b.FAccountSetId == accountSetId && b.FAccountId == binding.FCashAccountId!.Value);


            var latestBalance = await balanceQuery
                .OrderByDescending(b => b.FPeriodId)
                .ThenByDescending(b => b.FID)
                .FirstOrDefaultAsync();

            if (latestBalance != null)
            {
                openingCash += latestBalance.FEndDebit - latestBalance.FEndCredit;
            }
        }

        foreach (var binding in activeBindings.Where(b => b.FOpeningSource == "bank_transaction_balance" && b.FChannelId.HasValue))
        {
            var latestTransaction = await _bankTransactionRepository.Query()
                .Where(t => t.FChannelId == binding.FChannelId!.Value && t.FBalance.HasValue)
                .OrderByDescending(t => t.FTransactionDate)
                .ThenByDescending(t => t.FID)
                .FirstOrDefaultAsync();

            openingCash += latestTransaction?.FBalance ?? 0m;
        }

        return openingCash;
    }

    private static void NormalizeAndValidateBinding(TreasuryAccountBindingDto dto)
    {
        if (dto.AccountSetId <= 0)
        {
            throw new InvalidOperationException("账套ID不能为空");
        }

        dto.AccountNo = NormalizeNullable(dto.AccountNo);
        dto.OpeningSource = string.IsNullOrWhiteSpace(dto.OpeningSource)
            ? "account_balance"
            : dto.OpeningSource.Trim();
        dto.Remark = NormalizeNullable(dto.Remark);

        if (dto.ChannelId.GetValueOrDefault() <= 0 &&
            dto.CashAccountId.GetValueOrDefault() <= 0 &&
            string.IsNullOrWhiteSpace(dto.AccountNo))
        {
            throw new InvalidOperationException("资金账户绑定需配置渠道、现金科目或账号");
        }

        if (dto.OpeningSource != "manual" &&
            dto.OpeningSource != "account_balance" &&
            dto.OpeningSource != "bank_transaction_balance")
        {
            throw new InvalidOperationException("期初来源不支持");
        }

        if (dto.OpeningSource == "manual" && !dto.ManualOpeningAmount.HasValue)
        {
            dto.ManualOpeningAmount = 0m;
        }
    }

    private static void NormalizeAndValidatePlanLine(TreasuryPlanLineDto dto)
    {
        if (dto.AccountSetId <= 0)
        {
            throw new InvalidOperationException("账套ID不能为空");
        }

        if (dto.OrgId.GetValueOrDefault() <= 0)
        {
            throw new InvalidOperationException("资金计划组织不能为空");
        }

        if (dto.PlanDate == default)
        {
            throw new InvalidOperationException("计划日期不能为空");
        }

        dto.PlanDate = dto.PlanDate.Date;
        dto.WeekStartDate = dto.WeekStartDate == default
            ? GetWeekStartDate(dto.PlanDate)
            : dto.WeekStartDate.Date;
        dto.Direction = string.IsNullOrWhiteSpace(dto.Direction) ? "outflow" : dto.Direction.Trim();
        dto.CashCategory = string.IsNullOrWhiteSpace(dto.CashCategory) ? "other" : dto.CashCategory.Trim();
        dto.SourceType = string.IsNullOrWhiteSpace(dto.SourceType) ? "manual" : dto.SourceType.Trim();
        dto.CounterpartyName = NormalizeNullable(dto.CounterpartyName);
        dto.Remark = NormalizeNullable(dto.Remark);

        if (dto.Direction != "inflow" && dto.Direction != "outflow")
        {
            throw new InvalidOperationException("资金方向只能是流入或流出");
        }

        if (dto.Amount < 0)
        {
            throw new InvalidOperationException("资金计划金额不能为负数");
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            throw new InvalidOperationException("资金计划概率必须在0到100之间");
        }
    }

    private static DateTime GetWeekStartDate(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var offset = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.Date.AddDays(offset);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ApplyBinding(FinTreasuryAccountBinding entity, TreasuryAccountBindingDto dto, DateTime now)
    {
        entity.FAccountSetId = dto.AccountSetId;
        entity.FOrgId = dto.OrgId;
        entity.FChannelId = dto.ChannelId;
        entity.FCashAccountId = dto.CashAccountId;
        entity.FAccountNo = dto.AccountNo;
        entity.FOpeningSource = dto.OpeningSource;
        entity.FManualOpeningAmount = dto.ManualOpeningAmount;
        entity.FStatus = dto.Status;
        entity.FRemark = dto.Remark;
        entity.FUpdatedTime = now;
    }

    private static void ApplyPlanLine(FinTreasuryPlanLine entity, TreasuryPlanLineDto dto, DateTime now)
    {
        entity.FAccountSetId = dto.AccountSetId;
        entity.FOrgId = dto.OrgId!.Value;
        entity.FPlanDate = dto.PlanDate;
        entity.FWeekStartDate = dto.WeekStartDate;
        entity.FDirection = dto.Direction;
        entity.FCashCategory = dto.CashCategory;
        entity.FAmount = dto.Amount;
        entity.FProbability = dto.Probability;
        entity.FSourceType = dto.SourceType;
        entity.FSourceId = dto.SourceId;
        entity.FCounterpartyName = dto.CounterpartyName;
        entity.FRemark = dto.Remark;
        entity.FUpdatedTime = now;
    }

    private static TreasuryAccountBindingDto MapBinding(FinTreasuryAccountBinding entity)
    {
        return new TreasuryAccountBindingDto
        {
            Id = entity.FID,
            AccountSetId = entity.FAccountSetId,
            OrgId = entity.FOrgId,
            ChannelId = entity.FChannelId,
            CashAccountId = entity.FCashAccountId,
            AccountNo = entity.FAccountNo,
            OpeningSource = entity.FOpeningSource,
            ManualOpeningAmount = entity.FManualOpeningAmount,
            Status = entity.FStatus,
            Remark = entity.FRemark
        };
    }

    private static TreasuryPlanLineDto MapPlanLine(FinTreasuryPlanLine entity)
    {
        return new TreasuryPlanLineDto
        {
            Id = entity.FID,
            AccountSetId = entity.FAccountSetId,
            OrgId = entity.FOrgId,
            PlanDate = entity.FPlanDate,
            WeekStartDate = entity.FWeekStartDate,
            Direction = entity.FDirection,
            CashCategory = entity.FCashCategory,
            Amount = entity.FAmount,
            Probability = entity.FProbability,
            SourceType = entity.FSourceType,
            SourceId = entity.FSourceId,
            CounterpartyName = entity.FCounterpartyName,
            Remark = entity.FRemark
        };
    }
}
