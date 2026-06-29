using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// AutoVoucher 规则跨账套科目重映射结果
/// </summary>
public class RemapResult
{
    public string RemappedJson { get; set; } = string.Empty;
    public List<string> MissingCodes { get; set; } = new();
    public int RemappedCount { get; set; }
}

/// <summary>
/// AutoVoucher 规则跨账套科目重映射解析器接口
/// </summary>
public interface IAccountMappingResolver
{
    /// <summary>根据科目编码在目标账套中反查 FID</summary>
    Task<long?> ResolveByCodeAsync(long accountSetId, string accountCode);

    /// <summary>根据科目 FID 在目标账套中反查编码</summary>
    Task<string?> ResolveCodeByIdAsync(long accountSetId, long accountId);

    /// <summary>遍历规则JSON中的所有科目位置，按accountCode在目标账套反查并覆盖accountId。</summary>
    Task<RemapResult> RemapRuleConfigAsync(string ruleConfigJson, long sourceAccountSetId, long targetAccountSetId);

    /// <summary>对规则JSON做"补齐accountCode"：accountId有值但accountCode为空时反查补齐。</summary>
    Task<string> EnsureAccountCodesAsync(string ruleConfigJson, long accountSetId);

    /// <summary>预加载目标账套的 code→FID 字典（供同步调用场景使用）</summary>
    Task<Dictionary<string, long>> LoadCodeToFidMapAsync(long accountSetId);

    /// <summary>预加载目标账套的 FID→code 字典（供同步调用场景使用）</summary>
    Task<Dictionary<long, string>> LoadFidToCodeMapAsync(long accountSetId);

    /// <summary>根据辅助核算项目编码在目标账套中反查 FID</summary>
    Task<long?> ResolveAuxItemByCodeAsync(long accountSetId, string code, string? auxType = null);

    /// <summary>根据辅助核算项目 FID 在目标账套中反查编码</summary>
    Task<string?> ResolveAuxItemCodeByIdAsync(long accountSetId, long itemId);

    /// <summary>预加载账套的辅助核算项目 code→FID 字典</summary>
    Task<Dictionary<string, long>> LoadAuxItemCodeToFidMapAsync(long accountSetId);

    /// <summary>预加载账套的辅助核算项目 FID→code 字典</summary>
    Task<Dictionary<long, string>> LoadAuxItemFidToCodeMapAsync(long accountSetId);
}

