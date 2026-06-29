using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_物流信息准确汇总 暂存表实体。
/// 来源：物流信息指数（excel（物流信息指数）.xls，真 OLE2，含 3 个 sheet，本表对应 sheet「准确性汇总（按天）」，单行表头，14 列）。
/// 1 行/网点/统计日期 的汇总表，本文件无网点编号，仅有网点名称。
/// 14 个业务列全部先以 string? 落地（数量/比率列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongInfoIndexAccurate : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongPenetration）
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

    // 业务字段（14 列，对应 sheet「准确性汇总（按天）」表头）
    public string? F统计日期 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F揽收总量 { get; set; }
    public string? F派件总量 { get; set; }
    public string? F签收总量 { get; set; }
    public string? F扫描单量 { get; set; }
    public string? F问题单量 { get; set; }
    public string? F揽收晚于派件量 { get; set; }
    public string? F揽收晚于签收量 { get; set; }
    public string? F派件晚于签收量 { get; set; }
    public string? F不准确率 { get; set; }
    public string? F到件晚于签收量 { get; set; }
    public string? F到件不准确率 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
