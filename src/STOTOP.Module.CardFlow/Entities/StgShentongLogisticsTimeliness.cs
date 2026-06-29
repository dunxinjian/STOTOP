using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_物流及时准确明细 暂存表实体。
/// 来源：到件晚于签收/派件晚于签收/揽收上传不及时 三个文件同结构（sheet「及时性和准确性明细」），靠「问题类型」列区分。
/// 17 个业务列全部先以 string? 落地（时间列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongLogisticsTimeliness : BaseEntity, IStagingRecord
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

    // 业务字段（17 列，对应 sheet「及时性和准确性明细」表头）
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F问题类型 { get; set; }
    public string? F扫描时间 { get; set; }
    public string? F上传时间 { get; set; }
    public string? F入库时间 { get; set; }
    public string? F扫描类型 { get; set; }
    public string? F扫描员 { get; set; }
    public string? F扫描员编号 { get; set; }
    public string? F设备类型 { get; set; }
    public string? F设备ID { get; set; }
    public string? F是否黑土共配 { get; set; }
    public string? F订单平台 { get; set; }
    public string? F派件员编号 { get; set; }
    public string? F派件员名称 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
