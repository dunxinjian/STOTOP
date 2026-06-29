using System.ComponentModel.DataAnnotations;
using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 网点名称映射列表项
/// </summary>
public class NetworkPointAliasDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NetworkPointCode { get; set; } = string.Empty;
    public string? NetworkPointName { get; set; }
    public long OrgId { get; set; }
}

/// <summary>
/// 网点名称映射查询请求
/// </summary>
public class NetworkPointAliasQueryRequest : PagedRequest
{
    /// <summary>按网点编号精确过滤</summary>
    public string? NetworkPointCode { get; set; }
}

/// <summary>
/// 新增网点名称映射请求
/// </summary>
public class CreateNetworkPointAliasRequest
{
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "网点编号不能为空")]
    public string NetworkPointCode { get; set; } = string.Empty;
}

/// <summary>
/// 批量新增网点名称映射请求
/// </summary>
public class BatchCreateNetworkPointAliasRequest
{
    public List<CreateNetworkPointAliasRequest> Items { get; set; } = new();
}

/// <summary>
/// 批量新增结果
/// </summary>
public class BatchCreateNetworkPointAliasResultDto
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
}
