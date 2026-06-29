# CardFlow 预演≠运行时收敛（设计稿）

> 日期：2026-06-19　状态：设计已确认（无产品分叉，纯收敛 + bug 修复）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）③ 节点路由。本子项收敛 [③a 路由报告](../../audits/audit-report-3a-routing.md) **#5（预演与运行时上下文三处分叉）** 与 **#4 在预演侧的残留**。
> 已完成前序子项：脱敏链重构、①v2 信封、②组织隔离三连、③ 空条件 catch-all（sub-1）、③#4（运行时 TypeError 选边）。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-19 复核）。

---

## 0. 背景与缺陷（已核实）

路由「求值引擎」本身健康，但**预演（设计器「模拟运行」）与运行时各自构造一份求值上下文 + 各写一份选边守卫**，导致「预演通过 ≠ 上线走对」。逐条核对运行时与预演两侧实现：

| 维度 | 运行时 | 预演 | 现状 |
|---|---|---|---|
| **InitiatorOrg 键** | `["id"] = card.FOrgId`（[ConditionEvaluationContextBuilder.cs:44-47](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:44)） | `["orgId"] = request.OrgId`（[CardFlowPathPreviewService.cs:255-258](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:255)） | **键名失配**：按组织路由的条件预演恒不命中 |
| **DetailSummary** | 构建 `rowCount/amount/tax/actualPayAmount`（[:65](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:65)、`BuildDetailSummary` :69-93） | **完全不构造**（`BuildPreviewContext` 无 DetailSummary 赋值） | 任何 `detailSummary.*` 条件预演 `Exists=false`，`notExists` 类算子预演反而恒真，方向不可预测 |
| **空条件非默认规则** | route-scoped 守卫 → `Matched=false`、`continue` 不选中（[StageRouteResolver.cs:65-82](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:65)） | `SelectRoute` 直接 `Evaluate(route.FConditionJson, …)`，空条件经 [ConditionRuleEvaluator.cs:13-14](../../../src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:13) 返回 `Match("空条件默认匹配")` → 预演**仍把空条件非默认规则当 catch-all 选中**（[CardFlowPathPreviewService.cs:160-166](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:160)） | **预演带 ③ sub-1 已修复的 catch-all bug**，预演结论与运行时相反 |
| **TypeError 命中** | ③#4 已改为 `if (evaluation.Matched)` 单条件选中（[:97](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:97)） | 仍 `if (evaluation.Matched && evaluation.TypeErrors.Count == 0)`（[:164](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:164)） | **守卫失配**：含类型错子条件的 or 命中，运行时选中、预演跳过 |
| **来源边匹配** | `FFromStageKey OR FFromStageDefinitionId` 双键（[:48-49](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:48)） | 仅 `route.FFromStageKey == currentKey` 单键（[:108](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:108)） | **失配**：仅靠 DefinitionId 关联的边预演漏看 |

### 已核实的关键事实
- **预演的动态策略守卫**（[CardFlowPathPreviewService.cs:198](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:198)，`!evaluation.Matched || evaluation.TypeErrors.Count > 0`）与运行时 [DynamicStagePolicyResolver.cs:101](../../../src/STOTOP.Module.CardFlow/Services/DynamicStagePolicyResolver.cs:101) **本就彼此一致**（③#4 未改 DynamicStagePolicyResolver）。故**本次不动它**，避免引入新的两侧不一致。
- 预演请求 [CardFlowPathPreviewRequest](../../../src/STOTOP.Module.CardFlow/Dtos/Requests.cs:156) **无明细输入**字段。故本次预演侧 DetailSummary 以「空明细 → 四键全 0」的一致形状产出（消除 `notExists` 反转），**真实明细金额的预演需前端补明细输入（#5d，延后）**。
- 测试基建齐备：[CardFlowPathPreviewServiceTests](../../../tests/STOTOP.Module.CardFlow.Tests/Rules/CardFlowPathPreviewServiceTests.cs) 已用 `TestDbContextFactory.Create` 种「流程定义 + 版本 + 节点 + 路由规则」跑真预演，可直接追加预演↔运行时等价回归。

---

## 1. 目标 / 非目标

