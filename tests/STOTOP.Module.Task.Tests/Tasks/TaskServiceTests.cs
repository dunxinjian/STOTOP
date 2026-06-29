using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;
using Xunit;

namespace STOTOP.Module.Task.Tests.Tasks;

using Task = global::System.Threading.Tasks.Task;

public class TaskServiceTests
{
    private static TaskService CreateService(STOTOPDbContext db)
        => new TaskService(
            db,
            TaskTestFakes.PointService(),
            TaskTestFakes.EventDispatcher(),
            TaskTestFakes.Logger<TaskService>());

    // 直接落库一个任务（绕过 CreateAsync 的编号/成员副作用），FOrgId 由组织上下文自动回填
    private static async Task<TmTask> SeedTaskAsync(
        STOTOPDbContext db,
        long id,
        long creatorId = 1,
        long? assigneeId = null,
        int status = 0,
        int visibility = 0,
        long parentTaskId = 0,
        bool isTemplate = false,
        long? projectId = null,
        long? orgId = null)
    {
        var task = new TmTask
        {
            FID = id,
            FTitle = "任务" + id,
            FCreatorId = creatorId,
            FAssigneeId = assigneeId,
            FStatus = status,
            FVisibility = visibility,
            FParentTaskId = parentTaskId,
            FIsTemplate = isTemplate,
            FProjectId = projectId,
            FCode = $"TM-{id:D3}"
        };
        if (orgId.HasValue) task.FOrgId = orgId.Value;
        db.Set<TmTask>().Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    [Fact]
    public async Task 新建任务自动生成编号与默认待办状态并把创建人加为成员()
    {
        await using var db = TestDbContextFactory.Create(nameof(新建任务自动生成编号与默认待办状态并把创建人加为成员), orgId: 1);
        var svc = CreateService(db);

        var result = await svc.CreateAsync(new CreateTaskRequest { Title = "首个任务" }, orgId: 1, creatorId: 7);

        Assert.Equal(200, result.Code); // ApiResult 成功约定 Code=200（见 STOTOP.Core.Models.ApiResult）
        Assert.NotNull(result.Data);
        Assert.Equal("TM-001", result.Data!.Code);
        Assert.Equal(0, result.Data.Status); // 默认待办

        // 创建人以负责人角色(FRole=0)自动成为参与者
        var member = await db.Set<TmTaskMember>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.FTaskId == result.Data.Id && m.FUserId == 7);
        Assert.NotNull(member);
        Assert.Equal(0, member!.FRole);
    }

    [Fact]
    public async Task 任务编号按组织内最大编号递增()
    {
        await using var db = TestDbContextFactory.Create(nameof(任务编号按组织内最大编号递增), orgId: 1);
        await SeedTaskAsync(db, id: 100); // 已存在 TM-100
        var svc = CreateService(db);

        var result = await svc.CreateAsync(new CreateTaskRequest { Title = "下一个" }, orgId: 1, creatorId: 1);

        Assert.Equal(200, result.Code);
        Assert.Equal("TM-101", result.Data!.Code);
    }

