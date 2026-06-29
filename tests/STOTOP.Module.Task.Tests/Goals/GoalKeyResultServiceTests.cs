using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;
using Xunit;

namespace STOTOP.Module.Task.Tests.Goals;

using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// GoalKeyResult 服务簇首批单元测试：聚焦确定性行为
/// （KR 进度计算/度量类型分支、目标进度按权重汇总、目标树过滤/嵌套、状态默认值、组织过滤、校验）。
/// ApiResult 成功约定 Code=200（见 STOTOP.Core/Models/ApiResult.cs）。
/// </summary>
public class GoalKeyResultServiceTests
{
    private static KeyResultService NewKrService(STOTOPDbContext db)
        => new KeyResultService(db, TaskTestFakes.ServiceProvider(db));

    private static GoalService NewGoalService(STOTOPDbContext db)
        => new GoalService(db, NewKrService(db));

    /// <summary>种子一个目标并返回其 FID（FOrgId 由保存自动回填）。</summary>
    private static async Task<long> SeedGoalAsync(STOTOPDbContext db, string title = "G1", long parentId = 0, string level = "公司")
    {
        var goal = new TmGoal
        {
            FTitle = title,
            FParentId = parentId,
            FLevel = level,
            FCreatorId = 1,
            FStartDate = new DateTime(2026, 1, 1),
            FEndDate = new DateTime(2026, 12, 31)
        };
        db.Set<TmGoal>().Add(goal);
        await db.SaveChangesAsync();
        return goal.FID;
    }

    // ---------------------------------------------------------------------
    // GoalService.CreateAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task 新建目标成功且默认进度为0状态为0()
    {
        await using var db = TestDbContextFactory.Create(nameof(新建目标成功且默认进度为0状态为0), orgId: 1);
        var svc = NewGoalService(db);

        var result = await svc.CreateAsync(new CreateGoalRequest
        {
            Title = "年度营收目标",
            Level = "公司",
            Weight = 100,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        }, orgId: 1, creatorId: 7);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal("年度营收目标", result.Data!.Title);
        Assert.Equal(0, result.Data.Progress); // 新目标无 KR，进度 0
        Assert.Equal(0, result.Data.Status);   // 默认状态 0
        Assert.Equal(7, result.Data.CreatorId);
    }

    // ---------------------------------------------------------------------
    // KeyResultService.CreateAsync 校验
    // ---------------------------------------------------------------------

