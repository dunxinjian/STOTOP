using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinChangeHistory : BaseEntity
{
    public string FEntityType { get; set; } = string.Empty;
    public long FEntityId { get; set; }
    public string FFieldName { get; set; } = string.Empty;
    public string? FOldValue { get; set; }
    public string? FNewValue { get; set; }
    public long FOperatorId { get; set; }
    public string FOperatorName { get; set; } = string.Empty;
    public DateTime FOperationTime { get; set; }
    public long FAccountSetId { get; set; }
}
