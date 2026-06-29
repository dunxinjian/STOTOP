using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 申通承运商质量事件：各 STG 来源明细归一后的单条质量事件（一行一事件），是统一质控的事实表。
/// 表：QL申通_承运商质量事件。FID 由 DbContext 统一配 IDENTITY 主键，实体不声明。
/// </summary>
public class QlShentongQualityEvent : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>承运商（固定"申通"）</summary>
    public string F承运商 { get; set; } = "申通";
    /// <summary>业务日期</summary>
    public DateTime? F业务日期 { get; set; }
    /// <summary>统计年月（yyyyMM，由 ParseUtil.Ym 产出，无连字符）</summary>
    public string? F统计年月 { get; set; }
    /// <summary>运单号</summary>
    public string? F运单号 { get; set; }
    /// <summary>网点编码</summary>
    public string? F网点编码 { get; set; }
    /// <summary>网点名称</summary>
    public string? F网点名称 { get; set; }
    /// <summary>员工工号</summary>
    public string? F员工工号 { get; set; }
    /// <summary>员工姓名原文</summary>
    public string? F员工姓名原文 { get; set; }
    /// <summary>员工ID（匹配后回填）</summary>
    public long? F员工ID { get; set; }
    /// <summary>电商平台</summary>
    public string? F电商平台 { get; set; }
    /// <summary>质量域</summary>
    public string F质量域 { get; set; } = string.Empty;
    /// <summary>问题类型编码</summary>
    public string? F问题类型编码 { get; set; }
    /// <summary>问题类型名称</summary>
    public string? F问题类型名称 { get; set; }
    /// <summary>严重度</summary>
    public int F严重度 { get; set; }
    /// <summary>是否考核件</summary>
    public bool F是否考核件 { get; set; }
    /// <summary>考核金额</summary>
    public decimal? F考核金额 { get; set; }
    /// <summary>责任方</summary>
    public string? F责任方 { get; set; }
    /// <summary>来源 STG 表名</summary>
    public string F来源STG表 { get; set; } = string.Empty;
    /// <summary>来源行ID（STG 表 FID）</summary>
    public long F来源行ID { get; set; }
    /// <summary>来源批次ID</summary>
    public long? F来源批次ID { get; set; }
    /// <summary>关键字段 JSON（原始关键列快照）</summary>
    public string? F关键字段JSON { get; set; }
    /// <summary>网点匹配状态（0=未匹配 1=已匹配 等）</summary>
    public int F网点匹配状态 { get; set; }
    /// <summary>员工匹配状态（0=未匹配 1=已匹配 等）</summary>
    public int F员工匹配状态 { get; set; }
    /// <summary>是否已提升异常单</summary>
    public bool F是否已提升异常单 { get; set; }
    /// <summary>关联异常单ID</summary>
    public long? F关联异常单ID { get; set; }
    /// <summary>创建时间</summary>
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