    [Fact]
    public async Task 为不存在的目标创建关键成果返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(为不存在的目标创建关键成果返回失败), orgId: 1);
        var svc = NewKrService(db);

        var result = await svc.CreateAsync(99999, new CreateKeyResultRequest
        {
            Title = "孤儿KR",
            MeasureType = 1,
            TargetValue = 100
        });

        Assert.Equal(400, result.Code); // Fail 默认 code=400
        Assert.Null(result.Data);
    }

    // ---------------------------------------------------------------------
    // KeyResultService 进度计算：度量类型分支
    // ---------------------------------------------------------------------

    [Fact]
    public async Task 数值型关键成果按区间线性计算进度()
    {
        await using var db = TestDbContextFactory.Create(nameof(数值型关键成果按区间线性计算进度), orgId: 1);
        var goalId = await SeedGoalAsync(db);
        var svc = NewKrService(db);

        // 数值型(0)：起始 0 → 目标 200，当前 50 → (50-0)/(200-0)*100 = 25
        var created = await svc.CreateAsync(goalId, new CreateKeyResultRequest
        {
            Title = "签约金额",
            MeasureType = 0,
            StartValue = 0,
            TargetValue = 200,
            Weight = 100
        });
        Assert.Equal(200, created.Code);

        var updated = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest
        {
            CurrentValue = 50
        });

        Assert.Equal(200, updated.Code);
        Assert.Equal(25, updated.Data!.Progress);
        Assert.Equal(50m, updated.Data.CurrentValue);
    }

    [Fact]
    public async Task 数值型关键成果超出目标进度封顶为100()
    {
        await using var db = TestDbContextFactory.Create(nameof(数值型关键成果超出目标进度封顶为100), orgId: 1);
        var goalId = await SeedGoalAsync(db);
        var svc = NewKrService(db);

        var created = await svc.CreateAsync(goalId, new CreateKeyResultRequest
        {
            Title = "拜访次数",
            MeasureType = 0,
            StartValue = 10,
            TargetValue = 20,
            Weight = 100
        });

        // 当前 100 → (100-10)/(20-10)*100 = 900 → Clamp 到 100
        var updated = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest
        {
            CurrentValue = 100
        });

        Assert.Equal(100, updated.Data!.Progress);
    }

    [Fact]
    public async Task 百分比型关键成果直接取当前值并封顶()
    {
        await using var db = TestDbContextFactory.Create(nameof(百分比型关键成果直接取当前值并封顶), orgId: 1);
        var goalId = await SeedGoalAsync(db);
        var svc = NewKrService(db);

        // 百分比型(1)：进度 = Clamp(round(当前值),0,100)
        var created = await svc.CreateAsync(goalId, new CreateKeyResultRequest
        {
            Title = "完成率",
            MeasureType = 1,
            TargetValue = 100,
            Weight = 100
        });

        var at60 = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest { CurrentValue = 60 });
        Assert.Equal(60, at60.Data!.Progress);

        var over = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest { CurrentValue = 150 });
        Assert.Equal(100, over.Data!.Progress); // 超 100 封顶
    }

    [Fact]
    public async Task 里程碑型关键成果达到目标值才算100否则0()
    {
        await using var db = TestDbContextFactory.Create(nameof(里程碑型关键成果达到目标值才算100否则0), orgId: 1);
        var goalId = await SeedGoalAsync(db);
        var svc = NewKrService(db);

        // 里程碑型(2)：当前>=目标 → 100，否则 0
        var created = await svc.CreateAsync(goalId, new CreateKeyResultRequest
        {
            Title = "上线发布",
            MeasureType = 2,
            TargetValue = 1,
            Weight = 100
        });

        var notReached = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest { CurrentValue = 0 });
        Assert.Equal(0, notReached.Data!.Progress);

        var reached = await svc.UpdateProgressAsync(created.Data!.Id, new UpdateKeyResultProgressRequest { CurrentValue = 1 });
        Assert.Equal(100, reached.Data!.Progress);
    }

    // ---------------------------------------------------------------------
    // GoalService.RecalculateProgressAsync：按权重汇总
    // ---------------------------------------------------------------------

    [Fact]
    public async Task 目标进度按关键成果加权汇总()
    {
        await using var db = TestDbContextFactory.Create(nameof(目标进度按关键成果加权汇总), orgId: 1);
        var goalId = await SeedGoalAsync(db);

        // KR1: 进度100 权重60；KR2: 进度0 权重40 → (100*60+0*40)/100 = 60
        db.Set<TmKeyResult>().AddRange(
            new TmKeyResult { FGoalId = goalId, FTitle = "KR1", FProgress = 100, FWeight = 60, FMeasureType = 1 },
            new TmKeyResult { FGoalId = goalId, FTitle = "KR2", FProgress = 0, FWeight = 40, FMeasureType = 1 });
        await db.SaveChangesAsync();

        var goalSvc = NewGoalService(db);
        var recalc = await goalSvc.RecalculateProgressAsync(goalId);
        Assert.Equal(200, recalc.Code);

        var detail = await goalSvc.GetByIdAsync(goalId);
        Assert.Equal(60, detail.Data!.Progress);
    }

    [Fact]
    public async Task 无关键成果时目标进度归零()
    {
        await using var db = TestDbContextFactory.Create(nameof(无关键成果时目标进度归零), orgId: 1);
        var goalId = await SeedGoalAsync(db);

        // 先人为写入一个非零进度，再重算应归 0（无 KR）
        var tracked = await db.Set<TmGoal>().AsTracking().FirstAsync(g => g.FID == goalId);
        tracked.FProgress = 88;
        await db.SaveChangesAsync();

        var goalSvc = NewGoalService(db);
        var recalc = await goalSvc.RecalculateProgressAsync(goalId);

        Assert.Equal(200, recalc.Code);
        var detail = await goalSvc.GetByIdAsync(goalId);
        Assert.Equal(0, detail.Data!.Progress);
    }

    [Fact]
    public async Task 关键成果总权重为零时目标进度为零()
    {
        await using var db = TestDbContextFactory.Create(nameof(关键成果总权重为零时目标进度为零), orgId: 1);
        var goalId = await SeedGoalAsync(db);

        // 所有 KR 权重为 0 → totalWeight==0 分支 → 进度 0（即便 KR 自身进度非 0）
        db.Set<TmKeyResult>().AddRange(
            new TmKeyResult { FGoalId = goalId, FTitle = "KR1", FProgress = 100, FWeight = 0, FMeasureType = 1 },
            new TmKeyResult { FGoalId = goalId, FTitle = "KR2", FProgress = 50, FWeight = 0, FMeasureType = 1 });
        await db.SaveChangesAsync();

        var goalSvc = NewGoalService(db);
        await goalSvc.RecalculateProgressAsync(goalId);

        var detail = await goalSvc.GetByIdAsync(goalId);
        Assert.Equal(0, detail.Data!.Progress);
    }

    // ---------------------------------------------------------------------
    // GoalService.GetTreeAsync：嵌套 + 过滤 + 组织
    // ---------------------------------------------------------------------

    [Fact]
    public async Task 目标树仅返回根节点并嵌套子目标()
    {
        await using var db = TestDbContextFactory.Create(nameof(目标树仅返回根节点并嵌套子目标), orgId: 1);
        var rootId = await SeedGoalAsync(db, "根目标", parentId: 0, level: "公司");
        await SeedGoalAsync(db, "子目标A", parentId: rootId, level: "部门");
        await SeedGoalAsync(db, "子目标B", parentId: rootId, level: "部门");

        var svc = NewGoalService(db);
        var result = await svc.GetTreeAsync(new GoalTreeQueryRequest(), orgId: 1);

        Assert.Equal(200, result.Code);
        Assert.Single(result.Data!);                 // 只有 1 个根（ParentId==0）
        var root = result.Data![0];
        Assert.Equal("根目标", root.Title);
        Assert.Equal(2, root.Children.Count);         // 两个子目标挂在根下
        Assert.All(root.Children, c => Assert.Equal(rootId, c.ParentId));
    }

    [Fact]
    public async Task 目标树按层级过滤()
    {
        await using var db = TestDbContextFactory.Create(nameof(目标树按层级过滤), orgId: 1);
        await SeedGoalAsync(db, "公司目标", parentId: 0, level: "公司");
        await SeedGoalAsync(db, "部门目标", parentId: 0, level: "部门");

        var svc = NewGoalService(db);
        var result = await svc.GetTreeAsync(new GoalTreeQueryRequest { Level = "部门" }, orgId: 1);

        Assert.Equal(200, result.Code);
        Assert.Single(result.Data!);
        Assert.Equal("部门目标", result.Data![0].Title);
    }

    [Fact]
    public async Task 目标树按组织过滤其它组织不可见()
    {
        await using var db = TestDbContextFactory.Create(nameof(目标树按组织过滤其它组织不可见), orgId: 1);
        await SeedGoalAsync(db, "组织1的目标", parentId: 0, level: "公司"); // 保存自动回填 FOrgId=1

        var svc = NewGoalService(db);

        // GetTreeAsync 内部显式 Where(g => g.FOrgId == orgId)：传 orgId:2 应为空，传 orgId:1 应可见
        var otherOrg = await svc.GetTreeAsync(new GoalTreeQueryRequest(), orgId: 2);
        Assert.Equal(200, otherOrg.Code);
        Assert.Empty(otherOrg.Data!);

        var ownOrg = await svc.GetTreeAsync(new GoalTreeQueryRequest(), orgId: 1);
        Assert.Single(ownOrg.Data!);
    }
}
