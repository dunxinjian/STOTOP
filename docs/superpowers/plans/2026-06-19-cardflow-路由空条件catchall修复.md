# CardFlow 路由空条件 catch-all 修复 实现计划（③-sub-1）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让非默认路由规则的空条件 / 空条件组运行时**永不命中**（不再成隐形 catch-all），并在发布期拦住「非默认分支空条件」。

**Architecture:** 三处独立小改：求值器空条件组 → Matched=false（共享 `ConditionRuleEvaluator`）；`StageRouteResolver` 非默认规则空 `FConditionJson` route-scoped 守卫不命中；`ValidateRouteRulesAsync` 新增「非默认分支必须配置条件」对偶校验。均不碰 `FlowEngineService`。

**Tech Stack:** .NET 10 / EF Core / xUnit（EFCore.InMemory，`--arch x64` 宿主）。

**上游 spec：** [docs/superpowers/specs/2026-06-19-cardflow-路由空条件catchall修复-design.md](../specs/2026-06-19-cardflow-路由空条件catchall修复-design.md)。

**已核实事实：**
- `ConditionEvaluationContext` 有无参构造（成员默认 `new()` 集合），可 `new ConditionEvaluationContext()`。
- `StageRouteResolverTests.cs` 有私有 `SeedStages(db)` 返回 `(Source,Finance,GeneralManager)`，`Source.FStageKey=="manager"`；构造 `new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db))`。**Task 2 测试追加到该文件**以复用 `SeedStages`。
- `PublishAsync(long id, long operatorId)`（FlowDefinitionService:199）在 line 225 调 `ValidateRouteRulesAsync(draftVersion.FID, draftStages)`，前置只需 def(draft) + draft 版本 + draftStages，无其它抛点。
- `FlowDefinitionService` 构造 `(STOTOPDbContext, ILogger<FlowDefinitionService>)`；测试用 `NullLogger<...>.Instance`。
- 项目 `Task` 命名空间遮蔽 → 测试 async 返回类型用 `global::System.Threading.Tasks.Task`。

**通用命令：** 测试 `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64`；模块编译 `dotnet build src/STOTOP.Module.CardFlow/STOTOP.Module.CardFlow.csproj`。**共享 master：仅 `git add` 本任务文件，勿 `-A`；并发提交裹入则核实内容报 SHA。**

---

## 文件结构
**修改：**
- `src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs` — 空条件组 → Matched=false。
- `src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs` — 非默认空条件守卫。
- `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs` — 发布校验对偶。
- `tests/STOTOP.Module.CardFlow.Tests/Rules/StageRouteResolverTests.cs` — 追加路由守卫测试（复用 SeedStages）。
**新增：**
- `tests/STOTOP.Module.CardFlow.Tests/Rules/ConditionRuleEmptyGroupTests.cs`
- `tests/STOTOP.Module.CardFlow.Tests/Rules/RoutePublishValidationTests.cs`

---

## Task 1：求值器空条件组 → 不匹配（#3）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs`（`EvaluateGroup`，约 :73-74）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/ConditionRuleEmptyGroupTests.cs`

- [ ] **Step 1：写失败测试**
```csharp
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class ConditionRuleEmptyGroupTests
{
    [Fact]
    public void EmptyConditionGroup_DoesNotMatch()
    {
        var result = new ConditionRuleEvaluator()
            .Evaluate("""{"logic":"and","conditions":[]}""", new ConditionEvaluationContext());
        Assert.False(result.Matched);
    }

    [Fact]
    public void OrGroup_ContainingOnlyEmptyGroup_DoesNotMatch()
    {
        var result = new ConditionRuleEvaluator()
            .Evaluate("""{"logic":"or","conditions":[{"logic":"and","conditions":[]}]}""", new ConditionEvaluationContext());
        Assert.False(result.Matched); // 内层空组不再贡献 true
    }
}
```

- [ ] **Step 2：运行确认失败**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter ConditionRuleEmptyGroupTests`
Expected: 2 FAIL（当前空组返回 Match → Matched=true）。

- [ ] **Step 3：改 `EvaluateGroup`**
在 `ConditionRuleEvaluator.cs` 的 `EvaluateGroup` 里，把
```csharp
        if (childResults.Count == 0)
            return ConditionRuleEvaluationResult.Match("空条件组默认匹配");
```
替换为
```csharp
        if (childResults.Count == 0)
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                Explanation = "空条件组，不匹配"
            };
```

- [ ] **Step 4：运行确认通过**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter ConditionRuleEmptyGroupTests`
Expected: 2 passed。

