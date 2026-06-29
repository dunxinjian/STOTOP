using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/dispatch-rules")]
public class DispatchRuleController : ControllerBase
{
    private readonly STOTOPDbContext _db;

    public DispatchRuleController(STOTOPDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ApiResult<List<DispatchRuleDto>>> GetList(
        [FromQuery] string? module,
        [FromQuery] string? bizType,
        [FromQuery] bool? enabled)
    {
        var query = _db.Set<WfDispatchRule>().AsQueryable();

        if (!string.IsNullOrEmpty(module))
            query = query.Where(r => r.FModule == module);
        if (!string.IsNullOrEmpty(bizType))
            query = query.Where(r => r.FBizType == bizType);
        if (enabled.HasValue)
            query = query.Where(r => r.FIsEnabled == enabled.Value);

        var rules = await query.OrderByDescending(r => r.FPriority).ThenBy(r => r.FCreateTime).ToListAsync();

        var result = rules.Select(MapToDto).ToList();
        return ApiResult<List<DispatchRuleDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<DispatchRuleDto>> GetById(long id)
    {
        var rule = await _db.Set<WfDispatchRule>().FindAsync(id);
        if (rule == null)
        {
            return ApiResult<DispatchRuleDto>.Fail("派发规则不存在");
        }
        return ApiResult<DispatchRuleDto>.Success(MapToDto(rule));
    }

    [HttpPost]
    public async Task<ApiResult<DispatchRuleDto>> Create([FromBody] CreateDispatchRuleRequest request)
    {
        var rule = new WfDispatchRule
        {
            FOrgId = request.OrgId,
            FName = request.Name,
            FDescription = request.Description,
            FModule = request.Module,
            FBizType = request.BizType,
            FDispatchMode = request.DispatchMode,
            FAutoAssignRule = request.AutoAssignRule,
            FTimeout = request.Timeout,
            FEscalationRule = request.EscalationRule,
            FPriority = request.Priority,
            FIsEnabled = request.IsEnabled,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<WfDispatchRule>().Add(rule);
        await _db.SaveChangesAsync();

        return ApiResult<DispatchRuleDto>.Success(MapToDto(rule), "创建派发规则成功");
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<DispatchRuleDto>> Update(long id, [FromBody] UpdateDispatchRuleRequest request)
    {
        var rule = await _db.Set<WfDispatchRule>().FindAsync(id);
        if (rule == null)
        {
            return ApiResult<DispatchRuleDto>.Fail("派发规则不存在");
        }

        rule.FName = request.Name;
        rule.FDescription = request.Description;
        rule.FModule = request.Module;
        rule.FBizType = request.BizType;
        rule.FDispatchMode = request.DispatchMode;
        rule.FAutoAssignRule = request.AutoAssignRule;
        rule.FTimeout = request.Timeout;
        rule.FEscalationRule = request.EscalationRule;
        rule.FPriority = request.Priority;
        rule.FIsEnabled = request.IsEnabled;
        rule.FUpdateTime = DateTime.Now;

        _db.Set<WfDispatchRule>().Update(rule);
        await _db.SaveChangesAsync();

        return ApiResult<DispatchRuleDto>.Success(MapToDto(rule), "更新派发规则成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var rule = await _db.Set<WfDispatchRule>().FindAsync(id);
        if (rule == null)
        {
            return ApiResult.Fail("派发规则不存在");
        }

        _db.Set<WfDispatchRule>().Remove(rule);
        await _db.SaveChangesAsync();

        return ApiResult.Ok("删除派发规则成功");
    }

    [HttpPost("{id}/toggle")]
    public async Task<ApiResult<DispatchRuleDto>> Toggle(long id)
    {
        var rule = await _db.Set<WfDispatchRule>().FindAsync(id);
        if (rule == null)
        {
            return ApiResult<DispatchRuleDto>.Fail("派发规则不存在");
        }

        rule.FIsEnabled = !rule.FIsEnabled;
        rule.FUpdateTime = DateTime.Now;

        _db.Set<WfDispatchRule>().Update(rule);
        await _db.SaveChangesAsync();

        return ApiResult<DispatchRuleDto>.Success(MapToDto(rule), rule.FIsEnabled ? "已启用" : "已禁用");
    }

    private static DispatchRuleDto MapToDto(WfDispatchRule rule) => new()
    {
        Id = rule.FID,
        Uid = rule.FUID,
        Name = rule.FName,
        Description = rule.FDescription,
        Module = rule.FModule,
        BizType = rule.FBizType,
        DispatchMode = rule.FDispatchMode,
        AutoAssignRule = rule.FAutoAssignRule,
        Timeout = rule.FTimeout,
        EscalationRule = rule.FEscalationRule,
        Priority = rule.FPriority,
        IsEnabled = rule.FIsEnabled
    };
}

public class CreateDispatchRuleRequest
{
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public int DispatchMode { get; set; }
    public string? AutoAssignRule { get; set; }
    public int Timeout { get; set; }
    public string? EscalationRule { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdateDispatchRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public int DispatchMode { get; set; }
    public string? AutoAssignRule { get; set; }
    public int Timeout { get; set; }
    public string? EscalationRule { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}
