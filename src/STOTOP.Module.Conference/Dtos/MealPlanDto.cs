namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 餐食计划详情DTO
/// </summary>
public class MealPlanDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public DateTime Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? DiningMode { get; set; }
    public string? Location { get; set; }
    public int ExpectedCount { get; set; }
    public int ActualCount { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>用餐人员列表</summary>
    public List<MealAttendeeDto> Attendees { get; set; } = new();
}

/// <summary>
/// 餐食计划列表项DTO
/// </summary>
public class MealPlanListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public DateTime Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? DiningMode { get; set; }
    public string? Location { get; set; }
    public int ExpectedCount { get; set; }
    public int ActualCount { get; set; }
    /// <summary>桌次数</summary>
    public int TableCount { get; set; }
    /// <summary>用餐人员列表</summary>
    public List<MealAttendeeDto> Attendees { get; set; } = new();
}

/// <summary>
/// 餐食人员DTO
/// </summary>
public class MealAttendeeDto
{
    public long AttendeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? DietPreference { get; set; }
    public string? DietNote { get; set; }
}

/// <summary>
/// 创建餐食计划请求
/// </summary>
public class CreateMealPlanRequest
{
    public DateTime Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? DiningMode { get; set; }
    public string? Location { get; set; }
    public int ExpectedCount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新餐食计划请求
/// </summary>
public class UpdateMealPlanRequest
{
    public DateTime Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? DiningMode { get; set; }
    public string? Location { get; set; }
    public int ExpectedCount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 设置用餐人员请求
/// </summary>
public class MealAttendeeRequest
{
    /// <summary>用餐人员列表</summary>
    public List<MealAttendeeInput> Attendees { get; set; } = new();
}

/// <summary>
/// 用餐人员输入项
/// </summary>
public class MealAttendeeInput
{
    public long AttendeeId { get; set; }
    /// <summary>饮食备注</summary>
    public string? DietNote { get; set; }
}
