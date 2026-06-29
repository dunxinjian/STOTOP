namespace STOTOP.Module.CardFlow.Dtos;

public class StagingQueryFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public long? BatchId { get; set; }
    public int? Status { get; set; }  // F处理状态
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; } = true;
    /// <summary>按字段名过滤</summary>
    public string? FieldName { get; set; }
    /// <summary>按字段值过滤</summary>
    public string? FieldValue { get; set; }
}

public class StagingStatsDto
{
    public string TargetTable { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int UnprocessedCount { get; set; }  // F处理状态=0
    public int ProcessedCount { get; set; }     // F处理状态=1
    public int FailedCount { get; set; }        // F处理状态=2
    public decimal TotalIncome { get; set; }    // 收入合计
    public decimal TotalExpense { get; set; }   // 支出合计
}

public class BatchUpdateStatusRequest
{
    public List<long> Ids { get; set; } = new();
    public int NewStatus { get; set; }
}

public class BatchReprocessRequest
{
    public List<long> Ids { get; set; } = new();
}

