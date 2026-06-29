using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 业务员详情
/// </summary>
public class SalesmanDto
{
    public string EmployeeNo { get; set; } = string.Empty;
    public string NetworkPointCode { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public DateOnly? HireDate { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 创建业务员请求
/// </summary>
public class CreateSalesmanRequest
{
    /// <summary>HR员工ID（从HR员工列表选择本网点岗位为"业务员"的人员）</summary>
    public long EmployeeId { get; set; }
    /// <summary>网点编号</summary>
    public string NetworkPointCode { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新业务员请求
/// </summary>
public class UpdateSalesmanRequest
{
    public string? Phone { get; set; }
    public string? Remark { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 业务员查询请求
/// </summary>
public class SalesmanQueryRequest : PagedRequest
{
    public string? NetworkPointCode { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// HR员工候选项（从HR员工表筛选可用的业务员岗位人员）
/// </summary>
public class HrEmployeeCandidateDto
{
    public long EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Phone { get; set; }
}
