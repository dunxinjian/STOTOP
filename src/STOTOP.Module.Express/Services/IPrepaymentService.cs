using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IPrepaymentService
{
    Task<PrepaymentDto> RechargeAsync(string clientId, decimal amount, DateTime? paymentDate, string? paymentMethod, string? remark);
    Task<PrepaymentBalanceDto?> GetBalanceAsync(string clientId);
    Task<PagedResult<PrepaymentTransactionDto>> GetTransactionsAsync(TransactionQueryRequest request);
    Task WriteOffAsync(string clientId, long invoiceId, decimal totalAmount, int paymentMode, decimal? prepayRatio, decimal? prepayPerTicket, int totalWaybills);
}
