using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_积压明细 暂存表实体。
/// 来源：末端时效积压明细（sheet「末端时效积压明细」）。
/// 41 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// 注意：「积压8-15天标识」「积压16-30天标识」「积压31-60天标识」含非法字符 -，dbColumn 去掉为 F积压815天标识 / F积压1630天标识 / F积压3160天标识。
/// </summary>
public class StgShentongBacklog : BaseEntity, IStagingRecord
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

    // 业务字段（41 列，对应 sheet「末端时效积压明细」表头）
    public string? F业务日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F大区名称 { get; set; }
    public string? F省区名称 { get; set; }
    public string? F省份名称 { get; set; }
    public string? F所属网点编码 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F应签网点编码 { get; set; }
    public string? F应签网点 { get; set; }
    public string? F四级区域编码 { get; set; }
    public string? F四级区域 { get; set; }
    public string? F三段码 { get; set; }
    public string? F最后扫描组织编码 { get; set; }
    public string? F最后扫描组织名称 { get; set; }
    public string? F最后扫描组织父级编码 { get; set; }
    public string? F最后扫描组织父级名称 { get; set; }
    public string? F最后扫描时间 { get; set; }
    public string? F最后扫描类型 { get; set; }
    public string? F最后扫描类型编码 { get; set; }
    public string? F扫描员 { get; set; }
    public string? F扫描员编码 { get; set; }
    public string? F业务员 { get; set; }
    public string? F业务员编码 { get; set; }
    public string? F问题件一级类型 { get; set; }
    public string? F问题件二级类型 { get; set; }
    public string? F退回件标识 { get; set; }
    public string? F积压1天标识 { get; set; }
    public string? F积压2天标识 { get; set; }
    public string? F积压3天标识 { get; set; }
    public string? F积压4天标识 { get; set; }
    public string? F积压5天标识 { get; set; }
    public string? F积压六6天标识 { get; set; }
    public string? F积压7天标识 { get; set; }
    public string? F积压815天标识 { get; set; }
    public string? F积压1630天标识 { get; set; }
    public string? F积压3160天标识 { get; set; }
    public string? F超过3天标识 { get; set; }
    public string? F超过5天标识 { get; set; }
    public string? F超过7天标识 { get; set; }
    public string? F是否积压剔除标识 { get; set; }
    public string? F是否实时签收 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
