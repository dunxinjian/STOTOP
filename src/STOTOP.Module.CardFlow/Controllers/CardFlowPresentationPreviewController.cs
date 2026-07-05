using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// 设计期呈现预览：喂"草稿定义 + 样例数据"给运行时同一个 StageViewProfileResolver，返回节点工作视图真值，
/// 让设计器预览与运行时呈现一字不差（替代前端复刻的 access 归一 / 脱敏 / 聚合逻辑，消除口径漂移）。
/// 复用现有已注册服务，controller 由 MVC 自动发现，零 DI 注册改动（并发工作树友好）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/definitions")]
public class CardFlowPresentationPreviewController : ControllerBase
{
    private readonly STOTOPDbContext _db;
    private readonly IStageConfigParser _stageConfigParser;
    private readonly IStageViewProfileResolver _stageViewResolver;

    public CardFlowPresentationPreviewController(
        STOTOPDbContext db,
        IStageConfigParser stageConfigParser,
        IStageViewProfileResolver stageViewResolver)
    {
        _db = db;
        _stageConfigParser = stageConfigParser;
        _stageViewResolver = stageViewResolver;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    public sealed record CardPresentationPreviewDto(
        StageWorkViewDto WorkView,
        string RedactedDataJson,
        List<CardDetailRowDto> RedactedDetails);

    [HttpPost("{id:long}/draft-version/preview-presentation")]
    public async Task<ApiResult<CardPresentationPreviewDto>> PreviewPresentation(
        long id, [FromBody] CardPresentationPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StageKey))
        {
            return ApiResult<CardPresentationPreviewDto>.Fail("缺少 stageKey");
        }

        // 版本回退：指定版本 → 草稿 → 当前发布版（与 preview-path 同口径）
        var version = request.FlowVersionId.HasValue
            ? await _db.Set<CfFlowVersion>().FirstOrDefaultAsync(v => v.FID == request.FlowVersionId.Value && v.FFlowDefinitionId == id)
            : await _db.Set<CfFlowVersion>()
                .Where(v => v.FFlowDefinitionId == id && v.FStatus == "draft")
                .OrderByDescending(v => v.FVersionNumber)
                .FirstOrDefaultAsync();
        version ??= await _db.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == id && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync();
        if (version == null)
        {
            return ApiResult<CardPresentationPreviewDto>.Fail("没有可预览的流程版本");
        }

        var stageDefinition = await _db.Set<CfStageDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FFlowVersionId == version.FID && s.FStageKey == request.StageKey);
        if (stageDefinition == null)
        {
            return ApiResult<CardPresentationPreviewDto>.Fail($"节点 {request.StageKey} 不存在");
        }

        var normalizedConfig = _stageConfigParser.Parse(stageDefinition.FInputFieldsJson);
        var orgId = GetOrgId();

        // 内存构造卡片与明细样例，喂运行时 resolver（纯内存无 DB 依赖）
        var card = new CfCard { FDataJson = request.DataJson ?? "{}", FOrgId = orgId };
        var details = (request.Details ?? new List<PreviewDetailRow>())
            .OrderBy(row => row.SortOrder)
            .Select((row, index) => new CfCardDetail
            {
                FDataJson = row.DataJson,
                FDetailTableKey = string.IsNullOrWhiteSpace(row.DetailTableKey) ? "default" : row.DetailTableKey,
                FSortOrder = row.SortOrder != 0 ? row.SortOrder : index
            })
            .ToList();

        var resolved = _stageViewResolver.Resolve(
            version.FCardSchemaJson,
            version.FDetailSchemaJson,
            stageDefinition,
            card,
            details,
            GetUserId(),
            normalizedConfig);

        var dto = new CardPresentationPreviewDto(
            StageWorkViewMapper.ToDto(resolved),
            resolved.RedactedDataJson,
            resolved.RedactedDetails.Select(row => new CardDetailRowDto
            {
                Id = row.Id,
                DetailTableKey = string.IsNullOrWhiteSpace(row.DetailTableKey) ? "default" : row.DetailTableKey,
                SortOrder = row.SortOrder,
                DataJson = row.DataJson
            }).ToList());

        return ApiResult<CardPresentationPreviewDto>.Success(dto);
    }
}
