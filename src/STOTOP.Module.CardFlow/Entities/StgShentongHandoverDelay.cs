using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_交货滞留明细 暂存表实体。
/// 来源：网点交货滞留v3明细导出（sheet「sheet1」）。
/// 34 个业务列全部先以 string? 落地（时间/数值列也是字符串，归一阶段再解析）。
/// 注意：「装车/发件网点」含非法字符 /，dbColumn 去掉斜杠为 F装车发件网点（实体/EF/DDL/映射四处一致）。
/// </summary>
public class StgShentongHandoverDelay : BaseEntity, IStagingRecord
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

    // 业务字段（34 列，对应 sheet「sheet1」表头）
    public string? F业务日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F电商平台 { get; set; }
    public string? F客户名称 { get; set; }
    public string? F当前交货状态 { get; set; }
    public string? F揽收网点 { get; set; }
    public string? F揽收所属网点 { get; set; }
    public string? F装车发件网点 { get; set; }
    public string? F任务号 { get; set; }
    public string? F车牌号 { get; set; }
    public string? F计划下一站中心 { get; set; }
    public string? F实际下一站中心 { get; set; }
    public string? F装车用时 { get; set; }
    public string? F在途用时 { get; set; }
    public string? F交货用时 { get; set; }
    public string? F揽收时间 { get; set; }
    public string? F网点装车时间 { get; set; }
    public string? F交货时间 { get; set; }
    public string? F交货截止时间 { get; set; }
    public string? F中心到件时间 { get; set; }
    public string? F考核标识 { get; set; }
    public string? F考核达标标识 { get; set; }
    public string? F错发下一站标识 { get; set; }
    public string? F地区件标识 { get; set; }
    public string? F交货滞留截止时间 { get; set; }
    public string? F交货滞留标识 { get; set; }
    public string? F线路类型 { get; set; }
    public string? F内网揽收时间 { get; set; }
    public string? F外网揽收时间 { get; set; }
    public string? F揽收超48h标识 { get; set; }
    public string? F揽收小件员名称 { get; set; }
    public string? F首中心操作时间 { get; set; }
    public string? F考核滞留且揽收超48小时标识 { get; set; }
    public string? F揽收选取类型 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
