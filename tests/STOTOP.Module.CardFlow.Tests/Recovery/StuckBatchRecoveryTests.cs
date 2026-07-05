using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Recovery;

// STOTOP.Module.Task 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 崩溃恢复盲区回归：批次级插件链被中断后卡在 FStatus=3（质量后、凭证/汇总未跑）时，
/// 必须能被 BatchJobProcessorService.SelectStuckBatchesAsync 捞回并从断点续跑，
/// 否则自动凭证节点永不执行、批次被永久遗弃（见线上 batch #3262）。
/// </summary>
public class StuckBatchRecoveryTests
{
    private static readonly DateTime Stale = DateTime.Now.AddMinutes(-20);
    private static readonly DateTime Fresh = DateTime.Now.AddMinutes(-1);
    private static DateTime Cutoff => DateTime.Now.AddMinutes(-10);

    private static void AddBatch(
        STOTOPDbContext db, long id, int status, DateTime? updated,
        bool revoked = false, long flowDefId = 2257, int[]? execStatuses = null)
    {
        db.Set<CfBatch>().Add(new CfBatch
        {
            FID = id,
            FStatus = status,
            FIsRevoked = revoked,
            FUpdatedTime = updated,
            FFlowDefinitionId = flowDefId,
            FOrgId = 192,
            FTenantId = 1,
            FTriggerType = "fileUpload",
            FCreatedTime = DateTime.Now
        });
        if (execStatuses != null)
        {
            for (var i = 0; i < execStatuses.Length; i++)
            {
                db.Set<CfPluginExecution>().Add(new CfPluginExecution
                {
                    FID = id * 100 + i,
                    FBatchId = id,
                    FAutoPluginIndex = i,
                    FAutoPluginName = $"plugin{i}",
                    FStatus = execStatuses[i],
                    FOrgId = 192,
                    FTenantId = 1,
                    FCreatedTime = DateTime.Now
                });
            }
        }
    }

    // ── 核心盲区：卡在 3 + 有待执行(10)/进行中(11) 插件 → 选中 ──

    [Fact]
    public async Task 卡在质量后_有待执行插件_应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(卡在质量后_有待执行插件_应被选中));
        // 导入12/质量12/自动凭证10(从未启动)/汇总10 —— 复刻 batch #3262
        AddBatch(db, 3262, status: 3, updated: Stale, execStatuses: new[] { 12, 12, 10, 10 });
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        var picked = Assert.Single(result);
        Assert.Equal(3262, picked.FID);
        Assert.Equal(3, picked.FStatus);
        // 断点续跑批次级链
        Assert.Equal(BatchJobKind.ProcessBatchStages,
            BatchJobProcessorService.MapRecoveryKind(picked.FStatus, isBatchAutoFlow: true));
    }

    [Fact]
    public async Task 卡在质量后_凭证插件进行中被中断_应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(卡在质量后_凭证插件进行中被中断_应被选中));
        AddBatch(db, 3300, status: 3, updated: Stale, execStatuses: new[] { 12, 12, 11, 10 });
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.Contains(result, b => b.FID == 3300);
    }

    // ── 防误伤：已完成 fan-out 的正常 3 态批次不选 ──

    [Fact]
    public async Task 卡片已建_批次级链全完成_不应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(卡片已建_批次级链全完成_不应被选中));
        // 所有批次级执行记录=12（已完成），批次处于 3 属正常"已 fan-out 到卡片级"
        AddBatch(db, 3400, status: 3, updated: Stale, execStatuses: new[] { 12, 12, 12, 12 });
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.DoesNotContain(result, b => b.FID == 3400);
    }

    [Fact]
    public async Task 卡在3_无任何执行记录_不应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(卡在3_无任何执行记录_不应被选中));
        AddBatch(db, 3500, status: 3, updated: Stale, execStatuses: null);
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.DoesNotContain(result, b => b.FID == 3500);
    }

    // ── 不回归：原有 {0,2,4} 仍被选中 ──

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task 原有卡住状态_仍被选中(int status)
    {
        using var db = TestDbContextFactory.Create($"legacy_{status}");
        AddBatch(db, 4000 + status, status: status, updated: Stale, execStatuses: null);
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.Contains(result, b => b.FID == 4000 + status);
    }

    // ── 排除：已撤销 / 未陈旧 ──

    [Fact]
    public async Task 已撤销批次_不应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(已撤销批次_不应被选中));
        AddBatch(db, 4100, status: 3, updated: Stale, revoked: true, execStatuses: new[] { 12, 12, 10, 10 });
        AddBatch(db, 4101, status: 4, updated: Stale, revoked: true);
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.Empty(result);
    }

    [Fact]
    public async Task 未超时批次_不应被选中()
    {
        using var db = TestDbContextFactory.Create(nameof(未超时批次_不应被选中));
        AddBatch(db, 4200, status: 3, updated: Fresh, execStatuses: new[] { 12, 12, 10, 10 });
        AddBatch(db, 4201, status: 4, updated: Fresh);
        await db.SaveChangesAsync();

        var result = await BatchJobProcessorService.SelectStuckBatchesAsync(db, Cutoff, default);

        Assert.Empty(result);
    }

    // ── 纯函数：状态 → Kind 映射 ──

    [Theory]
    [InlineData(0, true, BatchJobKind.ProcessBatchStages)]
    [InlineData(0, false, BatchJobKind.ParseAndStage)]
    [InlineData(2, false, BatchJobKind.QualityCheckAndFanOut)]
    [InlineData(3, false, BatchJobKind.ProcessBatchStages)]
    [InlineData(4, false, BatchJobKind.ProcessBatchStages)]
    public void 状态到Kind映射(int status, bool isBatchAutoFlow, BatchJobKind expected)
    {
        Assert.Equal(expected, BatchJobProcessorService.MapRecoveryKind(status, isBatchAutoFlow));
    }
}
