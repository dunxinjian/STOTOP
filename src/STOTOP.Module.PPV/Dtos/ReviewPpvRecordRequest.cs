namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// PPV 审核请求
/// </summary>
public class ReviewPpvRecordRequest
{
    public bool Approve { get; set; }
    public string? Remark { get; set; }
}
