namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 理赔审批记录详情 DTO
/// </summary>
public class InsApprovalRecordDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long SettlementId { get; set; }
    public long StepConfigId { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public long ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public int ApprovalAction { get; set; }
    public string? ApprovalComment { get; set; }
    public long? TransferTargetId { get; set; }
    public string? TransferTargetName { get; set; }
    public DateTime ApprovalTime { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 理赔审批记录列表项 DTO
/// </summary>
public class InsApprovalRecordListItemDto
{
    public long Id { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public int ApprovalAction { get; set; }
    public string? ApprovalComment { get; set; }
    public DateTime ApprovalTime { get; set; }
}

/// <summary>
/// 创建审批记录请求（审批操作）
/// </summary>
public class CreateInsApprovalRecordRequest
{
    public long SettlementId { get; set; }
    public int ApprovalAction { get; set; }
    public string? ApprovalComment { get; set; }
    public long? TransferTargetId { get; set; }
    public string? TransferTargetName { get; set; }
}
