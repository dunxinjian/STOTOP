using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_应拦截明细 暂存表实体。
/// 来源：应拦截量数据报表导出（文件后缀 .xls 但实为 xlsx——A1 魔数 PK，ExcelInputPlugin 已支持；
/// sheet「0」，单行表头，26 列）。
/// 26 个业务列全部先以 string? 落地（时间/金额/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongInterceptDetail : BaseEntity, IStagingRecord
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

    // 业务字段（26 列，对应 sheet「0」表头）
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F拦截来源 { get; set; }
    public string? F应拦截网点 { get; set; }
    public string? F所属网点 { get; set; }
    public string? F拦截类型 { get; set; }
    public string? F派件小件员 { get; set; }
    public string? F到件时间 { get; set; }
    public string? F最新OP时间 { get; set; }
    public string? F最新OP节点 { get; set; }
    public string? F驿站名称 { get; set; }
    public string? F退件打印时间 { get; set; }
    public string? F退件操作人 { get; set; }
    public string? F最迟转出时间 { get; set; }
    public string? F逆向转出时间 { get; set; }
    public string? F逆向交货组织 { get; set; }
    public string? F逆向转出时长 { get; set; }
    public string? F逆向转出时效 { get; set; }
    public string? F预计考核金额 { get; set; }
    public string? F拦截录入网点 { get; set; }
    public string? F拦截录入时间 { get; set; }
    public string? F拦截发起节点 { get; set; }
    public string? F是否拦截成功 { get; set; }
    public string? F是否转出 { get; set; }
    public string? F是否及时转出 { get; set; }
    public string? F正向签收 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
