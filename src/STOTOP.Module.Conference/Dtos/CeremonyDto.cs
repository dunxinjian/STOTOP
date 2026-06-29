namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 典礼流程项DTO
/// </summary>
public class CeremonyItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string? Responsible { get; set; }
    public string? Music { get; set; }
    public string? Lighting { get; set; }
    public string? Props { get; set; }
    public string? Remark { get; set; }
    public int Sort { get; set; }
    public string Phase { get; set; } = "仪式";
}

/// <summary>
/// 创建典礼流程项请求
/// </summary>
public class CreateCeremonyItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int Duration { get; set; } = 5;
    public string? Responsible { get; set; }
    public string? Music { get; set; }
    public string? Lighting { get; set; }
    public string? Props { get; set; }
    public string? Remark { get; set; }
    public int Sort { get; set; }
    public string Phase { get; set; } = "仪式";
}

/// <summary>
/// 更新典礼流程项请求
/// </summary>
public class UpdateCeremonyItemRequest
{
    public string? Name { get; set; }
    public string? StartTime { get; set; }
    public int? Duration { get; set; }
    public string? Responsible { get; set; }
    public string? Music { get; set; }
    public string? Lighting { get; set; }
    public string? Props { get; set; }
    public string? Remark { get; set; }
    public int? Sort { get; set; }
    public string? Phase { get; set; }
}

/// <summary>
/// 典礼流程重排序请求
/// </summary>
public class ReorderCeremonyRequest
{
    public List<long> ItemIds { get; set; } = new();
}
