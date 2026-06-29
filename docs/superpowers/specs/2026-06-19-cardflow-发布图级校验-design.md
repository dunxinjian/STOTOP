# CardFlow 发布图级校验（设计稿）

> 日期：2026-06-19　状态：设计已确认（无产品分叉，纯防御性校验）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）③ 节点路由，子项 **#8（发布校验不查环/不可达/终端，非法环流程可发布上线；同仓 Orchestration 却查环，口径不对称）**，即原计划拆出的 **③-sub-2**。
> 同批前序已完成：脱敏链、①v2、②组织隔离三连、③ 空条件 catch-all（sub-1）、③#4 TypeError 选边、③ 预演≠运行时收敛、③ 退回拓扑收敛。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-19 复核）。

---

## 0. 背景与缺陷（已核实）

发布校验 `FlowDefinitionService.ValidateRouteRulesAsync`（[:733-768](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:733)，由 `PublishAsync` [:225](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:225) 调用）只做**节点级**校验：来源节点存在、单默认分支、优先级不重、目标节点存在、默认分支无条件、非默认必有条件（sub-1 加的对偶）。**无任何图级校验**：

- 可发布含 **A→B→A 环**的 active 路由图。auto 节点环 → 运行时 `StageRouteResolver` 自动反复 re-route（每步写一条 `CfRouteDecisionSnapshot`、状态机错乱、生命周期无法终止）。运行时 `StageRouteResolver` 也无防环。
- 可发布**从起点不可达**的节点（死配置）。

### 已核实的关键事实
- **同仓已有 Kahn 拓扑、口径不对称**：`OrchestrationEngineService.ValidateDag`（[:627-665](../../../src/STOTOP.Module.CardFlow/Services/OrchestrationEngineService.cs:627)）对编排模板做 Kahn 拓扑、**阻断所有环**，其 `ValidateForPublishAsync`（[:670](../../../src/STOTOP.Module.CardFlow/Services/OrchestrationEngineService.cs:670)）发布期调用。审计明确要 CardFlow 路由发布与之**对称**。`ValidateDag` 入参是编排 JSON（节点/边模型），CardFlow 路由图是 `CfStageDefinition` 节点 + `CfStageRouteRule` 边，**复用算法不复用方法**。
- **无终端标记**：`CfStageDefinition`（[entity](../../../src/STOTOP.Module.CardFlow/Entities/CfStageDefinition.cs)）只有 `FType`（human/auto），**无 end/terminal 字段**。运行时把任何「无出边」叶子当「流程结束」（`StageRouteResolver`：无出边 → CompleteCard）。故审计检查②「非终端节点必须有出边或显式标记终端」**无法干净实现**（分不清「故意结束」vs「忘连边」，加标记 = 范围溢出）→ **本子项放弃终端检查**；DAG 必有汇点，「流程可终止」由无环天然保证。
- **rule-mode 门槛**：运行时 `StageRouteResolver` 只要版本有任意 active 规则即整版进入 rule-mode；线性流（0 规则）按 sortOrder。故图级校验**仅当存在 active 规则时**才有意义，线性流跳过。
- **入口节点**：`PublishAsync` 传入的 `stages` 已按 `FSortOrder` 排序（[:223](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:223)）；入口 = `stages.First()` 的 `FStageKey`（与预演 `stages.First()` 口径一致）。
- **合法「环」不在静态路由图**：退回（`ReturnToStageRuntime`）是运行时机制、**非 `CfStageRouteRule` 边**；退回造成的「路径回环」是运行时路径、不在静态路由图里。故静态 active 路由图本就应是 DAG，阻断所有环不误伤合法退回。动态插入节点是运行时 `CfStageInstance`、非 `CfStageDefinition`，不入静态图。
- **校验只在发布**：克隆走 `SaveRouteRulesAsync`（不调 `ValidateRouteRulesAsync`，见审计 #14），故图级校验只在发布期生效，scope 与现状一致。
- 校验失败靠 `throw InvalidOperationException`，`PublishAsync` 上层捕获转错误返回（**throw-only 通道，无告警通道**）。

---

## 1. 目标 / 非目标

