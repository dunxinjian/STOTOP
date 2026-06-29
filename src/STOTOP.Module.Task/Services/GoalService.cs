using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class GoalService : IGoalService
{
    private readonly STOTOPDbContext _db;
    private readonly IKeyResultService _krService;

    public GoalService(STOTOPDbContext db, IKeyResultService krService)
    {
        _db = db;
        _krService = krService;
    }

    public async Task<ApiResult<List<GoalTreeDto>>> GetTreeAsync(GoalTreeQueryRequest query, long orgId)
    {
        var q = _db.Set<TmGoal>().Where(g => g.FOrgId == orgId);

        if (query.GoalOrgId.HasValue)
            q = q.Where(g => g.FGoalOrgId == query.GoalOrgId.Value);
        if (query.ResponsibleId.HasValue)
            q = q.Where(g => g.FResponsibleId == query.ResponsibleId.Value);
        if (query.Status.HasValue)
            q = q.Where(g => g.FStatus == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Level))
            q = q.Where(g => g.FLevel == query.Level);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
            q = q.Where(g => g.FTitle.Contains(query.Keyword));

        var goals = await q.OrderBy(g => g.FParentId).ThenByDescending(g => g.FCreateTime).ToListAsync();

        // 获取KR数量
        var goalIds = goals.Select(g => g.FID).ToList();
        var krCounts = await _db.Set<TmKeyResult>()
            .Where(kr => goalIds.Contains(kr.FGoalId))
            .GroupBy(kr => kr.FGoalId)
            .Select(g => new { GoalId = g.Key, Count = g.Count() })
            .ToListAsync();
        var krCountDict = krCounts.ToDictionary(x => x.GoalId, x => x.Count);

        // 获取责任人名称
        var userIds = goals.Where(g => g.FResponsibleId.HasValue).Select(g => g.FResponsibleId!.Value).Distinct().ToList();
        var userDict = await GetUserNameDict(userIds);

        var dtos = goals.Select(g => new GoalTreeDto
        {
            Id = g.FID,
            UID = g.FUID,
            Title = g.FTitle,
            ResponsibleId = g.FResponsibleId,
            ResponsibleName = g.FResponsibleId.HasValue && userDict.ContainsKey(g.FResponsibleId.Value) ? userDict[g.FResponsibleId.Value] : null,
            ParentId = g.FParentId,
            Level = g.FLevel,
            Progress = g.FProgress,
            Status = g.FStatus,
            KeyResultCount = krCountDict.GetValueOrDefault(g.FID, 0)
        }).ToList();

        var tree = BuildTree(dtos);
        return ApiResult<List<GoalTreeDto>>.Success(tree);
    }

    public async Task<ApiResult<GoalDetailDto>> GetByIdAsync(long id)
    {
        var goal = await _db.Set<TmGoal>()
            .Include(g => g.KeyResults)
            .FirstOrDefaultAsync(g => g.FID == id);

        if (goal == null)
            return ApiResult<GoalDetailDto>.Fail("目标不存在");

        // 获取子目标
        var children = await _db.Set<TmGoal>()
            .Where(g => g.FParentId == id)
            .OrderByDescending(g => g.FCreateTime)
            .ToListAsync();

        // 获取用户名
        var userIds = new List<long>();
        if (goal.FResponsibleId.HasValue) userIds.Add(goal.FResponsibleId.Value);
        userIds.Add(goal.FCreatorId);
        userIds.AddRange(goal.KeyResults.Where(kr => kr.FResponsibleId.HasValue).Select(kr => kr.FResponsibleId!.Value));
        userIds.AddRange(children.Where(c => c.FResponsibleId.HasValue).Select(c => c.FResponsibleId!.Value));
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 获取子目标KR数量
        var childIds = children.Select(c => c.FID).ToList();
        var krCounts = await _db.Set<TmKeyResult>()
            .Where(kr => childIds.Contains(kr.FGoalId))
            .GroupBy(kr => kr.FGoalId)
            .Select(g => new { GoalId = g.Key, Count = g.Count() })
            .ToListAsync();
        var krCountDict = krCounts.ToDictionary(x => x.GoalId, x => x.Count);

        var dto = MapToDetailDto(goal, userDict);
        dto.KeyResults = goal.KeyResults.OrderBy(kr => kr.FSort).Select(kr => MapKrToListDto(kr, userDict)).ToList();
        dto.Children = children.Select(c => new GoalListDto
        {
            Id = c.FID,
            UID = c.FUID,
            Title = c.FTitle,
            OrgId = c.FOrgId,
            GoalOrgId = c.FGoalOrgId,
            ResponsibleId = c.FResponsibleId,
            ResponsibleName = c.FResponsibleId.HasValue && userDict.ContainsKey(c.FResponsibleId.Value) ? userDict[c.FResponsibleId.Value] : null,
            ParentId = c.FParentId,
            Level = c.FLevel,
            StartDate = c.FStartDate,
            EndDate = c.FEndDate,
            Progress = c.FProgress,
            Weight = c.FWeight,
            Status = c.FStatus,
            KeyResultCount = krCountDict.GetValueOrDefault(c.FID, 0),
            ChildrenCount = 0,
            CreateTime = c.FCreateTime,
            UpdateTime = c.FUpdateTime
        }).ToList();

        return ApiResult<GoalDetailDto>.Success(dto);
    }

    public async Task<ApiResult<GoalDetailDto>> CreateAsync(CreateGoalRequest request, long orgId, long creatorId)
    {
        var goal = new TmGoal
        {
            FTitle = request.Title,
            FDescription = request.Description,
            FOrgId = orgId,
            FGoalOrgId = request.GoalOrgId,
            FResponsibleId = request.ResponsibleId,
            FParentId = request.ParentId,
            FLevel = request.Level,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FWeight = request.Weight,
            FCreatorId = creatorId,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        try
        {
            _db.Set<TmGoal>().Add(goal);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return ApiResult<GoalDetailDto>.Fail($"创建目标失败：{ex.InnerException?.Message ?? ex.Message}");
        }

        return await GetByIdAsync(goal.FID);
    }

    public async Task<ApiResult<GoalDetailDto>> UpdateAsync(long id, UpdateGoalRequest request)
    {
        var goal = await _db.Set<TmGoal>().AsTracking().FirstOrDefaultAsync(g => g.FID == id);
        if (goal == null)
            return ApiResult<GoalDetailDto>.Fail("目标不存在");

        goal.FTitle = request.Title;
        goal.FDescription = request.Description;
        goal.FGoalOrgId = request.GoalOrgId;
        goal.FResponsibleId = request.ResponsibleId;
        goal.FLevel = request.Level;
        goal.FStartDate = request.StartDate;
        goal.FEndDate = request.EndDate;
        goal.FWeight = request.Weight;
        goal.FStatus = request.Status;
        goal.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<GoalDetailDto>> DecomposeAsync(long id, DecomposeGoalRequest request, long orgId, long creatorId)
    {
        var parent = await _db.Set<TmGoal>().FirstOrDefaultAsync(g => g.FID == id);
        if (parent == null)
            return ApiResult<GoalDetailDto>.Fail("父目标不存在");

        var child = new TmGoal
        {
            FTitle = request.Title,
            FDescription = request.Description,
            FOrgId = orgId,
            FGoalOrgId = request.GoalOrgId,
            FResponsibleId = request.ResponsibleId,
            FParentId = id,
            FLevel = request.Level,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FWeight = request.Weight,
            FCreatorId = creatorId,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        try
        {
            _db.Set<TmGoal>().Add(child);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return ApiResult<GoalDetailDto>.Fail($"分解目标失败：{ex.InnerException?.Message ?? ex.Message}");
        }

        return await GetByIdAsync(child.FID);
    }

    public async Task<ApiResult<List<GoalListDto>>> GetChildrenAsync(long id)
    {
        var children = await _db.Set<TmGoal>()
            .Where(g => g.FParentId == id)
            .OrderByDescending(g => g.FCreateTime)
            .ToListAsync();

        var goalIds = children.Select(c => c.FID).ToList();
        var krCounts = await _db.Set<TmKeyResult>()
            .Where(kr => goalIds.Contains(kr.FGoalId))
            .GroupBy(kr => kr.FGoalId)
            .Select(g => new { GoalId = g.Key, Count = g.Count() })
            .ToListAsync();
        var krCountDict = krCounts.ToDictionary(x => x.GoalId, x => x.Count);

        var childrenCounts = await _db.Set<TmGoal>()
            .Where(g => goalIds.Contains(g.FParentId))
            .GroupBy(g => g.FParentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToListAsync();
        var childrenCountDict = childrenCounts.ToDictionary(x => x.ParentId, x => x.Count);

        var userIds = children.Where(c => c.FResponsibleId.HasValue).Select(c => c.FResponsibleId!.Value).Distinct().ToList();
        var userDict = await GetUserNameDict(userIds);

        var dtos = children.Select(c => new GoalListDto
        {
            Id = c.FID,
            UID = c.FUID,
            Title = c.FTitle,
            OrgId = c.FOrgId,
            GoalOrgId = c.FGoalOrgId,
            ResponsibleId = c.FResponsibleId,
            ResponsibleName = c.FResponsibleId.HasValue && userDict.ContainsKey(c.FResponsibleId.Value) ? userDict[c.FResponsibleId.Value] : null,
            ParentId = c.FParentId,
            Level = c.FLevel,
            StartDate = c.FStartDate,
            EndDate = c.FEndDate,
            Progress = c.FProgress,
            Weight = c.FWeight,
            Status = c.FStatus,
            KeyResultCount = krCountDict.GetValueOrDefault(c.FID, 0),
            ChildrenCount = childrenCountDict.GetValueOrDefault(c.FID, 0),
            CreateTime = c.FCreateTime,
            UpdateTime = c.FUpdateTime
        }).ToList();

        return ApiResult<List<GoalListDto>>.Success(dtos);
    }

    public async Task<ApiResult<List<TaskListDto>>> GetTasksAsync(long id)
    {
        var tasks = await _db.Set<TmTask>()
            .Where(t => t.FGoalId == id && !t.FIsTemplate)
            .OrderByDescending(t => t.FCreateTime)
            .ToListAsync();

        var userIds = new List<long>();
        userIds.AddRange(tasks.Where(t => t.FAssigneeId.HasValue).Select(t => t.FAssigneeId!.Value));
        userIds.AddRange(tasks.Select(t => t.FCreatorId));
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 子任务统计
        var taskIds = tasks.Select(t => t.FID).ToList();
        var subCounts = await _db.Set<TmTask>()
            .Where(t => taskIds.Contains(t.FParentTaskId))
            .GroupBy(t => t.FParentTaskId)
            .Select(g => new { ParentId = g.Key, Total = g.Count(), Completed = g.Count(t => t.FStatus == 2) })
            .ToListAsync();
        var subCountDict = subCounts.ToDictionary(x => x.ParentId);

        // 标签
        var tagLinks = await _db.Set<TmTaskTag>().Where(tt => taskIds.Contains(tt.FTaskId)).ToListAsync();
        var tagIds = tagLinks.Select(tl => tl.FTagId).Distinct().ToList();
        var tags = await _db.Set<TmTag>().Where(t => tagIds.Contains(t.FID)).ToListAsync();
        var tagDict = tags.ToDictionary(t => t.FID);

        var dtos = tasks.Select(t =>
        {
            var dto = MapTaskToListDto(t, userDict);
            if (subCountDict.TryGetValue(t.FID, out var sc))
            {
                dto.SubTaskCount = sc.Total;
                dto.CompletedSubTaskCount = sc.Completed;
            }
            dto.Tags = tagLinks.Where(tl => tl.FTaskId == t.FID && tagDict.ContainsKey(tl.FTagId))
                .Select(tl => new TagSimpleDto { Id = tagDict[tl.FTagId].FID, Name = tagDict[tl.FTagId].FName, Color = tagDict[tl.FTagId].FColor })
                .ToList();
            return dto;
        }).ToList();

        return ApiResult<List<TaskListDto>>.Success(dtos);
    }

    public async Task<ApiResult<bool>> RecalculateProgressAsync(long id)
    {
        var goal = await _db.Set<TmGoal>().AsTracking().FirstOrDefaultAsync(g => g.FID == id);
        if (goal == null)
            return ApiResult<bool>.Fail("目标不存在");

        var krs = await _db.Set<TmKeyResult>().Where(kr => kr.FGoalId == id).ToListAsync();
        if (krs.Count == 0)
        {
            goal.FProgress = 0;
        }
        else
        {
            var totalWeight = krs.Sum(kr => kr.FWeight);
            if (totalWeight == 0)
            {
                goal.FProgress = 0;
            }
            else
            {
                var weightedProgress = krs.Sum(kr => kr.FProgress * kr.FWeight);
                goal.FProgress = (int)Math.Round((double)weightedProgress / totalWeight);
            }
        }

        goal.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true);
    }

    #region Private Helpers

    private async Task<Dictionary<long, string>> GetUserNameDict(List<long> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<long, string>();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        return users.ToDictionary(u => u.FID, u => u.FName);
    }

    private static List<GoalTreeDto> BuildTree(List<GoalTreeDto> nodes)
    {
        var lookup = nodes.ToLookup(n => n.ParentId);
        foreach (var node in nodes)
        {
            node.Children = lookup[node.Id].ToList();
        }
        return nodes.Where(n => n.ParentId == 0).ToList();
    }

    private static GoalDetailDto MapToDetailDto(TmGoal g, Dictionary<long, string> userDict) => new()
    {
        Id = g.FID,
        UID = g.FUID,
        Title = g.FTitle,
        Description = g.FDescription,
        OrgId = g.FOrgId,
        GoalOrgId = g.FGoalOrgId,
        ResponsibleId = g.FResponsibleId,
        ResponsibleName = g.FResponsibleId.HasValue && userDict.ContainsKey(g.FResponsibleId.Value) ? userDict[g.FResponsibleId.Value] : null,
        ParentId = g.FParentId,
        Level = g.FLevel,
        StartDate = g.FStartDate,
        EndDate = g.FEndDate,
        Progress = g.FProgress,
        Weight = g.FWeight,
        Status = g.FStatus,
        CreatorId = g.FCreatorId,
        CreatorName = userDict.GetValueOrDefault(g.FCreatorId),
        CreateTime = g.FCreateTime,
        UpdateTime = g.FUpdateTime
    };

    private static KeyResultListDto MapKrToListDto(TmKeyResult kr, Dictionary<long, string> userDict) => new()
    {
        Id = kr.FID,
        UID = kr.FUID,
        GoalId = kr.FGoalId,
        Title = kr.FTitle,
        MeasureType = kr.FMeasureType,
        TargetValue = kr.FTargetValue,
        CurrentValue = kr.FCurrentValue,
        StartValue = kr.FStartValue,
        Unit = kr.FUnit,
        Weight = kr.FWeight,
        Progress = kr.FProgress,
        Status = kr.FStatus,
        ResponsibleId = kr.FResponsibleId,
        ResponsibleName = kr.FResponsibleId.HasValue && userDict.ContainsKey(kr.FResponsibleId.Value) ? userDict[kr.FResponsibleId.Value] : null,
        Sort = kr.FSort,
        CreateTime = kr.FCreateTime,
        UpdateTime = kr.FUpdateTime
    };

    private static TaskListDto MapTaskToListDto(TmTask t, Dictionary<long, string> userDict) => new()
    {
        Id = t.FID,
        UID = t.FUID,
        Title = t.FTitle,
        OrgId = t.FOrgId,
        ProjectId = t.FProjectId,
        GoalId = t.FGoalId,
        KRId = t.FKRId,
        ParentTaskId = t.FParentTaskId,
        Type = t.FType,
        Priority = t.FPriority,
        Status = t.FStatus,
        AssigneeId = t.FAssigneeId,
        AssigneeName = t.FAssigneeId.HasValue && userDict.ContainsKey(t.FAssigneeId.Value) ? userDict[t.FAssigneeId.Value] : null,
        CreatorId = t.FCreatorId,
        CreatorName = userDict.GetValueOrDefault(t.FCreatorId),
        PlanStart = t.FPlanStart,
        PlanEnd = t.FPlanEnd,
        Progress = t.FProgress,
        Code = t.FCode,
        CreateTime = t.FCreateTime,
        UpdateTime = t.FUpdateTime
    };

    #endregion
}
