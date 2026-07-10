# CardFlow M8-A 发起节点三件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让引擎真正消费"发起节点"三件——重提重路由(E1)、结构化发起范围校验、代提交(onBehalf)——并把设计器起点弹层升级为可配置的"发起抽屉"，全程守"不做假配置"。

**Architecture:** 后端全在 `STOTOP.Module.CardFlow`。发起范围 + 代提交范围合存 `CfFlowDefinition` 新列 `F发起策略JSON`（一列两策略）；代提交运行时留痕存 `CfCard` 新列 `F代理人ID/F代理人姓名`；重提策略复用已存的 `CfFlowVersion.FFlowSettingsJson.resubmitStrategy`。schema 变更走版本化 seeder V71/V72（无 EF migrations）。前端起点弹层升级为复用现有 `a-drawer` 外壳的发起抽屉。

**Tech Stack:** .NET 10 / EF Core / SQL Server / xUnit(InMemory) ；Vue 3 / TS / Ant Design Vue / Pinia / vitest。

设计真源：[docs/superpowers/specs/2026-07-09-cardflow-M8-A-发起节点三件-design.md](../specs/2026-07-09-cardflow-M8-A-发起节点三件-design.md)。

## Global Constraints

- **不新增 `cc` FType**：引擎节点分派二元 `FType=="auto"?auto:human`（FlowEngineService.cs:508）。本批不碰节点类型。
- **不做假配置**：引擎不消费的 UI 不出真开关。件① 落地后 `重提策略` 注释"（引擎真消费）"才成真。
- **无 EF migrations**：schema 走版本化 seeder（V 编号），原生 SQL 用 `ExecSql`/`SeederHelper`，`if (!SeederHelper.IsSqlServer(ctx)) return;` 守卫；InMemory 测试库靠实体+EF 配置自动建列，不跑 seeder。
- **DB 列名 `F+中文`，C# 属性 `F+PascalCase`，`HasColumnName` 映射**。表名：`CfFlowDefinition`→`CF卡片流程`，`CfCard`→`CF流程实例`，`CfFlowVersion`→（不改）。
- **全局 NoTracking**：纯读 `AnyAsync/ToListAsync` 无碍；回写实体前 `.AsTracking()` 或显式 `_dbContext.Entry(x).State = EntityState.Modified`。
- **真库事务须 `IExecutionStrategy.ExecuteAsync` 包裹**（已有代码即此模式，勿改）。
- **向后兼容硬约束**：`F发起策略JSON` 为 null/空 → 发起范围=不限制、代提交=关闭；`ActualInitiatorId` 为 null → 本人发起（现状）。**否则一上线既有流程发起全崩**。
- **系统触发链豁免**发起范围校验：`BatchTriggerService`/编排/fileUpload 为系统身份，不做发起人范围校验。
- 后端 `build-filter cardflow`（slash `/build cardflow`）+ `test-dotnet cardflow`（slash `/test cardflow`）；前端 `type-check`+`vitest`+`lint:style`（零裸 hex）。
- **每件独立 commit，经 hook 编译门禁；不 push 等人点头。** CardFlow.Tests flaky，判回归多跑几次。
- 提交后端命名：`fix(cardflow)`/`feat(cardflow)`；commit message 结尾加 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`。

---

# Commit 1 — 件① E1 重提强制重路由（引擎，无 seeder）

引擎读版本 `FFlowSettingsJson.resubmitStrategy`：`fromRejected` 从最近被驳回节点续跑，`fromStart`（默认）从头。UI 无需改（单选已在、注释已写"引擎真消费"，落地后成真）。

## Task 1: ResubmitAsync 消费 resubmitStrategy

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（`ResubmitAsync` :1021-1105；新增 private helper）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/ResubmitStrategyTests.cs`（Create）

**Interfaces:**
- Consumes: `CfFlowVersion.FFlowSettingsJson`（既有列）、`CfStageInstance.FFinalAction`（既有，reject 置 `"rejected"`）。
- Produces: 无新增公共签名（仅改 `ResubmitAsync` 内部行为 + 新增 `private static string GetResubmitStrategy(string?)`）。

- [ ] **Step 1: 写失败测试**（mirror `FlowActionNoTrackingPersistenceTests` 的 `CreateNoTrackingDb`/`CreateEngine`）

在新文件写两用例。种子：两节点流程 A(sort1,StageDefId=6201)→B(sort2,StageDefId=6202)，版本 `FFlowSettingsJson` 置策略；卡片 `returned`，并预置一条 B 节点已驳回实例（`FFinalAction="rejected"`,`FStatus="returned"`,`FRound=1`）。

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ResubmitStrategyTests
{
    private const long DefId = 3400, VerId = 3401, StageA = 6201, StageB = 6202, Initiator = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task 重提fromRejected从被驳回节点续跑()
    {
        using var db = CreateDb(nameof(重提fromRejected从被驳回节点续跑), """{"resubmitStrategy":"fromRejected"}""");
        SeedReturnedCardRejectedAtB(db);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var result = await CreateEngine(db).ResubmitAsync(9710, Initiator);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var newStage = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9710 && s.FRound == 2);
        Assert.Equal(StageB, newStage.FStageDefinitionId); // 回到被驳回的 B，非 A
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 重提fromStart默认回到首节点()
    {
        using var db = CreateDb(nameof(重提fromStart默认回到首节点), null); // 无策略=缺省 fromStart
        SeedReturnedCardRejectedAtB(db);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var result = await CreateEngine(db).ResubmitAsync(9710, Initiator);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var newStage = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9710 && s.FRound == 2);
        Assert.Equal(StageA, newStage.FStageDefinitionId); // 回到首节点 A
    }

    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateDb(string name, string? settingsJson)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = DefId, FFlowName = "重提策略", FFlowCode = "resubmit-strategy", FOrgId = 1, FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = VerId, FFlowDefinitionId = DefId, FStatus = "published", FIsCurrentVersion = true, FFlowSettingsJson = settingsJson });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = StageA, FFlowVersionId = VerId, FSortOrder = 1, FStageName = "A", FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers", FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}""" });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = StageB, FFlowVersionId = VerId, FSortOrder = 2, FStageName = "B", FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers", FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}""" });
        db.Set<SysUser>().Add(new SysUser { FID = 51, FName = "审批人" });
        return db;
    }

    private static void SeedReturnedCardRejectedAtB(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfCard>().Add(new CfCard { FID = 9710, FFlowDefinitionId = DefId, FFlowVersionId = VerId, FTitle = "重提", FStatus = "returned", FInitiatorId = Initiator, FInitiatorName = "发起人", FCurrentRound = 1, FOrgId = 1, FDataJson = "{}" });
        db.Set<CfStageInstance>().Add(new CfStageInstance { FID = 9810, FCardId = 9710, FStageDefinitionId = StageB, FStageName = "B", FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "returned", FFinalAction = "rejected", FCompletedTime = DateTime.Now });
    }

    // CreateEngine：整体照抄 FlowActionNoTrackingPersistenceTests.CreateEngine（同一 fakes 装配）
    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var orchestration = new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance);
        return new FlowEngineService(db, new FakeNumberSequenceService(), new FakeCardSchemaService(),
            new ApprovalModeHandler(), new SequentialApprovalRuntime(), new ReturnToStageRuntime(),
            new StageConfigParser(), new StageFieldAccessService(), new StageActionPolicyService(),
            new ConditionRuleEvaluator(), new ApproverResolver(db), new FakeBudgetOccupationService(),
            new DbTodoService(db), new FakeNotificationDispatcher(), new AutoPluginFactory(provider),
            provider, provider.GetRequiredService<IServiceScopeFactory>(), orchestration,
            new FakeBatchNotifier(), new FakeBatchLifecycleService(), NullLogger<FlowEngineService>.Instance);
    }
}
```

- [ ] **Step 2: 跑测试确认红**

Run: `scripts/dev/test-dotnet.ps1 cardflow` 过滤 `ResubmitStrategyTests`（或 `/test cardflow`）。
Expected: `重提fromRejected从被驳回节点续跑` FAIL（现硬编码回 A，断言 B 不成立）；`重提fromStart默认回到首节点` 可能已 PASS。

- [ ] **Step 3: 实现引擎消费**

在 `FlowEngineService.cs` 文件顶部确认 `using System.Text.Json;`（若无则加）。在 `ResubmitAsync`（:1052-1059）把"取首节点"改为按策略取重启节点：

```csharp
// 获取节点定义
var stages = await _dbContext.Set<CfStageDefinition>()
    .Where(s => s.FFlowVersionId == card.FFlowVersionId)
    .OrderBy(s => s.FSortOrder)
    .ToListAsync();

