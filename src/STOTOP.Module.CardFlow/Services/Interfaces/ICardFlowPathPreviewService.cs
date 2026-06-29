using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardFlowPathPreviewService
{
    Task<CardFlowPathPreviewDto> PreviewDraftVersionAsync(
        long definitionId,
        CardFlowPathPreviewRequest request,
        CancellationToken cancellationToken = default);
}
