using STOTOP.Core.Models;

namespace STOTOP.Module.HR.Dtos;

public class EmployeeDto
{
    public long Id { get; set; }
    public string FUID { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserAccount { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? IdCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Ethnicity { get; set; }
    public string? Education { get; set; }
    public string? MaritalStatus { get; set; }
    public string? HomeAddress { get; set; }
    public string? HouseholdAddress { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? RegularDate { get; set; }
    public DateTime? LeaveDate { get; set; }
    public int EmployeeStatus { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<PaymentAccountDto> PaymentAccounts { get; set; } = new();
}

public class PaymentAccountDto
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string? AccountType { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public int IsDefault { get; set; }
    public string? Remark { get; set; }
}

public class CreateEmployeeRequest
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? IdCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Ethnicity { get; set; }
    public string? Education { get; set; }
    public string? MaritalStatus { get; set; }
    public string? HomeAddress { get; set; }
    public string? HouseholdAddress { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? RegularDate { get; set; }
    public int EmployeeStatus { get; set; } = 1;
    public string? Remark { get; set; }
    public List<SavePaymentAccountRequest>? PaymentAccounts { get; set; }
}

public class UpdateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? IdCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Ethnicity { get; set; }
    public string? Education { get; set; }
    public string? MaritalStatus { get; set; }
    public string? HomeAddress { get; set; }
    public string? HouseholdAddress { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? RegularDate { get; set; }
    public DateTime? LeaveDate { get; set; }
    public int EmployeeStatus { get; set; } = 1;
    public string? Remark { get; set; }
    public List<SavePaymentAccountRequest>? PaymentAccounts { get; set; }
}

public class SavePaymentAccountRequest
{
    public long? Id { get; set; }
    public string? AccountType { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public int IsDefault { get; set; }
    public string? Remark { get; set; }
}

public class EmployeePagedRequest : PagedRequest
{
    public int? EmployeeStatus { get; set; }
}

public class UserSimpleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
}
