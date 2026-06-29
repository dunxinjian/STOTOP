namespace STOTOP.Module.Finance.Dtos;

public class AssetCategoryDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DepreciationMethod { get; set; } = string.Empty;
    public int UsefulLife { get; set; }
    public decimal ResidualRate { get; set; }
    public long? DepreciationAccountId { get; set; }
    public string? DepreciationAccountName { get; set; }
}

public class CreateAssetCategoryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DepreciationMethod { get; set; } = string.Empty;
    public int UsefulLife { get; set; }
    public decimal ResidualRate { get; set; }
    public long? DepreciationAccountId { get; set; }
}

public class AssetCardDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetValue { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime? StartDepreciationDate { get; set; }
    public int? UsefulLife { get; set; }
    public decimal? ResidualRate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

public class CreateAssetCardRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public long? DepartmentId { get; set; }
    public decimal OriginalValue { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime? StartDepreciationDate { get; set; }
    public int? UsefulLife { get; set; }
    public decimal? ResidualRate { get; set; }
    public string? Remark { get; set; }
}

public class UpdateAssetCardRequest
{
    public string Name { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string? Remark { get; set; }
}

public class DepreciationResultDto
{
    public int DepreciatedCount { get; set; }
    public decimal TotalDepreciationAmount { get; set; }
    public long? VoucherId { get; set; }
    public List<long> VoucherIds { get; set; } = new();
}

public class DepreciationPreviewDto
{
    public List<DepreciationDetailDto> Details { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int AssetCount { get; set; }
}

public class DepreciationDetailDto
{
    public long AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DepreciationMethod { get; set; } = string.Empty;
    public decimal OriginalValue { get; set; }
    public decimal NetValue { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal MonthlyDepreciation { get; set; }
}

public class AssetImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AssetImportResult
{
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<AssetImportError> Errors { get; set; } = new();
}
