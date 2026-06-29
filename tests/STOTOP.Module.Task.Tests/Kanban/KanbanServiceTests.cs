using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.Task.Tests.Kanban;

using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// KanbanService 单元测试簇：看板按状态分组、可见范围过滤(admin/非admin)、
/// 排除模板任务与子任务(FParentTaskId==0)、子任务统计、拖拽移动状态流转。
/// 构造函数仅 (STOTOPDbContext db)，本服务不依赖 TaskTestFakes。
/// </summary>
public class KanbanServiceTests
{
    private const long Org = 1;

    private static TmTask NewTask(string title, int status = 0, long creatorId = 1, long? assigneeId = null,
        int visibility = 0, bool isTemplate = false, long parentTaskId = 0, int sort = 0, long? projectId = null)
        => new TmTask
        {
            FTitle = title,
            FStatus = status,
            FCreatorId = creatorId,
            FAssigneeId = assigneeId,
            FVisibility = visibility,
            FIsTemplate = isTemplate,
            FParentTaskId = parentTaskId,
            FSort = sort,
            FProjectId = projectId
        };

    [Fact]
    public async Task 看板始终返回五个状态列且按状态升序()
    {
        await using var db = TestDbContextFactory.Create(nameof(看板始终返回五个状态列且按状态升序), orgId: Org);
        db.Set<TmTask>().Add(NewTask("仅一个待办任务", status: 0));
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 1, isAdmin: true);

