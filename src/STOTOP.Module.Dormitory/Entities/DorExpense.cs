using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 费用记录
/// </summary>
public class DorExpense : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FRoomId { get; set; }
    public string FExpenseType { get; set; } = string.Empty;
    public decimal FAmount { get; set; }
    public string FMonth { get; set; } = string.Empty;
    public string? FShareMethod { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
