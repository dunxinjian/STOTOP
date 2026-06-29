using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountSetAuthorization : BaseEntity
{
    public long FUserId { get; set; }
    public long FAccountSetId { get; set; }
    public long FAccountSetRoleId { get; set; }
    public long FOrgId { get; set; }
    public long FGrantedBy { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
