# CardFlow 路由空条件 catch-all 修复（设计稿）

> 日期：2026-06-19　状态：设计已确认（无产品分叉，纯 bug 修复）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）③ 节点路由，子项 sub-1。本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-19 复核）。

---

## 0. 背景与缺陷（已核实）

非默认路由规则若条件为空，运行时被当成「命中」，成为最高优先级隐形 catch-all，截胡所有真实条件分支与默认分支：

| 编号 | 缺陷 | 证据 |
|---|---|---|
| #2 | 非默认规则 `FConditionJson` 为空/null（前端删光条件即回写 `conditionJson:null`），运行时 `ConditionRuleEvaluator.Evaluate` 开头 `IsNullOrWhiteSpace → Match("空条件默认匹配")`；`StageRouteResolver` 非默认规则迭代里立即 `Matched && TypeErrors==0` 选中 break → 该规则按 FPriority/FID 成为无条件 catch-all。 | [ConditionRuleEvaluator.cs:13-14](../../../src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:13)、[StageRouteResolver.cs:65-87](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:65) |
| #3 | 空条件组 `{"logic":"and","conditions":[]}`（前端 addNestedGroup 留空，或顶层非空但内含空组），`EvaluateGroup` `childResults.Count==0 → Match("空条件组默认匹配")`；`or` 下空组令整组恒真。 | [ConditionRuleEvaluator.cs:73-74](../../../src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:73) |
| #2b | 发布校验 `ValidateRouteRulesAsync` 有「默认分支不能带条件」却**无对偶**「非默认分支必须带条件」——空条件非默认规则可顺利发布上线。 | [FlowDefinitionService.cs:758-764](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:758) |

**已核实的关键事实：**
- `StageRouteResolver` 默认分支由 `FIsDefault` 单独处理（[:91](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:91)），条件迭代只跑 `!rule.FIsDefault`（[:65](../../../src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:65)）。故「非默认规则空条件不命中」不影响默认分支兜底。
- `Evaluate` 顶层空 `conditionJson`（[:13](../../../src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:13)）是**所有消费者共享**入口（路由 / `DynamicStagePolicyResolver` / `ConditionEvaluator`(DSL) / `CardFlowPathPreviewService`）。对非路由消费者，「无条件即适用」可能是合法语义，**故不改 line 13**；路由侧用 route-scoped 守卫覆盖。
- 空条件**组**（line 74）算命中在任何场景都是配置错误，改 `Matched=false` 是安全方向，可在共享求值器层改。

---

## 1. 目标 / 非目标

### 目标
1. 非默认路由规则空条件**永不命中**（运行时不再成 catch-all）。
2. 空条件组 `conditions:[]` **永不命中**（含 `or` 下的嵌套空组）。
3. 发布期拦住「非默认分支空条件」，给作者反馈（防御纵深）。

### 非目标（明确不做）
- ❌ 不动 `Evaluate` 顶层空 `conditionJson`（line 13）的共享语义（非路由消费者可能依赖）。
- ❌ 不做发布图级校验（环/可达/终端）——合法 return 循环存在，需 human-vs-auto-cycle 专门设计，拆 ③-sub-2。
- ❌ 不动 TypeError 毒化路由边（#4）、退回拓扑、组织链路由——各自后续子项。
- ❌ 不改前端 ConditionBuilder（前端"空条件不写规则"是另一回事；本轮只让后端对空条件 fail-closed）。

---

## 2. 设计

### 2.1 路由层守卫（#2，route-scoped）
`StageRouteResolver.ResolveNextStageAsync` 的非默认规则迭代（:65-87）：进入 `Evaluate` 前先判 `string.IsNullOrWhiteSpace(rule.FConditionJson)`：
- 为空 → 记一条 candidate `Matched=false`、`Explanation="非默认分支缺条件，不命中"`、`TypeErrors=["非默认分支未配置条件"]`，`continue`（不调 Evaluate，不选中）。
- 非空 → 走现有 `Evaluate` 路径不变。
这样空条件非默认规则永不命中，卡片落到后续条件规则或默认分支（既有兜底逻辑不变）。

### 2.2 求值器空条件组（#3，共享求值器）
`ConditionRuleEvaluator.EvaluateGroup`（:73-74）：`if (childResults.Count == 0)` 由 `return Match("空条件组默认匹配")` 改为：
```csharp
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                Explanation = "空条件组，不匹配"
            };
```
嵌套空组随之在父组按 and/or 正常参与（`and` 下使整组 false，`or` 下不再贡献 true）。

### 2.3 发布校验对偶（#2b）
`ValidateRouteRulesAsync` 的 `foreach (var rule in group)`（:758-764）在既有「默认分支不能带条件」旁加：
```csharp
                if (!rule.FIsDefault && string.IsNullOrWhiteSpace(rule.FConditionJson))
                    throw new InvalidOperationException($"非默认分支必须配置条件：{rule.FEdgeKey}");
```

---

## 3. 兼容性与风险

- **2.1 route-scoped**：只影响路由非默认规则；默认分支、其它 Evaluate 消费者不受影响。
- **2.2 共享求值器**：空条件组算命中是配置错误，改 false 是 fail-closed 安全方向；理论上若某存量配置故意用空组当"恒真占位"会行为改变——属修正错误用法，概率极低。`Evaluate` 顶层空 conditionJson（line 13）不动，非路由消费者语义不变。
- **2.3 发布校验**：新增 throw 只在「发布带空条件非默认规则」时触发——这本就是要拦的非法配置；存量已发布流程不会被重新校验（除非再次发布），不会突然失效。
- 风险整体低：三处都是把"静默错路由"改为"fail-closed/发布期拦截"。

---

## 4. 测试

新增 `tests/STOTOP.Module.CardFlow.Tests/Rules/RouteEmptyConditionTests.cs`（求值器层，纯函数，无 DbContext）：
- 空条件组 `{"logic":"and","conditions":[]}` → `Matched=false`。
- `or` 下含一个真子项 + 一个空组 → 真子项决定（空组不贡献 true，结果取决于真子项）。
- 正常条件组（含真实条件）→ 行为不变（回归）。

`StageRouteResolver` 层（需 DbContext + 真实规则）的「非默认空条件不命中、落默认分支」用集成测试或现有 `StageRouteResolverTests` 追加（计划阶段确认其测试基建；若构造成本高，至少在求值器层 + 发布校验层各覆盖）。

发布校验：`ValidateRouteRulesAsync` 经 `PublishAsync` 间接测——非默认空条件规则发布 → 抛 `InvalidOperationException`（计划阶段确认现有发布测试样板，照搬构造）。

回归：全量 `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64`（x64 必带）绿；模块编译 0 错。

---

## 5. 任务分解预览（供 writing-plans）
1. 求值器空条件组 → Matched=false（含单测）。
2. StageRouteResolver 非默认空条件守卫（含测试，按测试基建可达性定层）。
3. ValidateRouteRulesAsync 非默认空条件对偶校验（含测试）。
4. 收尾验证（全量单测 + 模块编译）。

> 转 writing-plans 逐项细化为 TDD 步骤。
