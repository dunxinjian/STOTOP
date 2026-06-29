using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class InsClaimService : IInsClaimService
{
    private readonly IRepository<InsClaim> _claimRepository;

    public InsClaimService(IRepository<InsClaim> claimRepository)
    {
        _claimRepository = claimRepository;
    }

    public async Task<PagedResult<InsClaimListItemDto>> GetListAsync(InsClaimQueryRequest request)
    {
        var query = _claimRepository.Query();

        if (request.BusinessType.HasValue)
        {
            query = query.Where(c => c.FBusinessType == request.BusinessType.Value);
        }

        if (request.RelatedObjectId.HasValue)
        {
            query = query.Where(c => c.FRelatedObjectId == request.RelatedObjectId.Value);
        }

        if (request.AccidentType.HasValue)
        {
            query = query.Where(c => c.FAccidentType == request.AccidentType.Value);
        }

        if (request.ClaimStatus.HasValue)
        {
            query = query.Where(c => c.FClaimStatus == request.ClaimStatus.Value);
        }

        if (request.PolicyId.HasValue)
        {
            query = query.Where(c => c.FPolicyId == request.PolicyId.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsClaimListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InsClaimDto?> GetByIdAsync(long id)
    {
        var entity = await _claimRepository.Query()
            .Include(c => c.Policy)
            .FirstOrDefaultAsync(c => c.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InsClaimDto> CreateAsync(CreateInsClaimRequest request)
    {
        var entity = new InsClaim
        {
            FUID = Guid.NewGuid().ToString("N"),
            FPolicyId = request.PolicyId,
            FBusinessType = request.BusinessType,
            FRelatedObjectId = request.RelatedObjectId,
            FRelatedObjectName = request.RelatedObjectName,
            FClaimNumber = await GenerateClaimNumberAsync(),
            FClaimDate = request.ClaimDate,
            FClaimLocation = request.ClaimLocation,
            FAccidentType = request.AccidentType,
            FAccidentDescription = request.AccidentDescription,
            FCounterpartyInfo = request.CounterpartyInfo,
            FEstimatedLoss = request.EstimatedLoss,
            FLiabilityDivision = request.LiabilityDivision,
            FPartyId = request.PartyId,
            FPartyName = request.PartyName,
            FCaseNumber = request.CaseNumber,
            FClaimImages = request.ClaimImages,
            FClaimStatus = 1, // 已登记
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _claimRepository.AddAsync(entity);

        return (await GetByIdAsync(entity.FID))!;
    }

    public async Task<InsClaimDto?> UpdateAsync(long id, UpdateInsClaimRequest request)
    {
        var entity = await _claimRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return null;

        entity.FClaimLocation = request.ClaimLocation;
        entity.FAccidentType = request.AccidentType;
        entity.FAccidentDescription = request.AccidentDescription;
        entity.FCounterpartyInfo = request.CounterpartyInfo;
        entity.FEstimatedLoss = request.EstimatedLoss;
        entity.FActualLoss = request.ActualLoss;
        entity.FLiabilityDivision = request.LiabilityDivision;
        entity.FPartyId = request.PartyId;
        entity.FPartyName = request.PartyName;
        entity.FCaseNumber = request.CaseNumber;
        entity.FClaimImages = request.ClaimImages;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _claimRepository.UpdateAsync(entity);

        return await GetByIdAsync(id);
    }

    public async Task<bool> CloseAsync(long id, string? closedRemark)
    {
        var entity = await _claimRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return false;

        if (entity.FClaimStatus == 3) return false; // 已经是结案状态

        entity.FClaimStatus = 3; // 已结案
        entity.FClosedDate = DateTime.Now;
        entity.FClosedRemark = closedRemark;
        entity.FUpdatedTime = DateTime.Now;

        await _claimRepository.UpdateAsync(entity);

        return true;
    }

    #region Private Methods

    private async Task<string> GenerateClaimNumberAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"CLM{today}";

        var count = await _claimRepository.Query()
            .CountAsync(c => c.FClaimNumber.StartsWith(prefix));

        return $"{prefix}{(count + 1):D4}";
    }

    #endregion

    #region Mapping

    private static InsClaimDto MapToDto(InsClaim entity)
    {
        return new InsClaimDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            PolicyId = entity.FPolicyId,
            BusinessType = entity.FBusinessType,
            RelatedObjectId = entity.FRelatedObjectId,
            RelatedObjectName = entity.FRelatedObjectName,
            ClaimNumber = entity.FClaimNumber,
            ClaimDate = entity.FClaimDate,
            ClaimLocation = entity.FClaimLocation,
            AccidentType = entity.FAccidentType,
            AccidentDescription = entity.FAccidentDescription,
            CounterpartyInfo = entity.FCounterpartyInfo,
            EstimatedLoss = entity.FEstimatedLoss,
            ActualLoss = entity.FActualLoss,
            LiabilityDivision = entity.FLiabilityDivision,
            PartyId = entity.FPartyId,
            PartyName = entity.FPartyName,
            CaseNumber = entity.FCaseNumber,
            ClaimImages = entity.FClaimImages,
            ClaimStatus = entity.FClaimStatus,
            ClosedDate = entity.FClosedDate,
            ClosedRemark = entity.FClosedRemark,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static InsClaimListItemDto MapToListItemDto(InsClaim entity)
    {
        return new InsClaimListItemDto
        {
            Id = entity.FID,
            ClaimNumber = entity.FClaimNumber,
            BusinessType = entity.FBusinessType,
            RelatedObjectName = entity.FRelatedObjectName,
            ClaimDate = entity.FClaimDate,
            AccidentType = entity.FAccidentType,
            EstimatedLoss = entity.FEstimatedLoss,
            ActualLoss = entity.FActualLoss,
            ClaimStatus = entity.FClaimStatus
        };
    }

    #endregion
}
