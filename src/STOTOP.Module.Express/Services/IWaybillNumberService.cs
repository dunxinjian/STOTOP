using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IWaybillNumberService
{
    Task<List<WaybillNumberPoolDto>> GetPoolListAsync();
    Task<WaybillNumberPoolDto> CreatePoolAsync(CreatePoolRequest request);
    Task<WaybillNumberTransactionDto> AllocateAsync(long poolId, string clientId, string brandCode, int quantity);
    Task<WaybillNumberTransactionDto> ReturnAsync(string clientId, string brandCode, int quantity);
    Task<ClientWaybillBalanceDto?> GetClientBalanceAsync(string clientId, string brandCode);
}
