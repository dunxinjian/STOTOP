using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_渗透建站考核 暂存表实体。
/// 来源：渗透率建站考核导出（sheet「渗透率建站考核」，单行表头，24 列；1 行/网点/周期的汇总表）。
/// 24 个业务列全部先以 string? 落地（比率/数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongPenetration : BaseEntity, IStagingRecord
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

    // 业务字段（24 列，对应 sheet「渗透率建站考核」表头）
    public string? F统计周期 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F网点编号 { get; set; }
    public string? F自建渗透率当月目标 { get; set; }
    public string? F已认证自建渗透率 { get; set; }
    public string? F已认证自建渗透率差值 { get; set; }
    public string? F已认证自建渗透率环比 { get; set; }
    public string? F总入库量 { get; set; }
    public string? F已认证自建入库量 { get; set; }
    public string? F建站当季目标 { get; set; }
    public string? F建站当月目标 { get; set; }
    public string? F菜鸟活跃 { get; set; }
    public string? F喵站活跃 { get; set; }
    public string? F多多活跃 { get; set; }
    public string? F喵柜抵扣建站数 { get; set; }
    public string? F建站待完成 { get; set; }
    public string? F菜鸟当月新增 { get; set; }
    public string? F建柜目标 { get; set; }
    public string? F喵柜激活格口数 { get; set; }
    public string? F喵柜激活格口数环比 { get; set; }
    public string? F喵柜待完成格口数 { get; set; }
    public string? F全cp日均入库量 { get; set; }
    public string? F申通日均入库量 { get; set; }
    public string? F喵柜组数 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
