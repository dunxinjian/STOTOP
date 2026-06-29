using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class InsPolicyService : IInsPolicyService
{
    private readonly IRepository<InsPolicy> _policyRepository;
    private readonly IRepository<InsCompany> _companyRepository;

    public InsPolicyService(
        IRepository<InsPolicy> policyRepository,
        IRepository<InsCompany> companyRepository)
    {
        _policyRepository = policyRepository;
        _companyRepository = companyRepository;
    }

    public async Task<PagedResult<InsPolicyListItemDto>> GetListAsync(InsPolicyQueryRequest request)
    {
        var query = _policyRepository.Query()
            .Include(p => p.InsuranceCompany)
            .AsQueryable();

        if (request.BusinessType.HasValue)
        {
            query = query.Where(p => p.FBusinessType == request.BusinessType.Value);
        }

        if (request.RelatedObjectId.HasValue)
        {
            query = query.Where(p => p.FRelatedObjectId == request.RelatedObjectId.Value);
        }

        if (request.InsuranceCategory.HasValue)
        {
            query = query.Where(p => p.FInsuranceCategory == request.InsuranceCategory.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.InsuranceType))
        {
            query = query.Where(p => p.FInsuranceType != null && p.FInsuranceType.Contains(request.InsuranceType));
        }

        if (request.InsuranceStatus.HasValue)
        {
            query = query.Where(p => p.FInsuranceStatus == request.InsuranceStatus.Value);
        }

        if (request.InsuranceCompanyId.HasValue)
        {
            query = query.Where(p => p.FInsuranceCompanyId == request.InsuranceCompanyId.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsPolicyListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InsPolicyDto?> GetByIdAsync(long id)
    {
        var entity = await _policyRepository.Query()
            .Include(p => p.InsuranceCompany)
            .Include(p => p.CoInsuranceFund)
            .FirstOrDefaultAsync(p => p.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InsPolicyDto> CreateAsync(CreateInsPolicyRequest request)
    {
        var entity = new InsPolicy
        {
            FUID = Guid.NewGuid().ToString("N"),
            FBusinessType = request.BusinessType,
            FRelatedObjectId = request.RelatedObjectId,
            FRelatedObjectName = request.RelatedObjectName,
            FInsuranceCategory = request.InsuranceCategory,
            FInsuranceType = request.InsuranceType,
            FInsuranceCompanyId = request.InsuranceCompanyId,
            FPolicyNumber = request.PolicyNumber,
            FPremium = request.Premium,
            FInsuredAmount = request.InsuredAmount,
            FContactPerson = request.ContactPerson,
            FContactPhone = request.ContactPhone,
            FCoInsuranceFundId = request.CoInsuranceFundId,
            FParticipationNumber = request.ParticipationNumber,
            FPaymentCycle = request.PaymentCycle,
            FPerPeriodAmount = request.PerPeriodAmount,
            FEffectiveDate = request.EffectiveDate,
            FExpiryDate = request.ExpiryDate,
            FInsuranceStatus = 1, // 有效
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _policyRepository.AddAsync(entity);

        return (await GetByIdAsync(entity.FID))!;
    }

    public async Task<InsPolicyDto?> UpdateAsync(long id, UpdateInsPolicyRequest request)
    {
        var entity = await _policyRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (entity == null) return null;

        entity.FInsuranceType = request.InsuranceType;
        entity.FInsuranceCompanyId = request.InsuranceCompanyId;
        entity.FPolicyNumber = request.PolicyNumber;
        entity.FPremium = request.Premium;
        entity.FInsuredAmount = request.InsuredAmount;
        entity.FContactPerson = request.ContactPerson;
        entity.FContactPhone = request.ContactPhone;
        entity.FCoInsuranceFundId = request.CoInsuranceFundId;
        entity.FParticipationNumber = request.ParticipationNumber;
        entity.FPaymentCycle = request.PaymentCycle;
        entity.FPerPeriodAmount = request.PerPeriodAmount;
        entity.FEffectiveDate = request.EffectiveDate;
        entity.FExpiryDate = request.ExpiryDate;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _policyRepository.UpdateAsync(entity);

        return await GetByIdAsync(id);
    }

    public async Task<List<InsPolicyListItemDto>> GetExpiringAsync(int days = 30)
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        var expiryThreshold = now.AddDays(days);

        var items = await _policyRepository.Query()
            .Include(p => p.InsuranceCompany)
            .Where(p => p.FInsuranceStatus == 1 &&
                        p.FExpiryDate >= now &&
                        p.FExpiryDate <= expiryThreshold)
            .OrderBy(p => p.FExpiryDate)
            .ToListAsync();

        return items.Select(MapToListItemDto).ToList();
    }

    public async Task<List<InsPolicyListItemDto>> GetByObjectAsync(int bizType, long objectId)
    {
        var items = await _policyRepository.Query()
            .Include(p => p.InsuranceCompany)
            .Where(p => p.FBusinessType == bizType && p.FRelatedObjectId == objectId)
            .OrderByDescending(p => p.FCreatedTime)
            .ToListAsync();

        return items.Select(MapToListItemDto).ToList();
    }

    #region Mapping

    private static InsPolicyDto MapToDto(InsPolicy entity)
    {
        return new InsPolicyDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            BusinessType = entity.FBusinessType,
            RelatedObjectId = entity.FRelatedObjectId,
            RelatedObjectName = entity.FRelatedObjectName,
            InsuranceCategory = entity.FInsuranceCategory,
            InsuranceType = entity.FInsuranceType,
            InsuranceCompanyId = entity.FInsuranceCompanyId,
            InsuranceCompanyName = entity.InsuranceCompany?.FCompanyName,
            PolicyNumber = entity.FPolicyNumber,
            Premium = entity.FPremium,
            InsuredAmount = entity.FInsuredAmount,
            ContactPerson = entity.FContactPerson,
            ContactPhone = entity.FContactPhone,
            CoInsuranceFundId = entity.FCoInsuranceFundId,
            CoInsuranceFundName = entity.CoInsuranceFund?.FFundName,
            ParticipationNumber = entity.FParticipationNumber,
            PaymentCycle = entity.FPaymentCycle,
            PerPeriodAmount = entity.FPerPeriodAmount,
            EffectiveDate = entity.FEffectiveDate,
            ExpiryDate = entity.FExpiryDate,
            InsuranceStatus = entity.FInsuranceStatus,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static InsPolicyListItemDto MapToListItemDto(InsPolicy entity)
    {
        return new InsPolicyListItemDto
        {
            Id = entity.FID,
            BusinessType = entity.FBusinessType,
            RelatedObjectName = entity.FRelatedObjectName,
            InsuranceCategory = entity.FInsuranceCategory,
            InsuranceType = entity.FInsuranceType,
            InsuranceCompanyName = entity.InsuranceCompany?.FCompanyName,
            PolicyNumber = entity.FPolicyNumber,
            Premium = entity.FPremium,
            EffectiveDate = entity.FEffectiveDate,
            ExpiryDate = entity.FExpiryDate,
            InsuranceStatus = entity.FInsuranceStatus
        };
    }

    #endregion
}
