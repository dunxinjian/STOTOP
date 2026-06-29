using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointRecord : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FUserId { get; set; }
    public long FSourceId { get; set; }
    public long? FRuleId { get; set; }
    public int FType { get; set; }
    public int FPointValue { get; set; }
    public int FBalance { get; set; }
    public string? FRelatedModule { get; set; }
    public string? FRelatedEntityType { get; set; }
    public long? FRelatedEntityId { get; set; }
    public long FOperatorId { get; set; }
    public string FRemark { get; set; } = string.Empty;
    /// <summary>账户类型：1=A / 2=B</summary>
    public int F账户类型 { get; set; } = 1;
    /// <summary>关联业务事件类型（事件幂等键之一）</summary>
    public string? F关联事件类型 { get; set; }
    /// <summary>关联业务事件 ID（事件幂等键之二，手工记录留 NULL）</summary>
    public string? F关联事件ID { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
