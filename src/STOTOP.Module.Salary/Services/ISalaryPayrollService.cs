using STOTOP.Core.Models;
using STOTOP.Module.Salary.Dtos;

namespace STOTOP.Module.Salary.Services;

public interface ISalaryPayrollService
{
    Task<ApiResult<List<SalaryPayrollDto>>> GetListAsync(long orgId, string? period = null, int? status = null);
    Task<ApiResult<SalaryPayrollDto>> GetDetailAsync(long orgId, long id);
    Task<ApiResult<List<SalaryPayrollDto>>> GetMyPayrollAsync(long orgId, long userId, int count = 12);
    Task<ApiResult> AuditAsync(long orgId, long id, long auditorId);
    Task<ApiResult> ReleaseAsync(long orgId, long id);
    Task<ApiResult> RecalcAsync(long orgId, RecalcPayrollRequest request);
}
