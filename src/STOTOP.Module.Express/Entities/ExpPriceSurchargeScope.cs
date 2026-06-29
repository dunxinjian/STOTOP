namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 加收规则作用域关联
/// </summary>
public class ExpPriceSurchargeScope
{
    public long Id { get; set; }
    /// <summary>加收规则ID</summary>
    public long FSurchargeId { get; set; }
    /// <summary>关联类型 KH/DL/WD/YW/CB/YZ/QUOTATION</summary>
    public string FLinkedType { get; set; } = string.Empty;
    /// <summary>业务对象编号 或 报价ID</summary>
    public string FLinkedId { get; set; } = string.Empty;

    /// <summary>所属加收规则</summary>
    public ExpPriceSurcharge Surcharge { get; set; } = null!;
}
