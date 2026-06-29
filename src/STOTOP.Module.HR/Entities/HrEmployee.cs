using STOTOP.Core.Models;

namespace STOTOP.Module.HR.Entities;

public class HrEmployee : BaseEntity
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FUserId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string? FGender { get; set; }
    public DateTime? FBirthDate { get; set; }
    public string? FIdCardNumber { get; set; }
    public string? FPhone { get; set; }
    public string? FEthnicity { get; set; }
    public string? FEducation { get; set; }
    public string? FMaritalStatus { get; set; }
    public string? FHomeAddress { get; set; }
    public string? FHouseholdAddress { get; set; }
    public string? FEmergencyContact { get; set; }
    public string? FEmergencyContactPhone { get; set; }
    public string? FEmergencyContactRelation { get; set; }
    public DateTime? FEntryDate { get; set; }
    public DateTime? FRegularDate { get; set; }
    public DateTime? FLeaveDate { get; set; }
    public int FEmployeeStatus { get; set; } = 1;
    public string? FRemark { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<HrEmployeePaymentAccount> PaymentAccounts { get; set; } = new List<HrEmployeePaymentAccount>();
}
