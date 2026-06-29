using STOTOP.Module.Workflow.DTOs;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IChainService
{
    // 创建新链路（返回 ChainId）
    Task<string> CreateChainAsync(long orgId, string title, long creatorId);

    // 获取链路时间线（WorkItem 列表 + 事件历史）
    Task<ChainTimelineDto> GetTimelineAsync(string chainId);

    // 评论
    Task<ChainCommentDto> AddCommentAsync(string chainId, long authorId, string authorName, string content, long? workItemId = null, long? replyToId = null);
    Task<List<ChainCommentDto>> GetCommentsAsync(string chainId, int page = 1, int pageSize = 50);
    Task DeleteCommentAsync(long commentId, long operatorId);

    // 关注
    Task FollowAsync(string chainId, long userId, string userName);
    Task UnfollowAsync(string chainId, long userId);
    Task<List<ChainFollowerDto>> GetFollowersAsync(string chainId);
    Task<bool> IsFollowingAsync(string chainId, long userId);

    // 自动关注（参与链路的人自动关注）
    Task AutoFollowParticipantsAsync(string chainId);
}
