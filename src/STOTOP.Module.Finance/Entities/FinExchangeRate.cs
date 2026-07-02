using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinExchangeRate : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }  // 组织ID
    public string FCurrencyCode { get; set; } = string.Empty;  // USD/EUR/JPY/HKD等
    public string FCurrencyName { get; set; } = string.Empty;  // 美元/欧元/日元/港币
    public decimal FRate { get; set; }                          // 汇率（1外币=?人民币）
    public DateTime FEffectiveDate { get; set; }                // 生效日期
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