if (stages.Count == 0) return CardOperationResult.Fail("流程无节点定义");

// 重提策略：fromRejected=从最近被驳回节点续跑；缺省 fromStart=从头
var version = await _dbContext.Set<CfFlowVersion>().AsNoTracking()
    .FirstOrDefaultAsync(v => v.FID == card.FFlowVersionId);
var strategy = GetResubmitStrategy(version?.FFlowSettingsJson);
CfStageDefinition restartStage = stages[0];
if (string.Equals(strategy, "fromRejected", StringComparison.OrdinalIgnoreCase))
{
    var lastRejected = await _dbContext.Set<CfStageInstance>().AsNoTracking()
        .Where(s => s.FCardId == card.FID && s.FFinalAction == "rejected")
        .OrderByDescending(s => s.FRound).ThenByDescending(s => s.FCompletedTime)
        .FirstOrDefaultAsync();
    var matched = lastRejected == null ? null
        : stages.FirstOrDefault(s => s.FID == lastRejected.FStageDefinitionId);
    if (matched != null) restartStage = matched; // 找不到→防御回退 stages[0]
}

var firstStage = restartStage;
```

在 `ResubmitAsync` 方法之后新增 helper：

```csharp
/// <summary>读版本流程设置里的重提策略（缺省 fromStart）；非法 JSON 静默降级。</summary>
private static string GetResubmitStrategy(string? flowSettingsJson)
{
    if (string.IsNullOrWhiteSpace(flowSettingsJson)) return "fromStart";
    try
    {
        using var doc = JsonDocument.Parse(flowSettingsJson);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("resubmitStrategy", out var v)
            && v.ValueKind == JsonValueKind.String)
        {
            return v.GetString() ?? "fromStart";
        }
    }
    catch (JsonException) { /* 静默降级 */ }
    return "fromStart";
}
```

> 说明：其余（`FCurrentRound+1`、建实例用 `firstStage`、`OccupyBudgetOnSubmitAsync` 幂等键已含 `resubmit:{round}`、auto/human 分派、`LogActionAsync "resubmit"`）保持不变。变量名仍用 `firstStage` 以最小化 diff。

- [ ] **Step 4: 跑测试确认绿**

Run: `/test cardflow`（过滤 `ResubmitStrategyTests`）。Expected: 两用例 PASS。

- [ ] **Step 5: 回归 + 提交**

Run: `/build cardflow` 编译过；`/test cardflow` 过滤 `FlowActionNoTrackingPersistenceTests` 仍绿（多跑 2 次防 flaky）。

```bash
git add src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs tests/STOTOP.Module.CardFlow.Tests/Approval/ResubmitStrategyTests.cs
git commit -m "feat(cardflow): 重提策略引擎消费 fromRejected 从被驳回节点续跑 (M8-A E1)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

# Commit 2 — 件② 结构化发起范围校验（seeder V71）

`CfFlowDefinition` 加 `F发起策略JSON`；引擎在发起流程清单与创建卡片时按 `initiatorScope`（角色/组织/岗位/人员 union，空=不限制）校验发起人；设计器起点弹层升级为发起抽屉。

## Task 2: 实体 + EF 配置 + seeder V71（schema）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfFlowDefinition.cs`
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfFlowDefinitionConfiguration.cs`
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`

**Interfaces:**
- Produces: `CfFlowDefinition.FStartPolicyJson : string?`（列 `F发起策略JSON`）。

- [ ] **Step 1: 加实体属性**

`CfFlowDefinition.cs`，在 `FAllowedRolesJson`（:13）下加：
```csharp
public string? FAllowedRolesJson { get; set; }
/// <summary>发起策略 JSON：initiatorScope(角色/组织/岗位/人员) + onBehalf(代提交开关+agentScope)。null=不限制发起、不允许代提交。</summary>
public string? FStartPolicyJson { get; set; }
```

- [ ] **Step 2: 加 EF 映射**

`CfFlowDefinitionConfiguration.cs`，在 `FAllowedRolesJson` 映射（:20）下加：
```csharp
builder.Property(e => e.FStartPolicyJson).HasColumnName("F发起策略JSON").HasColumnType("nvarchar(max)");
```

- [ ] **Step 3: 加 seeder V71**

`CardFlowSeeder.cs` steps 列表在 V70（:94）后加一行：
```csharp
            new(71, "M8-A 发起范围: CF卡片流程 加 F发起策略JSON 列(nvarchar max null, 结构化发起范围+代提交范围) (2026-07-10)", MigrateV71),
```
在 `MigrateV70` 方法之后加：
```csharp
/// <summary>V71：CF卡片流程 加 F发起策略JSON（M8-A 发起范围+代提交范围结构化策略，null=不限制）。</summary>
private static void MigrateV71(STOTOPDbContext ctx)
{
    if (!SeederHelper.IsSqlServer(ctx)) return;
    ExecSql(ctx, @"IF COL_LENGTH(N'CF卡片流程', N'F发起策略JSON') IS NULL
        ALTER TABLE [CF卡片流程] ADD [F发起策略JSON] NVARCHAR(MAX) NULL;");
}
```

- [ ] **Step 4: 编译 + 提交（本 task 无独立测试，随 Task 3-6 一起验证；此步只确保编译）**

Run: `/build cardflow`。Expected: 编译通过。（暂不提交，与 Task 3-6 合成 commit 2；或先提交 schema 骨架，见 Task 6 收口。）

## Task 3: StartPolicy 模型 + InitiatorScopeResolver（核心逻辑，TDD）

