using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 末端驿站详情
/// </summary>
public class LastMileStationDto
{
    public string Id { get; set; } = string.Empty;
    public int StationType { get; set; }
    public long? OrgId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? BusinessHours { get; set; }
    public int? DailyVolume { get; set; }
    public int? ShelfCount { get; set; }
    public decimal? Area { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>业务对象ID（来自EXP业务对象表）</summary>
    public long? ServiceObjectId { get; set; }
}

/// <summary>
/// 创建末端驿站请求
/// </summary>
public class CreateLastMileStationRequest
{
    /// <summary>类型 1=直营 2=合作</summary>
    public int StationType { get; set; } = 1;
    /// <summary>组织ID（直营时必填）</summary>
    public long? OrgId { get; set; }
    /// <summary>编码（合作时必填）</summary>
    public string? Code { get; set; }
    /// <summary>名称（合作时必填）</summary>
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? BusinessHours { get; set; }
    public int? DailyVolume { get; set; }
    public int? ShelfCount { get; set; }
    public decimal? Area { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新末端驿站请求
/// </summary>
public class UpdateLastMileStationRequest
{
    /// <summary>类型 1=直营 2=合作</summary>
    public int StationType { get; set; }
    /// <summary>组织ID（直营时必填）</summary>
    public long? OrgId { get; set; }
    /// <summary>编码（合作时必填）</summary>
    public string? Code { get; set; }
    /// <summary>名称（合作时必填）</summary>
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? BusinessHours { get; set; }
    public int? DailyVolume { get; set; }
    public int? ShelfCount { get; set; }
    public decimal? Area { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 末端驿站查询请求
/// </summary>
public class LastMileStationQueryRequest : PagedRequest
{
    public int? Status { get; set; }
    public int? StationType { get; set; }
}
