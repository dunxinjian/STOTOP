using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 平台套餐（PLT套餐，多租户阶段4·平台层）。定义租户可订阅的资源上限与模块开关。
/// 平台层·不实现 <see cref="ITenantScoped"/>（跨租户共享的产品目录，无 F组织ID）。
/// </summary>
public class PltPlan : BaseEntity
{
    /// <summary>套餐名称</summary>
    public string FName { get; set; } = string.Empty;

    /// <summary>套餐编号（唯一）</summary>
    public string FCode { get; set; } = string.Empty;

    /// <summary>最大用户数（0=不限）</summary>
    public int FMaxUsers { get; set; }

    /// <summary>最大网点数（0=不限）</summary>
    public int FMaxOutlets { get; set; }

    /// <summary>模块开关（JSON，与 SYS菜单 取交集控制可见模块，F模块开关）</summary>
    public string? FModuleFlags { get; set; }

    public int FStatus { get; set; } = 1;

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
