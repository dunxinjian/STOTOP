using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IConditionEvaluator
{
    bool Evaluate(string condition, Dictionary<string, object?> data, List<SchemaFieldDefinition> schemaFields);
}
