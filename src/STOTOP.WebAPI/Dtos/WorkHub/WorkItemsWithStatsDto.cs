using STOTOP.Core.Models;

namespace STOTOP.WebAPI.Dtos.WorkHub;

/// <summary>
/// 合并响应：工作项分页列表 + 统计信息
/// </summary>
public class WorkItemsWithStatsDto
{
    public PagedResult<WorkItemDto> Items { get; set; } = null!;
    public WorkHubStatsDto Stats { get; set; } = null!;
}
