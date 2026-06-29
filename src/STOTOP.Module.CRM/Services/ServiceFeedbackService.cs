using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class ServiceFeedbackService : IServiceFeedbackService
{
    private readonly IRepository<CrmServiceFeedback> _feedbackRepository;

    public ServiceFeedbackService(IRepository<CrmServiceFeedback> feedbackRepository)
    {
        _feedbackRepository = feedbackRepository;
    }

    public async Task<PagedResult<ServiceFeedbackListItemDto>> GetFeedbacksAsync(ServiceFeedbackQueryRequest request)
    {
        var query = _feedbackRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(f => f.FCustomerId == request.CustomerId);

        if (request.SubmitterId.HasValue)
            query = query.Where(f => f.FSubmitterId == request.SubmitterId.Value);

        if (request.Category.HasValue)
            query = query.Where(f => f.FCategory == request.Category.Value);

        if (request.Status.HasValue)
            query = query.Where(f => f.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(f => f.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ServiceFeedbackListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ServiceFeedbackDto?> GetFeedbackByIdAsync(long id)
    {
        var entity = await _feedbackRepository.Query()
            .FirstOrDefaultAsync(f => f.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ServiceFeedbackDto> CreateFeedbackAsync(CreateServiceFeedbackRequest request)
    {
        var entity = new CrmServiceFeedback
        {
            FSubmitterId = request.SubmitterId,
            FOrgId = request.OrgId ?? 0,
            FCustomerId = request.CustomerId,
            FOrderId = request.OrderId,
            FCategory = request.Category,
            FTitle = request.Title,
            FDescription = request.Description,
            FSuggestion = request.Suggestion,
            FAttachments = request.Attachments,
            FStatus = 0, // 待审阅
            FCreatedTime = DateTime.Now
        };

        await _feedbackRepository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ServiceFeedbackDto?> UpdateFeedbackAsync(long id, UpdateServiceFeedbackRequest request)
    {
        var entity = await _feedbackRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(f => f.FID == id);

        if (entity == null) return null;

        if (entity.FStatus > 1) // 改善中及之后不允许修改
            throw new InvalidOperationException("当前状态不允许修改反馈信息");

        entity.FCategory = request.Category;
        entity.FTitle = request.Title;
        entity.FDescription = request.Description;
        entity.FSuggestion = request.Suggestion;
        entity.FAttachments = request.Attachments;
        entity.FUpdatedTime = DateTime.Now;

        await _feedbackRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteFeedbackAsync(long id)
    {
        var entity = await _feedbackRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _feedbackRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> HandleFeedbackAsync(long id, HandleFeedbackRequest request)
    {
        var entity = await _feedbackRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(f => f.FID == id);

        if (entity == null) return false;

        // 状态流转验证：待审阅(0)→已受理(1)→改善中(2)→已落实(3)/已驳回(4)
        var validTransitions = new Dictionary<int, int[]>
        {
            { 0, new[] { 1, 4 } },       // 待审阅 -> 已受理 or 已驳回
            { 1, new[] { 2, 4 } },       // 已受理 -> 改善中 or 已驳回
            { 2, new[] { 3, 4 } }        // 改善中 -> 已落实 or 已驳回
        };

        if (!validTransitions.ContainsKey(entity.FStatus) || !validTransitions[entity.FStatus].Contains(request.NewStatus))
        {
            throw new InvalidOperationException($"不允许从状态 {entity.FStatus} 流转到 {request.NewStatus}");
        }

        entity.FStatus = request.NewStatus;
        entity.FHandlerId = request.HandlerId;
        entity.FHandleResult = request.HandleResult;
        entity.FUpdatedTime = DateTime.Now;

        await _feedbackRepository.UpdateAsync(entity);
        return true;
    }

    #region Mapping

    private static ServiceFeedbackDto MapToDto(CrmServiceFeedback entity)
    {
        return new ServiceFeedbackDto
        {
            Id = entity.FID,
            SubmitterId = entity.FSubmitterId,
            OrgId = entity.FOrgId,
            CustomerId = entity.FCustomerId,
            OrderId = entity.FOrderId,
            Category = entity.FCategory,
            Title = entity.FTitle,
            Description = entity.FDescription,
            Suggestion = entity.FSuggestion,
            Attachments = entity.FAttachments,
            Status = entity.FStatus,
            HandlerId = entity.FHandlerId,
            HandleResult = entity.FHandleResult,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static ServiceFeedbackListItemDto MapToListItemDto(CrmServiceFeedback entity)
    {
        return new ServiceFeedbackListItemDto
        {
            Id = entity.FID,
            SubmitterId = entity.FSubmitterId,
            CustomerId = entity.FCustomerId,
            Category = entity.FCategory,
            Title = entity.FTitle,
            Status = entity.FStatus,
            HandlerId = entity.FHandlerId,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
