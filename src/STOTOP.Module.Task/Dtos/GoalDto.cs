using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 目标列表DTO
/// </summary>
public class GoalListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public long GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public string? ResponsibleName { get; set; }
    public long ParentId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Progress { get; set; }
    public int Weight { get; set; }
    public int Status { get; set; }
    public int KeyResultCount { get; set; }
    public int ChildrenCount { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 目标详情DTO（含KR列表+子目标列表）
/// </summary>
public class GoalDetailDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OrgId { get; set; }
    public long GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public string? ResponsibleName { get; set; }
    public long ParentId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Progress { get; set; }
    public int Weight { get; set; }
    public int Status { get; set; }
    public long CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<KeyResultListDto> KeyResults { get; set; } = new();
    public List<GoalListDto> Children { get; set; } = new();
}

/// <summary>
/// 目标树节点DTO
/// </summary>
public class GoalTreeDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long? ResponsibleId { get; set; }
    public string? ResponsibleName { get; set; }
    public long ParentId { get; set; }
    public string Level { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int Status { get; set; }
    public int KeyResultCount { get; set; }
    public List<GoalTreeDto> Children { get; set; } = new();
}

/// <summary>
/// 创建目标请求
/// </summary>
public class CreateGoalRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public long ParentId { get; set; } = 0;
    public string Level { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Weight { get; set; } = 100;
}

/// <summary>
/// 更新目标请求
/// </summary>
public class UpdateGoalRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Weight { get; set; } = 100;
    public int Status { get; set; }
}

/// <summary>
/// 目标分解请求（创建子目标）
/// </summary>
public class DecomposeGoalRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Weight { get; set; } = 100;
}

/// <summary>
/// 目标树形查询请求
/// </summary>
public class GoalTreeQueryRequest
{
    public string? Level { get; set; }
    public long? GoalOrgId { get; set; }
    public long? ResponsibleId { get; set; }
    public int? Status { get; set; }
    public string? Keyword { get; set; }
}
