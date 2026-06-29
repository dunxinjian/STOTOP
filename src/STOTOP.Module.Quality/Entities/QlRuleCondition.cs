using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 规则条件
/// </summary>
public class QlRuleCondition : BaseEntity
{
    public long FRuleId { get; set; }
    public string FFieldName { get; set; } = string.Empty;
    public string FOperator { get; set; } = string.Empty;
    public string FThreshold { get; set; } = string.Empty;
    public string FLogicRelation { get; set; } = "AND";
    public int FSort { get; set; }
}
