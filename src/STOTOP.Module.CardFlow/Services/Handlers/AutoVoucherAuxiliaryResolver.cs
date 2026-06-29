using STOTOP.Module.CardFlow.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Services.Handlers;

/// <summary>
/// AutoVoucher V2 辅助核算解析器
/// <para>支持多类型（customer/supplier/department/project/employee/business_unit/express_brand）</para>
/// <para>支持 fixed/dynamic 两种来源模式</para>
/// <para>纯逻辑类，不直接依赖 DbContext，由调用方预加载数据传入 Initialize</para>
/// </summary>
public class AutoVoucherAuxiliaryResolver
{
    private readonly ILogger _logger;

    /// <summary>
    /// [I2] 按 AuxType 预加载到内存，避免逐行查询
    /// </summary>
    private Dictionary<string, List<AuxiliaryItemInfo>> _auxItemsByType = new();

    /// <summary>
    /// 初始化解析器
    /// </summary>
    /// <param name="logger">日志记录器（由调用方传入，不指定泛型）</param>
    public AutoVoucherAuxiliaryResolver(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// [E9][I2] 预加载辅助核算项目（含组织隔离和状态过滤）
    /// <para>在匹配流程开始前一次性调用，后续 ResolveAuxiliary 调用均从内存字典中查找</para>
    /// </summary>
    /// <param name="items">已从数据库查询的辅助核算项目（已过滤 FEnableStatus==1 且 FOrgId 匹配）</param>
    public void Initialize(IEnumerable<AuxiliaryItemInfo> items)
    {
        _auxItemsByType = items
            .GroupBy(a => a.AuxType)
            .ToDictionary(g => g.Key, g => g.ToList());

        _logger.LogDebug("辅助核算预加载完成，共 {Count} 条记录，{Types} 种类型",
            _auxItemsByType.Values.Sum(v => v.Count),
            _auxItemsByType.Keys.Count);
    }

    /// <summary>
    /// 解析单条分录行的所有辅助核算配置
    /// <para>遍历 configs 中每个 AuxiliaryConfigV2，根据 SourceType(fixed/dynamic) 和 MatchBy 策略
    /// 在预加载的辅助核算项目中查找匹配项，返回解析结果列表</para>
    /// </summary>
    /// <param name="row">当前数据行（字典形式，dynamic 模式从中取字段值）</param>
    /// <param name="configs">分录行的辅助核算配置列表（来自 EntryLineV2.AuxiliaryConfigs）</param>
    /// <returns>解析结果列表（JSON序列化后存入 FAuxiliaryJson）</returns>
    public List<AuxiliaryResult> ResolveAuxiliary(
        IDictionary<string, object> row,
        List<AuxiliaryConfigV2>? configs)
    {
        if (configs == null || configs.Count == 0)
            return new List<AuxiliaryResult>();

        var results = new List<AuxiliaryResult>();

        foreach (var auxConfig in configs)
        {
            var candidates = _auxItemsByType.TryGetValue(auxConfig.AuxType, out var list)
                ? list : new List<AuxiliaryItemInfo>();

            AuxiliaryItemInfo? resolvedItem = null;

            if (auxConfig.SourceType == "fixed")
            {
                resolvedItem = ResolveFixed(auxConfig, candidates);
            }
            else // dynamic
            {
                resolvedItem = ResolveDynamic(auxConfig, row, candidates);
            }

            if (resolvedItem != null)
            {
                results.Add(new AuxiliaryResult
                {
                    Type = auxConfig.AuxType,
                    Id = resolvedItem.Id,
                    Code = resolvedItem.Code,
                    Name = resolvedItem.Name
                });
            }
            else
            {
                // 未匹配时记录 warning 但不阻断（[D9] UnmatchedAction 在上层处理）
                _logger.LogWarning(
                    "辅助核算[{AuxType}]未匹配: SourceType={SourceType}, MatchBy={MatchBy}, SourceField={Field}, Value={Value}",
                    auxConfig.AuxType, auxConfig.SourceType, auxConfig.MatchBy,
                    auxConfig.SourceField,
                    auxConfig.SourceType == "fixed" ? auxConfig.FixedValue
                        : row.TryGetValue(auxConfig.SourceField ?? "", out var logVal) ? logVal?.ToString() : null);
            }
        }

        return results;
    }

    /// <summary>
    /// fixed 模式解析：按 FixedItemId 或 FixedValue（编码/名称）查找
    /// </summary>
    private AuxiliaryItemInfo? ResolveFixed(AuxiliaryConfigV2 auxConfig, List<AuxiliaryItemInfo> candidates)
    {
        if (auxConfig.FixedItemId.HasValue)
        {
            return candidates.FirstOrDefault(a => a.Id == auxConfig.FixedItemId.Value);
        }

        if (!string.IsNullOrEmpty(auxConfig.FixedValue))
        {
            return candidates.FirstOrDefault(a =>
                a.Code == auxConfig.FixedValue || a.Name == auxConfig.FixedValue);
        }

        return null;
    }

    /// <summary>
    /// dynamic 模式解析：从源数据行取字段值，按 MatchBy 策略匹配
    /// <para>MatchBy 策略说明：</para>
    /// <para>- code: 源字段值按 Code 精确匹配</para>
    /// <para>- name: 源字段值按 Name 精确匹配</para>
    /// <para>- keyword: 源字段值包含项目名（最长优先，避免短名称误匹配）</para>
    /// <para>- contains: 项目名包含源字段值（最短优先=最精确匹配）</para>
    /// </summary>
    private AuxiliaryItemInfo? ResolveDynamic(
        AuxiliaryConfigV2 auxConfig,
        IDictionary<string, object> row,
        List<AuxiliaryItemInfo> candidates)
    {
        // [D11] Null 安全检查
        var fieldValue = row.TryGetValue(auxConfig.SourceField ?? "", out var rawVal)
            ? rawVal?.ToString()?.Trim() : null;

        if (string.IsNullOrEmpty(fieldValue))
            return null;

        return auxConfig.MatchBy switch
        {
            // code / exact_code: 源字段值按 Code 精确匹配辅助核算项目
            "code" or "exact_code" => candidates.FirstOrDefault(a => a.Code == fieldValue),

            // name / exact_name: 源字段值按 Name 精确匹配辅助核算项目
            "name" or "exact_name" => candidates.FirstOrDefault(a => a.Name == fieldValue),

            // keyword / source_contains_name: 源数据包含项目名（最长优先，避免"城区"先于"南郊城区"匹配）
            "keyword" or "source_contains_name" => candidates
                .Where(a => !string.IsNullOrEmpty(a.Name) && fieldValue.Contains(a.Name))
                .OrderByDescending(a => a.Name!.Length)
                .FirstOrDefault(),

            // [D10] contains / name_contains_source: 项目名包含源数据（最短优先=最精确匹配）
            "contains" or "name_contains_source" => candidates
                .Where(a => !string.IsNullOrEmpty(a.Name) && a.Name.Contains(fieldValue))
                .OrderBy(a => a.Name!.Length)
                .FirstOrDefault(),

            _ => null
        };
    }
}

/// <summary>
/// 辅助核算项目信息（从数据库预加载的DTO）
/// <para>由调用方从 AuxiliaryItem 表查询并过滤后传入 Initialize</para>
/// </summary>
public class AuxiliaryItemInfo
{
    /// <summary>辅助核算项目ID（对应 FID）</summary>
    public long Id { get; set; }

    /// <summary>辅助类型编码（对应 FAuxType，如 customer/supplier/department 等）</summary>
    public string AuxType { get; set; } = string.Empty;

    /// <summary>辅助核算项目编码（对应 FCode）</summary>
    public string? Code { get; set; }

    /// <summary>辅助核算项目名称（对应 FName）</summary>
    public string? Name { get; set; }
}

/// <summary>
/// 辅助核算解析结果（序列化到 FAuxiliaryJson）
/// <para>每条结果对应一个辅助核算类型的匹配项</para>
/// </summary>
public class AuxiliaryResult
{
    /// <summary>辅助类型编码（如 customer/supplier/department 等）</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>匹配到的辅助核算项目ID</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>匹配到的辅助核算项目编码</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>匹配到的辅助核算项目名称</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
