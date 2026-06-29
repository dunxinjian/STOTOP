using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

/// <summary>
/// PM积分清算记录 - 月清/年清留痕
/// 唯一约束：(F组织ID, F员工ID, F账户类型, F清算期间, F清算类型) 保证幂等
/// </summary>
public class PmPointResetRecord : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>员工（用户）ID</summary>
    public long F员工ID { get; set; }
    /// <summary>账户类型：1=A 终身资本 / 2=B 周期清算（清算通常仅作用 B 分）</summary>
    public int F账户类型 { get; set; } = 2;
    /// <summary>清算周期键：月清=yyyy-MM / 年清=yyyy</summary>
    public string F清算期间 { get; set; } = string.Empty;
    /// <summary>清算类型：1=月清 / 2=年清</summary>
    public int F清算类型 { get; set; }
    /// <summary>清算策略：0=归零 / 1=转福利券 / 2=自定义</summary>
    public int F清算策略 { get; set; }
    /// <summary>清算前的可用余额</summary>
    public int F清算前余额 { get; set; }
    /// <summary>清算后的可用余额</summary>
    public int F清算后余额 { get; set; }
    /// <summary>转换比例（默认 1.0）</summary>
    public decimal F转换比例 { get; set; } = 1.0m;
    /// <summary>本次清算转换出的福利券面值</summary>
    public decimal F兑换福利券值 { get; set; }
    /// <summary>关联兑换记录ID（清算策略=1 转福利券时填充）</summary>
    public Guid? F关联兑换记录ID { get; set; }
    /// <summary>快照日期（用于半开区间余额定位）</summary>
    public DateTime F快照日期 { get; set; }
    /// <summary>清算 Job 实际执行时间</summary>
    public DateTime F执行时间 { get; set; } = DateTime.Now;
    /// <summary>备注（异常信息、自定义策略说明等）</summary>
    public string? F备注 { get; set; }
}
