using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 末端驿站（主键为 F编号，不继承 BaseEntity）
/// 混合模式：直营=组织扩展，合作=独立实体
/// </summary>
public class ExpLastMileStation
{
    /// <summary>编号（主键）</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>类型 1=直营 2=合作</summary>
    public int FStationType { get; set; } = 1;
    /// <summary>组织ID</summary>
    public long? FOrgId { get; set; }
    /// <summary>名称（合作驿站自有名称）</summary>
    public string? FName { get; set; }
    /// <summary>地址</summary>
    public string? FAddress { get; set; }
    /// <summary>运营时间</summary>
    public string? FBusinessHours { get; set; }
    /// <summary>日处理量</summary>
    public int? FDailyVolume { get; set; }
    /// <summary>货架数</summary>
    public int? FShelfCount { get; set; }
    /// <summary>面积</summary>
    public decimal? FArea { get; set; }
    /// <summary>联系人</summary>
    public string? FContactName { get; set; }
    /// <summary>联系电话</summary>
    public string? FContactPhone { get; set; }
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

    /// <summary>关联组织（直营时有值）</summary>
    public SysOrganization? Organization { get; set; }
}
