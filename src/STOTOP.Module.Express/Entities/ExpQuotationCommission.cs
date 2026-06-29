using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价_佣金配置
/// </summary>
public class ExpQuotationCommission : BaseEntity
{
    /// <summary>报价ID</summary>
    public long FQuotationId { get; set; }
    /// <summary>是否启用</summary>
    public bool FEnabled { get; set; }
    /// <summary>计算方式 1固定 2费率 3按重量</summary>
    public int FCalcMethod { get; set; } = 1;
    /// <summary>费率</summary>
    public decimal? FRate { get; set; }
    /// <summary>固定金额</summary>
    public decimal? FFixedAmount { get; set; }
    /// <summary>单位重量金额</summary>
    public decimal? FWeightAmount { get; set; }
    /// <summary>关联业务对象类型 DL/WD/YW/CB/YZ</summary>
    public string FTargetClientType { get; set; } = string.Empty;
    /// <summary>关联业务对象ID</summary>
    public string? FTargetClientId { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    /// <summary>所属报价</summary>
    public ExpQuotation Quotation { get; set; } = null!;
}
