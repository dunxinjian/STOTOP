using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 根据组织ID推导当前有效账套ID
/// 使用原生SQL查询 FIN账套 表（避免 System 模块对 Finance 模块的循环引用）
/// </summary>
public class OrgAccountSetResolver : IOrgAccountSetResolver
{
    private readonly STOTOPDbContext _context;

    public OrgAccountSetResolver(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<long?> GetActiveAccountSetIdAsync(long orgId)
    {
        // 1. 查默认账套：F组织ID = orgId AND F是否默认 = 1
        var defaultId = await _context.Database
            .SqlQueryRaw<long>(
                "SELECT TOP 1 FID AS [Value] FROM [FIN账套] WHERE [F组织ID] = {0} AND [F是否默认] = 1",
                orgId)
            .FirstOrDefaultAsync();

        if (defaultId > 0) return defaultId;

        // 2. 取该组织下最新的启用账套（F状态 = 1）
        var latestId = await _context.Database
            .SqlQueryRaw<long>(
                "SELECT TOP 1 FID AS [Value] FROM [FIN账套] WHERE [F组织ID] = {0} AND [F状态] = 1 ORDER BY FID DESC",
                orgId)
            .FirstOrDefaultAsync();

        return latestId > 0 ? latestId : null;
    }
}
