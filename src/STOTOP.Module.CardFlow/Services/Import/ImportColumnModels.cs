using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Services.Import;

/// <summary>列映射配置项</summary>
public class ColumnMappingItem
{
    [JsonPropertyName("excelColumn")]
    public string ExcelColumn { get; set; } = string.Empty;

    [JsonPropertyName("dbColumn")]
    public string DbColumn { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    public List<string>? Aliases { get; set; }
}

/// <summary>合计行检测配置</summary>
public class TotalRowDetectionConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("emptyFields")]
    public List<string>? EmptyFields { get; set; }

    [JsonPropertyName("containsKeywords")]
    public List<string>? ContainsKeywords { get; set; }
}
