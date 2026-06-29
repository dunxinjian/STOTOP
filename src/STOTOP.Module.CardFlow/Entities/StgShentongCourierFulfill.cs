using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_小件员履约指标 暂存表实体。
/// 来源：小件员履约指标历史数据导出（小件员履约指标历史数据导出_导出任务_*.xlsx，sheet 小件员履约指标历史数据导出）。
/// 双行表头「坑」源：第 1 行是分组名（当日派签情况/按需上门情况/工单情况/违规行为/真实情况），
/// 第 2 行是真字段名 → 规则用 headerRow=2, dataStartRow=3。
/// 注意：所属网点/所属小件员 为纵向合并单元格（A1:A2 / B1:B2），但底层 XML 在 row2 同样写入了真值，
/// 故 headerRow=2 时这两列可正常按名映射；19 列名 row2 全唯一，全部映射。
/// 员工级粒度（1 行/网点/小件员），此文件无日期列，按「所属网点 + 所属小件员」去重。
/// 19 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// 路由：upload-auto 读第 1 行=分组名，内容匹配无效 → 靠 fileNamePattern 小件员履约指标* 路由。
/// </summary>
public class StgShentongCourierFulfill : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongSignRateAssess）
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

    // 业务字段（19 列，均为 row2 全局唯一名）
    public string? F所属网点 { get; set; }
    public string? F所属小件员 { get; set; }
    public string? F当日派签量 { get; set; }
    public string? F未当日派签量 { get; set; }
    public string? F当日派签率 { get; set; }
    public string? F应上门量 { get; set; }
    public string? F未上门量 { get; set; }
    public string? F按需上门率 { get; set; }
    public string? F客诉发起量 { get; set; }
    public string? F工单定责量 { get; set; }
    public string? F客诉发起率 { get; set; }
    public string? F虚假电联 { get; set; }
    public string? F无效电联 { get; set; }
    public string? F双签 { get; set; }
    public string? F照片定位虚假 { get; set; }
    public string? F签收文本不规范 { get; set; }
    public string? F引导代收 { get; set; }
    public string? F回访虚假量 { get; set; }
    public string? F回访真实率 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
