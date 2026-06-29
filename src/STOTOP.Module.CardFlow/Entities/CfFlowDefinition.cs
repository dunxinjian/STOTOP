using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfFlowDefinition : BaseEntity, IOrgScoped
{
    public string FFlowName { get; set; } = string.Empty;
    public string FFlowCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public string FStatus { get; set; } = "draft";
    public string? FNumberTemplate { get; set; }
    public string? FTitleTemplate { get; set; }
    public string? FAllowedRolesJson { get; set; }
    public long? FFlowGroupId { get; set; }
    public long FOrgId { get; set; }
    /// <summary>流程绑定的目标账套ID（跨账套场景必填）</summary>
    public long? FAccountSetId { get; set; }
    /// <summary>触发方式配置 JSON（human/fileUpload/scheduled/conditional + 参数）</summary>
    public string? FTriggerConfigJson { get; set; }
    /// <summary>
    /// 文件匹配规则JSON，用于自动匹配上传文件到流程定义
    /// 格式: { "fullColumnIdentifier": "col1,col2,...", "columnIdentifier": "col1,col2", "fileNamePattern": "*交易明细*" }
    /// </summary>
    public string? FMatchPattern { get; set; }
    /// <summary>是否为可克隆的流程模板（"从模板创建"列表来源）</summary>
    public bool FIsTemplate { get; set; } = false;
    public long FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
