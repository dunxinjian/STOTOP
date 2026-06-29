using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class FeedbackService : IFeedbackService
{
    private static readonly Dictionary<int, int[]> ValidTransitions = new()
    {
        [0] = new[] { 1, 2, 6 },
        [1] = new[] { 2, 6 },
        [2] = new[] { 3, 4, 6 },
        [3] = new[] { 4, 6 },
        [4] = new[] { 5, 6 },
        [5] = new[] { 4, 6 },
        [6] = Array.Empty<int>()
    };

    private readonly STOTOPDbContext _db;

    public FeedbackService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<FeedbackCardDto>> GetPagedAsync(FeedbackQueryRequest request, long currentUserId)
    {
        var query = ApplyFilters(_db.Set<SysFeedbackCard>().AsNoTracking(), request, currentUserId);
        var total = await query.CountAsync();
        var cards = await query
            .OrderByDescending(c => c.FSeverity)
            .ThenByDescending(c => c.FUpdateTime)
            .Skip((Math.Max(request.PageIndex, 1) - 1) * Math.Max(request.PageSize, 1))
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .ToListAsync();

        return new PagedResult<FeedbackCardDto>
        {
            Items = await MapCardsAsync(cards),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<FeedbackCardDto>> GetBoardAsync(FeedbackQueryRequest request, long currentUserId)
    {
        var query = ApplyFilters(_db.Set<SysFeedbackCard>().AsNoTracking(), request, currentUserId);
        var cards = await query
            .OrderBy(c => c.FStatus)
            .ThenByDescending(c => c.FSeverity)
            .ThenByDescending(c => c.FUpdateTime)
            .Take(500)
            .ToListAsync();
        return await MapCardsAsync(cards);
    }

    public async Task<List<FeedbackStatusCountDto>> GetStatusCountsAsync(FeedbackQueryRequest request, long currentUserId)
    {
        var query = ApplyFilters(_db.Set<SysFeedbackCard>().AsNoTracking(), request, currentUserId, ignoreStatus: true);
        return await query
            .GroupBy(c => c.FStatus)
            .Select(g => new FeedbackStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderBy(x => x.Status)
            .ToListAsync();
    }

    public async Task<FeedbackDetailDto?> GetByIdAsync(long id)
    {
        var card = await _db.Set<SysFeedbackCard>().AsNoTracking().FirstOrDefaultAsync(c => c.FID == id);
        return card == null ? null : await MapDetailAsync(card);
    }

    public async Task<FeedbackDetailDto> CreateAsync(CreateFeedbackRequest request, long submitterId, long orgId)
    {
        ValidateCreateRequest(request);

        var card = new SysFeedbackCard
        {
            FOrgId = orgId,
            FTitle = request.Title.Trim(),
            FType = request.Type,
            FModule = request.Module.Trim(),
            FSeverity = request.Severity,
            FStatus = 0,
            FSubmitterId = submitterId,
            FDescription = TrimToNull(request.Description),
            FReproduceSteps = TrimToNull(request.ReproduceSteps),
            FExpectedResult = TrimToNull(request.ExpectedResult),
            FActualResult = TrimToNull(request.ActualResult),
            FAttachmentLinks = TrimToNull(request.AttachmentLinks),
            FPageUrl = TrimToNull(request.PageUrl),
            FClientInfo = TrimToNull(request.ClientInfo),
            FVersion = TrimToNull(request.Version),
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<SysFeedbackCard>().Add(card);
        await _db.SaveChangesAsync();

        await AddActivityAsync(card, submitterId, "create", "提交反馈", null, card.FStatus);
        return (await GetByIdAsync(card.FID))!;
    }

    public async Task<FeedbackDetailDto?> UpdateAsync(long id, UpdateFeedbackRequest request, long actorId)
    {
        ValidateCreateRequest(request);
        var card = await _db.Set<SysFeedbackCard>().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (card == null) return null;
        if (card.FStatus == 6)
            throw new InvalidOperationException("已关闭的反馈不能编辑");

        card.FTitle = request.Title.Trim();
        card.FType = request.Type;
        card.FModule = request.Module.Trim();
        card.FSeverity = request.Severity;
        card.FAssigneeId = request.AssigneeId;
        card.FDescription = TrimToNull(request.Description);
        card.FReproduceSteps = TrimToNull(request.ReproduceSteps);
        card.FExpectedResult = TrimToNull(request.ExpectedResult);
        card.FActualResult = TrimToNull(request.ActualResult);
        card.FAttachmentLinks = TrimToNull(request.AttachmentLinks);
        card.FPageUrl = TrimToNull(request.PageUrl);
        card.FClientInfo = TrimToNull(request.ClientInfo);
        card.FVersion = TrimToNull(request.Version);
        card.FConclusion = TrimToNull(request.Conclusion);
        card.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await AddActivityAsync(card, actorId, "update", "更新反馈信息", card.FStatus, card.FStatus);
        return await GetByIdAsync(id);
    }

    public async Task<FeedbackDetailDto?> AssignAsync(long id, AssignFeedbackRequest request, long actorId)
    {
        var card = await _db.Set<SysFeedbackCard>().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (card == null) return null;
        if (card.FStatus == 6)
            throw new InvalidOperationException("已关闭的反馈不能分派");

        card.FAssigneeId = request.AssigneeId;
        card.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        var content = string.IsNullOrWhiteSpace(request.Comment) ? "分派负责人" : request.Comment.Trim();
        await AddActivityAsync(card, actorId, "assign", content, card.FStatus, card.FStatus);
        return await GetByIdAsync(id);
    }

    public async Task<FeedbackDetailDto?> TransitionAsync(long id, TransitionFeedbackRequest request, long actorId)
    {
        var card = await _db.Set<SysFeedbackCard>().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (card == null) return null;
        if (!ValidTransitions.TryGetValue(card.FStatus, out var targets) || !targets.Contains(request.Status))
            throw new InvalidOperationException($"不允许从状态 {card.FStatus} 流转到 {request.Status}");

        var fromStatus = card.FStatus;
        card.FStatus = request.Status;
        card.FConclusion = TrimToNull(request.Conclusion) ?? card.FConclusion;
        card.FClosedTime = request.Status == 6 ? DateTime.Now : null;
        card.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        await AddActivityAsync(card, actorId, "transition", TrimToNull(request.Comment) ?? "流转状态", fromStatus, request.Status);
        return await GetByIdAsync(id);
    }

    public async Task<FeedbackActivityDto?> AddCommentAsync(long id, AddFeedbackCommentRequest request, long actorId)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("评论内容不能为空");

        var card = await _db.Set<SysFeedbackCard>().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (card == null) return null;

        card.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
        return await AddActivityAsync(card, actorId, "comment", request.Content.Trim(), card.FStatus, card.FStatus);
    }

    private IQueryable<SysFeedbackCard> ApplyFilters(
        IQueryable<SysFeedbackCard> query,
        FeedbackQueryRequest request,
        long currentUserId,
        bool ignoreStatus = false)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c =>
                c.FTitle.Contains(keyword) ||
                (c.FDescription != null && c.FDescription.Contains(keyword)) ||
                (c.FActualResult != null && c.FActualResult.Contains(keyword)));
        }

        if (request.Type.HasValue) query = query.Where(c => c.FType == request.Type.Value);
        if (!string.IsNullOrWhiteSpace(request.Module)) query = query.Where(c => c.FModule == request.Module);
        if (request.Severity.HasValue) query = query.Where(c => c.FSeverity == request.Severity.Value);
        if (!ignoreStatus && request.Status.HasValue) query = query.Where(c => c.FStatus == request.Status.Value);
        if (request.SubmitterId.HasValue) query = query.Where(c => c.FSubmitterId == request.SubmitterId.Value);
        if (request.AssigneeId.HasValue) query = query.Where(c => c.FAssigneeId == request.AssigneeId.Value);
        if (request.Mine) query = query.Where(c => c.FSubmitterId == currentUserId || c.FAssigneeId == currentUserId);
        return query;
    }

    private async Task<FeedbackDetailDto> MapDetailAsync(SysFeedbackCard card)
    {
        var detail = (await MapCardsAsync(new[] { card })).First();
        var activities = await _db.Set<SysFeedbackActivity>()
            .AsNoTracking()
            .Where(a => a.FFeedbackId == card.FID)
            .OrderByDescending(a => a.FCreateTime)
            .ToListAsync();

        var userMap = await LoadUserMapAsync(activities.Select(a => a.FActorId));
        return new FeedbackDetailDto
        {
            Id = detail.Id,
            Uid = detail.Uid,
            OrgId = detail.OrgId,
            Title = detail.Title,
            Type = detail.Type,
            Module = detail.Module,
            Severity = detail.Severity,
            Status = detail.Status,
            SubmitterId = detail.SubmitterId,
            SubmitterName = detail.SubmitterName,
            AssigneeId = detail.AssigneeId,
            AssigneeName = detail.AssigneeName,
            Description = detail.Description,
            ReproduceSteps = detail.ReproduceSteps,
            ExpectedResult = detail.ExpectedResult,
            ActualResult = detail.ActualResult,
            AttachmentLinks = detail.AttachmentLinks,
            PageUrl = detail.PageUrl,
            ClientInfo = detail.ClientInfo,
            Version = detail.Version,
            Conclusion = detail.Conclusion,
            CreateTime = detail.CreateTime,
            UpdateTime = detail.UpdateTime,
            ClosedTime = detail.ClosedTime,
            Activities = activities.Select(a => MapActivity(a, userMap)).ToList()
        };
    }

    private async Task<List<FeedbackCardDto>> MapCardsAsync(IEnumerable<SysFeedbackCard> cards)
    {
        var list = cards.ToList();
        var userIds = list.Select(c => c.FSubmitterId)
            .Concat(list.Where(c => c.FAssigneeId.HasValue).Select(c => c.FAssigneeId!.Value));
        var userMap = await LoadUserMapAsync(userIds);

        return list.Select(c => new FeedbackCardDto
        {
            Id = c.FID,
            Uid = c.FUID,
            OrgId = c.FOrgId,
            Title = c.FTitle,
            Type = c.FType,
            Module = c.FModule,
            Severity = c.FSeverity,
            Status = c.FStatus,
            SubmitterId = c.FSubmitterId,
            SubmitterName = userMap.GetValueOrDefault(c.FSubmitterId),
            AssigneeId = c.FAssigneeId,
            AssigneeName = c.FAssigneeId.HasValue ? userMap.GetValueOrDefault(c.FAssigneeId.Value) : null,
            Description = c.FDescription,
            ReproduceSteps = c.FReproduceSteps,
            ExpectedResult = c.FExpectedResult,
            ActualResult = c.FActualResult,
            AttachmentLinks = c.FAttachmentLinks,
            PageUrl = c.FPageUrl,
            ClientInfo = c.FClientInfo,
            Version = c.FVersion,
            Conclusion = c.FConclusion,
            CreateTime = c.FCreateTime,
            UpdateTime = c.FUpdateTime,
            ClosedTime = c.FClosedTime
        }).ToList();
    }

    private async Task<Dictionary<long, string>> LoadUserMapAsync(IEnumerable<long> userIds)
    {
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();

        return await _db.Set<SysUser>()
            .AsNoTracking()
            .Where(u => ids.Contains(u.FID))
            .ToDictionaryAsync(u => u.FID, u => u.FName);
    }

    private async Task<FeedbackActivityDto> AddActivityAsync(
        SysFeedbackCard card,
        long actorId,
        string action,
        string? content,
        int? fromStatus,
        int? toStatus)
    {
        var activity = new SysFeedbackActivity
        {
            FOrgId = card.FOrgId,
            FFeedbackId = card.FID,
            FActorId = actorId,
            FAction = action,
            FContent = content,
            FFromStatus = fromStatus,
            FToStatus = toStatus,
            FCreateTime = DateTime.Now
        };

        _db.Set<SysFeedbackActivity>().Add(activity);
        await _db.SaveChangesAsync();

        var userMap = await LoadUserMapAsync(new[] { actorId });
        return MapActivity(activity, userMap);
    }

    private static FeedbackActivityDto MapActivity(SysFeedbackActivity activity, Dictionary<long, string> userMap)
    {
        return new FeedbackActivityDto
        {
            Id = activity.FID,
            FeedbackId = activity.FFeedbackId,
            ActorId = activity.FActorId,
            ActorName = userMap.GetValueOrDefault(activity.FActorId),
            Action = activity.FAction,
            Content = activity.FContent,
            FromStatus = activity.FFromStatus,
            ToStatus = activity.FToStatus,
            CreateTime = activity.FCreateTime
        };
    }

    private static void ValidateCreateRequest(CreateFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("反馈标题不能为空");
        if (string.IsNullOrWhiteSpace(request.Module))
            throw new InvalidOperationException("所属模块不能为空");
        if (request.Type is < 1 or > 5)
            throw new InvalidOperationException("反馈类型无效");
        if (request.Severity is < 1 or > 4)
            throw new InvalidOperationException("严重程度无效");
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
