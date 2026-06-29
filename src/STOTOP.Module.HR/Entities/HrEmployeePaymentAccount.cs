using STOTOP.Core.Models;

namespace STOTOP.Module.HR.Entities;

public class HrEmployeePaymentAccount : BaseEntity
{
    public long FEmployeeId { get; set; }
    public string? FAccountType { get; set; }
    public string? FAccountName { get; set; }
    public string? FAccountNumber { get; set; }
    public string? FBankName { get; set; }
    public string? FBankBranch { get; set; }
    public int FIsDefault { get; set; } = 0;
    public string? FRemark { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual HrEmployee Employee { get; set; } = null!;
}
