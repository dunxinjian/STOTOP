using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IStageConfigParser
{
    StageConfigEnvelope Parse(string? inputFieldsJson);
}
