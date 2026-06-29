using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>质量问题类型管理 - CRUD</summary>
[Authorize]
[ApiController]
[Route("api/quality-center/issue-types")]
public class CfQualityIssueTypeController : ControllerBase
{
    private readonly STOTOPDbContext _context;

    public CfQualityIssueTypeController(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>获取质量问题类型列表（支持分页/筛选）</summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<QualityIssueTypeDto>>> GetList([FromQuery] QualityIssueTypeListRequest request)
    {
        var query = _context.Set<CfQualityIssueType>().AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(t => t.FStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(t => t.FCategory == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Module))
            query = query.Where(t => t.FModule == request.Module);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(t => t.FName.Contains(request.Keyword) || t.FCode.Contains(request.Keyword));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.FIsBuiltIn)
            .ThenBy(t => t.FCode)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new QualityIssueTypeDto
            {
                Id = t.FID,
                Code = t.FCode,
                Name = t.FName,
                Description = t.FDescription,
                Module = t.FModule,
                SourceAutoPlugin = t.FSourceAutoPlugin,
                SeverityLevel = t.FSeverityLevel,
                Category = t.FCategory,
                IsBuiltIn = t.FIsBuiltIn,
                SuggestedFix = t.FSuggestedFix,
                DetailRoute = t.FDetailRoute,
                DispatchMode = t.FDispatchMode,
                DispatchTarget = t.FDispatchTarget,
                ResolveMode = t.FResolveMode,
                CardFlowCode = t.FCardFlowCode,
                CardTemplateCode = t.FCardTemplateCode,
                ActionSchemaJson = t.FActionSchemaJson,
                AfterResolvedAction = t.FAfterResolvedAction,
                AggregationMode = t.FAggregationMode,
                OrgScoped = t.FOrgScoped,
                TimeoutHours = t.FTimeoutHours,
                Status = t.FStatus,
                CreatedAt = t.FCreatedTime,
                UpdatedAt = t.FUpdatedTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<QualityIssueTypeDto>>.Success(new PagedResult<QualityIssueTypeDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.Page,
            PageSize = request.PageSize
        });
    }

