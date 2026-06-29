using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using System.Security.Claims;

namespace STOTOP.Module.Task.Services;

public class ReviewService : IReviewService
{
    private readonly STOTOPDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPointService _pointService;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(STOTOPDbContext db, IHttpContextAccessor httpContextAccessor, IPointService pointService, ILogger<ReviewService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _pointService = pointService;
        _logger = logger;
    }

    private long GetCurrentUserId()
    {
        var userIdStr = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var userId)) return userId;
        return 0;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    public async Task<ApiResult<PagedResult<ReviewListDto>>> GetPagedListAsync(ReviewPagedRequest request)
    {
        var orgId = GetCurrentOrgId();
        var query = _db.Set<TmReviewRecord>()
            .Where(r => r.FOrgId == orgId)
            .AsQueryable();

        if (request.RelationType.HasValue)
            query = query.Where(r => r.FRelationType == request.RelationType.Value);

        if (request.RelationId.HasValue)
            query = query.Where(r => r.FRelationId == request.RelationId.Value);

        if (request.ReviewerId.HasValue)
            query = query.Where(r => r.FReviewerId == request.ReviewerId.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.FStatus == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(r => r.FTitle.Contains(keyword));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 获取复盘人姓名
        var reviewerIds = items.Select(r => r.FReviewerId).Distinct().ToList();
        var reviewerNames = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => reviewerIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var nameDict = reviewerNames.ToDictionary(u => u.FID, u => u.FName);

        var dtos = items.Select(r => new ReviewListDto
        {
            Id = r.FID,
            UID = r.FUID,
            RelationType = r.FRelationType,
            RelationId = r.FRelationId,
            OrgId = r.FOrgId,
            Title = r.FTitle,
            ReviewerId = r.FReviewerId,
            ReviewerName = nameDict.GetValueOrDefault(r.FReviewerId),
            Status = r.FStatus,
            CreateTime = r.FCreateTime,
            UpdateTime = r.FUpdateTime
        }).ToList();

        return ApiResult<PagedResult<ReviewListDto>>.Success(new PagedResult<ReviewListDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<ReviewDetailDto>> GetByIdAsync(long id)
    {
        var review = await _db.Set<TmReviewRecord>().FirstOrDefaultAsync(r => r.FID == id);
        if (review == null)
            return ApiResult<ReviewDetailDto>.Fail("复盘记录不存在");

        var dto = await MapToDetailDto(review);
        return ApiResult<ReviewDetailDto>.Success(dto);
    }

    public async Task<ApiResult<ReviewDetailDto>> CreateAsync(CreateReviewRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var orgId = GetCurrentOrgId();

        var review = new TmReviewRecord
        {
            FRelationType = request.RelationType,
            FRelationId = request.RelationId,
            FOrgId = orgId,
            FTitle = request.Title,
            FWentWell = request.WentWell,
            FToImprove = request.ToImprove,
            FLessonsLearned = request.LessonsLearned,
            FActionPlan = request.ActionPlan,
            FReviewerId = currentUserId,
            FParticipantIds = request.ParticipantIds != null
                ? string.Join(",", request.ParticipantIds)
                : null,
            FStatus = 0, // 草稿
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmReviewRecord>().Add(review);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(review.FID);
    }

    public async Task<ApiResult<ReviewDetailDto>> UpdateAsync(long id, UpdateReviewRequest request)
    {
        var review = await _db.Set<TmReviewRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (review == null)
            return ApiResult<ReviewDetailDto>.Fail("复盘记录不存在");

        review.FTitle = request.Title;
        review.FWentWell = request.WentWell;
        review.FToImprove = request.ToImprove;
        review.FLessonsLearned = request.LessonsLearned;
        review.FActionPlan = request.ActionPlan;
        review.FParticipantIds = request.ParticipantIds != null
            ? string.Join(",", request.ParticipantIds)
            : null;
        review.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<bool>> PublishAsync(long id)
    {
        var review = await _db.Set<TmReviewRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (review == null)
            return ApiResult<bool>.Fail("复盘记录不存在");

        if (review.FStatus != 0)
            return ApiResult<bool>.Fail("仅草稿状态的复盘可以发布");

        review.FStatus = 1; // 已发布
        review.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        // 复盘发布后触发积分
        try
        {
            await _pointService.TriggerEventAsync(new PointEventDto
            {
                EventType = "review.published",
                UserId = review.FReviewerId,
                OrgId = review.FOrgId,
                SourceModule = "task",
                EntityType = "review",
                EntityId = review.FID,
                Context = new Dictionary<string, object>
                {
                    { "RelationType", review.FRelationType },
                    { "RelationId", review.FRelationId }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "复盘发布积分触发失败，ReviewId={ReviewId}", review.FID);
        }

        return ApiResult<bool>.Success(true, "发布成功");
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var review = await _db.Set<TmReviewRecord>().FirstOrDefaultAsync(r => r.FID == id);
        if (review == null)
            return ApiResult<bool>.Fail("复盘记录不存在");

        if (review.FStatus != 0)
            return ApiResult<bool>.Fail("仅草稿状态的复盘可以删除");

        // 删除关联附件（F关联类型=3 复盘）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 3 && a.FRelationId == id)
            .ToListAsync();
        _db.Set<TmAttachment>().RemoveRange(attachments);

        _db.Set<TmReviewRecord>().Remove(review);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<KnowledgeDetailDto>> ExtractKnowledgeAsync(long id, ExtractKnowledgeRequest request)
    {
        var review = await _db.Set<TmReviewRecord>().FirstOrDefaultAsync(r => r.FID == id);
        if (review == null)
            return ApiResult<KnowledgeDetailDto>.Fail("复盘记录不存在");

        var currentUserId = GetCurrentUserId();

        // 将复盘的「经验方法」字段自动填充到知识文章内容
        var knowledge = new TmKnowledge
        {
            FTitle = request.Title,
            FContent = review.FLessonsLearned,
            FCategory = request.Category,
            FOrgId = review.FOrgId,
            FAuthorId = currentUserId,
            FSourceReviewId = review.FID,
            FSourceTaskId = review.FRelationType == 0 ? review.FRelationId : (long?)null,   // 0=任务
            FSourceProjectId = review.FRelationType == 1 ? review.FRelationId : (long?)null, // 1=项目
            FStatus = 1, // 直接发布
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmKnowledge>().Add(knowledge);
        await _db.SaveChangesAsync();

        // 关联标签
        if (request.TagIds != null && request.TagIds.Count > 0)
        {
            foreach (var tagId in request.TagIds)
            {
                _db.Set<TmTaskTag>().Add(new TmTaskTag
                {
                    FTaskId = knowledge.FID,
                    FTagId = tagId
                });
            }
            await _db.SaveChangesAsync();
        }

        // 构建返回DTO
        var dto = new KnowledgeDetailDto
        {
            Id = knowledge.FID,
            UID = knowledge.FUID,
            Title = knowledge.FTitle,
            Content = knowledge.FContent,
            Category = knowledge.FCategory,
            OrgId = knowledge.FOrgId,
            AuthorId = knowledge.FAuthorId,
            SourceReviewId = knowledge.FSourceReviewId,
            SourceTaskId = knowledge.FSourceTaskId,
            SourceProjectId = knowledge.FSourceProjectId,
            ViewCount = 0,
            LikeCount = 0,
            CollectCount = 0,
            Status = knowledge.FStatus,
            IsPinned = false,
            CreateTime = knowledge.FCreateTime,
            UpdateTime = knowledge.FUpdateTime
        };

        return ApiResult<KnowledgeDetailDto>.Success(dto, "知识提炼成功");
    }

    public async Task<ApiResult<List<ReviewListDto>>> GetByEntityAsync(int relationType, long entityId)
    {
        var reviews = await _db.Set<TmReviewRecord>()
            .Where(r => r.FRelationType == relationType && r.FRelationId == entityId)
            .OrderByDescending(r => r.FCreateTime)
            .ToListAsync();

        var reviewerIds = reviews.Select(r => r.FReviewerId).Distinct().ToList();
        var reviewerNames = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => reviewerIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var nameDict = reviewerNames.ToDictionary(u => u.FID, u => u.FName);

        var dtos = reviews.Select(r => new ReviewListDto
        {
            Id = r.FID,
            UID = r.FUID,
            RelationType = r.FRelationType,
            RelationId = r.FRelationId,
            OrgId = r.FOrgId,
            Title = r.FTitle,
            ReviewerId = r.FReviewerId,
            ReviewerName = nameDict.GetValueOrDefault(r.FReviewerId),
            Status = r.FStatus,
            CreateTime = r.FCreateTime,
            UpdateTime = r.FUpdateTime
        }).ToList();

        return ApiResult<List<ReviewListDto>>.Success(dtos);
    }

    private async Task<ReviewDetailDto> MapToDetailDto(TmReviewRecord review)
    {
        var dto = new ReviewDetailDto
        {
            Id = review.FID,
            UID = review.FUID,
            RelationType = review.FRelationType,
            RelationId = review.FRelationId,
            OrgId = review.FOrgId,
            Title = review.FTitle,
            WentWell = review.FWentWell,
            ToImprove = review.FToImprove,
            LessonsLearned = review.FLessonsLearned,
            ActionPlan = review.FActionPlan,
            ReviewerId = review.FReviewerId,
            ParticipantIds = review.FParticipantIds,
            Status = review.FStatus,
            CreateTime = review.FCreateTime,
            UpdateTime = review.FUpdateTime
        };

        // 获取复盘人姓名
        var reviewer = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => u.FID == review.FReviewerId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.ReviewerName = reviewer?.FName;

        // 解析参与者
        if (!string.IsNullOrWhiteSpace(review.FParticipantIds))
        {
            var participantIds = review.FParticipantIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var pid) ? pid : 0)
                .Where(pid => pid > 0)
                .ToList();

            if (participantIds.Count > 0)
            {
                var participants = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
                    .Where(u => participantIds.Contains(u.FID))
                    .Select(u => new ParticipantDto { UserId = u.FID, UserName = u.FName })
                    .ToListAsync();
                dto.Participants = participants;
            }
        }

        // 获取附件（F关联类型=3 复盘）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 3 && a.FRelationId == review.FID)
            .ToListAsync();

        var attachUserIds = attachments.Select(a => a.FUserId).Distinct().ToList();
        var attachUsers = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => attachUserIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var attachUserDict = attachUsers.ToDictionary(u => u.FID, u => u.FName);

        dto.Attachments = attachments.Select(a => new AttachmentListDto
        {
            Id = a.FID,
            RelationType = a.FRelationType,
            RelationId = a.FRelationId,
            UserId = a.FUserId,
            UserName = attachUserDict.GetValueOrDefault(a.FUserId),
            OriginalFileName = a.FOriginalFileName,
            StoragePath = a.FStoragePath,
            FileSize = a.FFileSize,
            FileType = a.FFileType,
            CreateTime = a.FCreateTime
        }).ToList();

        return dto;
    }
}
