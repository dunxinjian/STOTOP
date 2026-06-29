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
}
