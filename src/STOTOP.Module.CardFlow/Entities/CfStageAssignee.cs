using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfStageAssignee : BaseEntity
{
    public long FStageInstanceId { get; set; }
    public long FUserId { get; set; }
    public string FUserName { get; set; } = string.Empty;
    public string? FRoleCode { get; set; }
    public int FSortOrder { get; set; } = 0;
    public DateTime FAssignedTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public string FStatus { get; set; } = "pending";
    public string? FOpinion { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