    /// <summary>新增自定义质量问题类型</summary>
    [HttpPost]
    public async Task<ApiResult<QualityIssueTypeDto>> Create([FromBody] CreateQualityIssueTypeRequest request)
    {
        // 校验 Code 唯一性
        var exists = await _context.Set<CfQualityIssueType>()
            .AnyAsync(t => t.FCode == request.Code);
        if (exists)
            return ApiResult<QualityIssueTypeDto>.Fail($"问题类型编码 '{request.Code}' 已存在");

        var entity = new CfQualityIssueType
        {
            FCode = request.Code,
            FName = request.Name,
            FDescription = request.Description,
            FModule = request.Module,
            FSourceAutoPlugin = request.SourceAutoPlugin,
            FSeverityLevel = request.SeverityLevel,
            FCategory = request.Category,
            FIsBuiltIn = false, // 新增的一定不是内置
            FSuggestedFix = request.SuggestedFix,
            FDetailRoute = request.DetailRoute,
            FDispatchMode = request.DispatchMode,
            FDispatchTarget = request.DispatchTarget,
            FResolveMode = request.ResolveMode,
            FCardFlowCode = request.CardFlowCode,
            FCardTemplateCode = request.CardTemplateCode,
            FActionSchemaJson = request.ActionSchemaJson,
            FAfterResolvedAction = request.AfterResolvedAction,
            FAggregationMode = string.IsNullOrWhiteSpace(request.AggregationMode) ? "BatchIssue" : request.AggregationMode,
            FOrgScoped = request.OrgScoped,
            FTimeoutHours = request.TimeoutHours,
            FStatus = request.Status,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _context.Set<CfQualityIssueType>().Add(entity);
        await _context.SaveChangesAsync();

        return ApiResult<QualityIssueTypeDto>.Success(MapToDto(entity), "创建成功");
    }

    /// <summary>更新质量问题类型（内置类型仅可改派发配置）</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] UpdateQualityIssueTypeRequest request)
    {
        var entity = await _context.Set<CfQualityIssueType>().AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);
        if (entity == null)
            return ApiResult.Fail("问题类型不存在", 404);

        if (entity.FIsBuiltIn)
        {
            // 内置类型仅允许修改派发配置相关字段
            if (request.DispatchMode != null)
                entity.FDispatchMode = request.DispatchMode;
            if (request.DispatchTarget != null)
                entity.FDispatchTarget = request.DispatchTarget;
            if (request.ResolveMode != null)
                entity.FResolveMode = request.ResolveMode;
            if (request.CardFlowCode != null)
                entity.FCardFlowCode = request.CardFlowCode;
            if (request.CardTemplateCode != null)
                entity.FCardTemplateCode = request.CardTemplateCode;
            if (request.ActionSchemaJson != null)
                entity.FActionSchemaJson = request.ActionSchemaJson;
            if (request.AfterResolvedAction != null)
                entity.FAfterResolvedAction = request.AfterResolvedAction;
            if (request.AggregationMode != null)
                entity.FAggregationMode = request.AggregationMode;
            if (request.OrgScoped.HasValue)
                entity.FOrgScoped = request.OrgScoped.Value;
            if (request.TimeoutHours.HasValue)
                entity.FTimeoutHours = request.TimeoutHours.Value;
            if (request.Status.HasValue)
                entity.FStatus = request.Status.Value;
        }
        else
        {
            // 非内置类型可修改所有字段
            if (request.Name != null) entity.FName = request.Name;
            if (request.Description != null) entity.FDescription = request.Description;
            if (request.Module != null) entity.FModule = request.Module;
            if (request.SourceAutoPlugin != null) entity.FSourceAutoPlugin = request.SourceAutoPlugin;
            if (request.SeverityLevel != null) entity.FSeverityLevel = request.SeverityLevel;
            if (request.Category != null) entity.FCategory = request.Category;
            if (request.SuggestedFix != null) entity.FSuggestedFix = request.SuggestedFix;
            if (request.DetailRoute != null) entity.FDetailRoute = request.DetailRoute;
            if (request.DispatchMode != null) entity.FDispatchMode = request.DispatchMode;
            if (request.DispatchTarget != null) entity.FDispatchTarget = request.DispatchTarget;
            if (request.ResolveMode != null) entity.FResolveMode = request.ResolveMode;
            if (request.CardFlowCode != null) entity.FCardFlowCode = request.CardFlowCode;
            if (request.CardTemplateCode != null) entity.FCardTemplateCode = request.CardTemplateCode;
            if (request.ActionSchemaJson != null) entity.FActionSchemaJson = request.ActionSchemaJson;
            if (request.AfterResolvedAction != null) entity.FAfterResolvedAction = request.AfterResolvedAction;
            if (request.AggregationMode != null) entity.FAggregationMode = request.AggregationMode;
            if (request.OrgScoped.HasValue) entity.FOrgScoped = request.OrgScoped.Value;
            if (request.TimeoutHours.HasValue) entity.FTimeoutHours = request.TimeoutHours.Value;
            if (request.Status.HasValue) entity.FStatus = request.Status.Value;
        }

        entity.FUpdatedTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResult.Ok("更新成功");
    }

    /// <summary>删除质量问题类型（仅非内置）</summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        var entity = await _context.Set<CfQualityIssueType>().FindAsync(id);
        if (entity == null)
            return ApiResult.Fail("问题类型不存在", 404);

        if (entity.FIsBuiltIn)
            return ApiResult.Fail("内置类型不允许删除");

        _context.Set<CfQualityIssueType>().Remove(entity);
        await _context.SaveChangesAsync();

        return ApiResult.Ok("删除成功");
    }

    private static QualityIssueTypeDto MapToDto(CfQualityIssueType entity) => new()
    {
        Id = entity.FID,
        Code = entity.FCode,
        Name = entity.FName,
        Description = entity.FDescription,
        Module = entity.FModule,
        SourceAutoPlugin = entity.FSourceAutoPlugin,
        SeverityLevel = entity.FSeverityLevel,
        Category = entity.FCategory,
        IsBuiltIn = entity.FIsBuiltIn,
        SuggestedFix = entity.FSuggestedFix,
        DetailRoute = entity.FDetailRoute,
        DispatchMode = entity.FDispatchMode,
        DispatchTarget = entity.FDispatchTarget,
        ResolveMode = entity.FResolveMode,
        CardFlowCode = entity.FCardFlowCode,
        CardTemplateCode = entity.FCardTemplateCode,
        ActionSchemaJson = entity.FActionSchemaJson,
        AfterResolvedAction = entity.FAfterResolvedAction,
        AggregationMode = entity.FAggregationMode,
        OrgScoped = entity.FOrgScoped,
        TimeoutHours = entity.FTimeoutHours,
        Status = entity.FStatus,
        CreatedAt = entity.FCreatedTime,
        UpdatedAt = entity.FUpdatedTime
    };
}
