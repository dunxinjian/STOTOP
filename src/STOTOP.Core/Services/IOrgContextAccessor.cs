namespace STOTOP.Core.Services;

/// <summary>
/// 提供当前请求的组织上下文。
/// </summary>
public interface IOrgContextAccessor
{
    /// <summary>
    /// 当前组织ID。为 null 时表示无组织上下文（后台任务、数据库迁移等场景），
    /// 此时全局过滤器将跳过过滤。
    /// 支持在 Hangfire Job 或 BatchContextScope 中显式设置，以切换组织上下文。
    /// </summary>
    long? CurrentOrgId { get; set; }

    /// <summary>
    /// v2 多租户：当前【租户=客户】id（区域公司间在租户内用 R8 数据范围，不是租户切换）。
    /// 为 null 表示无租户上下文：fail-closed 过滤器读空集（不认 null、不认 0）。
    /// 默认接口成员（默认 null/no-op），实现可覆盖；过渡期由中间件在请求层赋值（阶段1b）。
    /// </summary>
    long? CurrentTenantId { get => null; set { } }

    /// <summary>平台/批量受控作用域：为 true 时跳过租户硬墙（仅平台层/seeder/迁移经受控工厂置位）。默认 false。</summary>
    bool IsPlatformScope { get => false; set { } }
}
