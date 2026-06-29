using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Classification;
using STOTOP.Module.CardFlow.Services.DispatchRule;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/dispatch-rules")]
public class CfDispatchRuleController : ControllerBase
{
    private readonly DispatchRuleService _ruleService;
    private readonly ClassificationHandlerFactory _handlerFactory;
    private readonly ClassificationEngine _engine;
    private readonly STOTOPDbContext _context;

    public CfDispatchRuleController(
        DispatchRuleService ruleService,
        ClassificationHandlerFactory handlerFactory,
        ClassificationEngine engine,
        STOTOPDbContext context)
    {
        _ruleService = ruleService;
        _handlerFactory = handlerFactory;
        _engine = engine;
        _context = context;
    }

    /// <summary>获取派发规则列表</summary>
    [HttpGet]
    [RequirePermission(CardFlowPermissions.DispatchRuleView)]
    public async Task<ApiResult<List<DispatchRuleResponseDto>>> GetList(
        [FromQuery] int? status = null,
        [FromQuery] string? handlerType = null,
        [FromQuery] string? targetTable = null)
    {
        var result = await _ruleService.GetAllAsync(status, handlerType, targetTable);
        return ApiResult<List<DispatchRuleResponseDto>>.Success(result);
    }

    /// <summary>获取派发规则详情</summary>
    [HttpGet("{id:long}")]
    [RequirePermission(CardFlowPermissions.DispatchRuleView)]
    public async Task<ApiResult<DispatchRuleResponseDto>> GetById(long id)
    {
        var result = await _ruleService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<DispatchRuleResponseDto>.Fail("派发规则不存在");
        return ApiResult<DispatchRuleResponseDto>.Success(result);
    }

    /// <summary>创建派发规则</summary>
    [HttpPost]
    [RequirePermission(CardFlowPermissions.DispatchRuleManage)]
    public async Task<ApiResult<DispatchRuleResponseDto>> Create([FromBody] DispatchRuleCreateDto dto)
    {
        try
        {
            var result = await _ruleService.CreateAsync(dto);
            return ApiResult<DispatchRuleResponseDto>.Success(result, "创建派发规则成功");
        }
        catch (Exception ex)
        {
            return ApiResult<DispatchRuleResponseDto>.Fail(ex.Message);
        }
    }

    /// <summary>更新派发规则</summary>
    [HttpPut("{id:long}")]
    [RequirePermission(CardFlowPermissions.DispatchRuleManage)]
    public async Task<ApiResult<DispatchRuleResponseDto>> Update(long id, [FromBody] DispatchRuleUpdateDto dto)
    {
        try
        {
            var result = await _ruleService.UpdateAsync(id, dto);
            return ApiResult<DispatchRuleResponseDto>.Success(result, "更新派发规则成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<DispatchRuleResponseDto>.Fail(ex.Message);
        }
    }

    /// <summary>删除派发规则</summary>
    [HttpDelete("{id:long}")]
    [RequirePermission(CardFlowPermissions.DispatchRuleManage)]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _ruleService.DeleteAsync(id);
            return ApiResult.Ok("删除派发规则成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>获取所有可用处理器类型列表（含说明）</summary>
    [HttpGet("handler-types")]
    [RequirePermission(CardFlowPermissions.DispatchRuleView)]
    public ApiResult<List<HandlerTypeInfo>> GetHandlerTypes()
    {
        var registeredTypes = _handlerFactory.GetRegisteredTypes();

        var handlerTypeDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlertNotify"] = "告警通知 - 通过系统通知/钉钉等通道发送告警",
            ["InfoRecord"] = "信息记录 - 将分类信息记录到日志",
            ["AutoVoucher"] = "自动凭证 - 自动生成财务凭证",
            ["WorkTask"] = "工作任务 - 创建工作任务",
            ["Workflow"] = "工作流 - 触发自定义工作流"
        };

        var result = registeredTypes.Select(t => new HandlerTypeInfo
        {
            Type = t,
            Description = handlerTypeDescriptions.GetValueOrDefault(t, t)
        }).ToList();

        foreach (var (type, desc) in handlerTypeDescriptions)
        {
            if (!registeredTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(new HandlerTypeInfo { Type = type, Description = desc + "（未注册）" });
            }
        }

        return ApiResult<List<HandlerTypeInfo>>.Success(result);
    }

    /// <summary>测试运行：指定一个历史批次ID，执行该规则并返回命中结果（不持久化）</summary>
    [HttpPost("{id:long}/test")]
    [RequirePermission(CardFlowPermissions.DispatchRuleManage)]
    public async Task<ApiResult<List<ClassificationItem>>> TestRun(long id, [FromBody] TestRunRequest request)
    {
        try
        {
            var dispatchRule = await _context.Set<CfDispatchRule>().FirstOrDefaultAsync(r => r.FID == id);
            if (dispatchRule == null)
                return ApiResult<List<ClassificationItem>>.Fail("派发规则不存在");

            var batch = await _context.Set<CfBatch>().FirstOrDefaultAsync(b => b.FID == request.BatchId);
            if (batch == null)
                return ApiResult<List<ClassificationItem>>.Fail("批次不存在");

            string? targetTable = batch.FActualTargetTable; // DC文件类型已废除，直接从批次记录取实际目标表

            if (string.IsNullOrWhiteSpace(targetTable))
                return ApiResult<List<ClassificationItem>>.Fail("无法确定目标暂存表");

            var items = await _engine.TestRunRuleAsync(dispatchRule, request.BatchId, targetTable);
            return ApiResult<List<ClassificationItem>>.Success(items);
        }
        catch (Exception ex)
        {
            return ApiResult<List<ClassificationItem>>.Fail($"测试运行失败: {ex.Message}");
        }
    }
}

public class TestRunRequest
{
    public long BatchId { get; set; }
}
