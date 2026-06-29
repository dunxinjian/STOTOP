namespace STOTOP.Module.Finance.Dtos;

public class TrialBalanceDto
{
    public long Id { get; set; }
    public long PeriodId { get; set; }
    public long AccountSetId { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
    public List<TrialBalanceDetailDto> Details { get; set; } = new();
    public DateTime GeneratedTime { get; set; }
}

public class TrialBalanceDetailDto
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}
