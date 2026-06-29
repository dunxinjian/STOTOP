using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsCoInsuranceFund : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FFundName { get; set; } = string.Empty;          // 基金名称
    public string FFundCode { get; set; } = string.Empty;          // 基金编码
    public int FBusinessType { get; set; } = 1;                    // 当前仅1=三轮车，预留扩展
    public string? FFundDescription { get; set; }                  // 基金说明
    public decimal FTotalContributions { get; set; }               // 累计缴费
    public decimal FTotalPayouts { get; set; }                     // 累计赔付
    public decimal FFundBalance { get; set; }                      // 基金余额
    public decimal? FContributionStandard { get; set; }            // 缴费标准
    public int? FPaymentCycle { get; set; }                        // 1=月,2=季,3=年
    public decimal FDeductible { get; set; }                       // 免赔额
    public decimal? FSinglePayoutLimit { get; set; }               // 单次赔付上限
    public decimal? FAnnualPayoutLimit { get; set; }               // 年度赔付上限
    public int FFundStatus { get; set; } = 1;                      // 1=运行中, 2=已冻结, 3=已关闭
    public DateOnly FEffectiveDate { get; set; }                   // 生效日期
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public List<InsPolicy> Policies { get; set; } = new();
    public List<InsFundContribution> Contributions { get; set; } = new();
}
