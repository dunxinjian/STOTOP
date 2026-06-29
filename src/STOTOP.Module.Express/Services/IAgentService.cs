using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IAgentService
{
    Task<PagedResult<AgentDto>> GetListAsync(AgentQueryRequest request);
    Task<AgentDto?> GetByIdAsync(string code);
    Task<AgentDto> CreateAsync(CreateAgentRequest request);
    Task<AgentDto?> UpdateAsync(string code, UpdateAgentRequest request);
    Task<bool> DeleteAsync(string code);
}
