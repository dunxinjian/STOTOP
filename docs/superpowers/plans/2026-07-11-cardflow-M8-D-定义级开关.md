# CardFlow M8-D 定义级开关 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 CardFlow 发布设置里「审批人去重」「允许发起人撤回」两个二期灰行落成引擎真消费的定义级开关（停用节点 skip 本批不做）。

**Architecture:** 两开关均存入 `CfFlowVersion.FFlowSettingsJson`（与 `rejectStrategy`/`resubmitStrategy` 同款不透明 JSON blob，零 schema）。新增一个共享静态读取器 `FlowSettingsReader.ReadBool` 供三处消费：件①去重在 `AssignStageHandlersAsync` 与节点级 `FSkipDuplicateApprover` **OR 叠加**；件②撤回在 `WithdrawAsync` 加 gate（缺失即允许，仅显式 false 拦）；件② P1 在 `GetByIdAsync` 透出只读标志让运行时撤回按钮尊重开关。

**Tech Stack:** .NET 10 / EF Core（InMemory 测试）/ xUnit / Vue 3 + TS / Ant Design Vue（PC）+ Vant（移动）。

## Global Constraints

- 后端全在 `src/STOTOP.Module.CardFlow`；编译用 `scripts/dev/build-filter.ps1 cardflow`，测试用 `scripts/dev/test-dotnet.ps1 CardFlow`（filter 匹配项目名，跑整个 `CardFlow.Tests`；该套件重且 flaky，判回归多跑几次、以目标用例为准而非 tail 退出码）。单用例快迭代可临时 `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj --filter "FullyQualifiedName~<Class>" -m:1 /p:UseSharedCompilation=false`，但 commit 前须过项目级 run。
- **零 schema**：不新增实体列 / 不写版本化 seeder(V 编号) / 不改请求 DTO 结构。响应 DTO 加只读字段允许（仅件② P1）。
- JSON 键用 camelCase：`skipDuplicateApprover` / `allowInitiatorRevoke`（前端 `JSON.stringify(state.settings)` 直接序列化 TS 属性名，后端按同名读）。
- **撤回 gate 三态**：`allowInitiatorRevoke` 缺失(null)/true → 放行；仅显式 false → 拦（`ReadBool` default = **true**）。**去重** default = **false**。
- DB 全局 NoTracking：更新实体前 `_dbContext.Entry(x).State = EntityState.Modified`（撤回路径已具备，本批不新增写实体）。
- 前端：禁裸 hex（用 `var(--token)`）、禁裸 any；`npm run type-check` + `npm run lint:style`（在 `web/`）每件必绿。
- 测试：中文或既有英文 `[Fact]` 命名风格随文件；`CreateNoTrackingDb` 复现生产 `NoTrackingWithIdentityResolution`。
- 各任务独立 commit，经 husky + Claude hook 编译门禁（改 `.cs` 触发），**不 push**。
- 停用节点 skip：本批不做，不新增占位、不动引擎。发布设置中间那行「允许加签/转交」灰行**不碰**。

---

## File Structure

- **Create** `src/STOTOP.Module.CardFlow/Services/FlowSettingsReader.cs` — 共享静态：从 `FFlowSettingsJson` 读布尔开关，非法/缺键静默返默认。
- **Modify** `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs` — 件① `AssignStageHandlersAsync:3141` 放宽 if；件② `WithdrawAsync:1352` 后加 gate。
- **Modify** `src/STOTOP.Module.CardFlow/Services/CardService.cs` — 件② P1 `GetByIdAsync` 透出 `AllowInitiatorRevoke`（复用已加载的 `flowVersion`）。
- **Modify** `src/STOTOP.Module.CardFlow/Dtos/Responses.cs` — 件② P1 `CardDetailDto` 加 `AllowInitiatorRevoke`。
- **Modify** `web/src/views/cardflow/FlowDefinitionEditPage.vue` — 两键入 `FlowSettings`+init；2790/2807 两灰行转真开关。
- **Modify** `web/src/types/cardflow.ts` — 件② P1 `CardDetailDto` 加 `allowInitiatorRevoke?`。
- **Modify** `web/src/views/cardflow/CardDetailPage.vue` — 件② P1 PC 撤回按钮 gate。
- **Modify** `web/src/components/cardflow/CardFlowPanel.vue` — 件② P1 移动撤回按钮 gate。
- **Modify (test)** `tests/STOTOP.Module.CardFlow.Tests/Approval/CrossStageDeduplicateTests.cs` — 件① flow-level 去重用例（扩展既有 seed helper）。
- **Create (test)** `tests/STOTOP.Module.CardFlow.Tests/Approval/InitiatorRevokeGateTests.cs` — 件② gate 用例。