    [Fact]
    public async Task 状态从待办进行中会自动设置实际开始时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(状态从待办进行中会自动设置实际开始时间), orgId: 1);
        await SeedTaskAsync(db, id: 1, status: 0);
        var svc = CreateService(db);

        var result = await svc.ChangeStatusAsync(1, new ChangeTaskStatusRequest { Status = 1 });

        Assert.Equal(200, result.Code);
        var task = await db.Set<TmTask>().AsNoTracking().FirstAsync(t => t.FID == 1);
        Assert.Equal(1, task.FStatus);
        Assert.NotNull(task.FActualStart);
    }

    [Fact]
    public async Task 状态完成会设置进度100与实际结束时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(状态完成会设置进度100与实际结束时间), orgId: 1);
        await SeedTaskAsync(db, id: 1, status: 1); // 进行中 → 已完成
        var svc = CreateService(db);

        var result = await svc.ChangeStatusAsync(1, new ChangeTaskStatusRequest { Status = 2 });

        Assert.Equal(200, result.Code);
        var task = await db.Set<TmTask>().AsNoTracking().FirstAsync(t => t.FID == 1);
        Assert.Equal(2, task.FStatus);
        Assert.Equal(100, task.FProgress);
        Assert.NotNull(task.FActualEnd);
    }

    [Fact]
    public async Task 非法状态流转被拒绝且状态不变()
    {
        await using var db = TestDbContextFactory.Create(nameof(非法状态流转被拒绝且状态不变), orgId: 1);
        await SeedTaskAsync(db, id: 1, status: 0); // 待办只允许 → 进行中/已取消
        var svc = CreateService(db);

        var result = await svc.ChangeStatusAsync(1, new ChangeTaskStatusRequest { Status = 2 }); // 待办→已完成 非法

        Assert.NotEqual(200, result.Code);
        var task = await db.Set<TmTask>().AsNoTracking().FirstAsync(t => t.FID == 1);
        Assert.Equal(0, task.FStatus); // 未变更
    }

    [Fact]
    public async Task 自身依赖被拒绝()
    {
        await using var db = TestDbContextFactory.Create(nameof(自身依赖被拒绝), orgId: 1);
        await SeedTaskAsync(db, id: 1);
        var svc = CreateService(db);

        var result = await svc.AddDependencyAsync(1, new AddTaskDependencyRequest { DependsOnTaskId = 1 });

        Assert.NotEqual(200, result.Code);
        Assert.Equal(0, await db.Set<TmTaskDependency>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task 循环依赖被检测并拒绝()
    {
        await using var db = TestDbContextFactory.Create(nameof(循环依赖被检测并拒绝), orgId: 1);
        await SeedTaskAsync(db, id: 1);
        await SeedTaskAsync(db, id: 2);
        var svc = CreateService(db);

        // 1 依赖 2（合法）
        var first = await svc.AddDependencyAsync(1, new AddTaskDependencyRequest { DependsOnTaskId = 2 });
        Assert.Equal(200, first.Code);

        // 2 依赖 1 会成环（1->2->1），应被拒绝
        var second = await svc.AddDependencyAsync(2, new AddTaskDependencyRequest { DependsOnTaskId = 1 });
        Assert.NotEqual(200, second.Code);

        // 只保留第一条合法依赖
        Assert.Equal(1, await db.Set<TmTaskDependency>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task 重复依赖被拒绝()
    {
        await using var db = TestDbContextFactory.Create(nameof(重复依赖被拒绝), orgId: 1);
        await SeedTaskAsync(db, id: 1);
        await SeedTaskAsync(db, id: 2);
        var svc = CreateService(db);

        var first = await svc.AddDependencyAsync(1, new AddTaskDependencyRequest { DependsOnTaskId = 2 });
        Assert.Equal(200, first.Code);

        var dup = await svc.AddDependencyAsync(1, new AddTaskDependencyRequest { DependsOnTaskId = 2 });
        Assert.NotEqual(200, dup.Code);
        Assert.Equal(1, await db.Set<TmTaskDependency>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task 私密任务对无关用户不可见但管理员可见()
    {
        await using var db = TestDbContextFactory.Create(nameof(私密任务对无关用户不可见但管理员可见), orgId: 1);
        // 私密(visibility=3) 任务，创建人=9 执行人=9
        await SeedTaskAsync(db, id: 1, creatorId: 9, assigneeId: 9, visibility: 3);
        var svc = CreateService(db);

        // 无关用户 5：非创建人/执行人/成员 → 不可见
        var outsider = await svc.GetPagedListAsync(new TaskPagedRequest(), orgId: 1, currentUserId: 5, isAdmin: false);
        Assert.Equal(200, outsider.Code);
        Assert.DoesNotContain(outsider.Data!.Items, x => x.Id == 1);

        // 管理员可见所有
        var admin = await svc.GetPagedListAsync(new TaskPagedRequest(), orgId: 1, currentUserId: 5, isAdmin: true);
        Assert.Contains(admin.Data!.Items, x => x.Id == 1);

        // 创建人本人可见
        var owner = await svc.GetPagedListAsync(new TaskPagedRequest(), orgId: 1, currentUserId: 9, isAdmin: false);
        Assert.Contains(owner.Data!.Items, x => x.Id == 1);
    }

    [Fact]
    public async Task 默认分页列表排除模板任务()
    {
        await using var db = TestDbContextFactory.Create(nameof(默认分页列表排除模板任务), orgId: 1);
        await SeedTaskAsync(db, id: 1, creatorId: 1, visibility: 0, isTemplate: false);
        await SeedTaskAsync(db, id: 2, creatorId: 1, visibility: 0, isTemplate: true);
        var svc = CreateService(db);

        var result = await svc.GetPagedListAsync(new TaskPagedRequest(), orgId: 1, currentUserId: 1, isAdmin: true);

        Assert.Equal(200, result.Code);
        Assert.Contains(result.Data!.Items, x => x.Id == 1);
        Assert.DoesNotContain(result.Data.Items, x => x.Id == 2); // 模板被默认排除
    }

    [Fact]
    public async Task 删除存在子任务的任务被拒绝()
    {
        await using var db = TestDbContextFactory.Create(nameof(删除存在子任务的任务被拒绝), orgId: 1);
        await SeedTaskAsync(db, id: 1);
        await SeedTaskAsync(db, id: 2, parentTaskId: 1); // 子任务
        var svc = CreateService(db);

        var result = await svc.DeleteAsync(1);

        Assert.NotEqual(200, result.Code);
        Assert.True(await db.Set<TmTask>().AnyAsync(t => t.FID == 1)); // 仍存在
    }

    [Fact]
    public async Task 跨组织任务在另一组织上下文不可见()
    {
        // 组织1种入任务
        await using (var seedDb = TestDbContextFactory.Create(nameof(跨组织任务在另一组织上下文不可见), orgId: 1))
        {
            await SeedTaskAsync(seedDb, id: 1, creatorId: 1, visibility: 0);
        }
        // 同库名+组织2上下文查询（注：TestDbContextFactory 每次 Create 生成独立库，
        // 因此此处用同一上下文先种入再切换断言更稳妥）

        await using var db = TestDbContextFactory.Create(nameof(跨组织任务在另一组织上下文不可见), orgId: 2);
        await SeedTaskAsync(db, id: 10, creatorId: 1, visibility: 0); // 组织2自己的任务
        var svc = CreateService(db);

        var result = await svc.GetPagedListAsync(new TaskPagedRequest(), orgId: 2, currentUserId: 1, isAdmin: true);

        Assert.Equal(200, result.Code);
        Assert.Contains(result.Data!.Items, x => x.Id == 10);
        Assert.DoesNotContain(result.Data.Items, x => x.Id == 1); // 组织1任务不可见
    }
}
