namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 理赔审批配置详情 DTO
/// </summary>
public class InsApprovalConfigDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string StepCode { get; set; } = string.Empty;
    public int ApproverType { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRoleCode { get; set; }
    public bool CanReject { get; set; }
    public int? RejectTargetStep { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 理赔审批配置列表项 DTO
/// </summary>
public class InsApprovalConfigListItemDto
{
    public long Id { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string StepCode { get; set; } = string.Empty;
    public int ApproverType { get; set; }
    public string? ApproverName { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 创建审批配置请求
/// </summary>
public class CreateInsApprovalConfigRequest
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string StepCode { get; set; } = string.Empty;
    public int ApproverType { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRoleCode { get; set; }
    public bool CanReject { get; set; } = true;
    public int? RejectTargetStep { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新审批配置请求
/// </summary>
public class UpdateInsApprovalConfigRequest
{
    public string StepName { get; set; } = string.Empty;
    public string StepCode { get; set; } = string.Empty;
    public int ApproverType { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRoleCode { get; set; }
    public bool CanReject { get; set; } = true;
    public int? RejectTargetStep { get; set; }
    public string? Remark { get; set; }
}