        Assert.Equal(200, result.Code); // ApiResult 成功 Code=200
        var cols = result.Data!.Columns;
        Assert.Equal(5, cols.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, cols.Select(c => c.Status).ToArray());
        Assert.Equal(new[] { "待办", "进行中", "已完成", "已取消", "已延期" }, cols.Select(c => c.StatusName).ToArray());
    }

    [Fact]
    public async Task 任务按状态分组到对应列且Count正确()
    {
        await using var db = TestDbContextFactory.Create(nameof(任务按状态分组到对应列且Count正确), orgId: Org);
        db.Set<TmTask>().AddRange(
            NewTask("待办1", status: 0),
            NewTask("待办2", status: 0),
            NewTask("进行中1", status: 1),
            NewTask("已完成1", status: 2)
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 1, isAdmin: true);

        var cols = result.Data!.Columns;
        Assert.Equal(2, cols.Single(c => c.Status == 0).Count);
        Assert.Equal(1, cols.Single(c => c.Status == 1).Count);
        Assert.Equal(1, cols.Single(c => c.Status == 2).Count);
        Assert.Equal(0, cols.Single(c => c.Status == 3).Count);
        Assert.Equal(2, cols.Single(c => c.Status == 0).Cards.Count);
    }

    [Fact]
    public async Task 排除模板任务与子任务()
    {
        await using var db = TestDbContextFactory.Create(nameof(排除模板任务与子任务), orgId: Org);
        db.Set<TmTask>().AddRange(
            NewTask("普通顶层任务", status: 0),                    // 计入
            NewTask("模板任务", status: 0, isTemplate: true),      // 排除：FIsTemplate
            NewTask("子任务", status: 0, parentTaskId: 999)        // 排除：FParentTaskId != 0
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 1, isAdmin: true);

        var allCards = result.Data!.Columns.SelectMany(c => c.Cards).ToList();
        Assert.Single(allCards);
        Assert.Equal("普通顶层任务", allCards[0].Title);
    }

    [Fact]
    public async Task 卡片按Sort升序排列()
    {
        await using var db = TestDbContextFactory.Create(nameof(卡片按Sort升序排列), orgId: Org);
        // 同一状态列内：FSort 升序优先
        db.Set<TmTask>().AddRange(
            NewTask("排序30", status: 0, sort: 30),
            NewTask("排序10", status: 0, sort: 10),
            NewTask("排序20", status: 0, sort: 20)
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 1, isAdmin: true);

        var todoCards = result.Data!.Columns.Single(c => c.Status == 0).Cards;
        Assert.Equal(new[] { "排序10", "排序20", "排序30" }, todoCards.Select(c => c.Title).ToArray());
    }

    [Fact]
    public async Task 非admin看不到他人私有任务但能看到公开任务()
    {
        await using var db = TestDbContextFactory.Create(nameof(非admin看不到他人私有任务但能看到公开任务), orgId: Org);
        const long me = 100;
        const long other = 200;
        db.Set<TmTask>().AddRange(
            NewTask("公开任务", status: 0, creatorId: other, visibility: 0),                   // FVisibility==0 全员可见
            NewTask("他人私有任务", status: 0, creatorId: other, assigneeId: other, visibility: 3) // 私有且与 me 无关 → 不可见
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: me, isAdmin: false);

        var titles = result.Data!.Columns.SelectMany(c => c.Cards).Select(c => c.Title).ToList();
        Assert.Contains("公开任务", titles);
        Assert.DoesNotContain("他人私有任务", titles);
    }

    [Fact]
    public async Task admin可见所有任务无视可见范围()
    {
        await using var db = TestDbContextFactory.Create(nameof(admin可见所有任务无视可见范围), orgId: Org);
        const long other = 200;
        db.Set<TmTask>().AddRange(
            NewTask("公开任务", status: 0, creatorId: other, visibility: 0),
            NewTask("他人私有任务", status: 0, creatorId: other, assigneeId: other, visibility: 3)
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 999, isAdmin: true);

        var titles = result.Data!.Columns.SelectMany(c => c.Cards).Select(c => c.Title).ToList();
        Assert.Contains("公开任务", titles);
        Assert.Contains("他人私有任务", titles);
    }

    [Fact]
    public async Task 非admin能看到指派给自己的私有任务并解析负责人姓名()
    {
        await using var db = TestDbContextFactory.Create(nameof(非admin能看到指派给自己的私有任务并解析负责人姓名), orgId: Org);
        const long me = 100;
        const long other = 200;
        // 负责人姓名经 SysUser 字典解析回填卡片 AssigneeName
        db.Set<SysUser>().Add(new SysUser { FID = me, FName = "张三", FAccount = "zs" });
        // FVisibility==3 私有，创建人是 other，但 assignee 是 me → 末尾 OR 分支 FAssigneeId==currentUserId 命中
        db.Set<TmTask>().Add(NewTask("指派给我的私有任务", status: 1, creatorId: other, assigneeId: me, visibility: 3));
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: me, isAdmin: false);

        var card = result.Data!.Columns.SelectMany(c => c.Cards).Single(c => c.Title == "指派给我的私有任务");
        Assert.Equal(me, card.AssigneeId);
        Assert.Equal("张三", card.AssigneeName);
    }

    [Fact]
    public async Task 子任务统计汇总到父卡片()
    {
        await using var db = TestDbContextFactory.Create(nameof(子任务统计汇总到父卡片), orgId: Org);
        var parent = NewTask("父任务", status: 1);
        db.Set<TmTask>().Add(parent);
        await db.SaveChangesAsync(); // 先保存拿到父 FID

        db.Set<TmTask>().AddRange(
            NewTask("子1已完成", status: 2, parentTaskId: parent.FID),
            NewTask("子2已完成", status: 2, parentTaskId: parent.FID),
            NewTask("子3进行中", status: 1, parentTaskId: parent.FID)
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest(), Org, currentUserId: 1, isAdmin: true);

        var card = result.Data!.Columns.SelectMany(c => c.Cards).Single(c => c.Id == parent.FID);
        Assert.Equal(3, card.SubTaskCount);
        Assert.Equal(2, card.CompletedSubTaskCount);
    }

    [Fact]
    public async Task 按项目筛选只返回该项目任务()
    {
        await using var db = TestDbContextFactory.Create(nameof(按项目筛选只返回该项目任务), orgId: Org);
        db.Set<TmTask>().AddRange(
            NewTask("项目10任务", status: 0, projectId: 10),
            NewTask("项目20任务", status: 0, projectId: 20),
            NewTask("无项目任务", status: 0, projectId: null)
        );
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.GetKanbanDataAsync(new KanbanQueryRequest { ProjectId = 10 }, Org, currentUserId: 1, isAdmin: true);

        var titles = result.Data!.Columns.SelectMany(c => c.Cards).Select(c => c.Title).ToList();
        Assert.Single(titles);
        Assert.Equal("项目10任务", titles[0]);
        Assert.Equal(10, result.Data!.ProjectId); // DTO 回传查询的 ProjectId
    }

    [Fact]
    public async Task 移动到已完成自动置进度100并写实际完成时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(移动到已完成自动置进度100并写实际完成时间), orgId: Org);
        var task = NewTask("待完成任务", status: 1);
        task.FProgress = 40;
        db.Set<TmTask>().Add(task);
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.MoveAsync(new KanbanMoveRequest { TaskId = task.FID, TargetStatus = 2, TargetSort = 5 });

        Assert.Equal(200, result.Code);
        Assert.True(result.Data);
        var saved = await db.Set<TmTask>().FindAsync(task.FID);
        Assert.Equal(2, saved!.FStatus);
        Assert.Equal(5, saved.FSort);
        Assert.Equal(100, saved.FProgress);
        Assert.NotNull(saved.FActualEnd);
    }

    [Fact]
    public async Task 移动到进行中首次写实际开始时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(移动到进行中首次写实际开始时间), orgId: Org);
        var task = NewTask("待启动任务", status: 0); // FActualStart 默认 null
        db.Set<TmTask>().Add(task);
        await db.SaveChangesAsync();
        var svc = new KanbanService(db);

        var result = await svc.MoveAsync(new KanbanMoveRequest { TaskId = task.FID, TargetStatus = 1, TargetSort = 0 });

        Assert.Equal(200, result.Code);
        var saved = await db.Set<TmTask>().FindAsync(task.FID);
        Assert.Equal(1, saved!.FStatus);
        Assert.NotNull(saved.FActualStart);
    }

    [Fact]
    public async Task 移动不存在的任务返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(移动不存在的任务返回失败), orgId: Org);
        var svc = new KanbanService(db);

        var result = await svc.MoveAsync(new KanbanMoveRequest { TaskId = 99999, TargetStatus = 1, TargetSort = 0 });

        Assert.NotEqual(200, result.Code); // ApiResult.Fail 默认 Code=400
        Assert.False(result.Data);
        Assert.Equal("任务不存在", result.Message);
    }
}
