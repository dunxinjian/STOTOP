using STOTOP.Core.Models;
using STOTOP.Module.HR.Dtos;

namespace STOTOP.Module.HR.Services;

public interface IEmployeeService
{
    Task<ApiResult<PagedResult<EmployeeDto>>> GetPagedListAsync(EmployeePagedRequest request);
    Task<ApiResult<EmployeeDto>> GetByIdAsync(long id);
    Task<ApiResult<EmployeeDto>> CreateAsync(CreateEmployeeRequest request);
    Task<ApiResult<EmployeeDto>> UpdateAsync(long id, UpdateEmployeeRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<EmployeeDto?>> GetByUserIdAsync(long userId);
    Task<ApiResult<List<UserSimpleDto>>> SearchAvailableUsersAsync(string? keyword);
}
