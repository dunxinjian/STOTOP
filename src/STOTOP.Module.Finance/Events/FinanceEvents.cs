using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Finance.Events;

public class VoucherPendingAuditEvent : BusinessEvent
{
    public long VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long CreatorId { get; set; }
    public long AuditorId { get; set; }
    public long AccountSetId { get; set; }
}

public class AccountPeriodClosedEvent : BusinessEvent
{
    public long PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public long AccountSetId { get; set; }
}