### 目标
1. 抽纯函数 `RouteGraphValidator`：对 active 路由图做 **Kahn 环检测（阻断所有环）** + **从入口 BFS 可达性检测**，返回错误列表（列出涉事节点）。
2. `ValidateRouteRulesAsync` 在现有校验后、仅 rule-mode 时调它，有错即 `throw InvalidOperationException`（阻断发布），与同仓 Orchestration `ValidateDag` 口径对称。
3. 单测钉死：纯校验器各分支 + 发布期含环流程被拦。

### 非目标（明确不做）
- ❌ **终端检查**（`CfStageDefinition` 无 end 标记，分不清故意结束 vs 忘连边；DAG 已保证可终止）。
- ❌ **告警通道**：发布 throw-only，本轮图级问题一律硬阻断，不引入「警告 vs 错误」返回结构改造。
- ❌ 不动运行时 `StageRouteResolver`（运行时防环属另事，本轮只堵发布口）。
- ❌ 不在克隆期校验（克隆链是 #14，另项）。
- ❌ 不碰组织链/角色路由、sub-1 空条件、#4 TypeError、退回拓扑（各自已修或另项）。
- ❌ 不改 `CfStageDefinition`/`CfStageRouteRule` 结构、无迁移。

---

## 2. 设计

### 2.1 纯函数 `RouteGraphValidator`（交付物 1）

新建 `src/STOTOP.Module.CardFlow/Services/RouteGraphValidator.cs`，**无 DbContext、纯函数**：

```csharp
namespace STOTOP.Module.CardFlow.Services;

public static class RouteGraphValidator
{
    /// <summary>
    /// nodeKeys：全部 stage key（去空去重）。entryKey：首 sortOrder 节点 key。
    /// edges：active 路由规则 (from, to)。返回错误列表（空 = 通过）。
    /// 先环检测（阻断所有环）；无环（DAG）才查可达性（环图可达性无良定义）。
    /// 所有 key 比较大小写不敏感。
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyCollection<string> nodeKeys,
        string? entryKey,
        IReadOnlyCollection<(string From, string To)> edges)
    {
        var nodes = nodeKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (nodes.Count == 0)
            return Array.Empty<string>();

        // 只保留两端都在 nodes 里的边（来源/目标存在性由 ValidateRouteRulesAsync 既有校验先保证；
        // 这里防御性过滤，避免悬空边干扰拓扑）
        var validEdges = edges
            .Where(e => nodes.Contains(e.From) && nodes.Contains(e.To))
            .ToList();

        var errors = new List<string>();

        // ① Kahn 环检测（阻断所有环）
        var inDegree = nodes.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adj = nodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (from, to) in validEdges)
        {
            inDegree[to] += 1;
            adj[from].Add(to);
        }
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = 0;
        var localIn = new Dictionary<string, int>(inDegree, StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            visited++;
            foreach (var next in adj[n])
            {
                localIn[next] -= 1;
                if (localIn[next] == 0) queue.Enqueue(next);
            }
        }
        if (visited < nodes.Count)
        {
            var cyclic = localIn.Where(kv => kv.Value > 0).Select(kv => kv.Key)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            errors.Add($"流程路由存在环，涉及节点：{string.Join("、", cyclic)}");
            return errors;   // 有环：先报环，不再查可达性
        }

        // ② 从入口 BFS 可达性（DAG）
        if (!string.IsNullOrWhiteSpace(entryKey) && nodes.Contains(entryKey))
        {
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryKey };
            var bfs = new Queue<string>();
            bfs.Enqueue(entryKey);
            while (bfs.Count > 0)
            {
                foreach (var next in adj[bfs.Dequeue()])
                    if (reachable.Add(next)) bfs.Enqueue(next);
            }
            var unreachable = nodes.Where(n => !reachable.Contains(n))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            if (unreachable.Count > 0)
                errors.Add($"流程存在从起点不可达的节点：{string.Join("、", unreachable)}");
        }

        return errors;
    }
}
```

> `localIn` 是 `inDegree` 的副本供 Kahn 消耗，保留 `inDegree` 不必要——实现里直接用一份消耗即可；上方为表意清晰分开写，计划阶段可合并为一份。环节点取「Kahn 结束后仍 inDegree>0」者（即环及其下游）。

### 2.2 `ValidateRouteRulesAsync` 接线（交付物 2）

