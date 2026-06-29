using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointApplication : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FApplicantId { get; set; }
    public long FRuleId { get; set; }
    public string FApplicationNote { get; set; } = string.Empty;
    public string? FAttachment { get; set; }
    public int FStatus { get; set; }
    public long? FApproverId { get; set; }
    public string? FApprovalComment { get; set; }
    public DateTime? FApprovalTime { get; set; }
    /// <summary>账户类型：1=A / 2=B（默认 B）</summary>
    public int F账户类型 { get; set; } = 2;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
