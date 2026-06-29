using System.ComponentModel.DataAnnotations;
using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 业务员名称映射列表项
/// </summary>
public class SalesmanAliasDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public long OrgId { get; set; }
}

/// <summary>
/// 业务员名称映射查询请求
/// </summary>
public class SalesmanAliasQueryRequest : PagedRequest
{
    /// <summary>按员工工号精确过滤</summary>
    public string? EmployeeNo { get; set; }
}

/// <summary>
/// 新增业务员名称映射请求
/// </summary>
public class CreateSalesmanAliasRequest
{
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "员工工号不能为空")]
    public string EmployeeNo { get; set; } = string.Empty;
}

/// <summary>
/// 批量新增业务员名称映射请求
/// </summary>
public class BatchCreateSalesmanAliasRequest
{
    public List<CreateSalesmanAliasRequest> Items { get; set; } = new();
}

/// <summary>
/// 批量新增结果
/// </summary>
public class BatchCreateSalesmanAliasResultDto
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
}
