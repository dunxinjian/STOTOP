using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class CardRelationService : ICardRelationService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<CardRelationService> _logger;

    public CardRelationService(STOTOPDbContext dbContext, ILogger<CardRelationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ValidatePrerequisitesAsync(long cardId)
    {
        var prerequisites = await _dbContext.Set<CfCardRelation>()
            .Where(r => r.FTargetCardId == cardId && r.FRelationType == "prerequisite")
            .ToListAsync();

        if (!prerequisites.Any()) return;

        var sourceCardIds = prerequisites.Select(r => r.FSourceCardId).ToList();
        var sourceCards = await _dbContext.Set<CfCard>()
            .Where(c => sourceCardIds.Contains(c.FID))
            .ToListAsync();

        var incompleteCards = sourceCards.Where(c => c.FStatus != "completed").ToList();
        if (incompleteCards.Any())
        {
            var numbers = string.Join(", ", incompleteCards.Select(c => c.FCardNumber ?? c.FID.ToString()));
            throw new InvalidOperationException($"前置卡片未完成: {numbers}");
        }
    }

    public async Task ExecuteOffsetAsync(long cardId)
    {
        var offsetRelations = await _dbContext.Set<CfCardRelation>()
            .Where(r => r.FSourceCardId == cardId && r.FRelationType == "offset" && r.FOffsetAmount.HasValue)
            .ToListAsync();

        if (!offsetRelations.Any()) return;

        foreach (var relation in offsetRelations)
        {
            var balance = await _dbContext.Set<CfCardBalance>()
                .FirstOrDefaultAsync(b => b.FCardId == relation.FTargetCardId && b.FStatus == "active");

            if (balance == null)
                throw new InvalidOperationException($"目标卡片 {relation.FTargetCardId} 无可冲抵余额");

            if (balance.FRemainingAmount < relation.FOffsetAmount!.Value)
                throw new InvalidOperationException($"目标卡片 {relation.FTargetCardId} 余额不足");

            balance.FOffsetAmount += relation.FOffsetAmount.Value;
            balance.FRemainingAmount -= relation.FOffsetAmount.Value;
            balance.FUpdatedTime = DateTime.Now;

            if (balance.FRemainingAmount == 0)
                balance.FStatus = "exhausted";
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Executed offset for card {CardId}, {Count} relations processed", cardId, offsetRelations.Count);
    }

    public async Task RollbackOffsetAsync(long cardId)
    {
        var offsetRelations = await _dbContext.Set<CfCardRelation>()
            .Where(r => r.FSourceCardId == cardId && r.FRelationType == "offset" && r.FOffsetAmount.HasValue)
            .ToListAsync();

        if (!offsetRelations.Any()) return;

        foreach (var relation in offsetRelations)
        {
            var balance = await _dbContext.Set<CfCardBalance>()
                .FirstOrDefaultAsync(b => b.FCardId == relation.FTargetCardId);

            if (balance == null) continue;

            balance.FOffsetAmount -= relation.FOffsetAmount!.Value;
            balance.FRemainingAmount += relation.FOffsetAmount.Value;
            balance.FUpdatedTime = DateTime.Now;

            if (balance.FStatus == "exhausted")
                balance.FStatus = "active";
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Rolled back offset for card {CardId}", cardId);
    }

    public async Task<List<CardBalanceDto>> GetAvailableBalancesAsync(long userId, long orgId)
    {
        return await _dbContext.Set<CfCardBalance>()
            .Where(b => b.FOrgId == orgId && b.FRemainingAmount > 0 && b.FStatus == "active")
            .Join(_dbContext.Set<CfCard>(), b => b.FCardId, c => c.FID, (b, c) => new CardBalanceDto
            {
                Id = b.FID,
                CardId = b.FCardId,
                CardNumber = c.FCardNumber,
                CardTitle = c.FTitle,
                OriginalAmount = b.FOriginalAmount,
                OffsetAmount = b.FOffsetAmount,
                RemainingAmount = b.FRemainingAmount,
                Status = b.FStatus
            })
            .ToListAsync();
    }
}
