using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface IApprovalConfigService
{
    Task<PagedResult<InsApprovalConfigDto>> GetListAsync(long orgId, int pageIndex = 1, int pageSize = 20);
    Task<InsApprovalConfigDto> CreateAsync(long orgId, CreateInsApprovalConfigRequest request);
    Task<InsApprovalConfigDto?> UpdateAsync(long id, UpdateInsApprovalConfigRequest request);
    Task ToggleStatusAsync(long id);
    Task ReorderAsync(List<ApprovalStepOrderItem> items);
}

/// <summary>
/// 环节排序项
/// </summary>
public class ApprovalStepOrderItem
{
    public long Id { get; set; }
    public int StepOrder { get; set; }
}
