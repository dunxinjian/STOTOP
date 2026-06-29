using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// 卡片流程编排中心 API：编排模板管理、编排实例操作、自由派发。
/// </summary>
[Authorize]
[ApiController]
public class OrchestrationController : ControllerBase
{
    private readonly STOTOPDbContext _db;
    private readonly OrchestrationEngineService _engine;
    private readonly AdHocDispatchService _adHoc;

    public OrchestrationController(
        STOTOPDbContext db,
        OrchestrationEngineService engine,
        AdHocDispatchService adHoc)
    {
        _db = db;
        _engine = engine;
        _adHoc = adHoc;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // =====================================================================
    // 编排模板管理
    // =====================================================================

    /// <summary>编排模板列表（分页）</summary>
    [HttpGet("/api/orchestration/templates")]
    public async Task<ApiResult<PagedResult<OrchestrationTemplateDto>>> GetTemplates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var orgId = GetOrgId();
        var query = _db.Set<CfOrchestrationTemplate>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.FOrgId == orgId || t.FOrgId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(t => t.FCode.Contains(keyword) || t.FName.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.FStatus == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.FID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new OrchestrationTemplateDto
            {
                Id = t.FID,
                Code = t.FCode,
                Name = t.FName,
                Description = t.FDescription,
                Status = t.FStatus,
                MaxTriggerCount = t.FMaxTriggerCount,
                CreatorId = t.FCreatorId,
                CreatedTime = t.FCreatedTime,
                UpdatedTime = t.FUpdatedTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<OrchestrationTemplateDto>>.Success(new PagedResult<OrchestrationTemplateDto>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    /// <summary>编排模板详情</summary>
    [HttpGet("/api/orchestration/templates/{id:long}")]
    public async Task<ApiResult<OrchestrationTemplateDetailDto>> GetTemplate(long id)
    {
        var orgId = GetOrgId();
        var t = await _db.Set<CfOrchestrationTemplate>().AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FID == id && (x.FOrgId == orgId || x.FOrgId == 0));
        if (t == null)
        {
            return ApiResult<OrchestrationTemplateDetailDto>.Fail("编排模板不存在");
        }

        return ApiResult<OrchestrationTemplateDetailDto>.Success(new OrchestrationTemplateDetailDto
        {
            Id = t.FID,
            Code = t.FCode,
            Name = t.FName,
            Description = t.FDescription,
            Status = t.FStatus,
            MaxTriggerCount = t.FMaxTriggerCount,
            NodesJson = t.FNodesJson,
            EdgesJson = t.FEdgesJson,
            OrgId = t.FOrgId,
            CreatorId = t.FCreatorId,
            CreatedTime = t.FCreatedTime,
            UpdatedTime = t.FUpdatedTime
        });
    }

    /// <summary>创建编排模板（默认 draft 状态）</summary>
    [HttpPost("/api/orchestration/templates")]
    public async Task<ApiResult<OrchestrationTemplateDetailDto>> CreateTemplate(
        [FromBody] CreateTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return ApiResult<OrchestrationTemplateDetailDto>.Fail("Code 不能为空");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<OrchestrationTemplateDetailDto>.Fail("Name 不能为空");

        var orgId = GetOrgId();
        var exists = await _db.Set<CfOrchestrationTemplate>()
            .AnyAsync(t => t.FOrgId == orgId && t.FCode == request.Code);
        if (exists)
        {
            return ApiResult<OrchestrationTemplateDetailDto>.Fail($"编排模板编码 {request.Code} 已存在");
        }

        var entity = new CfOrchestrationTemplate
        {
            FCode = request.Code,
            FName = request.Name,
            FDescription = request.Description,
            FOrgId = orgId,
            FNodesJson = request.NodesJson,
            FEdgesJson = request.EdgesJson,
            FStatus = "draft",
            FMaxTriggerCount = request.MaxTriggerCount > 0 ? request.MaxTriggerCount : 50,
            FCreatorId = GetUserId(),
            FCreatedTime = DateTime.Now
        };
        _db.Set<CfOrchestrationTemplate>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<OrchestrationTemplateDetailDto>.Success(new OrchestrationTemplateDetailDto
        {
            Id = entity.FID,
            Code = entity.FCode,
            Name = entity.FName,
            Description = entity.FDescription,
            Status = entity.FStatus,
            MaxTriggerCount = entity.FMaxTriggerCount,
            NodesJson = entity.FNodesJson,
            EdgesJson = entity.FEdgesJson,
            OrgId = entity.FOrgId,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        }, "创建编排模板成功");
    }

    /// <summary>更新编排模板（仅 draft 状态可改）</summary>
    [HttpPut("/api/orchestration/templates/{id:long}")]
    public async Task<ApiResult> UpdateTemplate(long id, [FromBody] UpdateTemplateRequest request)
    {
        var orgId = GetOrgId();
        var entity = await _db.Set<CfOrchestrationTemplate>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FID == id && (t.FOrgId == orgId || t.FOrgId == 0));
        if (entity == null) return ApiResult.Fail("编排模板不存在");

        if (entity.FStatus != "draft")
        {
            return ApiResult.Fail($"模板当前状态为 {entity.FStatus}，仅 draft 状态可修改");
        }

        if (request.Name != null) entity.FName = request.Name;
        if (request.Description != null) entity.FDescription = request.Description;
        if (request.NodesJson != null) entity.FNodesJson = request.NodesJson;
        if (request.EdgesJson != null) entity.FEdgesJson = request.EdgesJson;
        if (request.MaxTriggerCount.HasValue && request.MaxTriggerCount.Value > 0)
        {
            entity.FMaxTriggerCount = request.MaxTriggerCount.Value;
        }
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return ApiResult.Ok("更新成功");
    }

    /// <summary>发布编排模板</summary>
    [HttpPost("/api/orchestration/templates/{id:long}/publish")]
    public async Task<ApiResult<PublishTemplateResponse>> PublishTemplate(long id)
    {
        var orgId = GetOrgId();
        var entity = await _db.Set<CfOrchestrationTemplate>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FID == id && (t.FOrgId == orgId || t.FOrgId == 0));
        if (entity == null)
        {
            return ApiResult<PublishTemplateResponse>.Fail("编排模板不存在");
        }
        if (entity.FStatus == "published")
        {
            return ApiResult<PublishTemplateResponse>.Success(
                new PublishTemplateResponse { IsValid = true, Errors = new List<string>() },
                "模板已为发布状态");
        }

        var (isValid, errors) = await _engine.ValidateForPublishAsync(id);
        if (!isValid)
        {
            return ApiResult<PublishTemplateResponse>.Fail(
                "模板校验未通过：" + string.Join("；", errors));
        }

        entity.FStatus = "published";
        entity.FUpdatedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult<PublishTemplateResponse>.Success(
            new PublishTemplateResponse { IsValid = true, Errors = errors },
            "发布成功");
    }

    /// <summary>停用编排模板</summary>
    [HttpPost("/api/orchestration/templates/{id:long}/disable")]
    public async Task<ApiResult> DisableTemplate(long id)
    {
        var orgId = GetOrgId();
        var entity = await _db.Set<CfOrchestrationTemplate>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FID == id && (t.FOrgId == orgId || t.FOrgId == 0));
        if (entity == null) return ApiResult.Fail("编排模板不存在");

        if (entity.FStatus == "disabled")
        {
            return ApiResult.Ok("模板已为停用状态");
        }

        entity.FStatus = "disabled";
        entity.FUpdatedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult.Ok("停用成功");
    }

