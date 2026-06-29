using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class DelegationService : IDelegationService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<DelegationService> _logger;

    public DelegationService(STOTOPDbContext dbContext, ILogger<DelegationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<DelegationDto>> GetMyDelegationsAsync(long userId)
    {
        return await _dbContext.Set<CfDelegation>()
            .Where(d => d.FDelegatorId == userId && d.FStatus != "cancelled")
            .OrderByDescending(d => d.FStartTime)
            .Select(d => new DelegationDto
            {
                Id = d.FID,
                DelegatorId = d.FDelegatorId,
                DelegatorName = d.FDelegatorName,
                TrusteeId = d.FTrusteeId,
                TrusteeName = d.FTrusteeName,
                StartTime = d.FStartTime,
                EndTime = d.FEndTime,
                ApplicableFlowsJson = d.FApplicableFlowsJson,
                Status = d.FStatus
            })
            .ToListAsync();
    }

    public async Task<DelegationDto> CreateAsync(CreateDelegationRequest request, long userId)
    {
        var entity = new CfDelegation
        {
            FDelegatorId = userId,
            FDelegatorName = string.Empty,
            FTrusteeId = request.TrusteeId,
            FTrusteeName = request.TrusteeName,
            FStartTime = request.StartTime,
            FEndTime = request.EndTime,
            FApplicableFlowsJson = request.ApplicableFlowsJson,
            FStatus = "active",
            FOrgId = 0
        };

        _dbContext.Set<CfDelegation>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created delegation {Id} from user {UserId} to {TrusteeId}", entity.FID, userId, request.TrusteeId);

        return new DelegationDto
        {
            Id = entity.FID,
            DelegatorId = entity.FDelegatorId,
            DelegatorName = entity.FDelegatorName,
            TrusteeId = entity.FTrusteeId,
            TrusteeName = entity.FTrusteeName,
            StartTime = entity.FStartTime,
            EndTime = entity.FEndTime,
            ApplicableFlowsJson = entity.FApplicableFlowsJson,
            Status = entity.FStatus
        };
    }

    public async Task<DelegationDto> UpdateAsync(long id, UpdateDelegationRequest request, long userId)
    {
        var entity = await _dbContext.Set<CfDelegation>().FirstOrDefaultAsync(d => d.FID == id && d.FDelegatorId == userId)
            ?? throw new InvalidOperationException("委托记录不存在或无权操作");

        if (request.TrusteeId.HasValue)
            entity.FTrusteeId = request.TrusteeId.Value;
        if (!string.IsNullOrEmpty(request.TrusteeName))
            entity.FTrusteeName = request.TrusteeName;
        if (request.StartTime.HasValue)
            entity.FStartTime = request.StartTime.Value;
        if (request.EndTime.HasValue)
            entity.FEndTime = request.EndTime.Value;
        if (request.ApplicableFlowsJson != null)
            entity.FApplicableFlowsJson = request.ApplicableFlowsJson;

        await _dbContext.SaveChangesAsync();

        return new DelegationDto
        {
            Id = entity.FID,
            DelegatorId = entity.FDelegatorId,
            DelegatorName = entity.FDelegatorName,
            TrusteeId = entity.FTrusteeId,
            TrusteeName = entity.FTrusteeName,
            StartTime = entity.FStartTime,
            EndTime = entity.FEndTime,
            ApplicableFlowsJson = entity.FApplicableFlowsJson,
            Status = entity.FStatus
        };
    }

    public async Task CancelAsync(long id, long userId)
    {
        var entity = await _dbContext.Set<CfDelegation>().FirstOrDefaultAsync(d => d.FID == id && d.FDelegatorId == userId)
            ?? throw new InvalidOperationException("委托记录不存在或无权操作");

        entity.FStatus = "cancelled";
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Cancelled delegation {Id} by user {UserId}", id, userId);
    }
}
