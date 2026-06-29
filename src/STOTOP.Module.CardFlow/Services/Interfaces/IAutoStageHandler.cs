namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IAutoStageHandler
{
    Task<AutoStageResult> ExecuteAsync(AutoStageContext context);
}

public record AutoStageContext(long CardId, string CardDataJson, string StageConfigJson, long OrgId);
public record AutoStageResult(bool Success, string? Output, string? ErrorMessage);
