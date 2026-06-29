using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Validation;

public interface IImportCalculationValidationService
{
    Task<ImportValidationSummaryDto> GetSummaryAsync(long batchId, long orgId, CancellationToken cancellationToken = default);

    Task<ImportValidationReportDto> RunAsync(
        long batchId,
        long orgId,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ImportValidationFindingDto?> GetRowDetailAsync(
        long batchId,
        long rowId,
        long orgId,
        CancellationToken cancellationToken = default);
}
