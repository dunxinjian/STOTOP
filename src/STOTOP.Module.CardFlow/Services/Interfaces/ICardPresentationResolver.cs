using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardPresentationResolver
{
    CardPresentationRuntimeView Resolve(CardPresentationResolveRequest request);
}
