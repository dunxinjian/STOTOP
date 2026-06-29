using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>收入登记</summary>
public class ConfIncome : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public long? FAttendeeId { get; set; }
    public string? FType { get; set; }
    public decimal FAmount { get; set; }
    public string? FPaymentMethod { get; set; }
    public string? FPayerName { get; set; }
    public string? FPayerOrganization { get; set; }
    public DateTime FPaymentDate { get; set; }
    public string? FReceiptNumber { get; set; }
    public string? FRemark { get; set; }
    public string? FRegistrant { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public ConfAttendee? Attendee { get; set; }
}
