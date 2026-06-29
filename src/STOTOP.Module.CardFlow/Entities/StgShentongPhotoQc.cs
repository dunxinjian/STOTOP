using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_照片质检明细 暂存表实体。
/// 来源：抖音照片质检导出（文件后缀 .xls 但实为 xlsx——A1 魔数 PK，ExcelInputPlugin 已支持；
/// sheet「0」，单行表头，25 列）。
/// 主键列为「单号」（不是「运单号」）。
/// 25 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongPhotoQc : BaseEntity, IStagingRecord
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

    // 业务字段（25 列，对应 sheet「0」表头）
    public string? F单号 { get; set; }
    public string? F业务类型 { get; set; }
    public string? F是否履约 { get; set; }
    public string? F签收人 { get; set; }
    public string? F是否质检合格 { get; set; }
    public string? F不合格类型 { get; set; }
    public string? F是否上门 { get; set; }
    public string? F小件员名称 { get; set; }
    public string? F小件员编码 { get; set; }
    public string? F网点编码 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F大区编码 { get; set; }
    public string? F大区名称 { get; set; }
    public string? F省区编码 { get; set; }
    public string? F省区名称 { get; set; }
    public string? F片区编码 { get; set; }
    public string? F片区名称 { get; set; }
    public string? F收件地址 { get; set; }
    public string? F收件手机号 { get; set; }
    public string? F投诉类型 { get; set; }
    public string? F投诉内容 { get; set; }
    public string? F投诉时间 { get; set; }
    public string? F投诉来源 { get; set; }
    public string? F是否拍照 { get; set; }
    public string? F分区 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
