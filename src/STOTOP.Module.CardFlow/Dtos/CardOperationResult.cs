namespace STOTOP.Module.CardFlow.Dtos;

public class CardOperationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long? CardId { get; set; }
    public string? CardNumber { get; set; }
    public string? NewStatus { get; set; }

    public static CardOperationResult Ok(long cardId, string? message = null) => new() { Success = true, CardId = cardId, Message = message };
    public static CardOperationResult Fail(string message) => new() { Success = false, Message = message };
}
