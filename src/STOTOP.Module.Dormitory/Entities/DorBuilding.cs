using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

public class DorBuilding : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string? FAddress { get; set; }
    public int FTotalFloors { get; set; } = 1;
    public long? FManagerId { get; set; }
    public string? FDormitoryType { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public List<DorRoom> Rooms { get; set; } = new();
}
