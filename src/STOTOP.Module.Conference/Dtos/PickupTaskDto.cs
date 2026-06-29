namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 接送任务详情DTO
/// </summary>
public class PickupTaskDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long? VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? Type { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>乘客列表</summary>
    public List<AttendeeListItemDto> Passengers { get; set; } = new();
}

/// <summary>
/// 接送任务列表项DTO
/// </summary>
public class PickupTaskListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long? VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? DriverName { get; set; }
    public string? Type { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>乘客人数</summary>
    public int PassengerCount { get; set; }
    /// <summary>乘客姓名（逗号分隔）</summary>
    public string PassengerNames { get; set; } = string.Empty;
}

/// <summary>
/// 接送任务详情DTO（含乘客列表，供编辑加载使用）
/// </summary>
public class PickupTaskDetailDto : PickupTaskListItemDto
{
    public string? Remark { get; set; }
    /// <summary>乘客列表</summary>
    public List<PickupPassengerDto> Passengers { get; set; } = new();
}

/// <summary>
/// 接送乘客DTO
/// </summary>
public class PickupPassengerDto
{
    public long AttendeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CompanionCount { get; set; }
}

/// <summary>
/// 创建接送任务请求
/// </summary>
public class CreatePickupTaskRequest
{
    public long? VehicleId { get; set; }
    public string? Type { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新接送任务请求
/// </summary>
public class UpdatePickupTaskRequest
{
    public long? VehicleId { get; set; }
    public string? Type { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 设置接送乘客请求
/// </summary>
public class PickupPassengerRequest
{
    /// <summary>参会人ID列表</summary>
    public List<long> AttendeeIds { get; set; } = new();
}

/// <summary>
/// 自动生成接送任务预览DTO
/// </summary>
public class AutoGeneratePreviewDto
{
    /// <summary>将要创建的接送任务</summary>
    public List<PickupTaskPreviewItem> TasksToCreate { get; set; } = new();
    /// <summary>已跳过的人员（已有安排）</summary>
    public List<string> SkippedAttendees { get; set; } = new();
    /// <summary>无法安排的人员（缺少行程信息）</summary>
    public List<string> UnableToArrange { get; set; } = new();
}

/// <summary>
/// 接送任务预览子项
/// </summary>
public class PickupTaskPreviewItem
{
    public string? Type { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public List<string> PassengerNames { get; set; } = new();
    public long? SuggestedVehicleId { get; set; }
    public string? SuggestedVehiclePlate { get; set; }
}

/// <summary>
/// 提交自动生成请求
/// </summary>
public class CommitAutoGenerateRequest
{
    public List<PickupTaskPreviewItem> Tasks { get; set; } = new();
}

/// <summary>
/// 优化合并预览DTO
/// </summary>
public class OptimizePreviewDto
{
    /// <summary>合并前任务数</summary>
    public int BeforeCount { get; set; }
    /// <summary>合并后任务数</summary>
    public int AfterCount { get; set; }
    /// <summary>合并方案</summary>
    public List<MergeGroup> MergeGroups { get; set; } = new();
}

/// <summary>
/// 合并组
/// </summary>
public class MergeGroup
{
    /// <summary>要合并的原任务ID</summary>
    public List<long> SourceTaskIds { get; set; } = new();
    /// <summary>合并后的任务预览</summary>
    public PickupTaskPreviewItem MergedTask { get; set; } = new();
    /// <summary>合并原因</summary>
    public string Reason { get; set; } = string.Empty;
}
