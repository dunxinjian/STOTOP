using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using STOTOP.Core.Models;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/auto-plugin-rules")]
public class CfAutoPluginRuleController : ControllerBase
{
    private readonly STOTOPDbContext _context;
    private readonly AutoVoucherHandler _autoVoucherHandler;
    private readonly IOrgContextAccessor _orgContextAccessor;
    public CfAutoPluginRuleController(
        STOTOPDbContext context,
        AutoVoucherHandler autoVoucherHandler,
        IOrgContextAccessor orgContextAccessor)
    {
        _context = context;
        _autoVoucherHandler = autoVoucherHandler;
        _orgContextAccessor = orgContextAccessor;
    }

    /// <summary>按类型查规则列表，typeCode 可选，支持分页，可选 ?orgId= 查看指定组织的规则</summary>
    [HttpGet]
    public async Task<IActionResult> GetRules(
        [FromQuery] string? typeCode,
        [FromQuery] bool includeConfig = false,
        [FromQuery] int? pageIndex = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? orgId = null)
    {
        // 可选临时切换组织上下文，让 IOrgScoped 过滤器返回目标组织的数据
        var savedOrgId = _orgContextAccessor.CurrentOrgId;
        if (orgId.HasValue)
            _orgContextAccessor.CurrentOrgId = orgId.Value;

        try
        {
            var query = _context.Set<CfPluginRule>().AsQueryable();
            if (!string.IsNullOrWhiteSpace(typeCode))
                query = query.Where(r => r.F类型编码 == typeCode);

            // Step 1: 批量获取引用计数（消除 N+1）
            var ruleIds = await query.Select(r => r.FID).ToListAsync();
            var refCounts = await _context.Set<CfPluginDef>()
                .Where(a => a.F规则ID != null && ruleIds.Contains(a.F规则ID!.Value))
                .GroupBy(a => a.F规则ID)
                .Select(g => new { RuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RuleId!.Value, x => x.Count);

            // Step 2: 主查询（可选分页）
            IQueryable<CfPluginRule> finalQuery = query.OrderByDescending(r => r.F创建时间);
            if (pageIndex.HasValue)
                finalQuery = finalQuery.Skip((pageIndex.Value - 1) * pageSize).Take(pageSize);

            var entities = await finalQuery.ToListAsync();
            var rules = entities.Select(r => new PluginRuleDto
            {
                Id = r.FID,
                OrgId = r.FOrgId,
                TypeCode = r.F类型编码,
                RuleName = r.F规则名称,
                ConfigJson = includeConfig ? r.F规则配置JSON : null,
                Status = r.F状态,
                Description = r.F说明,
                ReferenceCount = refCounts.GetValueOrDefault(r.FID, 0),
                ConcurrencyStamp = r.FConcurrencyStamp,
                CreateTime = r.F创建时间.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdateTime = r.F更新时间?.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList();

            return Ok(ApiResult<List<PluginRuleDto>>.Success(rules));
        }
        finally
        {
            if (orgId.HasValue)
                _orgContextAccessor.CurrentOrgId = savedOrgId;
        }
    }

    /// <summary>获取规则详情（包含完整 ConfigJson 和 ConcurrencyStamp）</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRule(long id)
    {
        var rule = await _context.Set<CfPluginRule>().FindAsync(id);
        if (rule == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        var refCount = await _context.Set<CfPluginDef>().CountAsync(a => a.F规则ID == id);

        var dto = new PluginRuleDto
        {
            Id = rule.FID,
            OrgId = rule.FOrgId,
            TypeCode = rule.F类型编码,
            RuleName = rule.F规则名称,
            ConfigJson = rule.F规则配置JSON,
            Status = rule.F状态,
            Description = rule.F说明,
            ReferenceCount = refCount,
            ConcurrencyStamp = rule.FConcurrencyStamp,
            CreateTime = rule.F创建时间.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdateTime = rule.F更新时间?.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return Ok(ApiResult<PluginRuleDto>.Success(dto));
    }

    /// <summary>创建规则</summary>
    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] CreatePluginRuleRequest request)
    {
        var entity = new CfPluginRule
        {
            F类型编码 = request.TypeCode,
            F规则名称 = request.RuleName,
            F规则配置JSON = request.ConfigJson,
            F说明 = request.Description,
            F状态 = 1,
            F创建时间 = DateTime.Now
        };
        _context.Set<CfPluginRule>().Add(entity);
        await _context.SaveChangesAsync();

        return Ok(ApiResult<PluginRuleDto>.Success(new PluginRuleDto
        {
            Id = entity.FID,
            OrgId = entity.FOrgId,
            TypeCode = entity.F类型编码,
            RuleName = entity.F规则名称,
            ConfigJson = entity.F规则配置JSON,
            Status = entity.F状态,
            Description = entity.F说明,
            ReferenceCount = 0,
            ConcurrencyStamp = entity.FConcurrencyStamp,
            CreateTime = entity.F创建时间.ToString("yyyy-MM-dd HH:mm:ss")
        }, "创建成功"));
    }

    /// <summary>更新规则（并发保护：业务层 + DB层双重乐观锁）</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] UpdatePluginRuleRequest request)
    {
        var entity = await _context.Set<CfPluginRule>().AsTracking().FirstOrDefaultAsync(r => r.FID == id);
        if (entity == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        // ① 业务层并发检查
        if (!string.IsNullOrEmpty(request.ConcurrencyStamp) && entity.FConcurrencyStamp != request.ConcurrencyStamp)
            return Conflict(ApiResult.Fail("规则已被其他用户修改，请刷新后重试"));

        // AutoVoucher 类型必须有有效的规则配置
        if (entity.F类型编码 == "AutoVoucher" && !string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            try
            {
                var avConfig = JsonSerializer.Deserialize<JsonElement>(request.ConfigJson);
                // 检查 ruleGroups 是否为空数组
                if (avConfig.TryGetProperty("ruleGroups", out var ruleGroups)
                    && ruleGroups.ValueKind == JsonValueKind.Array
                    && ruleGroups.GetArrayLength() == 0)
                {
                    return Ok(ApiResult.Fail("AutoVoucher 规则的 ruleGroups 不能为空"));
                }
            }
            catch (JsonException)
            {
                // JSON 格式错误时由其他地方处理
            }
        }

        // 复合键漂移处理：检测旧配置中消失的 (groupIndex, lineNo) 并标记失效
        await InvalidateDisappearedHitStats(id, entity.F规则配置JSON, request.ConfigJson);

        entity.F规则名称 = request.RuleName;
        entity.F规则配置JSON = request.ConfigJson;
        entity.F说明 = request.Description;
        entity.F状态 = request.Status;
        if (!string.IsNullOrWhiteSpace(request.TypeCode))
            entity.F类型编码 = request.TypeCode;
        entity.F更新时间 = DateTime.Now;
        entity.FConcurrencyStamp = Guid.NewGuid().ToString("N");

        // ② DB层安全网（EF Core ConcurrencyToken）
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResult.Fail("规则已被其他用户修改，请刷新后重试"));
        }

        return Ok(ApiResult<PluginRuleDto>.Success(new PluginRuleDto
        {
            Id = entity.FID,
            OrgId = entity.FOrgId,
            TypeCode = entity.F类型编码,
            RuleName = entity.F规则名称,
            ConfigJson = entity.F规则配置JSON,
            Status = entity.F状态,
            Description = entity.F说明,
            ConcurrencyStamp = entity.FConcurrencyStamp,
            CreateTime = entity.F创建时间.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdateTime = entity.F更新时间?.ToString("yyyy-MM-dd HH:mm:ss")
        }, "更新成功"));
    }

    /// <summary>删除规则（有引用保护）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteRule(long id)
    {
        var entity = await _context.Set<CfPluginRule>().FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        var refCount = await _context.Set<CfPluginDef>().CountAsync(a => a.F规则ID == id);
        if (refCount > 0)
            return Ok(ApiResult.Fail($"该规则被 {refCount} 条管道节点引用，无法删除"));

        _context.Set<CfPluginRule>().Remove(entity);
        await _context.SaveChangesAsync();

        return Ok(ApiResult.Ok("删除成功"));
    }

    /// <summary>获取规则命中率统计</summary>
    [HttpGet("{id:long}/hit-stats")]
    public async Task<IActionResult> GetHitStats(long id, [FromQuery] int days = 7)
    {
        var since = DateTime.Now.AddDays(-days);
        var stats = await _context.Set<CfPluginRuleHitStat>()
            .Where(s => s.FRuleId == id && !s.FInvalidated && s.FStatTime >= since)
            .GroupBy(s => new { s.FRuleGroupIndex, s.FEntryLineNo })
            .Select(g => new
            {
                GroupIndex = g.Key.FRuleGroupIndex,
                LineNo = g.Key.FEntryLineNo,
                TotalHits = g.Sum(x => x.FHitRowCount),
                TotalMisses = g.Sum(x => x.FMissRowCount)
            })
            .ToListAsync();
        return Ok(ApiResult<object>.Success(stats));
    }

    /// <summary>按类型获取启用的规则（下拉选用，简化字段）</summary>
    [HttpGet("by-type/{typeCode}")]
    public async Task<IActionResult> GetRulesByType(string typeCode)
    {
        var list = await _context.Set<CfPluginRule>()
            .Where(r => r.F类型编码 == typeCode && r.F状态 == 1 && r.FOrgId > 0)
            .OrderBy(r => r.F规则名称)
            .Select(r => new PluginRuleSelectDto
            {
                Id = r.FID,
                RuleName = r.F规则名称,
                OrgId = r.FOrgId
            })
            .ToListAsync();

        return Ok(ApiResult<List<PluginRuleSelectDto>>.Success(list));
    }

    /// <summary>获取规则对应的自动插件配置参数 Schema</summary>
    [HttpGet("{id:long}/params-schema")]
    public async Task<IActionResult> GetRuleParamsSchema(long id)
    {
        var rule = await _context.Set<CfPluginRule>().FindAsync(id);
        if (rule == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        // AgentFactory 已废弃，参数 Schema 功能暂不可用
        return Ok(ApiResult<object>.Success(new { parameters = Array.Empty<object>() }));
    }

    /// <summary>DryRun 预演（新规则，无 id）</summary>
    [HttpPost("dry-run")]
    public async Task<IActionResult> DryRunNew([FromBody] DryRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfigJson))
            return BadRequest(ApiResult.Fail("新规则预演必须提供 configJson"));

        return await ExecuteDryRunAsync(request.BatchId, request.ConfigJson, request.GroupField);
    }

    /// <summary>DryRun 预演（已保存规则）</summary>
    [HttpPost("{id:long}/dry-run")]
    public async Task<IActionResult> DryRun(long id, [FromBody] DryRunRequest request)
    {
        var rule = await _context.Set<CfPluginRule>().FindAsync(id);
        if (rule == null) return NotFound(ApiResult.Fail("规则不存在"));

        var configJson = !string.IsNullOrWhiteSpace(request.ConfigJson)
            ? request.ConfigJson
            : rule.F规则配置JSON;

        if (string.IsNullOrWhiteSpace(configJson))
            return BadRequest(ApiResult.Fail("规则配置为空，无法预演"));

        return await ExecuteDryRunAsync(request.BatchId, configJson, request.GroupField);
    }

    private async Task<IActionResult> ExecuteDryRunAsync(long batchId, string configJson, string? groupField = null)
    {
        // 1. 解析 configJson 为 V2 配置
        RulesBasedVoucherConfigV2? config;
        try
        {
            config = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResult.Fail($"configJson 解析失败: {ex.Message}"));
        }

        if (config == null)
            return BadRequest(ApiResult.Fail("configJson 解析结果为空"));
        if (string.IsNullOrWhiteSpace(config.StagingTable))
            return BadRequest(ApiResult.Fail("配置中缺少 stagingTable"));
        if ((config.AccountSetId ?? 0) == 0)
            return BadRequest(ApiResult.Fail("配置中缺少 accountSetId"));

        // 2. 查找批次信息以获取 OrgId
        var batch = await _context.Set<CfBatch>().FindAsync(batchId);

        // 3. 调用 V2 DryRun
        try
        {
            var result = await _autoVoucherHandler.HandleV2DryRunAsync(
                config, batchId, batch?.FOrgId ?? 0, groupField);
            return Ok(ApiResult<DryRunResult>.Success(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResult.Fail($"预演执行失败: {ex.Message}"));
        }
    }

    /// <summary>复制指定规则到当前组织（支持从任意组织复制）</summary>
    [HttpPost("{id}/copy")]
    public async Task<IActionResult> CopyRule(long id)
    {
        var source = await _context.Set<CfPluginRule>().AsNoTracking().FirstOrDefaultAsync(r => r.FID == id);
        if (source == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        var currentOrgId = _orgContextAccessor.CurrentOrgId;
        var copy = new CfPluginRule
        {
            F规则名称 = source.F规则名称 + " (副本)",
            F类型编码 = source.F类型编码,
            F规则配置JSON = source.F规则配置JSON,
            F说明 = source.F说明,
            FOrgId = currentOrgId ?? 0,
            F状态 = 1,
            F创建时间 = DateTime.Now
        };

        _context.Set<CfPluginRule>().Add(copy);
        await _context.SaveChangesAsync();

        return Ok(ApiResult<object>.Success(new { id = copy.FID }, "规则复制成功"));
    }

    /// <summary>复合键漂移处理：检测 configJson 中消失的 (groupIndex, lineNo) 并标记失效</summary>
    private async Task InvalidateDisappearedHitStats(long ruleId, string? oldConfigJson, string? newConfigJson)
    {
        var oldKeys = ExtractGroupLineKeys(oldConfigJson);
        var newKeys = ExtractGroupLineKeys(newConfigJson);

        // 找出消失的组合
        var disappeared = oldKeys.Except(newKeys).ToList();
        if (disappeared.Count == 0) return;

        // 批量标记失效
        var groupIndices = disappeared.Select(k => k.GroupIndex).Distinct().ToList();
        var lineNos = disappeared.Select(k => k.LineNo).Distinct().ToList();

        // 使用筛选后再精确匹配
        var candidates = await _context.Set<CfPluginRuleHitStat>()
            .Where(s => s.FRuleId == ruleId && !s.FInvalidated
                && groupIndices.Contains(s.FRuleGroupIndex)
                && lineNos.Contains(s.FEntryLineNo))
            .ToListAsync();

        var disappearedSet = new HashSet<(int, int)>(disappeared.Select(k => (k.GroupIndex, k.LineNo)));
        var toInvalidate = candidates.Where(s => disappearedSet.Contains((s.FRuleGroupIndex, s.FEntryLineNo))).ToList();

        foreach (var stat in toInvalidate)
            stat.FInvalidated = true;
    }

    /// <summary>从 configJson 中提取所有 (groupIndex, lineNo) 组合</summary>
    private static List<(int GroupIndex, int LineNo)> ExtractGroupLineKeys(string? configJson)
    {
        var keys = new List<(int GroupIndex, int LineNo)>();
        if (string.IsNullOrWhiteSpace(configJson)) return keys;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ruleGroups", out var ruleGroupsEl)
                || ruleGroupsEl.ValueKind != JsonValueKind.Array)
                return keys;

            int groupIndex = 0;
            foreach (var group in ruleGroupsEl.EnumerateArray())
            {
                if (group.TryGetProperty("lines", out var linesEl)
                    && linesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var line in linesEl.EnumerateArray())
                    {
                        int lineNo = line.TryGetProperty("lineNo", out var lnProp)
                            ? lnProp.GetInt32() : 0;
                        keys.Add((groupIndex, lineNo));
                    }
                }
                groupIndex++;
            }
        }
        catch
        {
            // 解析失败时返回空集合，不影响业务流程
        }

        return keys;
    }
}

#region DTOs

public class PluginRuleDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public int Status { get; set; }
    public string? Description { get; set; }
    public int ReferenceCount { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public string CreateTime { get; set; } = string.Empty;
    public string? UpdateTime { get; set; }
}

public class CreatePluginRuleRequest
{
    public string TypeCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public string? Description { get; set; }
}

public class UpdatePluginRuleRequest
{
    public string? TypeCode { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public string? ConcurrencyStamp { get; set; }
}

public class PluginRuleSelectDto
{
    public long Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public long OrgId { get; set; }
}

public class DryRunRequest
{
    public long BatchId { get; set; }
    public string? ConfigJson { get; set; }
    /// <summary>可选：指定汇总字段，默认用 configJson 中的 groupBy</summary>
    public string? GroupField { get; set; }
}

#endregion
