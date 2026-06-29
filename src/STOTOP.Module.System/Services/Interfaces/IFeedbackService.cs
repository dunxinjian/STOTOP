using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IFeedbackService
{
    Task<PagedResult<FeedbackCardDto>> GetPagedAsync(FeedbackQueryRequest request, long currentUserId);
    Task<List<FeedbackCardDto>> GetBoardAsync(FeedbackQueryRequest request, long currentUserId);
    Task<List<FeedbackStatusCountDto>> GetStatusCountsAsync(FeedbackQueryRequest request, long currentUserId);
    Task<FeedbackDetailDto?> GetByIdAsync(long id);
    Task<FeedbackDetailDto> CreateAsync(CreateFeedbackRequest request, long submitterId, long orgId);
    Task<FeedbackDetailDto?> UpdateAsync(long id, UpdateFeedbackRequest request, long actorId);
    Task<FeedbackDetailDto?> AssignAsync(long id, AssignFeedbackRequest request, long actorId);
    Task<FeedbackDetailDto?> TransitionAsync(long id, TransitionFeedbackRequest request, long actorId);
    Task<FeedbackActivityDto?> AddCommentAsync(long id, AddFeedbackCommentRequest request, long actorId);
}