**Files:**
- Create: `src/STOTOP.Module.CardFlow/Models/StartPolicyModels.cs`
- Create: `src/STOTOP.Module.CardFlow/Services/Interfaces/IInitiatorScopeResolver.cs`
- Create: `src/STOTOP.Module.CardFlow/Services/InitiatorScopeResolver.cs`
- Modify: `src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs`（DI 注册）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/InitiatorScopeResolverTests.cs`（Create）

**Interfaces:**
- Produces:
  - `StartPolicy { InitiatorScope? InitiatorScope; OnBehalfPolicy? OnBehalf }`
  - `InitiatorScope { List<long> Roles/Orgs/Positions/Users; bool IsEmpty }`
  - `OnBehalfPolicy { bool Enabled; InitiatorScope AgentScope }`
  - `StartPolicyCodec.Parse(string? startPolicyJson, string? legacyAllowedRolesJson) : StartPolicy`
  - `IInitiatorScopeResolver.GetUserMembershipsAsync(long userId, CancellationToken) : Task<UserMemberships>`
  - `IInitiatorScopeResolver.IsInScope(UserMemberships m, long userId, InitiatorScope? scope) : bool`
  - `UserMemberships(HashSet<long> RoleIds, HashSet<long> OrgIds, HashSet<long> PositionIds)`

- [ ] **Step 1: 写模型**（`Models/StartPolicyModels.cs`）

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Models;

public sealed class StartPolicy
{
    public InitiatorScope? InitiatorScope { get; set; }
    public OnBehalfPolicy? OnBehalf { get; set; }
}

public sealed class InitiatorScope
{
    public List<long> Roles { get; set; } = new();
    public List<long> Orgs { get; set; } = new();
    public List<long> Positions { get; set; } = new();
    public List<long> Users { get; set; } = new();

    [JsonIgnore]
    public bool IsEmpty => Roles.Count == 0 && Orgs.Count == 0 && Positions.Count == 0 && Users.Count == 0;
}

public sealed class OnBehalfPolicy
{
    public bool Enabled { get; set; }
    public InitiatorScope AgentScope { get; set; } = new();
}

public static class StartPolicyCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>解析发起策略；startPolicyJson 为空时从 legacy 可发起角色JSON 派生角色维（向后兼容，无数据回填）。非法 JSON 静默降级为空策略（=不限制）。</summary>
    public static StartPolicy Parse(string? startPolicyJson, string? legacyAllowedRolesJson)
    {
        if (!string.IsNullOrWhiteSpace(startPolicyJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<StartPolicy>(startPolicyJson, Options);
                if (parsed != null) return parsed;
            }
            catch (JsonException) { /* 静默降级 */ }
        }

        var policy = new StartPolicy();
        if (!string.IsNullOrWhiteSpace(legacyAllowedRolesJson))
        {
            try
            {
                var roleStrings = JsonSerializer.Deserialize<List<string>>(legacyAllowedRolesJson, Options) ?? new();
                var roleIds = roleStrings.Select(s => long.TryParse(s, out var id) ? id : 0L).Where(id => id > 0).ToList();
                if (roleIds.Count > 0) policy.InitiatorScope = new InitiatorScope { Roles = roleIds };
            }
            catch (JsonException) { /* 静默降级 */ }
        }
        return policy;
    }
}
```

- [ ] **Step 2: 写接口**（`Services/Interfaces/IInitiatorScopeResolver.cs`）

```csharp
using STOTOP.Module.CardFlow.Models;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public sealed record UserMemberships(HashSet<long> RoleIds, HashSet<long> OrgIds, HashSet<long> PositionIds);

public interface IInitiatorScopeResolver
{
    Task<UserMemberships> GetUserMembershipsAsync(long userId, CancellationToken ct = default);
    bool IsInScope(UserMemberships memberships, long userId, InitiatorScope? scope);
}
```

- [ ] **Step 3: 写失败测试**（`Rules/InitiatorScopeResolverTests.cs`）

```csharp
using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class InitiatorScopeResolverTests
{
    private const long UserId = 700;

    private static STOTOP.Infrastructure.Data.STOTOPDbContext Db(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.Set<SysUserRole>().Add(new SysUserRole { FID = 1, FUserId = UserId, FRoleId = 10 });
        db.Set<SysUserOrganization>().Add(new SysUserOrganization { FID = 1, FUserId = UserId, FOrgId = 20, FStatus = 1 });
        db.Set<SysUserPosition>().Add(new SysUserPosition { FID = 1, FUserId = UserId, FPositionId = 30 });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 空scope放行()
    {
        using var db = Db(nameof(空scope放行));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope()));   // 全空=不限制
        Assert.True(r.IsInScope(m, UserId, null));                    // null=不限制
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 角色命中放行未命中拒绝()
    {
        using var db = Db(nameof(角色命中放行未命中拒绝));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 10 } }));
        Assert.False(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 } }));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 组织岗位人员维度各自命中()
    {
        using var db = Db(nameof(组织岗位人员维度各自命中));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Orgs = { 20 } }));
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Positions = { 30 } }));
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Users = { UserId } }));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task union任一维度命中即放行()
    {
        using var db = Db(nameof(union任一维度命中即放行));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        // 角色不中(99) 但 组织中(20) → union 放行
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 }, Orgs = { 20 } }));
        // 全不中 → 拒绝
        Assert.False(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 }, Orgs = { 88 }, Positions = { 77 }, Users = { 66 } }));
    }

    [Fact]
    public void 兼容读legacy可发起角色JSON派生角色维()
    {
        var p = StartPolicyCodec.Parse(null, "[\"10\",\"11\"]");
        Assert.NotNull(p.InitiatorScope);
        Assert.Equal(new List<long> { 10, 11 }, p.InitiatorScope!.Roles);
    }
}
```

> 若 `SysUserRole`/`SysUserOrganization`/`SysUserPosition` 的字段名与此处不符（如 `FStatus` 类型），执行时先 Read 这三个实体（`src/STOTOP.Module.System/Entities/`）核对：已知 `SysUserRole(FUserId,FRoleId)`、`SysUserOrganization(FUserId,FOrgId,FStatus,F是否当前)`、`SysUserPosition(FUserId,FPositionId,FIsPrimary)`（后者无 FStatus）。

- [ ] **Step 4: 跑测试确认红**

Run: `/test cardflow`（过滤 `InitiatorScopeResolverTests`）。Expected: FAIL（`InitiatorScopeResolver` 未定义）。

- [ ] **Step 5: 写实现**（`Services/InitiatorScopeResolver.cs`）

```csharp
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>发起范围/代提交范围成员判定：给定用户归属(角色/组织/岗位) union 比对 scope。空 scope=不限制。</summary>
public sealed class InitiatorScopeResolver : IInitiatorScopeResolver
{
    private readonly STOTOPDbContext _dbContext;
    public InitiatorScopeResolver(STOTOPDbContext dbContext) => _dbContext = dbContext;

    public async Task<UserMemberships> GetUserMembershipsAsync(long userId, CancellationToken ct = default)
    {
        var roleIds = await _dbContext.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId).Select(ur => ur.FRoleId).ToListAsync(ct);
        var orgIds = await _dbContext.Set<SysUserOrganization>()
            .Where(uo => uo.FUserId == userId && uo.FStatus == 1).Select(uo => uo.FOrgId).ToListAsync(ct);
        var positionIds = await _dbContext.Set<SysUserPosition>()
            .Where(up => up.FUserId == userId).Select(up => up.FPositionId).ToListAsync(ct);
        return new UserMemberships(roleIds.ToHashSet(), orgIds.ToHashSet(), positionIds.ToHashSet());
    }

    public bool IsInScope(UserMemberships memberships, long userId, InitiatorScope? scope)
    {
        if (scope == null || scope.IsEmpty) return true;                    // 不限制
        if (scope.Users.Contains(userId)) return true;
        if (scope.Roles.Any(memberships.RoleIds.Contains)) return true;
        if (scope.Orgs.Any(memberships.OrgIds.Contains)) return true;
        if (scope.Positions.Any(memberships.PositionIds.Contains)) return true;
        return false;
    }
}
```

- [ ] **Step 6: 注册 DI**

`CardFlowModuleExtensions.cs` 找到其他 `services.AddScoped<IXxx, Xxx>()` 处，加：
```csharp
services.AddScoped<IInitiatorScopeResolver, InitiatorScopeResolver>();
```

- [ ] **Step 7: 跑测试确认绿**

Run: `/test cardflow`（过滤 `InitiatorScopeResolverTests`）。Expected: 全 PASS。

