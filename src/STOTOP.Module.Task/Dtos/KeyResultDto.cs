namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 关键成果列表DTO
/// </summary>
public class KeyResultListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public long GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MeasureType { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal StartValue { get; set; }
    public string? Unit { get; set; }
    public int Weight { get; set; }
    public int Progress { get; set; }
    public int Status { get; set; }
    public long? ResponsibleId { get; set; }
    public string? ResponsibleName { get; set; }
    public int Sort { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 创建关键成果请求
/// </summary>
public class CreateKeyResultRequest
{
    public string Title { get; set; } = string.Empty;
    public int MeasureType { get; set; }
    public decimal TargetValue { get; set; }
    public decimal StartValue { get; set; } = 0;
    public string? Unit { get; set; }
    public int Weight { get; set; } = 100;
    public long? ResponsibleId { get; set; }
    public int Sort { get; set; } = 0;
}

/// <summary>
/// 更新关键成果请求
/// </summary>
public class UpdateKeyResultRequest
{
    public string Title { get; set; } = string.Empty;
    public int MeasureType { get; set; }
    public decimal TargetValue { get; set; }
    public decimal StartValue { get; set; } = 0;
    public string? Unit { get; set; }
    public int Weight { get; set; } = 100;
    public long? ResponsibleId { get; set; }
    public int Sort { get; set; } = 0;
    public int Status { get; set; }
}

/// <summary>
/// 更新关键成果进度请求（更新当前值，自动重算进度）
/// </summary>
public class UpdateKeyResultProgressRequest
{
    public decimal CurrentValue { get; set; }
    public string? Remark { get; set; }
}
