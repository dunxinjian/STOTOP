namespace STOTOP.Module.Finance.Dtos;

/// <summary>账套规则 DTO（F 前缀对齐 AccountSetDto，序列化后前端得 fXxx）</summary>
public class AccountSetRuleDto
{
    public long FAccountSetId { get; set; }
    public bool FRequireAuditSeparation { get; set; }
    public string? FProfitAccountCode { get; set; }
    public string? FRetainedAccountCode { get; set; }
    public List<string> FEnabledVoucherWords { get; set; } = new();
}

/// <summary>账套规则保存请求（Upsert 语义，按当前账套一账套一行）</summary>
public class UpdateAccountSetRuleRequest
{
    public bool FRequireAuditSeparation { get; set; }
    public string? FProfitAccountCode { get; set; }
    public string? FRetainedAccountCode { get; set; }
    public List<string> FEnabledVoucherWords { get; set; } = new();
}
