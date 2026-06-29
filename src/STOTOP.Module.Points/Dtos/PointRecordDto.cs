using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分记录 - 列表 DTO
/// </summary>
public class PointRecordListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public long SourceId { get; set; }
    public string? SourceName { get; set; }
    public long? RuleId { get; set; }
    public string? RuleName { get; set; }
    public int Type { get; set; }
    public int PointValue { get; set; }
    public int Balance { get; set; }
    public string? RelatedModule { get; set; }
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public long OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string Remark { get; set; } = string.Empty;
    /// <summary>账户类型（1=A / 2=B），与 PmPointRecord.F账户类型 一致</summary>
    public int AccountType { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 积分记录 - 详情 DTO
/// </summary>
public class PointRecordDetailDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public long SourceId { get; set; }
    public string? SourceName { get; set; }
    public long? RuleId { get; set; }
    public string? RuleName { get; set; }
    public int Type { get; set; }
    public int PointValue { get; set; }
    public int Balance { get; set; }
    public string? RelatedModule { get; set; }
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public long OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string Remark { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 积分记录查询请求（支持多维筛选：来源/类型/时间/用户）
/// </summary>
public class PointRecordPagedRequest : PagedRequest
{
    public long? UserId { get; set; }
    public long? SourceId { get; set; }
    public int? Type { get; set; }
    /// <summary>账户类型筛选（1=A / 2=B）</summary>
    public int? AccountType { get; set; }
    public string? RelatedModule { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 手动奖分请求
/// </summary>
public class ManualAwardRequest
{
    public long UserId { get; set; }
    public long SourceId { get; set; }
    public int PointValue { get; set; }
    public string Remark { get; set; } = string.Empty;
    /// <summary>关联事件类型（事件幂等键，可选）</summary>
    public string? RelatedEventType { get; set; }
    /// <summary>关联事件ID（事件幂等键，可选）</summary>
    public string? RelatedEventId { get; set; }
    /// <summary>关联模块编码（默认 "Points"）</summary>
    public string? RelatedModule { get; set; }
    /// <summary>关联实体类型（可选）</summary>
    public string? RelatedEntityType { get; set; }
    /// <summary>关联实体ID（可选）</summary>
    public long? RelatedEntityId { get; set; }
}

/// <summary>
/// 手动扣分请求
/// </summary>
public class ManualDeductRequest
{
    public long UserId { get; set; }
    public long SourceId { get; set; }
    public int PointValue { get; set; }
    public string Remark { get; set; } = string.Empty;
    /// <summary>关联事件类型（事件幂等键，可选）</summary>
    public string? RelatedEventType { get; set; }
    /// <summary>关联事件ID（事件幂等键，可选）</summary>
    public string? RelatedEventId { get; set; }
    /// <summary>关联模块编码（默认 "Points"）</summary>
    public string? RelatedModule { get; set; }
    /// <summary>关联实体类型（可选）</summary>
    public string? RelatedEntityType { get; set; }
    /// <summary>关联实体ID（可选）</summary>
    public long? RelatedEntityId { get; set; }
}