---

## Task 1: 件① 审批人去重(定义级) — 引擎 OR 叠加 + 前端开关

**Files:**
- Create: `src/STOTOP.Module.CardFlow/Services/FlowSettingsReader.cs`
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:3141`
- Modify: `web/src/views/cardflow/FlowDefinitionEditPage.vue:112-122,156-166,2790-2797`
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/CrossStageDeduplicateTests.cs`

**Interfaces:**
- Produces: `FlowSettingsReader.ReadBool(string? flowSettingsJson, string propertyName, bool defaultValue) : bool`（Task 2、Task 3 复用）。
- Consumes: 既有 `CrossStageDeduplicateTests` 的 `CreateNoTrackingDb`/`CreateEngine`/`SeedTwoStageFlowAsync`。

- [ ] **Step 1: 扩展测试 seed helper 承载 flow-level 设置**

在 `CrossStageDeduplicateTests.cs` 的 `SeedTwoStageFlowAsync` 签名加可选参数并写入版本 JSON（不影响既有 4 个调用方，默认 null）：

```csharp
private static async global::System.Threading.Tasks.Task SeedTwoStageFlowAsync(
    STOTOP.Infrastructure.Data.STOTOPDbContext db,
    long flowDefId, long flowVersionId, long stageDefIdA, long stageDefIdB,
    string stageBUsersJson, bool stageBSkipDuplicateApprover, string? flowSettingsJson = null)
```

并把版本创建处改为携带该 JSON：

```csharp
db.Set<CfFlowVersion>().Add(new CfFlowVersion
{
    FID = flowVersionId, FFlowDefinitionId = flowDefId, FStatus = "published", FIsCurrentVersion = true,
    FFlowSettingsJson = flowSettingsJson
});
```

- [ ] **Step 2: 写失败测试（flow-level ON 独立触发去重；stage-level OFF）**

在 `CrossStageDeduplicateTests.cs` 追加两个 `[Fact]`（放在场景4 之后、`CreateNoTrackingDb` 之前）：

