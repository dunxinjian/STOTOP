using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 申通员工日质量指标：按 业务日期×网点×员工 聚合的员工口径质量指标。
/// 表：QL申通_员工日质量指标。FID 由 DbContext 统一配 IDENTITY 主键，实体不声明。
/// </summary>
public class QlShentongEmployeeDailyMetric : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>承运商（固定"申通"）</summary>
    public string F承运商 { get; set; } = "申通";
    /// <summary>业务日期</summary>
    public DateTime F业务日期 { get; set; }
    /// <summary>统计年月（yyyyMM，由 ParseUtil.Ym 产出，无连字符）</summary>
    public string? F统计年月 { get; set; }
    /// <summary>网点编码</summary>
    public string F网点编码 { get; set; } = string.Empty;
    /// <summary>员工工号</summary>
    public string F员工工号 { get; set; } = string.Empty;
    /// <summary>员工姓名原文</summary>
    public string? F员工姓名原文 { get; set; }
    /// <summary>员工ID（匹配后回填）</summary>
    public long? F员工ID { get; set; }

    // ── 派签 ──
    public int? F派件量 { get; set; }
    public int? F当日派签量 { get; set; }
    public decimal? F当日派签率 { get; set; }
    public int? F应上门量 { get; set; }
    public int? F未上门量 { get; set; }
    public decimal? F按需上门率 { get; set; }

    // ── 客诉 ──
    public int? F客诉发起量 { get; set; }
    public int? F工单定责量 { get; set; }
    public decimal? F客诉发起率 { get; set; }

    // ── 质检 / 时效 ──
    public int? F虚假签收数 { get; set; }
    public int? F照片质检不合格数 { get; set; }
    public int? F派送超时T0数 { get; set; }
    public int? F派送超时T1数 { get; set; }
    public int? F派送超时T2数 { get; set; }
    public int? F派送超时T3数 { get; set; }
    public int? F揽收不及时数 { get; set; }
    public int? F上传不及时数 { get; set; }
    public int? F问题件数 { get; set; }

    // ── 违规 ──
    public int? F违规虚假电联 { get; set; }
    public int? F违规无效电联 { get; set; }
    public int? F违规双签 { get; set; }
    public int? F违规照片定位虚假 { get; set; }
    public int? F违规签收文本不规范 { get; set; }
    public int? F违规引导代收 { get; set; }
    public decimal? F回访真实率 { get; set; }

    /// <summary>考核金额合计</summary>
    public decimal? F考核金额合计 { get; set; }
    /// <summary>来源批次ID</summary>
    public long? F来源批次ID { get; set; }
    /// <summary>创建时间</summary>
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
