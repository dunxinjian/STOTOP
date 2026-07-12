# CardFlow M8-E 处理人策略补全 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 CardFlow `IApproverResolver` 增补三种处理人策略——`superiorChain`（连续多级直属上级）、`prevStage`（上一节点处理人指定）、`initiatorSelect`（发起人自选，全链路真做），前端处理人下拉 5→8，全部真实消费无占位。

**Architecture:** superiorChain/prevStage 是零 schema 的纯 resolver 增量（`ApproverResolver` 已注入 `STOTOPDbContext`，就地查库）；initiatorSelect 需一条新列 `CfCard.FInitiatorAssignmentsJson`（版本化 seeder V79）+ 提交/更新链 DTO 扩展 + 发起端（`CardFlowPanel` fill 模式）选人器。三策略都是 camelCase，**必须在保存归一化与解析归一化两侧各补显式 case**，否则保存后被强制小写、resolver 匹配失败。

**Tech Stack:** 后端 .NET 10 / EF Core / xUnit（InMemory + `TestDbContextFactory`）；前端 Vue 3 / TS / Ant Design Vue / vitest。构建 `build-filter cardflow`，测试 `test-dotnet CardFlow`，前端 `type-check` + `vitest` + `lint:style`。

## Global Constraints

- 模块锁：`STOTOP.Module.CardFlow`；后端 `scripts/dev/build-filter.ps1 cardflow`（或 `/build cardflow`），测试 `scripts/dev/test-dotnet.ps1 CardFlow`（或 `/test CardFlow`）。
- 不新增 `cc` FType（引擎节点分派 auto/human 二元）。
- 无 EF migrations；schema 变更走版本化 seeder（V 编号），原生 DDL 用 `SeederHelper.ExecuteRawSql`（经 `ExecSql` 包装，幂等 `IF COL_LENGTH(...) IS NULL`）。改 seeder 前**重查 `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs` 实际末版本 + SYS 迁移历史再定 V 号**（当前末版本 V78，下一个 V79；勿硬编——并发流可能已占）。
- DB 列名 `F+中文`，C# 属性 `F+PascalCase`，`HasColumnName` 映射。策略标识 `FAssigneeStrategy` 列 `HasMaxLength(30)`——三新标识最长 15，安全。
- 每件独立 commit，经 husky/hook 编译门禁（改 `.cs` 触发 `scripts/dev/hook-precommit-gate.ps1`）；**不 push 等人点头**。
- **并发协调**：主树 `master`，另有 `.claude/worktrees/fix+cardflow-empty-draft` worktree 隔离运行、org-manager-fixes 尚未启动。每个 commit 前 `git status --short` 确认无并发 M 冲突，`git add` 只加显式路径、**绝不 `-A`/`-am`**。若并发流先落地改了 `ApproverResolver.cs` switch / `NormalizeStrategy` / seeder 版本，手动并入我的增量。
- **不做假配置**：三策略均真实消费引擎。superiorChain 依赖 `SysUserOrganization.FDirectSuperiorId`（baseline 零种子，未维护则解析空走 fallback——真功能+待填数据，非假配置）。
- 缩进 `.cs`=4 / `.ts`/`.vue`=2；utf-8/lf/文末换行。

## 已发现的既有缺陷（用户已批准在 Task 1 顺带修）

`FlowDefinitionService.NormalizeAssigneeStrategy`(`:715-724`) 对未列举策略走 `_ => strategy!.ToLowerInvariant()` 强制小写，导致现有 **`orgChain` 保存后变 `"orgchain"`、resolver 大小写敏感 switch 只认 `"orgChain"` → 端到端失效**（没被发现因 FManagerId 零种子、从未真跑）。**用户已批准在 Task 1 顺带修**：同一 switch 加一行 `"orgchain" => "orgChain",` + 一个 orgChain 保存 round-trip 回归测试。此修在 `FlowDefinitionService`（CardFlow 域），不碰 org-manager-fixes 领地的 `ApproverResolver.ResolveOrgChainAsync` 解析逻辑。

## 关键真源锚点（实现前已核实）

- `ApproverResolver.ResolveAsync` switch：`src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs:31-41`；`NormalizeStrategy`：`:418-434`（大小写敏感）。结果类型 `ApproverResolveResult`（`.Approvers`/`.Success`/`.ErrorMessage`/`.FallbackReason`，`Models/Approval/ApproverStrategyModels.cs`）。helper：`ResolveUserIdsAsync`（已 `FStatus==1` 过滤+去重+SortOrder）、`NormalizeUserIds`、`ParseObject`/`TryGetProperty`/`ReadString`/`TryReadLong`/`TryGetProperty`。
- 保存归一化：`FlowDefinitionService.NormalizeAssigneeStrategy`（`:715-724`，键小写、值须 camelCase）。
- 实体字段：`SysUserOrganization.FDirectSuperiorId`(long?)/`F是否当前`(bool)/`FStatus`(int)/`FUserId`；`CfStageInstance.FCardId/FStageDefinitionId(long?)/FType/FRound/FStatus("completed"/"cancelled")/FCompletedTime`；`CfStageAssignee.FStageInstanceId/FUserId/FStatus("approved"/"rejected")`；`CfStageDefinition.FID/FFlowVersionId/FStageKey/FType/FAssigneeStrategy/FAssigneeConfigJson`；`CfCard.FID/FDataJson/FInitiatorId/FCurrentRound/FOrgId`。
- 前端：`stageDefinitionShared.ts`（`ASSIGNEE_STRATEGY_LABELS:65`、`normalizeAssigneeStrategy:38`、`formatAssigneeSummary:77`）；`StageConfigPanel.vue`（`ASSIGNEE_STRATEGIES:59`、edit refs `:245-250`、`isFallbackConfigStrategy:535`、`buildAssigneeConfig:546`、`rehydrateSelection:593`、附属 UI `:953-998`、下拉渲染 `:945`）；`CardFlowPanel.vue`（`loadCardDetail:598`、`buildSavePayload:1109`、`doSubmit:1145`、fill 模板 `:1575`）；`types/cardflow.ts`（`FlowVersionDetailDto.stages:80`、`StageDefinitionDto.assigneeStrategy:105`、`UpdateCardRequest:700`）；`api/cardflow.ts`（`updateCard:262`、`submitCard:314`、`getFlowVersionDetail:149`）；`UserSelect.vue`（单选，`useUserSearch`）。
- 测试锚点：`tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs`（`TestDbContextFactory.Create(nameof(...))` + `new ApproverResolver(db)` + `db.Set<T>().Add`，英文 `[Fact]` 名）；`tests/.../Rules/FlowDefinitionStableKeyTests.cs`（`new FlowDefinitionService(db, NullLogger<...>.Instance)` + `SaveDraftVersionAsync(100, new SaveDraftVersionRequest{Stages={...}}, 1)` + `GetVersionDetailAsync(100, detail.Id)`，`SeedDraft` helper）。

## File Structure

