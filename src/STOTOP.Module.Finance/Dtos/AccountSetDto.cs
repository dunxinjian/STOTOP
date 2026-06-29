namespace STOTOP.Module.Finance.Dtos;

public class AccountSetDto
{
    public long Id { get; set; }
    public string FName { get; set; } = "";
    public string FCode { get; set; } = "";
    public string FCompanyName { get; set; } = "";
    public string? FDescription { get; set; }
    public bool FIsDefault { get; set; }
    public int FStatus { get; set; }
    public int FSortOrder { get; set; }
    public int FStartYear { get; set; }
    public int FStartMonth { get; set; }
    public long? FOrgId { get; set; }  // 组织ID

    // 模板参数 - 用于创建时自动初始化
    public string? FTemplateType { get; set; }      // "empty" | "industry" | "existing"
    public string? FIndustryCode { get; set; }       // 行业模板编码，如 "standard"(小企业标准), "express-delivery"(快递行业)
    public long? FSourceAccountSetId { get; set; }   // 源账套ID（复制模式）
}

/// <summary>
/// 科目模板DTO - 用于模板预览（创建账套时的行业模板预览）
/// </summary>
public class AccountSetTemplatePreviewDto
{
    public string FCode { get; set; } = "";
    public string FName { get; set; } = "";
    public string FCategory { get; set; } = "";
    public string FBalanceDirection { get; set; } = "";
    public int FLevel { get; set; }
    public int FIsLeaf { get; set; }
}

public class CreateAccountSetRequest
{
    public string FName { get; set; } = "";
    public string FCode { get; set; } = "";
    public string FCompanyName { get; set; } = "";
    public string? FDescription { get; set; }
    public bool FIsDefault { get; set; }
    public int FStatus { get; set; }
    public int FSortOrder { get; set; }
    public int FStartYear { get; set; }
    public int FStartMonth { get; set; }
    public string? FTemplateType { get; set; }
    public string? FIndustryCode { get; set; }
    public long? FSourceAccountSetId { get; set; }
    /// <summary>科目模板ID（可选，使用自定义科目模板初始化）</summary>
    public long? TemplateId { get; set; }
}
