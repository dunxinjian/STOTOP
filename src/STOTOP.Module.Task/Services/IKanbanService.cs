using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IKanbanService
{
    Task<ApiResult<KanbanDataDto>> GetKanbanDataAsync(KanbanQueryRequest query, long orgId, long currentUserId, bool isAdmin);
    Task<ApiResult<bool>> MoveAsync(KanbanMoveRequest request);
}