## Task 4: CreateAsync + GetAvailableFlowsAsync 消费发起范围

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs`（ctor 注入 resolver；`GetAvailableFlowsAsync` :41；`CreateAsync` :756）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/InitiatorScopeEnforcementTests.cs`（Create）

**Interfaces:**
- Consumes: `IInitiatorScopeResolver`、`StartPolicyCodec.Parse`、`CfFlowDefinition.FStartPolicyJson/FAllowedRolesJson`。
- Produces: `CreateAsync` 未授权发起 → `throw InvalidOperationException("无发起权限")`；`GetAvailableFlowsAsync` 按发起范围过滤。

- [ ] **Step 1: 写失败测试**（构造 CardService；执行时 Read `CardService.cs:18-39` ctor 与 `tests/.../Approval/FlowEngineTestFakes.cs` 复用/仿造 fakes：`ICardFlowSourceContextVerifier`→返回 `VerifyAsync` 成功空结果、`ICardRedactionService`→passthrough；`StageConfigParser`/stageViewResolver 用真实无参实现）

```csharp
[Fact]
public async global::System.Threading.Tasks.Task 发起人不在发起范围内_创建被拒()
{
    using var db = TestDbContextFactory.Create(nameof(发起人不在发起范围内_创建被拒));
    db.Set<CfFlowDefinition>().Add(new CfFlowDefinition {
        FID = 3500, FFlowName="范围流程", FFlowCode="scope-flow", FOrgId=1, FStatus="published",
        FCreatorId=1, FCreatedTime=DateTime.Now,
        FStartPolicyJson = """{"initiatorScope":{"roles":[10]}}""" }); // 仅角色10可发起
    db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID=3501, FFlowDefinitionId=3500, FStatus="published", FIsCurrentVersion=true });
    // 用户 700 无角色10
    await db.SaveChangesAsync();

    var svc = BuildCardService(db);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3500, OrgId = 1, DataJson = "{}" }, userId: 700));
}

[Fact]
public async global::System.Threading.Tasks.Task 发起范围为空_任何人可创建()
{
    using var db = TestDbContextFactory.Create(nameof(发起范围为空_任何人可创建));
    db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID=3510, FFlowName="开放流程", FFlowCode="open-flow", FOrgId=1, FStatus="published", FCreatorId=1, FCreatedTime=DateTime.Now });
    db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID=3511, FFlowDefinitionId=3510, FStatus="published", FIsCurrentVersion=true });
    await db.SaveChangesAsync();

    var svc = BuildCardService(db);
    var card = await svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3510, OrgId = 1, DataJson = "{}" }, userId: 700);
    Assert.NotNull(card);
}
```
（`BuildCardService(db)` 私有 helper：`new CardService(db, NullLogger<CardService>.Instance, new StageConfigParser(), <stageViewResolver>, <fakeSourceVerifier>, <fakeRedaction>, new InitiatorScopeResolver(db))`——具体 fake 类型按 ctor 参数补齐。）

- [ ] **Step 2: 跑测试确认红**

Run: `/test cardflow`（过滤 `InitiatorScopeEnforcementTests`）。Expected: `发起人不在发起范围内_创建被拒` FAIL（现无校验，不抛）。

- [ ] **Step 3: ctor 注入 resolver**

`CardService.cs` ctor（:18-39）加参数 `IInitiatorScopeResolver initiatorScopeResolver` 并存字段 `_initiatorScopeResolver`。

- [ ] **Step 4: CreateAsync 加校验**

`CreateAsync`（:758 加载 flowDef 后、:769 建 card 前）插入：
```csharp
var startPolicy = StartPolicyCodec.Parse(flowDef.FStartPolicyJson, flowDef.FAllowedRolesJson);
if (startPolicy.InitiatorScope is { IsEmpty: false } scope)
{
    var memberships = await _initiatorScopeResolver.GetUserMembershipsAsync(userId);
    if (!_initiatorScopeResolver.IsInScope(memberships, userId, scope))
        throw new InvalidOperationException("您不在该流程的可发起范围内，无法发起");
}
```

- [ ] **Step 5: GetAvailableFlowsAsync 加过滤**

把 `GetAvailableFlowsAsync`（:41-54）改为先取候选（含策略列），再按发起范围内存过滤：
```csharp
public async Task<List<AvailableFlowDto>> GetAvailableFlowsAsync(long userId, long orgId)
{
    var candidates = await _dbContext.Set<CfFlowDefinition>()
        .Where(x => x.FStatus == "published" && x.FOrgId == orgId)
        .Where(x => x.FTriggerConfigJson == null || !x.FTriggerConfigJson.Contains("fileUpload"))
        .Select(x => new { x.FID, x.FFlowName, x.FFlowCode, x.FDescription, x.FStartPolicyJson, x.FAllowedRolesJson })
        .ToListAsync();

    UserMemberships? memberships = null;
    var result = new List<AvailableFlowDto>();
    foreach (var c in candidates)
    {
        var scope = StartPolicyCodec.Parse(c.FStartPolicyJson, c.FAllowedRolesJson).InitiatorScope;
        if (scope is { IsEmpty: false })
        {
            memberships ??= await _initiatorScopeResolver.GetUserMembershipsAsync(userId);
            if (!_initiatorScopeResolver.IsInScope(memberships, userId, scope)) continue;
        }
        result.Add(new AvailableFlowDto { Id = c.FID, FlowName = c.FFlowName, FlowCode = c.FFlowCode, Description = c.FDescription });
    }
    return result;
}
```

- [ ] **Step 6: 跑测试确认绿 + 回归**

Run: `/test cardflow`（过滤 `InitiatorScopeEnforcementTests`）→ PASS。再 `/build cardflow` 编过。

## Task 5: FlowDefinition DTO + 服务映射（端到端 startPolicyJson）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Requests.cs`（Create/Update 流程定义请求）
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Responses.cs`（FlowDefinitionDto）
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs`（10 处映射）
- Modify: `web/src/types/cardflow.ts`（3 个 interface）

**Interfaces:**
- Produces: `startPolicyJson` 端到端读写（create/update/get/list/template/clone）。

- [ ] **Step 1: 后端 DTO**

`Requests.cs`：`CreateFlowDefinitionRequest`（:78-89）与 `UpdateFlowDefinitionRequest`（:91-100）各加：
```csharp
public string? StartPolicyJson { get; set; }
```
`Responses.cs`：`FlowDefinitionDto`（:5-29）在 `AllowedRolesJson` 下加：
```csharp
public string? StartPolicyJson { get; set; }
```

- [ ] **Step 2: 服务映射（对称加 10 处，照 `FAllowedRolesJson`）**

`FlowDefinitionService.cs` 每一处 `AllowedRolesJson`/`FAllowedRolesJson` 旁加对应 `StartPolicyJson`/`FStartPolicyJson`：
- :102 `ListAsync` DTO：`StartPolicyJson = x.FStartPolicyJson,`
- :148 `GetByIdAsync` DTO：`StartPolicyJson = entity.FStartPolicyJson,`
- :169 `CreateAsync` 实体写：`FStartPolicyJson = request.StartPolicyJson,`
- :190 `CreateAsync` 返回 DTO：`StartPolicyJson = entity.FStartPolicyJson,`
- :211-212 `UpdateAsync` 条件写：`if (request.StartPolicyJson != null) entity.FStartPolicyJson = request.StartPolicyJson;`
- :230 `UpdateAsync` 返回 DTO：`StartPolicyJson = entity.FStartPolicyJson,`
- :1183 `CloneInternalAsync`：`FStartPolicyJson = sourceDefinition.FStartPolicyJson,`
- :1276 `GetTemplatesAsync` 投影：`StartPolicyJson = x.FStartPolicyJson,`
- :1335 `SaveAsTemplateAsync`：`existingTemplate.FStartPolicyJson = source.FStartPolicyJson;`
- :1449 `MapToDto`：`StartPolicyJson = entity.FStartPolicyJson,`

