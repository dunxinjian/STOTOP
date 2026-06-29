using STOTOP.Core.Models;
using STOTOP.Module.Salary.Dtos;

namespace STOTOP.Module.Salary.Services;

public interface ISalaryArchiveService
{
    Task<ApiResult<List<SalaryArchiveDto>>> GetListAsync(long orgId);
    Task<ApiResult<SalaryArchiveDto>> GetByEmployeeAsync(long orgId, long employeeId);
    Task<ApiResult<SalaryArchiveDto>> CreateAsync(long orgId, CreateSalaryArchiveRequest request);
    Task<ApiResult<SalaryArchiveDto>> UpdateAsync(long orgId, long id, UpdateSalaryArchiveRequest request);
    Task<ApiResult> AdjustGradeAsync(long orgId, long employeeId, long newGradeId);
}
