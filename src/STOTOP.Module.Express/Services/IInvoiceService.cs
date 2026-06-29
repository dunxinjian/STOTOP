using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> GetPagedListAsync(InvoiceQueryRequest request);
    Task<InvoiceDetailDto?> GetDetailAsync(long id);
    Task<InvoiceDto> GenerateInvoiceAsync(string clientId, string brandCode, DateTime periodStart, DateTime periodEnd);
    Task<InvoiceDto?> ConfirmAsync(long id);
    Task<InvoiceDto?> SendAsync(long id);
    Task<InvoiceDto?> ReceivePaymentAsync(long id, decimal amount);
    Task<ReconciliationDetailDto> GetReconciliationDetailAsync(long invoiceId);
    Task<bool> ConfirmReconciliationAsync(long invoiceId, ReconciliationConfirmRequest request);
    Task<bool> RaiseDisputeAsync(long invoiceId, ReconciliationDisputeRequest request);
    Task<bool> ResolveDisputeAsync(long invoiceId, ReconciliationResolveRequest request);
    Task<byte[]> ExportReconciliationAsync(long invoiceId);
}
