using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

public class MigrationMappingService
{
    private readonly STOTOPDbContext _ctx;

    public MigrationMappingService(STOTOPDbContext ctx)
    {
        _ctx = ctx;
    }

    #region 加载映射到内存

    /// <summary>
    /// 加载科目映射（按源科目编码分组）
    /// </summary>
    public async Task<Dictionary<string, List<FinAccountMappingDetail>>> LoadAccountMappingsAsync(Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAccountMappingDetail>()
            .Where(m => m.F方案ID == schemeId && m.F状态 == 1)
            .OrderBy(m => m.F优先级)
            .AsNoTracking()
            .ToListAsync();

        return mappings.GroupBy(m => m.F源科目编码)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 加载辅助映射（按类型+编码组合键）
    /// </summary>
    public async Task<Dictionary<(string type, string code), FinAuxMappingDetail>> LoadAuxMappingsAsync(Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAuxMappingDetail>()
            .Where(m => m.F方案ID == schemeId && m.F状态 == 1)
            .AsNoTracking()
            .ToListAsync();

        return mappings.ToDictionary(m => (m.F辅助类型, m.F源编码));
    }

    /// <summary>
    /// 加载资产映射（按源资产编号索引）
    /// </summary>
    public async Task<Dictionary<string, FinAssetMappingDetail>> LoadAssetMappingsAsync(Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAssetMappingDetail>()
            .Where(m => m.F方案ID == schemeId && m.F状态 == 1)
            .AsNoTracking()
            .ToListAsync();

        return mappings.ToDictionary(m => m.F源资产编号);
    }

    #endregion

    #region 解析方法

    /// <summary>
    /// 科目映射解析：查找源科目编码对应的目标科目
    /// 1. 查映射字典中 sourceCode 对应的列表，按 F优先级 升序
    /// 2. 遍历：类型=条件(2) → 检查 F条件JSON 是否匹配 → 类型=直接(1) → 直接返回
    /// 3. 无匹配 → 返回 null
    /// </summary>
    public (long? targetId, string targetCode, string targetName)? ResolveAccount(
        string sourceCode,
        Dictionary<string, List<FinAccountMappingDetail>> mappings,
        string? summary = null,
        string? direction = null,
        decimal? amount = null)
    {
        if (!mappings.TryGetValue(sourceCode, out var candidates))
            return null;

        foreach (var mapping in candidates)
        {
            if (mapping.F映射类型 == 2)
            {
                // 条件映射：解析条件JSON进行匹配
                if (MatchCondition(mapping.F条件JSON, summary, direction, amount))
                {
                    return (mapping.F目标科目ID, mapping.F目标科目编码, mapping.F目标科目名称);
                }
            }
            else
            {
                // 直接映射
                return (mapping.F目标科目ID, mapping.F目标科目编码, mapping.F目标科目名称);
            }
        }

        return null;
    }

    /// <summary>
    /// 辅助核算映射解析
    /// </summary>
    public string? ResolveAuxiliary(
        string auxType,
        string sourceCode,
        Dictionary<(string, string), FinAuxMappingDetail> mappings,
        string defaultStrategy)
    {
        if (mappings.TryGetValue((auxType, sourceCode), out var mapping))
        {
            return mapping.F目标编码;
        }

        // 未找到映射，根据策略返回
        var strategy = defaultStrategy;
        return strategy switch
        {
            "ignore" => null,
            "create" => sourceCode, // 原样返回，由上层创建
            _ => throw new InvalidOperationException($"辅助核算映射缺失: 类型={auxType}, 编码={sourceCode}")
        };
    }

    /// <summary>
    /// 资产映射解析
    /// </summary>
    public long? ResolveAsset(string sourceAssetCode, Dictionary<string, FinAssetMappingDetail> mappings)
    {
        if (mappings.TryGetValue(sourceAssetCode, out var mapping))
        {
            return mapping.F目标资产卡片ID;
        }
        return null;
    }

    #endregion

    #region 向导：科目自动匹配

    /// <summary>
    /// 自动匹配源科目到目标账套科目
    /// 匹配策略：编码精确 → 名称精确 → 名称模糊（去掉分隔符比较）
    /// </summary>
    public async Task<MigrationAutoMatchResponse> AutoMatchSubjectsAsync(
        Guid schemeId,
        long targetAccountSetId,
        List<SourceSubjectInfo> sourceSubjects)
    {
        // 加载目标账套的所有科目
        var targetAccounts = await _ctx.Set<FinAccount>()
            .Where(a => a.FAccountSetId == targetAccountSetId && a.FEnableStatus == 1)
            .AsNoTracking()
            .ToListAsync();

        var codeIndex = targetAccounts.ToDictionary(a => a.FCode, a => a);
        var nameIndex = targetAccounts
            .GroupBy(a => a.FName)
            .ToDictionary(g => g.Key, g => g.First());
        var normalizedNameIndex = targetAccounts
            .GroupBy(a => NormalizeName(a.FName))
            .ToDictionary(g => g.Key, g => g.First());

        var matches = new List<MigrationAutoMatchResult>();
        var unmatched = new List<SourceSubjectInfo>();

        foreach (var source in sourceSubjects)
        {
            // 1. 编码精确匹配
            if (codeIndex.TryGetValue(source.Code, out var byCode))
            {
                matches.Add(new MigrationAutoMatchResult
                {
                    SourceCode = source.Code,
                    SourceName = source.Name,
                    TargetId = byCode.FID,
                    TargetCode = byCode.FCode,
                    TargetName = byCode.FName,
                    Confidence = "exact_code"
                });
                continue;
            }

            // 2. 名称精确匹配
            if (!string.IsNullOrEmpty(source.Name) && nameIndex.TryGetValue(source.Name, out var byName))
            {
                matches.Add(new MigrationAutoMatchResult
                {
                    SourceCode = source.Code,
                    SourceName = source.Name,
                    TargetId = byName.FID,
                    TargetCode = byName.FCode,
                    TargetName = byName.FName,
                    Confidence = "exact_name"
                });
                continue;
            }

            // 3. 名称模糊匹配（去掉分隔符）
            if (!string.IsNullOrEmpty(source.Name))
            {
                var normalized = NormalizeName(source.Name);
                if (normalizedNameIndex.TryGetValue(normalized, out var byFuzzy))
                {
                    matches.Add(new MigrationAutoMatchResult
                    {
                        SourceCode = source.Code,
                        SourceName = source.Name,
                        TargetId = byFuzzy.FID,
                        TargetCode = byFuzzy.FCode,
                        TargetName = byFuzzy.FName,
                        Confidence = "fuzzy_name"
                    });
                    continue;
                }
            }

            // 未匹配
            matches.Add(new MigrationAutoMatchResult
            {
                SourceCode = source.Code,
                SourceName = source.Name,
                Confidence = "none"
            });
            unmatched.Add(source);
        }

        return new MigrationAutoMatchResponse { Matches = matches, Unmatched = unmatched };
    }

    #endregion

    #region 向导：提交保存配置

    /// <summary>
    /// 保存向导配置（科目映射、辅助映射、资产映射）
    /// </summary>
    public async Task CommitWizardAsync(WizardCommitRequest request)
    {
        var now = DateTime.Now;

        // 清除旧映射
        var oldAccountMappings = await _ctx.Set<FinAccountMappingDetail>()
            .Where(m => m.F方案ID == request.SchemeId)
            .ToListAsync();
        _ctx.Set<FinAccountMappingDetail>().RemoveRange(oldAccountMappings);

        var oldAuxMappings = await _ctx.Set<FinAuxMappingDetail>()
            .Where(m => m.F方案ID == request.SchemeId)
            .ToListAsync();
        _ctx.Set<FinAuxMappingDetail>().RemoveRange(oldAuxMappings);

        var oldAssetMappings = await _ctx.Set<FinAssetMappingDetail>()
            .Where(m => m.F方案ID == request.SchemeId)
            .ToListAsync();
        _ctx.Set<FinAssetMappingDetail>().RemoveRange(oldAssetMappings);

        // 新增科目映射
        foreach (var item in request.AccountMappings)
        {
            _ctx.Set<FinAccountMappingDetail>().Add(new FinAccountMappingDetail
            {
                FID = Guid.NewGuid(),
                F方案ID = request.SchemeId,
                F源科目编码 = item.SourceCode,
                F源科目名称 = item.SourceName,
                F目标科目ID = item.TargetAccountId,
                F目标科目编码 = item.TargetCode,
                F目标科目名称 = item.TargetName,
                F映射类型 = item.MappingType,
                F条件JSON = item.ConditionJson,
                F优先级 = item.Priority,
                F说明 = item.Description,
                F状态 = 1,
                F创建时间 = now
            });
        }

        // 新增辅助映射
        foreach (var item in request.AuxMappings)
        {
            _ctx.Set<FinAuxMappingDetail>().Add(new FinAuxMappingDetail
            {
                FID = Guid.NewGuid(),
                F方案ID = request.SchemeId,
                F辅助类型 = item.AuxType,
                F源编码 = item.SourceCode,
                F源名称 = item.SourceName,
                F目标辅助项目ID = item.TargetAuxItemId,
                F目标编码 = item.TargetCode,
                F目标名称 = item.TargetName,
                F处理策略 = item.Strategy,
                F状态 = 1,
                F创建时间 = now
            });
        }

        // 新增资产映射
        foreach (var item in request.AssetMappings)
        {
            _ctx.Set<FinAssetMappingDetail>().Add(new FinAssetMappingDetail
            {
                FID = Guid.NewGuid(),
                F方案ID = request.SchemeId,
                F源资产编号 = item.SourceAssetCode,
                F目标资产卡片ID = item.TargetAssetCardId,
                F目标资产编号 = item.TargetAssetCode,
                F目标资产名称 = item.TargetAssetName,
                F状态 = 1,
                F创建时间 = now
            });
        }

        // 更新方案的更新时间
        var scheme = await _ctx.Set<FinMigrationScheme>()
            .FirstOrDefaultAsync(s => s.FID == request.SchemeId);
        if (scheme != null)
        {
            scheme.F更新时间 = now;
        }

        await _ctx.SaveChangesAsync();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 匹配条件JSON
    /// 支持格式: {"summary":"xxx","direction":"debit|credit","amountMin":0,"amountMax":999}
    /// </summary>
    private static bool MatchCondition(string? conditionJson, string? summary, string? direction, decimal? amount)
    {
        if (string.IsNullOrEmpty(conditionJson))
            return false;

        try
        {
            var condition = JsonSerializer.Deserialize<JsonElement>(conditionJson);

            // 匹配摘要关键字
            if (condition.TryGetProperty("summary", out var summaryProp))
            {
                var keyword = summaryProp.GetString();
                if (!string.IsNullOrEmpty(keyword) && !string.IsNullOrEmpty(summary))
                {
                    if (!summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (!string.IsNullOrEmpty(keyword))
                {
                    return false;
                }
            }

            // 匹配方向
            if (condition.TryGetProperty("direction", out var dirProp))
            {
                var dir = dirProp.GetString();
                if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(direction))
                {
                    if (!dir.Equals(direction, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (!string.IsNullOrEmpty(dir))
                {
                    return false;
                }
            }

            // 匹配金额范围
            if (amount.HasValue)
            {
                if (condition.TryGetProperty("amountMin", out var minProp) && minProp.TryGetDecimal(out var min))
                {
                    if (amount.Value < min) return false;
                }
                if (condition.TryGetProperty("amountMax", out var maxProp) && maxProp.TryGetDecimal(out var max))
                {
                    if (amount.Value > max) return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 标准化名称用于模糊匹配（去除分隔符、空格、特殊字符）
    /// </summary>
    private static string NormalizeName(string name)
    {
        return Regex.Replace(name, @"[\s\-_/\\（）()【】\[\]·.。、]", "");
    }

    #endregion
}
