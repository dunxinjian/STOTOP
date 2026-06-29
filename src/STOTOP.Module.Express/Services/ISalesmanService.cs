using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface ISalesmanService
{
    Task<PagedResult<SalesmanDto>> GetListAsync(SalesmanQueryRequest request);
    Task<SalesmanDto?> GetByNoAsync(string employeeNo);
    Task<SalesmanDto> CreateAsync(CreateSalesmanRequest request);
    Task<SalesmanDto?> UpdateAsync(string employeeNo, UpdateSalesmanRequest request);
    Task<bool> DeleteAsync(string employeeNo);
    /// <summary>获取可选的HR员工候选人列表（本网点岗位为"业务员"的人员）</summary>
    Task<List<HrEmployeeCandidateDto>> GetCandidatesAsync(string networkPointCode);
}
