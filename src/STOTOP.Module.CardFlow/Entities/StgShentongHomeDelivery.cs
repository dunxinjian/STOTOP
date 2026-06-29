using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_送货上门明细 暂存表实体。
/// 来源：送货上门达成分析历史详情导出（sheet「送货上门达成分析历史详情导出」，单行表头，17 列）。
/// 注意：「违规行为-二级内容」含非法字符 -，dbColumn 去掉为 F违规行为二级内容（excelColumn 保留原文）。
/// 「电联录音」为 URL（可能逗号分隔多条录音地址，长文本）。
/// 17 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongHomeDelivery : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongFakeSign）
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

    // 业务字段（17 列，对应 sheet「送货上门达成分析历史详情导出」表头）
    public string? F订单来源 { get; set; }
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F运单状态 { get; set; }
    public string? F承包区编号 { get; set; }
    public string? F承包区名称 { get; set; }
    public string? F业务员工号 { get; set; }
    public string? F派送小件员名称 { get; set; }
    public string? F回执情况 { get; set; }
    public string? F签收人信息 { get; set; }
    public string? F履约情况 { get; set; }
    public string? F签收日期 { get; set; }
    public string? F违规行为二级内容 { get; set; } // Excel 列名「违规行为-二级内容」含非法字符 -，dbColumn 去掉
    public string? F工单判罚类型 { get; set; }
    public string? F是否电联 { get; set; }
    public string? F是否接通 { get; set; }
    public string? F电联录音 { get; set; } // URL 长文本

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
