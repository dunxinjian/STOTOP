namespace STOTOP.Module.CardFlow.Services.Redaction;

public interface ICardRedactionService
{
    CardRedactionResult Redact(CardRedactionRequest request);
}
