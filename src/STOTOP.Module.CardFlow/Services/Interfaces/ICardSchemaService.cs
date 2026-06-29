using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardSchemaService
{
    ValidationResult ValidateCardData(string schemaJson, string dataJson);
    string GenerateTitle(string template, string dataJson, string flowName, string cardNumber);
}

public record ValidationResult(bool IsValid, List<string> Errors);
