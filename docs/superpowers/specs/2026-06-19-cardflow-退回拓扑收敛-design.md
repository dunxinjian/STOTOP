# CardFlow 退回拓扑收敛（设计稿）

> 日期：2026-06-19　状态：设计已确认（无产品分叉，纯正确性收敛）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）③ 节点路由，子项 **#6（退回目标/作废下游纯按 FSortOrder，与运行时按路由图选路是两套拓扑）**。
> 同批前序已完成：脱敏链、①v2、②组织隔离三连、③ 空条件 catch-all、③#4 TypeError 选边、③ 预演≠运行时收敛。本子项是「第2批·单一真源收敛」里「退回与正向共用拓扑」一条。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-19 复核）。

---

## 0. 背景与缺陷（已核实）

退回相关逻辑全部以 `FSortOrder` 为唯一拓扑依据，但 rule-mode 正向流转 `StageRouteResolver` 按路由图（`FToStageKey`/`FToStageDefinitionId`）跳转，可跳过中间 sortOrder 节点、可让小 sortOrder 节点排在大 sortOrder 之后。两套口径在条件路由非线性时必然背离。

| 位置 | 现状（按 sortOrder） | 非线性下的错 |
|---|---|---|
| `ResolvePreviousTarget`（[ReturnToStageRuntime.cs:7-39](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:7)） | 取 `FSortOrder < current` 中**最大 sortOrder** 的已完成人工节点 | 真实前驱若 sortOrder 比 current **大**（被路由图跳到后面），会被漏掉，退回到错误的更早节点 |
| `ResolveSpecifiedTarget`（[:41-75](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:41)） | 校验 `target.FSortOrder >= current.FSortOrder → 失败` | 真实路径上居 current 之前、但 sortOrder 更大的合法目标被误拒；sortOrder 更小但不在真实路径上的被误受 |
| `SupersedeDownstreamCompletedStages`（[:77-107](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:77)） | 作废 `FSortOrder > target` 的已完成人工实例 | 作废集与真实下游不符：漏作废（实际下游但 sortOrder 小）、误作废（sortOrder 大但不在本段路径上） |

**真实路径已持久化但未被利用**：`CfRouteDecisionSnapshot`（[entity](../../../src/STOTOP.Module.CardFlow/Entities/CfRouteDecisionSnapshot.cs)）每步路由写一条边 `FFromStageDefinitionId → FToStageDefinitionId`（+ `FRound`/`FDecisionTime`），由 [FlowEngineService.WriteRouteDecisionSnapshotAsync:1847-1869](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1847) 写入。退回逻辑从不读它。

### 已核实的关键事实
- **快照仅 rule-mode 写**：[AdvanceToNextStageCoreAsync:1639](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1639) `if (routeResult.RuleMode)` 分支内才 `WriteRouteDecisionSnapshotAsync`；线性兜底分支（:1652+）不写。故**线性流/遗留卡无快照**，sortOrder 恰是其真实路径 → 必须保留 sortOrder 回退。
- **退回在同一轮内**：`ReturnToStageAsync`（[:803-887](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:803)）退回到中途人工节点时**不增轮次**（`targetInstance.FRound = card.FCurrentRound`，:881），卡片在同轮从 target 重新前进。故**同一轮内路径可重走**，且重走可能因卡片数据变化走**不同分支**。整轮重启（resubmit，[:1013](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1013) `FCurrentRound += 1`，从首节点开始）是另一条流程，不在本子项范围。
- **终态快照 To 为空**：流程结束时 `FToStageDefinitionId = routeResult.NextStage?.FID` 为 null（[:1861](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1861)），非真实入边，重建时跳过。
- 调用点：`ReturnToStageAsync` 已加载 `stages`（按 sortOrder）+ `stageInstances`（本卡全部），分别调三方法（[:818-868](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:818)）。
- 现有纯单测 [ReturnToStageRuntimeTests](../../../tests/STOTOP.Module.CardFlow.Tests/Approval/ReturnToStageRuntimeTests.cs) 用内存列表直驱三方法。

---

## 1. 目标 / 非目标

### 目标
1. 抽共享纯函数 `RoutePathReconstructor`：从 `CfRouteDecisionSnapshot` 边重建本轮**真实执行路径**，用「每个节点的最新入边」回溯（正确处理同轮退回后重走/改分支），带防环。
2. `ReturnToStageRuntime` 三方法收敛到真实路径口径，与正向 `StageRouteResolver` 路由图同源；**无快照时回退现有 sortOrder 逻辑**（线性/遗留卡行为不变）。
3. 退回目标、指定退回校验、作废下游三者口径一致。
4. 单测钉死：纯重建器 + 三方法非线性 case；现有测试传空快照走回退保持绿。

