using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsApprovalRecord : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FSettlementId { get; set; }                        // FK → INS理赔记录
    public long FStepConfigId { get; set; }                        // FK → INS理赔审批配置
    public int FStepOrder { get; set; }                            // 环节序号
    public string FStepName { get; set; } = string.Empty;          // 环节名称
    public long FApproverId { get; set; }                          // 审批人ID
    public string FApproverName { get; set; } = string.Empty;      // 审批人姓名
    public int FApprovalAction { get; set; }                       // 1=通过, 2=驳回, 3=转办
    public string? FApprovalComment { get; set; }                  // 审批意见
    public long? FTransferTargetId { get; set; }                   // 转办目标人ID
    public string? FTransferTargetName { get; set; }               // 转办目标人姓名
    public DateTime FApprovalTime { get; set; } = DateTime.Now;    // 审批时间
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public InsSettlement Settlement { get; set; } = null!;
    public InsApprovalConfig StepConfig { get; set; } = null!;
}
