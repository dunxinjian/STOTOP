using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 用户网点权限详情
/// </summary>
public class UserNetworkPermissionDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string NetworkPointCode { get; set; } = string.Empty;
    public int PermissionType { get; set; } = 1;
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建用户网点权限请求
/// </summary>
public class CreateUserNetworkPermissionRequest
{
    public long UserId { get; set; }
    public string NetworkPointCode { get; set; } = string.Empty;
    public int PermissionType { get; set; } = 1;
}

/// <summary>
/// 用户网点权限查询请求
/// </summary>
public class UserNetworkPermissionQueryRequest : PagedRequest
{
    public long? UserId { get; set; }
    public string? NetworkPointCode { get; set; }
}