    // =====================================================================
    // 编排实例操作
    // =====================================================================

    /// <summary>启动编排实例</summary>
    [HttpPost("/api/orchestration/instances")]
    public async Task<ApiResult<StartInstanceResponse>> StartInstance(
        [FromBody] StartInstanceRequest request)
    {
        if (request.TemplateId <= 0)
        {
            return ApiResult<StartInstanceResponse>.Fail("templateId 无效");
        }

        try
        {
            var instanceId = await _engine.StartAsync(
                request.TemplateId, GetUserId(), request.InputData);
            return ApiResult<StartInstanceResponse>.Success(
                new StartInstanceResponse { InstanceId = instanceId }, "启动成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<StartInstanceResponse>.Fail(ex.Message);
        }
    }

    /// <summary>编排实例列表（分页）</summary>
    [HttpGet("/api/orchestration/instances")]
    public async Task<ApiResult<PagedResult<OrchestrationInstanceDto>>> GetInstances(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? templateId = null,
        [FromQuery] string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var orgId = GetOrgId();
        var query = _db.Set<CfOrchestrationInstance>().AsNoTracking()
            .Where(i => i.FOrgId == orgId);

        if (templateId.HasValue) query = query.Where(i => i.FTemplateId == templateId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.FStatus == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.FID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new OrchestrationInstanceDto
            {
                Id = i.FID,
                TemplateId = i.FTemplateId,
                Status = i.FStatus,
                CompletionReason = i.FCompletionReason,
                TriggerCount = i.FTriggerCount,
                InitiatorId = i.FInitiatorId,
                InitiatedTime = i.FInitiatedTime,
                CompletedTime = i.FCompletedTime,
                FailureReason = i.FFailureReason
            })
            .ToListAsync();

        return ApiResult<PagedResult<OrchestrationInstanceDto>>.Success(new PagedResult<OrchestrationInstanceDto>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    /// <summary>编排实例详情（含节点进度）</summary>
    [HttpGet("/api/orchestration/instances/{id:long}")]
    public async Task<ApiResult<OrchestrationInstanceDetailDto>> GetInstance(long id)
    {
        var instance = await _db.Set<CfOrchestrationInstance>().AsNoTracking()
            .FirstOrDefaultAsync(i => i.FID == id);
        if (instance == null)
        {
            return ApiResult<OrchestrationInstanceDetailDto>.Fail("编排实例不存在");
        }

        var nodeInstances = await _db.Set<CfOrchestrationNodeInstance>().AsNoTracking()
            .Where(n => n.FOrchestrationInstanceId == id)
            .OrderBy(n => n.FID)
            .Select(n => new OrchestrationNodeInstanceDto
            {
                Id = n.FID,
                NodeId = n.FNodeId,
                Status = n.FStatus,
                EndStatusType = n.FEndStatusType,
                RelatedCardId = n.FRelatedCardId,
                RelatedBatchId = n.FRelatedBatchId,
                ResultJson = n.FResultJson,
                StartTime = n.FStartTime,
                CompletedTime = n.FCompletedTime
            })
            .ToListAsync();

        return ApiResult<OrchestrationInstanceDetailDto>.Success(new OrchestrationInstanceDetailDto
        {
            Id = instance.FID,
            TemplateId = instance.FTemplateId,
            OrgId = instance.FOrgId,
            Status = instance.FStatus,
            CompletionReason = instance.FCompletionReason,
            SnapshotNodesJson = instance.FSnapshotNodesJson,
            SnapshotEdgesJson = instance.FSnapshotEdgesJson,
            ContextJson = instance.FContextJson,
            TriggerCount = instance.FTriggerCount,
            InitiatorId = instance.FInitiatorId,
            InitiatedTime = instance.FInitiatedTime,
            CompletedTime = instance.FCompletedTime,
            FailureReason = instance.FFailureReason,
            NodeInstances = nodeInstances
        });
    }

    /// <summary>编排实例下的派发记录列表</summary>
    [HttpGet("/api/orchestration/instances/{id:long}/dispatches")]
    public async Task<ApiResult<List<DispatchRecordDto>>> GetDispatches(long id)
    {
        var list = await _db.Set<CfDispatchRecord>().AsNoTracking()
            .Where(d => d.FOrchestrationInstanceId == id)
            .OrderBy(d => d.FID)
            .Select(d => new DispatchRecordDto
            {
                Id = d.FID,
                OrchestrationInstanceId = d.FOrchestrationInstanceId,
                DispatchType = d.FDispatchType,
                SourceNodeId = d.FSourceNodeId,
                SourceCardId = d.FSourceCardId,
                SourceFlowCode = d.FSourceFlowCode,
                TargetNodeId = d.FTargetNodeId,
                TargetCardId = d.FTargetCardId,
                TargetFlowCode = d.FTargetFlowCode,
                DataPayloadJson = d.FDataPayloadJson,
                Status = d.FStatus,
                OperatorId = d.FOperatorId,
                CreatedTime = d.FCreatedTime,
                TriggeredTime = d.FTriggeredTime,
                FailureReason = d.FFailureReason
            })
            .ToListAsync();
        return ApiResult<List<DispatchRecordDto>>.Success(list);
    }

    /// <summary>暂停编排实例</summary>
    [HttpPost("/api/orchestration/instances/{id:long}/pause")]
    public async Task<ApiResult> PauseInstance(long id)
    {
        try
        {
            await _engine.PauseAsync(id);
            return ApiResult.Ok("已暂停");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>恢复编排实例</summary>
    [HttpPost("/api/orchestration/instances/{id:long}/resume")]
    public async Task<ApiResult> ResumeInstance(long id)
    {
        try
        {
            await _engine.ResumeAsync(id);
            return ApiResult.Ok("已恢复");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>取消编排实例</summary>
    [HttpDelete("/api/orchestration/instances/{id:long}")]
    public async Task<ApiResult> CancelInstance(long id)
    {
        try
        {
            await _engine.CancelAsync(id);
            return ApiResult.Ok("已取消");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // =====================================================================
    // 自由派发
    // =====================================================================

    /// <summary>查询某卡片完成后的可派发目标列表</summary>
    [HttpGet("/api/adhoc-dispatch/available")]
    public async Task<ApiResult<List<DispatchOption>>> GetAvailableTargets([FromQuery] long cardId)
    {
        if (cardId <= 0) return ApiResult<List<DispatchOption>>.Fail("cardId 无效");
        var targets = await _adHoc.GetAvailableTargetsAsync(cardId);
        return ApiResult<List<DispatchOption>>.Success(targets);
    }

    /// <summary>执行自由派发</summary>
    [HttpPost("/api/adhoc-dispatch")]
    public async Task<ApiResult<AdHocDispatchResponse>> Dispatch([FromBody] AdHocDispatchRequest request)
    {
        if (request.SourceCardId <= 0)
            return ApiResult<AdHocDispatchResponse>.Fail("sourceCardId 无效");
        if (string.IsNullOrWhiteSpace(request.TargetFlowCode))
            return ApiResult<AdHocDispatchResponse>.Fail("targetFlowCode 不能为空");

        try
        {
            var newCardId = await _adHoc.DispatchAsync(
                request.SourceCardId, request.TargetFlowCode, GetUserId(), request.CustomData);
            return ApiResult<AdHocDispatchResponse>.Success(
                new AdHocDispatchResponse { CardId = newCardId }, "派发成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AdHocDispatchResponse>.Fail(ex.Message);
        }
    }
}

// =====================================================================
// DTO 定义
// =====================================================================

public class CreateTemplateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NodesJson { get; set; }
    public string? EdgesJson { get; set; }
    public int MaxTriggerCount { get; set; } = 50;
}

public class UpdateTemplateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? NodesJson { get; set; }
    public string? EdgesJson { get; set; }
    public int? MaxTriggerCount { get; set; }
}

public class StartInstanceRequest
{
    public long TemplateId { get; set; }
    public JsonElement? InputData { get; set; }
}

public class StartInstanceResponse
{
    public long InstanceId { get; set; }
}

public class AdHocDispatchRequest
{
    public long SourceCardId { get; set; }
    public string TargetFlowCode { get; set; } = string.Empty;
    public JsonElement? CustomData { get; set; }
}

public class AdHocDispatchResponse
{
    public long CardId { get; set; }
}

public class PublishTemplateResponse
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class OrchestrationTemplateDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MaxTriggerCount { get; set; }
    public long CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

public class OrchestrationTemplateDetailDto : OrchestrationTemplateDto
{
    public long OrgId { get; set; }
    public string? NodesJson { get; set; }
    public string? EdgesJson { get; set; }
}

public class OrchestrationInstanceDto
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CompletionReason { get; set; }
    public int TriggerCount { get; set; }
    public long InitiatorId { get; set; }
    public DateTime InitiatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string? FailureReason { get; set; }
}

public class OrchestrationInstanceDetailDto : OrchestrationInstanceDto
{
    public long OrgId { get; set; }
    public string? SnapshotNodesJson { get; set; }
    public string? SnapshotEdgesJson { get; set; }
    public string? ContextJson { get; set; }
    public List<OrchestrationNodeInstanceDto> NodeInstances { get; set; } = new();
}

public class OrchestrationNodeInstanceDto
{
    public long Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EndStatusType { get; set; }
    public long? RelatedCardId { get; set; }
    public long? RelatedBatchId { get; set; }
    public string? ResultJson { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
}

public class DispatchRecordDto
{
    public long Id { get; set; }
    public long? OrchestrationInstanceId { get; set; }
    public string DispatchType { get; set; } = string.Empty;
    public string? SourceNodeId { get; set; }
    public long? SourceCardId { get; set; }
    public string? SourceFlowCode { get; set; }
    public string? TargetNodeId { get; set; }
    public long? TargetCardId { get; set; }
    public string? TargetFlowCode { get; set; }
    public string? DataPayloadJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? OperatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? TriggeredTime { get; set; }
    public string? FailureReason { get; set; }
}
