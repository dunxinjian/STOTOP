using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_出仓考核汇总 暂存表实体。
/// 来源：出仓考核汇总导出（出仓考核汇总导出_导出任务_*.xlsx，sheet 出仓考核汇总导出）。
/// 单行表头，31 列，网点×日期粒度（1 行/网点/日期）。含大量全角括号列
/// （一频次（考核）应出仓量、一频次（考核）预估考核金额（元）等），dbColumn 去掉括号 → F一频次考核应出仓量 等。
/// 31 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongOutboundAssess : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongSignRateAssess）
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

    // 业务字段（31 列；全角括号列 dbColumn 去括号）
    public string? F所属网点编码 { get; set; }
    public string? F统计日期 { get; set; }
    public string? F省公司 { get; set; }
    public string? F片区 { get; set; }
    public string? F所属网点 { get; set; }
    public string? F转运中心 { get; set; }
    public string? F派次类型 { get; set; }
    public string? F一频次考核应出仓日期 { get; set; }
    public string? F一频次考核应出仓时间 { get; set; }
    public string? F一频次考核应出仓量 { get; set; }
    public string? F一频次考核出仓及时量 { get; set; }
    public string? F一频次考核出仓及时率 { get; set; }
    public string? F一频次考核未及时出仓量 { get; set; }
    public string? F一频次考核考核目标 { get; set; }
    public string? F一频次考核预估考核金额元 { get; set; }
    public string? F二频次监控应出仓日期 { get; set; }
    public string? F二频次监控应出仓时间 { get; set; }
    public string? F二频次监控应出仓量 { get; set; }
    public string? F二频次监控出仓及时量 { get; set; }
    public string? F二频次监控出仓及时率 { get; set; }
    public string? F二频次监控未及时出仓量 { get; set; }
    public string? F二频次监控考核目标 { get; set; }
    public string? F二频次监控预估考核金额元 { get; set; }
    public string? F三频次监控应出仓日期 { get; set; }
    public string? F三频次监控应出仓时间 { get; set; }
    public string? F三频次监控应出仓量 { get; set; }
    public string? F三频次监控出仓及时量 { get; set; }
    public string? F三频次监控出仓及时率 { get; set; }
    public string? F三频次监控未及时出仓量 { get; set; }
    public string? F三频次监控考核目标 { get; set; }
    public string? F三频次监控预估考核金额元 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
