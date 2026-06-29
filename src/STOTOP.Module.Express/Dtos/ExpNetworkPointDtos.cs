using System.ComponentModel.DataAnnotations;
using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 网点详情
/// </summary>
public class NetworkPointDto
{
    public string Id { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ShortName { get; set; }
    public string? FullName { get; set; }
    public string? ParentPointCode { get; set; }
    public long OrgId { get; set; }
    public int PointLevel { get; set; }
    public int IsPrimaryPoint { get; set; } = 1;
    public string? CoverageArea { get; set; }
    public int? DailyCapacity { get; set; }
    public decimal? StorageArea { get; set; }
    public string? BusinessHours { get; set; }
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 创建网点请求
/// </summary>
public class CreateNetworkPointRequest
{
    [Required(ErrorMessage = "编号不能为空")]
    public string Code { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ParentPointCode { get; set; }
    public long OrgId { get; set; }
    public int PointLevel { get; set; } = 1;
    public int IsPrimaryPoint { get; set; } = 1;
    public string? CoverageArea { get; set; }
    public int? DailyCapacity { get; set; }
    public decimal? StorageArea { get; set; }
    public string? BusinessHours { get; set; }
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新网点请求
/// </summary>
public class UpdateNetworkPointRequest
{
    public string? ShortName { get; set; }
    public string? ParentPointCode { get; set; }
    public long OrgId { get; set; }
    public int PointLevel { get; set; } = 1;
    public int IsPrimaryPoint { get; set; } = 1;
    public string? CoverageArea { get; set; }
    public int? DailyCapacity { get; set; }
    public decimal? StorageArea { get; set; }
    public string? BusinessHours { get; set; }
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 网点查询请求
/// </summary>
public class NetworkPointQueryRequest : PagedRequest
{
    public int? Status { get; set; }
}
