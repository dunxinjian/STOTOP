namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 业务代理（主键为 F编号，不继承 BaseEntity）
/// </summary>
public class ExpAgent
{
    /// <summary>编号（主键）</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>代理级别</summary>
    public int FAgentLevel { get; set; } = 1;
    /// <summary>代理区域</summary>
    public string? FAgentRegion { get; set; }
    /// <summary>联系人</summary>
    public string? FContactName { get; set; }
    /// <summary>联系电话</summary>
    public string? FContactPhone { get; set; }
    /// <summary>地址</summary>
    public string? FAddress { get; set; }
    /// <summary>合作开始日期</summary>
    public DateOnly? FCooperationStartDate { get; set; }
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
