using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class KanbanService : IKanbanService
{
    private readonly STOTOPDbContext _db;

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        { 0, "待办" },
        { 1, "进行中" },
        { 2, "已完成" },
        { 3, "已取消" },
        { 4, "已延期" }
    };

    public KanbanService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<KanbanDataDto>> GetKanbanDataAsync(KanbanQueryRequest query, long orgId, long currentUserId, bool isAdmin)
    {
        var q = _db.Set<TmTask>()
            .Where(t => t.FOrgId == orgId && !t.FIsTemplate && t.FParentTaskId == 0);

        // 可见范围过滤(非admin)
        if (!isAdmin)
        {
            var memberProjectIds = _db.Set<TmProjectMember>()
                .Where(pm => pm.FUserId == currentUserId)
                .Select(pm => pm.FProjectId);

            var memberTaskIds = _db.Set<TmTaskMember>()
                .Where(tm => tm.FUserId == currentUserId)
                .Select(tm => tm.FTaskId);

            q = q.Where(t =>
                t.FVisibility == 0 ||
                (t.FVisibility == 1 && t.FProjectId.HasValue && memberProjectIds.Contains(t.FProjectId.Value)) ||
                (t.FVisibility == 2 && memberTaskIds.Contains(t.FID)) ||
                (t.FVisibility == 3 && (t.FCreatorId == currentUserId || t.FAssigneeId == currentUserId)) ||
                t.FCreatorId == currentUserId ||
                t.FAssigneeId == currentUserId ||
                memberTaskIds.Contains(t.FID)
            );
        }

        // 筛选条件
        if (query.ProjectId.HasValue)
            q = q.Where(t => t.FProjectId == query.ProjectId.Value);
        if (query.AssigneeId.HasValue)
            q = q.Where(t => t.FAssigneeId == query.AssigneeId.Value);
        if (query.Priority.HasValue)
            q = q.Where(t => t.FPriority == query.Priority.Value);

        // 标签筛选
        if (query.TagIds != null && query.TagIds.Count > 0)
        {
            var taskIdsWithTags = _db.Set<TmTaskTag>()
                .Where(tt => query.TagIds.Contains(tt.FTagId))
                .Select(tt => tt.FTaskId)
                .Distinct();
            q = q.Where(t => taskIdsWithTags.Contains(t.FID));
        }

        var tasks = await q.OrderBy(t => t.FSort).ThenByDescending(t => t.FCreateTime).ToListAsync();
        var taskIds = tasks.Select(t => t.FID).ToList();

        // 用户名
        var userIds = tasks.Where(t => t.FAssigneeId.HasValue).Select(t => t.FAssigneeId!.Value).Distinct().ToList();
        var userDict = await GetUserNameDict(userIds);

        // 子任务统计
        var subCounts = await _db.Set<TmTask>()
            .Where(t => taskIds.Contains(t.FParentTaskId))
            .GroupBy(t => t.FParentTaskId)
            .Select(g => new { ParentId = g.Key, Total = g.Count(), Completed = g.Count(t => t.FStatus == 2) })
            .ToListAsync();
        var subCountDict = subCounts.ToDictionary(x => x.ParentId);

        // 标签
        var tagLinks = await _db.Set<TmTaskTag>().Where(tt => taskIds.Contains(tt.FTaskId)).ToListAsync();
        var tagIdsList = tagLinks.Select(tl => tl.FTagId).Distinct().ToList();
        var tagDict = tagIdsList.Count > 0
            ? await _db.Set<TmTag>().Where(t => tagIdsList.Contains(t.FID)).ToDictionaryAsync(t => t.FID)
            : new Dictionary<long, TmTag>();

        // 按状态分组
        var grouped = tasks.GroupBy(t => t.FStatus);
        var columns = new List<KanbanColumnDto>();

        // 确保所有状态列都存在
        foreach (var status in StatusNames.Keys.OrderBy(k => k))
        {
            var statusTasks = grouped.FirstOrDefault(g => g.Key == status)?.ToList() ?? new List<TmTask>();
            columns.Add(new KanbanColumnDto
            {
                Status = status,
                StatusName = StatusNames[status],
                Count = statusTasks.Count,
                Cards = statusTasks.Select(t =>
                {
                    var card = new KanbanCardDto
                    {
                        Id = t.FID,
                        UID = t.FUID,
                        Title = t.FTitle,
                        Code = t.FCode,
                        Priority = t.FPriority,
                        Status = t.FStatus,
                        AssigneeId = t.FAssigneeId,
                        AssigneeName = t.FAssigneeId.HasValue ? userDict.GetValueOrDefault(t.FAssigneeId.Value) : null,
                        PlanEnd = t.FPlanEnd,
                        Progress = t.FProgress,
                        Sort = t.FSort
                    };

                    if (subCountDict.TryGetValue(t.FID, out var sc))
                    {
                        card.SubTaskCount = sc.Total;
                        card.CompletedSubTaskCount = sc.Completed;
                    }

                    card.Tags = tagLinks.Where(tl => tl.FTaskId == t.FID && tagDict.ContainsKey(tl.FTagId))
                        .Select(tl => new TagSimpleDto { Id = tagDict[tl.FTagId].FID, Name = tagDict[tl.FTagId].FName, Color = tagDict[tl.FTagId].FColor })
                        .ToList();

                    return card;
                }).ToList()
            });
        }

        return ApiResult<KanbanDataDto>.Success(new KanbanDataDto
        {
            ProjectId = query.ProjectId,
            Columns = columns
        });
    }

    public async Task<ApiResult<bool>> MoveAsync(KanbanMoveRequest request)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == request.TaskId);
        if (task == null)
            return ApiResult<bool>.Fail("任务不存在");

        task.FStatus = request.TargetStatus;
        task.FSort = request.TargetSort;
        task.FUpdateTime = DateTime.Now;

        // 自动设置实际时间
        if (request.TargetStatus == 1 && task.FActualStart == null)
            task.FActualStart = DateTime.Now;
        if (request.TargetStatus == 2)
        {
            task.FActualEnd = DateTime.Now;
            task.FProgress = 100;
        }

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    private async Task<Dictionary<long, string>> GetUserNameDict(List<long> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<long, string>();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        return users.ToDictionary(u => u.FID, u => u.FName);
    }
}
