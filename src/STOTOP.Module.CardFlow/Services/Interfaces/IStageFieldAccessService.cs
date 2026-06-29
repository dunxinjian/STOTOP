using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IStageFieldAccessService
{
    StageFieldAccessValidationResult ValidateSupplement(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyDictionary<string, object>? supplement);

    StageFieldAccessValidationResult ValidateRequiredFields(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyDictionary<string, object?> currentData,
        IReadOnlyDictionary<string, object>? supplement);

    StageFieldAccessValidationResult ValidateDetailEdits(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyCollection<DetailRowEditRequest>? detailEdits);

    IReadOnlySet<string> GetWritableFieldKeys(StageConfigEnvelope normalizedConfig);
}

public sealed class StageFieldAccessValidationResult
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage { get; set; }

    public static StageFieldAccessValidationResult Ok() => new();

    public static StageFieldAccessValidationResult Fail(string message) => new()
    {
        ErrorMessage = message
    };
}
