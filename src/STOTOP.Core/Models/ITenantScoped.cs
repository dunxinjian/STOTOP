namespace STOTOP.Core.Models;

/// <summary>
/// 标记需要按租户（区域公司）隔离的实体——多租户隔离的第 1 层硬墙。
/// 实现此接口的实体进 fail-closed 全局过滤器：无租户上下文且非平台作用域时，读空集 / 写抛异常；
/// 既不认 null 也不认 0（详见 design/23-multitenant-org-redesign.md §6.2）。隔离键 F租户ID = 区域公司根节点。
/// 注意：迁移阶段 0 仅加 F租户ID 列、暂不在实体上实现本接口、暂不启用过滤器；本接口先就位供阶段 1 引用。
/// </summary>
public interface ITenantScoped
{
    long FTenantId { get; set; }
}
