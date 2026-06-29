namespace STOTOP.Core.Models;

/// <summary>
/// 标记需要按所属组织进行数据隔离的实体（用于组织扩展实体，其 FOrgId 有其他用途）。
/// 全局查询过滤器将基于 FOwnerOrgId 字段进行过滤。
/// FOwnerOrgId == 0 表示全局共享数据，对所有组织可见。
/// </summary>
public interface IOrgOwned
{
    long FOwnerOrgId { get; set; }
}
