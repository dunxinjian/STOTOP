using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class CoInsuranceFundService : ICoInsuranceFundService
{
    private readonly IRepository<InsCoInsuranceFund> _fundRepo;
    private readonly IRepository<InsFundContribution> _contributionRepo;
    private readonly IRepository<InsPolicy> _policyRepo;

    public CoInsuranceFundService(
        IRepository<InsCoInsuranceFund> fundRepo,
        IRepository<InsFundContribution> contributionRepo,
        IRepository<InsPolicy> policyRepo)
    {
        _fundRepo = fundRepo;
        _contributionRepo = contributionRepo;
        _policyRepo = policyRepo;
    }

    public async Task<PagedResult<InsCoInsuranceFundListItemDto>> GetListAsync(InsCoInsuranceFundQueryRequest request)
    {
        var query = _fundRepo.Query().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(f => f.FFundName.Contains(request.Keyword) || f.FFundCode.Contains(request.Keyword));
        if (request.BusinessType.HasValue)
            query = query.Where(f => f.FBusinessType == request.BusinessType.Value);
        if (request.FundStatus.HasValue)
            query = query.Where(f => f.FFundStatus == request.FundStatus.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsCoInsuranceFundListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InsCoInsuranceFundDto?> GetByIdAsync(long id)
    {
        var entity = await _fundRepo.Query()
            .FirstOrDefaultAsync(f => f.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InsCoInsuranceFundDto> CreateAsync(CreateInsCoInsuranceFundRequest request)
    {
        var entity = new InsCoInsuranceFund
        {
            FUID = Guid.NewGuid().ToString("N"),
            FFundName = request.FundName,
            FFundCode = request.FundCode,
            FBusinessType = request.BusinessType,
            FFundDescription = request.FundDescription,
            FContributionStandard = request.ContributionStandard,
            FPaymentCycle = request.PaymentCycle,
            FDeductible = request.Deductible,
            FSinglePayoutLimit = request.SinglePayoutLimit,
            FAnnualPayoutLimit = request.AnnualPayoutLimit,
            FEffectiveDate = request.EffectiveDate,
            FFundStatus = 1,
            FTotalContributions = 0,
            FTotalPayouts = 0,
            FFundBalance = 0,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _fundRepo.AddAsync(entity);
        return (await GetByIdAsync(entity.FID))!;
    }

    public async Task<InsCoInsuranceFundDto?> UpdateAsync(long id, UpdateInsCoInsuranceFundRequest request)
    {
        var entity = await _fundRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(f => f.FID == id);

        if (entity == null) return null;

        entity.FFundName = request.FundName;
        entity.FFundDescription = request.FundDescription;
        entity.FContributionStandard = request.ContributionStandard;
        entity.FPaymentCycle = request.PaymentCycle;
        entity.FDeductible = request.Deductible;
        entity.FSinglePayoutLimit = request.SinglePayoutLimit;
        entity.FAnnualPayoutLimit = request.AnnualPayoutLimit;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _fundRepo.UpdateAsync(entity);
        return await GetByIdAsync(id);
    }

    /// <summary>
    /// 批量生成缴费单：按基金下所有共保基金(FInsuranceCategory=2)且有效的保单生成指定周期的缴费记录
    /// </summary>
    public async Task<int> GenerateContributionsAsync(long fundId, DateOnly periodStart, DateOnly periodEnd)
    {
        var fund = await _fundRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(f => f.FID == fundId);

        if (fund == null)
            throw new InvalidOperationException("基金不存在");

        if (fund.FFundStatus != 1)
            throw new InvalidOperationException("基金当前状态不允许生成缴费单");

        // 查询基金下所有有效的共保基金保单 (FInsuranceCategory=2, FInsuranceStatus=1)
        var policies = await _policyRepo.Query()
            .Where(p => p.FCoInsuranceFundId == fundId
                     && p.FInsuranceCategory == 2
                     && p.FInsuranceStatus == 1
                     && p.FEffectiveDate <= periodEnd
                     && p.FExpiryDate >= periodStart)
            .ToListAsync();

        if (policies.Count == 0)
            return 0;

        var count = 0;
        foreach (var policy in policies)
        {
            // 检查是否已存在相同周期的缴费记录
            var exists = await _contributionRepo.Query()
                .AnyAsync(c => c.FFundId == fundId
                            && c.FPolicyId == policy.FID
                            && c.FPeriodStart == periodStart
                            && c.FPeriodEnd == periodEnd);

            if (exists) continue;

            var contribution = new InsFundContribution
            {
                FUID = Guid.NewGuid().ToString("N"),
                FOrgId = policy.FOrgId,
                FFundId = fundId,
                FPolicyId = policy.FID,
                FBusinessType = policy.FBusinessType,
                FRelatedObjectId = policy.FRelatedObjectId,
                FRelatedObjectName = policy.FRelatedObjectName,
                FContributionNumber = $"CTB{DateTime.Now:yyyyMMdd}{new Random().Next(100000, 999999)}",
                FContributionAmount = policy.FPerPeriodAmount ?? fund.FContributionStandard ?? 0,
                FPeriodStart = periodStart,
                FPeriodEnd = periodEnd,
                FPaymentStatus = 1, // 待缴
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _contributionRepo.AddAsync(contribution);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 确认缴费：更新缴费状态为已缴 + 更新基金累计缴费和余额
    /// </summary>
    public async Task ConfirmContributionAsync(long contributionId)
    {
        var contribution = await _contributionRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == contributionId);

        if (contribution == null)
            throw new InvalidOperationException("缴费记录不存在");

        if (contribution.FPaymentStatus != 1)
            throw new InvalidOperationException("只有待缴状态的记录才能确认缴费");

        contribution.FPaymentStatus = 2; // 已缴
        contribution.FPaymentDate = DateOnly.FromDateTime(DateTime.Now);
        contribution.FUpdatedTime = DateTime.Now;
        await _contributionRepo.UpdateAsync(contribution);

        // 更新基金的累计缴费和余额（原子 UPDATE，避免并发缴费/拨付的读-改-写丢失更新）
        var contributionAmount = contribution.FContributionAmount;
        await _fundRepo.Query()
            .Where(f => f.FID == contribution.FFundId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.FTotalContributions, f => f.FTotalContributions + contributionAmount)
                .SetProperty(f => f.FFundBalance, f => f.FFundBalance + contributionAmount)
                .SetProperty(f => f.FUpdatedTime, DateTime.Now));
    }

    public async Task<PagedResult<InsFundContributionListItemDto>> GetContributionsAsync(InsFundContributionQueryRequest request)
    {
        var query = _contributionRepo.Query()
            .Include(c => c.Fund)
            .AsQueryable();

        if (request.FundId.HasValue)
            query = query.Where(c => c.FFundId == request.FundId.Value);
        if (request.BusinessType.HasValue)
            query = query.Where(c => c.FBusinessType == request.BusinessType.Value);
        if (request.RelatedObjectId.HasValue)
            query = query.Where(c => c.FRelatedObjectId == request.RelatedObjectId.Value);
        if (request.PaymentStatus.HasValue)
            query = query.Where(c => c.FPaymentStatus == request.PaymentStatus.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsFundContributionListItemDto>
        {
            Items = items.Select(MapToContributionListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    #region Mapping

    private static InsCoInsuranceFundDto MapToDto(InsCoInsuranceFund entity)
    {
        return new InsCoInsuranceFundDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            FundName = entity.FFundName,
            FundCode = entity.FFundCode,
            BusinessType = entity.FBusinessType,
            FundDescription = entity.FFundDescription,
            TotalContributions = entity.FTotalContributions,
            TotalPayouts = entity.FTotalPayouts,
            FundBalance = entity.FFundBalance,
            ContributionStandard = entity.FContributionStandard,
            PaymentCycle = entity.FPaymentCycle,
            Deductible = entity.FDeductible,
            SinglePayoutLimit = entity.FSinglePayoutLimit,
            AnnualPayoutLimit = entity.FAnnualPayoutLimit,
            FundStatus = entity.FFundStatus,
            EffectiveDate = entity.FEffectiveDate,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static InsCoInsuranceFundListItemDto MapToListItemDto(InsCoInsuranceFund entity)
    {
        return new InsCoInsuranceFundListItemDto
        {
            Id = entity.FID,
            FundName = entity.FFundName,
            FundCode = entity.FFundCode,
            BusinessType = entity.FBusinessType,
            FundBalance = entity.FFundBalance,
            TotalContributions = entity.FTotalContributions,
            TotalPayouts = entity.FTotalPayouts,
            FundStatus = entity.FFundStatus,
            EffectiveDate = entity.FEffectiveDate
        };
    }

    private static InsFundContributionListItemDto MapToContributionListItemDto(InsFundContribution entity)
    {
        return new InsFundContributionListItemDto
        {
            Id = entity.FID,
            ContributionNumber = entity.FContributionNumber,
            FundName = entity.Fund?.FFundName,
            RelatedObjectName = entity.FRelatedObjectName,
            ContributionAmount = entity.FContributionAmount,
            PeriodStart = entity.FPeriodStart,
            PeriodEnd = entity.FPeriodEnd,
            PaymentDate = entity.FPaymentDate,
            PaymentStatus = entity.FPaymentStatus
        };
    }

    #endregion
}