### 目标
1. **抽共享 `ConditionContextFactory` 纯函数**，独占 `ConditionEvaluationContext` 全键空间组装；运行时（`ConditionEvaluationContextBuilder`）与预演（`CardFlowPathPreviewService`）两处求值上下文收敛为单一真源。
   - `InitiatorOrg` 键统一为 `id`（修预演 `orgId` 失配）。
   - `DetailSummary` 在预演侧补建为一致形状（空明细→四键全 0）。
2. **就地对齐预演 `SelectRoute` 选边守卫**与运行时 `StageRouteResolver`：空条件非默认规则不命中、TypeError 命中仍选中、来源边补 DefinitionId 双键。
3. 用**预演↔运行时等价回归测试**钉死一致性，防止未来再次漂移。

### 非目标（明确不做）
- ❌ 不接线性兜底 #7（`FlowEngineService` → 旧 `ConditionEvaluator` DSL，仅塞 CardData）——跨旧求值器，是另一改造，留后续子项；工厂留好扩展点即可。
- ❌ 不做前端 #5d（`PathPreviewPanel.vue` 写死 `amount/feeType/hasExpenseRequest/hasLoan/cardStatus` 五字段）——前端按 `cardSchema` 动态生成预演表单 + 明细行输入是独立子项，与本后端收敛解耦。
- ❌ **不抽共享选边器**：本次「选边」用就地对齐（复刻三条规则 + 等价回归），不重构 `StageRouteResolver`/候选快照。用户已拍板「就地对齐守卫」。
- ❌ 不动预演动态策略守卫（[:198](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:198)）——与其运行时对端本就一致。
- ❌ 不收敛 `ParseObject`（两份各异：运行时 `GetRawText` vs 预演 `ToString` 兜底）与 `ResolveField`（#11）——非本子项命名范围，改了是边角行为变更；工厂收「已解析的 Dictionary」，把 JSON 解析留在各 wrapper。

---

## 2. 设计

### 2.1 共享纯函数 `ConditionContextFactory`（交付物 1，方案 A：独占全键空间）

新建 `src/STOTOP.Module.CardFlow/Services/ConditionContextFactory.cs`，**无 DbContext、纯函数**：

```csharp
namespace STOTOP.Module.CardFlow.Services;

public sealed class ConditionContextInputs
{
    public required Dictionary<string, object?> CardData { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> DetailData { get; init; }
        = Array.Empty<IReadOnlyDictionary<string, object?>>();

    // Source（运行时 5 键 / 预演 3 键；运行时独有的两键预演传 null）
    public string? SourceModule { get; init; }
    public string? SourceType { get; init; }
    public long? SourceId { get; init; }
    public string? ReturnUrl { get; init; }
    public string? SourceTitle { get; init; }

    // Initiator
    public long? InitiatorId { get; init; }
    public string? InitiatorName { get; init; }

    // Org（键名钉死 "id"）
    public long? OrgId { get; init; }

    // CurrentStageResult 基段（completedStageKey 由运行时 wrapper 调用后补写——那是 DB I/O）
    public int? CurrentRound { get; init; }
    public long? CurrentStageInstanceId { get; init; }
    public string? CurrentStageAction { get; init; }
    public bool HasCurrentStage { get; init; }   // 预演无当前节点 → false，CurrentStageResult 留空
}

public static class ConditionContextFactory
{
    public static ConditionEvaluationContext Build(ConditionContextInputs inputs);
}
```

工厂内部职责（全键空间单一真源）：
- `CardData` 原样透传（已由 wrapper 解析、合并）。
- `SourceContext = { sourceModule, sourceType, sourceId, returnUrl, sourceTitle }`（键名一份；预演侧 returnUrl/sourceTitle 为 null）。
- `Initiator = { id, name }`。
- **`InitiatorOrg = { ["id"] = OrgId }`**（消除 orgId/id 失配的根）。
- `CurrentStageResult`：`HasCurrentStage` 为真时填 `{ round, stageInstanceId, action }`；否则留空 dict（与现预演一致）。
- **`DetailSummary = BuildDetailSummary(DetailData)`**：把现 `ConditionEvaluationContextBuilder.BuildDetailSummary`/`ReadDecimal`（[:69-100](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:69)）整段搬入工厂；空明细 → `{rowCount:0, amount:0, tax:0, actualPayAmount:0}`（形状恒定）。

