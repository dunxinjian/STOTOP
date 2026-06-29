using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class TaskCommentService : ITaskCommentService
{
    private readonly STOTOPDbContext _db;

    public TaskCommentService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<TaskCommentListDto>>> GetPagedListAsync(long taskId, CommentPagedRequest query)
    {
        var q = _db.Set<TmTaskComment>()
            .Where(c => c.FTaskId == taskId && c.FParentCommentId == 0);

        if (query.Type.HasValue)
            q = q.Where(c => c.FType == query.Type.Value);

        var total = await q.CountAsync();

        var comments = await q
            .OrderByDescending(c => c.FCreateTime)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var commentIds = comments.Select(c => c.FID).ToList();

        // 加载回复
        var replies = await _db.Set<TmTaskComment>()
            .Where(c => commentIds.Contains(c.FParentCommentId))
            .OrderBy(c => c.FCreateTime)
            .ToListAsync();

        var allCommentIds = commentIds.Concat(replies.Select(r => r.FID)).ToList();

        // 加载表情回应
        var reactions = await _db.Set<TmCommentReaction>()
            .Where(r => allCommentIds.Contains(r.FCommentId))
            .ToListAsync();

        // 加载附件（RelationType=1 评论）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 1 && allCommentIds.Contains(a.FRelationId))
            .ToListAsync();

        // 获取用户名
        var userIds = comments.Select(c => c.FUserId)
            .Concat(replies.Select(r => r.FUserId))
            .Concat(attachments.Select(a => a.FUserId))
            .Distinct().ToList();
        var userDict = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToDictionaryAsync(u => u.FID, u => u.FName);

        var items = comments.Select(c => MapToDto(c, replies, reactions, attachments, userDict)).ToList();

        return ApiResult<PagedResult<TaskCommentListDto>>.Success(new PagedResult<TaskCommentListDto>
        {
            Items = items,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        });
    }

    public async Task<ApiResult<TaskCommentListDto>> CreateAsync(long taskId, CreateTaskCommentRequest request, long operatorId)
    {
        var taskExists = await _db.Set<TmTask>().AnyAsync(t => t.FID == taskId);
        if (!taskExists)
            return ApiResult<TaskCommentListDto>.Fail("任务不存在");

        if (request.ParentCommentId > 0)
        {
            var parentExists = await _db.Set<TmTaskComment>()
                .AnyAsync(c => c.FID == request.ParentCommentId && c.FTaskId == taskId);
            if (!parentExists)
                return ApiResult<TaskCommentListDto>.Fail("父评论不存在");
        }

        var comment = new TmTaskComment
        {
            FTaskId = taskId,
            FUserId = operatorId,
            FContent = request.Content,
            FType = request.Type,
            FParentCommentId = request.ParentCommentId,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmTaskComment>().Add(comment);

        // 记录活动日志
        _db.Set<TmActivityLog>().Add(new TmActivityLog
        {
            FTaskId = taskId,
            FActionType = 20, // 评论
            FNewValue = request.Content.Length > 100 ? request.Content[..100] + "..." : request.Content,
            FOperatorId = operatorId,
            FRemark = "添加评论",
            FCreateTime = DateTime.Now
        });

        await _db.SaveChangesAsync();

        var userName = await _db.Set<SysUser>()
            .Where(u => u.FID == operatorId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new TaskCommentListDto
        {
            Id = comment.FID,
            TaskId = comment.FTaskId,
            UserId = comment.FUserId,
            UserName = userName,
            Content = comment.FContent,
            Type = comment.FType,
            ParentCommentId = comment.FParentCommentId,
            PushedToDingTalk = comment.FPushedToDingTalk,
            CreateTime = comment.FCreateTime,
            UpdateTime = comment.FUpdateTime
        };

        return ApiResult<TaskCommentListDto>.Success(dto);
    }

    public async Task<ApiResult<TaskCommentListDto>> UpdateAsync(long taskId, long commentId, UpdateTaskCommentRequest request)
    {
        var comment = await _db.Set<TmTaskComment>()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == commentId && c.FTaskId == taskId);

        if (comment == null)
            return ApiResult<TaskCommentListDto>.Fail("评论不存在");

        comment.FContent = request.Content;
        comment.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        var userName = await _db.Set<SysUser>()
            .Where(u => u.FID == comment.FUserId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new TaskCommentListDto
        {
            Id = comment.FID,
            TaskId = comment.FTaskId,
            UserId = comment.FUserId,
            UserName = userName,
            Content = comment.FContent,
            Type = comment.FType,
            ParentCommentId = comment.FParentCommentId,
            PushedToDingTalk = comment.FPushedToDingTalk,
            CreateTime = comment.FCreateTime,
            UpdateTime = comment.FUpdateTime
        };

        return ApiResult<TaskCommentListDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long taskId, long commentId)
    {
        var comment = await _db.Set<TmTaskComment>()
            .FirstOrDefaultAsync(c => c.FID == commentId && c.FTaskId == taskId);

        if (comment == null)
            return ApiResult<bool>.Fail("评论不存在");

        // 删除子回复
        var replies = await _db.Set<TmTaskComment>()
            .Where(c => c.FParentCommentId == commentId)
            .ToListAsync();

        var allIds = replies.Select(r => r.FID).Append(commentId).ToList();

        // 删除关联的表情回应
        var reactions = await _db.Set<TmCommentReaction>()
            .Where(r => allIds.Contains(r.FCommentId))
            .ToListAsync();
        _db.Set<TmCommentReaction>().RemoveRange(reactions);

        // 删除关联的附件记录
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 1 && allIds.Contains(a.FRelationId))
            .ToListAsync();
        _db.Set<TmAttachment>().RemoveRange(attachments);

        _db.Set<TmTaskComment>().RemoveRange(replies);
        _db.Set<TmTaskComment>().Remove(comment);

        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<List<ReactionSummaryDto>>> ToggleReactionAsync(long taskId, long commentId, ToggleReactionRequest request, long userId)
    {
        var commentExists = await _db.Set<TmTaskComment>()
            .AnyAsync(c => c.FID == commentId && c.FTaskId == taskId);
        if (!commentExists)
            return ApiResult<List<ReactionSummaryDto>>.Fail("评论不存在");

        var existing = await _db.Set<TmCommentReaction>()
            .FirstOrDefaultAsync(r => r.FCommentId == commentId && r.FUserId == userId && r.FEmojiCode == request.EmojiCode);

        if (existing != null)
        {
            _db.Set<TmCommentReaction>().Remove(existing);
        }
        else
        {
            _db.Set<TmCommentReaction>().Add(new TmCommentReaction
            {
                FCommentId = commentId,
                FUserId = userId,
                FEmojiCode = request.EmojiCode,
                FCreateTime = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();

        var summary = await BuildReactionSummaryAsync(commentId, userId);
        return ApiResult<List<ReactionSummaryDto>>.Success(summary);
    }

    public async Task<ApiResult<bool>> RemoveReactionAsync(long taskId, long commentId, string emoji, long userId)
    {
        var reaction = await _db.Set<TmCommentReaction>()
            .FirstOrDefaultAsync(r => r.FCommentId == commentId && r.FUserId == userId && r.FEmojiCode == emoji);

        if (reaction == null)
            return ApiResult<bool>.Fail("表情回应不存在");

        _db.Set<TmCommentReaction>().Remove(reaction);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "移除成功");
    }

    #region Private Helpers

    private async Task<List<ReactionSummaryDto>> BuildReactionSummaryAsync(long commentId, long currentUserId)
    {
        var reactions = await _db.Set<TmCommentReaction>()
            .Where(r => r.FCommentId == commentId)
            .ToListAsync();

        return reactions
            .GroupBy(r => r.FEmojiCode)
            .Select(g => new ReactionSummaryDto
            {
                EmojiCode = g.Key,
                Count = g.Count(),
                HasReacted = g.Any(r => r.FUserId == currentUserId),
                UserIds = g.Select(r => r.FUserId).ToList()
            })
            .ToList();
    }

    private static TaskCommentListDto MapToDto(
        TmTaskComment comment,
        List<TmTaskComment> allReplies,
        List<TmCommentReaction> allReactions,
        List<TmAttachment> allAttachments,
        Dictionary<long, string> userDict)
    {
        var commentReactions = allReactions.Where(r => r.FCommentId == comment.FID).ToList();
        var commentAttachments = allAttachments.Where(a => a.FRelationId == comment.FID).ToList();
        var commentReplies = allReplies.Where(r => r.FParentCommentId == comment.FID).ToList();

        return new TaskCommentListDto
        {
            Id = comment.FID,
            TaskId = comment.FTaskId,
            UserId = comment.FUserId,
            UserName = userDict.GetValueOrDefault(comment.FUserId),
            Content = comment.FContent,
            Type = comment.FType,
            ParentCommentId = comment.FParentCommentId,
            PushedToDingTalk = comment.FPushedToDingTalk,
            CreateTime = comment.FCreateTime,
            UpdateTime = comment.FUpdateTime,
            Reactions = commentReactions
                .GroupBy(r => r.FEmojiCode)
                .Select(g => new ReactionSummaryDto
                {
                    EmojiCode = g.Key,
                    Count = g.Count(),
                    UserIds = g.Select(r => r.FUserId).ToList()
                })
                .ToList(),
            Attachments = commentAttachments.Select(a => new AttachmentListDto
            {
                Id = a.FID,
                RelationType = a.FRelationType,
                RelationId = a.FRelationId,
                UserId = a.FUserId,
                UserName = userDict.GetValueOrDefault(a.FUserId),
                OriginalFileName = a.FOriginalFileName,
                StoragePath = a.FStoragePath,
                FileSize = a.FFileSize,
                FileType = a.FFileType,
                CreateTime = a.FCreateTime
            }).ToList(),
            Replies = commentReplies.Select(r => MapToDto(r, allReplies, allReactions, allAttachments, userDict)).ToList()
        };
    }

    #endregion
}
