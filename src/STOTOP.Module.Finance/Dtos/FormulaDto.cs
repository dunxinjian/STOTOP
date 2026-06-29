using global::System.Text.Json.Serialization;

namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 公式记录DTO
/// </summary>
public class FormulaDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("reportType")]
    public string ReportType { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("rowIndex")]
    public int RowIndex { get; set; }

    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    [JsonPropertyName("formulaType")]
    public string FormulaType { get; set; } = string.Empty;

    [JsonPropertyName("accountCodes")]
    public string? AccountCodes { get; set; }

    [JsonPropertyName("displayConfig")]
    public string? DisplayConfig { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("accountSetId")]
    public long AccountSetId { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }
}

/// <summary>
/// 创建公式请求
/// </summary>
public class CreateFormulaRequest
{
    [JsonPropertyName("reportType")]
    public string ReportType { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("rowIndex")]
    public int RowIndex { get; set; }

    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    [JsonPropertyName("formulaType")]
    public string FormulaType { get; set; } = string.Empty;

    [JsonPropertyName("accountCodes")]
    public string? AccountCodes { get; set; }

    [JsonPropertyName("displayConfig")]
    public string? DisplayConfig { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("accountSetId")]
    public long AccountSetId { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新公式请求
/// </summary>
public class UpdateFormulaRequest
{
    [JsonPropertyName("itemName")]
    public string? ItemName { get; set; }

    [JsonPropertyName("rowIndex")]
    public int? RowIndex { get; set; }

    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    [JsonPropertyName("formulaType")]
    public string? FormulaType { get; set; }

    [JsonPropertyName("accountCodes")]
    public string? AccountCodes { get; set; }

    [JsonPropertyName("displayConfig")]
    public string? DisplayConfig { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }

    [JsonPropertyName("sortOrder")]
    public int? SortOrder { get; set; }
}

/// <summary>
/// 公式测试请求
/// </summary>
public class FormulaTestRequest
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = string.Empty;

    [JsonPropertyName("accountAmounts")]
    public Dictionary<string, decimal> AccountAmounts { get; set; } = new();

    [JsonPropertyName("rowResults")]
    public Dictionary<int, decimal> RowResults { get; set; } = new();
}

/// <summary>
/// 公式测试结果
/// </summary>
public class FormulaTestResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public decimal Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// 初始化默认公式请求
/// </summary>
public class InitDefaultFormulasRequest
{
    [JsonPropertyName("reportType")]
    public string ReportType { get; set; } = string.Empty;

    [JsonPropertyName("accountSetId")]
    public long AccountSetId { get; set; }
}