> 执行时对每处先 Read 上下文确认行号未漂移（可 grep `AllowedRolesJson` 定位）。

- [ ] **Step 3: 前端类型**

`web/src/types/cardflow.ts`：
- `FlowDefinitionDto`（:13 附近）加：`startPolicyJson?: string | null`
- `CreateFlowDefinitionRequest`（:46 附近）加：`startPolicyJson?: string | null`
- `UpdateFlowDefinitionRequest`（:57 附近）加：`startPolicyJson?: string | null`

- [ ] **Step 4: 编译 + type-check**

Run: `/build cardflow`；`cd web && npm run type-check`。Expected: 均过。

## Task 6: 发起抽屉 UI（起点弹层升级 + 四维选择器）

**Files:**
- Create: `web/src/components/cardflow/startPolicyShared.ts`（+ `.spec.ts`）
- Modify: `web/src/components/cardflow/designer/FlowVerticalGraph.vue`（起点 emit）
- Modify: `web/src/views/cardflow/FlowDefinitionEditPage.vue`（state + drawer 分支 + 发起抽屉内容 + 保存/回读 + 删旧"可发起角色"行）

**Interfaces:**
- Consumes: `getRoleList`/`getPositionList`（`@/api/system`）、`useUserSearch`/`useOrgSearch`（`@/composables`）、抽屉外壳（FlowDefinitionEditPage.vue:2929）。
- Produces: `state.basic.initiatorScope`（`{roles:number[],orgs:number[],positions:number[],users:number[]}`）序列化进 `startPolicyJson`。

- [ ] **Step 1: 写 shared + vitest**（`startPolicyShared.ts`）

```ts
export interface ScopeDims { roles: number[]; orgs: number[]; positions: number[]; users: number[] }
export interface OnBehalfConfig { enabled: boolean; agentScope: ScopeDims }
export interface StartPolicy { initiatorScope: ScopeDims; onBehalf: OnBehalfConfig }

export function emptyScope(): ScopeDims { return { roles: [], orgs: [], positions: [], users: [] } }
export function emptyStartPolicy(): StartPolicy {
  return { initiatorScope: emptyScope(), onBehalf: { enabled: false, agentScope: emptyScope() } }
}
export function isScopeEmpty(s: ScopeDims): boolean {
  return !s.roles.length && !s.orgs.length && !s.positions.length && !s.users.length
}
/** 解析后端 startPolicyJson；空则回退 allowedRolesJson 派生角色维（前端兼容显示）。 */
export function parseStartPolicy(startPolicyJson?: string | null, allowedRolesJson?: string | null): StartPolicy {
  const p = emptyStartPolicy()
  if (startPolicyJson) {
    try {
      const raw = JSON.parse(startPolicyJson)
      const s = raw?.initiatorScope ?? {}
      p.initiatorScope = { roles: nums(s.roles), orgs: nums(s.orgs), positions: nums(s.positions), users: nums(s.users) }
      if (raw?.onBehalf) p.onBehalf = { enabled: !!raw.onBehalf.enabled, agentScope: {
        roles: nums(raw.onBehalf.agentScope?.roles), orgs: nums(raw.onBehalf.agentScope?.orgs),
        positions: nums(raw.onBehalf.agentScope?.positions), users: nums(raw.onBehalf.agentScope?.users) } }
      return p
    } catch { /* 降级到 legacy */ }
  }
  if (allowedRolesJson) {
    try { p.initiatorScope.roles = nums(JSON.parse(allowedRolesJson)) } catch { /* ignore */ }
  }
  return p
}
/** 序列化为后端列值；全空返回 undefined（=不写策略，保持不限制）。 */
export function serializeStartPolicy(p: StartPolicy): string | undefined {
  const meaningful = !isScopeEmpty(p.initiatorScope) || p.onBehalf.enabled || !isScopeEmpty(p.onBehalf.agentScope)
  return meaningful ? JSON.stringify(p) : undefined
}
function nums(a: unknown): number[] {
  return Array.isArray(a) ? a.map((x) => Number(x)).filter((n) => Number.isFinite(n) && n > 0) : []
}
```

`.spec.ts` 用例：`parseStartPolicy(null, '["10","11"]').initiatorScope.roles === [10,11]`；`serializeStartPolicy(emptyStartPolicy()) === undefined`；round-trip `parse(serialize(x))` 保值。

Run: `cd web && npx vitest run src/components/cardflow/startPolicyShared.spec.ts`。Expected: PASS。

- [ ] **Step 2: 起点节点可点开抽屉**（`FlowVerticalGraph.vue`）

emit 定义（:46 附近）加 `'select-start': []`。起点 `a-popover`（:264-280）保留说明，但把内部节点 `div` 加 `@click.stop="$emit('select-start')"`（或加一个"配置发起范围"按钮触发）。父页面 `FlowVerticalGraph` 使用处（`FlowDefinitionEditPage.vue:2528/2548` 附近）加 `@select-start="selectDesignerStart"`。

- [ ] **Step 3: state + drawer 分支 + 保存/回读**（`FlowDefinitionEditPage.vue`）

- `interface BasicInfo`（:90-101）加：`initiatorScope: import('...').ScopeDims`（或直接内联类型）+ `onBehalf: OnBehalfConfig`。`initialState()`（:129-157）用 `emptyScope()/emptyStartPolicy()` 初始化（`state.basic.initiatorScope`, `state.basic.onBehalf`）。
- 回读（:1224 附近 allowedRoles 解析处）改用 `const sp = parseStartPolicy(d.startPolicyJson, d.allowedRolesJson); state.basic.initiatorScope = sp.initiatorScope; state.basic.onBehalf = sp.onBehalf`。
- 保存 payload（:1477 与 :1507 两处 create/update）：新增 `startPolicyJson: serializeStartPolicy({ initiatorScope: state.basic.initiatorScope, onBehalf: state.basic.onBehalf })`，并把 `allowedRolesJson` 同步为角色子集：`allowedRolesJson: state.basic.initiatorScope.roles.length ? JSON.stringify(state.basic.initiatorScope.roles.map(String)) : undefined`（更新处用 `''` 空串清空，仿现有 :1507）。
- `designerSelection.type`（现 `'node'|'edge'|'blank'`）加 `'start'`；`selectDesignerStart()`：`designerSelection.value = { type: 'start' }; designerDrawerOpen.value = true`。`designerDrawerTitle`（:373-377）加 case `start → '发起人节点'`。

- [ ] **Step 4: 发起抽屉内容**（`FlowDefinitionEditPage.vue` 抽屉 :2929-3022 内加分支）

在 `a-drawer` 内加 `v-else-if="designerSelection.type === 'start'"` 段，四维选择器：角色/岗位用 `a-select mode="multiple" :options`（roles 复用 `roleOptions` 映射为数值 value；positions 用新增 `positionOptions`）；组织用 `useOrgSearch` 远端搜索多选；人员照 `approvalAdminUserIds` 范式（`useUserSearch`/`getUserList`）。四维全空显式提示"未限制：本流程组织内任何有菜单权限者可发起"。样式复用 `.sde-fld`/`.cfd-setrow`。

