namespace STOTOP.Module.System.Services;

/// <summary>
/// 根据组织ID推导当前有效账套ID
/// </summary>
public interface IOrgAccountSetResolver
{
    /// <summary>
    /// 根据组织ID推导当前有效账套ID
    /// 规则：查 FIN账套 表 F组织ID = orgId AND F是否默认 = 1；若无则取该组织下最新的启用账套；均无则返回 null
    /// </summary>
    Task<long?> GetActiveAccountSetIdAsync(long orgId);
}
