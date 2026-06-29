using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IStageActionPolicyService
{
    StageActionPolicyValidationResult ValidateAction(StageConfigEnvelope normalizedConfig, string action);
}

public sealed class StageActionPolicyValidationResult
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage { get; set; }

    public static StageActionPolicyValidationResult Ok() => new();

    public static StageActionPolicyValidationResult Fail(string message) => new()
    {
        ErrorMessage = message
    };
}
