using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_末端派送网点汇总 暂存表实体。
/// 来源：末端派送考核(新)网点汇总V2（末端派送考核(新)网点汇总V2-*.xlsx，sheet sheet1）。
/// 单行表头，55 列，应签网点×日期粒度（1 行/网点/日期）。
/// 列名均合法（无括号/斜杠等非法字符），dbColumn 直接加 F 前缀（含 FT0/FT1/FT2/FT3 延迟列）。
/// 55 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongDeliveryNetSummary : BaseEntity, IStagingRecord
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

    // 业务字段（55 列）
    public string? F统计日期 { get; set; }
    public string? F中转站 { get; set; }
    public string? F应签所属网点 { get; set; }
    public string? F应签网点 { get; set; }
    public string? F派件员 { get; set; }
    public string? F四级区域 { get; set; }
    public string? F五级区域 { get; set; }
    public string? F频次类型 { get; set; }
    public string? F派次类型 { get; set; }
    public string? F预计考核金额 { get; set; }
    public string? F有偿派费金额 { get; set; }
    public string? F预计返款金额 { get; set; }
    public string? F一阶段考核数量 { get; set; }
    public string? F一阶段及时签收数量 { get; set; }
    public string? F一阶段及时签收率 { get; set; }
    public string? F一阶段目标值 { get; set; }
    public string? F一阶段预计考核金额 { get; set; }
    public string? F一阶段未及时签收数量 { get; set; }
    public string? F二阶段考核数量 { get; set; }
    public string? F二阶段及时签收数量 { get; set; }
    public string? F二阶段及时签收率 { get; set; }
    public string? F二阶段目标值 { get; set; }
    public string? F二阶段预计考核金额 { get; set; }
    public string? F二阶段未及时签收数量 { get; set; }
    public string? FT0延迟签收数量 { get; set; }
    public string? FT1延迟签收数量 { get; set; }
    public string? FT2延迟签收数量 { get; set; }
    public string? FT3延迟签收数量 { get; set; }
    public string? F当天考核数量 { get; set; }
    public string? F当天预估考核金额 { get; set; }
    public string? F当天签收及时量 { get; set; }
    public string? F当天及时签收率 { get; set; }
    public string? F当天目标值 { get; set; }
    public string? F当天签收延迟24h内数量 { get; set; }
    public string? F当天签收延迟24至48h数量 { get; set; }
    public string? F当天签收延迟超48h数量 { get; set; }
    public string? F当天签收延迟24h内率 { get; set; }
    public string? F当天签收延迟24至48h率 { get; set; }
    public string? F当天签收延迟超48h率 { get; set; }
    public string? F14点签收数量 { get; set; }
    public string? F20点签收数量 { get; set; }
    public string? F14点及时签收率 { get; set; }
    public string? F20点及时签收率 { get; set; }
    public string? F签收时长 { get; set; }
    public string? F网点时效用时 { get; set; }
    public string? F先签后派数量 { get; set; }
    public string? F先第三方后派数量 { get; set; }
    public string? F应签收数量 { get; set; }
    public string? F已签收数量 { get; set; }
    public string? F签收进度 { get; set; }
    public string? F未签收数量 { get; set; }
    public string? F未签收有问题件数量 { get; set; }
    public string? F已派件数量 { get; set; }
    public string? F未派件数量 { get; set; }
    public string? F已派未签数量 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
