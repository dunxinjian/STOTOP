namespace STOTOP.Module.Quality.Dtos.CarrierDashboard;

/// <summary>视图1 网点总览 KPI 卡（率值=末日快照，金额/计数=期间累计）</summary>
public class NetworkKpiDto
{
    /// <summary>当天及时签收率（主，末日快照）</summary>
    public decimal? SignRateToday { get; set; }
    /// <summary>48h 签收率（副，末日快照）</summary>
    public decimal? SignRate48h { get; set; }
    /// <summary>一频次出仓及时率（末日快照）</summary>
    public decimal? OutboundOnTimeRate { get; set; }
    /// <summary>滞留率（末日快照）</summary>
    public decimal? RetentionRate { get; set; }
    /// <summary>积压倍数（末日快照）</summary>
    public decimal? BacklogMultiple { get; set; }
    /// <summary>遗失率 ppm（末日快照）</summary>
    public decimal? LossRatePpm { get; set; }
    /// <summary>虚签投诉率（末日快照）</summary>
    public decimal? FakeSignRate { get; set; }
    /// <summary>考核金额合计（期间累计）</summary>
    public decimal TotalAssessFee { get; set; }
    /// <summary>问题件数（期间事件计数）</summary>
    public int ProblemEventCount { get; set; }
}

/// <summary>视图1 近30天趋势点（每天率值多网点件量加权）</summary>
public class NetworkTrendPointDto
{
    public string Date { get; set; } = string.Empty;
    public decimal? SignRateToday { get; set; }
    public decimal? OutboundOnTimeRate { get; set; }
    public decimal? RetentionRate { get; set; }
    public decimal? FakeSignRate { get; set; }
}

/// <summary>域分布 / 考核金额按域构成 共用项</summary>
public class DomainStatItem
{
    /// <summary>质量域 / 金额域名</summary>
    public string Domain { get; set; } = string.Empty;
    /// <summary>事件计数（域分布用）</summary>
    public int Count { get; set; }
    /// <summary>预估考核金额（金额构成用，期间累计）</summary>
    public decimal Fee { get; set; }
}

/// <summary>视图2 红黑榜单项</summary>
public class EmployeeRankItemDto
{
    public string EmpNo { get; set; } = string.Empty;
    public string? EmpName { get; set; }
    public string? NetworkCode { get; set; }
    /// <summary>该维度的值（计数或金额）</summary>
    public decimal Value { get; set; }
}

/// <summary>视图2 红黑榜（worst=表现差/数值高，best=表现好/数值低）</summary>
public class EmployeeRankDto
{
    public string Dimension { get; set; } = string.Empty;
    public List<EmployeeRankItemDto> Worst { get; set; } = new();
    public List<EmployeeRankItemDto> Best { get; set; } = new();
}

/// <summary>视图2 员工 21 指标明细行（计数=期间 SUM，金额=期间 SUM）</summary>
public class EmployeeMetricRowDto
{
    public string EmpNo { get; set; } = string.Empty;
    public string? EmpName { get; set; }
    public string? NetworkCode { get; set; }
    public int 派件量 { get; set; }
    public int 当日派签量 { get; set; }
    public int 应上门量 { get; set; }
    public int 未上门量 { get; set; }
    public int 客诉发起量 { get; set; }
    public int 工单定责量 { get; set; }
    public int 虚假签收数 { get; set; }
    public int 照片质检不合格数 { get; set; }
    public int 派送超时T0数 { get; set; }
    public int 派送超时T1数 { get; set; }
    public int 派送超时T2数 { get; set; }
    public int 派送超时T3数 { get; set; }
    public int 揽收不及时数 { get; set; }
    public int 上传不及时数 { get; set; }
    public int 问题件数 { get; set; }
    public int 违规虚假电联 { get; set; }
    public int 违规无效电联 { get; set; }
    public int 违规双签 { get; set; }
    public int 违规照片定位虚假 { get; set; }
    public int 违规签收文本不规范 { get; set; }
    public int 违规引导代收 { get; set; }
    public decimal 考核金额合计 { get; set; }
}

/// <summary>视图2 单员工事件时间线项</summary>
public class EmployeeEventItemDto
{
    public DateTime? Date { get; set; }
    public string? Waybill { get; set; }
    public string? NetworkName { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string? ProblemName { get; set; }
    public int Severity { get; set; }
    public decimal? Fee { get; set; }
}

/// <summary>视图3 问题件明细行</summary>
public class QualityEventRowDto
{
    public long Id { get; set; }
    public DateTime? Date { get; set; }
    public string? Waybill { get; set; }
    public string? NetworkCode { get; set; }
    public string? NetworkName { get; set; }
    public string? EmpNo { get; set; }
    public string? EmpNameRaw { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string? ProblemName { get; set; }
    public int Severity { get; set; }
    public bool IsAssessed { get; set; }
    public decimal? Fee { get; set; }
    public string? Platform { get; set; }
    /// <summary>待认领（员工匹配状态 ∈ {0,3}）</summary>
    public bool IsPending { get; set; }
    /// <summary>多域命中重点件（同运单 ≥2 域）</summary>
    public bool IsMultiDomain { get; set; }
}

/// <summary>视图3 分页结果</summary>
public class EventPageDto
{
    public List<QualityEventRowDto> Items { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>视图2 21 指标明细分页结果</summary>
public class EmployeeMetricsPageDto
{
    public List<EmployeeMetricRowDto> Items { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>视图3 问题件查询条件</summary>
public class EventQuery
{
    public string Carrier { get; set; } = "申通";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? NetworkCode { get; set; }
    public string? EmpNo { get; set; }
    public string? Domain { get; set; }
    public string? Platform { get; set; }
    public int? Severity { get; set; }
    public bool MultiDomainOnly { get; set; }
    public bool PendingOnly { get; set; }
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 50;
}

/// <summary>视图1 网点筛选下拉选项</summary>
public class NetworkOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
}