> `DetailData` 收「已解析的明细行字典」，工厂不碰 JSON 解析（保持纯、与 DbContext 解耦）。

### 2.2 运行时 wrapper：`ConditionEvaluationContextBuilder.BuildAsync` 改造

行为不变，仅把组装收敛到工厂：
1. DB 加载 `CfCardDetail`（不变）→ 用现有 `ParseObject` 把每行 `FDataJson` 解析为字典列表。
2. 调 `ConditionContextFactory.Build(new ConditionContextInputs { CardData=ParseObject(card.FDataJson), DetailData=detailDicts, SourceModule=card.FSourceModule, …, ReturnUrl=card.FReturnUrl, SourceTitle=card.FSourceTitle, InitiatorId=card.FInitiatorId, InitiatorName=card.FInitiatorName, OrgId=card.FOrgId, CurrentRound=card.FCurrentRound, CurrentStageInstanceId=currentStage?.FID, CurrentStageAction=currentStage?.FFinalAction, HasCurrentStage=true })`。
3. 工厂返回后，**保留** `completedStageKey` 的二次 DB 查（[:56-63](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:56)）：`context.CurrentStageResult["completedStageKey"] = …`。
4. `BuildDetailSummary`/`ReadDecimal` 从本类移除（已搬入工厂）。`ParseObject`/`ToPlainValue` 保留（解析职责仍在此）。

### 2.3 预演 wrapper：`CardFlowPathPreviewService.BuildPreviewContext` 改造

`BuildPreviewContext(request)`：
1. 解析并合并 `InitialDataJson` + `DataJson`（不变）→ cardData。
2. 调 `ConditionContextFactory.Build(new ConditionContextInputs { CardData=cardData, DetailData=空, SourceModule=request.SourceModule, SourceType=request.SourceType, SourceId=request.SourceId, ReturnUrl=null, SourceTitle=null, InitiatorId=request.InitiatorId, InitiatorName=null, OrgId=request.OrgId, HasCurrentStage=false })`。
3. `orgId→id` 修正、`DetailSummary` 补建在此自动兑现，预演上下文键形状与运行时一致。

### 2.4 选边守卫就地对齐（交付物 2）

`CardFlowPathPreviewService`：

**(a) 来源边匹配补 DefinitionId 双键**——outgoing 过滤（[:107-111](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:107)）。当前预演按 `current.FStageKey` 走，需同时按 `current.FID` 匹配：
```csharp
var outgoing = routes
    .Where(route =>
        (!string.IsNullOrWhiteSpace(currentKey) && string.Equals(route.FFromStageKey, currentKey, StringComparison.OrdinalIgnoreCase))
        || route.FFromStageDefinitionId == current.FID)
    .OrderBy(route => route.FPriority).ThenBy(route => route.FID).ToList();
```

**(b) `SelectRoute` 非默认规则迭代**（[:160-166](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:160)）镜像 [StageRouteResolver.cs:65-104](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:65)：
```csharp
foreach (var route in outgoing.Where(route => !route.FIsDefault))
{
    if (string.IsNullOrWhiteSpace(route.FConditionJson))
    {
        step.Candidates.Add(/* Matched=false, Explanation="非默认分支缺条件，不命中",
                               TypeErrors=["非默认分支未配置条件"] */);
        continue;   // 空条件非默认规则永不被预演选中（消除 catch-all）
    }
    var evaluation = _conditionRuleEvaluator.Evaluate(route.FConditionJson, context);
    step.Candidates.Add(ToCandidate(route, evaluation));
    if (evaluation.Matched)   // ← 去掉 `&& evaluation.TypeErrors.Count == 0`
        return route;
}
```

> 默认分支兜底（[:168-183](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:168)）不变。`step.Reason` 文案沿用现有；可选地在含 TypeError 命中时附「（含类型错误子条件，已忽略）」与运行时 `result.Reason`（[:100-102](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:100)）对齐——细节归计划阶段。

---

## 3. 兼容性与风险

