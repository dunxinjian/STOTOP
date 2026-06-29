using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF链路关注 - 记录关注链路的用户</summary>
public class WfChainFollower : BaseEntity
{
    public string FChainId { get; set; } = string.Empty;
    public long FUserId { get; set; }
    public string? FUserName { get; set; }
    public DateTime FFollowTime { get; set; } = DateTime.Now;
    public bool FIsMuted { get; set; } = false;
}