```csharp
// ── 场景5：定义级开关 ON（节点级 OFF）→ 全流程套用去重，51 在 A 审后从 B 剔除 → B 无人可分派自动通过 ──
[Fact]
public async global::System.Threading.Tasks.Task Approve_FlowLevelSkipDuplicate_StageLevelOff_AutoCompletesStageB()
{
    const long flowDefId = 3640;
    const long flowVersionId = 3641;
    const long stageDefIdA = 6641;
    const long stageDefIdB = 6642;

    using var db = CreateNoTrackingDb(nameof(Approve_FlowLevelSkipDuplicate_StageLevelOff_AutoCompletesStageB));
    await SeedTwoStageFlowAsync(
        db, flowDefId, flowVersionId, stageDefIdA, stageDefIdB,
        stageBUsersJson: """{"users":[{"userId":51,"userName":"审批人A"}]}""",
        stageBSkipDuplicateApprover: false,                       // 节点级关闭
        flowSettingsJson: """{"skipDuplicateApprover":true}""");  // 定义级开启

    db.Set<CfCard>().Add(new CfCard
    {
        FID = 9805, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
        FTitle = "定义级去重-全部剔除", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
        FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
    });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var engine = CreateEngine(db);
    Assert.True((await engine.SubmitAsync(9805, InitiatorId)).Success);
    db.ChangeTracker.Clear();
    Assert.True((await engine.ApproveAsync(9805, ApproverA, new ApproveRequest { Opinion = "同意" })).Success);
    db.ChangeTracker.Clear();

    var stageBInstance = await db.Set<CfStageInstance>().AsNoTracking()
        .SingleAsync(s => s.FCardId == 9805 && s.FStageDefinitionId == stageDefIdB);
    Assert.Equal("completed", stageBInstance.FStatus);   // 定义级去重触发 → 自动通过
    Assert.Equal("approved", stageBInstance.FFinalAction);
    var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9805);
    Assert.Equal("completed", card.FStatus);
}

// ── 场景6：定义级 ON + 节点级 OFF + B 部分重叠(51 重复,52 独有) → 仅剔 51，保留 52 ──
[Fact]
public async global::System.Threading.Tasks.Task Approve_FlowLevelSkipDuplicate_PartialOverlap_RemovesOnlyDuplicate()
{
    const long flowDefId = 3650;
    const long flowVersionId = 3651;
    const long stageDefIdA = 6651;
    const long stageDefIdB = 6652;

    using var db = CreateNoTrackingDb(nameof(Approve_FlowLevelSkipDuplicate_PartialOverlap_RemovesOnlyDuplicate));
    await SeedTwoStageFlowAsync(
        db, flowDefId, flowVersionId, stageDefIdA, stageDefIdB,
        stageBUsersJson: """{"users":[{"userId":51,"userName":"审批人A"},{"userId":52,"userName":"审批人B"}]}""",
        stageBSkipDuplicateApprover: false,
        flowSettingsJson: """{"skipDuplicateApprover":true}""");

    db.Set<CfCard>().Add(new CfCard
    {
        FID = 9806, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
        FTitle = "定义级去重-部分重复", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
        FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
    });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var engine = CreateEngine(db);
    Assert.True((await engine.SubmitAsync(9806, InitiatorId)).Success);
    db.ChangeTracker.Clear();
    Assert.True((await engine.ApproveAsync(9806, ApproverA, new ApproveRequest { Opinion = "同意" })).Success);
    db.ChangeTracker.Clear();

    var stageBInstance = await db.Set<CfStageInstance>().AsNoTracking()
        .SingleAsync(s => s.FCardId == 9806 && s.FStageDefinitionId == stageDefIdB);
    Assert.Equal("active", stageBInstance.FStatus);
    var stageBAssignees = await db.Set<CfStageAssignee>().AsNoTracking()
        .Where(a => a.FStageInstanceId == stageBInstance.FID).ToListAsync();
    Assert.Single(stageBAssignees);
    Assert.Equal(ApproverOnlyAtB, stageBAssignees[0].FUserId);   // 51 剔除，52 保留
}
```

- [ ] **Step 3: 跑测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: FAIL —— 两新用例失败（定义级未消费，51 未被剔除：场景5 B 仍 `active`、场景6 B 有 2 个处理人）。既有场景1-4 仍通过。

- [ ] **Step 4: 新建共享读取器**

Create `src/STOTOP.Module.CardFlow/Services/FlowSettingsReader.cs`：

```csharp
using System.Text.Json;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 读 CfFlowVersion.FFlowSettingsJson 里的布尔型定义级开关（如 skipDuplicateApprover / allowInitiatorRevoke）。
/// 缺键 / 非对象 / 非布尔 / 非法 JSON 一律静默返回 defaultValue —— 与 FlowEngineService.GetResubmitStrategy 同款容错。
/// </summary>
public static class FlowSettingsReader
{
    public static bool ReadBool(string? flowSettingsJson, string propertyName, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(flowSettingsJson)) return defaultValue;
        try
        {
            using var doc = JsonDocument.Parse(flowSettingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(propertyName, out var v)
                && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            {
                return v.GetBoolean();
            }
        }
        catch (JsonException) { /* 静默降级 */ }
        return defaultValue;
    }
}
```

- [ ] **Step 5: 引擎放宽去重条件（OR 叠加）**

`FlowEngineService.cs`：`flowSettingsJson` 已在 `AssignStageHandlersAsync:3114-3117` 加载。将 `:3141` 的判断：

