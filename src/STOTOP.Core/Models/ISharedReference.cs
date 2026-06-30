namespace STOTOP.Core.Models;

/// <summary>
/// 标记跨租户共享的参考数据（如品牌、行政区划等）。
/// 此类实体走独立过滤器——根本不挂租户隔离条件，绝不是用 F租户ID==0 搭便车
/// （详见 design/23-multitenant-org-redesign.md §6.2 第 2 条）。仅作类型标记，无成员。
/// </summary>
public interface ISharedReference
{
}
