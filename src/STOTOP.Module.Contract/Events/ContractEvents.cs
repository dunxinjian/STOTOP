using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Contract.Events;

public class ContractExpiringEvent : BusinessEvent
{
    public long ContractId { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public long RecipientId { get; set; }
    public int DaysRemaining { get; set; }
}

public class ContractSignedEvent : BusinessEvent
{
    public long ContractId { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public long SignedByUserId { get; set; }
}

public class ContractExpiredEvent : BusinessEvent
{
    public long ContractId { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public long ResponsibleUserId { get; set; }
}
