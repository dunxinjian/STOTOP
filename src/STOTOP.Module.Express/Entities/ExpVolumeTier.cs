using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

public class ExpVolumeTier : BaseEntity, IOrgScoped
{
    public string FBusinessObjectId { get; set; } = string.Empty;
    public int FMinMonthlyVolume { get; set; }
    public long FQuotationPlanId { get; set; }
    public bool FIsActive { get; set; } = true;
    public long FOrgId { get; set; }
    public string FBrandCode { get; set; } = string.Empty;
}
