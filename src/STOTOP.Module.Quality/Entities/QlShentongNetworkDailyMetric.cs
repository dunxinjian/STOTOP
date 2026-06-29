using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 申通网点日质量指标：按 业务日期×网点 聚合的网点口径质量指标（含物流信息、出仓、滞留、签收、积压、遗失、投诉、拦截、渗透等）。
/// 表：QL申通_网点日质量指标。FID 由 DbContext 统一配 IDENTITY 主键，实体不声明。
/// 率字段用 decimal(9,4)，金额/量级 decimal(18,2) 或 int。
/// </summary>
public class QlShentongNetworkDailyMetric : BaseEntity, IOrgScoped
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
    /// <summary>网点名称</summary>
    public string? F网点名称 { get; set; }
    /// <summary>片区</summary>
    public string? F片区 { get; set; }
    /// <summary>省区</summary>
    public string? F省区 { get; set; }

    // ── 物流信息上传/缺失/准确 ──
    public decimal? F揽收上传不及时率 { get; set; }
    public decimal? F派件上传不及时率 { get; set; }
    public decimal? F签收上传不及时率 { get; set; }
    public decimal? F揽收缺失率 { get; set; }
    public decimal? F派件缺失率 { get; set; }
    public decimal? F到件缺失率 { get; set; }
    public decimal? F不准确率 { get; set; }
    public decimal? F到件不准确率 { get; set; }

    // ── 揽收 ──
    public decimal? F及时揽收率 { get; set; }
    public int? F未及时揽收量 { get; set; }

    // ── 出仓 ──
    public decimal? F一频次出仓及时率 { get; set; }
    public int? F未及时出仓量 { get; set; }
    public decimal? F出仓预估考核金额 { get; set; }

    // ── 滞留 ──
    public decimal? F滞留率 { get; set; }
    public int? F考核滞留量 { get; set; }
    public decimal? F滞留预估考核金额 { get; set; }

    // ── 签收 ──
    public decimal? F一阶段及时签收率 { get; set; }
    public decimal? F二阶段及时签收率 { get; set; }
    public decimal? F当天及时签收率 { get; set; }
    public decimal? F派送预估考核金额 { get; set; }
    public decimal? F有偿派费金额 { get; set; }
    public decimal? F预计返款金额 { get; set; }
    public decimal? F48h签收率 { get; set; }
    public decimal? F签收率考核金额 { get; set; }

    // ── 积压 ──
    public int? F日均出港量 { get; set; }
    public int? F日均进港量 { get; set; }
    public decimal? F积压倍数 { get; set; }
    public int? F超3天积压量 { get; set; }
    public int? F超5天积压量 { get; set; }
    public int? F超7天积压量 { get; set; }

    // ── 遗失 ──
    public decimal? F遗失率ppm { get; set; }
    public int? F遗失量 { get; set; }

    // ── 进港投诉 / 虚签 ──
    public int? F进港投诉量 { get; set; }
    public decimal? F进港投诉率 { get; set; }
    public decimal? F虚签投诉率 { get; set; }
    public int? F7日虚签投诉量 { get; set; }

    // ── 拦截 / 转出 ──
    public int? F应拦截量 { get; set; }
    public decimal? F拦截成功率 { get; set; }
    public decimal? F及时转出率 { get; set; }

    // ── 渗透 / 建站 / 喵柜 ──
    public decimal? F自建渗透率 { get; set; }
    public decimal? F渗透率目标 { get; set; }
    public int? F建站待完成 { get; set; }
    public int? F喵柜激活格口数 { get; set; }

    /// <summary>考核金额合计</summary>
    public decimal? F考核金额合计 { get; set; }
    /// <summary>来源批次ID</summary>
    public long? F来源批次ID { get; set; }
    /// <summary>创建时间</summary>
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
