using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IInvoiceService
{
    Task<int> ImportInvoicesAsync(Stream fileStream, long accountSetId);
    Task<InvoicePagedResult> GetInvoicesAsync(InvoiceQueryRequest request, long accountSetId);
    Task<InvoiceDto?> GetInvoiceByIdAsync(long id);
    Task<bool> MatchInvoiceAsync(long invoiceId, long voucherId, long accountSetId);
    Task<VoucherDto> GenerateVoucherFromInvoiceAsync(long invoiceId, long accountSetId, InvoiceGenerateVoucherRequest request);
    Task<List<TaxSummaryDto>> GetTaxSummaryAsync(int year, long accountSetId);
}
