// 回归：卡片级 auto 节点的插件生命周期。
//
// Bug：FlowEngineService.ExecuteAutoStageAsync 早先用【单参】AutoPluginFactory.Create(code)，
// 该重载从工厂自身捕获的根 ServiceProvider 解析插件（插件为 Scoped、构造注入 Scoped STOTOPDbContext）。
// 生产未开 ValidateScopes，于是插件及其 DbContext 被缓存在【根作用域】=应用级近单例、永不释放、
// 跨并发卡片级 auto 共享（DbContext 非线程安全）。
//
// 修复：卡片级改为每次 _scopeFactory.CreateScope() 起独立子作用域、用【双参】Create(code, scope) 解析、
// 用后随 using 释放；同时把 PluginContext.Services 指向该子作用域。
// 既消除 captive/并发共享/累积，又保留「插件用独立上下文 + 引擎 ReloadAsync 重读」既定语义。
//
// 这些断言全部确定性（无需真并发）：用一个【构造注入 Scoped 探针(IDisposable)】的测试插件，
// 探针即真实插件 _dbContext 的等价物。修复前探针==根容器实例且永不释放；修复后是子作用域新实例且已释放。
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Tests.Approval; // 复用 FlowEngineTestFakes 的内部假实现
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.AutoPlugin;

public class CardLevelAutoPluginScopeTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task CardLevelAuto_PluginScopedDependency_IsFreshAndDisposed_NotRootCaptive()
    {
        using var db = TestDbContextFactory.Create(
            nameof(CardLevelAuto_PluginScopedDependency_IsFreshAndDisposed_NotRootCaptive));

        var (provider, factory, recorder) = BuildPluginContainer();
        // 根容器直接解析出的 Scoped 探针 —— 即「captive 根实例」。
        var rootCaptiveProbe = provider.GetRequiredService<ScopeProbe>();

        SeedAutoFirstStageCard(db, cardId: 9600, flowDefId: 3262, flowVersionId: 3263, registryId: 8801);
        await db.SaveChangesAsync();

        var engine = BuildEngine(db, provider, factory);
        var result = await engine.SubmitAsync(9600, 99);

        Assert.True(result.Success, $"提交应成功（auto 节点应被执行）：{result.Message}");
        var call = Assert.Single(recorder.Calls); // 插件确被执行一次

        // 核心1：插件拿到的 Scoped 依赖不是根容器 captive 实例（修复前两者相同 → 失败）。
        Assert.NotSame(rootCaptiveProbe, call.CtorProbe);
        // 核心2：用后即弃 —— 该子作用域依赖在卡片级 auto 执行后被释放（修复前永不释放 → 失败）。
        Assert.True(call.CtorProbe.Disposed,
            "卡片级 auto 执行后，插件所在子作用域应被释放（其 Scoped 依赖 Dispose）");
        // 一致性：构造注入与 PluginContext.Services 解析来自同一子作用域。
        Assert.Same(call.CtorProbe, call.ContextProbe);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CardLevelAuto_SeparateExecutions_GetDistinctScopes_NoSharedDbContext()
    {
        // 两次卡片级 auto 执行（顺序即可，确定性）必须拿到【不同】的 Scoped 依赖实例：
        // 这正是并发安全的根因——修复前两次共享同一根 captive 实例（→ 失败），修复后各自独立。
        using var db = TestDbContextFactory.Create(
            nameof(CardLevelAuto_SeparateExecutions_GetDistinctScopes_NoSharedDbContext));

        var (provider, factory, recorder) = BuildPluginContainer();

        SeedAutoFirstStageCard(db, cardId: 9601, flowDefId: 3272, flowVersionId: 3273, registryId: 8811);
        SeedAutoFirstStageCard(db, cardId: 9602, flowDefId: 3282, flowVersionId: 3283, registryId: 8812);
        await db.SaveChangesAsync();

        var engine = BuildEngine(db, provider, factory);
        Assert.True((await engine.SubmitAsync(9601, 99)).Success);
        Assert.True((await engine.SubmitAsync(9602, 99)).Success);

        Assert.Equal(2, recorder.Calls.Count);
        Assert.NotSame(recorder.Calls[0].CtorProbe, recorder.Calls[1].CtorProbe);
        Assert.True(recorder.Calls[0].CtorProbe.Disposed && recorder.Calls[1].CtorProbe.Disposed,
            "每次卡片级 auto 执行的子作用域都应在用后释放");
    }

    // ── 测试基建 ────────────────────────────────────────────────────────────

    private static (ServiceProvider provider, AutoPluginFactory factory, ProbeRecorder recorder) BuildPluginContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ProbeRecorder>();   // 跨作用域汇总记录（本测试容器内单例）
        services.AddScoped<ScopeProbe>();          // 模拟 Scoped STOTOPDbContext
        services.AddScoped<ProbePlugin>();         // 模拟卡片级插件（构造注入 ScopeProbe）
        // 默认 BuildServiceProvider() 的 validateScopes=false —— 复刻生产（非 Development）行为。
        var provider = services.BuildServiceProvider();
        var factory = new AutoPluginFactory(provider); // 同生产：工厂捕获根容器
        factory.Register<ProbePlugin>(ProbePlugin.Code);
        var recorder = provider.GetRequiredService<ProbeRecorder>();
        return (provider, factory, recorder);
    }

    private static void SeedAutoFirstStageCard(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long cardId, long flowDefId, long flowVersionId, long registryId)
    {
        if (!db.Set<SysUser>().Local.Any(u => u.FID == 99))
            db.Set<SysUser>().Add(new SysUser { FID = 99, FName = "发起人" });

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = flowDefId, FFlowName = "captive回归", FFlowCode = $"captive-{flowDefId}", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = flowVersionId, FFlowDefinitionId = flowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfAutoPluginRegistry>().Add(new CfAutoPluginRegistry
        {
            FID = registryId, F插件编码 = ProbePlugin.Code
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = registryId * 10 + 1, FFlowVersionId = flowVersionId, FSortOrder = 1,
            FStageName = "自动探针", FType = "auto", F插件注册ID = registryId
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
            FTitle = "captive", FStatus = "draft", FInitiatorId = 99, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
    }

    private static FlowEngineService BuildEngine(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, ServiceProvider provider, AutoPluginFactory factory)
    {
        var orchestration = new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance);
        return new FlowEngineService(
            db,
            new FakeNumberSequenceService(),
            new FakeCardSchemaService(),
            new ApprovalModeHandler(),
            new SequentialApprovalRuntime(),
            new ReturnToStageRuntime(),
            new StageConfigParser(),
            new StageFieldAccessService(),
            new StageActionPolicyService(),
            new ConditionEvaluator(),
            new ApproverResolver(db),
            new FakeBudgetOccupationService(),
            new DbTodoService(db),
            new FakeNotificationDispatcher(),
            factory,
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            orchestration,
            new FakeBatchNotifier(),
            new FakeBatchLifecycleService(),
            NullLogger<FlowEngineService>.Instance);
    }

    // ── 测试用插件与探针 ─────────────────────────────────────────────────────

    /// <summary>模拟 Scoped STOTOPDbContext：可观测实例标识与是否被释放。</summary>
    internal sealed class ScopeProbe : global::System.IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    internal sealed record CapturedProbe(ScopeProbe CtorProbe, ScopeProbe ContextProbe);

    /// <summary>跨作用域汇总插件执行时观测到的探针。</summary>
    internal sealed class ProbeRecorder
    {
        private readonly object _gate = new();
        public List<CapturedProbe> Calls { get; } = new();
        public void Record(ScopeProbe ctor, ScopeProbe context)
        {
            lock (_gate) Calls.Add(new CapturedProbe(ctor, context));
        }
    }

    /// <summary>模拟卡片级插件：像 AutoVoucherPlugin 那样【构造注入】Scoped 依赖。</summary>
    internal sealed class ProbePlugin : ProcessingPluginBase
    {
        public const string Code = "CaptiveScopeProbePlugin";
        private readonly ScopeProbe _probe;
        private readonly ProbeRecorder _recorder;

        public ProbePlugin(ScopeProbe probe, ProbeRecorder recorder)
        {
            _probe = probe;
            _recorder = recorder;
        }

        public override string PluginName => Code;

        public override global::System.Threading.Tasks.Task<PluginResult> ExecuteAsync(PluginContext context)
        {
            var fromContext = (ScopeProbe)context.Services.GetService(typeof(ScopeProbe))!;
            _recorder.Record(_probe, fromContext);
            return global::System.Threading.Tasks.Task.FromResult(PluginResult.Ok());
        }
    }
}
