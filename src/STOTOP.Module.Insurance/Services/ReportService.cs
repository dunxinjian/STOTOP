using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class ReportService : IReportService
{
    private readonly IRepository<InsPolicy> _policyRepo;
    private readonly IRepository<InsClaim> _claimRepo;
    private readonly IRepository<InsSettlement> _settlementRepo;
    private readonly IRepository<InsCoInsuranceFund> _fundRepo;
    private readonly IRepository<InsFundContribution> _contributionRepo;

    public ReportService(
        IRepository<InsPolicy> policyRepo,
        IRepository<InsClaim> claimRepo,
        IRepository<InsSettlement> settlementRepo,
        IRepository<InsCoInsuranceFund> fundRepo,
        IRepository<InsFundContribution> contributionRepo)
    {
        _policyRepo = policyRepo;
        _claimRepo = claimRepo;
        _settlementRepo = settlementRepo;
        _fundRepo = fundRepo;
        _contributionRepo = contributionRepo;
    }

    /// <summary>
    /// 保费汇总：按业务类型/保险大类/时段统计
    /// </summary>
    public async Task<List<PremiumSummaryItem>> GetPremiumSummaryAsync(
        long orgId, int? businessType, int? insuranceCategory, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _policyRepo.Query()
            .Where(p => p.FOrgId == orgId);

        if (businessType.HasValue)
            query = query.Where(p => p.FBusinessType == businessType.Value);
        if (insuranceCategory.HasValue)
            query = query.Where(p => p.FInsuranceCategory == insuranceCategory.Value);
        if (startDate.HasValue)
            query = query.Where(p => p.FCreatedTime >= startDate.Value.ToDateTime(TimeOnly.MinValue));
        if (endDate.HasValue)
            query = query.Where(p => p.FCreatedTime <= endDate.Value.ToDateTime(TimeOnly.MaxValue));

        var result = await query
            .GroupBy(p => new { p.FBusinessType, p.FInsuranceCategory, Period = p.FCreatedTime.ToString("yyyy-MM") })
            .Select(g => new PremiumSummaryItem
            {
                BusinessType = g.Key.FBusinessType,
                InsuranceCategory = g.Key.FInsuranceCategory,
                Period = g.Key.Period,
                PolicyCount = g.Count(),
                TotalPremium = g.Sum(p => p.FPremium ?? 0),
                TotalInsuredAmount = g.Sum(p => p.FInsuredAmount ?? 0)
            })
            .OrderBy(x => x.Period)
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 出险分析：按事故类型/月份/业务类型统计
    /// </summary>
    public async Task<List<ClaimAnalysisItem>> GetClaimAnalysisAsync(
        long orgId, int? accidentType, int? businessType, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _claimRepo.Query()
            .Where(c => c.FOrgId == orgId);

        if (accidentType.HasValue)
            query = query.Where(c => c.FAccidentType == accidentType.Value);
        if (businessType.HasValue)
            query = query.Where(c => c.FBusinessType == businessType.Value);
        if (startDate.HasValue)
            query = query.Where(c => c.FClaimDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(c => c.FClaimDate <= endDate.Value);

        var result = await query
            .GroupBy(c => new { c.FAccidentType, c.FBusinessType, Period = c.FClaimDate.ToString("yyyy-MM") })
            .Select(g => new ClaimAnalysisItem
            {
                AccidentType = g.Key.FAccidentType,
                BusinessType = g.Key.FBusinessType,
                Period = g.Key.Period,
                ClaimCount = g.Count(),
                TotalEstimatedLoss = g.Sum(c => c.FEstimatedLoss ?? 0),
                TotalActualLoss = g.Sum(c => c.FActualLoss ?? 0)
            })
            .OrderBy(x => x.Period)
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 赔付分析：按理赔类型/月份/状态统计
    /// </summary>
    public async Task<List<SettlementAnalysisItem>> GetSettlementAnalysisAsync(
        long orgId, int? settlementType, int? settlementStatus, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _settlementRepo.Query()
            .Where(s => s.FOrgId == orgId);

        if (settlementType.HasValue)
            query = query.Where(s => s.FSettlementType == settlementType.Value);
        if (settlementStatus.HasValue)
            query = query.Where(s => s.FSettlementStatus == settlementStatus.Value);
        if (startDate.HasValue)
            query = query.Where(s => s.FApplyDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(s => s.FApplyDate <= endDate.Value);

        var result = await query
            .GroupBy(s => new { s.FSettlementType, s.FSettlementStatus, Period = s.FApplyDate.ToString("yyyy-MM") })
            .Select(g => new SettlementAnalysisItem
            {
                SettlementType = g.Key.FSettlementType,
                SettlementStatus = g.Key.FSettlementStatus,
                Period = g.Key.Period,
                SettlementCount = g.Count(),
                TotalAssessedAmount = g.Sum(s => s.FAssessedAmount ?? 0),
                TotalSettlementAmount = g.Sum(s => s.FSettlementAmount ?? 0)
            })
            .OrderBy(x => x.Period)
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 基金收支：按基金/月份统计
    /// </summary>
    public async Task<List<FundBalanceItem>> GetFundBalanceAsync(
        long orgId, long? fundId, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _contributionRepo.Query()
            .Include(c => c.Fund)
            .Where(c => c.FOrgId == orgId && c.FPaymentStatus == 2); // 已缴费的

        if (fundId.HasValue)
            query = query.Where(c => c.FFundId == fundId.Value);
        if (startDate.HasValue)
            query = query.Where(c => c.FPaymentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(c => c.FPaymentDate <= endDate.Value);

        var contributions = await query
            .GroupBy(c => new { c.FFundId, FundName = c.Fund.FFundName, Period = c.FPaymentDate!.Value.ToString("yyyy-MM") })
            .Select(g => new FundBalanceItem
            {
                FundId = g.Key.FFundId,
                FundName = g.Key.FundName,
                Period = g.Key.Period,
                Contributions = g.Sum(c => c.FContributionAmount),
                Payouts = 0,
                Balance = 0
            })
            .OrderBy(x => x.Period)
            .ToListAsync();

        // 补充基金余额信息
        var fundIds = contributions.Select(c => c.FundId).Distinct().ToList();
        var funds = await _fundRepo.Query()
            .Where(f => fundIds.Contains(f.FID))
            .ToListAsync();

        foreach (var item in contributions)
        {
            var fund = funds.FirstOrDefault(f => f.FID == item.FundId);
            if (fund != null)
            {
                item.Balance = fund.FFundBalance;
                item.Payouts = fund.FTotalPayouts;
            }
        }

        return contributions;
    }

    /// <summary>
    /// 综合看板
    /// </summary>
    public async Task<InsuranceOverviewDto> GetOverviewAsync(long orgId)
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        var thirtyDaysLater = now.AddDays(30);

        var activePolicyCount = await _policyRepo.Query()
            .Where(p => p.FOrgId == orgId && p.FInsuranceStatus == 1)
            .CountAsync();

        var expiringPolicyCount = await _policyRepo.Query()
            .Where(p => p.FOrgId == orgId && p.FInsuranceStatus == 1
                     && p.FExpiryDate >= now && p.FExpiryDate <= thirtyDaysLater)
            .CountAsync();

        var ongoingClaimCount = await _claimRepo.Query()
            .Where(c => c.FOrgId == orgId && (c.FClaimStatus == 1 || c.FClaimStatus == 2 || c.FClaimStatus == 3))
            .CountAsync();

        var pendingSettlementCount = await _settlementRepo.Query()
            .Where(s => s.FOrgId == orgId && s.FSettlementStatus == 10)
            .CountAsync();

        var totalFundBalance = await _fundRepo.Query()
            .Where(f => f.FOrgId == orgId && f.FFundStatus == 1)
            .SumAsync(f => f.FFundBalance);

        var totalPremium = await _policyRepo.Query()
            .Where(p => p.FOrgId == orgId)
            .SumAsync(p => p.FPremium ?? 0);

        var totalPayout = await _settlementRepo.Query()
            .Where(s => s.FOrgId == orgId && (s.FSettlementStatus == 20 || s.FSettlementStatus == 30))
            .SumAsync(s => s.FSettlementAmount ?? 0);

        return new InsuranceOverviewDto
        {
            ActivePolicyCount = activePolicyCount,
            ExpiringPolicyCount = expiringPolicyCount,
            OngoingClaimCount = ongoingClaimCount,
            PendingSettlementCount = pendingSettlementCount,
            TotalFundBalance = totalFundBalance,
            TotalPremium = totalPremium,
            TotalPayout = totalPayout
        };
    }
}
