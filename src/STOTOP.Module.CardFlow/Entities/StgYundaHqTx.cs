using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// 韵达总部交易明细暂存表（STG韵达总部交易明细）。列对齐 Taicang/韵达系统交易明细 真实 22 列
/// （sheet「网点交易记录」），外加导入 transformRules 派生的两非负金额列 F发生额收入/F发生额支出
/// （凭证规则 3151 按列取数、方向固定，规避单列带符号金额坑）。
/// 蓝本：StgJituHqTx（极兔 V64）。
/// </summary>
public class StgYundaHqTx : BaseEntity, IStagingRecord, ITenantScoped
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
    public string? F归属网点编号 { get; set; }   // 经营单元核心名（transformRules 从 F公司编码 派生：城区/浏河）
    public long FTenantId { get; set; }           // 租户ID（区域公司，多租户隔离键）

    // 业务字段（对齐韵达真实 22 列）
    public string F所属公司 { get; set; } = string.Empty;   // 江苏太仓通达公司 / 江苏太仓浏河公司
    public string F公司编码 { get; set; } = string.Empty;   // 992209 城区 / 744706 浏河（outlet 维度键）
    public string? F网点业务类型 { get; set; }              // 网点窗口
    public string? F所属业务类型 { get; set; }              // 普通预付款/实业预付款/东普预付款/代收货款预付款
    public string F交易凭证 { get; set; } = string.Empty;   // 跨批次去重键
    public string? F交易类型 { get; set; }                  // 公司扣款/退款/提款/充值凭证
    public decimal F期初金额 { get; set; }
    public decimal F交易金额 { get; set; }                  // 单列带符号：扣款(+)成本/退款(-)收入
    public decimal F期末余额 { get; set; }
    public string? F交易来源 { get; set; }                  // FC/wl/HG/OA
    public DateTime? F交易日期 { get; set; }                // 凭证日期字段
    public string? F业务时间 { get; set; }
    public string? F到账时间 { get; set; }
    public string F收费公司 { get; set; } = string.Empty;   // 管理/收费公司
    public string F收费公司编码 { get; set; } = string.Empty;
    public string? F费用大类 { get; set; }                  // 运营类-派件-有偿类(10) 等
    public string? F收费项目 { get; set; }
    public string? F收费项目编码 { get; set; }
    public string? F三级科目 { get; set; }                  // 三级收费科目名称（摘要用）
    public string? F三级科目编码 { get; set; }              // 三级收费科目编码（凭证匹配键 exactMatchField）
    public string? F数据来源 { get; set; }
    public string? F备注 { get; set; }

    // 派生列（导入 transformRules 写入；凭证规则按列取数）
    public decimal F发生额收入 { get; set; }                // 负额绝对值
    public decimal F发生额支出 { get; set; }                // 正额
}
