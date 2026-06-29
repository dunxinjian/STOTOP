namespace STOTOP.Module.Finance.Dtos;

#region 方案 DTO

public class MigrationSchemeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceAccountSetId { get; set; } = string.Empty;
    public long TargetAccountSetId { get; set; }
    public string AuxMissingStrategy { get; set; } = "error";
    public string? Description { get; set; }
    public int Status { get; set; }
    public long OrgId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public int AccountMappingCount { get; set; }
    public int AuxMappingCount { get; set; }
    public int AssetMappingCount { get; set; }
}

public class CreateMigrationSchemeRequest
{
    public string Name { get; set; } = string.Empty;
    public string SourceAccountSetId { get; set; } = string.Empty;
    public long TargetAccountSetId { get; set; }
    public string AuxMissingStrategy { get; set; } = "error";
    public string? Description { get; set; }
}

public class UpdateMigrationSchemeRequest
{
    public string? Name { get; set; }
    public string? SourceAccountSetId { get; set; }
    public long? TargetAccountSetId { get; set; }
    public string? AuxMissingStrategy { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
}

#endregion

#region 科目映射 DTO

public class AccountMappingDto
{
    public Guid Id { get; set; }
    public Guid SchemeId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public long? TargetAccountId { get; set; }
    public string TargetCode { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public int MappingType { get; set; }
    public string? ConditionJson { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateAccountMappingRequest
{
    public Guid SchemeId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public long? TargetAccountId { get; set; }
    public string TargetCode { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public int MappingType { get; set; } = 1;
    public string? ConditionJson { get; set; }
    public int Priority { get; set; } = 10;
    public string? Description { get; set; }
}

public class BatchCreateAccountMappingRequest
{
    public List<CreateAccountMappingRequest> Items { get; set; } = new();
}

public class UpdateAccountMappingRequest
{
    public string? SourceCode { get; set; }
    public string? SourceName { get; set; }
    public long? TargetAccountId { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetName { get; set; }
    public int? MappingType { get; set; }
    public string? ConditionJson { get; set; }
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
}

#endregion

#region 辅助映射 DTO

public class AuxMappingDto
{
    public Guid Id { get; set; }
    public Guid SchemeId { get; set; }
    public string AuxType { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public long? TargetAuxItemId { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetName { get; set; }
    public string? Strategy { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateAuxMappingRequest
{
    public Guid SchemeId { get; set; }
    public string AuxType { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public long? TargetAuxItemId { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetName { get; set; }
    public string? Strategy { get; set; }
}

public class BatchCreateAuxMappingRequest
{
    public List<CreateAuxMappingRequest> Items { get; set; } = new();
}

public class UpdateAuxMappingRequest
{
    public string? AuxType { get; set; }
    public string? SourceCode { get; set; }
    public string? SourceName { get; set; }
    public long? TargetAuxItemId { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetName { get; set; }
    public string? Strategy { get; set; }
    public int? Status { get; set; }
}

#endregion

#region 资产映射 DTO

public class AssetMappingDto
{
    public Guid Id { get; set; }
    public Guid SchemeId { get; set; }
    public string SourceAssetCode { get; set; } = string.Empty;
    public long? TargetAssetCardId { get; set; }
    public string? TargetAssetCode { get; set; }
    public string? TargetAssetName { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateAssetMappingRequest
{
    public Guid SchemeId { get; set; }
    public string SourceAssetCode { get; set; } = string.Empty;
    public long? TargetAssetCardId { get; set; }
    public string? TargetAssetCode { get; set; }
    public string? TargetAssetName { get; set; }
}

public class BatchCreateAssetMappingRequest
{
    public List<CreateAssetMappingRequest> Items { get; set; } = new();
}

public class UpdateAssetMappingRequest
{
    public string? SourceAssetCode { get; set; }
    public long? TargetAssetCardId { get; set; }
    public string? TargetAssetCode { get; set; }
    public string? TargetAssetName { get; set; }
    public int? Status { get; set; }
}

#endregion

#region 向导 DTO

public class SourceSubjectInfo
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int Count { get; set; }
}

public class MigrationAutoMatchResult
{
    public string SourceCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public long? TargetId { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetName { get; set; }
    public string Confidence { get; set; } = "none"; // exact_code/exact_name/fuzzy_name/none
}

public class AutoMatchRequest
{
    public Guid SchemeId { get; set; }
    public long TargetAccountSetId { get; set; }
    public List<SourceSubjectInfo> Subjects { get; set; } = new();
}

public class MigrationAutoMatchResponse
{
    public List<MigrationAutoMatchResult> Matches { get; set; } = new();
    public List<SourceSubjectInfo> Unmatched { get; set; } = new();
}

public class ExtractSubjectsRequest
{
    public string? FileId { get; set; }
    public string? FilePath { get; set; }
    public string SubjectCodeColumn { get; set; } = string.Empty;
    public string? SubjectNameColumn { get; set; }
}

public class ExtractSubjectsResponse
{
    public List<SourceSubjectInfo> Subjects { get; set; } = new();
}

public class ColumnRoleConfig
{
    public string? VoucherDateColumn { get; set; }
    public string? VoucherNoColumn { get; set; }
    public string? VoucherWordColumn { get; set; }
    public string? SubjectCodeColumn { get; set; }
    public string? SubjectNameColumn { get; set; }
    public string? SummaryColumn { get; set; }
    public string? DebitColumn { get; set; }
    public string? CreditColumn { get; set; }
    public string? AuxiliaryColumn { get; set; }
    public string? AttachmentCountColumn { get; set; }
}

public class WizardPreviewRequest
{
    public Guid SchemeId { get; set; }
    public string? FileId { get; set; }
    public string? FilePath { get; set; }
    public ColumnRoleConfig ColumnRoles { get; set; } = new();
    public int Rows { get; set; } = 5;
}

public class WizardPreviewResponse
{
    public List<WizardPreviewItem> Previews { get; set; } = new();
}

public class WizardPreviewItem
{
    public Dictionary<string, string?> OriginalRow { get; set; } = new();
    public WizardConvertedVoucher? Converted { get; set; }
    public string? Error { get; set; }
}

public class WizardConvertedVoucher
{
    public string? VoucherDate { get; set; }
    public string? VoucherNo { get; set; }
    public string? VoucherWord { get; set; }
    public string? Summary { get; set; }
    public string? TargetAccountCode { get; set; }
    public string? TargetAccountName { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
}

public class WizardCommitRequest
{
    public Guid SchemeId { get; set; }
    public List<CreateAccountMappingRequest> AccountMappings { get; set; } = new();
    public List<CreateAuxMappingRequest> AuxMappings { get; set; } = new();
    public List<CreateAssetMappingRequest> AssetMappings { get; set; } = new();
    public ColumnRoleConfig ColumnRoles { get; set; } = new();
    public Dictionary<string, string>? AuxConfig { get; set; }
}

public class WizardCommitResponse
{
    public bool Success { get; set; }
    public long? AgentRuleId { get; set; }
}

#endregion