```vue
<section v-else-if="designerSelection.type === 'start'" class="fdef-drawer-section">
  <header class="page-section__title fdef-drawer-section__head">
    <strong>发起人节点</strong>
    <span>发起范围圈定谁可发起；四维留空=不限制</span>
  </header>
  <div class="sde-fld">
    <label class="sde-fld__label">可发起角色</label>
    <a-select v-model:value="state.basic.initiatorScope.roles" mode="multiple"
      placeholder="留空=不限制" :options="roleOptionsNumeric" />
  </div>
  <div class="sde-fld">
    <label class="sde-fld__label">可发起组织</label>
    <a-select v-model:value="state.basic.initiatorScope.orgs" mode="multiple"
      placeholder="留空=不限制" :options="orgScopeOptions" :loading="orgScopeLoading"
      show-search option-filter-prop="label" :filter-option="false" @search="onOrgScopeSearch" />
  </div>
  <div class="sde-fld">
    <label class="sde-fld__label">可发起岗位</label>
    <a-select v-model:value="state.basic.initiatorScope.positions" mode="multiple"
      placeholder="留空=不限制" :options="positionOptions" />
  </div>
  <div class="sde-fld">
    <label class="sde-fld__label">指定发起人</label>
    <a-select v-model:value="state.basic.initiatorScope.users" mode="multiple"
      placeholder="留空=不限制" :options="userScopeOptions" :loading="userScopeLoading"
      show-search option-filter-prop="label" :filter-option="false" @search="onUserScopeSearch" />
  </div>
  <p v-if="startScopeEmpty" class="sde-fld__hint">未限制：本流程组织内任何有菜单权限者可发起。</p>
  <!-- 代提交段在 Task 11 追加 -->
</section>
```

配套 setup：`roleOptionsNumeric`（computed：`roleOptions.value.map(o => ({ value: Number(o.value), label: o.label }))`）；`positionOptions`（onMounted 调 `getPositionList({pageIndex:1,pageSize:200})` → `{value:id,label:name}`）；`orgScopeOptions`/`onOrgScopeSearch`（`useOrgSearch`）；`userScopeOptions`/`onUserScopeSearch`（`useUserSearch` 或仿 `loadApprovalAdminUsers`）；`startScopeEmpty` computed。

- [ ] **Step 5: 删旧"可发起角色"行**

删除 `FlowDefinitionEditPage.vue:1976-1982` 的独立"可发起角色"字段行（角色维已迁入发起抽屉，单一真源）。`state.basic.allowedRoles` 字段可保留供兼容同步（或删除，改由 `initiatorScope.roles` 派生）——执行时若删除须同步清理所有 `allowedRoles` 引用。

- [ ] **Step 6: type-check + lint + vitest + 预览核对**

Run: `cd web && npm run type-check && npm run lint:style && npx vitest run src/components/cardflow/startPolicyShared.spec.ts`。
预览：`preview_start` 打开设计器，点起点节点 → 发起抽屉展开，四维选择器可选、留空提示显示；`preview_inspect` 核对 ≥5 项（抽屉宽度/字段行间距/令牌色，对齐 `ui-baseline.md`）。

- [ ] **Step 7: 提交 commit 2**（把 Task 2-6 合并为一个 commit）

```bash
git add src/STOTOP.Module.CardFlow src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs web/src tests/STOTOP.Module.CardFlow.Tests
git commit -m "feat(cardflow): 结构化发起范围校验(角色/组织/岗位/人员)+发起抽屉 (M8-A · V71)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

# Commit 3 — 件③ 代提交 onBehalf（seeder V72）

`CfCard` 加代理人列；`CreateAsync` 支持 `ActualInitiatorId`（校验 agentScope 越权护栏后置 `FInitiatorId`=被代理人、`FAgentId`=操作人）；放宽 `SubmitAsync` 门禁；修访问门/日志姓名口径；`GetAvailableFlowsAsync` 标记 `onBehalfEnabled`；发起页选"代谁发起"。

## Task 7: CfCard 代理人列 + EF 配置 + seeder V72

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfCard.cs`
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfCardConfiguration.cs`
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`

**Interfaces:**
- Produces: `CfCard.FAgentId : long?`（列 `F代理人ID`）、`CfCard.FAgentName : string?`（列 `F代理人姓名`）。

- [ ] **Step 1: 实体属性**（`CfCard.cs`，在 `FInitiatorName`（:13）下）
```csharp
/// <summary>代提交人ID：null=本人发起；非 null=代提交，FInitiatorId 为被代理人、本列为真实操作人。</summary>
public long? FAgentId { get; set; }
public string? FAgentName { get; set; }
```
- [ ] **Step 2: EF 映射**（`CfCardConfiguration.cs`，在 `FInitiatorName`（:20）下）
```csharp
builder.Property(e => e.FAgentId).HasColumnName("F代理人ID");
builder.Property(e => e.FAgentName).HasColumnName("F代理人姓名").HasMaxLength(100);
```
- [ ] **Step 3: seeder V72**（steps 列表 V71 后加）
```csharp
            new(72, "M8-A 代提交: CF流程实例 加 F代理人ID/F代理人姓名 列(onBehalf 真实操作人留痕) (2026-07-10)", MigrateV72),
```
方法：
```csharp
/// <summary>V72：CF流程实例 加 F代理人ID/F代理人姓名（M8-A 代提交留痕，null=本人发起）。</summary>
private static void MigrateV72(STOTOPDbContext ctx)
{
    if (!SeederHelper.IsSqlServer(ctx)) return;
    ExecSql(ctx, @"IF COL_LENGTH(N'CF流程实例', N'F代理人ID') IS NULL
        ALTER TABLE [CF流程实例] ADD [F代理人ID] BIGINT NULL;");
    ExecSql(ctx, @"IF COL_LENGTH(N'CF流程实例', N'F代理人姓名') IS NULL
        ALTER TABLE [CF流程实例] ADD [F代理人姓名] NVARCHAR(100) NULL;");
}
```
- [ ] **Step 4: 编译**：`/build cardflow` 过。

## Task 8: CreateCardRequest.ActualInitiatorId（前后端字段）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Requests.cs`（CreateCardRequest :227-238）
- Modify: `web/src/types/cardflow.ts`（CreateCardRequest :656-666）

- [ ] **Step 1: 后端字段**
```csharp
/// <summary>代提交：代替谁发起（被代理人 userId）。null=本人发起。</summary>
public long? ActualInitiatorId { get; set; }
```
- [ ] **Step 2: 前端字段**（`CreateCardRequest`）
```ts
actualInitiatorId?: number | null
```
- [ ] **Step 3: 编译 + type-check**：`/build cardflow`；`cd web && npm run type-check`。

## Task 9: CreateAsync 代提交逻辑 + 姓名解析

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs`（`CreateAsync` :756-793；新增 `ResolveUserNameAsync`）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/OnBehalfCreateTests.cs`（Create）

**Interfaces:**
- Consumes: `IInitiatorScopeResolver`（Task 3）、`StartPolicyCodec.Parse`、`request.ActualInitiatorId`。
- Produces: 代提交时 `card.FInitiatorId=被代理人`、`FAgentId=操作人`、双方姓名解析；未授权代理 → `throw InvalidOperationException`。