### 非目标（明确不做）
- ❌ 不碰发布图级校验（环/可达/终端，sub-2，需 human-vs-auto-cycle 设计）。
- ❌ 不碰组织链/角色路由（真产品分叉）。
- ❌ 不动正向 `StageRouteResolver`、快照写入逻辑、`CfRouteDecisionSnapshot` 结构。
- ❌ 不改 round 语义，不动 resubmit 整轮重启路径（[:995-1015](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:995)）。
- ❌ 不为退回新增 schema 列 / 迁移（复用已持久化快照，方案 A）。

---

## 2. 设计

### 2.1 共享纯函数 `RoutePathReconstructor`（交付物 1）

新建 `src/STOTOP.Module.CardFlow/Services/RoutePathReconstructor.cs`，**无 DbContext、纯函数**：

```csharp
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

public static class RoutePathReconstructor
{
    /// 入参 snapshots 已按 card + round 过滤。
    /// 返回从起点到 current 的有序 def id 链（含 current 末尾）。
    /// 沿「每个节点的最新入边」回溯；current 无入边 → 仅返回 [current]（调用方据此回退）。
    public static IReadOnlyList<long> Reconstruct(
        IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots,
        long currentStageDefinitionId)
    {
        // 每个 To 节点取最新一条入边（FDecisionTime 降序、FID 兜底）→ From
        var latestInto = snapshots
            .Where(s => s.FToStageDefinitionId.HasValue && s.FFromStageDefinitionId.HasValue)
            .GroupBy(s => s.FToStageDefinitionId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.FDecisionTime).ThenByDescending(s => s.FID)
                      .First().FFromStageDefinitionId!.Value);

        var reversed = new List<long> { currentStageDefinitionId };
        var visited = new HashSet<long> { currentStageDefinitionId };
        var node = currentStageDefinitionId;
        while (latestInto.TryGetValue(node, out var from) && visited.Add(from))
        {
            reversed.Add(from);
            node = from;
        }
        reversed.Reverse();
        return reversed;   // 起点 … current
    }
}
```

- `visited.Add(from)` 同时承担「未访问过」判定与去重：遇环（#8 auto 环属另项）即停，不死循环。
- 「最新入边」使同轮退回后重走/改分支时拿到**当前实际前驱**：例 `manager→gm→finance`，退回 manager 后改走 `manager→director→finance`，从 finance 回溯取最新入边得 `…→director→finance`，而非首次的 gm。

### 2.2 `ReturnToStageRuntime` 三方法收敛（交付物 2）

三方法签名各加末参 `IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots`。各方法先 `var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId)`；**`path.Count >= 2`（即 current 有真实前驱）→ 真实路径口径，否则回退现有 sortOrder 逻辑**。

下文 `stageById`、`IsHuman`、completed/非动态/本轮 等沿用现有私有辅助。

**(a) `ResolvePreviousTarget`** —— 真实路径分支：
- 从 `path` 末尾（current）向前找**最近的人工节点**：遍历 `path[^2]、path[^3]…`，取首个 `IsHuman(def)` 且在 `stageInstances` 中有「本轮 completed 非动态」实例的 def。
- 命中 → `Ok(def, 该实例)`（实例取该 def 本轮 completed 非动态、最大 FID）；遍历完无 → `Fail("未找到可退回的上一人工节点")`。
- 无路径 → 现 sortOrder 逻辑（[:18-38](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:18)）。

**(b) `ResolveSpecifiedTarget`** —— 保留现有「target 存在 + 是人工 + 本轮已完成」校验；把 `target.FSortOrder >= current.FSortOrder → Fail`（[:60](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:60)）换为：
- 真实路径分支：`target.FID` 须出现在 `path` 中、且**索引 < current 索引**（current 是 path 末元素）；否则 `Fail("退回目标不在当前节点的实际路径上")`。
- 无路径 → 现 sortOrder 校验。

**(c) `SupersedeDownstreamCompletedStages`** —— 真实路径分支：
- `target` 在 `path` 中的索引之后那段 `path[targetIdx+1 .. ^1]` 为**下游 def 集**（current 末元素含在内无害：current 调用时状态已置 `returned` 非 `completed`，不入作废集）。
- 作废 = `stageInstances` 中「本轮 completed 非动态人工」且其 def ∈ 下游 def 集者，置 `cancelled`/`return-superseded`/`FCompletedTime`（同现行 [:99-104](../../../src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:99)）。
- `target` 不在路径 → 回退现 sortOrder>target 逻辑。

> 语义说明：作废集 = **当前真实路径上 target 之后**的已完成人工节点。早前一次重走遗留的已完成实例，若已被上一次退回作废→状态 `cancelled` 不入集；若不在当前路径上→不作废（比 sortOrder 口径更精准，sortOrder 会误作废 sortOrder 大却不在本段的节点）。

### 2.3 接线（交付物 3）

