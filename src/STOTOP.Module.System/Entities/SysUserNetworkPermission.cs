using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 用户网点权限
/// </summary>
public class SysUserNetworkPermission : BaseEntity
{
    /// <summary>用户ID</summary>
    public long FUserId { get; set; }
    /// <summary>网点编号</summary>
    public string FNetworkPointCode { get; set; } = string.Empty;
    /// <summary>权限类型 1查看 2编辑 3管理</summary>
    public int FPermissionType { get; set; } = 1;
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
