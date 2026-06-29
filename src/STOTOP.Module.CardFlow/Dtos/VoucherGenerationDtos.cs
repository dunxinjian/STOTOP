namespace STOTOP.Module.CardFlow.Dtos;

public class VoucherGenerationRecordDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public string TargetTable { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int UnmatchedRows { get; set; }
    public List<UnmatchedDetailDto>? UnmatchedDetails { get; set; }
    public int GeneratedVoucherCount { get; set; }
    public List<long>? VoucherIds { get; set; }
    public int Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

public class UnmatchedDetailDto
{
    public long RowId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
