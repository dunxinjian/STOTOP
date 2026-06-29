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

public class KnowledgeService : IKnowledgeService
{
    private readonly STOTOPDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPointService _pointService;
    private readonly ILogger<KnowledgeService> _logger;

    public KnowledgeService(STOTOPDbContext db, IHttpContextAccessor httpContextAccessor, IPointService pointService, ILogger<KnowledgeService> logger)
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

    public async Task<ApiResult<PagedResult<KnowledgeListDto>>> GetPagedListAsync(KnowledgePagedRequest request)
    {
        var orgId = GetCurrentOrgId();
        var query = _db.Set<TmKnowledge>()
            .Where(k => k.FOrgId == orgId)
            .AsQueryable();

        if (request.Category.HasValue)
            query = query.Where(k => k.FCategory == request.Category.Value);

        if (request.AuthorId.HasValue)
            query = query.Where(k => k.FAuthorId == request.AuthorId.Value);

        if (request.Status.HasValue)
            query = query.Where(k => k.FStatus == request.Status.Value);

        if (request.IsPinned.HasValue)
            query = query.Where(k => k.FIsPinned == request.IsPinned.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(k => k.FTitle.Contains(keyword) ||
                (k.FContent != null && k.FContent.Contains(keyword)));
        }

        // 标签筛选：通过 TmTaskTag 关联查询（复用标签关联表）
        if (request.TagIds != null && request.TagIds.Count > 0)
        {
            var knowledgeIdsWithTags = _db.Set<TmTaskTag>()
                .Where(tt => request.TagIds.Contains(tt.FTagId))
                .Select(tt => tt.FTaskId)
                .Distinct();
            query = query.Where(k => knowledgeIdsWithTags.Contains(k.FID));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(k => k.FIsPinned)
            .ThenByDescending(k => k.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = await MapToListDtos(items);

        return ApiResult<PagedResult<KnowledgeListDto>>.Success(new PagedResult<KnowledgeListDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<KnowledgeDetailDto>> GetByIdAsync(long id)
    {
        var knowledge = await _db.Set<TmKnowledge>().FirstOrDefaultAsync(k => k.FID == id);
        if (knowledge == null)
            return ApiResult<KnowledgeDetailDto>.Fail("知识文章不存在");

        // 自动+1浏览数
        var tracked = await _db.Set<TmKnowledge>().AsTracking().FirstOrDefaultAsync(k => k.FID == id);
        if (tracked != null)
        {
            tracked.FViewCount += 1;
            await _db.SaveChangesAsync();
        }

        var currentUserId = GetCurrentUserId();

        var dto = await MapToDetailDto(knowledge, currentUserId);
        dto.ViewCount = (knowledge.FViewCount + 1); // 反映+1后的值

        return ApiResult<KnowledgeDetailDto>.Success(dto);
    }

    public async Task<ApiResult<KnowledgeDetailDto>> CreateAsync(CreateKnowledgeRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var orgId = GetCurrentOrgId();

        var knowledge = new TmKnowledge
        {
            FTitle = request.Title,
            FContent = request.Content,
            FCategory = request.Category,
            FOrgId = orgId,
            FAuthorId = currentUserId,
            FSourceReviewId = request.SourceReviewId,
            FSourceTaskId = request.SourceTaskId,
            FSourceProjectId = request.SourceProjectId,
            FStatus = 1, // 已发布
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

        // 知识文章发布后触发积分
        try
        {
            await _pointService.TriggerEventAsync(new PointEventDto
            {
                EventType = "knowledge.shared",
                UserId = currentUserId,
                OrgId = orgId,
                SourceModule = "task",
                EntityType = "knowledge",
                EntityId = knowledge.FID,
                Context = new Dictionary<string, object>
                {
                    { "Category", knowledge.FCategory },
                    { "Title", knowledge.FTitle }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "知识分享积分触发失败，KnowledgeId={KnowledgeId}", knowledge.FID);
        }

        return await GetByIdAsync(knowledge.FID);
    }

    public async Task<ApiResult<KnowledgeDetailDto>> UpdateAsync(long id, UpdateKnowledgeRequest request)
    {
        var knowledge = await _db.Set<TmKnowledge>()
            .AsTracking()
            .FirstOrDefaultAsync(k => k.FID == id);

        if (knowledge == null)
            return ApiResult<KnowledgeDetailDto>.Fail("知识文章不存在");

        knowledge.FTitle = request.Title;
        knowledge.FContent = request.Content;
        knowledge.FCategory = request.Category;
        knowledge.FStatus = request.Status;
        knowledge.FIsPinned = request.IsPinned;
        knowledge.FUpdateTime = DateTime.Now;

        // 更新标签：先删后增
        if (request.TagIds != null)
        {
            var existingTags = await _db.Set<TmTaskTag>()
                .Where(tt => tt.FTaskId == id)
                .ToListAsync();
            _db.Set<TmTaskTag>().RemoveRange(existingTags);

            foreach (var tagId in request.TagIds)
            {
                _db.Set<TmTaskTag>().Add(new TmTaskTag
                {
                    FTaskId = id,
                    FTagId = tagId
                });
            }
        }

        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var knowledge = await _db.Set<TmKnowledge>().FirstOrDefaultAsync(k => k.FID == id);
        if (knowledge == null)
            return ApiResult<bool>.Fail("知识文章不存在");

        // 删除关联数据
        var interactions = await _db.Set<TmKnowledgeInteraction>()
            .Where(i => i.FKnowledgeId == id).ToListAsync();
        _db.Set<TmKnowledgeInteraction>().RemoveRange(interactions);

        var comments = await _db.Set<TmKnowledgeComment>()
            .Where(c => c.FKnowledgeId == id).ToListAsync();
        _db.Set<TmKnowledgeComment>().RemoveRange(comments);

        var tags = await _db.Set<TmTaskTag>()
            .Where(tt => tt.FTaskId == id).ToListAsync();
        _db.Set<TmTaskTag>().RemoveRange(tags);

        // 删除关联附件（F关联类型=4 知识）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 4 && a.FRelationId == id).ToListAsync();
        _db.Set<TmAttachment>().RemoveRange(attachments);

        _db.Set<TmKnowledge>().Remove(knowledge);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<bool>> ToggleLikeAsync(long id)
    {
        var knowledge = await _db.Set<TmKnowledge>()
            .AsTracking()
            .FirstOrDefaultAsync(k => k.FID == id);
        if (knowledge == null)
            return ApiResult<bool>.Fail("知识文章不存在");

        var currentUserId = GetCurrentUserId();

        // 互动类型 0=点赞
        var existing = await _db.Set<TmKnowledgeInteraction>()
            .FirstOrDefaultAsync(i => i.FKnowledgeId == id && i.FUserId == currentUserId && i.FInteractionType == 0);

        if (existing != null)
        {
            // 取消点赞
            _db.Set<TmKnowledgeInteraction>().Remove(existing);
            knowledge.FLikeCount = Math.Max(0, knowledge.FLikeCount - 1);
            await _db.SaveChangesAsync();
            return ApiResult<bool>.Success(false, "已取消点赞");
        }
        else
        {
            // 点赞
            _db.Set<TmKnowledgeInteraction>().Add(new TmKnowledgeInteraction
            {
                FKnowledgeId = id,
                FUserId = currentUserId,
                FInteractionType = 0,
                FCreateTime = DateTime.Now
            });
            knowledge.FLikeCount += 1;
            await _db.SaveChangesAsync();
            return ApiResult<bool>.Success(true, "点赞成功");
        }
    }

    public async Task<ApiResult<bool>> ToggleCollectAsync(long id)
    {
        var knowledge = await _db.Set<TmKnowledge>()
            .AsTracking()
            .FirstOrDefaultAsync(k => k.FID == id);
        if (knowledge == null)
            return ApiResult<bool>.Fail("知识文章不存在");

        var currentUserId = GetCurrentUserId();

        // 互动类型 1=收藏
        var existing = await _db.Set<TmKnowledgeInteraction>()
            .FirstOrDefaultAsync(i => i.FKnowledgeId == id && i.FUserId == currentUserId && i.FInteractionType == 1);

        if (existing != null)
        {
            // 取消收藏
            _db.Set<TmKnowledgeInteraction>().Remove(existing);
            knowledge.FCollectCount = Math.Max(0, knowledge.FCollectCount - 1);
            await _db.SaveChangesAsync();
            return ApiResult<bool>.Success(false, "已取消收藏");
        }
        else
        {
            // 收藏
            _db.Set<TmKnowledgeInteraction>().Add(new TmKnowledgeInteraction
            {
                FKnowledgeId = id,
                FUserId = currentUserId,
                FInteractionType = 1,
                FCreateTime = DateTime.Now
            });
            knowledge.FCollectCount += 1;
            await _db.SaveChangesAsync();
            return ApiResult<bool>.Success(true, "收藏成功");
        }
    }

    public async Task<ApiResult<List<KnowledgeCommentDto>>> GetCommentsAsync(long knowledgeId)
    {
        var comments = await _db.Set<TmKnowledgeComment>()
            .Where(c => c.FKnowledgeId == knowledgeId)
            .OrderBy(c => c.FCreateTime)
            .ToListAsync();

        var userIds = comments.Select(c => c.FUserId).Distinct().ToList();
        var users = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID, u => u.FName);

        var allDtos = comments.Select(c => new KnowledgeCommentDto
        {
            Id = c.FID,
            KnowledgeId = c.FKnowledgeId,
            UserId = c.FUserId,
            UserName = userDict.GetValueOrDefault(c.FUserId),
            Content = c.FContent,
            ParentCommentId = c.FParentCommentId,
            CreateTime = c.FCreateTime
        }).ToList();

        // 构建树形结构
        var topLevel = allDtos.Where(c => c.ParentCommentId == 0).ToList();
        var childMap = allDtos.Where(c => c.ParentCommentId > 0)
            .GroupBy(c => c.ParentCommentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var comment in topLevel)
        {
            if (childMap.TryGetValue(comment.Id, out var replies))
                comment.Replies = replies;
        }

        return ApiResult<List<KnowledgeCommentDto>>.Success(topLevel);
    }

    public async Task<ApiResult<KnowledgeCommentDto>> CreateCommentAsync(long knowledgeId, CreateKnowledgeCommentRequest request)
    {
        var exists = await _db.Set<TmKnowledge>().AnyAsync(k => k.FID == knowledgeId);
        if (!exists)
            return ApiResult<KnowledgeCommentDto>.Fail("知识文章不存在");

        var currentUserId = GetCurrentUserId();

        var comment = new TmKnowledgeComment
        {
            FKnowledgeId = knowledgeId,
            FUserId = currentUserId,
            FContent = request.Content,
            FParentCommentId = request.ParentCommentId,
            FCreateTime = DateTime.Now
        };

        _db.Set<TmKnowledgeComment>().Add(comment);
        await _db.SaveChangesAsync();

        // 获取用户名
        var userName = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => u.FID == currentUserId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new KnowledgeCommentDto
        {
            Id = comment.FID,
            KnowledgeId = comment.FKnowledgeId,
            UserId = comment.FUserId,
            UserName = userName,
            Content = comment.FContent,
            ParentCommentId = comment.FParentCommentId,
            CreateTime = comment.FCreateTime
        };

        return ApiResult<KnowledgeCommentDto>.Success(dto);
    }

    public async Task<ApiResult<PagedResult<KnowledgeListDto>>> GetMyCollectionsAsync()
    {
        var currentUserId = GetCurrentUserId();

        // 互动类型 1=收藏
        var collectedIds = await _db.Set<TmKnowledgeInteraction>()
            .Where(i => i.FUserId == currentUserId && i.FInteractionType == 1)
            .OrderByDescending(i => i.FCreateTime)
            .Select(i => i.FKnowledgeId)
            .ToListAsync();

        var knowledges = await _db.Set<TmKnowledge>()
            .Where(k => collectedIds.Contains(k.FID))
            .ToListAsync();

        // 保持收藏顺序
        var ordered = collectedIds
            .Select(id => knowledges.FirstOrDefault(k => k.FID == id))
            .Where(k => k != null)
            .Cast<TmKnowledge>()
            .ToList();

        var dtos = await MapToListDtos(ordered);

        return ApiResult<PagedResult<KnowledgeListDto>>.Success(new PagedResult<KnowledgeListDto>
        {
            Items = dtos,
            Total = dtos.Count,
            PageIndex = 1,
            PageSize = dtos.Count
        });
    }

    public async Task<ApiResult<List<KnowledgeListDto>>> GetHotAsync()
    {
        var orgId = GetCurrentOrgId();

        var items = await _db.Set<TmKnowledge>()
            .Where(k => k.FOrgId == orgId && k.FStatus == 1)
            .OrderByDescending(k => k.FViewCount + k.FLikeCount * 3)
            .Take(20)
            .ToListAsync();

        var dtos = await MapToListDtos(items);

        return ApiResult<List<KnowledgeListDto>>.Success(dtos);
    }

    #region Private Helpers

    private async Task<List<KnowledgeListDto>> MapToListDtos(List<TmKnowledge> items)
    {
        if (items.Count == 0) return new List<KnowledgeListDto>();

        var authorIds = items.Select(k => k.FAuthorId).Distinct().ToList();
        var authors = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => authorIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var authorDict = authors.ToDictionary(u => u.FID, u => u.FName);

        // 获取标签
        var knowledgeIds = items.Select(k => k.FID).ToList();
        var tagRelations = await _db.Set<TmTaskTag>()
            .Where(tt => knowledgeIds.Contains(tt.FTaskId))
            .ToListAsync();
        var tagIds = tagRelations.Select(tr => tr.FTagId).Distinct().ToList();
        var tags = await _db.Set<TmTag>()
            .Where(t => tagIds.Contains(t.FID))
            .ToListAsync();
        var tagDict = tags.ToDictionary(t => t.FID);

        var tagsByKnowledge = tagRelations
            .GroupBy(tr => tr.FTaskId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(tr => tagDict.TryGetValue(tr.FTagId, out var tag)
                    ? new TagSimpleDto { Id = tag.FID, Name = tag.FName, Color = tag.FColor }
                    : null)
                    .Where(t => t != null)
                    .Cast<TagSimpleDto>()
                    .ToList()
            );

        return items.Select(k => new KnowledgeListDto
        {
            Id = k.FID,
            UID = k.FUID,
            Title = k.FTitle,
            Category = k.FCategory,
            OrgId = k.FOrgId,
            AuthorId = k.FAuthorId,
            AuthorName = authorDict.GetValueOrDefault(k.FAuthorId),
            SourceReviewId = k.FSourceReviewId,
            ViewCount = k.FViewCount,
            LikeCount = k.FLikeCount,
            CollectCount = k.FCollectCount,
            Status = k.FStatus,
            IsPinned = k.FIsPinned,
            CreateTime = k.FCreateTime,
            UpdateTime = k.FUpdateTime,
            Tags = tagsByKnowledge.GetValueOrDefault(k.FID) ?? new List<TagSimpleDto>()
        }).ToList();
    }

    private async Task<KnowledgeDetailDto> MapToDetailDto(TmKnowledge knowledge, long currentUserId)
    {
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
            ViewCount = knowledge.FViewCount,
            LikeCount = knowledge.FLikeCount,
            CollectCount = knowledge.FCollectCount,
            Status = knowledge.FStatus,
            IsPinned = knowledge.FIsPinned,
            CreateTime = knowledge.FCreateTime,
            UpdateTime = knowledge.FUpdateTime
        };

        // 作者名
        var author = await _db.Set<STOTOP.Module.System.Entities.SysUser>()
            .Where(u => u.FID == knowledge.FAuthorId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.AuthorName = author?.FName;

        // 当前用户是否已点赞/收藏
        dto.HasLiked = await _db.Set<TmKnowledgeInteraction>()
            .AnyAsync(i => i.FKnowledgeId == knowledge.FID && i.FUserId == currentUserId && i.FInteractionType == 0);
        dto.HasCollected = await _db.Set<TmKnowledgeInteraction>()
            .AnyAsync(i => i.FKnowledgeId == knowledge.FID && i.FUserId == currentUserId && i.FInteractionType == 1);

        // 标签
        var tagRelations = await _db.Set<TmTaskTag>()
            .Where(tt => tt.FTaskId == knowledge.FID)
            .ToListAsync();
        if (tagRelations.Count > 0)
        {
            var tagIds = tagRelations.Select(tr => tr.FTagId).ToList();
            dto.Tags = await _db.Set<TmTag>()
                .Where(t => tagIds.Contains(t.FID))
                .Select(t => new TagSimpleDto { Id = t.FID, Name = t.FName, Color = t.FColor })
                .ToListAsync();
        }

        // 附件（F关联类型=4 知识）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 4 && a.FRelationId == knowledge.FID)
            .ToListAsync();
        if (attachments.Count > 0)
        {
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
        }

        // 评论列表
        var commentsResult = await GetCommentsAsync(knowledge.FID);
        if (commentsResult.Code == 200 && commentsResult.Data != null)
            dto.Comments = commentsResult.Data;

        return dto;
    }

    #endregion
}
