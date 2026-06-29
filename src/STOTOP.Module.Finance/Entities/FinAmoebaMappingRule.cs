using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAmoebaMappingRule : BaseEntity, IOrgScoped
{
    public long FUnitId { get; set; }           // F经营单元ID
    public int FDataSourceType { get; set; }    // F数据源类型: 1=计费结果 2=凭证分录 3=资产卡片
    public string? FSiteCode { get; set; }      // F网点编号
    public string? FBrandCode { get; set; }     // F品牌编码
    public string? FDirection { get; set; }     // F业务方向: 进港/出港/综合
    public string? FAuxField { get; set; }      // F辅助匹配字段
    public string? FAuxValue { get; set; }      // F辅助匹配值
    public int FPriority { get; set; }          // F优先级
    public string? FRemark { get; set; }        // F备注
    public long FOrgId { get; set; }            // F组织ID
}
