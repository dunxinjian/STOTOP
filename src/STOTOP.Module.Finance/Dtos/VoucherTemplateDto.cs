namespace STOTOP.Module.Finance.Dtos;

public class VoucherTemplateDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<VoucherTemplateEntryDto> Entries { get; set; } = new();
}

public class VoucherTemplateEntryDto
{
    public long Id { get; set; }
    public string? Summary { get; set; }
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public int Seq { get; set; }
    public string? AuxiliaryJson { get; set; }
}

public class VoucherTemplateCreateRequest
{
    public long AccountSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sort { get; set; }
    public List<VoucherTemplateEntryDto> Entries { get; set; } = new();
}

public class VoucherTemplateListDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int EntryCount { get; set; }
    public DateTime CreateTime { get; set; }
}
