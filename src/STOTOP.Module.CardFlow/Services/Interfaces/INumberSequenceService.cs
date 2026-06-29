namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface INumberSequenceService
{
    Task<string> GenerateNumberAsync(long flowDefinitionId, long orgId, string template);
}
