namespace STOTOP.Module.System;

/// <summary>
/// 系统管理模块权限编码常量（仅定义写操作权限）
/// </summary>
public static class SystemPermissions
{
    // 用户管理
    public const string UserCreate = "sys:user:create";
    public const string UserEdit = "sys:user:edit";
    public const string UserDelete = "sys:user:delete";
    public const string UserResetPassword = "sys:user:reset-password";

    // 角色管理
    public const string RoleCreate = "sys:role:create";
    public const string RoleEdit = "sys:role:edit";
    public const string RoleDelete = "sys:role:delete";
    public const string RoleAssignPermission = "sys:role:assign-permission";

    // 权限管理
    public const string PermissionCreate = "sys:permission:create";
    public const string PermissionEdit = "sys:permission:edit";
    public const string PermissionDelete = "sys:permission:delete";

    // 组织管理
    public const string OrgCreate = "sys:org:create";
    public const string OrgEdit = "sys:org:edit";
    public const string OrgDelete = "sys:org:delete";

    // 钉钉集成
    public const string DingTalkManage = "sys:dingtalk:manage";

    // 内测反馈
    public const string FeedbackManage = "sys:feedback:manage";
}
