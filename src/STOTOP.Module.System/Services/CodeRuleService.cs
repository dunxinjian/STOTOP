using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class CodeRuleService : ICodeRuleService
{
    private readonly STOTOPDbContext _context;

    public CodeRuleService(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 根据规则编码生成下一个编码（核心方法，并发安全）
    /// </summary>
    public async Task<string> GenerateNextCodeAsync(string ruleCode, long? orgId = null)
    {
        var rule = await _context.Set<SysCodeRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FRuleCode == ruleCode && r.FEnabled);

        if (rule == null)
            throw new InvalidOperationException($"编码规则 '{ruleCode}' 不存在或未启用");

        var periodKey = GetPeriodKey(rule.FResetCycle);
        var seqValue = await GetNextSequenceValueAsync(rule.FID, orgId, periodKey);

        return BuildCode(rule, periodKey, seqValue);
    }

    /// <summary>
    /// 批量生成编码
    /// </summary>
    public async Task<List<string>> GenerateBatchCodesAsync(string ruleCode, int count, long? orgId = null)
    {
        if (count <= 0 || count > 1000)
            throw new ArgumentException("批量生成数量必须在 1-1000 之间");

        var rule = await _context.Set<SysCodeRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FRuleCode == ruleCode && r.FEnabled);

        if (rule == null)
            throw new InvalidOperationException($"编码规则 '{ruleCode}' 不存在或未启用");

        var periodKey = GetPeriodKey(rule.FResetCycle);
        var codes = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            var seqValue = await GetNextSequenceValueAsync(rule.FID, orgId, periodKey);
            codes.Add(BuildCode(rule, periodKey, seqValue));
        }

        return codes;
    }

    /// <summary>
    /// 预览编码格式（不消耗序号）
    /// </summary>
    public string PreviewCode(SysCodeRule rule)
    {
        var periodKey = GetPeriodKey(rule.FResetCycle);
        var sampleSeq = 1L;
        return BuildCode(rule, periodKey, sampleSeq);
    }

    public async Task<ApiResult<string>> PreviewCodeAsync(long id)
    {
        var rule = await _context.Set<SysCodeRule>().FindAsync(id);
        if (rule == null)
            return ApiResult<string>.Fail("编码规则不存在");

        return ApiResult<string>.Success(PreviewCode(rule));
    }

    public async Task<ApiResult<List<CodeRuleDto>>> GetAllRulesAsync()
    {
        var rules = await _context.Set<SysCodeRule>()
            .OrderBy(r => r.FCreateTime)
            .ToListAsync();

        var dtos = rules.Select(r => MapToDto(r)).ToList();
        return ApiResult<List<CodeRuleDto>>.Success(dtos);
    }

    public async Task<ApiResult<CodeRuleDto>> GetRuleByIdAsync(long id)
    {
        var rule = await _context.Set<SysCodeRule>().FindAsync(id);
        if (rule == null)
            return ApiResult<CodeRuleDto>.Fail("编码规则不存在");

        return ApiResult<CodeRuleDto>.Success(MapToDto(rule));
    }

    public async Task<ApiResult<CodeRuleDto>> UpdateRuleAsync(long id, CodeRuleUpdateDto dto)
    {
        var rule = await _context.Set<SysCodeRule>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);
        if (rule == null)
            return ApiResult<CodeRuleDto>.Fail("编码规则不存在");

        rule.FPrefix = dto.Prefix;
        rule.FDateFormat = dto.DateFormat;
        rule.FSeqLength = dto.SeqLength;
        rule.FSeparator = dto.Separator;
        rule.FResetCycle = dto.ResetPeriod;
        rule.FOrgIsolation = dto.OrgIsolation;
        rule.FEnabled = dto.Enabled;
        rule.FDescription = dto.Description;
        rule.FUpdateTime = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<CodeRuleDto>.Success(MapToDto(rule));
    }

    public async Task<ApiResult<CodeRuleDto>> CreateRuleAsync(CodeRuleCreateDto dto)
    {
        // 检查 RuleCode 唯一性
        var exists = await _context.Set<SysCodeRule>()
            .AnyAsync(r => r.FRuleCode == dto.RuleCode);
        if (exists)
            return ApiResult<CodeRuleDto>.Fail($"规则编码 '{dto.RuleCode}' 已存在");

        var rule = new SysCodeRule
        {
            FRuleCode = dto.RuleCode,
            FRuleName = dto.RuleName,
            FBusinessEntity = dto.BusinessEntity,
            FCodeField = string.IsNullOrWhiteSpace(dto.CodeField) ? "F编码" : dto.CodeField,
            FPrefix = dto.Prefix,
            FDateFormat = dto.DateFormat,
            FSeqLength = dto.SeqLength,
            FSeparator = dto.Separator,
            FResetCycle = dto.ResetPeriod,
            FOrgIsolation = dto.OrgIsolation,
            FEnabled = true,
            FDescription = dto.Description,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _context.Set<SysCodeRule>().Add(rule);
        await _context.SaveChangesAsync();
        return ApiResult<CodeRuleDto>.Success(MapToDto(rule));
    }

    public async Task<ApiResult> DeleteRuleAsync(long id)
    {
        var rule = await _context.Set<SysCodeRule>().FindAsync(id);
        if (rule == null)
            return ApiResult.Fail("编码规则不存在");

        // 删除关联的序列记录
        var sequences = await _context.Set<SysCodeSequence>()
            .Where(s => s.FRuleId == id)
            .ToListAsync();
        if (sequences.Count > 0)
            _context.Set<SysCodeSequence>().RemoveRange(sequences);

        _context.Set<SysCodeRule>().Remove(rule);
        await _context.SaveChangesAsync();
        return ApiResult.Ok("删除成功");
    }

    /// <summary>
    /// 原子递增获取序列号（并发安全）
    /// </summary>
    private async Task<long> GetNextSequenceValueAsync(long ruleId, long? orgId, string periodKey)
    {
        var paramRuleId = new SqlParameter("@ruleId", ruleId);
        var paramOrgId = new SqlParameter("@orgId", (object?)orgId ?? DBNull.Value);
        var paramPeriodKey = new SqlParameter("@periodKey", periodKey);

        // 尝试原子更新并返回新值
        var updateSql = @"
UPDATE [SYS编码序列]
SET [F当前值] = [F当前值] + 1, [F修改时间] = GETDATE()
OUTPUT INSERTED.[F当前值]
WHERE [F规则ID] = @ruleId
  AND ISNULL([F组织ID], 0) = ISNULL(@orgId, 0)
  AND [F周期标识] = @periodKey";

        var result = await _context.Database
            .SqlQueryRaw<long>(updateSql, paramRuleId, paramOrgId, paramPeriodKey)
            .ToListAsync();

        if (result.Count > 0)
            return result[0];

        // 新周期或新组织，插入初始记录
        var paramRuleId2 = new SqlParameter("@ruleId", ruleId);
        var paramOrgId2 = new SqlParameter("@orgId", (object?)orgId ?? DBNull.Value);
        var paramPeriodKey2 = new SqlParameter("@periodKey", periodKey);

        var insertSql = @"
INSERT INTO [SYS编码序列] ([F规则ID], [F组织ID], [F周期标识], [F当前值], [F修改时间])
VALUES (@ruleId, @orgId, @periodKey, 1, GETDATE());
SELECT CAST(1 AS BIGINT);";

        try
        {
            var insertResult = await _context.Database
                .SqlQueryRaw<long>(insertSql, paramRuleId2, paramOrgId2, paramPeriodKey2)
                .ToListAsync();

            return insertResult[0];
        }
        catch (Exception)
        {
            // 并发场景：如果 INSERT 因唯一约束冲突失败，则重试 UPDATE
            var paramRuleId3 = new SqlParameter("@ruleId", ruleId);
            var paramOrgId3 = new SqlParameter("@orgId", (object?)orgId ?? DBNull.Value);
            var paramPeriodKey3 = new SqlParameter("@periodKey", periodKey);

            var retryResult = await _context.Database
                .SqlQueryRaw<long>(updateSql, paramRuleId3, paramOrgId3, paramPeriodKey3)
                .ToListAsync();

            return retryResult[0];
        }
    }

    /// <summary>
    /// 根据重置周期计算周期标识
    /// </summary>
    private static string GetPeriodKey(string resetCycle)
    {
        var now = DateTime.Now;
        return resetCycle.ToLower() switch
        {
            "day" => now.ToString("yyyyMMdd"),
            "month" => now.ToString("yyyyMM"),
            "year" => now.ToString("yyyy"),
            _ => "" // never
        };
    }

    /// <summary>
    /// 拼装编码
    /// </summary>
    private static string BuildCode(SysCodeRule rule, string periodKey, long seqValue)
    {
        var parts = new List<string>();
        var sep = rule.FSeparator ?? "";

        // 前缀
        if (!string.IsNullOrEmpty(rule.FPrefix))
            parts.Add(rule.FPrefix);

        // 日期段
        if (!string.IsNullOrEmpty(rule.FDateFormat))
        {
            var datePart = DateTime.Now.ToString(rule.FDateFormat);
            parts.Add(datePart);
        }

        // 流水号
        parts.Add(seqValue.ToString().PadLeft(rule.FSeqLength, '0'));

        return string.Join(sep, parts);
    }

    private CodeRuleDto MapToDto(SysCodeRule rule)
    {
        return new CodeRuleDto
        {
            Id = rule.FID,
            RuleCode = rule.FRuleCode,
            RuleName = rule.FRuleName,
            BusinessEntity = rule.FBusinessEntity,
            CodeField = rule.FCodeField,
            Prefix = rule.FPrefix,
            DateFormat = rule.FDateFormat,
            SeqLength = rule.FSeqLength,
            Separator = rule.FSeparator,
            ResetPeriod = rule.FResetCycle,
            OrgIsolation = rule.FOrgIsolation,
            Enabled = rule.FEnabled,
            Description = rule.FDescription,
            Preview = PreviewCode(rule),
            CreateTime = rule.FCreateTime,
            UpdateTime = rule.FUpdateTime
        };
    }
}
