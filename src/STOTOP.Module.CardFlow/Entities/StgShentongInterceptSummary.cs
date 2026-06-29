using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_拦截汇总 暂存表实体。
/// 来源：网点数据报表导出（网点数据报表导出__*.xls，扩展名为 .xls 实为 xlsx，sheet 名 "0"）。
/// 单行表头，10 列，所属网点×日期粒度（1 行/网点/日期）。列名均合法（无非法字符），dbColumn 直接加 F 前缀。
/// 10 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongInterceptSummary : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongBacklogMonitor）
    public long F批次ID { get; set; }
    public int F处理状态 { get; set; }
    public string? F错误信息 { get; set; }
    public long? F关联凭证ID { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public string? FDataScopeId { get; set; }
    public long? FSourceWorkItemId { get; set; }
    public bool FIsRevoked { get; set; }
    public long FOrgId { get; set; }
    public long? F账套ID { get; set; }
    public string? F归属网点编号 { get; set; }

    // 业务字段（10 列；列名均合法）
    public string? F统计日期 { get; set; }
    public string? F所属网点 { get; set; }
    public string? F应拦截网点 { get; set; }
    public string? F应拦截量 { get; set; }
    public string? F拦截成功量 { get; set; }
    public string? F拦截成功率 { get; set; }
    public string? F未拦截成功量 { get; set; }
    public string? F及时转出量 { get; set; }
    public string? F及时转出率 { get; set; }
    public string? F未及时转出量 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