- **2.1 工厂为纯新增**：不改 `ConditionEvaluationContext` 模型（键集不变）。运行时上下文与改造前**逐键等价**（含 `completedStageKey` 仍由 wrapper 补写），现有运行时/路由测试应原样绿。
- **2.3 预演上下文行为变更（向修正方向）**：`InitiatorOrg` 由 `orgId` 改 `id` → 之前恒不命中的组织路由条件预演开始能命中（这正是 bug 修复）；`DetailSummary` 由「缺失」改「全 0」→ `detailSummary.* exists` 由 false 变 true、`notExists` 由 true 变 false（消除反转）。属把「预演结论与运行时相反」改为一致，是预期内的行为修正。
- **2.4 选边守卫**：把预演「与运行时相反的选边」改为一致——空条件非默认不再被预演当 catch-all、TypeError 命中预演不再跳过、DefinitionId-only 边预演可跟随。均为 fail-closed/对齐方向。
- **存量数据**：无 schema/迁移变更，无落库改动；仅影响「预演」这一只读模拟与「运行时构造上下文」的内部组装路径。
- 整体风险低：一处纯新增 + 两处 wrapper 等价改造 + 一处守卫对齐，均有测试钉住。

---

## 4. 测试（TDD，x64 必带）

### 4.1 工厂纯单测（无 DbContext）
新增 `tests/STOTOP.Module.CardFlow.Tests/Rules/ConditionContextFactoryTests.cs`：
- `InitiatorOrg` 键为 `id`、值为传入 OrgId。
- 空明细 → `DetailSummary = {rowCount:0, amount:0, tax:0, actualPayAmount:0}`（四键齐、全 0）。
- 有明细（含 `amount`/`tax`/`payAmount`/`实付金额` 等别名）→ 聚合数值与现 `BuildDetailSummary` 口径一致（回归口径）。
- `HasCurrentStage=false` → `CurrentStageResult` 为空；`=true` → 含 `round/stageInstanceId/action`。

### 4.2 运行时等价回归（保护重构）
- 现有 `StageRouteResolverTests` / 路由相关测试全绿（证明 `BuildAsync` 收敛后上下文逐键等价）。若现无直接断言上下文键的测试，在工厂单测层覆盖即可，不强行加重型集成测试。

### 4.3 预演↔运行时等价回归（`CardFlowPathPreviewServiceTests` 追加，复用 `TestDbContextFactory`）
- **空条件非默认规则不被预演当 catch-all**：种一条 `FConditionJson=null` 的非默认规则 + 一条默认分支 → 预演落默认分支（而非空条件规则）。
- **DefinitionId-only 边可跟随**：种一条 `FFromStageKey` 为空、仅 `FFromStageDefinitionId` 关联的边 → 预演能选中并前进。
- **InitiatorOrg/DetailSummary 形状一致**：构造同一份输入分别走运行时上下文与预演上下文，断言 `InitiatorOrg["id"]` 同源、`DetailSummary` 四键齐。
- （可选）**TypeError 命中预演仍选中**：or 组含一个类型不兼容子条件但整体真命中 → 预演选中该边（与运行时一致）。

### 4.4 收尾
- `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64` 全绿（x64 必带，见团队约定）。
- `STOTOP.Module.CardFlow` 编译 0 错。

---

## 5. 任务分解预览（供 writing-plans）

1. **工厂落地**：新建 `ConditionContextInputs` + `ConditionContextFactory`，搬入 `BuildDetailSummary`/`ReadDecimal`；工厂纯单测（4.1）。【TDD：先测后实现】
2. **运行时 wrapper 收敛**：`ConditionEvaluationContextBuilder.BuildAsync` 改调工厂、保留 `completedStageKey` 补写、移除已搬走的私有方法；现有运行时/路由测试回归绿（4.2）。
3. **预演 wrapper 收敛**：`CardFlowPathPreviewService.BuildPreviewContext` 改调工厂（orgId→id、DetailSummary 补建）；预演上下文形状一致性测试（4.3 第三条）。
4. **选边守卫对齐**：`SelectRoute` 空条件守卫 + 去 TypeErrors 子句、outgoing 来源边补 DefinitionId 双键；预演↔运行时等价回归（4.3 其余）。
5. **收尾验证**：全量模块单测 x64 绿 + 编译 0 错。

> 转 writing-plans 逐项细化为 TDD 步骤（红→绿→重构），每步独立可验证。
