namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// 流程定义候选项 DTO（匹配失败时供前端选择）
/// </summary>
public class FlowDefinitionCandidateDto
{
    public long FID { get; set; }
    public string FFlowName { get; set; } = string.Empty;
    public string FFlowCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
}
