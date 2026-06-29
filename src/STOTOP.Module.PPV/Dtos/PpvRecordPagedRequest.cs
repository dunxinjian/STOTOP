namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// PPV 产值记录分页查询请求
/// </summary>
public class PpvRecordPagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>期间过滤，格式 yyyyMM</summary>
    public string? Period { get; set; }
    /// <summary>员工ID过滤</summary>
    public long? EmployeeId { get; set; }
    /// <summary>审核状态过滤：0=待审核 1=已通过 2=已驳回</summary>
    public int? Status { get; set; }
}
