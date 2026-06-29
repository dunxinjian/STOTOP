using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class ProjectService : IProjectService
{
    private readonly STOTOPDbContext _db;

    public ProjectService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<ProjectListDto>>> GetPagedListAsync(ProjectPagedRequest query, long orgId)
    {
        var q = _db.Set<TmProject>().Where(p => p.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(p => p.FName.Contains(kw));
        }
        if (query.Status.HasValue)
            q = q.Where(p => p.FStatus == query.Status.Value);
        if (query.ManagerId.HasValue)
            q = q.Where(p => p.FManagerId == query.ManagerId.Value);

        var total = await q.CountAsync();

        var projects = await q
            .OrderByDescending(p => p.FCreateTime)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var projectIds = projects.Select(p => p.FID).ToList();

        // 成员统计
        var memberCounts = await _db.Set<TmProjectMember>()
            .Where(m => projectIds.Contains(m.FProjectId))
            .GroupBy(m => m.FProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync();
        var memberCountDict = memberCounts.ToDictionary(x => x.ProjectId, x => x.Count);

        // 任务统计
        var taskStats = await _db.Set<TmTask>()
            .Where(t => t.FProjectId.HasValue && projectIds.Contains(t.FProjectId.Value) && !t.FIsTemplate && t.FParentTaskId == 0)
            .GroupBy(t => t.FProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Total = g.Count(), Completed = g.Count(t => t.FStatus == 2) })
            .ToListAsync();
        var taskStatDict = taskStats.ToDictionary(x => x.ProjectId);

        // 用户名
        var userIds = new List<long>();
        userIds.AddRange(projects.Select(p => p.FManagerId));
        userIds.AddRange(projects.Where(p => p.FGoalId.HasValue).Select(p => p.FGoalId!.Value)); // for goal titles
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 目标标题
        var goalIds = projects.Where(p => p.FGoalId.HasValue).Select(p => p.FGoalId!.Value).Distinct().ToList();
        var goalTitles = goalIds.Count > 0
            ? await _db.Set<TmGoal>().Where(g => goalIds.Contains(g.FID)).Select(g => new { g.FID, g.FTitle }).ToDictionaryAsync(g => g.FID, g => g.FTitle)
            : new Dictionary<long, string>();

        var items = projects.Select(p =>
        {
            var dto = new ProjectListDto
            {
                Id = p.FID,
                UID = p.FUID,
                Name = p.FName,
                Description = p.FDescription,
                OrgId = p.FOrgId,
                GoalId = p.FGoalId,
                GoalTitle = p.FGoalId.HasValue && goalTitles.ContainsKey(p.FGoalId.Value) ? goalTitles[p.FGoalId.Value] : null,
                ManagerId = p.FManagerId,
                ManagerName = userDict.GetValueOrDefault(p.FManagerId),
                StartDate = p.FStartDate,
                EndDate = p.FEndDate,
                Status = p.FStatus,
                MemberCount = memberCountDict.GetValueOrDefault(p.FID, 0),
                TaskCount = taskStatDict.TryGetValue(p.FID, out var ts) ? ts.Total : 0,
                CompletedTaskCount = taskStatDict.TryGetValue(p.FID, out var ts2) ? ts2.Completed : 0,
                CreateTime = p.FCreateTime,
                UpdateTime = p.FUpdateTime
            };
            return dto;
        }).ToList();

        return ApiResult<PagedResult<ProjectListDto>>.Success(new PagedResult<ProjectListDto>
        {
            Items = items,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        });
    }

    public async Task<ApiResult<ProjectDetailDto>> GetByIdAsync(long id)
    {
        var project = await _db.Set<TmProject>()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.FID == id);

        if (project == null)
            return ApiResult<ProjectDetailDto>.Fail("项目不存在");

        // 用户名
        var userIds = new List<long> { project.FManagerId, project.FCreatorId };
        userIds.AddRange(project.Members.Select(m => m.FUserId));
        var userDict = await GetUserNameDict(userIds.Distinct().ToList());

        // 目标标题
        string? goalTitle = null;
        if (project.FGoalId.HasValue)
        {
            goalTitle = await _db.Set<TmGoal>()
                .Where(g => g.FID == project.FGoalId.Value)
                .Select(g => g.FTitle)
                .FirstOrDefaultAsync();
        }

        // 任务统计
        var taskStats = await _db.Set<TmTask>()
            .Where(t => t.FProjectId == id && !t.FIsTemplate && t.FParentTaskId == 0)
            .GroupBy(t => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(t => t.FStatus == 2),
                InProgress = g.Count(t => t.FStatus == 1),
                Overdue = g.Count(t => t.FStatus == 4)
            })
            .FirstOrDefaultAsync();

        var dto = new ProjectDetailDto
        {
            Id = project.FID,
            UID = project.FUID,
            Name = project.FName,
            Description = project.FDescription,
            OrgId = project.FOrgId,
            GoalId = project.FGoalId,
            GoalTitle = goalTitle,
            ManagerId = project.FManagerId,
            ManagerName = userDict.GetValueOrDefault(project.FManagerId),
            StartDate = project.FStartDate,
            EndDate = project.FEndDate,
            Status = project.FStatus,
            CreatorId = project.FCreatorId,
            CreatorName = userDict.GetValueOrDefault(project.FCreatorId),
            CreateTime = project.FCreateTime,
            UpdateTime = project.FUpdateTime,
            TaskCount = taskStats?.Total ?? 0,
            CompletedTaskCount = taskStats?.Completed ?? 0,
            InProgressTaskCount = taskStats?.InProgress ?? 0,
            OverdueTaskCount = taskStats?.Overdue ?? 0,
            Members = project.Members.OrderBy(m => m.FRole).ThenBy(m => m.FJoinTime).Select(m => new ProjectMemberDto
            {
                Id = m.FID,
                ProjectId = m.FProjectId,
                UserId = m.FUserId,
                UserName = userDict.GetValueOrDefault(m.FUserId),
                Role = m.FRole,
                JoinTime = m.FJoinTime
            }).ToList()
        };

        return ApiResult<ProjectDetailDto>.Success(dto);
    }

    public async Task<ApiResult<ProjectDetailDto>> CreateAsync(CreateProjectRequest request, long orgId, long creatorId)
    {
        var project = new TmProject
        {
            FName = request.Name,
            FDescription = request.Description,
            FOrgId = orgId,
            FGoalId = request.GoalId,
            FManagerId = request.ManagerId,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FCreatorId = creatorId,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmProject>().Add(project);
        await _db.SaveChangesAsync();

        // 自动将负责人加为成员(角色0=负责人)
        var member = new TmProjectMember
        {
            FProjectId = project.FID,
            FUserId = request.ManagerId,
            FRole = 0,
            FJoinTime = DateTime.Now
        };
        _db.Set<TmProjectMember>().Add(member);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(project.FID);
    }

    public async Task<ApiResult<ProjectDetailDto>> UpdateAsync(long id, UpdateProjectRequest request)
    {
        var project = await _db.Set<TmProject>().AsTracking().FirstOrDefaultAsync(p => p.FID == id);
        if (project == null)
            return ApiResult<ProjectDetailDto>.Fail("项目不存在");

        project.FName = request.Name;
        project.FDescription = request.Description;
        project.FGoalId = request.GoalId;
        project.FManagerId = request.ManagerId;
        project.FStartDate = request.StartDate;
        project.FEndDate = request.EndDate;
        project.FStatus = request.Status;
        project.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<List<ProjectMemberDto>>> GetMembersAsync(long projectId)
    {
        var members = await _db.Set<TmProjectMember>()
            .Where(m => m.FProjectId == projectId)
            .OrderBy(m => m.FRole)
            .ThenBy(m => m.FJoinTime)
            .ToListAsync();

        var userIds = members.Select(m => m.FUserId).Distinct().ToList();
        var userDict = await GetUserNameDict(userIds);

        var dtos = members.Select(m => new ProjectMemberDto
        {
            Id = m.FID,
            ProjectId = m.FProjectId,
            UserId = m.FUserId,
            UserName = userDict.GetValueOrDefault(m.FUserId),
            Role = m.FRole,
            JoinTime = m.FJoinTime
        }).ToList();

        return ApiResult<List<ProjectMemberDto>>.Success(dtos);
    }

    public async Task<ApiResult<ProjectMemberDto>> AddMemberAsync(long projectId, AddProjectMemberRequest request)
    {
        var exists = await _db.Set<TmProjectMember>()
            .AnyAsync(m => m.FProjectId == projectId && m.FUserId == request.UserId);

        if (exists)
            return ApiResult<ProjectMemberDto>.Fail("该用户已是项目成员");

        var member = new TmProjectMember
        {
            FProjectId = projectId,
            FUserId = request.UserId,
            FRole = request.Role,
            FJoinTime = DateTime.Now
        };

        _db.Set<TmProjectMember>().Add(member);
        await _db.SaveChangesAsync();

        var userDict = await GetUserNameDict(new List<long> { request.UserId });

        return ApiResult<ProjectMemberDto>.Success(new ProjectMemberDto
        {
            Id = member.FID,
            ProjectId = member.FProjectId,
            UserId = member.FUserId,
            UserName = userDict.GetValueOrDefault(member.FUserId),
            Role = member.FRole,
            JoinTime = member.FJoinTime
        });
    }

    public async Task<ApiResult<bool>> RemoveMemberAsync(long projectId, long userId)
    {
        var member = await _db.Set<TmProjectMember>()
            .FirstOrDefaultAsync(m => m.FProjectId == projectId && m.FUserId == userId);

        if (member == null)
            return ApiResult<bool>.Fail("成员不存在");

        _db.Set<TmProjectMember>().Remove(member);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "移除成功");
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

    #endregion
}
