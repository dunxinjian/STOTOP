using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardFlowSourceContextVerifier
{
    Task<SourceContextVerificationResult> VerifyAsync(
        CreateCardRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SourceContextVerificationResult
{
    public bool HasSourceContext { get; set; }
    public bool SourceVerified { get; set; }
    public string? TrustedDataJson { get; set; }
    public string? StoredInitialDataJson { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}