`ValidateRouteRulesAsync`（[:733](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:733)）在现有 `foreach (var group in rules...)` 校验**之后**追加：

```csharp
        if (rules.Count > 0)
        {
            var nodeKeys = stages
                .Select(s => s.FStageKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();
            var entryKey = stages.FirstOrDefault()?.FStageKey;
            var edges = rules
                .Select(r => (From: r.FFromStageKey, To: r.FToStageKey))
                .ToList();

            var graphErrors = RouteGraphValidator.Validate(nodeKeys, entryKey, edges);
            if (graphErrors.Count > 0)
                throw new InvalidOperationException(string.Join("；", graphErrors));
        }
```

- `rules` 是本方法已加载的 active 规则（[:739-741](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:739)）。`rules.Count == 0`（线性流）→ 跳过。
- 既有来源/目标存在性校验已在前面跑过，故进到图级校验时边两端节点基本都存在；`RouteGraphValidator` 内再防御性过滤悬空边。

---

## 3. 兼容性与风险

- **只在发布期、只在 rule-mode**：线性流、克隆、运行时全不受影响。存量已发布流程不被重新校验（除非再次发布），不会突然失效。
- **行为变更**：含环 / 有不可达节点的流程**发布被拒**（之前可发）——这正是要堵的非法配置，是修正方向。
- **不误伤合法退回**：退回不是路由规则边，静态 active 路由图本就应是 DAG；动态插入节点不在静态图。
- **口径对称**：与 `OrchestrationEngineService.ValidateDag` 一致（都阻断所有环），消除审计指出的不对称。
- **纯函数**：`RouteGraphValidator` 无 DB、无 I/O，易穷举单测；Kahn/BFS 均有界、无死循环风险。
- 风险整体低：一处纯新增 + 一处发布校验追加 throw，改动面集中，无 schema/迁移。

---

## 4. 测试（TDD，x64 必带）

### 4.1 `RouteGraphValidatorTests`（纯，无 DbContext）
新增 `tests/STOTOP.Module.CardFlow.Tests/Rules/RouteGraphValidatorTests.cs`：
- 合法 DAG 链（A→B→C，entry A）→ 无错。
- 环 A→B→A（entry A）→ 一条错含「环」且列 A、B。
- 自环 A→A → 环错列 A。
- 不可达：nodes {A,B,C}、entry A、边 A→B、C 孤立 → 一条错含「不可达」且列 C。
- 单节点无出边（nodes {A}、entry A、无边）→ 无错。
- 有环 + 另有孤立节点 → **只报环**（不报不可达，验证「先报环」短路）。
- entry 为空/不在 nodes → 不抛、不报可达（防御）。

### 4.2 发布集成（`PublishAsync` 间接测 `ValidateRouteRulesAsync`）
- 种 rule-mode 流程（≥1 active 规则）含环 A→B→A → `PublishAsync` 抛 `InvalidOperationException`，消息含「环」。
- 种含不可达节点的 rule-mode 流程 → 抛，消息含「不可达」。
- 正常 DAG rule-mode 流程 → 不因图级校验抛（既有发布路径其余校验照常）。
- 线性流（0 规则）→ 图级校验不参与（回归既有行为）。

> 计划阶段确认现有发布测试基建（`FlowDefinitionService` 的 DbContext 构造样板 / 是否已有 publish 测试可照搬）；若构造成本高，至少纯校验器层（4.1）穷举覆盖 + 发布层补 1-2 条关键 case。

### 4.3 回归
- `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64` 全绿（重点 `FlowDefinition*`/`ConditionRuleEmptyGroup` 等既有路由/发布测试不回归）。
- `STOTOP.Module.CardFlow` 编译 0 错。

---

## 5. 任务分解预览（供 writing-plans）
1. **纯函数 `RouteGraphValidator`** + `RouteGraphValidatorTests`（7 case，TDD 先红后绿）。
2. **`ValidateRouteRulesAsync` 接线**（rule-mode 时调校验器、throw）+ 发布集成测试（含环/不可达被拦、正常 DAG/线性流不误拦）。
3. **收尾验证**：全量模块单测 x64 + 编译 0 错。

> 转 writing-plans 逐项细化为 TDD 步骤（红→绿→重构），每步独立可验证。
