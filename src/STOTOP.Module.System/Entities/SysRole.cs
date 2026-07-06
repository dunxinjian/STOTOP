using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysRole : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FStatus { get; set; } = 1;

    /// <summary>角色作用域（多租户阶段4·R5）：platform=平台级全局共享 / tenant=租户私有（开通时克隆）。
    /// 存量角色回填 platform。</summary>
    public string FScope { get; set; } = SysRoleScope.Platform;

    /// <summary>所属租户ID（→ 租户根组织 FID）。platform 角色=0；tenant 角色=该租户。</summary>
    public long FTenantId { get; set; }

    /// <summary>是否管理员型角色（持有即在其作用域内拿全量权限；tenant 型再按套餐 FModuleFlags 裁剪菜单）。
    /// 全局 admin 角色(FID=1) 与 各租户私有 admin 角色 均为 true。</summary>
    public bool FIsAdmin { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
    public virtual ICollection<SysRolePermission> RolePermissions { get; set; } = new List<SysRolePermission>();
}

/// <summary>角色作用域取值（<see cref="SysRole.FScope"/>）。</summary>
public static class SysRoleScope
{
    public const string Platform = "platform";
    public const string Tenant = "tenant";
}