/// <summary>
/// AutoVoucher 规则跨账套科目重映射解析器实现
/// </summary>
public class AccountMappingResolver : IAccountMappingResolver
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<AccountMappingResolver> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AccountMappingResolver(STOTOPDbContext dbContext, ILogger<AccountMappingResolver> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<long?> ResolveByCodeAsync(long accountSetId, string accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode)) return null;
        var account = await _dbContext.Set<FinAccount>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FAccountSetId == accountSetId && a.FCode == accountCode);
        return account?.FID;
    }

    public async Task<string?> ResolveCodeByIdAsync(long accountSetId, long accountId)
    {
        var account = await _dbContext.Set<FinAccount>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FAccountSetId == accountSetId && a.FID == accountId);
        return account?.FCode;
    }

    public async Task<Dictionary<string, long>> LoadCodeToFidMapAsync(long accountSetId)
    {
        return await _dbContext.Set<FinAccount>()
            .AsNoTracking()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToDictionaryAsync(a => a.FCode, a => a.FID);
    }

    public async Task<Dictionary<long, string>> LoadFidToCodeMapAsync(long accountSetId)
    {
        return await _dbContext.Set<FinAccount>()
            .AsNoTracking()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToDictionaryAsync(a => a.FID, a => a.FCode);
    }

    public async Task<long?> ResolveAuxItemByCodeAsync(long accountSetId, string code, string? auxType = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var query = _dbContext.Set<FinAuxiliaryItem>()
            .AsNoTracking()
            .Where(a => a.FAccountSetId == accountSetId && a.FCode == code);
        if (!string.IsNullOrEmpty(auxType))
            query = query.Where(a => a.FAuxType == auxType);
        var item = await query.FirstOrDefaultAsync();
        return item?.FID;
    }

    public async Task<string?> ResolveAuxItemCodeByIdAsync(long accountSetId, long itemId)
    {
        var item = await _dbContext.Set<FinAuxiliaryItem>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FAccountSetId == accountSetId && a.FID == itemId);
        return item?.FCode;
    }

    public async Task<Dictionary<string, long>> LoadAuxItemCodeToFidMapAsync(long accountSetId)
    {
        var items = await _dbContext.Set<FinAuxiliaryItem>()
            .AsNoTracking()
            .Where(a => a.FAccountSetId == accountSetId && a.FCode != null && a.FCode != "")
            .Select(a => new { a.FCode, a.FID })
            .ToListAsync();
        // 同一编码可能在不同类型下重复，取第一个
        var dict = new Dictionary<string, long>();
        foreach (var a in items)
        {
            dict.TryAdd(a.FCode, a.FID);
        }
        return dict;
    }

    public async Task<Dictionary<long, string>> LoadAuxItemFidToCodeMapAsync(long accountSetId)
    {
        return await _dbContext.Set<FinAuxiliaryItem>()
            .AsNoTracking()
            .Where(a => a.FAccountSetId == accountSetId && a.FCode != null && a.FCode != "")
            .ToDictionaryAsync(a => a.FID, a => a.FCode);
    }

    public async Task<RemapResult> RemapRuleConfigAsync(string ruleConfigJson, long sourceAccountSetId, long targetAccountSetId)
    {
        var result = new RemapResult();

        if (string.IsNullOrWhiteSpace(ruleConfigJson))
        {
            result.RemappedJson = ruleConfigJson ?? string.Empty;
            return result;
        }

        RulesBasedVoucherConfigV2? config;
        try
        {
            config = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(ruleConfigJson, _jsonOptions);
        }
        catch (JsonException)
        {
            // 非 V2 格式，原样返回
            result.RemappedJson = ruleConfigJson;
            return result;
        }

        if (config == null)
        {
            result.RemappedJson = ruleConfigJson;
            return result;
        }

        // 加载源账套 FID→Code 和目标账套 Code→FID
        var sourceFidToCode = await LoadFidToCodeMapAsync(sourceAccountSetId);
        var targetCodeToFid = await LoadCodeToFidMapAsync(targetAccountSetId);

        // 加载辅助核算项目映射字典
        var sourceAuxFidToCode = await LoadAuxItemFidToCodeMapAsync(sourceAccountSetId);
        var targetAuxCodeToFid = await LoadAuxItemCodeToFidMapAsync(targetAccountSetId);

        // 遍历所有科目位置进行重映射
        foreach (var group in config.RuleGroups)
        {
            foreach (var line in group.Lines)
            {
                // 1. EntryLineV2.AccountId + AccountCode
                RemapAccountField(
                    line, sourceFidToCode, targetCodeToFid, result,
                    $"RuleGroup[{group.Name}].Line[{line.LineNo}]");

                // 2. EntryLineV2.DefaultAccountId（无对应 Code 字段，用源账套反查）
                if (line.DefaultAccountId.HasValue)
                {
                    var defaultCode = sourceFidToCode.TryGetValue(line.DefaultAccountId.Value, out var dc) ? dc : null;
                    if (!string.IsNullOrEmpty(defaultCode))
                    {
                        if (targetCodeToFid.TryGetValue(defaultCode, out var newFid))
                        {
                            line.DefaultAccountId = newFid;
                            result.RemappedCount++;
                        }
                        else
                        {
                            result.MissingCodes.Add($"DefaultAccount:{defaultCode}(RuleGroup[{group.Name}].Line[{line.LineNo}])");
                        }
                    }
                }

                // 3. AccountMatchItem.AccountId + AccountCode
                if (line.AccountMatchRules is { Count: > 0 })
                {
                    foreach (var matchItem in line.AccountMatchRules)
                    {
                        RemapMatchItem(
                            matchItem, sourceFidToCode, targetCodeToFid, result,
                            $"RuleGroup[{group.Name}].Line[{line.LineNo}].Match[{matchItem.MatchValue}]");
                    }
                }

                // 4. AuxiliaryConfigs 辅助核算项目 FixedItemId + FixedItemCode
                if (line.AuxiliaryConfigs is { Count: > 0 })
                {
                    foreach (var auxCfg in line.AuxiliaryConfigs)
                    {
                        RemapAuxiliaryItem(
                            auxCfg, sourceAuxFidToCode, targetAuxCodeToFid, result,
                            $"RuleGroup[{group.Name}].Line[{line.LineNo}].Aux[{auxCfg.AuxType}]");
                    }
                }
            }
        }

        // 更新目标账套ID
        config.AccountSetId = targetAccountSetId;

        result.RemappedJson = JsonSerializer.Serialize(config, _serializeOptions);
        return result;
    }

    public async Task<string> EnsureAccountCodesAsync(string ruleConfigJson, long accountSetId)
    {
        if (string.IsNullOrWhiteSpace(ruleConfigJson))
            return ruleConfigJson ?? string.Empty;

        RulesBasedVoucherConfigV2? config;
        try
        {
            config = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(ruleConfigJson, _jsonOptions);
        }
        catch (JsonException)
        {
            return ruleConfigJson;
        }

        if (config == null)
            return ruleConfigJson;

        // 加载账套 FID→Code 映射
        var fidToCode = await LoadFidToCodeMapAsync(accountSetId);
        var auxFidToCode = await LoadAuxItemFidToCodeMapAsync(accountSetId);
        bool modified = false;

        foreach (var group in config.RuleGroups)
        {
            foreach (var line in group.Lines)
            {
                // EntryLineV2.AccountId 有值但 AccountCode 为空 → 补齐
                if (line.AccountId.HasValue && string.IsNullOrEmpty(line.AccountCode))
                {
                    if (fidToCode.TryGetValue(line.AccountId.Value, out var code))
                    {
                        line.AccountCode = code;
                        modified = true;
                    }
                }

                // AccountMatchRules
                if (line.AccountMatchRules is { Count: > 0 })
                {
                    foreach (var matchItem in line.AccountMatchRules)
                    {
                        if (matchItem.AccountId > 0 && string.IsNullOrEmpty(matchItem.AccountCode))
                        {
                            if (fidToCode.TryGetValue(matchItem.AccountId, out var code))
                            {
                                matchItem.AccountCode = code;
                                modified = true;
                            }
                        }
                    }
                }

                // AuxiliaryConfigs: FixedItemId 有值且 FixedItemCode 为空 → 反查补齐
                if (line.AuxiliaryConfigs is { Count: > 0 })
                {
                    foreach (var auxCfg in line.AuxiliaryConfigs)
                    {
                        if (auxCfg.FixedItemId.HasValue && string.IsNullOrEmpty(auxCfg.FixedItemCode))
                        {
                            if (auxFidToCode.TryGetValue(auxCfg.FixedItemId.Value, out var auxCode))
                            {
                                auxCfg.FixedItemCode = auxCode;
                                modified = true;
                            }
                        }
                    }
                }
            }
        }

        if (!modified)
            return ruleConfigJson;

        return JsonSerializer.Serialize(config, _serializeOptions);
    }

    #region Private helpers

    private static void RemapAccountField(
        EntryLineV2 line,
        Dictionary<long, string> sourceFidToCode,
        Dictionary<string, long> targetCodeToFid,
        RemapResult result,
        string location)
    {
        if (!line.AccountId.HasValue && string.IsNullOrEmpty(line.AccountCode))
            return;

        // 确定编码：优先使用已有的 AccountCode，否则从源账套反查
        var code = line.AccountCode;
        if (string.IsNullOrEmpty(code) && line.AccountId.HasValue)
        {
            code = sourceFidToCode.TryGetValue(line.AccountId.Value, out var sc) ? sc : null;
        }

        if (string.IsNullOrEmpty(code))
            return;

        // 用编码在目标账套反查 FID
        if (targetCodeToFid.TryGetValue(code, out var newFid))
        {
            line.AccountId = newFid;
            line.AccountCode = code; // 保证双写
            result.RemappedCount++;
        }
        else
        {
            result.MissingCodes.Add($"{code}({location})");
        }
    }

    private static void RemapMatchItem(
        AccountMatchItem matchItem,
        Dictionary<long, string> sourceFidToCode,
        Dictionary<string, long> targetCodeToFid,
        RemapResult result,
        string location)
    {
        // 确定编码
        var code = matchItem.AccountCode;
        if (string.IsNullOrEmpty(code) && matchItem.AccountId > 0)
        {
            code = sourceFidToCode.TryGetValue(matchItem.AccountId, out var sc) ? sc : null;
        }

        if (string.IsNullOrEmpty(code))
            return;

        // 用编码在目标账套反查 FID
        if (targetCodeToFid.TryGetValue(code, out var newFid))
        {
            matchItem.AccountId = newFid;
            matchItem.AccountCode = code;
            result.RemappedCount++;
        }
        else
        {
            result.MissingCodes.Add($"{code}({location})");
        }
    }

    private static void RemapAuxiliaryItem(
        AuxiliaryConfigV2 auxCfg,
        Dictionary<long, string> sourceAuxFidToCode,
        Dictionary<string, long> targetAuxCodeToFid,
        RemapResult result,
        string location)
    {
        // 仅 FixedItemId 有值时才重映射；旧规则仅有 FixedValue 的保持原样
        if (!auxCfg.FixedItemId.HasValue)
            return;

        // 确定编码：优先使用已有 FixedItemCode，否则从源账套反查
        var code = auxCfg.FixedItemCode;
        if (string.IsNullOrEmpty(code))
        {
            code = sourceAuxFidToCode.TryGetValue(auxCfg.FixedItemId.Value, out var sc) ? sc : null;
        }

        if (string.IsNullOrEmpty(code))
        {
            result.MissingCodes.Add($"AuxItem:UnknownCode(FID={auxCfg.FixedItemId.Value})({location})");
            return;
        }

        // 用编码在目标账套反查新 FID
        if (targetAuxCodeToFid.TryGetValue(code, out var newFid))
        {
            auxCfg.FixedItemId = newFid;
            auxCfg.FixedItemCode = code; // 保证双写
            result.RemappedCount++;
        }
        else
        {
            result.MissingCodes.Add($"AuxItem:{code}({location})");
        }
    }

    #endregion
}
