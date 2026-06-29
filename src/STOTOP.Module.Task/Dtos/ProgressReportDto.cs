using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 进度上报列表DTO
/// </summary>
public class ProgressReportListDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public int Progress { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal? Hours { get; set; }
    public bool PushedToDingTalk { get; set; }
    public DateTime CreateTime { get; set; }
    public List<AttachmentListDto> Attachments { get; set; } = new();
}

/// <summary>
/// 创建进度上报请求（含进度值+说明+工时+附件）
/// </summary>
public class CreateProgressReportRequest
{
    public int Progress { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal? Hours { get; set; }
}

/// <summary>
/// 进度上报分页查询请求
/// </summary>
public class ProgressReportPagedRequest : PagedRequest
{
    public long? ReporterId { get; set; }
}
