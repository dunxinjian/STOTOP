# CardFlow 阶段3a：死成员清理 + ReadFieldKeys bug 修复（设计稿）

> 日期：2026-06-16　状态：设计已确认，待 writing-plans 细化
> 上游：阶段3 拆分路线图 [`2026-06-16-cardflow-阶段3-收敛重复-路线图.md`](2026-06-16-cardflow-阶段3-收敛重复-路线图.md) 的子项 3a + 3b（风险最低的第一批）。
> 工作区：独立 worktree `cardflow-phase3a`（基于含阶段1+2 的 master，基线 98 passed / 1 skipped）。本文为方案文档，**不修改代码**；现状已在该 worktree 核实，带真实 file:line。

---

## 0. 目标 / 非目标

**目标**：以最低风险开启阶段3——清掉 3 个零调用死成员（3a），修一个会导致"审批节点字段权限全丢"的真 bug（3b）。不碰正在跑的 `FlowEngineService`。

**非目标**：不做阶段3 的任何"碰引擎"收敛（3c–3g 各自后续专项）；不改业务行为（3a 是纯删，3b 是修正缺陷使其符合既有意图）。

---

## 1. 子项 3a：死成员纯删（零风险）

每删一项**先 grep 复核零调用**（沿用阶段1 纪律），删后编译 + 既有 98 单测仍绿。

| 死成员 | 位置 | 证据（worktree 内核实） | 删除范围 |
|---|---|---|---|
| `MatchFlowDefinitionAsync` | `Services/BatchTriggerService.cs` | 接口声明 `IBatchTriggerService`:65 + 实现 :616（`[Obsolete]` 一行转发 `MatchFlowDefinitionsAsync`）；**无调用方** | 删接口成员 + 实现方法 |
| `ValidateRowBuiltin` | `AutoPlugin/Implementations/QualityAnalysisPlugin.cs:365` | `private static` 死方法；**无调用方** | 删整方法 |
| `ITransformEngine.Preview` | `Services/Import/TransformEngine/ITransformEngine.cs:11` + `JintTransformEngine.cs:49` | 接口方法 + 实现；**无调用方**（grep 到的其它 `Preview` 均为无关 HTTP 端点/别的服务） | 删接口成员 + 实现方法 |

> 风险：零。若某 grep 出现意外调用方 → 停止、保留、上报（同阶段1）。

---

## 2. 子项 3b：修 `ReadFieldKeys` 处理 Object 形态（真 bug）

### 缺陷
`StageViewProfileResolver.ReadFieldKeys`（`Services/StageViewProfileResolver.cs:245`）：
```csharp
using var document = JsonDocument.Parse(schemaJson);
if (document.RootElement.ValueKind != JsonValueKind.Array)
{
    return new List<string>();   // ← Object 形态直接返回空
}
```
当卡片 schema 存为 Object 形态 `{"version":2,"fields":[...]}` 时返回空字段列表 → 该节点 `fieldAccess` 全空 → **审批节点字段权限全部丢失**。
而 `CardPresentationResolver.ReadCardSchema`（`Services/CardPresentationResolver.cs:409`）对 Object 形态会 `Deserialize<CardSchemaV2>` 取 `.Fields`，两者口径漂移。

### 修法（方案 A：内联补 Object 分支，已确认）
在 `ReadFieldKeys` 内，先解析出"字段数组" `JsonElement`，再跑现有的"逐元素取 `key`"循环：
- root 为 `Array` → 用 root（现有行为不变）。
- root 为 `Object` 且含 `fields` 属性且该属性为 `Array` → 用 `fields` 属性。
- 其余 → 返回空（不变）。

保留 ReadFieldKeys 现有的裸 `JsonDocument` 风格，**零外部耦合**（不引入 CardSchemaV2/JsonOptions）。

> **计划阶段须核实**：Object 形态里字段数组的属性名。`CardSchemaV2.Fields`（`Models/Schema/CardSchemaV2Models.cs`）的 `JsonPropertyName` 预期为 `"fields"`（与 bug 示例一致）；写代码前读该模型确认，若不是 "fields" 以模型实际值为准。

### 不采用方案 B 的理由
方案 B（改用 `Deserialize<CardSchemaV2>` 复用 ReadCardSchema 同款）更 DRY，但把 CardSchemaV2/JsonOptions 耦合进 StageViewProfileResolver、改动面更大，不符合"安全第一批"的最小化定位。

### 测试
在 `tests/STOTOP.Module.CardFlow.Tests/Approval/StageViewProfileResolverTests.cs`（已存在）新增/补充用例：
- **Array 形态**：schema 为 `[{"key":"amount",...},{"key":"category",...}]` → 解析出 fieldKeys 含 amount/category（回归保护，原本就工作）。
- **Object 形态**：schema 为 `{"version":2,"fields":[{"key":"amount",...}]}` → 修复后应解析出 fieldKeys 含 amount（修复前会是空 → 证明 bug 已修）。
- 断言点：通过 `StageViewProfileResolver.Resolve` 产出的 `fieldAccess` 非空、含预期字段；或直接对 `ReadFieldKeys` 行为做最小可测断言（若该方法不可直接测，则经 `Resolve` 的公共出口断言 fieldAccess）。

> 测试可达性在计划阶段确认：`ReadFieldKeys` 是 `private static`，应经 `Resolve`（公共方法）的 `fieldAccess` 输出间接断言；若构造 `Resolve` 的输入成本过高，则评估将该解析逻辑提取为可测的 internal 静态方法（仅在确有必要时，最小改动）。

---

## 3. 执行顺序、验证、回滚

**顺序**：3a 三个删除各自独立 commit（零风险先清场）→ 3b 修复 + 测试一个 commit。

**验证**：
- 每个 3a 删除：`dotnet build`（worktree 有独立 bin/obj，构建干净无 DLL 锁）+ `dotnet test`（仍 98 passed / 1 skipped）。
- 3b：`dotnet test` 新增两形态用例绿 + 既有全绿（通过数 = 98 + 新增）。

**回滚**：每子项独立 commit，可单独 revert；全在 worktree 分支 `cardflow-phase3a`，不影响主树。**commit 不 push。**

---

## 4. 任务分解预览（供 writing-plans）

1. 删 `MatchFlowDefinitionAsync`（接口 + 实现）。
2. 删 `ValidateRowBuiltin`。
3. 删 `ITransformEngine.Preview`（接口 + 实现）。
4. 修 `ReadFieldKeys` 补 Object 分支（先核实 `fields` 属性名）+ 新增 Array/Object 两形态测试。
5. 收尾验证（全量 dotnet test + dotnet build）。

> 完成后经作者复核，回到阶段3 路线图选下一个子项（3c 撤销双份 / 3d 处理器工厂 / 3e 前端冻结 / 3f 字段模型 / 3g 条件 evaluator）。
