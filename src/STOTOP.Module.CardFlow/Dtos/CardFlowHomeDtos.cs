namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// 公司/经营单元 DTO（来自 IMP网点公司 表）
/// </summary>
public class ImportCompanyDto
{
    public int Fid { get; set; }
    public string FName { get; set; } = "";
    public bool FIsBusinessUnit { get; set; }
    public int FSortOrder { get; set; }
}

/// <summary>
/// CardFlow 首页统计 DTO
/// </summary>
public class CardFlowHomeDto
{
    public DownloadSummary Download { get; set; } = new();
    public ImportSummary Import { get; set; } = new();
}

/// <summary>
/// 下载任务统计摘要
/// </summary>
public class DownloadSummary
{
    public int TotalTasks { get; set; }
    public int TodayExecutions { get; set; }
    public int RecentFailures { get; set; }
}

/// <summary>
/// 导入统计摘要
/// </summary>
public class ImportSummary
{
    public int FileTypeCount { get; set; }
    public int TodayBatches { get; set; }
    public int PendingRows { get; set; }
}
