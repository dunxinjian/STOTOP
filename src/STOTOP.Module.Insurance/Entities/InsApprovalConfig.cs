using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsApprovalConfig : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public int FStepOrder { get; set; }                            // 环节序号
    public string FStepName { get; set; } = string.Empty;          // 环节名称
    public string FStepCode { get; set; } = string.Empty;          // 环节编码 review/approve/pay
    public int FApproverType { get; set; }                         // 1=指定人员, 2=指定角色, 3=部门负责人
    public long? FApproverId { get; set; }                         // 审批人ID
    public string? FApproverName { get; set; }                     // 审批人姓名
    public string? FApproverRoleCode { get; set; }                 // 审批角色编码
    public bool FCanReject { get; set; } = true;                   // 可驳回
    public int? FRejectTargetStep { get; set; }                    // 驳回目标环节
    public int FStatus { get; set; } = 1;
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
