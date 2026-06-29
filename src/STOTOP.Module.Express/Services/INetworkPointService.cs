using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface INetworkPointService
{
    Task<PagedResult<NetworkPointDto>> GetListAsync(NetworkPointQueryRequest request);
    Task<NetworkPointDto?> GetByIdAsync(string code);
    Task<bool> CheckCodeExistsAsync(string code);
    Task<NetworkPointDto> CreateAsync(CreateNetworkPointRequest request);
    Task<NetworkPointDto?> UpdateAsync(string code, UpdateNetworkPointRequest request);
    Task<bool> DeleteAsync(string code);
}
