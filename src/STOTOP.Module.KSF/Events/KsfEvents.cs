using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.KSF.Events;

/// <summary>
/// KSF 月度核算结果事件
///
/// 由 KsfCalcJob 在正式方案核算完成（F运行模式=1，F结果状态=2）后发布。
/// 下游订阅方（积分模块 Points）根据本事件联动奖扣分：
/// - FloatingAmount &gt; 0  → 调用 PointService.AwardAsync 写入 A 分（终身资本化）
/// - Deduction &gt; 0       → 调用 PointService.DeductAsync 写入 B 分（周期清算）
///
/// 幂等键：(OrgId, RelatedEventType="KsfMonthlyResult", RelatedEventId="{EmployeeId}_{Period}")
/// 由 PointService 内部依赖 PmPointRecord.F关联事件类型 + F关联事件ID 字段去重。
/// </summary>
public class KsfMonthlyResultEvent : BusinessEvent
{
    /// <summary>员工 UserId（非 EmployeeId 主键，统一与 Points 模块对齐）</summary>
    public long EmployeeId { get; set; }

    /// <summary>核算期间（如 "2026-04" 或 "202604"），与 PmKsfResult.F期间 一致</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>KSF 方案 ID（PmKsfPlan.FID）</summary>
    public long PlanId { get; set; }

    /// <summary>KSF 结果 ID（PmKsfResult.FID）</summary>
    public long ResultId { get; set; }

    /// <summary>浮动部分金额（正=超额奖励，负=未达目标，单位元或积分按规则换算）</summary>
    public decimal FloatingAmount { get; set; }

    /// <summary>加薪部分金额（A 分资本化候选，单位元）</summary>
    public decimal SalaryIncrease { get; set; }

    /// <summary>红线扣减金额（正值表示扣减额度）</summary>
    public decimal Deduction { get; set; }

    /// <summary>结果状态。1=试算 2=正式（仅 2 触发积分联动）</summary>
    public int ResultStatus { get; set; }
}
