using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountSetRolePermission : BaseEntity
{
    public long FAccountSetRoleId { get; set; }
    public string FPermissionCode { get; set; } = "";
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