- [ ] **Step 1: 写失败测试**（`OnBehalfCreateTests.cs`，构造 CardService 同 Task 4 `BuildCardService`）
```csharp
[Fact]
public async global::System.Threading.Tasks.Task 授权代提交_发起人为被代理人代理人留痕()
{
    using var db = TestDbContextFactory.Create(nameof(授权代提交_发起人为被代理人代理人留痕));
    db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID=3600, FFlowName="代提交流程", FFlowCode="onbehalf-flow", FOrgId=1, FStatus="published", FCreatorId=1, FCreatedTime=DateTime.Now,
        FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}""" }); // 用户900可代提交
    db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID=3601, FFlowDefinitionId=3600, FStatus="published", FIsCurrentVersion=true });
    db.Set<SysUser>().Add(new SysUser { FID=900, FName="代理人" });
    db.Set<SysUser>().Add(new SysUser { FID=901, FName="被代理人" });
    await db.SaveChangesAsync();

    var svc = BuildCardService(db);
    var card = await svc.CreateAsync(new CreateCardRequest { FlowDefinitionId=3600, OrgId=1, DataJson="{}", ActualInitiatorId=901 }, userId: 900);

    var saved = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == card.Id);
    Assert.Equal(901, saved.FInitiatorId);
    Assert.Equal("被代理人", saved.FInitiatorName);
    Assert.Equal(900, saved.FAgentId);
    Assert.Equal("代理人", saved.FAgentName);
}

[Fact]
public async global::System.Threading.Tasks.Task 未授权代提交_被拒()
{
    using var db = TestDbContextFactory.Create(nameof(未授权代提交_被拒));
    db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID=3610, FFlowName="代提交流程", FFlowCode="onbehalf-flow2", FOrgId=1, FStatus="published", FCreatorId=1, FCreatedTime=DateTime.Now,
        FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}""" });
    db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID=3611, FFlowDefinitionId=3610, FStatus="published", FIsCurrentVersion=true });
    await db.SaveChangesAsync();
    var svc = BuildCardService(db);
    // 用户 902 不在 agentScope
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        svc.CreateAsync(new CreateCardRequest { FlowDefinitionId=3610, OrgId=1, DataJson="{}", ActualInitiatorId=901 }, userId: 902));
}

[Fact]
public async global::System.Threading.Tasks.Task onBehalf未开启却传ActualInitiator_被拒()
{
    using var db = TestDbContextFactory.Create(nameof(onBehalf未开启却传ActualInitiator_被拒));
    db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID=3620, FFlowName="普通流程", FFlowCode="normal-flow", FOrgId=1, FStatus="published", FCreatorId=1, FCreatedTime=DateTime.Now });
    db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID=3621, FFlowDefinitionId=3620, FStatus="published", FIsCurrentVersion=true });
    await db.SaveChangesAsync();
    var svc = BuildCardService(db);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        svc.CreateAsync(new CreateCardRequest { FlowDefinitionId=3620, OrgId=1, DataJson="{}", ActualInitiatorId=901 }, userId: 900));
}
```

- [ ] **Step 2: 跑测试确认红**：`/test cardflow`（`OnBehalfCreateTests`）→ FAIL。

- [ ] **Step 3: 实现**（`CreateAsync`，在发起范围校验之后、建 card 前）
```csharp
// 代提交解析：ActualInitiatorId 有值时校验代提交范围，落被代理人+代理人留痕
long initiatorId = userId;
long? agentId = null;
if (request.ActualInitiatorId is { } actualId && actualId != userId)
{
    var onBehalf = startPolicy.OnBehalf;
    if (onBehalf is not { Enabled: true })
        throw new InvalidOperationException("该流程未开启代提交");
    var agentMemberships = await _initiatorScopeResolver.GetUserMembershipsAsync(userId);
    if (!_initiatorScopeResolver.IsInScope(agentMemberships, userId, onBehalf.AgentScope))
        throw new InvalidOperationException("您不在该流程的可代提交范围内");
    initiatorId = actualId;
    agentId = userId;
}
```
在 `new CfCard { ... }` 里改：
```csharp
FInitiatorId = initiatorId,
FInitiatorName = await ResolveUserNameAsync(initiatorId),
FAgentId = agentId,
FAgentName = agentId.HasValue ? await ResolveUserNameAsync(agentId.Value) : null,
```
> 注：`startPolicy` 变量来自 Task 4 Step 4 的 `StartPolicyCodec.Parse(...)`——确保它在代提交分支之前已解析（若发起范围分支是条件内解析，提到方法体前部统一解析一次）。

新增私有方法（照 `FlowEngineService.cs:2825` 范式）：
```csharp
private async Task<string> ResolveUserNameAsync(long userId)
{
    var name = await _dbContext.Set<SysUser>().Where(u => u.FID == userId).Select(u => u.FName).FirstOrDefaultAsync();
    return string.IsNullOrWhiteSpace(name) ? userId.ToString() : name;
}
```
文件顶部确认 `using STOTOP.Module.System.Entities;`（`SysUser`）。

> 副作用（正向修复）：非代提交路径 `FInitiatorName` 从此=真实姓名（原恒为空串的潜在 bug 一并修）。若有测试断言 `FInitiatorName==""` 需同步更新。

- [ ] **Step 4: 跑测试确认绿**：`/test cardflow`（`OnBehalfCreateTests`）→ PASS。

## Task 10: SubmitAsync 门禁放宽 + 访问门/日志口径

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（SubmitAsync :429、日志 :518）
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs`（isInitiator 访问门 :164）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/OnBehalfSubmitGateTests.cs`（Create）

**Interfaces:**
- Consumes: `CfCard.FAgentId`（Task 7）。
- Produces: `SubmitAsync` 放行 `operator==FInitiatorId || operator==FAgentId`；访问门加 agent；submit 日志姓名口径修正。

- [ ] **Step 1: 写失败测试**（mirror `FlowActionNoTrackingPersistenceTests`；单节点流程，卡片 draft，`FInitiatorId=被代理人`,`FAgentId=代理人`）
```csharp
[Fact]
public async global::System.Threading.Tasks.Task 代理人可提交被代理人的卡片()
{
    using var db = CreateNoTrackingDb(nameof(代理人可提交被代理人的卡片)); // 同 FlowActionNoTracking 的 helper
    await SeedFlowAsync(db); // 单 human 节点(审批人=51)
    db.Set<CfCard>().Add(new CfCard { FID=9720, FFlowDefinitionId=FlowDefId, FFlowVersionId=FlowVersionId,
        FTitle="代提交", FStatus="draft", FInitiatorId=901, FInitiatorName="被代理人", FAgentId=900, FAgentName="代理人",
        FCurrentRound=0, FOrgId=1, FDataJson="{}" });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();

    var result = await CreateEngine(db).SubmitAsync(9720, 900); // 代理人900 提交
    Assert.True(result.Success, result.Message);
}

[Fact]
public async global::System.Threading.Tasks.Task 无关人员不能提交()
{
    using var db = CreateNoTrackingDb(nameof(无关人员不能提交));
    await SeedFlowAsync(db);
    db.Set<CfCard>().Add(new CfCard { FID=9721, FFlowDefinitionId=FlowDefId, FFlowVersionId=FlowVersionId,
        FTitle="代提交", FStatus="draft", FInitiatorId=901, FInitiatorName="被代理人", FAgentId=null,
        FCurrentRound=0, FOrgId=1, FDataJson="{}" });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();

    var result = await CreateEngine(db).SubmitAsync(9721, 902); // 无关人员902
    Assert.False(result.Success);
}
```
（把 `CreateNoTrackingDb`/`SeedFlowAsync`/`CreateEngine` 从 `FlowActionNoTrackingPersistenceTests` 复制到本类，或提取为共享 helper。）

- [ ] **Step 2: 跑测试确认红**：`代理人可提交被代理人的卡片` FAIL（现门禁只放行发起人）。

