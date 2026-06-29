using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsPolicy : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    // 通用关联机制
    public int FBusinessType { get; set; }                         // 1=三轮车, 2=人员, 3=机动车, 4=财产
    public long FRelatedObjectId { get; set; }                     // 业务方主键
    public string? FRelatedObjectName { get; set; }                // 冗余（车牌号/姓名等）
    // 保险分类
    public int FInsuranceCategory { get; set; }                    // 1=商业保险, 2=共保基金
    public string? FInsuranceType { get; set; }                    // 机损险/三者险/交强险 等
    // 商业保险字段
    public long? FInsuranceCompanyId { get; set; }                 // FK → INS保险公司
    public string? FPolicyNumber { get; set; }                     // 保单号
    public decimal? FPremium { get; set; }                         // 保费
    public decimal? FInsuredAmount { get; set; }                   // 保额
    public string? FContactPerson { get; set; }                    // 联系人
    public string? FContactPhone { get; set; }                     // 联系电话
    // 共保基金字段
    public long? FCoInsuranceFundId { get; set; }                  // FK → INS共保基金
    public string? FParticipationNumber { get; set; }              // 参保编号
    public int? FPaymentCycle { get; set; }                        // 1=月,2=季,3=年
    public decimal? FPerPeriodAmount { get; set; }                 // 每期金额
    // 通用字段
    public DateOnly FEffectiveDate { get; set; }                   // 生效日期
    public DateOnly FExpiryDate { get; set; }                      // 到期日期
    public int FInsuranceStatus { get; set; } = 1;                 // 1=有效, 2=已过期, 3=已退保
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public InsCompany? InsuranceCompany { get; set; }
    public InsCoInsuranceFund? CoInsuranceFund { get; set; }
    public List<InsClaim> Claims { get; set; } = new();
    public List<InsSettlement> Settlements { get; set; } = new();
    public List<InsFundContribution> FundContributions { get; set; } = new();
}
