using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IPrepaymentWaybillService
{
    // Waybill Pool
    Task<PagedResult<WaybillPoolDto>> GetWaybillPoolsAsync(WaybillPoolQueryRequest request);
    Task<WaybillPoolDto?> GetWaybillPoolByIdAsync(long id);
    Task<WaybillPoolDto> CreateWaybillPoolAsync(CreateWaybillPoolRequest request);
    Task<bool> DeleteWaybillPoolAsync(long id);

    // Customer Account
    Task<CustomerAccountDto?> GetCustomerAccountAsync(string customerId, string brandCode);
    Task<CustomerAccountDto> RechargeAccountAsync(long accountId, decimal amount);
    Task<CustomerAccountDto> DeductAccountAsync(long accountId, decimal amount);

    // Prepayment
    Task<PagedResult<PrepaymentDto>> GetPrepaymentsAsync(PrepaymentQueryRequest request);
    Task<PrepaymentDto?> GetPrepaymentByIdAsync(long id);
    Task<PrepaymentDto> CreatePrepaymentAsync(CreatePrepaymentRequest request);
    Task<bool> ConfirmPrepaymentReceivedAsync(long id, decimal receivedAmount, long? bankTransactionId);

    // Waybill Allocation
    Task<WaybillAllocationDto> AllocateWaybillAsync(AllocateWaybillRequest request);
    Task<bool> RecycleWaybillAsync(long allocationId);
    Task<List<WaybillAllocationDto>> GetAllocationsByCustomerAsync(string customerId);
    Task<List<WaybillAllocationDto>> GetAllocationsByPoolAsync(long poolId);
}