- [ ] **Step 3: 放宽门禁**（`FlowEngineService.cs:429-430`）
```csharp
if (card.FInitiatorId != operatorId && card.FAgentId != operatorId)
    return CardOperationResult.Fail("只有发起人或代提交人可以提交");
```
- [ ] **Step 4: 修 submit 日志姓名口径**（:518）
```csharp
var submitName = operatorId == card.FAgentId ? (card.FAgentName ?? "") : card.FInitiatorName;
await LogActionAsync(card.FID, stageInstance.FID, "submit", operatorId, submitName, null);
```
- [ ] **Step 5: 访问门加 agent**（`CardService.cs:164`）
```csharp
var isInitiator = card.FInitiatorId == userId || card.FAgentId == userId;
```
- [ ] **Step 6: 跑测试确认绿 + 回归**：`/test cardflow`（`OnBehalfSubmitGateTests` + `FlowActionNoTrackingPersistenceTests`）→ PASS（多跑 2 次）。

> 说明（重提/作废门）：`ResubmitAsync:1032`、`VoidAsync:1120` 仍只放行 `FInitiatorId==operatorId`。按设计"被代理人+代理人皆可"，同法放宽为 `!= FInitiatorId && != FAgentId`。各加一句改动并补 1 个 mirror 测试（重提由代理人触发放行）。

## Task 11: GetAvailableFlowsAsync 标记 onBehalfEnabled + 发起页代提交

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Responses.cs`（AvailableFlowDto）
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs`（GetAvailableFlowsAsync）
- Modify: `web/src/types/cardflow.ts`（AvailableFlowDto :603-608）
- Modify: `web/src/views/workhub/InitiatePage.vue`（handleAction :158-176）
- Modify: `web/src/components/cardflow/designer/...`/`FlowDefinitionEditPage.vue`（发起抽屉加代提交段）

**Interfaces:**
- Produces: `AvailableFlowDto.onBehalfEnabled: bool`（当前用户对该流程可代提交）；发起页据此显示"代谁发起"选择器传 `actualInitiatorId`。

- [ ] **Step 1: AvailableFlowDto 加字段**（后端 Responses.cs + 前端 types）
后端：`public bool OnBehalfEnabled { get; set; }` 前端：`onBehalfEnabled: boolean`。
- [ ] **Step 2: GetAvailableFlowsAsync 计算 onBehalfEnabled**（在 Task 4 Step 5 的 foreach 内，构造 dto 时）
```csharp
var policy = StartPolicyCodec.Parse(c.FStartPolicyJson, c.FAllowedRolesJson);
// ... 发起范围过滤(已在) ...
var canProxy = policy.OnBehalf is { Enabled: true } ob && !ob.AgentScope.IsEmpty
    && (memberships ??= await _initiatorScopeResolver.GetUserMembershipsAsync(userId)) != null
    && _initiatorScopeResolver.IsInScope(memberships, userId, ob.AgentScope);
result.Add(new AvailableFlowDto { Id=c.FID, FlowName=c.FFlowName, FlowCode=c.FFlowCode, Description=c.FDescription, OnBehalfEnabled = canProxy });
```
- [ ] **Step 3: 发起抽屉加代提交段**（`FlowDefinitionEditPage.vue` `type==='start'` 段末，复用 `.cfd-setrow`）
```vue
<div class="cfd-setrow">
  <a-switch v-model:checked="state.basic.onBehalf.enabled" size="small" />
  <div class="cfd-setrow__text">
    <div class="cfd-setrow__title">允许代提交</div>
    <div class="cfd-setrow__desc">开启后，下列范围内的人可代他人发起。</div>
  </div>
</div>
<div v-if="state.basic.onBehalf.enabled" class="sde-fld">
  <label class="sde-fld__label">可代提交人（角色/组织/岗位/人员任一）</label>
  <!-- 复用与发起范围同款四个 a-select，v-model 绑 state.basic.onBehalf.agentScope.* -->
</div>
```
- [ ] **Step 4: 发起页选"代谁发起"**（`InitiatePage.vue:158-176`）
对 `item.onBehalfEnabled` 的流程，点击时先弹一个含 `UserSelect` 的小 Modal 取被代理人（可选，留空=本人），再 `createCard`：
```ts
const draft = await createCard({ flowDefinitionId: flowId, orgId, dataJson: '{}', actualInitiatorId: chosenUserId ?? undefined })
```
`UserSelect` 取 `.id`（`@/components/cardflow/fields/UserSelect.vue`，emit `{id,name,orgName}`）。`AvailableFlowDto` 增字段后 `cardFlows` 项带 `onBehalfEnabled`。

- [ ] **Step 5: 校验 + 提交 commit 3**
Run: `/build cardflow`；`/test cardflow`（全量，多跑 2 次）；`cd web && npm run type-check && npm run lint:style && npx vitest run`。预览发起页代提交入口 + 发起抽屉代提交段（`preview_inspect` ≥5 项）。
```bash
git add src/STOTOP.Module.CardFlow src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs web/src tests/STOTOP.Module.CardFlow.Tests
git commit -m "feat(cardflow): 代提交 onBehalf(越权护栏+被代理人留痕+门禁放宽) (M8-A · V72)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

# Commit 4 — 批收口：终审 + 回归

## Task 12: FInitiatorId 语义反转回归 + 整体终审

**Files:** 只读审查 + 必要修补。

- [ ] **Step 1: 子代理对抗性只读终审**（dispatch general-purpose）：核 `FInitiatorId` 全部读点在代提交后语义正确（迁移/预览/看板/待办/批次聚合），确认无处把它当"操作人"误用；核发起范围/代提交/重提三件端到端无缝；核前端 `startPolicyJson` round-trip 与旧 `allowedRolesJson` 兼容不丢。
- [ ] **Step 2: 修终审确诊问题**（逐条 TDD 补测→修→绿）。
- [ ] **Step 3: 全量回归**：`/build cardflow`；`/test cardflow`（多跑 3 次判 flaky）；`cd web && npm run type-check && npm run lint:style && npx vitest run`。
- [ ] **Step 4: 更新记忆**：写 memory（M8-A 已落 dev 前的实现要点 + V71/V72 + 遗留），更新 `MEMORY.md` 索引。
- [ ] **Step 5: 若有修补独立 commit**（不 push，等人点头）。

---

## Self-Review（写完对照 spec）

- **spec 覆盖**：件① Task 1 ✓；件② 结构化四维 Task 2-6 ✓（V71/DTO/resolver/CreateAsync/GetAvailableFlows/UI）；件③ Task 7-11 ✓（V72/DTO/CreateAsync/门禁/日志/访问门/onBehalfEnabled/发起页）；假配置修正=件① UI 注释成真 ✓；回归清单 Task 12 ✓。
- **占位扫描**：无 TBD；唯一"执行时 Read 核对"处（SysUser* 字段名、CardService ctor fakes、行号漂移）均为具体动作+已给已知字段，非占位。
- **类型一致**：`FStartPolicyJson`/`F发起策略JSON`、`FAgentId/FAgentName`/`F代理人ID/F代理人姓名`、`StartPolicy/InitiatorScope/OnBehalfPolicy`、`IInitiatorScopeResolver.GetUserMembershipsAsync/IsInScope`、`UserMemberships(RoleIds,OrgIds,PositionIds)`、`ActualInitiatorId`/`actualInitiatorId`、`OnBehalfEnabled`/`onBehalfEnabled`、前端 `parseStartPolicy/serializeStartPolicy/ScopeDims` 全plan一致。
- **顺序依赖**：resolver(Task3) 先于其消费者(Task4/9/11)；V71(Task2) 先于件③ onBehalf 读同列(Task9)；件③ 门禁(Task10) 依赖 V72(Task7) 的 FAgentId。
