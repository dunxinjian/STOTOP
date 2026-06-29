namespace STOTOP.Module.Finance.Dtos;

public class TreasuryAccountBindingDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public long? OrgId { get; set; }
    public long? ChannelId { get; set; }
    public long? CashAccountId { get; set; }
    public string? AccountNo { get; set; }
    public string OpeningSource { get; set; } = "account_balance";
    public decimal? ManualOpeningAmount { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

public class TreasuryPlanLineDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public long? OrgId { get; set; }
    public DateTime PlanDate { get; set; }
    public DateTime WeekStartDate { get; set; }
    public string Direction { get; set; } = "outflow";
    public string CashCategory { get; set; } = "other";
    public decimal Amount { get; set; }
    public decimal Probability { get; set; } = 100m;
    public string SourceType { get; set; } = "manual";
    public long? SourceId { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Remark { get; set; }
}

public class TreasuryWeekDto
{
    public DateTime WeekStartDate { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal EndingCash { get; set; }
    public bool BelowSafetyCash { get; set; }
}

public class Rolling13WeekTreasuryDto
{
    public decimal OpeningCash { get; set; }
    public decimal SafetyCash { get; set; }
    public List<TreasuryWeekDto> Weeks { get; set; } = new();
}
