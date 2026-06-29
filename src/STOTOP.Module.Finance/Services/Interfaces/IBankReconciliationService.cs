using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IBankReconciliationService
{
    Task<int> ImportBankStatementsAsync(Stream fileStream, BankStatementImportRequest request, long accountSetId);
    Task<BankStatementPagedResult> GetStatementsAsync(BankStatementQueryRequest request, long accountSetId);
    Task<List<UnmatchedVoucherDto>> GetUnmatchedVouchersAsync(DateTime startDate, DateTime endDate, long accountSetId);
    Task<int> AutoMatchAsync(long accountSetId);
    Task<bool> ManualMatchAsync(ManualMatchRequest request, long accountSetId);
    Task<bool> UnmatchAsync(long reconciliationId);
    Task<ReconciliationReportDto> GetReconciliationReportAsync(long periodId, long accountSetId);
}
