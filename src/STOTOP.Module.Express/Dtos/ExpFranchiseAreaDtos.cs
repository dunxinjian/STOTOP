using System.ComponentModel.DataAnnotations;
using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 承包区（新版）详情
/// </summary>
public class FranchiseAreaDto
{
    public string Id { get; set; } = string.Empty;
    public string? Code { get; set; }
    public long OrgId { get; set; }
    public string? Contractor { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? CoverageDistrict { get; set; }
    public decimal? ContractFee { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 创建承包区（新版）请求
/// </summary>
public class CreateFranchiseAreaRequest
{
    [Required(ErrorMessage = "编号不能为空")]
    public string Code { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public string? Contractor { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? CoverageDistrict { get; set; }
    public decimal? ContractFee { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新承包区（新版）请求
/// </summary>
public class UpdateFranchiseAreaRequest
{
    public long OrgId { get; set; }
    public string? Contractor { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? CoverageDistrict { get; set; }
    public decimal? ContractFee { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 承包区（新版）查询请求
/// </summary>
public class FranchiseAreaQueryRequest : PagedRequest
{
    public int? Status { get; set; }
    public long? OrgId { get; set; }
}