- [ ] **Step 5：模块编译 + 求值器既有回归**
Run: `dotnet build src/STOTOP.Module.CardFlow/STOTOP.Module.CardFlow.csproj`（0 错误）
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter ConditionRuleEvaluatorTests`（既有绿）

- [ ] **Step 6：提交**
```bash
git add src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs tests/STOTOP.Module.CardFlow.Tests/Rules/ConditionRuleEmptyGroupTests.cs
git commit -m "fix(cardflow): 空条件组求值改为不匹配,杜绝空组当 catch-all(#3)"
```

---

## Task 2：StageRouteResolver 非默认空条件守卫（#2）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs`（非默认规则迭代，:65-87）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/StageRouteResolverTests.cs`（追加，复用 `SeedStages`）

- [ ] **Step 1：追加失败测试**（加在 `StageRouteResolverTests` 类内，`SeedStages` 之前任意位置）
```csharp
    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_NonDefaultEmptyCondition_DoesNotBecomeCatchAll()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_NonDefaultEmptyCondition_DoesNotBecomeCatchAll));
        var stages = SeedStages(db);
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "empty_rule",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.GeneralManager.FID, FToStageKey = "gm",
                FRouteName = "空条件", FConditionJson = null, FPriority = 1, FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "default_finance",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.Finance.FID, FToStageKey = "finance",
                FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active"
            });
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":6800}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.Equal("default_finance", result.SelectedRoute?.FEdgeKey);   // 空条件非默认规则不命中 → 落默认分支
        Assert.Contains(result.Candidates, c => c.EdgeKey == "empty_rule" && !c.Matched);
    }
```

- [ ] **Step 2：运行确认失败**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter ResolveNextStage_NonDefaultEmptyCondition_DoesNotBecomeCatchAll`
Expected: FAIL（当前空条件规则 `empty_rule` 被命中选中，SelectedRoute 是它而非 default_finance）。

- [ ] **Step 3：加 route-scoped 守卫**
在 `StageRouteResolver.cs` 的 `foreach (var rule in outgoing.Where(rule => !rule.FIsDefault))` 循环体**最开头**（`var evaluation = _conditionRuleEvaluator.Evaluate(...)` 之前）插入：
```csharp
            if (string.IsNullOrWhiteSpace(rule.FConditionJson))
            {
                result.Candidates.Add(new StageRouteCandidateResult
                {
                    RouteRuleId = rule.FID,
                    EdgeKey = rule.FEdgeKey,
                    RouteName = rule.FRouteName,
                    ToStageKey = rule.FToStageKey,
                    Priority = rule.FPriority,
                    IsDefault = rule.FIsDefault,
                    Matched = false,
                    Explanation = "非默认分支缺条件，不命中",
                    TypeErrors = new List<string> { "非默认分支未配置条件" }
                });
                continue;
            }
```
> 注：`StageRouteCandidateResult.TypeErrors`/`ConsumedFields` 若是 `List<string>` 且默认已初始化，则 `Explanation`/`Matched` 直接赋值即可；`TypeErrors = new List<string>{...}` 与现有用法一致（现有代码把 `evaluation.TypeErrors` 赋给它）。若该属性是 init-only 或类型不同，按其真实定义调整（读 `StageRouteCandidateResult` 确认）。

