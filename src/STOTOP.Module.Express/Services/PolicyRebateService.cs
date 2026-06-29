using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class PolicyRebateService : IPolicyRebateService
{
    private readonly IRepository<ExpPolicyRebate> _rebateRepo;
    private readonly IRepository<ExpPolicyRebateTier> _tierRepo;
    private readonly IRepository<ExpPolicyRebateRule> _ruleRepo;
    private readonly IRepository<ExpPolicyRebateRuleItem> _ruleItemRepo;

    public PolicyRebateService(
        IRepository<ExpPolicyRebate> rebateRepo,
        IRepository<ExpPolicyRebateTier> tierRepo,
        IRepository<ExpPolicyRebateRule> ruleRepo,
        IRepository<ExpPolicyRebateRuleItem> ruleItemRepo)
    {
        _rebateRepo = rebateRepo;
        _tierRepo = tierRepo;
        _ruleRepo = ruleRepo;
        _ruleItemRepo = ruleItemRepo;
    }

    public async Task<PagedResult<PolicyRebateListItemDto>> GetPagedListAsync(PolicyRebateQueryRequest request)
    {
        var query = _rebateRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(e => e.FPolicyName.Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.RebateMode.HasValue)
            query = query.Where(e => e.FRebateMode == request.RebateMode.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<PolicyRebateListItemDto>
        {
            Items = items.Select(MapToListItem).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<PolicyRebateDetailDto?> GetDetailAsync(long id)
    {
        var entity = await _rebateRepo.GetByIdAsync(id);
        if (entity == null) return null;

        var tiers = await _tierRepo.Query()
            .Where(t => t.FPolicyRebateId == id)
            .OrderBy(t => t.FSortOrder)
            .ToListAsync();

        var rules = await _ruleRepo.Query()
            .Where(r => r.FPolicyRebateId == id)
            .OrderBy(r => r.FSortOrder)
            .ToListAsync();

        var ruleIds = rules.Select(r => r.FID).ToList();
        var ruleItems = ruleIds.Count > 0
            ? await _ruleItemRepo.Query()
                .Where(ri => ruleIds.Contains(ri.FRuleId))
                .OrderBy(ri => ri.FSortOrder)
                .ToListAsync()
            : new List<ExpPolicyRebateRuleItem>();

        return MapToDetail(entity, tiers, rules, ruleItems);
    }

    public async Task<PolicyRebateDetailDto> CreateAsync(CreatePolicyRebateRequest request)
    {
        var entity = new ExpPolicyRebate
        {
            FBrandCode = request.BrandCode,
            FPolicyName = request.PolicyName,
            FRebateMode = request.RebateMode,
            FFlatRebateAmount = request.FlatRebateAmount,
            FSettlementCycle = request.SettlementCycle,
            FEffectiveDate = request.EffectiveDate,
            FExpiryDate = request.ExpiryDate,
            FRemark = request.Remark,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };
        entity = await _rebateRepo.AddAsync(entity);

        // 创建阶梯
        var tiers = new List<ExpPolicyRebateTier>();
        foreach (var t in request.Tiers)
        {
            var tier = new ExpPolicyRebateTier
            {
                FPolicyRebateId = entity.FID,
                FDailyVolumeFrom = t.DailyVolumeFrom,
                FDailyVolumeTo = t.DailyVolumeTo,
                FRebatePerTicket = t.RebatePerTicket,
                FSortOrder = t.SortOrder
            };
            tier = await _tierRepo.AddAsync(tier);
            tiers.Add(tier);
        }

        // 创建规则 + 规则条件
        var rules = new List<ExpPolicyRebateRule>();
        var allRuleItems = new List<ExpPolicyRebateRuleItem>();
        foreach (var r in request.Rules)
        {
            var rule = new ExpPolicyRebateRule
            {
                FPolicyRebateId = entity.FID,
                FRuleType = r.RuleType,
                FRuleName = r.RuleName,
                FEnabled = r.Enabled,
                FSortOrder = r.SortOrder
            };
            rule = await _ruleRepo.AddAsync(rule);
            rules.Add(rule);

            foreach (var ri in r.Items)
            {
                var item = new ExpPolicyRebateRuleItem
                {
                    FRuleId = rule.FID,
                    FThresholdLower = ri.ThresholdLower,
                    FThresholdUpper = ri.ThresholdUpper,
                    FWeightFrom = ri.WeightFrom,
                    FWeightTo = ri.WeightTo,
                    FProvinceId = ri.ProvinceId,
                    FAdjustType = ri.AdjustType,
                    FAdjustCalcMethod = ri.AdjustCalcMethod,
                    FAdjustAmount = ri.AdjustAmount,
                    FAdjustRate = ri.AdjustRate,
                    FSortOrder = ri.SortOrder
                };
                item = await _ruleItemRepo.AddAsync(item);
                allRuleItems.Add(item);
            }
        }

        return MapToDetail(entity, tiers, rules, allRuleItems);
    }

    public async Task<PolicyRebateDetailDto?> UpdateAsync(long id, UpdatePolicyRebateRequest request)
    {
        var entity = await _rebateRepo.Query().AsTracking().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return null;

        entity.FPolicyName = request.PolicyName;
        entity.FRebateMode = request.RebateMode;
        entity.FFlatRebateAmount = request.FlatRebateAmount;
        entity.FSettlementCycle = request.SettlementCycle;
        entity.FEffectiveDate = request.EffectiveDate;
        entity.FExpiryDate = request.ExpiryDate;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;
        await _rebateRepo.UpdateAsync(entity);

        // 先删后建：删除旧的 tiers / rules / rule items
        var oldTiers = await _tierRepo.Query().Where(t => t.FPolicyRebateId == id).ToListAsync();
        foreach (var t in oldTiers) await _tierRepo.DeleteAsync(t.FID);

        var oldRules = await _ruleRepo.Query().Where(r => r.FPolicyRebateId == id).ToListAsync();
        var oldRuleIds = oldRules.Select(r => r.FID).ToList();
        if (oldRuleIds.Count > 0)
        {
            var oldItems = await _ruleItemRepo.Query().Where(ri => oldRuleIds.Contains(ri.FRuleId)).ToListAsync();
            foreach (var ri in oldItems) await _ruleItemRepo.DeleteAsync(ri.FID);
        }
        foreach (var r in oldRules) await _ruleRepo.DeleteAsync(r.FID);

        // 重建子表
        var tiers = new List<ExpPolicyRebateTier>();
        foreach (var t in request.Tiers)
        {
            var tier = new ExpPolicyRebateTier
            {
                FPolicyRebateId = id,
                FDailyVolumeFrom = t.DailyVolumeFrom,
                FDailyVolumeTo = t.DailyVolumeTo,
                FRebatePerTicket = t.RebatePerTicket,
                FSortOrder = t.SortOrder
            };
            tier = await _tierRepo.AddAsync(tier);
            tiers.Add(tier);
        }

        var rules = new List<ExpPolicyRebateRule>();
        var allRuleItems = new List<ExpPolicyRebateRuleItem>();
        foreach (var r in request.Rules)
        {
            var rule = new ExpPolicyRebateRule
            {
                FPolicyRebateId = id,
                FRuleType = r.RuleType,
                FRuleName = r.RuleName,
                FEnabled = r.Enabled,
                FSortOrder = r.SortOrder
            };
            rule = await _ruleRepo.AddAsync(rule);
            rules.Add(rule);

            foreach (var ri in r.Items)
            {
                var item = new ExpPolicyRebateRuleItem
                {
                    FRuleId = rule.FID,
                    FThresholdLower = ri.ThresholdLower,
                    FThresholdUpper = ri.ThresholdUpper,
                    FWeightFrom = ri.WeightFrom,
                    FWeightTo = ri.WeightTo,
                    FProvinceId = ri.ProvinceId,
                    FAdjustType = ri.AdjustType,
                    FAdjustCalcMethod = ri.AdjustCalcMethod,
                    FAdjustAmount = ri.AdjustAmount,
                    FAdjustRate = ri.AdjustRate,
                    FSortOrder = ri.SortOrder
                };
                item = await _ruleItemRepo.AddAsync(item);
                allRuleItems.Add(item);
            }
        }

        return MapToDetail(entity, tiers, rules, allRuleItems);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _rebateRepo.GetByIdAsync(id);
        if (entity == null) return false;

        // 删除子表数据
        var rules = await _ruleRepo.Query().Where(r => r.FPolicyRebateId == id).ToListAsync();
        var ruleIds = rules.Select(r => r.FID).ToList();
        if (ruleIds.Count > 0)
        {
            var items = await _ruleItemRepo.Query().Where(ri => ruleIds.Contains(ri.FRuleId)).ToListAsync();
            foreach (var ri in items) await _ruleItemRepo.DeleteAsync(ri.FID);
        }
        foreach (var r in rules) await _ruleRepo.DeleteAsync(r.FID);

        var tiers = await _tierRepo.Query().Where(t => t.FPolicyRebateId == id).ToListAsync();
        foreach (var t in tiers) await _tierRepo.DeleteAsync(t.FID);

        await _rebateRepo.DeleteAsync(id);
        return true;
    }

    public async Task<bool> EnableAsync(long id)
    {
        var entity = await _rebateRepo.Query().AsTracking().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return false;
        entity.FStatus = 1; // 生效
        entity.FUpdatedTime = DateTime.Now;
        await _rebateRepo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DisableAsync(long id)
    {
        var entity = await _rebateRepo.Query().AsTracking().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return false;
        entity.FStatus = 2; // 停用
        entity.FUpdatedTime = DateTime.Now;
        await _rebateRepo.UpdateAsync(entity);
        return true;
    }

    #region Mapping

    private static PolicyRebateListItemDto MapToListItem(ExpPolicyRebate e) => new()
    {
        Id = e.FID,
        BrandCode = e.FBrandCode,
        PolicyName = e.FPolicyName,
        RebateMode = e.FRebateMode,
        FlatRebateAmount = e.FFlatRebateAmount,
        SettlementCycle = e.FSettlementCycle,
        EffectiveDate = e.FEffectiveDate,
        ExpiryDate = e.FExpiryDate,
        Status = e.FStatus,
        CreatedTime = e.FCreatedTime
    };

    private static PolicyRebateDetailDto MapToDetail(
        ExpPolicyRebate e,
        List<ExpPolicyRebateTier> tiers,
        List<ExpPolicyRebateRule> rules,
        List<ExpPolicyRebateRuleItem> ruleItems)
    {
        var ruleItemsByRuleId = ruleItems.GroupBy(ri => ri.FRuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new PolicyRebateDetailDto
        {
            Id = e.FID,
            BrandCode = e.FBrandCode,
            PolicyName = e.FPolicyName,
            RebateMode = e.FRebateMode,
            FlatRebateAmount = e.FFlatRebateAmount,
            SettlementCycle = e.FSettlementCycle,
            EffectiveDate = e.FEffectiveDate,
            ExpiryDate = e.FExpiryDate,
            Status = e.FStatus,
            Remark = e.FRemark,
            CreatedTime = e.FCreatedTime,
            UpdatedTime = e.FUpdatedTime,
            Tiers = tiers.Select(t => new PolicyRebateTierDto
            {
                Id = t.FID,
                DailyVolumeFrom = t.FDailyVolumeFrom,
                DailyVolumeTo = t.FDailyVolumeTo,
                RebatePerTicket = t.FRebatePerTicket,
                SortOrder = t.FSortOrder
            }).ToList(),
            Rules = rules.Select(r => new PolicyRebateRuleDto
            {
                Id = r.FID,
                RuleType = r.FRuleType,
                RuleName = r.FRuleName,
                Enabled = r.FEnabled,
                SortOrder = r.FSortOrder,
                Items = ruleItemsByRuleId.GetValueOrDefault(r.FID, new())
                    .Select(ri => new PolicyRebateRuleItemDto
                    {
                        Id = ri.FID,
                        ThresholdLower = ri.FThresholdLower,
                        ThresholdUpper = ri.FThresholdUpper,
                        WeightFrom = ri.FWeightFrom,
                        WeightTo = ri.FWeightTo,
                        ProvinceId = ri.FProvinceId,
                        AdjustType = ri.FAdjustType,
                        AdjustCalcMethod = ri.FAdjustCalcMethod,
                        AdjustAmount = ri.FAdjustAmount,
                        AdjustRate = ri.FAdjustRate,
                        SortOrder = ri.FSortOrder
                    }).ToList()
            }).ToList()
        };
    }

    #endregion
}
