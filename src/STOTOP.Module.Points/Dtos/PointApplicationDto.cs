using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分申请 - 列表 DTO
/// </summary>
public class PointApplicationListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public long RuleId { get; set; }
    public string? RuleName { get; set; }
    public int PointValue { get; set; }
    public string ApplicationNote { get; set; } = string.Empty;
    public int Status { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovalTime { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 积分申请 - 详情 DTO
/// </summary>
public class PointApplicationDetailDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public long RuleId { get; set; }
    public string? RuleName { get; set; }
    public string? SourceName { get; set; }
    public int PointValue { get; set; }
    public string ApplicationNote { get; set; } = string.Empty;
    public string? Attachment { get; set; }
    public int Status { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApprovalComment { get; set; }
    public DateTime? ApprovalTime { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 提交积分申请请求
/// </summary>
public class SubmitPointApplicationRequest
{
    public long RuleId { get; set; }
    public string ApplicationNote { get; set; } = string.Empty;
    public string? Attachment { get; set; }
}

/// <summary>
/// 审批积分申请请求
/// </summary>
public class ApprovePointApplicationRequest
{
    public string? ApprovalComment { get; set; }
}

/// <summary>
/// 我的申请查询请求
/// </summary>
public class MyApplicationPagedRequest : PagedRequest
{
    public int? Status { get; set; }
}

/// <summary>
/// 待审批查询请求
/// </summary>
public class PendingApplicationPagedRequest : PagedRequest
{
    public long? ApplicantId { get; set; }
}

/// <summary>
/// 申请列表查询请求（管理员）
/// </summary>
public class ApplicationPagedRequest : PagedRequest
{
    public int? Status { get; set; }
    public long? ApplicantId { get; set; }
    public long? RuleId { get; set; }
}
