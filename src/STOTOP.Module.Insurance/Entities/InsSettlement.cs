using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsSettlement : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FClaimId { get; set; }                             // FK → INS出险记录
    public long FPolicyId { get; set; }                            // FK → INS保单
    public string FSettlementNumber { get; set; } = string.Empty;  // 理赔编号
    public int FSettlementType { get; set; }                       // 1=商业保险理赔, 2=共保基金理赔
    public DateOnly FApplyDate { get; set; }                       // 申请日期
    public long? FApplicantId { get; set; }                        // 申请人ID
    public string? FApplicantName { get; set; }                    // 申请人姓名
    public decimal? FAssessedAmount { get; set; }                  // 定损金额
    public decimal? FSettlementAmount { get; set; }                // 理赔金额
    public decimal? FSelfPayAmount { get; set; }                   // 自付金额
    public decimal? FDeductible { get; set; }                      // 免赔额
    public int FSettlementStatus { get; set; } = 1;                // 商业:1=已报案,2=定损中,3=理赔审核,4=已赔付,5=已拒赔; 共保:1=草稿,10=审批中,20=已通过,30=已拨付,99=已驳回
    public long? FCurrentStepId { get; set; }                      // FK → INS理赔审批配置
    public DateOnly? FPaymentDate { get; set; }                    // 赔付日期
    public string? FPaymentVoucher { get; set; }                   // 赔付凭证
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public InsClaim Claim { get; set; } = null!;
    public InsPolicy Policy { get; set; } = null!;
    public InsApprovalConfig? CurrentStep { get; set; }
    public List<InsApprovalRecord> ApprovalRecords { get; set; } = new();
}
