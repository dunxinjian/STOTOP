namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 业务对象报价汇总查询请求
/// </summary>
public class ClientQuotationSummaryQuery
{
    /// <summary>搜索关键词（模糊匹配名称/编号）</summary>
    public string? Keyword { get; set; }
    /// <summary>业务对象类型过滤：KH/DL/WD/CB/YZ</summary>
    public string? Type { get; set; }
    /// <summary>页码</summary>
    public int PageIndex { get; set; } = 1;
    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 50;
    /// <summary>
    /// 报价过滤：true=只返回有报价的, false=只返回无报价的, null=返回全部
    /// </summary>
    public bool? HasQuotation { get; set; }
}

/// <summary>
/// 业务对象报价汇总项
/// </summary>
public class ClientQuotationSummaryDto
{
    /// <summary>业务对象编号</summary>
    public string Id { get; set; } = "";
    /// <summary>名称</summary>
    public string Name { get; set; } = "";
    /// <summary>编号</summary>
    public string Code { get; set; } = "";
    /// <summary>业务对象类型 KH/DL/WD/CB/YZ</summary>
    public string Type { get; set; } = "";
    /// <summary>关联报价方案数量</summary>
    public int QuotationCount { get; set; }
}
