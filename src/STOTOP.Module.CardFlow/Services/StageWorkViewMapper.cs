using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// StageViewResolutionResult → StageWorkViewDto 投影。设计期呈现预览（CardFlowPresentationPreviewController）
/// 与运行时（CardService.GetByIdAsync）共用同一口径，保证"设计器预览 = 运行时真值"。
/// 注：CardService 目前仍保留一份等价的私有 ToStageWorkViewDto，后续可收敛到此处（避免本次改动并发中的 CardService）。
/// </summary>
public static class StageWorkViewMapper
{
    public static StageWorkViewDto ToDto(StageViewResolutionResult resolved)
    {
        return new StageWorkViewDto
        {
            Sections = resolved.Sections.Select(section => new StageViewSectionDto
            {
                Key = section.Key,
                Title = section.Title,
                Type = section.Type,
                Fields = section.Fields.Select(field => new StageViewFieldDto
                {
                    FieldKey = field.FieldKey,
                    Label = field.Label
                }).ToList()
            }).ToList(),
            FieldAccess = resolved.FieldAccess.ToDictionary(
                pair => pair.Key,
                pair => new StageFieldAccessDto
                {
                    Access = pair.Value.Access,
                    Required = pair.Value.Required,
                    MaskPattern = pair.Value.MaskPattern
                }),
            DetailAccess = resolved.DetailAccess.ToDictionary(
                pair => pair.Key,
                pair => new StageDetailAccessDto
                {
                    Access = pair.Value.Access,
                    Required = pair.Value.Required,
                    MaskPattern = pair.Value.MaskPattern
                }),
            Components = resolved.Presentation.Components,
            DetailSummary = resolved.Presentation.DetailSummary,
            ActionPolicy = new StageActionPolicyDto
            {
                AllowedActions = resolved.AllowedActions,
                CustomActions = resolved.CustomActions.Select(action => new CustomActionDto
                {
                    Code = action.Code,
                    Label = action.Label,
                    Handler = action.Handler,
                    RequireOpinion = action.RequireOpinion
                }).ToList()
            },
            Summary = resolved.Summary == null
                ? null
                : new StageSummaryProfileDto { Fields = resolved.Summary.Fields }
        };
    }
}
