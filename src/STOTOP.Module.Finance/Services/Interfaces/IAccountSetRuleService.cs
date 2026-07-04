using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services.Interfaces;

/// <summary>账套规则默认值（无配置时的回退口径 = 历史写死行为）</summary>
public static class AccountSetRuleDefaults
{
    /// <summary>本年利润科目编码（历史写死值）</summary>
    public const string ProfitAccountCode = "3103";
    /// <summary>利润分配-未分配利润科目编码（历史写死值）</summary>
    public const string RetainedAccountCode = "310405";
}

public interface IAccountSetRuleService
{
    /// <summary>按账套读规则实体；null = 无配置（调用方按 fail-safe 回退现状）。</summary>
    Task<FinAccountSetRule?> GetByAccountSetAsync(long accountSetId);

    /// <summary>前端读取：无行时返回默认值 DTO（开关关/编码空/凭证字全集）。</summary>
    Task<AccountSetRuleDto> GetDtoAsync(long accountSetId);

    /// <summary>P0-3 启用凭证字集合；无配置/空回退全集，且强制包含"记"。</summary>
    Task<string[]> GetEnabledVoucherWordsAsync(long accountSetId);

    /// <summary>P0-2 结转目标科目编码（本年利润, 未分配利润）；空配置回退默认字面量。</summary>
    Task<(string profitCode, string retainedCode)> GetClosingAccountCodesAsync(long accountSetId);

    /// <summary>保存（一账套一行 Upsert）；校验凭证字合法子集、结转科目在账套内存在。</summary>
    Task<AccountSetRuleDto> UpsertAsync(long accountSetId, UpdateAccountSetRuleRequest request, string operatorName);
}