- [ ] **Step 4：运行确认通过**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter "FullyQualifiedName~StageRouteResolverTests"`
Expected: 全绿（新用例 + 既有 3 个路由用例）。

- [ ] **Step 5：模块编译**
Run: `dotnet build src/STOTOP.Module.CardFlow/STOTOP.Module.CardFlow.csproj`（0 错误）

- [ ] **Step 6：提交**
```bash
git add src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs tests/STOTOP.Module.CardFlow.Tests/Rules/StageRouteResolverTests.cs
git commit -m "fix(cardflow): 路由非默认规则空条件不命中,杜绝隐形 catch-all 截胡真实分支(#2)"
```

---

## Task 3：ValidateRouteRulesAsync 非默认必须带条件（#2b）

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs`（`ValidateRouteRulesAsync`，:758-764）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/RoutePublishValidationTests.cs`

- [ ] **Step 1：写失败测试**
```csharp
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class RoutePublishValidationTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task Publish_NonDefaultRuleWithEmptyCondition_Throws()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_NonDefaultRuleWithEmptyCondition_Throws));
        var def = new CfFlowDefinition { FFlowName = "t", FFlowCode = "PUB1", FStatus = "draft" };
        db.Set<CfFlowDefinition>().Add(def);
        await db.SaveChangesAsync();
        var ver = new CfFlowVersion { FFlowDefinitionId = def.FID, FVersionNumber = 1, FStatus = "draft" };
        db.Set<CfFlowVersion>().Add(ver);
        await db.SaveChangesAsync();
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "a", FStageName = "A", FSortOrder = 1 },
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "b", FStageName = "B", FSortOrder = 2 });
        await db.SaveChangesAsync();
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e1", FFromStageKey = "a", FToStageKey = "b", FRouteName = "空", FConditionJson = null, FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e2", FFromStageKey = "a", FToStageKey = "b", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var svc = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() => svc.PublishAsync(def.FID, 1));
        Assert.Contains("非默认分支必须配置条件", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_NonDefaultRuleWithCondition_PassesRouteValidation()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_NonDefaultRuleWithCondition_PassesRouteValidation));
        var def = new CfFlowDefinition { FFlowName = "t", FFlowCode = "PUB2", FStatus = "draft" };
        db.Set<CfFlowDefinition>().Add(def);
        await db.SaveChangesAsync();
        var ver = new CfFlowVersion { FFlowDefinitionId = def.FID, FVersionNumber = 1, FStatus = "draft" };
        db.Set<CfFlowVersion>().Add(ver);
        await db.SaveChangesAsync();
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "a", FStageName = "A", FSortOrder = 1 },
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "b", FStageName = "B", FSortOrder = 2 });
        await db.SaveChangesAsync();
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e1", FFromStageKey = "a", FToStageKey = "b", FRouteName = "条件", FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e2", FFromStageKey = "a", FToStageKey = "b", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var svc = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        // 不应因路由校验抛「非默认分支必须配置条件」（其余发布步骤是否抛与本校验无关——只断言不是该条消息）
        var ex = await Record.ExceptionAsync(() => svc.PublishAsync(def.FID, 1));
        Assert.True(ex == null || !ex.Message.Contains("非默认分支必须配置条件"));
    }
}
```
> 第二个用例容忍 `PublishAsync` 后续步骤（FanOut 等）可能抛别的异常，只断言**不是**本次新增的「非默认分支必须配置条件」——即有条件的非默认规则通过了路由校验这一关。

- [ ] **Step 2：运行确认失败**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter RoutePublishValidationTests`
Expected: `Publish_NonDefaultRuleWithEmptyCondition_Throws` FAIL（当前无该校验，空条件非默认规则不抛此消息——可能抛别的或继续，断言 message 不含目标串 → 失败）。

- [ ] **Step 3：加对偶校验**
在 `ValidateRouteRulesAsync` 的 `foreach (var rule in group)` 里，既有
```csharp
                if (rule.FIsDefault && !string.IsNullOrWhiteSpace(rule.FConditionJson))
                    throw new InvalidOperationException($"默认分支不能配置条件：{rule.FEdgeKey}");
```
之后紧接加：
```csharp
                if (!rule.FIsDefault && string.IsNullOrWhiteSpace(rule.FConditionJson))
                    throw new InvalidOperationException($"非默认分支必须配置条件：{rule.FEdgeKey}");
```

- [ ] **Step 4：运行确认通过**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64 --filter RoutePublishValidationTests`
Expected: 2 passed。

- [ ] **Step 5：模块编译**
Run: `dotnet build src/STOTOP.Module.CardFlow/STOTOP.Module.CardFlow.csproj`（0 错误）

- [ ] **Step 6：提交**
```bash
git add src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs tests/STOTOP.Module.CardFlow.Tests/Rules/RoutePublishValidationTests.cs
git commit -m "fix(cardflow): 发布校验新增「非默认分支必须配置条件」对偶,发布期拦住空条件 catch-all(#2b)"
```

---

## Task 4：收尾验证

- [ ] **Step 1：全量回归 + 模块编译**
Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64`
Expected: 绿（既有 + 本计划新增 ~6 用例；需真 SQL Server 的集成测试若环境无连接失败属预存,与本改动无关——确认失败项均为 SQL Server 集成测试）。
Run: `dotnet build src/STOTOP.Module.CardFlow/STOTOP.Module.CardFlow.csproj`（0 错误）

- [ ] **Step 2：人工核对**
- 设计器删光某非默认分支条件 → 发布报「非默认分支必须配置条件」。
- 运行时：非默认空条件规则不再截胡，卡片走真实条件或默认分支。

---

## 自检（spec 覆盖）
- 目标1 非默认空条件不命中 → Task 2。
- 目标2 空条件组不命中 → Task 1。
- 目标3 发布期拦非默认空条件 → Task 3。
- 非目标（不动顶层空 conditionJson line 13 / 不做图级校验）→ 计划未触碰，符合。
