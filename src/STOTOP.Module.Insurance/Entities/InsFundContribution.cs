using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsFundContribution : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FFundId { get; set; }                              // FK → INS共保基金
    public long? FPolicyId { get; set; }                           // FK → INS保单
    public int FBusinessType { get; set; }
    public long FRelatedObjectId { get; set; }
    public string? FRelatedObjectName { get; set; }
    public string FContributionNumber { get; set; } = string.Empty; // 缴费编号
    public decimal FContributionAmount { get; set; }               // 缴费金额
    public DateOnly FPeriodStart { get; set; }                     // 缴费周期开始
    public DateOnly FPeriodEnd { get; set; }                       // 缴费周期结束
    public DateOnly? FPaymentDate { get; set; }                    // 缴费日期
    public int FPaymentStatus { get; set; } = 1;                   // 1=待缴,2=已缴,3=逾期,4=减免
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public InsCoInsuranceFund Fund { get; set; } = null!;
    public InsPolicy? Policy { get; set; }
}
