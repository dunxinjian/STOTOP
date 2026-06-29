namespace STOTOP.Module.System.Dtos;

public class CodeRuleDto
{
    public long Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string BusinessEntity { get; set; } = string.Empty;
    public string CodeField { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SeqLength { get; set; }
    public string? Separator { get; set; }
    public string ResetPeriod { get; set; } = string.Empty;
    public bool OrgIsolation { get; set; }
    public bool Enabled { get; set; }
    public string? Description { get; set; }
    public string Preview { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CodeRuleUpdateDto
{
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SeqLength { get; set; } = 4;
    public string? Separator { get; set; }
    public string ResetPeriod { get; set; } = "never";
    public bool OrgIsolation { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public class CodeRuleCreateDto
{
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string BusinessEntity { get; set; } = string.Empty;
    public string CodeField { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SeqLength { get; set; } = 4;
    public string? Separator { get; set; }
    public string ResetPeriod { get; set; } = "never";
    public bool OrgIsolation { get; set; } = false;
    public string? Description { get; set; }
}
