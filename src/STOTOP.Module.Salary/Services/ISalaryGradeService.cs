using STOTOP.Core.Models;
using STOTOP.Module.Salary.Dtos;

namespace STOTOP.Module.Salary.Services;

public interface ISalaryGradeService
{
    Task<ApiResult<List<SalaryGradeDto>>> GetListAsync(long orgId);
    Task<ApiResult<SalaryGradeDto>> CreateAsync(long orgId, CreateSalaryGradeRequest request);
    Task<ApiResult<SalaryGradeDto>> UpdateAsync(long orgId, long id, UpdateSalaryGradeRequest request);
    Task<ApiResult> EnableAsync(long orgId, long id);
}
