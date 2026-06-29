using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class TriggerActionService : ITriggerActionService
{
    private readonly STOTOPDbContext _db;

    public TriggerActionService(STOTOPDbContext db)
    {
        _db = db;
    }

    /// <summary>获取当前用户可用的触发动作列表（带权限过滤）</summary>
    public async Task<List<TriggerActionDto>> GetAvailableActionsAsync(long userId, long orgId)
    {
        // 1. 查询所有启用的触发动作
        var allActions = await _db.Set<WfTriggerAction>()
            .Where(a => a.FIsEnabled && (a.FOrgId == 0 || a.FOrgId == orgId))
            .OrderBy(a => a.FOrder)
            .ToListAsync();

        // 2. 检查是否是 admin 用户
        var account = await _db.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => u.FAccount)
            .FirstOrDefaultAsync();

        var isAdmin = string.Equals(account, "admin", StringComparison.OrdinalIgnoreCase);

        // admin 直接返回所有
        if (isAdmin)
        {
            return allActions.Select(MapToDto).ToList();
        }

        // 3. 获取用户的权限码集合
        var roleIds = await _db.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId)
            .Select(ur => ur.FRoleId)
            .ToListAsync();

        var userPermissionCodes = await _db.Set<SysRolePermission>()
            .Where(rp => roleIds.Contains(rp.FRoleId))
            .Join(_db.Set<SysPermission>(),
                rp => rp.FPermissionId,
                p => p.FID,
                (rp, p) => p.FCode)
            .Distinct()
            .ToListAsync();

        // 4. 按权限规则过滤动作
        var result = new List<TriggerActionDto>();
        foreach (var action in allActions)
        {
            if (string.IsNullOrEmpty(action.FRequiredPermission))
            {
                // FRequiredPermission 为空 → 检查用户是否有该模块的任何权限
                var modulePrefix = action.FModule + ":";
                if (userPermissionCodes.Any(code => code.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(MapToDto(action));
                }
            }
            else
            {
                // FRequiredPermission 有值 → 精确匹配权限码
                if (userPermissionCodes.Contains(action.FRequiredPermission, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(MapToDto(action));
                }
            }
        }

        return result;
    }

    private static TriggerActionDto MapToDto(WfTriggerAction action) => new()
    {
        Id = action.FID,
        Key = action.FKey,
        Label = action.FLabel,
        Icon = action.FIcon,
        Module = action.FModule,
        Route = action.FRoute,
        Category = action.FCategory,
        Description = action.FDescription,
        Order = action.FOrder
    };

    /// <summary>获取所有动作（管理用）</summary>
    public async Task<List<TriggerActionDto>> GetAllActionsAsync()
    {
        var actions = await _db.Set<WfTriggerAction>()
            .OrderBy(a => a.FOrder)
            .ToListAsync();

        return actions.Select(MapToDto).ToList();
    }

    /// <summary>切换启用状态</summary>
    public async Task ToggleAsync(long actionId)
    {
        var action = await _db.Set<WfTriggerAction>().FindAsync(actionId);
        if (action == null)
            throw new InvalidOperationException("触发动作不存在");

        action.FIsEnabled = !action.FIsEnabled;
        await _db.SaveChangesAsync();
    }
}
