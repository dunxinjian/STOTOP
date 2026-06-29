using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_末端派送考核明细 暂存表实体。
/// 来源：末端派送考核(新)明细V2（sheet「sheet1」）。
/// 63 个业务列全部先以 string? 落地（时间/数值/标识列也是字符串，归一阶段再解析）。
/// 注意：「当天签收延迟0-24h标识」「当天签收延迟24-48h标识」含非法字符 -，dbColumn 去掉为 F当天签收延迟024h标识 / F当天签收延迟2448h标识。
/// </summary>
public class StgShentongDeliveryAssess : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongLogisticsCompleteness）
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

    // 业务字段（63 列，对应 sheet「sheet1」表头）
    public string? F运单号 { get; set; }
    public string? F统计日期 { get; set; }
    public string? F中转站名称 { get; set; }
    public string? F应签收所属网点名称 { get; set; }
    public string? F应签收网点名称 { get; set; }
    public string? F发件频次名称 { get; set; }
    public string? F中转站发件时间 { get; set; }
    public string? F派件时间 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F一阶段签收时限 { get; set; }
    public string? F一阶段内签收标识 { get; set; }
    public string? F二阶段签收时限 { get; set; }
    public string? F二阶段内签收标识 { get; set; }
    public string? F当天签收时限 { get; set; }
    public string? F当天签收标识 { get; set; }
    public string? F频次开始时间 { get; set; }
    public string? F频次截止时间 { get; set; }
    public string? F带货网点名称 { get; set; }
    public string? F派件员姓名 { get; set; }
    public string? F四级区域名称 { get; set; }
    public string? F五级区域名称 { get; set; }
    public string? F派件网点名称 { get; set; }
    public string? F签收网点名称 { get; set; }
    public string? F派次类型名称 { get; set; }
    public string? F签收类型名称 { get; set; }
    public string? F签收时长 { get; set; }
    public string? F网点时效用时 { get; set; }
    public string? F时效配置 { get; set; }
    public string? F发件日期 { get; set; }
    public string? FT0延迟签收标识 { get; set; }
    public string? FT1延迟签收标识 { get; set; }
    public string? FT2延迟签收标识 { get; set; }
    public string? FT3延迟签收标识 { get; set; }
    public string? F当天签收延迟024h标识 { get; set; }
    public string? F当天签收延迟2448h标识 { get; set; }
    public string? F当天签收延迟超48h标识 { get; set; }
    public string? F14点签收时限 { get; set; }
    public string? F14点签收标识 { get; set; }
    public string? F20点签收时限 { get; set; }
    public string? F20点签收标识 { get; set; }
    public string? F已签收标识 { get; set; }
    public string? F未签收有问题件标识 { get; set; }
    public string? F已派未签标识 { get; set; }
    public string? F进村件标识 { get; set; }
    public string? F有进村件配置标识 { get; set; }
    public string? F进村件顺延天数 { get; set; }
    public string? F问题件原因 { get; set; }
    public string? F问题件类型名称 { get; set; }
    public string? F问题件登记时间 { get; set; }
    public string? F退回件原因 { get; set; }
    public string? F退回件扫描时间 { get; set; }
    public string? F是否曾经退回标识 { get; set; }
    public string? F是否曾经问题件标识 { get; set; }
    public string? F时效配置类型名称 { get; set; }
    public string? F未签收退回件标识 { get; set; }
    public string? F包号 { get; set; }
    public string? F预售标识 { get; set; }
    public string? F电商平台 { get; set; }
    public string? F配送类型名称 { get; set; }
    public string? F一阶段考核标识 { get; set; }
    public string? F二阶段考核标识 { get; set; }
    public string? F区域时效件 { get; set; }
    public string? F三段码 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
