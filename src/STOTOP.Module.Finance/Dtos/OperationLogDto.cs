using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Dtos;

public class OperationLogQueryRequest : PagedRequest
{
    public long AccountSetId { get; set; }
    public string? Module { get; set; }
    public string? OperationType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class OperationLogDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long? TargetId { get; set; }
    public string? TargetCode { get; set; }
    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime OperationTime { get; set; }
    public string? IpAddress { get; set; }
    public string? ExtraData { get; set; }
}