`FlowEngineService.ReturnToStageAsync`（[:803](../../../src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:803)）在已有 `stages`/`stageInstances` 加载后，加载本轮快照并传入三处调用：
```csharp
var routeSnapshots = await _dbContext.Set<CfRouteDecisionSnapshot>()
    .Where(s => s.FCardId == card.FID && s.FRound == card.FCurrentRound)
    .ToListAsync();
```
`ResolveSpecifiedTarget`/`ResolvePreviousTarget`/`SupersedeDownstreamCompletedStages` 三调用末尾各加 `routeSnapshots`。无其它改动。

---

## 3. 兼容性与风险

- **回退保证向后兼容**：线性流、遗留卡、rule-mode 但当前节点无入边快照 → `path.Count < 2` → 走原 sortOrder 逻辑，行为逐字不变。现有 3 个纯单测传空快照即落此路径，应原样绿（同时证「回退==旧行为」）。
- **行为变更仅限 rule-mode 非线性 + 有快照**：此时由「按 sortOrder 误选」改为「按真实路径正确选」，属修正。
- **作废集口径收紧**：path-based 比 sortOrder 更精准；理论上若存在「真实下游但 sortOrder 小」的节点，旧逻辑会漏作废、新逻辑会正确作废（修正）；反向「sortOrder 大却不在本段」旧误作废、新不作废（修正）。均为正确方向。
- **防环**：`visited` 集确保 auto 环（#8，另项）下不死循环；本子项不解决环本身，只保证退回重建不挂。
- **无 schema/迁移**，无落库结构改动；纯新增一个静态类 + 三方法加参 + 一处查询接线。
- 风险整体中低：核心是纯函数（易测），有 sortOrder 回退兜底，改动面集中。

---

## 4. 测试（TDD，x64 必带）

### 4.1 `RoutePathReconstructorTests`（纯，无 DbContext）
新增 `tests/STOTOP.Module.CardFlow.Tests/Approval/RoutePathReconstructorTests.cs`：
- 线性单链 A→B→C：从 C 重建得 `[A,B,C]`。
- 非线性（跳 sortOrder）：边 manager(1)→gm(3)、gm(3)→finance(2)，从 finance 得 `[manager,gm,finance]`（证按边非按 sortOrder）。
- 同轮退回后改分支：边序 manager→gm、gm→finance、（退回后）manager→director、director→finance，从 finance 取最新入边得 `[manager,director,finance]`（不含 gm）。
- 环：A→B、B→A，从 A 重建不死循环、有界返回。
- current 无入边：空快照 / 无指向 current 的边 → 返回 `[current]`（Count==1）。

### 4.2 `ReturnToStageRuntimeTests` 扩充（纯）
- **现有 3 测试**：调用处补传 `Array.Empty<CfRouteDecisionSnapshot>()` → 走 sortOrder 回退 → 断言不变（绿）。
- 新增非线性 case（构造 stages + instances + snapshots）：
  - `ResolvePreviousTarget`：manager(1)→gm(3)→finance(2) 路径，current=finance，断言目标 = **gm**（FID）、VisitedInstance = gm 本轮 completed 实例（非 sortOrder 取的 manager）。
  - `ResolveSpecifiedTarget`：同路径，指定 target=gm（sortOrder 3 > finance 2）→ 真实路径上居前 → **成功**（旧 sortOrder 校验会误拒）；指定一个不在路径上的节点 → `Fail("不在当前节点的实际路径上")`。
  - `SupersedeDownstreamCompletedStages`：退回 manager，作废集 = 路径上 manager 之后的 completed 人工（gm），断言对齐路径段而非 sortOrder。

### 4.3 回归
- `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64` 全绿（重点 `ReturnToStageRuntimeTests`/`FlowEngineReturnToStageTests` 等既有退回测试不回归）。
- `STOTOP.Module.CardFlow` 编译 0 错。

> 注：`FlowEngineService.ReturnToStageAsync` 接线为薄改动（加一处查询 + 传参），其端到端正确性由现有 `FlowEngineReturnToStageTests` 守护（线性场景走回退、行为不变）；rule-mode 非线性的真实路径正确性由 4.1/4.2 纯单测钉死。若现有引擎测试基建可低成本加一条 rule-mode 非线性退回集成测试，则在计划阶段评估补充，不强行引入重型集成。

---

## 5. 任务分解预览（供 writing-plans）
1. **纯函数 `RoutePathReconstructor`** + `RoutePathReconstructorTests`（5 case，TDD 先红后绿）。
2. **`ReturnToStageRuntime` 三方法加参 + 真实路径口径 + sortOrder 回退**；现有 3 测试补传空快照（保持绿）+ 三方法非线性 case（红→绿）。
3. **`FlowEngineService.ReturnToStageAsync` 接线**：加载本轮快照、三处传参；现有 `FlowEngineReturnToStageTests` 回归绿。
4. **收尾验证**：全量模块单测 x64 + 编译 0 错。

> 转 writing-plans 逐项细化为 TDD 步骤（红→绿→重构），每步独立可验证。
