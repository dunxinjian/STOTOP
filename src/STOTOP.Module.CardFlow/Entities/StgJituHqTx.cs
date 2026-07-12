using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgJituHqTx : BaseEntity, IStagingRecord, ITenantScoped
{
    // IStagingRecord 系统字段
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
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）

    // 业务字段
    public string F流水号 { get; set; } = string.Empty;
    public string? F运单编号 { get; set; }
    public string? F账户ID { get; set; }
    public DateTime? F业务日期 { get; set; }
    public string? F所属网点 { get; set; }
    public string? F网点编号 { get; set; }
    public string F网点名称 { get; set; } = string.Empty;
    public string? F所属代理 { get; set; }
    // 可空：资金类「提现」行的交易类型为空（真实数据 7 行）；插件把空值写 DBNull，SqlBulkCopy 对 NOT NULL 列不套 DEFAULT → 须可空(对齐申通 V58 F费用名称改可空)
    public string? F交易类型 { get; set; }
    public string? F转运中心 { get; set; }
    public string? F结算中心 { get; set; }
    public string? F结算对象 { get; set; }
    public string F费用主类 { get; set; } = string.Empty;
    public string F费用子类 { get; set; } = string.Empty;
    public decimal? F发生金额 { get; set; }
    public decimal? F本次余额 { get; set; }
    public DateTime? F预付时间 { get; set; }
    public string? F备注 { get; set; }

    // 派生收支双列（导入 transformRules 按 F发生金额 符号拆分：加款正=收入、扣款负=支出取绝对值）。
    // 对齐申通/韵达双列模型：凭证行按收/支列取数、方向固定，避免单列负数红字；createDraft 亦硬依赖此两列。
    public decimal? F发生额收入 { get; set; }
    public decimal? F发生额支出 { get; set; }
}
