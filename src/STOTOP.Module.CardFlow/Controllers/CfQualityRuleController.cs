using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/quality-rules")]
public class CfQualityRuleController : ControllerBase
{
    private readonly STOTOPDbContext _context;

    public CfQualityRuleController(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>获取质量规则列表（支持筛选）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] QualityRuleListRequest request)
    {
        var rules = _context.Set<CfQualityRule>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TargetTable))
            rules = rules.Where(r => r.FTargetTable == request.TargetTable);
        if (request.OrgId.HasValue)
            rules = rules.Where(r => r.FOrgId == request.OrgId.Value);
        if (request.IsEnabled.HasValue)
            rules = rules.Where(r => r.FEnabled == request.IsEnabled.Value);

        var list = await rules
            .Select(r => new QualityRuleDto
            {
                Id = r.FID,
                RuleName = r.FRuleName,
                RuleCode = r.FRuleCode,
                TargetTable = r.FTargetTable,
                RuleLevel = r.FRuleLevel,
                CheckType = r.FCheckType,
                TargetField = r.FTargetField,
                ParametersJson = r.FRuleParamsJson,
                ErrorTypeCode = r.FErrorCode,
                Severity = r.FSeverityLevel,
                QualityDimension = r.FQualityDimension,
                MessageTemplate = r.FErrorMessageTemplate,
                SuggestedFix = r.FSuggestedFix,
                IsBlocking = r.FIsBlocking,
                OrgId = r.FOrgId,
                Sort = r.FSortOrder,
                IsEnabled = r.FEnabled,
                CreateTime = r.FCreatedTime,
                UpdateTime = r.FUpdatedTime
            })
            .OrderBy(d => d.Sort)
            .ThenByDescending(d => d.CreateTime)
            .ToListAsync();

        return Ok(ApiResult<List<QualityRuleDto>>.Success(list));
    }

    /// <summary>获取质量规则详情</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var rule = await _context.Set<CfQualityRule>().FindAsync(id);
        if (rule == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        var dto = new QualityRuleDto
        {
            Id = rule.FID,
            RuleName = rule.FRuleName,
            RuleCode = rule.FRuleCode,
            TargetTable = rule.FTargetTable,
            RuleLevel = rule.FRuleLevel,
            CheckType = rule.FCheckType,
            TargetField = rule.FTargetField,
            ParametersJson = rule.FRuleParamsJson,
            ErrorTypeCode = rule.FErrorCode,
            Severity = rule.FSeverityLevel,
            QualityDimension = rule.FQualityDimension,
            MessageTemplate = rule.FErrorMessageTemplate,
            SuggestedFix = rule.FSuggestedFix,
            IsBlocking = rule.FIsBlocking,
            OrgId = rule.FOrgId,
            Sort = rule.FSortOrder,
            IsEnabled = rule.FEnabled,
            CreateTime = rule.FCreatedTime,
            UpdateTime = rule.FUpdatedTime
        };

        return Ok(ApiResult<QualityRuleDto>.Success(dto));
    }

    /// <summary>创建质量规则</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQualityRuleRequest request)
    {
        // 校验 RuleCode 唯一性
        var exists = await _context.Set<CfQualityRule>()
            .AnyAsync(r => r.FRuleCode == request.RuleCode);
        if (exists)
            return Ok(ApiResult.Fail($"规则编码 '{request.RuleCode}' 已存在"));

        var entity = new CfQualityRule
        {
            FRuleName = request.RuleName,
            FRuleCode = request.RuleCode,
            FTargetTable = request.TargetTable,
            FRuleLevel = request.RuleLevel,
            FCheckType = request.CheckType,
            FTargetField = request.TargetField,
            FRuleParamsJson = request.ParametersJson,
            FErrorCode = request.ErrorTypeCode,
            FSeverityLevel = request.Severity,
            FQualityDimension = request.QualityDimension,
            FErrorMessageTemplate = request.MessageTemplate,
            FSuggestedFix = request.SuggestedFix,
            FIsBlocking = request.IsBlocking,
            FOrgId = request.OrgId,
            FSortOrder = request.Sort,
            FEnabled = request.IsEnabled,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _context.Set<CfQualityRule>().Add(entity);
        await _context.SaveChangesAsync();

        return Ok(ApiResult<QualityRuleDto>.Success(new QualityRuleDto
        {
            Id = entity.FID,
            RuleName = entity.FRuleName,
            RuleCode = entity.FRuleCode,
            TargetTable = entity.FTargetTable,
            RuleLevel = entity.FRuleLevel,
            CheckType = entity.FCheckType,
            TargetField = entity.FTargetField,
            ParametersJson = entity.FRuleParamsJson,
            ErrorTypeCode = entity.FErrorCode,
            Severity = entity.FSeverityLevel,
            QualityDimension = entity.FQualityDimension,
            MessageTemplate = entity.FErrorMessageTemplate,
            SuggestedFix = entity.FSuggestedFix,
            IsBlocking = entity.FIsBlocking,
            OrgId = entity.FOrgId,
            Sort = entity.FSortOrder,
            IsEnabled = entity.FEnabled,
            CreateTime = entity.FCreatedTime,
            UpdateTime = entity.FUpdatedTime
        }, "创建成功"));
    }

    /// <summary>更新质量规则</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateQualityRuleRequest request)
    {
        var entity = await _context.Set<CfQualityRule>().AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);
        if (entity == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        // 校验 RuleCode 唯一性（排除自身）
        var codeConflict = await _context.Set<CfQualityRule>()
            .AnyAsync(r => r.FRuleCode == request.RuleCode && r.FID != id);
        if (codeConflict)
            return Ok(ApiResult.Fail($"规则编码 '{request.RuleCode}' 已被其他规则使用"));

        entity.FRuleName = request.RuleName;
        entity.FRuleCode = request.RuleCode;
        entity.FTargetTable = request.TargetTable;
        entity.FRuleLevel = request.RuleLevel;
        entity.FCheckType = request.CheckType;
        entity.FTargetField = request.TargetField;
        entity.FRuleParamsJson = request.ParametersJson;
        entity.FErrorCode = request.ErrorTypeCode;
        entity.FSeverityLevel = request.Severity;
        entity.FQualityDimension = request.QualityDimension;
        entity.FErrorMessageTemplate = request.MessageTemplate;
        entity.FSuggestedFix = request.SuggestedFix;
        entity.FIsBlocking = request.IsBlocking;
        entity.FOrgId = request.OrgId;
        entity.FSortOrder = request.Sort;
        entity.FEnabled = request.IsEnabled;
        entity.FUpdatedTime = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(ApiResult.Ok("更新成功"));
    }

    /// <summary>删除质量规则</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _context.Set<CfQualityRule>().FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("规则不存在", 404));

        _context.Set<CfQualityRule>().Remove(entity);
        await _context.SaveChangesAsync();

        return Ok(ApiResult.Ok("删除成功"));
    }

    /// <summary>测试质量规则（干跑，规则引擎在后续任务中实现）</summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestRule([FromBody] TestQualityRuleRequest request)
    {
        // TODO: 规则引擎在 Task #15 中实现，此处先返回 mock 结果
        var result = new TestQualityRuleResult
        {
            TotalChecked = 0,
            ViolationCount = 0,
            Violations = new List<TestViolationItem>()
        };

        return Ok(ApiResult<TestQualityRuleResult>.Success(result, "规则测试功能暂未实现，当前返回空结果"));
    }
}
