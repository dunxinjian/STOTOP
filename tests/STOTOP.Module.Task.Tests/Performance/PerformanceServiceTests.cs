using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.Task.Tests.Performance;

using Task = global::System.Threading.Tasks.Task;

public class PerformanceServiceTests
{
    private const long Org = 1;

    [Fact]
    public async Task 创建考核周期默认为草稿状态且记录数为零()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建考核周期默认为草稿状态且记录数为零), orgId: Org);
        var svc = new PerformanceService(db);

        var result = await svc.CreatePeriodAsync(
            new CreatePerformancePeriodRequest
            {
                Name = "2026Q1",
                Type = 1,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 3, 31)
            },
            orgId: Org, operatorId: 99);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Id > 0);
        Assert.Equal(0, result.Data.Status);
        Assert.Equal(0, result.Data.RecordCount);
        Assert.Equal(Org, result.Data.OrgId);
        Assert.Equal("2026Q1", result.Data.Name);
    }

    [Fact]
    public async Task 更新不存在的考核周期返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新不存在的考核周期返回失败), orgId: Org);
        var svc = new PerformanceService(db);

        var result = await svc.UpdatePeriodAsync(
            id: 999999,
            new UpdatePerformancePeriodRequest { Name = "x", Type = 1, Status = 1 });

        Assert.NotEqual(200, result.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task 周期分页按状态过滤并按创建时间倒序()
    {
        await using var db = TestDbContextFactory.Create(nameof(周期分页按状态过滤并按创建时间倒序), orgId: Org);
        db.Set<TmPerformancePeriod>().Add(new TmPerformancePeriod
        {
            FName = "草稿期", FType = 1, FStatus = 0,
            FCreateTime = new DateTime(2026, 1, 1), FUpdateTime = new DateTime(2026, 1, 1)
        });
        db.Set<TmPerformancePeriod>().Add(new TmPerformancePeriod
        {
            FName = "进行中较早", FType = 1, FStatus = 1,
            FCreateTime = new DateTime(2026, 2, 1), FUpdateTime = new DateTime(2026, 2, 1)
        });
        db.Set<TmPerformancePeriod>().Add(new TmPerformancePeriod
        {
            FName = "进行中较晚", FType = 1, FStatus = 1,
            FCreateTime = new DateTime(2026, 3, 1), FUpdateTime = new DateTime(2026, 3, 1)
        });
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.GetPeriodsPagedAsync(
            new PerformancePeriodPagedRequest { PageIndex = 1, PageSize = 10, Status = 1 }, Org);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Total);
        Assert.Equal("进行中较晚", result.Data.Items[0].Name);
        Assert.Equal("进行中较早", result.Data.Items[1].Name);
    }

    [Fact]
    public async Task 周期分页按组织隔离仅返回本组织数据()
    {
        var dbName = nameof(周期分页按组织隔离仅返回本组织数据);

        await using var db1 = TestDbContextFactory.Create(dbName, orgId: Org);
        db1.Set<TmPerformancePeriod>().Add(new TmPerformancePeriod { FName = "本组织期" });
        await db1.SaveChangesAsync();

        db1.Set<TmPerformancePeriod>().Add(new TmPerformancePeriod { FName = "他组织期", FOrgId = 2 });
        await db1.SaveChangesAsync();

        var svc = new PerformanceService(db1);
        var result = await svc.GetPeriodsPagedAsync(
            new PerformancePeriodPagedRequest { PageIndex = 1, PageSize = 10 }, Org);

        Assert.Equal(1, result.Data!.Total);
        Assert.Equal("本组织期", result.Data.Items[0].Name);
    }

    [Fact]
    public async Task 创建维度编码重复返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建维度编码重复返回失败), orgId: Org);
        var svc = new PerformanceService(db);

        var first = await svc.CreateDimensionAsync(
            new CreatePerformanceDimensionRequest
            {
                DimensionName = "任务完成率", DimensionCode = "completion_rate",
                DataSource = 0, Weight = 60, MaxScore = 100, Sort = 1
            }, Org);
        Assert.Equal(200, first.Code);

        var dup = await svc.CreateDimensionAsync(
            new CreatePerformanceDimensionRequest
            {
                DimensionName = "另一个", DimensionCode = "completion_rate",
                DataSource = 0, Weight = 40, MaxScore = 100, Sort = 2
            }, Org);

        Assert.NotEqual(200, dup.Code);
        Assert.Null(dup.Data);
    }

    [Fact]
    public async Task 删除已有评分记录的维度返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(删除已有评分记录的维度返回失败), orgId: Org);

        var dim = new TmPerformanceDimension
        {
            FDimensionName = "质量", FDimensionCode = "quality", FDataSource = 1, FWeight = 50, FMaxScore = 100
        };
        db.Set<TmPerformanceDimension>().Add(dim);
        await db.SaveChangesAsync();

        db.Set<TmPerformanceScore>().Add(new TmPerformanceScore
        {
            FRecordId = 1, FDimensionId = dim.FID, FScore = 80, FEvaluator = "superior"
        });
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.DeleteDimensionAsync(dim.FID);

        Assert.NotEqual(200, result.Code);
        Assert.True(await db.Set<TmPerformanceDimension>().AnyAsync(d => d.FID == dim.FID));
    }

    [Fact]
    public async Task 维度列表按排序字段升序返回()
    {
        await using var db = TestDbContextFactory.Create(nameof(维度列表按排序字段升序返回), orgId: Org);
        db.Set<TmPerformanceDimension>().Add(new TmPerformanceDimension { FDimensionName = "C", FDimensionCode = "c", FSort = 3 });
        db.Set<TmPerformanceDimension>().Add(new TmPerformanceDimension { FDimensionName = "A", FDimensionCode = "a", FSort = 1 });
        db.Set<TmPerformanceDimension>().Add(new TmPerformanceDimension { FDimensionName = "B", FDimensionCode = "b", FSort = 2 });
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.GetDimensionsAsync(Org);

        Assert.Equal(200, result.Code);
        Assert.Equal(new[] { "A", "B", "C" }, result.Data!.Select(d => d.DimensionName).ToArray());
    }

    [Fact]
    public async Task 上级评分综合得分按维度权重加权且优先级上级高于自动()
    {
        await using var db = TestDbContextFactory.Create(nameof(上级评分综合得分按维度权重加权且优先级上级高于自动), orgId: Org);

        var dimA = new TmPerformanceDimension { FDimensionName = "A", FDimensionCode = "a", FWeight = 60, FMaxScore = 100, FIsEnabled = true };
        var dimB = new TmPerformanceDimension { FDimensionName = "B", FDimensionCode = "b", FWeight = 40, FMaxScore = 100, FIsEnabled = true };
        db.Set<TmPerformanceDimension>().AddRange(dimA, dimB);
        await db.SaveChangesAsync();

        var period = new TmPerformancePeriod { FName = "P", FStatus = 1 };
        db.Set<TmPerformancePeriod>().Add(period);
        await db.SaveChangesAsync();

        var record = new TmPerformanceRecord { FPeriodId = period.FID, FEmployeeId = 7, FStatus = 1 };
        db.Set<TmPerformanceRecord>().Add(record);
        await db.SaveChangesAsync();

        db.Set<TmPerformanceScore>().AddRange(
            new TmPerformanceScore { FRecordId = record.FID, FDimensionId = dimA.FID, FScore = 50, FEvaluator = "auto" },
            new TmPerformanceScore { FRecordId = record.FID, FDimensionId = dimA.FID, FScore = 70, FEvaluator = "self" },
            new TmPerformanceScore { FRecordId = record.FID, FDimensionId = dimB.FID, FScore = 90, FEvaluator = "auto" });
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.ReviewAsync(
            record.FID,
            new SuperiorReviewRequest
            {
                Comment = "ok",
                Grade = "A",
                DimensionScores = new List<DimensionScoreInput>
                {
                    new() { DimensionId = dimA.FID, Score = 80, Remark = "好" }
                }
            },
            operatorId: 100);

        Assert.Equal(200, result.Code);

        var saved = await db.Set<TmPerformanceRecord>().AsNoTracking().FirstAsync(r => r.FID == record.FID);
        Assert.Equal(84.00m, saved.FOverallScore);
        Assert.Equal(2, saved.FStatus);
        Assert.Equal("A", saved.FGrade);
    }

    [Fact]
    public async Task 自评提交按权重计算自评得分并将状态从待自评推进到待上级评分()
    {
        await using var db = TestDbContextFactory.Create(nameof(自评提交按权重计算自评得分并将状态从待自评推进到待上级评分), orgId: Org);

        var dimA = new TmPerformanceDimension { FDimensionName = "A", FDimensionCode = "a", FWeight = 60, FMaxScore = 100 };
        var dimB = new TmPerformanceDimension { FDimensionName = "B", FDimensionCode = "b", FWeight = 40, FMaxScore = 100 };
        db.Set<TmPerformanceDimension>().AddRange(dimA, dimB);
        await db.SaveChangesAsync();

        var record = new TmPerformanceRecord { FPeriodId = 1, FEmployeeId = 7, FStatus = 0 };
        db.Set<TmPerformanceRecord>().Add(record);
        await db.SaveChangesAsync();

        // 首次自评（库中尚无 self 评分行）：服务应据本次提交的维度分 + 权重算出综合分
        // (90×60 + 60×40) / (60+40) = 78。此前因「Add 后未 SaveChanges 即查询」会算成 null，已修复并以本用例回归。
        var svc = new PerformanceService(db);
        var result = await svc.SelfEvaluateAsync(
            record.FID,
            new SelfEvaluateRequest
            {
                SelfComment = "自我评价",
                DimensionScores = new List<DimensionScoreInput>
                {
                    new() { DimensionId = dimA.FID, Score = 90 },
                    new() { DimensionId = dimB.FID, Score = 60 }
                }
            },
            operatorId: 7);

        Assert.Equal(200, result.Code);

        var saved = await db.Set<TmPerformanceRecord>().AsNoTracking().FirstAsync(r => r.FID == record.FID);
        Assert.Equal(78m, saved.FSelfScore);
        Assert.Equal(1, saved.FStatus);
        Assert.Equal("自我评价", saved.FSelfComment);
    }

    [Fact]
    public async Task 再次自评改分后综合分按新分重算()
    {
        await using var db = TestDbContextFactory.Create(nameof(再次自评改分后综合分按新分重算), orgId: Org);

        var dimA = new TmPerformanceDimension { FDimensionName = "A", FDimensionCode = "a", FWeight = 60, FMaxScore = 100 };
        var dimB = new TmPerformanceDimension { FDimensionName = "B", FDimensionCode = "b", FWeight = 40, FMaxScore = 100 };
        db.Set<TmPerformanceDimension>().AddRange(dimA, dimB);
        await db.SaveChangesAsync();

        var record = new TmPerformanceRecord { FPeriodId = 1, FEmployeeId = 7, FStatus = 0 };
        db.Set<TmPerformanceRecord>().Add(record);
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);

        // 第一次自评：90 / 60 → 78
        await svc.SelfEvaluateAsync(record.FID, new SelfEvaluateRequest
        {
            SelfComment = "v1",
            DimensionScores = new List<DimensionScoreInput>
            {
                new() { DimensionId = dimA.FID, Score = 90 },
                new() { DimensionId = dimB.FID, Score = 60 }
            }
        }, operatorId: 7);

        // 第二次自评改分：50 / 100 → 综合分按新分重算 (50×60 + 100×40)/100 = 70
        // （若仍漏 SaveChanges，查询会读到旧分算成 78，本用例即守住该回归）
        await svc.SelfEvaluateAsync(record.FID, new SelfEvaluateRequest
        {
            SelfComment = "v2",
            DimensionScores = new List<DimensionScoreInput>
            {
                new() { DimensionId = dimA.FID, Score = 50 },
                new() { DimensionId = dimB.FID, Score = 100 }
            }
        }, operatorId: 7);

        var saved = await db.Set<TmPerformanceRecord>().AsNoTracking().FirstAsync(r => r.FID == record.FID);
        Assert.Equal(70m, saved.FSelfScore);
        Assert.Equal("v2", saved.FSelfComment);
    }

    [Fact]
    public async Task 自评他人考核记录返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(自评他人考核记录返回失败), orgId: Org);

        var record = new TmPerformanceRecord { FPeriodId = 1, FEmployeeId = 7, FStatus = 0 };
        db.Set<TmPerformanceRecord>().Add(record);
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.SelfEvaluateAsync(
            record.FID,
            new SelfEvaluateRequest { SelfComment = "x", DimensionScores = new List<DimensionScoreInput>() },
            operatorId: 8);

        Assert.NotEqual(200, result.Code);
        var saved = await db.Set<TmPerformanceRecord>().AsNoTracking().FirstAsync(r => r.FID == record.FID);
        Assert.Equal(0, saved.FStatus);
        Assert.Null(saved.FSelfComment);
    }

    [Fact]
    public async Task 绩效汇算计算完成率自动维度得分并将进行中周期置为已汇算()
    {
        await using var db = TestDbContextFactory.Create(nameof(绩效汇算计算完成率自动维度得分并将进行中周期置为已汇算), orgId: Org);

        var period = new TmPerformancePeriod
        {
            FName = "汇算期", FStatus = 1,
            FStartDate = new DateTime(2026, 1, 1), FEndDate = new DateTime(2026, 3, 31)
        };
        db.Set<TmPerformancePeriod>().Add(period);
        await db.SaveChangesAsync();

        db.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 7, FOrgId = Org, FStatus = 1 });
        await db.SaveChangesAsync();

        db.Set<TmPerformanceDimension>().Add(new TmPerformanceDimension
        {
            FDimensionName = "任务完成率", FDimensionCode = "completion_rate",
            FDataSource = 0, FWeight = 100, FMaxScore = 100, FIsEnabled = true
        });
        await db.SaveChangesAsync();

        db.Set<TmTask>().AddRange(
            new TmTask { FTitle = "T1", FAssigneeId = 7, FParentTaskId = 0, FStatus = 2, FCreateTime = new DateTime(2026, 2, 1) },
            new TmTask { FTitle = "T2", FAssigneeId = 7, FParentTaskId = 0, FStatus = 0, FCreateTime = new DateTime(2026, 2, 2) });
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.CalculateAsync(period.FID);

        Assert.Equal(200, result.Code);

        var record = await db.Set<TmPerformanceRecord>().AsNoTracking()
            .FirstAsync(r => r.FPeriodId == period.FID && r.FEmployeeId == 7);
        Assert.Equal(2, record.FTaskTotal);
        Assert.Equal(1, record.FCompletedCount);
        Assert.Equal(50m, record.FCompletionRate);
        Assert.Equal(50m, record.FOverallScore);

        var savedPeriod = await db.Set<TmPerformancePeriod>().AsNoTracking().FirstAsync(p => p.FID == period.FID);
        Assert.Equal(2, savedPeriod.FStatus);
    }

    [Fact]
    public async Task 绩效汇算无活跃成员返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(绩效汇算无活跃成员返回失败), orgId: Org);

        var period = new TmPerformancePeriod
        {
            FName = "空组织期", FStatus = 1,
            FStartDate = new DateTime(2026, 1, 1), FEndDate = new DateTime(2026, 3, 31)
        };
        db.Set<TmPerformancePeriod>().Add(period);
        await db.SaveChangesAsync();

        var svc = new PerformanceService(db);
        var result = await svc.CalculateAsync(period.FID);

        Assert.NotEqual(200, result.Code);
        Assert.False(await db.Set<TmPerformanceRecord>().AnyAsync(r => r.FPeriodId == period.FID));
    }
}
