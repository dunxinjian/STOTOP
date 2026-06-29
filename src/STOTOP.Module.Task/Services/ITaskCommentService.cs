using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface ITaskCommentService
{
    /// <summary>
    /// 评论分页列表（含表情统计汇总、附件列表）
    /// </summary>
    Task<ApiResult<PagedResult<TaskCommentListDto>>> GetPagedListAsync(long taskId, CommentPagedRequest query);

    /// <summary>
    /// 添加评论（支持回复，记录活动日志）
    /// </summary>
    Task<ApiResult<TaskCommentListDto>> CreateAsync(long taskId, CreateTaskCommentRequest request, long operatorId);

    /// <summary>
    /// 编辑评论
    /// </summary>
    Task<ApiResult<TaskCommentListDto>> UpdateAsync(long taskId, long commentId, UpdateTaskCommentRequest request);

    /// <summary>
    /// 删除评论
    /// </summary>
    Task<ApiResult<bool>> DeleteAsync(long taskId, long commentId);

    /// <summary>
    /// 添加/移除表情回应（存在则删，不存在则加）
    /// </summary>
    Task<ApiResult<List<ReactionSummaryDto>>> ToggleReactionAsync(long taskId, long commentId, ToggleReactionRequest request, long userId);

    /// <summary>
    /// 移除指定表情
    /// </summary>
    Task<ApiResult<bool>> RemoveReactionAsync(long taskId, long commentId, string emoji, long userId);
}
