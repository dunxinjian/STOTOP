using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// M5-3 凭证试算预览（设计 E2）。
///
/// 【可行性结论（已读凭证引擎判定）】自动凭证引擎 AutoVoucherHandler 的运行时干跑
/// 强依赖已落库上下文：① 按 batchId 从 STG 暂存表（FActualTargetTable）用原生 SQL 读取数据行；
/// ② 从账套级 FinAccount / FinAuxiliaryItem 表解析科目与辅助核算。其规则列引用的是 STG 表列名
/// （如 F费用编码/F费用类别），与设计期卡片 cardDataJson 的字段键不同域——单张卡片 dataJson 无法
/// 无失真地喂进该引擎。故本端点【诚实降级】：不伪造分录，只做能静态预判的规则完整性检查
/// （节点是否为自动凭证节点 / 规则组是否为空），其余返回运行时才能生成的说明。
/// 若后续凭证引擎补出「卡片级 cardDataJson 干跑」入口，可在此接真试算。
/// </summary>
public sealed class VoucherPreviewService : IVoucherPreviewService
{
    private const string DegradeMessage = "凭证试算需完整卡片/批次上下文（STG 暂存表数据行 + 账套科目辅助核算），暂在运行时生成";

    private readonly STOTOPDbContext _dbContext;

    public VoucherPreviewService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VoucherPreviewDto> PreviewVoucherAsync(
        long definitionId,
        VoucherPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StageKey))
            return Fail("未指定节点 StageKey");

        var version = await ResolveVersionAsync(definitionId, request.FlowVersionId, cancellationToken);
        if (version == null)
            return Fail("没有可试算的流程版本");

        var stage = await _dbContext.Set<CfStageDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.FFlowVersionId == version.FID && s.FStageKey == request.StageKey,
                cancellationToken);
        if (stage == null)
            return Fail($"节点不存在：{request.StageKey}");

        var isAuto = string.Equals(stage.FType, "auto", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage.FType, "batchAuto", StringComparison.OrdinalIgnoreCase);
        if (!isAuto)
            return Fail($"节点「{stage.FStageName}」不是自动节点，无凭证试算");

        // 静态可预判①：未配插件规则 → 运行时无匹配规则（口径同 M5-2 autoStageError）
        if (!stage.F插件规则ID.HasValue)
            return Fail($"自动节点「{stage.FStageName}」未配置插件规则，运行时将无凭证规则可用");

        var rule = await _dbContext.Set<CfPluginRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == stage.F插件规则ID.Value, cancellationToken);
        if (rule == null)
            return Fail($"插件规则不存在（ID={stage.F插件规则ID}），运行时将无凭证规则可用");
        if (rule.F状态 != 1)
            return Fail($"插件规则「{rule.F规则名称}」已禁用，运行时不生成凭证");

        // 静态可预判②：ruleGroups 为空 → 运行时不生成凭证
        if (RuleGroupsEmpty(rule.F规则配置JSON))
            return Fail($"凭证规则「{rule.F规则名称}」未配置规则组（ruleGroups 为空），运行时不生成凭证");

        // 规则完整但真试算需运行时上下文 → 诚实降级（不伪造分录）
        return new VoucherPreviewDto { Success = false, Message = DegradeMessage };
    }

    private async Task<CfFlowVersion?> ResolveVersionAsync(
        long definitionId, long? flowVersionId, CancellationToken cancellationToken)
    {
        if (flowVersionId.HasValue)
        {
            return await _dbContext.Set<CfFlowVersion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.FID == flowVersionId.Value && v.FFlowDefinitionId == definitionId,
                    cancellationToken);
        }

        var draft = await _dbContext.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == definitionId && v.FStatus == "draft")
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (draft != null)
            return draft;

        return await _dbContext.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == definitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // 规则配置 JSON 的 ruleGroups（可能在根或 ruleConfig 下）为空/缺失 → true
    private static bool RuleGroupsEmpty(string? ruleConfigJson)
    {
        if (string.IsNullOrWhiteSpace(ruleConfigJson))
            return true;
        try
        {
            using var document = JsonDocument.Parse(ruleConfigJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return true;

            if (TryGetRuleGroups(root, out var direct))
                return direct.GetArrayLength() == 0;
            if (root.TryGetProperty("ruleConfig", out var ruleConfig)
                && ruleConfig.ValueKind == JsonValueKind.Object
                && TryGetRuleGroups(ruleConfig, out var nested))
                return nested.GetArrayLength() == 0;

            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool TryGetRuleGroups(JsonElement element, out JsonElement ruleGroups)
    {
        foreach (var name in new[] { "ruleGroups", "RuleGroups" })
        {
            if (element.TryGetProperty(name, out ruleGroups) && ruleGroups.ValueKind == JsonValueKind.Array)
                return true;
        }
        ruleGroups = default;
        return false;
    }

    private static VoucherPreviewDto Fail(string message)
        => new() { Success = false, Message = message };
}
