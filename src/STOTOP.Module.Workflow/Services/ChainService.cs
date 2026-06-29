using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class ChainService : IChainService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<ChainService> _logger;

    public ChainService(STOTOPDbContext db, ILogger<ChainService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<string> CreateChainAsync(long orgId, string title, long creatorId)
    {
        // ChainId 使用 GUID 生成，格式：chain_{32位hex}
        var chainId = $"chain_{Guid.NewGuid():N}";
        _logger.LogInformation("创建链路 {ChainId}，标题: {Title}，创建人: {CreatorId}", chainId, title, creatorId);
        return Task.FromResult(chainId);
    }

    public async Task<ChainTimelineDto> GetTimelineAsync(string chainId)
    {
        // 获取链路下所有工作项
        var workItems = await _db.Set<WfWorkItem>()
            .Where(w => w.FChainId == chainId)
            .OrderBy(w => w.FChainSeq)
            .ToListAsync();

        // 获取所有相关日志作为事件
        var workItemIds = workItems.Select(w => w.FID).ToList();
        var logs = await _db.Set<WfWorkItemLog>()
            .Where(l => workItemIds.Contains(l.FWorkItemId))
            .OrderBy(l => l.FCreateTime)
            .ToListAsync();

        // 构建参与人列表
        var participants = new List<ChainParticipantDto>();
        var creatorIds = workItems.Select(w => w.FCreatorId).Distinct();
        foreach (var cid in creatorIds)
        {
            participants.Add(new ChainParticipantDto { UserId = cid, Role = "Creator" });
        }
        var assigneeIds = workItems.Where(w => w.FAssigneeId.HasValue).Select(w => w.FAssigneeId!.Value).Distinct();
        foreach (var aid in assigneeIds)
        {
            if (!participants.Any(p => p.UserId == aid))
                participants.Add(new ChainParticipantDto { UserId = aid, UserName = workItems.First(w => w.FAssigneeId == aid).FAssigneeName ?? "", Role = "Assignee" });
        }

        // 获取关注者
        var followers = await _db.Set<WfChainFollower>()
            .Where(f => f.FChainId == chainId)
            .ToListAsync();
        foreach (var f in followers)
        {
            if (!participants.Any(p => p.UserId == f.FUserId))
                participants.Add(new ChainParticipantDto { UserId = f.FUserId, UserName = f.FUserName ?? "", Role = "Follower" });
        }

        return new ChainTimelineDto
        {
            ChainId = chainId,
            WorkItems = workItems.Select(MapToDto).ToList(),
            Events = logs.Select(l => new ChainEventDto
            {
                EventType = MapActionToEventType(l.FAction),
                Description = l.FContent ?? l.FAction,
                OperatorId = l.FOperatorId,
                OperatorName = l.FOperatorName,
                Time = l.FCreateTime
            }).ToList(),
            Participants = participants
        };
    }

    public async Task<ChainCommentDto> AddCommentAsync(string chainId, long authorId, string authorName, string content, long? workItemId = null, long? replyToId = null)
    {
        // 获取 OrgId（从链路下的工作项取）
        var orgId = await _db.Set<WfWorkItem>()
            .Where(w => w.FChainId == chainId)
            .Select(w => w.FOrgId)
            .FirstOrDefaultAsync();

        var comment = new WfChainComment
        {
            FOrgId = orgId,
            FChainId = chainId,
            FWorkItemId = workItemId,
            FAuthorId = authorId,
            FAuthorName = authorName,
            FContent = content,
            FReplyToId = replyToId
        };

        _db.Set<WfChainComment>().Add(comment);
        await _db.SaveChangesAsync();

        return new ChainCommentDto
        {
            Id = comment.FID,
            AuthorId = comment.FAuthorId,
            AuthorName = comment.FAuthorName,
            Content = comment.FContent,
            ReplyToId = comment.FReplyToId,
            CreateTime = comment.FCreateTime
        };
    }

    public async Task<List<ChainCommentDto>> GetCommentsAsync(string chainId, int page = 1, int pageSize = 50)
    {
        var comments = await _db.Set<WfChainComment>()
            .Where(c => c.FChainId == chainId && !c.FIsDeleted)
            .OrderByDescending(c => c.FCreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return comments.Select(c => new ChainCommentDto
        {
            Id = c.FID,
            AuthorId = c.FAuthorId,
            AuthorName = c.FAuthorName,
            Content = c.FContent,
            ReplyToId = c.FReplyToId,
            CreateTime = c.FCreateTime
        }).ToList();
    }

    public async Task DeleteCommentAsync(long commentId, long operatorId)
    {
        var comment = await _db.Set<WfChainComment>().FirstOrDefaultAsync(c => c.FID == commentId)
            ?? throw new InvalidOperationException($"评论 {commentId} 不存在");

        if (comment.FAuthorId != operatorId)
            throw new InvalidOperationException("只能删除自己的评论");

        comment.FIsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task FollowAsync(string chainId, long userId, string userName)
    {
        var existing = await _db.Set<WfChainFollower>()
            .FirstOrDefaultAsync(f => f.FChainId == chainId && f.FUserId == userId);

        if (existing != null) return;

        var follower = new WfChainFollower
        {
            FChainId = chainId,
            FUserId = userId,
            FUserName = userName
        };

        _db.Set<WfChainFollower>().Add(follower);
        await _db.SaveChangesAsync();
    }

    public async Task UnfollowAsync(string chainId, long userId)
    {
        var follower = await _db.Set<WfChainFollower>()
            .FirstOrDefaultAsync(f => f.FChainId == chainId && f.FUserId == userId);

        if (follower != null)
        {
            _db.Set<WfChainFollower>().Remove(follower);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<ChainFollowerDto>> GetFollowersAsync(string chainId)
    {
        var followers = await _db.Set<WfChainFollower>()
            .Where(f => f.FChainId == chainId)
            .ToListAsync();

        return followers.Select(f => new ChainFollowerDto
        {
            UserId = f.FUserId,
            UserName = f.FUserName,
            IsMuted = f.FIsMuted
        }).ToList();
    }

    public async Task<bool> IsFollowingAsync(string chainId, long userId)
    {
        return await _db.Set<WfChainFollower>()
            .AnyAsync(f => f.FChainId == chainId && f.FUserId == userId);
    }

    public async Task AutoFollowParticipantsAsync(string chainId)
    {
        // 获取链路下所有参与人（创建人 + 处理人）
        var workItems = await _db.Set<WfWorkItem>()
            .Where(w => w.FChainId == chainId)
            .ToListAsync();

        var existingFollowers = await _db.Set<WfChainFollower>()
            .Where(f => f.FChainId == chainId)
            .Select(f => f.FUserId)
            .ToListAsync();

        var participantIds = new HashSet<long>();
        foreach (var wi in workItems)
        {
            participantIds.Add(wi.FCreatorId);
            if (wi.FAssigneeId.HasValue)
                participantIds.Add(wi.FAssigneeId.Value);
        }

        foreach (var userId in participantIds)
        {
            if (!existingFollowers.Contains(userId))
            {
                var name = workItems.FirstOrDefault(w => w.FAssigneeId == userId)?.FAssigneeName ?? "";
                _db.Set<WfChainFollower>().Add(new WfChainFollower
                {
                    FChainId = chainId,
                    FUserId = userId,
                    FUserName = name
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    #region Private Helpers

    private static int MapActionToEventType(string action)
    {
        return action switch
        {
            "Created" => (int)ChainEventType.Created,
            "Assigned" => (int)ChainEventType.Assigned,
            "Started" => (int)ChainEventType.StageStarted,
            "Completed" => (int)ChainEventType.StageCompleted,
            "Cancelled" => (int)ChainEventType.Revoked,
            "Escalated" => (int)ChainEventType.Escalated,
            _ => (int)ChainEventType.Created
        };
    }

    private static WorkItemDto MapToDto(WfWorkItem entity)
    {
        return new WorkItemDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Title = entity.FTitle,
            Description = entity.FDescription,
            Type = entity.FType,
            Source = entity.FSource,
            Status = entity.FStatus,
            Priority = entity.FPriority,
            ChainId = entity.FChainId,
            ChainSeq = entity.FChainSeq,
            DataScopeId = entity.FDataScopeId,
            CreatorId = entity.FCreatorId,
            AssigneeId = entity.FAssigneeId,
            AssigneeName = entity.FAssigneeName,
            Module = entity.FModule,
            BizType = entity.FBizType,
            BizId = entity.FBizId,
            DetailRoute = entity.FDetailRoute,
            CreateTime = entity.FCreateTime,
            Deadline = entity.FDeadline,
            CompletedTime = entity.FCompletedTime,
            Result = entity.FResult,
            Remark = entity.FRemark
        };
    }

    #endregion
}
