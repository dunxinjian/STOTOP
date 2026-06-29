using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 异常处理日志
/// </summary>
public class QlExceptionLog : BaseEntity
{
    public long FExceptionId { get; set; }
    public long FOperatorId { get; set; }
    public string FAction { get; set; } = string.Empty;
    public string? FRemark { get; set; }
    public int? FFromStatus { get; set; }
    public int? FToStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
