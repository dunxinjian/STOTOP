using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 管理层配额 - 列表 DTO
/// </summary>
public class ManagerQuotaListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int AwardQuota { get; set; }
    public int DeductQuota { get; set; }
    public int UsedAward { get; set; }
    public int UsedDeduct { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建/更新管理层配额请求
/// </summary>
public class SaveManagerQuotaRequest
{
    public long ManagerId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int AwardQuota { get; set; }
    public int DeductQuota { get; set; }
}

/// <summary>
/// 我的当月配额 DTO
/// </summary>
public class MyQuotaDto
{
    public long Id { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int AwardQuota { get; set; }
    public int DeductQuota { get; set; }
    public int UsedAward { get; set; }
    public int UsedDeduct { get; set; }
    public int RemainingAward { get; set; }
    public int RemainingDeduct { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 管理层配额查询请求（支持按月份筛选）
/// </summary>
public class ManagerQuotaPagedRequest : PagedRequest
{
    public long? ManagerId { get; set; }
    public string? YearMonth { get; set; }
    public int? Status { get; set; }
}
