using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class InvoiceReviewService : IInvoiceReviewService
{
    private readonly IRepository<ExpInvoice> _invoiceRepo;
    private readonly IRepository<ExpInvoiceReviewRule> _ruleRepo;
    private readonly IRepository<ExpInvoiceReviewLog> _logRepo;
    private readonly IRepository<ExpBillingResult> _billingResultRepo;

    public InvoiceReviewService(
        IRepository<ExpInvoice> invoiceRepo,
        IRepository<ExpInvoiceReviewRule> ruleRepo,
        IRepository<ExpInvoiceReviewLog> logRepo,
        IRepository<ExpBillingResult> billingResultRepo)
    {
        _invoiceRepo = invoiceRepo;
        _ruleRepo = ruleRepo;
        _logRepo = logRepo;
        _billingResultRepo = billingResultRepo;
    }

    public async Task AutoReviewAsync(long invoiceId)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) return;

        // 读取所有启用的审核规则
        var rules = await _ruleRepo.Query()
            .Where(r => r.FEnabled)
            .OrderBy(r => r.FPriority)
            .ToListAsync();

        if (rules.Count == 0)
        {
            // 无规则，直接自动通过
            invoice.FReviewStatus = 1; // 自动通过
            invoice.FReviewTime = DateTime.Now;
            invoice.FUpdatedTime = DateTime.Now;
            await _invoiceRepo.UpdateAsync(invoice);

            await _logRepo.AddAsync(new ExpInvoiceReviewLog
            {
                FInvoiceId = invoiceId,
                FAction = 1, // 自动通过
                FRuleResult = "无审核规则，自动通过",
                FCreatedTime = DateTime.Now
            });
            return;
        }

        // 获取账单相关的计费结果用于检查
        var billingResults = await _billingResultRepo.Query()
            .Where(b => b.FInvoiceId == invoiceId && b.FPartyRole == 1)
            .ToListAsync();

        var allPassed = true;

        foreach (var rule in rules)
        {
            // 如果规则指定了业务对象或品牌，检查是否匹配
            if (!string.IsNullOrWhiteSpace(rule.FBusinessObjectId) && rule.FBusinessObjectId != invoice.FBusinessObjectId) continue;
            if (!string.IsNullOrWhiteSpace(rule.FBrandCode) && rule.FBrandCode != invoice.FBrandCode) continue;

            var passed = true;
            var resultMsg = "";

            switch (rule.FRuleType)
            {
                case 1: // 单票均价范围
                    if (invoice.FTotalWaybills > 0 && invoice.FTotalCharge.HasValue)
                    {
                        var avgPrice = invoice.FTotalCharge.Value / invoice.FTotalWaybills.Value;
                        passed = (!rule.FMinValue.HasValue || avgPrice >= rule.FMinValue.Value)
                              && (!rule.FMaxValue.HasValue || avgPrice <= rule.FMaxValue.Value);
                        resultMsg = $"单票均价={avgPrice:F2}, 范围=[{rule.FMinValue},{rule.FMaxValue}]";
                    }
                    break;
                case 2: // 总额偏差比：账单总应收 vs 计费结果汇总应收的偏差百分比
                    if (rule.FThreshold.HasValue && invoice.FTotalCharge.HasValue)
                    {
                        var calcTotalCharge = billingResults
                            .Where(b => b.FChargeAmount.HasValue)
                            .Sum(b => b.FChargeAmount!.Value);
                        if (calcTotalCharge != 0)
                        {
                            var chargeDeviation = Math.Abs(invoice.FTotalCharge.Value - calcTotalCharge) / calcTotalCharge * 100;
                            passed = chargeDeviation <= rule.FThreshold.Value;
                            resultMsg = $"账单总额={invoice.FTotalCharge:F2}, 计费汇总={calcTotalCharge:F2}, 偏差={chargeDeviation:F2}%, 阈值={rule.FThreshold}%";
                        }
                        else
                        {
                            resultMsg = $"总额={invoice.FTotalCharge:F2}, 计费汇总为0无法计算偏差";
                        }
                    }
                    break;
                case 3: // 单量偏差比：账单总单量 vs 计费结果记录数的偏差百分比
                    if (rule.FThreshold.HasValue && invoice.FTotalWaybills.HasValue)
                    {
                        var calcWaybillCount = billingResults.Count;
                        if (calcWaybillCount > 0)
                        {
                            var waybillDeviation = Math.Abs(invoice.FTotalWaybills.Value - calcWaybillCount) / (decimal)calcWaybillCount * 100;
                            passed = waybillDeviation <= rule.FThreshold.Value;
                            resultMsg = $"账单单量={invoice.FTotalWaybills}, 计费条数={calcWaybillCount}, 偏差={waybillDeviation:F2}%, 阈值={rule.FThreshold}%";
                        }
                        else
                        {
                            resultMsg = $"单量={invoice.FTotalWaybills}, 计费结果为空无法计算偏差";
                        }
                    }
                    break;
                case 4: // 异常运单比例
                    if (rule.FThreshold.HasValue && billingResults.Count > 0)
                    {
                        var errorCount = billingResults.Count(b => b.FCalcStatus == 2);
                        var errorRatio = (decimal)errorCount / billingResults.Count * 100;
                        passed = errorRatio <= rule.FThreshold.Value;
                        resultMsg = $"异常比例={errorRatio:F2}%, 阈值={rule.FThreshold}%";
                    }
                    break;
                case 5: // 均重范围
                    if (invoice.FAvgWeight.HasValue)
                    {
                        passed = (!rule.FMinValue.HasValue || invoice.FAvgWeight.Value >= rule.FMinValue.Value)
                              && (!rule.FMaxValue.HasValue || invoice.FAvgWeight.Value <= rule.FMaxValue.Value);
                        resultMsg = $"均重={invoice.FAvgWeight:F3}, 范围=[{rule.FMinValue},{rule.FMaxValue}]";
                    }
                    break;
            }

            await _logRepo.AddAsync(new ExpInvoiceReviewLog
            {
                FInvoiceId = invoiceId,
                FAction = passed ? 1 : 2, // 1自动通过 2自动驳回
                FRuleId = rule.FID,
                FRuleResult = (passed ? "通过: " : "不通过: ") + resultMsg,
                FCreatedTime = DateTime.Now
            });

            if (!passed) allPassed = false;
        }

        invoice.FReviewStatus = allPassed ? 1 : 2; // 1自动通过 2待人工
        invoice.FReviewTime = allPassed ? DateTime.Now : null;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);
    }

    public async Task ManualReviewAsync(long invoiceId, bool approved, string? remark)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) throw new InvalidOperationException("账单不存在");

        invoice.FReviewStatus = approved ? 2 : 3; // 2人工通过 3人工驳回
        invoice.FReviewTime = DateTime.Now;
        invoice.FReviewRemark = remark;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);

        await _logRepo.AddAsync(new ExpInvoiceReviewLog
        {
            FInvoiceId = invoiceId,
            FAction = approved ? 3 : 4, // 3人工通过 4人工驳回
            FRemark = remark,
            FCreatedTime = DateTime.Now
        });
    }

    public async Task ReverseReviewAsync(long invoiceId, string? remark)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) throw new InvalidOperationException("账单不存在");

        invoice.FReviewStatus = 4; // 反审核
        invoice.FReviewTime = null;
        invoice.FReviewRemark = remark;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);

        await _logRepo.AddAsync(new ExpInvoiceReviewLog
        {
            FInvoiceId = invoiceId,
            FAction = 5, // 反审核
            FRemark = remark,
            FCreatedTime = DateTime.Now
        });
    }

    public async Task<List<ReviewRuleDto>> GetRulesAsync()
    {
        var rules = await _ruleRepo.Query()
            .OrderBy(r => r.FPriority)
            .ToListAsync();

        return rules.Select(MapRuleToDto).ToList();
    }

    public async Task<ReviewRuleDto> CreateRuleAsync(CreateReviewRuleRequest request)
    {
        var entity = new ExpInvoiceReviewRule
        {
            FRuleName = request.RuleName,
            FRuleType = request.RuleType,
            FMinValue = request.MinValue,
            FMaxValue = request.MaxValue,
            FThreshold = request.Threshold,
            FBusinessObjectId = request.ClientId,
            FBrandCode = request.BrandCode,
            FPriority = request.Priority,
            FEnabled = request.Enabled,
            FCreatedTime = DateTime.Now
        };

        var result = await _ruleRepo.AddAsync(entity);
        return MapRuleToDto(result);
    }

    public async Task<ReviewRuleDto?> UpdateRuleAsync(long id, UpdateReviewRuleRequest request)
    {
        var entity = await _ruleRepo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FRuleName = request.RuleName;
        entity.FRuleType = request.RuleType;
        entity.FMinValue = request.MinValue;
        entity.FMaxValue = request.MaxValue;
        entity.FThreshold = request.Threshold;
        entity.FBusinessObjectId = request.ClientId;
        entity.FBrandCode = request.BrandCode;
        entity.FPriority = request.Priority;
        entity.FEnabled = request.Enabled;

        await _ruleRepo.UpdateAsync(entity);
        return MapRuleToDto(entity);
    }

    public async Task<bool> DeleteRuleAsync(long id)
    {
        var entity = await _ruleRepo.GetByIdAsync(id);
        if (entity == null) return false;

        await _ruleRepo.DeleteAsync(id);
        return true;
    }

    private static ReviewRuleDto MapRuleToDto(ExpInvoiceReviewRule e) => new()
    {
        Id = e.FID,
        RuleName = e.FRuleName,
        RuleType = e.FRuleType,
        MinValue = e.FMinValue,
        MaxValue = e.FMaxValue,
        Threshold = e.FThreshold,
        ClientId = e.FBusinessObjectId,
        BrandCode = e.FBrandCode,
        Priority = e.FPriority,
        Enabled = e.FEnabled,
        CreatedTime = e.FCreatedTime
    };
}
