namespace STOTOP.Module.CRM.Dtos;

#region ExternalContact

public class ExternalContactDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

public class CreateExternalContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? Remark { get; set; }
}

public class UpdateExternalContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
}

public class ExternalContactQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}

#endregion

#region Referral

public class ReferralDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long? OrgId { get; set; }
    public int ReferrerType { get; set; }
    public long? EmployeeId { get; set; }
    public long? ExternalContactId { get; set; }
    public string? ExternalContactName { get; set; }
    public DateOnly ReferralDate { get; set; }
    public string? Description { get; set; }
    public decimal? CommissionRate { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateReferralRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public long? OrgId { get; set; }
    public int ReferrerType { get; set; }
    public long? EmployeeId { get; set; }
    public long? ExternalContactId { get; set; }
    public DateOnly ReferralDate { get; set; }
    public string? Description { get; set; }
    public decimal? CommissionRate { get; set; }
}

public class ReferralQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CustomerId { get; set; }
    public long? OrgId { get; set; }
    public int? ReferrerType { get; set; }
    public long? EmployeeId { get; set; }
    public long? ExternalContactId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

#endregion

#region Commission

public class CommissionDto
{
    public long Id { get; set; }
    public long ReferralId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long? ContractId { get; set; }
    public decimal CommissionAmount { get; set; }
    public string? CalcBasis { get; set; }
    public long ApplicantId { get; set; }
    public int Status { get; set; }
    public long? OaProcessInstanceId { get; set; }
    public long? PaymentOrderId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateCommissionRequest
{
    public long ReferralId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long? ContractId { get; set; }
    public decimal CommissionAmount { get; set; }
    public string? CalcBasis { get; set; }
    public long ApplicantId { get; set; }
}

public class CommissionQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ReferralId { get; set; }
    public string? CustomerId { get; set; }
    public int? Status { get; set; }
    public long? OrgId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

#endregion

#region Commission Calc & Submit

public class CalcCommissionRequest
{
    public long ReferralId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long? ContractId { get; set; }
    /// <summary>
    /// 计算起始期间 YYYYMM
    /// </summary>
    public string StartPeriod { get; set; } = string.Empty;
    /// <summary>
    /// 计算截止期间 YYYYMM
    /// </summary>
    public string EndPeriod { get; set; } = string.Empty;
}

public class CalcCommissionResultDto
{
    public long ReferralId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CalcAmount { get; set; }
    public string CalcBasis { get; set; } = string.Empty;
}

public class SubmitCommissionRequest
{
    public long CommissionId { get; set; }
    public long OrgId { get; set; }
}

public class ApprovalCallbackRequest
{
    public long CommissionId { get; set; }
    /// <summary>
    /// true=通过, false=驳回
    /// </summary>
    public bool Approved { get; set; }
}

#endregion

#region Statistics

public class ReferralStatisticsDto
{
    public long? ReferrerId { get; set; }
    public string? ReferrerName { get; set; }
    public int ReferralCount { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal PaidCommission { get; set; }
}

#endregion