**Task 1 (superiorChain) — 零 schema：**
- Modify: `src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs`（switch +1 case、`NormalizeStrategy` +1 case、新私有 `ResolveSuperiorChainAsync`）
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs`（`NormalizeAssigneeStrategy` +1 case）
- Modify: `web/src/components/cardflow/stageDefinitionShared.ts`（labels + normalize + summary）
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（下拉项 + edit ref + build/rehydrate + 附属 UI + fallback 归类）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs`（+superiorChain 用例）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs`（**新建**，+superiorChain round-trip）
- Test: `web/src/components/cardflow/stageDefinitionShared.spec.ts`（+labels/normalize/summary 断言）

**Task 2 (prevStage) — 零 schema：** 同上各文件追加 prevStage 分支/用例。

**Task 3 (initiatorSelect) — 含 seeder 新列：**
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`（注册 V79 + `MigrateV79` 建列）
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfCard.cs`（+`FInitiatorAssignmentsJson`）
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfCardConfiguration.cs`（+列映射）
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Requests.cs`（`UpdateCardRequest` +`InitiatorAssignmentsJson`）
- Modify: `src/STOTOP.Module.CardFlow/Services/CardService.cs`（`UpdateAsync` 赋值 + `GetByIdAsync`/`CardDetailDto` 透出用于回显）
- Modify: `src/STOTOP.Module.CardFlow/Dtos/*`（`CardDetailDto` +`InitiatorAssignmentsJson`——回显草稿已选）
- Modify: `ApproverResolver.cs`（switch +1 case、`NormalizeStrategy` +1 case、新私有 `ResolveInitiatorSelectAsync`）
- Modify: `FlowDefinitionService.cs`（`NormalizeAssigneeStrategy` +1 case）
- Modify: `stageDefinitionShared.ts` / `StageConfigPanel.vue`（下拉项 + labels/normalize/summary；无策略附属，含 fallback 归类）
- Modify: `web/src/components/cardflow/CardFlowPanel.vue`（fill 选人器 + payload + 回显）
- Modify: `web/src/types/cardflow.ts`（`UpdateCardRequest` +字段、`CardDetailDto` +字段）
- Test: `ApproverResolverTests.cs`（+initiatorSelect 用例）、`AssigneeStrategyNormalizationTests.cs`（+round-trip）、`tests/.../Approval/CfCardInitiatorAssignmentsPersistenceTests.cs`（**新建**，实体 round-trip）、`stageDefinitionShared.spec.ts`（+断言）

---

## Task 1: superiorChain（连续多级直属上级）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs:31-41`（switch）、`:418-434`（NormalizeStrategy）、新增私有方法
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:715-724`
- Modify: `web/src/components/cardflow/stageDefinitionShared.ts`、`web/src/components/cardflow/StageConfigPanel.vue`
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs`、`tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs`（新建）、`web/src/components/cardflow/stageDefinitionShared.spec.ts`

**Interfaces:**
- Produces（后续任务/前端依赖）：策略标识字符串 `"superiorChain"`；config JSON `{ "maxLevels": <int 1-20, 默认5> }`；解析结果 `ResolvedApprover.Source == "superiorChain"`，approvers 为发起人向上第 1..N 级**在职**直属上级、按级序（L1 先）。

- [ ] **Step 1：写后端失败测试（resolver 逐级取上级 + 停用跳过穿透 + 防环 + maxLevels）**

追加到 `tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs`（放在类内末尾）：

```csharp
    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_WalksDirectSuperiorsInLevelOrder()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_WalksDirectSuperiorsInLevelOrder));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 2, FName = "一级主管", FStatus = 1 },
            new SysUser { FID = 3, FName = "二级主管", FStatus = 1 },
            new SysUser { FID = 4, FName = "三级主管", FStatus = 1 });
        db.Set<SysUserOrganization>().AddRange(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = 2, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 2, FOrgId = 100, FDirectSuperiorId = 3, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 3, FOrgId = 100, FDirectSuperiorId = 4, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 4, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":2}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 1);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 2, 3 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("superiorChain", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_SkipsInactiveSuperiorButPenetratesUpward()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_SkipsInactiveSuperiorButPenetratesUpward));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 2, FName = "已离职主管", FStatus = 0 },
            new SysUser { FID = 3, FName = "上级主管", FStatus = 1 });
        db.Set<SysUserOrganization>().AddRange(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = 2, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 2, FOrgId = 100, FDirectSuperiorId = 3, FStatus = 1, F是否当前 = true },
            new SysUserOrganization { FUserId = 3, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":5}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 1);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 3 }, result.Approvers.Select(a => a.UserId));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SuperiorChain_EmptyChainFallsBackToFlowAdmin()
    {
        using var db = TestDbContextFactory.Create(nameof(SuperiorChain_EmptyChainFallsBackToFlowAdmin));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 1, FName = "发起人", FStatus = 1 },
            new SysUser { FID = 9, FName = "流程管理员", FStatus = 1 });
        db.Set<SysUserOrganization>().Add(
            new SysUserOrganization { FUserId = 1, FOrgId = 100, FDirectSuperiorId = null, FStatus = 1, F是否当前 = true });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var stage = new CfStageDefinition { FAssigneeStrategy = "superiorChain", FAssigneeConfigJson = """{"maxLevels":3,"fallback":{"type":"flowAdmin"}}""" };

        var result = await resolver.ResolveAsync(stage, new CfCard(), new Dictionary<string, object?>(),
            flowOrgId: 100, initiatorId: 1, flowSettingsJson: """{"approvalAdminUserIds":[9]}""");

        Assert.True(result.Success);
        Assert.Equal(9, result.Approvers[0].UserId);
        Assert.Contains("flowAdmin", result.FallbackReason);
    }
```

- [ ] **Step 2：跑测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow` （或 `/test CardFlow`；可加过滤 `SuperiorChain`）
Expected: 三个 superiorChain 用例 FAIL——`ResolveAsync` switch 无 `"superiorChain"` case，落 `_ => "不支持的处理人策略"`，`result.Success==false`。

- [ ] **Step 3：resolver 加 case + 私有解析方法**

在 `ApproverResolver.cs:39` 的 `"initiator" => ...` 之后、`_ => ...` 之前插入一行：

```csharp
            "superiorChain" => await ResolveSuperiorChainAsync(config, initiatorId, cancellationToken),
```

在 `NormalizeStrategy`（`:420-433`）的 `"initiator" => "initiator",` 之后插入：

```csharp
            "superiorChain" => "superiorChain",
```

在类内（如 `ResolveOrgChainAsync` 之后）新增私有方法：

```csharp
    /// <summary>
    /// 连续多级直属上级：从发起人起沿 SysUserOrganization.FDirectSuperiorId 逐级向上取 N 级在职直属上级。
    /// 纯个人上级链（真源=SysUserOrganization.FDirectSuperiorId，决策 B），刻意不带 orgChain 的组织负责人兜底。
    /// 停用上级跳过但穿透（继续取其上级），visited 防环。空链交 ApplyFallbackAsync。
    /// 注：与超时升级的 FlowEngineService.ResolveSuperiorUserAsync 语义不同（后者带组织链兜底），未来可评估 DRY 合并。
    /// </summary>
    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveSuperiorChainAsync(
        JsonElement? config,
        long initiatorId,
        CancellationToken cancellationToken)
    {
        var maxLevels = TryGetProperty(config, "maxLevels", out var maxLevelsValue) && TryReadLong(maxLevelsValue, out var parsedMaxLevels)
            ? Math.Clamp((int)parsedMaxLevels, 1, 20)
            : 5;

        var chain = new List<long>();
        var visited = new HashSet<long> { initiatorId };
        var current = initiatorId;
        // 安全上限：即便全是停用上级穿透也不无限循环（maxLevels 只计在职上级）。
        for (var hops = 0; chain.Count < maxLevels && hops < 50; hops++)
        {
            var superiorId = await _dbContext.Set<SysUserOrganization>()
                .Where(uo => uo.FStatus == 1 && uo.F是否当前 && uo.FUserId == current)
                .Select(uo => uo.FDirectSuperiorId)
                .FirstOrDefaultAsync(cancellationToken);
            if (superiorId is null or <= 0 || !visited.Add(superiorId.Value))
            {
                break;
            }

            current = superiorId.Value; // 先前移以支持“停用跳过但穿透”
            var isActive = await _dbContext.Set<SysUser>()
                .AnyAsync(u => u.FStatus == 1 && u.FID == superiorId.Value, cancellationToken);
            if (isActive)
            {
                chain.Add(superiorId.Value);
            }
        }

        return await ResolveUserIdsAsync(chain, "superiorChain", cancellationToken);
    }
```

- [ ] **Step 4：跑测试确认后端通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: 三个 superiorChain 用例 PASS（其余 resolver 用例仍绿）。

- [ ] **Step 5：写保存归一化 round-trip 失败测试（新建文件）**

新建 `tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs`：

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class AssigneeStrategyNormalizationTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_PreservesSuperiorChainStrategyCasing()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_PreservesSuperiorChainStrategyCasing));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human",
                    AssigneeStrategy = "superiorChain", AssigneeConfigJson = """{"maxLevels":3}"""
                }
            }
        }, operatorId: 1);

        Assert.Equal("superiorChain", detail.Stages[0].AssigneeStrategy);
        var reloaded = await service.GetVersionDetailAsync(100, detail.Id);
        Assert.Equal("superiorChain", reloaded!.Stages[0].AssigneeStrategy);
    }

    // 顺带修：既有 orgChain 保存后被强制小写为 "orgchain" → resolver 只认 "orgChain" 端到端失效。回归钉死。
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_PreservesOrgChainStrategyCasing()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_PreservesOrgChainStrategyCasing));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human",
                    AssigneeStrategy = "orgChain", AssigneeConfigJson = """{"maxLevels":20}"""
                }
            }
        }, operatorId: 1);

        Assert.Equal("orgChain", detail.Stages[0].AssigneeStrategy);
        var reloaded = await service.GetVersionDetailAsync(100, detail.Id);
        Assert.Equal("orgChain", reloaded!.Stages[0].AssigneeStrategy);
    }

    private static void SeedDraft(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "费用报销", FFlowCode = "FYBS", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 200, FFlowDefinitionId = 100, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now
        });
    }
}
```

> 若 `StageDefinitionRequest` / `StageDefinitionDto` 的字段名与 `AssigneeStrategy`/`AssigneeConfigJson` 不符，先读 `src/STOTOP.Module.CardFlow/Dtos/Requests.cs` 与响应 DTO 核对（应存在，`FlowDefinitionService:675-676` 赋值 `FAssigneeStrategy = NormalizeAssigneeStrategy(...)`）。

- [ ] **Step 6：跑测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`（过滤 `AssigneeStrategyNormalization`）
Expected: FAIL——`NormalizeAssigneeStrategy` 把 `"superiorChain"` 走 `_ => ToLowerInvariant()` → 存 `"superiorchain"`，断言 `"superiorChain"` 不等。

- [ ] **Step 7：保存归一化补显式 case**

`FlowDefinitionService.cs:715-724`，在 `"fixed" => "fixed",` 之后插入两行（含顺带修 orgChain）：

```csharp
        "orgchain" => "orgChain",
        "superiorchain" => "superiorChain",
```

（switch 键匹配的是 `.ToLowerInvariant()` 结果，故键用全小写、值保 camelCase。`"orgchain" => "orgChain"` 修复既有 orgChain 端到端失效。）

- [ ] **Step 8：跑测试确认通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: `AssigneeStrategyNormalizationTests` PASS。

- [ ] **Step 9：前端写 vitest 失败断言**

追加到 `web/src/components/cardflow/stageDefinitionShared.spec.ts`（相应 describe 内）：

```ts
  it('ASSIGNEE_STRATEGY_LABELS 覆盖 orgChain 与 superiorChain', () => {
    expect(ASSIGNEE_STRATEGY_LABELS.orgChain).toBeTruthy()
    expect(ASSIGNEE_STRATEGY_LABELS.superiorChain).toBe('连续多级主管')
  })

  it('normalizeAssigneeStrategy 认 superiorChain 变体', () => {
    expect(normalizeAssigneeStrategy('superiorchain')).toBe('superiorChain')
    expect(normalizeAssigneeStrategy('superiorChain')).toBe('superiorChain')
  })
```

> 顶部若未 import `ASSIGNEE_STRATEGY_LABELS` / `normalizeAssigneeStrategy`，补进现有 import。

- [ ] **Step 10：跑 vitest 确认失败**

Run: `cd web && npx vitest run src/components/cardflow/stageDefinitionShared.spec.ts`
Expected: FAIL——labels 无 `orgChain`/`superiorChain`，normalize 无 `superiorchain` case。

- [ ] **Step 11：前端 stageDefinitionShared.ts 补 labels + normalize + summary**

`normalizeAssigneeStrategy`（`:38-52`）在 `case 'orgchain': return 'orgChain'` 之后插入：

```ts
    case 'superiorchain': return 'superiorChain'
```

`ASSIGNEE_STRATEGY_LABELS`（`:65-70`）补齐 orgChain + superiorChain：

```ts
export const ASSIGNEE_STRATEGY_LABELS: Record<string, string> = {
  role: '按角色',
  fixed: '指定人员',
  fieldUsers: '按字段取人',
  orgChain: '组织链主管',
  superiorChain: '连续多级主管',
  initiator: '发起人',
}
```

`formatAssigneeSummary`（`:88` 附近，`fieldUsers` 分支之后）插入：

```ts
  if (strategy === 'superiorChain') return `${label}·${config?.maxLevels || 5}级`
```

- [ ] **Step 12：跑 vitest 确认通过**

Run: `cd web && npx vitest run src/components/cardflow/stageDefinitionShared.spec.ts`
Expected: PASS。

- [ ] **Step 13：StageConfigPanel 加下拉项 + edit ref + build/rehydrate + 附属 UI + fallback 归类**

`StageConfigPanel.vue`：

① `ASSIGNEE_STRATEGIES`（`:59-65`）在 `orgChain` 之后插入：
```ts
  { value: 'superiorChain', label: '连续多级主管', hint: '从发起人沿直属上级链逐级向上取 N 级主管' },
```

② edit refs（`:250` `editOrgChainMaxLevels` 之后）：
```ts
const editSuperiorMaxLevels = ref<number>(5)
```

③ `isFallbackConfigStrategy`（`:535-537`）加 superiorChain：
```ts
function isFallbackConfigStrategy(strategy?: string) {
  return strategy === 'role' || strategy === 'fixed' || strategy === 'fieldUsers' || strategy === 'orgChain' || strategy === 'superiorChain'
}
```

④ `buildAssigneeConfig`（`:576` orgChain 分支之后）：
```ts
  if (stage.assigneeStrategy === 'superiorChain') {
    return { maxLevels: editSuperiorMaxLevels.value || 5, fallback }
  }
```

⑤ `rehydrateSelection` 重置（`:603` `editOrgChainMaxLevels.value = 20` 之后）加 `editSuperiorMaxLevels.value = 5`；解析块（`:618` 之后）加 `editSuperiorMaxLevels.value = config?.maxLevels || 5`。

⑥ 附属 UI（`:998` orgChain 的 `</div>` 之后）：
```html
            <div v-if="selectedStage.assigneeStrategy === 'superiorChain'" class="sde-fld">
              <label class="sde-fld__label">逐级主管层数</label>
              <a-input-number v-model:value="editSuperiorMaxLevels" :min="1" :max="20" style="width: 120px" />
              <p class="sde-fld__hint">从发起人沿直属上级逐级向上取指定层数（在职上级）</p>
            </div>
```

- [ ] **Step 14：前端门禁**

Run: `cd web && npm run type-check && npm run lint:style`
Expected: 均通过（无 TS 报错、无裸 hex）。

- [ ] **Step 15：后端编译 + 全量 CardFlow 测试**

Run: `scripts/dev/build-filter.ps1 cardflow` 然后 `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: 编译通过；CardFlow 测试全绿（flaky 则多跑一次确认）。

- [ ] **Step 16：Commit（先查并发冲突）**

```bash
git status --short
git add src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs \
        src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs \
        web/src/components/cardflow/stageDefinitionShared.ts \
        web/src/components/cardflow/StageConfigPanel.vue \
        tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs \
        tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs \
        web/src/components/cardflow/stageDefinitionShared.spec.ts
git commit -m "feat(cardflow): 处理人策略补 superiorChain 连续多级直属上级 (M8-E 件①)

从发起人沿 SysUserOrganization.FDirectSuperiorId 逐级取 N 级在职直属上级(停用跳过穿透+
防环+maxLevels)。纯个人上级链(决策B)不带 orgChain 组织负责人兜底。归一化两侧(保存
NormalizeAssigneeStrategy+解析 NormalizeStrategy)补 camelCase 显式case防被强制小写。
顺带修既有 orgChain 保存被强制小写致端到端失效(加 orgchain=>orgChain 一行+回归)。
前端下拉5→6+label/summary/maxLevels UI。零schema。

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

（hook 编译门禁自动拦截；若被拒则修编译错误后重试。）

---

## Task 2: prevStage（上一节点处理人指定）

**Files:** 同 Task 1 各文件追加 prevStage 分支/用例。
- Modify: `ApproverResolver.cs`（switch +case、NormalizeStrategy +case、新私有 `ResolvePrevStageAsync`）、`FlowDefinitionService.cs`（NormalizeAssigneeStrategy +case）、`stageDefinitionShared.ts`、`StageConfigPanel.vue`
- Test: `ApproverResolverTests.cs`、`AssigneeStrategyNormalizationTests.cs`、`stageDefinitionShared.spec.ts`

**Interfaces:**
- Consumes：`ApproverResolveResult`/`ResolvedApprover`；`CfStageInstance`/`CfStageAssignee`/`CfStageDefinition` 字段（见锚点）。
- Produces：策略标识 `"prevStage"`；config `{ "sourceStageKey"?: string }`（缺省=最近完成人工节点）；结果 approvers = 来源节点 `approved` 处理人，`Source == "prevStage"`。

- [ ] **Step 1：写 resolver 失败测试（显式来源 / 缺省最近完成 / 排除 rejected·cancelled·auto）**

追加到 `ApproverResolverTests.cs`。测试需 seed `CfCard`+`CfStageDefinition`+`CfStageInstance`+`CfStageAssignee`：

```csharp
    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_ExplicitSourceStageKey_TakesApprovedAssignees()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_ExplicitSourceStageKey_TakesApprovedAssignees));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 11, FName = "初审人", FStatus = 1 },
            new SysUser { FID = 12, FName = "驳回人", FStatus = 1 });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = 500, FFlowVersionId = 900, FStageKey = "first_review", FStageName = "初审", FType = "human" });
        db.Set<CfStageInstance>().Add(new CfStageInstance { FID = 600, FCardId = 700, FStageDefinitionId = 500, FStageName = "初审", FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-10) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 600, FUserId = 11, FUserName = "初审人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 600, FUserId = 12, FUserName = "驳回人", FStatus = "rejected" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 700, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 501, FFlowVersionId = 900, FStageKey = "second", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = """{"sourceStageKey":"first_review"}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 11 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("prevStage", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PrevStage_Default_TakesMostRecentCompletedHumanStage()
    {
        using var db = TestDbContextFactory.Create(nameof(PrevStage_Default_TakesMostRecentCompletedHumanStage));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 21, FName = "早节点人", FStatus = 1 },
            new SysUser { FID = 22, FName = "近节点人", FStatus = 1 });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 510, FFlowVersionId = 900, FStageKey = "early", FType = "human" },
            new CfStageDefinition { FID = 511, FFlowVersionId = 900, FStageKey = "recent", FType = "human" },
            new CfStageDefinition { FID = 512, FFlowVersionId = 900, FStageKey = "auto_node", FType = "auto" });
        db.Set<CfStageInstance>().AddRange(
            new CfStageInstance { FID = 610, FCardId = 700, FStageDefinitionId = 510, FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-30) },
            new CfStageInstance { FID = 611, FCardId = 700, FStageDefinitionId = 511, FType = "human", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-5) },
            new CfStageInstance { FID = 612, FCardId = 700, FStageDefinitionId = 512, FType = "auto", FRound = 1, FStatus = "completed", FCompletedTime = DateTime.Now.AddMinutes(-1) });
        db.Set<CfStageAssignee>().AddRange(
            new CfStageAssignee { FStageInstanceId = 610, FUserId = 21, FUserName = "早节点人", FStatus = "approved" },
            new CfStageAssignee { FStageInstanceId = 611, FUserId = 22, FUserName = "近节点人", FStatus = "approved" });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 700, FFlowVersionId = 900, FOrgId = 100 };
        var stage = new CfStageDefinition { FID = 513, FFlowVersionId = 900, FStageKey = "current", FAssigneeStrategy = "prevStage", FAssigneeConfigJson = null };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 22 }, result.Approvers.Select(a => a.UserId)); // 排除 auto_node(612) 与更早的 early(610)
    }
```

- [ ] **Step 2：跑测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`（过滤 `PrevStage`）
Expected: FAIL（无 `prevStage` case）。

- [ ] **Step 3：resolver 加 case + 私有方法**

`ApproverResolver.cs` switch（在 superiorChain case 之后）插入：
```csharp
            "prevStage" => await ResolvePrevStageAsync(config, stageDefinition, card, cancellationToken),
```

`NormalizeStrategy` 加：
```csharp
            "prevStage" => "prevStage",
```

新增私有方法：
```csharp
    /// <summary>
    /// 上一节点处理人指定：取来源节点 approved 处理人。config.sourceStageKey 显式指定来源节点(同版本 FStageKey)；
    /// 缺省=按 FCompletedTime 最近完成的人工节点(排除当前节点、排除 auto)。排除 rejected/cancelled、多轮取最新完成。
    /// </summary>
    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolvePrevStageAsync(
        JsonElement? config,
        CfStageDefinition stageDefinition,
        CfCard card,
        CancellationToken cancellationToken)
    {
        var sourceStageKey = TryGetProperty(config, "sourceStageKey", out var sourceKeyValue)
            ? ReadString(sourceKeyValue)
            : null;

        long? sourceInstanceId;
        if (!string.IsNullOrWhiteSpace(sourceStageKey))
        {
            var sourceDefId = await _dbContext.Set<CfStageDefinition>()
                .Where(s => s.FFlowVersionId == stageDefinition.FFlowVersionId && s.FStageKey == sourceStageKey)
                .Select(s => (long?)s.FID)
                .FirstOrDefaultAsync(cancellationToken);
            if (sourceDefId is null)
            {
                return new ApproverResolveResult { ErrorMessage = $"上一节点处理人策略：来源节点 {sourceStageKey} 不存在" };
            }

            sourceInstanceId = await _dbContext.Set<CfStageInstance>()
                .Where(si => si.FCardId == card.FID && si.FStageDefinitionId == sourceDefId && si.FStatus != "cancelled")
                .OrderByDescending(si => si.FRound)
                .Select(si => (long?)si.FID)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            sourceInstanceId = await (
                from si in _dbContext.Set<CfStageInstance>()
                join sd in _dbContext.Set<CfStageDefinition>() on si.FStageDefinitionId equals sd.FID
                where si.FCardId == card.FID
                    && si.FStageDefinitionId != stageDefinition.FID
                    && si.FStatus == "completed"
                    && sd.FType != "auto"
                orderby si.FCompletedTime descending
                select (long?)si.FID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (sourceInstanceId is null)
        {
            return new ApproverResolveResult();
        }

        var userIds = await _dbContext.Set<CfStageAssignee>()
            .Where(a => a.FStageInstanceId == sourceInstanceId && a.FStatus == "approved")
            .OrderBy(a => a.FSortOrder)
            .Select(a => a.FUserId)
            .ToListAsync(cancellationToken);

        return await ResolveUserIdsAsync(userIds, "prevStage", cancellationToken);
    }
```

- [ ] **Step 4：跑测试确认通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: prevStage 用例 PASS。

- [ ] **Step 5：保存归一化 round-trip 测试 + 归一化 case**

追加到 `AssigneeStrategyNormalizationTests.cs`（镜像 Task 1 Step 5 结构，策略换 `"prevStage"`、config `{"sourceStageKey":"x"}`）；跑确认失败；`FlowDefinitionService.NormalizeAssigneeStrategy` 加 `"prevstage" => "prevStage",`；跑确认通过。

```csharp
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_PreservesPrevStageStrategyCasing()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_PreservesPrevStageStrategyCasing));
        SeedDraft(db);
        await db.SaveChangesAsync();
        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages = { new StageDefinitionRequest { StageKey = "second", Name = "复核", SortOrder = 1, Type = "human", AssigneeStrategy = "prevStage" } }
        }, operatorId: 1);
        Assert.Equal("prevStage", detail.Stages[0].AssigneeStrategy);
        var reloaded = await service.GetVersionDetailAsync(100, detail.Id);
        Assert.Equal("prevStage", reloaded!.Stages[0].AssigneeStrategy);
    }
```

- [ ] **Step 6：前端 vitest 断言 + 实现**

`stageDefinitionShared.spec.ts` 加 prevStage 的 label/normalize 断言（跑失败）；`stageDefinitionShared.ts`：`normalizeAssigneeStrategy` 加 `case 'prevstage': return 'prevStage'`，`ASSIGNEE_STRATEGY_LABELS` 加 `prevStage: '上一节点处理人'`，`formatAssigneeSummary` 加：
```ts
  if (strategy === 'prevStage') return config?.sourceStageKey ? `${label}·${config.sourceStageKey}` : `${label}·最近完成`
```
跑 vitest 确认通过。

- [ ] **Step 7：StageConfigPanel 加下拉项 + edit ref + build/rehydrate + 附属 UI**

`ASSIGNEE_STRATEGIES` 加：
```ts
  { value: 'prevStage', label: '上一节点处理人', hint: '由上一节点(或指定来源节点)的处理人继续处理' },
```
`isFallbackConfigStrategy` 加 `|| strategy === 'prevStage'`；edit ref `const editPrevSourceStageKey = ref<string>('')`；`buildAssigneeConfig` 加分支 `if (stage.assigneeStrategy === 'prevStage') { const c: Record<string, unknown> = { fallback }; if (editPrevSourceStageKey.value) c.sourceStageKey = editPrevSourceStageKey.value; return c }`；`rehydrateSelection` 重置 `editPrevSourceStageKey.value = ''` + 解析 `editPrevSourceStageKey.value = config?.sourceStageKey || ''`；附属 UI（来源节点下拉，选项=本流程其它人工节点，`value=stage.id`）：
```html
            <div v-if="selectedStage.assigneeStrategy === 'prevStage'" class="sde-fld">
              <label class="sde-fld__label">来源节点（留空=最近完成人工节点）</label>
              <a-select
                v-model:value="editPrevSourceStageKey"
                style="width: 100%"
                placeholder="默认取最近完成的人工节点"
                allow-clear
                :options="stages.filter(s => s.type === 'manual' && s.id !== selectedStage!.id).map(s => ({ value: s.id, label: s.name || s.id }))"
              />
            </div>
```
> `stages` 与 `selectedStage` 为面板既有；`StageDefinition` 的人工类型判定沿用面板既有口径（`type === 'manual'`，见 getStageHealth）。若面板节点类型字段用 `'human'`，改用 `s.type !== 'auto'` 过滤更稳。实现前确认 `StageDefinition.type` 取值。

- [ ] **Step 8：门禁 + 全量测试**

Run: `cd web && npm run type-check && npm run lint:style`；`scripts/dev/build-filter.ps1 cardflow`；`scripts/dev/test-dotnet.ps1 CardFlow`
Expected: 全绿。

- [ ] **Step 9：Commit（先 `git status --short` 查冲突）**

```bash
git add src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs \
        src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs \
        web/src/components/cardflow/stageDefinitionShared.ts \
        web/src/components/cardflow/StageConfigPanel.vue \
        tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs \
        tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs \
        web/src/components/cardflow/stageDefinitionShared.spec.ts
git commit -m "feat(cardflow): 处理人策略补 prevStage 上一节点处理人指定 (M8-E 件②)

取来源节点 approved 处理人:config.sourceStageKey 显式指定(同版本FStageKey),缺省=按
FCompletedTime 最近完成人工节点(排除当前节点/auto/rejected/cancelled,多轮取最新)。镜像
FlowEngineService:3157 跨节点 join 范式。归一化两侧补 prevStage 显式case。前端下拉+来源
节点选择器+label/summary。零schema。

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: initiatorSelect（发起人自选 · 全链路真做）

**Files:**
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`（V79 注册 + `MigrateV79`）
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfCard.cs`、`Configurations/CfCardConfiguration.cs`
- Modify: `src/STOTOP.Module.CardFlow/Dtos/Requests.cs`（`UpdateCardRequest`）、`CardService.cs`（`UpdateAsync` + `GetByIdAsync`）、`CardDetailDto`（透出）
- Modify: `ApproverResolver.cs`、`FlowDefinitionService.cs`
- Modify: `stageDefinitionShared.ts`、`StageConfigPanel.vue`、`web/src/components/cardflow/CardFlowPanel.vue`、`web/src/types/cardflow.ts`
- Test: `ApproverResolverTests.cs`、`AssigneeStrategyNormalizationTests.cs`、`tests/.../Approval/CfCardInitiatorAssignmentsPersistenceTests.cs`（新建）、`stageDefinitionShared.spec.ts`

**Interfaces:**
- Produces：策略标识 `"initiatorSelect"`；列 `CfCard.FInitiatorAssignmentsJson`（`F发起人指定处理人JSON`），格式 `{ "<stageKey>": [{ "userId": <long>, "userName": <string> }] }`；resolver 按 `stageDefinition.FStageKey` 取选人；`UpdateCardRequest.InitiatorAssignmentsJson`；`CardDetailDto.initiatorAssignmentsJson`（回显）。

- [ ] **Step 1：写 resolver 失败测试（按 stageKey 取选人 + 未选走 fallback）**

追加到 `ApproverResolverTests.cs`：
```csharp
    [Fact]
    public async global::System.Threading.Tasks.Task InitiatorSelect_ReadsAssignmentsByStageKey()
    {
        using var db = TestDbContextFactory.Create(nameof(InitiatorSelect_ReadsAssignmentsByStageKey));
        db.Set<SysUser>().AddRange(
            new SysUser { FID = 31, FName = "发起人指定甲", FStatus = 1 },
            new SysUser { FID = 32, FName = "发起人指定乙", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 800, FInitiatorAssignmentsJson = """{"review":[{"userId":31,"userName":"发起人指定甲"},{"userId":32,"userName":"发起人指定乙"}]}""" };
        var stage = new CfStageDefinition { FStageKey = "review", FAssigneeStrategy = "initiatorSelect" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(), flowOrgId: 100, initiatorId: 99);

        Assert.True(result.Success);
        Assert.Equal(new long[] { 31, 32 }, result.Approvers.Select(a => a.UserId));
        Assert.All(result.Approvers, a => Assert.Equal("initiatorSelect", a.Source));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task InitiatorSelect_NoSelectionFallsBackToFlowAdmin()
    {
        using var db = TestDbContextFactory.Create(nameof(InitiatorSelect_NoSelectionFallsBackToFlowAdmin));
        db.Set<SysUser>().Add(new SysUser { FID = 9, FName = "流程管理员", FStatus = 1 });
        await db.SaveChangesAsync();

        var resolver = new ApproverResolver(db);
        var card = new CfCard { FID = 801, FInitiatorAssignmentsJson = """{"other":[{"userId":5}]}""" };
        var stage = new CfStageDefinition { FStageKey = "review", FAssigneeStrategy = "initiatorSelect", FAssigneeConfigJson = """{"fallback":{"type":"flowAdmin"}}""" };

        var result = await resolver.ResolveAsync(stage, card, new Dictionary<string, object?>(),
            flowOrgId: 100, initiatorId: 99, flowSettingsJson: """{"approvalAdminUserIds":[9]}""");

        Assert.True(result.Success);
        Assert.Equal(9, result.Approvers[0].UserId);
        Assert.Contains("flowAdmin", result.FallbackReason);
    }
```

> 该测试引用 `CfCard.FInitiatorAssignmentsJson` —— 编译会失败直到 Step 3 加实体属性。故 Step 2 先加实体属性再跑失败测试。

- [ ] **Step 2：加实体属性 + 列映射（先让测试可编译）**

`CfCard.cs`（`FRowVersion` 之前）加：
```csharp
    /// <summary>发起人自选处理人(initiatorSelect 策略)：发起时按 stageKey 指定后续节点处理人。
    /// 格式 { "&lt;stageKey&gt;": [{ "userId": long, "userName": string }] }。null=未指定。</summary>
    public string? FInitiatorAssignmentsJson { get; set; }
```
`CfCardConfiguration.cs`（`FRowVersion` 映射之前）加：
```csharp
        builder.Property(e => e.FInitiatorAssignmentsJson).HasColumnName("F发起人指定处理人JSON");
```

- [ ] **Step 3：跑 resolver 测试确认失败**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`（过滤 `InitiatorSelect`）
Expected: 编译通过、用例 FAIL（无 `initiatorSelect` case）。

- [ ] **Step 4：resolver 加 case + 私有方法 + 归一化**

`ApproverResolver.cs` switch（prevStage case 之后）：
```csharp
            "initiatorSelect" => ResolveInitiatorSelectAsync(card, stageDefinition),
```
（注：该方法不查库、同步返回，无需 await——但为 switch 表达式类型一致仍返回 `ApproverResolveResult`，写成同步方法即可；若与相邻 `await` 分支类型不合，包一层 `await Task.FromResult(...)` 或直接让方法返回 `ApproverResolveResult` 并去掉 await。实现时确认 switch 各臂类型统一：其余臂多为 `Task<ApproverResolveResult>`，故本方法也返回 `Task<ApproverResolveResult>`。）

采用异步签名以对齐 switch：
```csharp
            "initiatorSelect" => await ResolveInitiatorSelectAsync(card, stageDefinition, cancellationToken),
```
`NormalizeStrategy` 加 `"initiatorSelect" => "initiatorSelect",`。新增私有方法：
```csharp
    /// <summary>
    /// 发起人自选：发起时持久化于 CfCard.FInitiatorAssignmentsJson({ stageKey: [{userId,userName}] })，
    /// 本节点按 FStageKey 取发起人为其指定的处理人。未指定→空集交 ApplyFallbackAsync/fail-closed。
    /// </summary>
    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveInitiatorSelectAsync(
        CfCard card,
        CfStageDefinition stageDefinition,
        CancellationToken cancellationToken)
    {
        var assignments = ParseObject(card.FInitiatorAssignmentsJson);
        if (!TryGetProperty(assignments, stageDefinition.FStageKey, out var picked))
        {
            return new ApproverResolveResult();
        }

        var userIds = NormalizeJsonUserIds(picked).ToList();
        return await ResolveUserIdsAsync(userIds, "initiatorSelect", cancellationToken);
    }
```

- [ ] **Step 5：跑测试确认通过**

Run: `scripts/dev/test-dotnet.ps1 CardFlow`
Expected: initiatorSelect resolver 用例 PASS。

- [ ] **Step 6：保存归一化 round-trip（AssigneeStrategyNormalizationTests + case）**

追加 `SaveDraftVersion_PreservesInitiatorSelectCasing`（镜像前，策略 `"initiatorSelect"`）；跑失败；`FlowDefinitionService.NormalizeAssigneeStrategy` 加 `"initiatorselect" => "initiatorSelect",`；跑通过。

- [ ] **Step 7：实体持久化 round-trip 测试（新建）**

新建 `tests/STOTOP.Module.CardFlow.Tests/Approval/CfCardInitiatorAssignmentsPersistenceTests.cs`：
```csharp
using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CfCardInitiatorAssignmentsPersistenceTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task CfCard_PersistsInitiatorAssignmentsJson()
    {
        using var db = TestDbContextFactory.Create(nameof(CfCard_PersistsInitiatorAssignmentsJson));
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 900, FFlowDefinitionId = 1, FFlowVersionId = 1, FStatus = "draft",
            FInitiatorId = 1, FInitiatorName = "u", FCreatedTime = DateTime.Now, FOrgId = 1,
            FInitiatorAssignmentsJson = """{"review":[{"userId":7,"userName":"甲"}]}"""
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Set<CfCard>().FindAsync(900L);
        Assert.NotNull(reloaded);
        Assert.Contains("\"userId\":7", reloaded!.FInitiatorAssignmentsJson);
    }
}
```
Run 确认 PASS（属性存在即绿；InMemory 不校验列名——列名由 seeder/运行时保证）。

- [ ] **Step 8：DTO + CardService 持久化 + 回显透出**

`Requests.cs` `UpdateCardRequest`（`:263-268`）加：
```csharp
    /// <summary>发起人自选(initiatorSelect)：{ stageKey: [{userId,userName}] }。null=不更新。</summary>
    public string? InitiatorAssignmentsJson { get; set; }
```
`CardService.UpdateAsync`（`:889` `if (request.DataJson != null) card.FDataJson = request.DataJson;` 之后）加：
```csharp
        if (request.InitiatorAssignmentsJson != null)
            card.FInitiatorAssignmentsJson = request.InitiatorAssignmentsJson;
```
`CardDetailDto`（读 `src/STOTOP.Module.CardFlow/Dtos/` 找定义）加 `public string? InitiatorAssignmentsJson { get; set; }`，在 `GetByIdAsync` 的投影里加 `InitiatorAssignmentsJson = card.FInitiatorAssignmentsJson`（回显草稿已选，避免二次编辑丢失）。
Run: `scripts/dev/build-filter.ps1 cardflow` 确认编译通过。

- [ ] **Step 9：seeder 建列 V79（先重查末版本）**

先 `grep -n "new(7[5-9]\|new(8" src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs` 与查 SYS 迁移历史确认末版本；若仍为 V78，用 V79（否则用下一个空号）。在版本注册列表（`:102` V78 项之后）追加：
```csharp
            new(79, "M8-E 发起人自选(initiatorSelect): CF流程实例 加 F发起人指定处理人JSON 列(nvarchar(max) null) — 发起时按 stageKey 存 {stageKey:[{userId,userName}]}, resolver initiatorSelect 分支按节点键取选人 (2026-07-12)", MigrateV79),
```
在 `MigrateV78` 方法附近新增：
```csharp
    /// <summary>V79：CF流程实例 加 F发起人指定处理人JSON（M8-E initiatorSelect 发起人自选持久化）。</summary>
    private static void MigrateV79(STOTOPDbContext ctx)
    {
        ExecSql(ctx, @"IF COL_LENGTH(N'CF流程实例', N'F发起人指定处理人JSON') IS NULL ALTER TABLE [CF流程实例] ADD [F发起人指定处理人JSON] NVARCHAR(MAX) NULL;");
    }
```
Run: `scripts/dev/build-filter.ps1 cardflow` 确认编译通过（seeder 在 WebAPI 工程，`cardflow.slnf` 不含——用 `build-filter cardflow` 若不覆盖 WebAPI 则改用 `/p:UseSharedCompilation=false` 编 WebAPI 工程或整图 `-o scratch`；至少确认 CardFlowSeeder 语法编译）。

- [ ] **Step 10：前端策略项 + labels/summary（无策略附属，含 fallback）**

`stageDefinitionShared.ts`：`normalizeAssigneeStrategy` 加 `case 'initiatorselect': return 'initiatorSelect'`；`ASSIGNEE_STRATEGY_LABELS` 加 `initiatorSelect: '发起人自选'`；`formatAssigneeSummary` 加 `if (strategy === 'initiatorSelect') return '发起人自选'`。
`stageDefinitionShared.spec.ts` 加对应断言（先失败后绿）。
`StageConfigPanel.vue`：`ASSIGNEE_STRATEGIES` 加 `{ value: 'initiatorSelect', label: '发起人自选', hint: '发起时由发起人为本节点指定处理人' }`；`isFallbackConfigStrategy` 加 `|| strategy === 'initiatorSelect'`（无策略附属，但共享 fallback 便于未选时兜底）。**无** buildAssigneeConfig 专属分支（走 `return null` 尾，但需保留 fallback——故加分支 `if (stage.assigneeStrategy === 'initiatorSelect') return { fallback }`）。

- [ ] **Step 11：CardFlowPanel fill 选人器（类型 + ref + 加载 + 模板 + payload + 回显）**

`web/src/types/cardflow.ts`：`UpdateCardRequest`（`:700`）加 `initiatorAssignmentsJson?: string | null`；`CardDetailDto`（找定义）加 `initiatorAssignmentsJson?: string | null`。

`CardFlowPanel.vue`：
① 新增 state（近 `editFormData` 处）：
```ts
import { useUserSearch } from '@/composables/useUserSearch'
const { userOptions: initiatorUserOptions, loading: initiatorUserLoading, search: onInitiatorUserSearch, pin: pinInitiatorUser } = useUserSearch({ pageSize: 50 })
const initiatorSelectStages = ref<{ stageKey: string; stageName: string }[]>([])
const initiatorAssignments = ref<Record<string, { userId: number; userName: string }[]>>({})
```
② `loadCardDetail` 取到 `ver`（`:621` 之后、`:633` 之前）加：
```ts
        initiatorSelectStages.value = (ver.stages || [])
          .filter(s => s.assigneeStrategy === 'initiatorSelect' && s.stageKey)
          .map(s => ({ stageKey: s.stageKey as string, stageName: s.stageName || (s.stageKey as string) }))
```
③ fill 模式初始化（`:636-641` 块内）加回显已存选择：
```ts
      try {
        initiatorAssignments.value = card.initiatorAssignmentsJson ? JSON.parse(card.initiatorAssignmentsJson) : {}
      } catch { initiatorAssignments.value = {} }
      initiatorAssignments.value = { ...initiatorAssignments.value }
      // pin 已选项以便回显 label
      Object.values(initiatorAssignments.value).flat().forEach(u => pinInitiatorUser({ label: u.userName || `#${u.userId}`, value: u.userId, name: u.userName || `#${u.userId}` }))
```
④ 多选双向代理 helper（stageKey → number[]）：
```ts
function initiatorPicked(stageKey: string) {
  return {
    get: () => (initiatorAssignments.value[stageKey] || []).map(u => u.userId),
    set: (ids: number[]) => {
      const prev = new Map((initiatorAssignments.value[stageKey] || []).map(u => [u.userId, u]))
      initiatorAssignments.value[stageKey] = ids.map(id => {
        const opt = initiatorUserOptions.value.find(o => o.value === id)
        return { userId: id, userName: opt?.name || prev.get(id)?.userName || '' }
      })
      initiatorAssignments.value = { ...initiatorAssignments.value }
    },
  }
}
```
⑤ fill 表单区（在 fill 模式表单可滚动区、`cf-panel__actions--fill` 之前）插入选人器 section（仅当 `initiatorSelectStages.length`）：
```html
        <div v-if="mode === 'fill' && initiatorSelectStages.length" class="cf-panel__initiator-assign">
          <div class="cf-panel__section-title">指定处理人</div>
          <div v-for="st in initiatorSelectStages" :key="st.stageKey" class="cf-panel__assign-row">
            <label>{{ st.stageName }}</label>
            <a-select
              :value="initiatorPicked(st.stageKey).get()"
              mode="multiple"
              style="width: 100%"
              placeholder="搜索并选择处理人"
              :options="initiatorUserOptions"
              :loading="initiatorUserLoading"
              show-search
              :filter-option="false"
              @search="onInitiatorUserSearch"
              @change="(v: number[]) => initiatorPicked(st.stageKey).set(v)"
            />
          </div>
        </div>
```
⑥ `buildSavePayload`（`:1120-1124` return）加字段：
```ts
    initiatorAssignmentsJson: initiatorSelectStages.value.length ? JSON.stringify(initiatorAssignments.value) : null,
```
⑦ 样式：`.cf-panel__section-title` / `.cf-panel__assign-row` 用现有 token（`var(--...)`）；若需新样式加进 `<style>` 段，**禁裸 hex**。

- [ ] **Step 12：门禁 + 全量测试**

Run: `cd web && npm run type-check && npm run lint:style`；`scripts/dev/test-dotnet.ps1 CardFlow`
Expected: 全绿。

- [ ] **Step 13：Commit（先 `git status --short` 查冲突；确认 seeder 版本未被并发占）**

```bash
git add src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs \
        src/STOTOP.Module.CardFlow/Entities/CfCard.cs \
        src/STOTOP.Module.CardFlow/Configurations/CfCardConfiguration.cs \
        src/STOTOP.Module.CardFlow/Dtos/Requests.cs \
        src/STOTOP.Module.CardFlow/Services/CardService.cs \
        src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs \
        src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs \
        web/src/components/cardflow/stageDefinitionShared.ts \
        web/src/components/cardflow/StageConfigPanel.vue \
        web/src/components/cardflow/CardFlowPanel.vue \
        web/src/types/cardflow.ts \
        web/src/components/cardflow/stageDefinitionShared.spec.ts \
        tests/STOTOP.Module.CardFlow.Tests/Approval/ApproverResolverTests.cs \
        tests/STOTOP.Module.CardFlow.Tests/Rules/AssigneeStrategyNormalizationTests.cs \
        tests/STOTOP.Module.CardFlow.Tests/Approval/CfCardInitiatorAssignmentsPersistenceTests.cs
# 若 CardDetailDto 在独立文件，一并 add 其路径
git commit -m "feat(cardflow): 处理人策略补 initiatorSelect 发起人自选(全链路) (M8-E 件③)

发起时由发起人为节点指定处理人:新列 CfCard.F发起人指定处理人JSON(seeder V79)存
{stageKey:[{userId,userName}]};UpdateCardRequest 加字段+CardService.UpdateAsync 持久化+
CardDetailDto 回显;resolver initiatorSelect 按 FStageKey 取选人(未选走fallback);发起端
CardFlowPanel(fill)消费 ver.stages 出选人器(全部initiatorSelect节点超集)。归一化两侧补
显式case。前端下拉5→8完成。

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## 批收口（三件后）

- [ ] **整体终审**：dispatch 子代理对抗性只读审查三 commit 的集成缝（resolver switch/归一化三处一致性、seeder 版本连续、前端 8 项下拉与后端标识一一对应、fill 选人器与 resolver 读取格式对齐 `{stageKey:[{userId,userName}]}`、fail-closed 边界）。
- [ ] **回归**：`scripts/dev/test-dotnet.ps1 CardFlow`（多跑一次判 flaky）+ `cd web && npm run type-check && npm run lint:style && npx vitest run src/components/cardflow/stageDefinitionShared.spec.ts`。
- [ ] **运行时验证（可选，dev 库）**：起后端触发 V79 迁移，查 `CF流程实例` 有 `F发起人指定处理人JSON` 列；发起一张带 initiatorSelect 节点的卡片、选人、提交，确认该节点处理人=所选。
- [ ] **不 push**，汇报三 commit hash + 终审结论，等用户点头。

## Self-Review 记录

- **Spec 覆盖**：superiorChain/prevStage/initiatorSelect 三策略各有 Task；§1.1 归一化硬坑→每 Task 的保存 round-trip 测试 + 两侧显式 case；§1.2 labels/summary→前端步骤；§1.3 动态白名单不加→未触碰 `DynamicStagePolicyResolver`（符合）；§4 initiatorSelect 全链路→seeder+DTO+service+resolver+fill UI 全覆盖。
- **占位扫描**：无 TODO/TBD；所有新代码给出完整实现。既有代码锚点给 file:line，新代码给全码。
- **类型一致**：`superiorChain`/`prevStage`/`initiatorSelect` 三标识在 resolver switch、NormalizeStrategy(resolver)、NormalizeAssigneeStrategy(service)、前端 normalize/labels/ASSIGNEE_STRATEGIES 全程一致 camelCase；config 键（maxLevels/sourceStageKey）与前后端读写一致；initiatorSelect JSON 格式 `{stageKey:[{userId,userName}]}` 在 resolver 读、CardFlowPanel 写、实体列三处一致。
- **待实现期确认项**：`StageDefinitionRequest`/`StageDefinitionDto` 的 AssigneeStrategy 字段名；面板 `StageDefinition.type` 取值（'manual' vs 'human'）；`CardDetailDto` 定义位置；seeder 末版本执行时重查。