```csharp
        if (stageDef.FSkipDuplicateApprover)
```

改为：

```csharp
        var flowLevelSkipDup = FlowSettingsReader.ReadBool(flowSettingsJson, "skipDuplicateApprover", false);
        if (stageDef.FSkipDuplicateApprover || flowLevelSkipDup)
```

（`:3143-3178` 去重查询与"全剔空→auto-advance"逻辑不动；`FlowSettingsReader` 与 `FlowEngineService` 同命名空间 `STOTOP.Module.CardFlow.Services`，无需额外 using。）

- [ ] **Step 6: 跑测试确认通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: PASS —— 场景5（B `completed` 整卡完成）、场景6（B 仅剩 52）通过；既有场景1-4（节点级 OFF/ON、重提回归）不回归。

- [ ] **Step 7: 前端 FlowSettings 加键 + init 默认**

`FlowDefinitionEditPage.vue`：`FlowSettings` 接口（`:112-122`）加一行（放 `resubmitStrategy` 后）：

```typescript
  resubmitStrategy: 'fromStart' | 'fromRejected'
  skipDuplicateApprover: boolean
```

`initialState().settings`（`:156-166`）加默认（放 `resubmitStrategy` 后）：

```typescript
    resubmitStrategy: 'fromStart',
    skipDuplicateApprover: false,
```

- [ ] **Step 8: 前端「审批人去重」灰行转真开关**

`FlowDefinitionEditPage.vue:2790-2797` 整块替换为：

```html
              <!-- 审批人去重（定义级，M8-D 件①）：与节点级 FSkipDuplicateApprover OR 叠加 -->
              <div class="cfd-setrow">
                <a-switch v-model:checked="state.settings.skipDuplicateApprover" size="small" />
                <div class="cfd-setrow__text">
                  <div class="cfd-setrow__title">审批人去重</div>
                  <div class="cfd-setrow__desc">同一人在本流程更早环节已审批过，后续环节自动跳过其重复审批（与节点级去重叠加生效）。</div>
                </div>
              </div>
```

（保留其后 2799-2805「允许加签/转交」与 2807-2813「允许发起人撤回」两灰行原样，本步不碰。）

- [ ] **Step 9: 前端门禁**

Run（在 `web/`）：`npm run type-check` 然后 `npm run lint:style`
Expected: 均 PASS（无新 any、无裸 hex）。

- [ ] **Step 10: 提交件①**

```bash
git add src/STOTOP.Module.CardFlow/Services/FlowSettingsReader.cs \
        src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs \
        web/src/views/cardflow/FlowDefinitionEditPage.vue \
        tests/STOTOP.Module.CardFlow.Tests/Approval/CrossStageDeduplicateTests.cs
git commit -m "feat(cardflow): 审批人去重定义级开关(FFlowSettingsJson) + 引擎OR叠加节点级 (M8-D 件①)"
```
Expected: hook 编译门禁通过（改动 `.cs` 触发编译）。

---

## Task 2: 件② 允许发起人撤回(定义级) — WithdrawAsync gate + 前端开关

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1352`
- Modify: `web/src/views/cardflow/FlowDefinitionEditPage.vue:112-122,156-166,2807-2813`
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/InitiatorRevokeGateTests.cs`

**Interfaces:**
- Consumes: `FlowSettingsReader.ReadBool`（Task 1 产出）；`FlowEngineService.WithdrawAsync(long cardId, long operatorId)`（既有）。

- [ ] **Step 1: 写失败测试（显式 false 拦撤回）**

Create `tests/STOTOP.Module.CardFlow.Tests/Approval/InitiatorRevokeGateTests.cs`（harness 镜像 `FlowActionNoTrackingPersistenceTests` 的撤回用例）：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

