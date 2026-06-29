namespace STOTOP.Module.CardFlow.Dtos;

public class StagingTableInfo
{
    public string TableName { get; set; } = "";
    public List<StagingColumnInfo> Columns { get; set; } = new();
}

public class StagingColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}

/// <summary>管道（流程定义）列表DTO，对应前端 PipelineDto</summary>
public class PipelineListDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BizTag { get; set; } = string.Empty;
    public string? TagColor { get; set; }
    public bool EnableSubBatchParallel { get; set; }
    public int Status { get; set; }
    public int OrgId { get; set; }
    public int AutoPluginCount { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}
