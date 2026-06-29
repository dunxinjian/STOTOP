using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_疑似遗失明细 暂存表实体。
/// 来源：网点疑似遗失明细导出v5（sheet「sheet1」）。
/// 51 个业务列全部先以 string? 落地（时间/数值/标识列也是字符串，归一阶段再解析）。
/// 注意：「结算重量(kg)」「找回时长(h)」含非法字符 ()，dbColumn 去掉为 F结算重量kg / F找回时长h。
/// </summary>
public class StgShentongSuspectedLoss : BaseEntity, IStagingRecord
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

    // 业务字段（51 列，对应 sheet「sheet1」表头）
    public string? F运单号 { get; set; }
    public string? F先行理赔状态 { get; set; }
    public string? F是否找回 { get; set; }
    public string? F签收标识 { get; set; }
    public string? F实际金额 { get; set; }
    public string? F是否疫情件 { get; set; }
    public string? F内件品名 { get; set; }
    public string? F结算重量kg { get; set; }
    public string? F订单来源 { get; set; }
    public string? F3日轨迹中断触发类型 { get; set; }
    public string? F包号 { get; set; }
    public string? F集包站点 { get; set; }
    public string? F扫描站点 { get; set; }
    public string? F扫描站点所属省份 { get; set; }
    public string? F扫描站点所属南北区 { get; set; }
    public string? F最后扫描时间 { get; set; }
    public string? F扫描操作人 { get; set; }
    public string? F业务员 { get; set; }
    public string? F下一节点操作截止时间 { get; set; }
    public string? F3日轨迹中断触发时间 { get; set; }
    public string? F找货责任方1 { get; set; }
    public string? F找件责任1所属网点名称 { get; set; }
    public string? F找货责任方2 { get; set; }
    public string? F找件责任2所属网点名称 { get; set; }
    public string? F运输任务号 { get; set; }
    public string? F承运商 { get; set; }
    public string? F车牌号 { get; set; }
    public string? F揽收省份 { get; set; }
    public string? F揽收网点 { get; set; }
    public string? F问题件类型 { get; set; }
    public string? F退回件标识 { get; set; }
    public string? F拦截件标识 { get; set; }
    public string? F停滞用时 { get; set; }
    public string? F是否理赔 { get; set; }
    public string? F目的地省份 { get; set; }
    public string? F目的地网点 { get; set; }
    public string? F找回时的扫描类型 { get; set; }
    public string? F找回时的扫描站点 { get; set; }
    public string? F找回时间 { get; set; }
    public string? F找回时长h { get; set; }
    public string? F下一站 { get; set; }
    public string? F下一站省份 { get; set; }
    public string? F下一站所属南北区 { get; set; }
    public string? F责任方1所属省区 { get; set; }
    public string? F责任方2所属省区 { get; set; }
    public string? F订单网点 { get; set; }
    public string? F订单省份 { get; set; }
    public string? F考核剔除项 { get; set; }
    public string? F商家编码 { get; set; }
    public string? F商家名称 { get; set; }
    public string? F任务不发起原因 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