/// <summary>
/// M8-D 件②：允许发起人撤回(定义级) gate。WithdrawAsync 读卡片锁定版本 FFlowSettingsJson 的
/// allowInitiatorRevoke：缺失/true → 放行（保留现状）；仅显式 false → 拦。发起人/active/无人已审等既有校验不变。
/// </summary>
public class InitiatorRevokeGateTests
{
    private const long FlowDefId = 3700;
    private const long FlowVersionId = 3701;
    private const long StageDefId = 6701;
    private const long ApproverId = 51;
    private const long InitiatorId = 88;
    private const long OtherUserId = 77;   // 非发起人、非处理人

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeFalse_IsRejected()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeFalse_IsRejected));
        await SeedActiveCardAsync(db, cardId: 9710, stageInstanceId: 9810, assigneeId: 9910,
            flowSettingsJson: """{"allowInitiatorRevoke":false}""");

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9710, InitiatorId);

        Assert.False(result.Success);
        Assert.Equal("该流程不允许发起人撤回", result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9710);
        Assert.Equal("active", card.FStatus);   // 未被撤回
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeMissing_Succeeds()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeMissing_Succeeds));
        await SeedActiveCardAsync(db, cardId: 9711, stageInstanceId: 9811, assigneeId: 9911,
            flowSettingsJson: null);   // 存量流程：无该键 → 缺失即允许

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9711, InitiatorId);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9711);
        Assert.Equal("draft", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeTrue_Succeeds()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeTrue_Succeeds));
        await SeedActiveCardAsync(db, cardId: 9712, stageInstanceId: 9812, assigneeId: 9912,
            flowSettingsJson: """{"allowInitiatorRevoke":true}""");

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9712, InitiatorId);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9712);
        Assert.Equal("draft", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_NonInitiator_IsRejected_RegardlessOfGate()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_NonInitiator_IsRejected_RegardlessOfGate));
        await SeedActiveCardAsync(db, cardId: 9713, stageInstanceId: 9813, assigneeId: 9913,
            flowSettingsJson: null);   // 即使允许撤回，非发起人/代提交人仍被既有校验拦（gate 在其后，不越位）

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9713, OtherUserId);

        Assert.False(result.Success);
        Assert.Equal("只有发起人或代提交人可以撤回", result.Message);
    }

    /// <summary>播种一张 active 卡：当前节点 human/active + 唯一 pending 处理人；版本携带指定 flowSettingsJson。</summary>
    private static async global::System.Threading.Tasks.Task SeedActiveCardAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long cardId, long stageInstanceId, long assigneeId, string? flowSettingsJson)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "撤回gate回归", FFlowCode = $"revoke-gate-{cardId}", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true,
            FFlowSettingsJson = flowSettingsJson
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = StageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}"""
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "撤回gate", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = stageInstanceId, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = stageInstanceId, FCardId = cardId, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = assigneeId, FStageInstanceId = stageInstanceId, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
    }

    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
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
            new ConditionRuleEvaluator(),
            new ApproverResolver(db),
            new FakeBudgetOccupationService(),
            new DbTodoService(db),
            new FakeNotificationDispatcher(),
            new AutoPluginFactory(provider),
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            orchestration,
            new FakeBatchNotifier(),
            new FakeBatchLifecycleService(),
            NullLogger<FlowEngineService>.Instance);
    }
}
```

> 注：`Fake*` 测试替身与 `TestDbContextFactory` 是 `CardFlow.Tests` 项目内既有公共类型（`CrossStageDeduplicateTests`/`FlowActionNoTrackingPersistenceTests` 同款用法），直接复用。

- [ ] **Step 2: 跑测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: FAIL —— `Withdraw_AllowInitiatorRevokeFalse_IsRejected` 失败（当前无 gate，撤回成功、卡片变 draft）。另两用例（缺失/true）此刻即通过。

- [ ] **Step 3: WithdrawAsync 加 gate**

`FlowEngineService.cs:WithdrawAsync`：在状态校验 `:1352`（`if (card.FStatus != "active") return CardOperationResult.Fail("当前状态不允许撤回");`）之后、当前节点校验 `:1354` 之前，插入：

```csharp
                // M8-D 件②：定义级 gate —— 读卡片锁定版本设置，仅显式关闭时拦（缺失/true 放行，保留现状）
                var flowSettingsJson = await _dbContext.Set<CfFlowVersion>()
                    .Where(version => version.FID == card.FFlowVersionId)
                    .Select(version => version.FFlowSettingsJson)
                    .FirstOrDefaultAsync();
                if (!FlowSettingsReader.ReadBool(flowSettingsJson, "allowInitiatorRevoke", true))
                    return CardOperationResult.Fail("该流程不允许发起人撤回");
```

- [ ] **Step 4: 跑测试确认通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: PASS —— 三用例全绿；既有 `Withdraw_UnderNoTracking_PersistsDraftStatus`（版本无设置=缺失）不回归。

- [ ] **Step 5: 前端 FlowSettings 加键 + init 默认（默认 ON）**

`FlowDefinitionEditPage.vue`：`FlowSettings` 接口（Task 1 已加 `skipDuplicateApprover` 后）再加：

```typescript
  skipDuplicateApprover: boolean
  allowInitiatorRevoke: boolean
```

`initialState().settings` 加默认（**true**，对齐撤回现状放开）：

```typescript
    skipDuplicateApprover: false,
    allowInitiatorRevoke: true,
```

- [ ] **Step 6: 前端「允许发起人撤回」灰行转真开关**

`FlowDefinitionEditPage.vue:2807-2813` 整块替换为：

```html
              <!-- 允许发起人撤回（定义级，M8-D 件②）：缺省允许；WithdrawAsync 读锁定版本 gate -->
              <div class="cfd-setrow">
                <a-switch v-model:checked="state.settings.allowInitiatorRevoke" size="small" />
                <div class="cfd-setrow__text">
                  <div class="cfd-setrow__title">允许发起人撤回</div>
                  <div class="cfd-setrow__desc">流程进行中、当前节点无人审批时，允许发起人撤回，撤回后回到草稿。关闭后发起人无法撤回。</div>
                </div>
              </div>
```

- [ ] **Step 7: 前端门禁**

Run（在 `web/`）：`npm run type-check` 然后 `npm run lint:style`
Expected: 均 PASS。

- [ ] **Step 8: 提交件②**

```bash
git add src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs \
        web/src/views/cardflow/FlowDefinitionEditPage.vue \
        tests/STOTOP.Module.CardFlow.Tests/Approval/InitiatorRevokeGateTests.cs
git commit -m "feat(cardflow): 允许发起人撤回定义级开关 + WithdrawAsync gate (M8-D 件②)"
```
Expected: hook 编译门禁通过。

---

## Task 3: 件② P1 — 运行时撤回按钮尊重开关

> 说明：本任务让 PC/移动运行时撤回按钮在流程显式关闭撤回时隐藏（否则点了才被后端拒，体验差）。后端 gate（Task 2）是权威兜底；本任务只做**展示层**。JSON 解析正确性已由 `FlowSettingsReader` 单测（Task 1/2）覆盖，故不新增后端单测——DTO 透出为同一读取器的平凡接线。**独立 commit**（与 spec「2 commit」相比多一个，为 P1 可评审性；如需可与 Task 2 合并提交）。

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Responses.cs:233-248`（`CardDetailDto`）
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs:258-260`（`GetByIdAsync`）
- Modify: `web/src/types/cardflow.ts:407-415`（`CardDetailDto`）
- Modify: `web/src/views/cardflow/CardDetailPage.vue:134-139,166`
- Modify: `web/src/components/cardflow/CardFlowPanel.vue:1552`

**Interfaces:**
- Consumes: `FlowSettingsReader.ReadBool`（Task 1）；`GetByIdAsync` 内已加载的 `flowVersion`（`CardService.cs:258-260`）。
- Produces: `CardDetailDto.AllowInitiatorRevoke : bool`（后端）/ `CardDetailDto.allowInitiatorRevoke? : boolean`（前端）。

- [ ] **Step 1: 后端 DTO 加只读字段**

`Responses.cs` `CardDetailDto`（`:233-248`）在 `CurrentStageWorkView` 后加（默认 true）：

```csharp
    public StageWorkViewDto? CurrentStageWorkView { get; set; }
    /// <summary>M8-D 件② P1：该卡锁定版本是否允许发起人撤回（allowInitiatorRevoke，缺失=true）。仅供运行时按钮展示，权威 gate 在 WithdrawAsync。</summary>
    public bool AllowInitiatorRevoke { get; set; } = true;
```

- [ ] **Step 2: GetByIdAsync 透出标志**

`CardService.cs:GetByIdAsync`：`flowVersion` 已在 `:258-260` 加载。在其后（用到 `flowVersion` 的分支之前，如 `:262` 前）加一行：

```csharp
        var flowVersion = await _dbContext.Set<CfFlowVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.FID == card.FFlowVersionId);
        detail.AllowInitiatorRevoke = FlowSettingsReader.ReadBool(flowVersion?.FFlowSettingsJson, "allowInitiatorRevoke", true);
```

（`FlowSettingsReader` 与 `CardService` 同命名空间 `STOTOP.Module.CardFlow.Services`，无需 using。）

- [ ] **Step 3: 后端编译确认**

Run: `scripts/dev/build-filter.ps1 cardflow`
Expected: PASS（编译通过；无 schema 变更）。

- [ ] **Step 4: 前端类型加字段**

`web/src/types/cardflow.ts` `CardDetailDto`（`:407-415`）在 `currentRound` 附近加：

```typescript
  currentRound: number
  /** M8-D 件② P1：该卡锁定版本是否允许发起人撤回（缺失=true） */
  allowInitiatorRevoke?: boolean
```

- [ ] **Step 5: PC 撤回按钮 gate**

`CardDetailPage.vue`：`canWithdraw`（`:134-139`）末尾加条件：

```typescript
const canWithdraw = computed(
  () =>
    isInitiator.value &&
    card.value?.status === 'active' &&
    !hasAnyApproval.value &&
    card.value?.allowInitiatorRevoke !== false
)
```

`showToolbarWithdraw`（`:166`）改为：

```typescript
const showToolbarWithdraw = computed(() => isInitiator.value && card.value?.allowInitiatorRevoke !== false)
```

- [ ] **Step 6: 移动撤回按钮 gate**

`CardFlowPanel.vue:1552` 的撤回按钮加 `v-if`（仅 gate 撤回，不动同行催办）：

```html
            <VanButton v-if="cardDetail.allowInitiatorRevoke !== false" size="small" plain :loading="submitting" :disabled="submitting" @click="doWithdraw">撤回</VanButton>
```

- [ ] **Step 7: 前端门禁**

Run（在 `web/`）：`npm run type-check` 然后 `npm run lint:style`
Expected: 均 PASS。

- [ ] **Step 8: 提交件② P1**

```bash
git add src/STOTOP.Module.CardFlow/Dtos/Responses.cs \
        src/STOTOP.Module.CardFlow/Services/CardService.cs \
        web/src/types/cardflow.ts \
        web/src/views/cardflow/CardDetailPage.vue \
        web/src/components/cardflow/CardFlowPanel.vue
git commit -m "feat(cardflow): 运行时撤回按钮尊重定义级开关(卡片详情透出标志) (M8-D 件② P1)"
```
Expected: hook 编译门禁通过。

---

## 收口（批收尾，非单任务）

- [ ] **子代理对抗性只读整体终审**：dispatch general-purpose 只读审 3 件全 diff——核对 OR 叠加语义、gate 三态（缺失即允许）、读锁定版本而非草稿、前端默认值方向（去重 false / 撤回 true）、无裸 hex/any、停用节点确未触碰。
- [ ] **全量回归**：`scripts/dev/test-dotnet.ps1 CardFlow`（flaky 多跑）+ `web/` `npm run type-check` + `npm run lint:style` 全绿。
- [ ] **preview 验证件② P1**（可选，若起前端）：造一个 `allowInitiatorRevoke:false` 的流程 → 发起卡 → PC/移动撤回按钮应隐藏；`true`/缺失 → 按钮在。
- [ ] 三 commit 均**未 push**，等用户点头。
