using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Events;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class TaskService : ITaskService
{
    private readonly STOTOPDbContext _db;
    private readonly IPointService _pointService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<TaskService> _logger;

    public TaskService(STOTOPDbContext db, IPointService pointService, IEventDispatcher eventDispatcher, ILogger<TaskService> logger)
    {
        _db = db;
        _pointService = pointService;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<ApiResult<PagedResult<TaskListDto>>> GetPagedListAsync(TaskPagedRequest query, long orgId, long currentUserId, bool isAdmin)
    {
        var q = GetVisibleTasksQuery(orgId, currentUserId, isAdmin);

        // 默认排除模板任务
        if (query.IsTemplate.HasValue)
            q = q.Where(t => t.FIsTemplate == query.IsTemplate.Value);
        else
            q = q.Where(t => !t.FIsTemplate);

        // 默认只显示顶级任务
        if (query.ParentTaskId.HasValue)
            q = q.Where(t => t.FParentTaskId == query.ParentTaskId.Value);
        else
            q = q.Where(t => t.FParentTaskId == 0);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(t => t.FTitle.Contains(kw) || (t.FCode != null && t.FCode.Contains(kw)));
        }
        if (query.Status.HasValue)
            q = q.Where(t => t.FStatus == query.Status.Value);
        if (query.Priority.HasValue)
            q = q.Where(t => t.FPriority == query.Priority.Value);
        if (query.AssigneeId.HasValue)
            q = q.Where(t => t.FAssigneeId == query.AssigneeId.Value);
        if (query.ProjectId.HasValue)
            q = q.Where(t => t.FProjectId == query.ProjectId.Value);
        if (query.GoalId.HasValue)
            q = q.Where(t => t.FGoalId == query.GoalId.Value);
        if (query.KRId.HasValue)
            q = q.Where(t => t.FKRId == query.KRId.Value);
        if (query.Type.HasValue)
            q = q.Where(t => t.FType == query.Type.Value);
        if (query.PlanStartFrom.HasValue)
            q = q.Where(t => t.FPlanStart >= query.PlanStartFrom.Value);
        if (query.PlanStartTo.HasValue)
            q = q.Where(t => t.FPlanStart <= query.PlanStartTo.Value);
        if (query.PlanEndFrom.HasValue)
            q = q.Where(t => t.FPlanEnd >= query.PlanEndFrom.Value);
        if (query.PlanEndTo.HasValue)
            q = q.Where(t => t.FPlanEnd <= query.PlanEndTo.Value);

        // 标签筛选
        if (query.TagIds != null && query.TagIds.Count > 0)
        {
            var taskIdsWithTags = _db.Set<TmTaskTag>()
                .Where(tt => query.TagIds.Contains(tt.FTagId))
                .Select(tt => tt.FTaskId)
                .Distinct();
            q = q.Where(t => taskIdsWithTags.Contains(t.FID));
        }

        var total = await q.CountAsync();

        var tasks = await q
            .OrderByDescending(t => t.FCreateTime)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var dtos = await EnrichTaskListDtos(tasks);

        return ApiResult<PagedResult<TaskListDto>>.Success(new PagedResult<TaskListDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        });
    }

    public async Task<ApiResult<TaskDetailDto>> GetByIdAsync(long id)
    {
        var task = await _db.Set<TmTask>()
            .Include(t => t.Members)
            .Include(t => t.Tags).ThenInclude(tt => tt.Tag)
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOnTask)
            .FirstOrDefaultAsync(t => t.FID == id);

        if (task == null)
            return ApiResult<TaskDetailDto>.Fail("任务不存在");

        // 子任务
        var subTasks = await _db.Set<TmTask>()
            .Where(t => t.FParentTaskId == id)
            .OrderBy(t => t.FSort)
            .ThenByDescending(t => t.FCreateTime)
            .ToListAsync();

        // 收集用户ID
        var userIds = new List<long> { task.FCreatorId };
        if (task.FAssigneeId.HasValue) userIds.Add(task.FAssigneeId.Value);
        userIds.AddRange(task.Members.Select(m => m.FUserId));
        userIds.AddRange(subTasks.Where(s => s.FAssigneeId.HasValue).Select(s => s.FAssigneeId!.Value));
        userIds.AddRange(subTasks.Select(s => s.FCreatorId));
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 项目名称
        string? projectName = null;
        if (task.FProjectId.HasValue)
        {
            projectName = await _db.Set<TmProject>()
                .Where(p => p.FID == task.FProjectId.Value)
                .Select(p => p.FName)
                .FirstOrDefaultAsync();
        }

        // 目标/KR标题
        string? goalTitle = null, krTitle = null;
        if (task.FGoalId.HasValue)
            goalTitle = await _db.Set<TmGoal>().Where(g => g.FID == task.FGoalId.Value).Select(g => g.FTitle).FirstOrDefaultAsync();
        if (task.FKRId.HasValue)
            krTitle = await _db.Set<TmKeyResult>().Where(kr => kr.FID == task.FKRId.Value).Select(kr => kr.FTitle).FirstOrDefaultAsync();

        var dto = new TaskDetailDto
        {
            Id = task.FID,
            UID = task.FUID,
            Title = task.FTitle,
            Description = task.FDescription,
            OrgId = task.FOrgId,
            ProjectId = task.FProjectId,
            ProjectName = projectName,
            GoalId = task.FGoalId,
            GoalTitle = goalTitle,
            KRId = task.FKRId,
            KRTitle = krTitle,
            ParentTaskId = task.FParentTaskId,
            Type = task.FType,
            Priority = task.FPriority,
            Status = task.FStatus,
            AssigneeId = task.FAssigneeId,
            AssigneeName = task.FAssigneeId.HasValue ? userDict.GetValueOrDefault(task.FAssigneeId.Value) : null,
            CreatorId = task.FCreatorId,
            CreatorName = userDict.GetValueOrDefault(task.FCreatorId),
            PlanStart = task.FPlanStart,
            PlanEnd = task.FPlanEnd,
            ActualStart = task.FActualStart,
            ActualEnd = task.FActualEnd,
            EstimatedHours = task.FEstimatedHours,
            ActualHours = task.FActualHours,
            Progress = task.FProgress,
            Visibility = task.FVisibility,
            IsTemplate = task.FIsTemplate,
            Code = task.FCode,
            Sort = task.FSort,
            CreateTime = task.FCreateTime,
            UpdateTime = task.FUpdateTime,
            SubTasks = subTasks.Select(s => MapTaskToListDto(s, userDict)).ToList(),
            Members = task.Members.Select(m => new TaskMemberDto
            {
                Id = m.FID,
                TaskId = m.FTaskId,
                UserId = m.FUserId,
                UserName = userDict.GetValueOrDefault(m.FUserId),
                Role = m.FRole
            }).ToList(),
            Tags = task.Tags.Where(tt => tt.Tag != null).Select(tt => new TagSimpleDto
            {
                Id = tt.Tag!.FID,
                Name = tt.Tag.FName,
                Color = tt.Tag.FColor
            }).ToList(),
            Dependencies = task.Dependencies.Select(d => new TaskDependencyDto
            {
                Id = d.FID,
                TaskId = d.FTaskId,
                DependsOnTaskId = d.FDependsOnTaskId,
                DependsOnTaskTitle = d.DependsOnTask?.FTitle,
                DependsOnTaskStatus = d.DependsOnTask?.FStatus ?? 0,
                DependencyType = d.FDependencyType
            }).ToList()
        };

        return ApiResult<TaskDetailDto>.Success(dto);
    }

    public async Task<ApiResult<TaskDetailDto>> CreateAsync(CreateTaskRequest request, long orgId, long creatorId)
    {
        // 生成编号 TM-XXX
        var maxCode = await _db.Set<TmTask>()
            .Where(t => t.FOrgId == orgId && t.FCode != null)
            .OrderByDescending(t => t.FCode)
            .Select(t => t.FCode)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (maxCode != null && maxCode.StartsWith("TM-") && int.TryParse(maxCode.Substring(3), out var current))
            nextNum = current + 1;

        var code = $"TM-{nextNum:D3}";

        var task = new TmTask
        {
            FTitle = request.Title,
            FDescription = request.Description,
            FOrgId = orgId,
            FProjectId = request.ProjectId,
            FGoalId = request.GoalId,
            FKRId = request.KRId,
            FParentTaskId = request.ParentTaskId,
            FType = request.Type,
            FPriority = request.Priority,
            FAssigneeId = request.AssigneeId,
            FCreatorId = creatorId,
            FPlanStart = request.PlanStart,
            FPlanEnd = request.PlanEnd,
            FEstimatedHours = request.EstimatedHours,
            FVisibility = request.Visibility,
            FCode = code,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmTask>().Add(task);
        await _db.SaveChangesAsync();

        // 添加标签
        if (request.TagIds != null && request.TagIds.Count > 0)
        {
            foreach (var tagId in request.TagIds)
            {
                _db.Set<TmTaskTag>().Add(new TmTaskTag { FTaskId = task.FID, FTagId = tagId });
            }
            await _db.SaveChangesAsync();
        }

        // 添加参与者
        if (request.MemberUserIds != null && request.MemberUserIds.Count > 0)
        {
            foreach (var userId in request.MemberUserIds)
            {
                _db.Set<TmTaskMember>().Add(new TmTaskMember { FTaskId = task.FID, FUserId = userId, FRole = 2 });
            }
            await _db.SaveChangesAsync();
        }

        // 创建人自动加为参与者（负责人角色）
        var creatorExists = await _db.Set<TmTaskMember>().AnyAsync(m => m.FTaskId == task.FID && m.FUserId == creatorId);
        if (!creatorExists)
        {
            _db.Set<TmTaskMember>().Add(new TmTaskMember { FTaskId = task.FID, FUserId = creatorId, FRole = 0 });
            await _db.SaveChangesAsync();
        }

        return await GetByIdAsync(task.FID);
    }

    public async Task<ApiResult<TaskDetailDto>> UpdateAsync(long id, UpdateTaskRequest request)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<TaskDetailDto>.Fail("任务不存在");

        task.FTitle = request.Title;
        task.FDescription = request.Description;
        task.FProjectId = request.ProjectId;
        task.FGoalId = request.GoalId;
        task.FKRId = request.KRId;
        task.FType = request.Type;
        task.FPriority = request.Priority;
        task.FAssigneeId = request.AssigneeId;
        task.FPlanStart = request.PlanStart;
        task.FPlanEnd = request.PlanEnd;
        task.FEstimatedHours = request.EstimatedHours;
        task.FVisibility = request.Visibility;
        task.FUpdateTime = DateTime.Now;

        // 更新标签
        if (request.TagIds != null)
        {
            var existingTags = await _db.Set<TmTaskTag>().Where(tt => tt.FTaskId == id).ToListAsync();
            _db.Set<TmTaskTag>().RemoveRange(existingTags);
            foreach (var tagId in request.TagIds)
            {
                _db.Set<TmTaskTag>().Add(new TmTaskTag { FTaskId = id, FTagId = tagId });
            }
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<TaskDetailDto>> ChangeStatusAsync(long id, ChangeTaskStatusRequest request)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<TaskDetailDto>.Fail("任务不存在");

        // 状态流转校验: 0待办/1进行中/2已完成/3已取消/4已延期
        var validTransitions = new Dictionary<int, int[]>
        {
            { 0, new[] { 1, 3 } },           // 待办 → 进行中/已取消
            { 1, new[] { 0, 2, 3, 4 } },     // 进行中 → 待办/已完成/已取消/已延期
            { 2, new[] { 1 } },               // 已完成 → 进行中(重新开启)
            { 3, new[] { 0 } },               // 已取消 → 待办(恢复)
            { 4, new[] { 1, 3 } }             // 已延期 → 进行中/已取消
        };

        if (validTransitions.TryGetValue(task.FStatus, out var allowed) && !allowed.Contains(request.Status))
            return ApiResult<TaskDetailDto>.Fail($"不允许从状态{task.FStatus}变更到状态{request.Status}");

        var oldStatus = task.FStatus;
        task.FStatus = request.Status;
        task.FUpdateTime = DateTime.Now;

        // 自动设置实际时间
        if (request.Status == 1 && task.FActualStart == null)
            task.FActualStart = DateTime.Now;
        if (request.Status == 2)
        {
            task.FActualEnd = DateTime.Now;
            task.FProgress = 100;
        }

        await _db.SaveChangesAsync();

        // 发布任务状态变更事件
        try
        {
            await _eventDispatcher.PublishAsync(new TaskStatusChangedEvent
            {
                TaskId = task.FID,
                TaskTitle = task.FTitle,
                OldStatus = oldStatus.ToString(),
                NewStatus = request.Status.ToString(),
                ChangedByUserId = task.FAssigneeId ?? task.FCreatorId,
                TriggeredByUserId = task.FAssigneeId ?? task.FCreatorId,
                ModuleCode = "task"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发布任务状态变更事件失败，TaskId={TaskId}", task.FID);
        }

        // 任务完成后触发积分
        if (request.Status == 2)
        {
            try
            {
                var isOnTime = task.FActualEnd <= task.FPlanEnd;
                await _pointService.TriggerEventAsync(new PointEventDto
                {
                    EventType = isOnTime ? "task.completed" : "task.overdue",
                    UserId = task.FAssigneeId ?? task.FCreatorId,
                    OrgId = task.FOrgId,
                    SourceModule = "task",
                    EntityType = "task",
                    EntityId = task.FID,
                    Context = new Dictionary<string, object>
                    {
                        { "Priority", task.FPriority },
                        { "TaskCode", task.FCode ?? "" }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "任务完成积分触发失败，TaskId={TaskId}", task.FID);
            }
        }

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<TaskDetailDto>> SetPriorityAsync(long id, int priority)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<TaskDetailDto>.Fail("任务不存在");

        task.FPriority = priority;
        task.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<TaskDetailDto>> AssignAsync(long id, AssignTaskRequest request)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<TaskDetailDto>.Fail("任务不存在");

        task.FAssigneeId = request.AssigneeId;
        task.FUpdateTime = DateTime.Now;

        // 将执行人加入参与者
        if (request.AssigneeId.HasValue)
        {
            var memberExists = await _db.Set<TmTaskMember>()
                .AnyAsync(m => m.FTaskId == id && m.FUserId == request.AssigneeId.Value);
            if (!memberExists)
            {
                _db.Set<TmTaskMember>().Add(new TmTaskMember
                {
                    FTaskId = id,
                    FUserId = request.AssigneeId.Value,
                    FRole = 1 // 执行人
                });
            }
        }

        await _db.SaveChangesAsync();

        // 发布任务分配事件
        if (request.AssigneeId.HasValue)
        {
            try
            {
                await _eventDispatcher.PublishAsync(new TaskAssignedEvent
                {
                    TaskId = task.FID,
                    TaskTitle = task.FTitle,
                    AssignerId = task.FCreatorId,
                    AssigneeId = request.AssigneeId.Value,
                    Deadline = task.FPlanEnd,
                    OrgId = task.FOrgId,
                    TriggeredByUserId = task.FCreatorId,
                    ModuleCode = "task"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发布任务分配事件失败，TaskId={TaskId}", task.FID);
            }
        }

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<TaskDetailDto>> CreateSubtaskAsync(long parentId, CreateTaskRequest request, long orgId, long creatorId)
    {
        var parent = await _db.Set<TmTask>().FirstOrDefaultAsync(t => t.FID == parentId);
        if (parent == null)
            return ApiResult<TaskDetailDto>.Fail("父任务不存在");

        request.ParentTaskId = parentId;
        request.ProjectId ??= parent.FProjectId;
        request.GoalId ??= parent.FGoalId;
        request.KRId ??= parent.FKRId;

        return await CreateAsync(request, orgId, creatorId);
    }

    public async Task<ApiResult<List<TaskListDto>>> GetSubtasksAsync(long parentId)
    {
        var tasks = await _db.Set<TmTask>()
            .Where(t => t.FParentTaskId == parentId)
            .OrderBy(t => t.FSort)
            .ThenByDescending(t => t.FCreateTime)
            .ToListAsync();

        var dtos = await EnrichTaskListDtos(tasks);
        return ApiResult<List<TaskListDto>>.Success(dtos);
    }

    public async Task<ApiResult<bool>> SetVisibilityAsync(long id, SetTaskVisibilityRequest request)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<bool>.Fail("任务不存在");

        task.FVisibility = request.Visibility;
        task.FUpdateTime = DateTime.Now;

        // 更新自定义可见范围规则
        var existingRules = await _db.Set<TmTaskVisibility>().Where(v => v.FTaskId == id).ToListAsync();
        _db.Set<TmTaskVisibility>().RemoveRange(existingRules);

        if (request.Rules != null)
        {
            foreach (var rule in request.Rules)
            {
                _db.Set<TmTaskVisibility>().Add(new TmTaskVisibility
                {
                    FTaskId = id,
                    FTargetType = rule.TargetType,
                    FTargetId = rule.TargetId
                });
            }
        }

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<List<TaskDependencyDto>>> GetDependenciesAsync(long id)
    {
        var deps = await _db.Set<TmTaskDependency>()
            .Include(d => d.DependsOnTask)
            .Where(d => d.FTaskId == id)
            .ToListAsync();

        var dtos = deps.Select(d => new TaskDependencyDto
        {
            Id = d.FID,
            TaskId = d.FTaskId,
            DependsOnTaskId = d.FDependsOnTaskId,
            DependsOnTaskTitle = d.DependsOnTask?.FTitle,
            DependsOnTaskStatus = d.DependsOnTask?.FStatus ?? 0,
            DependencyType = d.FDependencyType
        }).ToList();

        return ApiResult<List<TaskDependencyDto>>.Success(dtos);
    }

    public async Task<ApiResult<TaskDependencyDto>> AddDependencyAsync(long id, AddTaskDependencyRequest request)
    {
        if (id == request.DependsOnTaskId)
            return ApiResult<TaskDependencyDto>.Fail("任务不能依赖自身");

        var exists = await _db.Set<TmTaskDependency>()
            .AnyAsync(d => d.FTaskId == id && d.FDependsOnTaskId == request.DependsOnTaskId);
        if (exists)
            return ApiResult<TaskDependencyDto>.Fail("依赖关系已存在");

        // 循环依赖检测 (BFS)
        if (await HasCircularDependency(id, request.DependsOnTaskId))
            return ApiResult<TaskDependencyDto>.Fail("检测到循环依赖，无法添加");

        var dep = new TmTaskDependency
        {
            FTaskId = id,
            FDependsOnTaskId = request.DependsOnTaskId,
            FDependencyType = request.DependencyType
        };

        _db.Set<TmTaskDependency>().Add(dep);
        await _db.SaveChangesAsync();

        var depTask = await _db.Set<TmTask>().FirstOrDefaultAsync(t => t.FID == request.DependsOnTaskId);

        return ApiResult<TaskDependencyDto>.Success(new TaskDependencyDto
        {
            Id = dep.FID,
            TaskId = dep.FTaskId,
            DependsOnTaskId = dep.FDependsOnTaskId,
            DependsOnTaskTitle = depTask?.FTitle,
            DependsOnTaskStatus = depTask?.FStatus ?? 0,
            DependencyType = dep.FDependencyType
        });
    }

    public async Task<ApiResult<bool>> RemoveDependencyAsync(long id, long depId)
    {
        var dep = await _db.Set<TmTaskDependency>()
            .FirstOrDefaultAsync(d => d.FID == depId && d.FTaskId == id);
        if (dep == null)
            return ApiResult<bool>.Fail("依赖关系不存在");

        _db.Set<TmTaskDependency>().Remove(dep);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "移除成功");
    }

    public async Task<ApiResult<List<TagSimpleDto>>> GetTagsAsync(long id)
    {
        var tags = await _db.Set<TmTaskTag>()
            .Include(tt => tt.Tag)
            .Where(tt => tt.FTaskId == id && tt.Tag != null)
            .Select(tt => new TagSimpleDto
            {
                Id = tt.Tag!.FID,
                Name = tt.Tag.FName,
                Color = tt.Tag.FColor
            })
            .ToListAsync();

        return ApiResult<List<TagSimpleDto>>.Success(tags);
    }

    public async Task<ApiResult<bool>> SetTagsAsync(long id, SetTaskTagsRequest request)
    {
        var taskExists = await _db.Set<TmTask>().AnyAsync(t => t.FID == id);
        if (!taskExists)
            return ApiResult<bool>.Fail("任务不存在");

        var existing = await _db.Set<TmTaskTag>().Where(tt => tt.FTaskId == id).ToListAsync();
        _db.Set<TmTaskTag>().RemoveRange(existing);

        foreach (var tagId in request.TagIds)
        {
            _db.Set<TmTaskTag>().Add(new TmTaskTag { FTaskId = id, FTagId = tagId });
        }

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<int> GetMyPendingCountAsync(long orgId, long currentUserId)
    {
        var taskIds = await _db.Set<TmTaskMember>()
            .Where(m => m.FUserId == currentUserId)
            .Select(m => m.FTaskId)
            .Distinct()
            .ToListAsync();

        return await _db.Set<TmTask>()
            .Where(t => t.FOrgId == orgId && !t.FIsTemplate && t.FParentTaskId == 0
                     && (t.FStatus == 0 || t.FStatus == 1) // 待办或进行中
                     && (t.FAssigneeId == currentUserId || t.FCreatorId == currentUserId || taskIds.Contains(t.FID)))
            .CountAsync();
    }

    public async Task<ApiResult<PagedResult<TaskListDto>>> GetMyTasksAsync(long orgId, long currentUserId)
    {
        var taskIds = await _db.Set<TmTaskMember>()
            .Where(m => m.FUserId == currentUserId)
            .Select(m => m.FTaskId)
            .Distinct()
            .ToListAsync();

        var q = _db.Set<TmTask>()
            .Where(t => t.FOrgId == orgId && !t.FIsTemplate &&
                        (t.FAssigneeId == currentUserId || t.FCreatorId == currentUserId || taskIds.Contains(t.FID)))
            .Where(t => t.FParentTaskId == 0);

        var total = await q.CountAsync();
        var tasks = await q.OrderByDescending(t => t.FCreateTime).Take(100).ToListAsync();

        var dtos = await EnrichTaskListDtos(tasks);

        return ApiResult<PagedResult<TaskListDto>>.Success(new PagedResult<TaskListDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = 1,
            PageSize = 100
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var task = await _db.Set<TmTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (task == null)
            return ApiResult<bool>.Fail("任务不存在");

        // 检查是否有子任务
        var hasChildren = await _db.Set<TmTask>().AnyAsync(t => t.FParentTaskId == id);
        if (hasChildren)
            return ApiResult<bool>.Fail("该任务存在子任务，请先删除子任务");

        // 删除关联数据
        var tags = await _db.Set<TmTaskTag>().Where(t => t.FTaskId == id).ToListAsync();
        _db.Set<TmTaskTag>().RemoveRange(tags);

        var members = await _db.Set<TmTaskMember>().Where(m => m.FTaskId == id).ToListAsync();
        _db.Set<TmTaskMember>().RemoveRange(members);

        var deps = await _db.Set<TmTaskDependency>().Where(d => d.FTaskId == id || d.FDependsOnTaskId == id).ToListAsync();
        _db.Set<TmTaskDependency>().RemoveRange(deps);

        var visibility = await _db.Set<TmTaskVisibility>().Where(v => v.FTaskId == id).ToListAsync();
        _db.Set<TmTaskVisibility>().RemoveRange(visibility);

        var comments = await _db.Set<TmTaskComment>().Where(c => c.FTaskId == id).ToListAsync();
        _db.Set<TmTaskComment>().RemoveRange(comments);

        var reminders = await _db.Set<TmTaskReminder>().Where(r => r.FTaskId == id).ToListAsync();
        _db.Set<TmTaskReminder>().RemoveRange(reminders);

        var attachments = await _db.Set<TmAttachment>().Where(a => a.FRelationId == id).ToListAsync();
        _db.Set<TmAttachment>().RemoveRange(attachments);

        _db.Set<TmTask>().Remove(task);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<bool>> BatchUpdateAsync(List<long> taskIds, int? status, long? assigneeId)
    {
        if (taskIds == null || taskIds.Count == 0)
            return ApiResult<bool>.Fail("请选择要更新的任务");

        if (!status.HasValue && !assigneeId.HasValue)
            return ApiResult<bool>.Fail("请提供要更新的字段");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var tasks = await _db.Set<TmTask>().AsTracking()
                .Where(t => taskIds.Contains(t.FID))
                .ToListAsync();

            if (tasks.Count != taskIds.Count)
                return ApiResult<bool>.Fail("部分任务不存在");

            foreach (var task in tasks)
            {
                if (status.HasValue)
                    task.FStatus = status.Value;
                if (assigneeId.HasValue)
                    task.FAssigneeId = assigneeId.Value;
                task.FUpdateTime = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResult<bool>.Success(true, "批量更新成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "批量更新任务失败，TaskIds={TaskIds}", string.Join(",", taskIds));
            return ApiResult<bool>.Fail("批量更新失败");
        }
    }

    #region Private Helpers

    /// <summary>
    /// 可见范围过滤逻辑:
    /// 0公开 → 同组织所有人可见
    /// 1项目内 → 项目成员可见
    /// 2仅参与者 → 任务参与者可见
    /// 3私密 → 仅创建人和执行人可见
    /// + admin可见所有
    /// + 模板任务不显示
    /// </summary>
    private IQueryable<TmTask> GetVisibleTasksQuery(long orgId, long currentUserId, bool isAdmin)
    {
        var q = _db.Set<TmTask>().Where(t => t.FOrgId == orgId);

        if (isAdmin)
            return q;

        // 获取当前用户参与的项目ID列表（子查询）
        var memberProjectIds = _db.Set<TmProjectMember>()
            .Where(pm => pm.FUserId == currentUserId)
            .Select(pm => pm.FProjectId);

        // 获取当前用户参与的任务ID列表（子查询）
        var memberTaskIds = _db.Set<TmTaskMember>()
            .Where(tm => tm.FUserId == currentUserId)
            .Select(tm => tm.FTaskId);

        // 获取自定义可见范围中包含当前用户的任务ID
        var customVisibleTaskIds = _db.Set<TmTaskVisibility>()
            .Where(v => (v.FTargetType == 2 && v.FTargetId == currentUserId))
            .Select(v => v.FTaskId);

        q = q.Where(t =>
            t.FVisibility == 0 ||                                                                    // 公开
            (t.FVisibility == 1 && t.FProjectId.HasValue && memberProjectIds.Contains(t.FProjectId.Value)) || // 项目内
            (t.FVisibility == 2 && memberTaskIds.Contains(t.FID)) ||                                 // 仅参与者
            (t.FVisibility == 3 && (t.FCreatorId == currentUserId || t.FAssigneeId == currentUserId)) || // 私密
            t.FCreatorId == currentUserId ||                                                          // 创建人始终可见
            t.FAssigneeId == currentUserId ||                                                         // 执行人始终可见
            memberTaskIds.Contains(t.FID) ||                                                          // 参与者始终可见
            customVisibleTaskIds.Contains(t.FID)                                                      // 自定义范围
        );

        return q;
    }

    /// <summary>
    /// BFS检测循环依赖：如果从 dependsOnTaskId 出发能到达 taskId，则存在环
    /// </summary>
    private async Task<bool> HasCircularDependency(long taskId, long dependsOnTaskId)
    {
        var visited = new HashSet<long>();
        var queue = new Queue<long>();
        queue.Enqueue(dependsOnTaskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == taskId)
                return true;

            if (!visited.Add(current))
                continue;

            var nextDeps = await _db.Set<TmTaskDependency>()
                .Where(d => d.FTaskId == current)
                .Select(d => d.FDependsOnTaskId)
                .ToListAsync();

            foreach (var next in nextDeps)
            {
                if (!visited.Contains(next))
                    queue.Enqueue(next);
            }
        }

        return false;
    }

    private async Task<List<TaskListDto>> EnrichTaskListDtos(List<TmTask> tasks)
    {
        if (tasks.Count == 0) return new List<TaskListDto>();

        var taskIds = tasks.Select(t => t.FID).ToList();

        // 用户名
        var userIds = new List<long>();
        userIds.AddRange(tasks.Where(t => t.FAssigneeId.HasValue).Select(t => t.FAssigneeId!.Value));
        userIds.AddRange(tasks.Select(t => t.FCreatorId));
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 项目名称
        var projectIds = tasks.Where(t => t.FProjectId.HasValue).Select(t => t.FProjectId!.Value).Distinct().ToList();
        var projectDict = projectIds.Count > 0
            ? await _db.Set<TmProject>().Where(p => projectIds.Contains(p.FID)).Select(p => new { p.FID, p.FName }).ToDictionaryAsync(p => p.FID, p => p.FName)
            : new Dictionary<long, string>();

        // 子任务统计
        var subCounts = await _db.Set<TmTask>()
            .Where(t => taskIds.Contains(t.FParentTaskId))
            .GroupBy(t => t.FParentTaskId)
            .Select(g => new { ParentId = g.Key, Total = g.Count(), Completed = g.Count(t => t.FStatus == 2) })
            .ToListAsync();
        var subCountDict = subCounts.ToDictionary(x => x.ParentId);

        // 标签
        var tagLinks = await _db.Set<TmTaskTag>().Where(tt => taskIds.Contains(tt.FTaskId)).ToListAsync();
        var tagIds = tagLinks.Select(tl => tl.FTagId).Distinct().ToList();
        var tags = tagIds.Count > 0
            ? await _db.Set<TmTag>().Where(t => tagIds.Contains(t.FID)).ToDictionaryAsync(t => t.FID)
            : new Dictionary<long, TmTag>();

        return tasks.Select(t =>
        {
            var dto = MapTaskToListDto(t, userDict);
            dto.ProjectName = t.FProjectId.HasValue ? projectDict.GetValueOrDefault(t.FProjectId.Value) : null;
            if (subCountDict.TryGetValue(t.FID, out var sc))
            {
                dto.SubTaskCount = sc.Total;
                dto.CompletedSubTaskCount = sc.Completed;
            }
            dto.Tags = tagLinks.Where(tl => tl.FTaskId == t.FID && tags.ContainsKey(tl.FTagId))
                .Select(tl => new TagSimpleDto { Id = tags[tl.FTagId].FID, Name = tags[tl.FTagId].FName, Color = tags[tl.FTagId].FColor })
                .ToList();
            return dto;
        }).ToList();
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
        AssigneeName = t.FAssigneeId.HasValue ? userDict.GetValueOrDefault(t.FAssigneeId.Value) : null,
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
