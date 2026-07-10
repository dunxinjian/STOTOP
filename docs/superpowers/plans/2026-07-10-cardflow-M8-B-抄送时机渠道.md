# CardFlow 设计器二期 · M8-B 抄送时机/渠道 · 设计+实施 Plan

> 承接 `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md`（拆批表 M8-B）。
> 起点 HEAD `4b7400e`（M8-A 收尾）。

## 〇、决策

1. **Scope**: 三个 timing（onEnter / onApprove / onReject）+ 应用内/钉钉 两 channel。企微/bot UI 灰置。
2. **Storage**: 复用现有 `CfStageDefinition.FCcConfigJson`（列 `F抄送配置JSON`，NVARCHAR(MAX) NULL），扩展 JSON schema。**零 seeder**。
3. 守铁律：不新增 `cc` FType；cc节点(auto+AlertNotify) 不动（那是 batch 数据告警，不在 M8-B 范畴）；不做假配置。

## 一、核实结论（2026-07-10）

| 维度 | 现状 | 判定 |
|---|---|---|
| timing: onEnter | auto cc节点隐式到达即火；人工节点的 FCcConfigJson.timing 死存储 | needs-engine-addition（人工节点路径） |
| timing: onApprove | 无钩子 | needs-engine-addition |
| timing: onReject | 无钩子 | needs-engine-addition |
| channel: 应用内 | 手动 CcAsync 创建 cc-todo 可见；auto AlertNotify 的 "system" 仅 log | needs-engine-addition（auto-cc → 真建 todo） |
| channel: 钉钉 | DingTalkChannel 实现 todo push；AlertNotifyHandler 实现工作通知 | can-land-now（只需对新建 cc-todo 调 DispatchCreateTodoAsync） |
| channel: 企微/bot | 无实现 | must-ui-placeholder |
| recipients | FCcConfigJson.users 死存储，引擎不读 | needs-engine-addition |

关键缺口：引擎 stage 生命周期无 "approve/reject/enter → fire cc" 钩子；`CreateTodoAsync` 后不调 `DispatchCreateTodoAsync`（新 todo 不推送钉钉）。

## 二、FCcConfigJson 扩展 schema

```json
{
  "users": [{ "userId": 123, "userName": "张三" }],
  "timing": "onEnter" | "onApprove" | "onReject" | "always",
  "channels": ["system", "dingtalk"]
}
```
- `timing` 缺省 `"onEnter"`；`channels` 缺省 `["system"]`；空 `users` / null = 不触发。
- `"always"` = onEnter + onApprove + onReject 全触发。

## 三、引擎消费设计

新增 `private async Task FireStageCcAsync(CfCard card, long stageInstanceId, long stageDefinitionId, string currentTiming)`：
1. 加载 `CfStageDefinition.FCcConfigJson`（`.AsNoTracking()`，用 stageDefinitionId）
2. 解析→ `CcNotifyConfig { List<CcUser> Users, string Timing, List<string> Channels }`（静默降级：非法 JSON / null → skip）
3. timing match：`config.Timing == currentTiming || config.Timing == "always"`；不中 → return
4. users 空 → return
5. 对每个 user：
   - channels 含 `"system"` → `await _todoService.CreateTodoAsync(card.FID, stageInstanceId, user.UserId, user.UserName, card.FTitle ?? "抄送通知", "cc")`（创建应用内 cc-todo）
   - channels 含 `"dingtalk"` → 同上创建 todo（若 system 已建则复用同一 todo） + `await _notificationDispatcher.DispatchCreateTodoAsync(todoId)`

调用点（3 处）：
- **onEnter**: `AssignStageHandlersAsync` 末尾（人工节点分配处理人后）→ `await FireStageCcAsync(card, stageInstance.FID, firstStage.FID, "onEnter")`
- **onApprove**: `ApproveAsync` 内，当前节点标 completed + 推进后（约 :690）→ `await FireStageCcAsync(card, stageInstance.FID, stageInstance.FStageDefinitionId, "onApprove")`
- **onReject**: `RejectAsync` 内，普通 reject 完成后（约 :805）→ `await FireStageCcAsync(card, stageInstance.FID, stageInstance.FStageDefinitionId, "onReject")`

不动：手动 `CcAsync`（操作人手动发起的独立动作，不受 FCcConfigJson 影响）；auto cc节点（AlertNotify pipeline，不在本批）。

## 四、前端

`StageConfigPanel.vue` 恢复 cc 配置（注释 "ccConfigJson 运行时零消费故不再出输入框" 删除或改为"引擎真消费"）：
- 抄送对象：`a-select mode="multiple"`（`useUserSearch` 范式）
- 抄送时机：`a-radio-group`（进入节点 / 审批通过 / 审批驳回 / 全部）
- 通知渠道：`a-checkbox-group`（应用内 ✓ / 钉钉 ✓ / 企微 disabled / bot disabled）

`stageDefinitionShared.ts` 加 `CcNotifyConfig` TS 类型 + parse/serialize helper。

---

# Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 让引擎真消费 `FCcConfigJson`——人工节点 onEnter/onApprove/onReject 时按配置的渠道通知抄送人；恢复设计器 cc 配置面板。

**Architecture:** 后端新增 `FireStageCcAsync` helper + 3 处调用点（stage entry/approve/reject）；前端 `StageConfigPanel` 恢复 cc 配置段。零 schema（列已存在），零 seeder。

**Tech Stack:** .NET 10 / EF Core / xUnit(InMemory) ；Vue 3 / TS / Ant Design Vue / vitest。

## Global Constraints

- 不新增 `cc` FType（二元 auto/human 不变）。cc 节点(auto+AlertNotify) 不动。
- 不做假配置：引擎消费的才出 UI（企微/bot 灰置 disabled）。
- 零 seeder。`FCcConfigJson` 列已存在（`CfStageDefinition`），扩展 JSON schema 即可。
- 向后兼容：null / empty users = 不触发；缺 `channels` → `["system"]`；缺 `timing` → `"onEnter"`。
- 全局 NoTracking：读 stageDefinition 用 `.AsNoTracking()`。
- 事务：`FireStageCcAsync` 在已有事务内调用（approve/reject/enter 路径已有 ExecutionStrategy + transaction），无需自建事务。
- 前端：`type-check` + `lint:style`（零裸 hex）+ `vitest` 绿。
- 每件独立 commit，经 hook 门禁，不 push 等点头。

---

### Task 1: CcNotifyConfig 模型 + 解析器 + 单测

**Files:**
- Create: `src/STOTOP.Module.CardFlow/Models/CcNotifyConfig.cs`
- Test: `tests/STOTOP.Module.CardFlow.Tests/Rules/CcNotifyConfigTests.cs`

**Interfaces:**
- Produces: `CcNotifyConfig { List<CcUser> Users; string Timing; List<string> Channels }` + `CcUser { long UserId; string UserName }` + `static CcNotifyConfig? Parse(string? json)`（null/空/非法→null；向后兼容缺字段）+ `bool ShouldFire(string currentTiming)`

- [ ] **Step 1: 写模型 + 解析器**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Models;

public sealed class CcNotifyConfig
{
    public List<CcUser> Users { get; set; } = new();
    public string Timing { get; set; } = "onEnter";
    public List<string> Channels { get; set; } = new() { "system" };

    [JsonIgnore]
    public bool HasRecipients => Users.Count > 0;

    public bool ShouldFire(string currentTiming)
        => HasRecipients && (string.Equals(Timing, "always", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Timing, currentTiming, StringComparison.OrdinalIgnoreCase));

    public bool HasChannel(string channel)
        => Channels.Any(c => string.Equals(c, channel, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>解析 FCcConfigJson；null/空/非法JSON→null(=不触发)。向后兼容缺字段。</summary>
    public static CcNotifyConfig? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var cfg = JsonSerializer.Deserialize<CcNotifyConfig>(json, Opts);
            return cfg is { HasRecipients: true } ? cfg : null;
        }
        catch (JsonException) { return null; }
    }
}

public sealed class CcUser
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 写测试**

```csharp
using STOTOP.Module.CardFlow.Models;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class CcNotifyConfigTests
{
    [Fact]
    public void null或空JSON返回null不触发()
    {
        Assert.Null(CcNotifyConfig.Parse(null));
        Assert.Null(CcNotifyConfig.Parse(""));
        Assert.Null(CcNotifyConfig.Parse("   "));
    }

    [Fact]
    public void 非法JSON返回null不抛()
    {
        Assert.Null(CcNotifyConfig.Parse("{bad json"));
    }

    [Fact]
    public void users为空返回null不触发()
    {
        Assert.Null(CcNotifyConfig.Parse("""{"users":[],"timing":"onApprove","channels":["dingtalk"]}"""));
    }

    [Fact]
    public void 正常解析含timing和channels()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"onApprove","channels":["system","dingtalk"]}""");
        Assert.NotNull(cfg);
        Assert.Single(cfg!.Users);
        Assert.Equal("onApprove", cfg.Timing);
        Assert.True(cfg.HasChannel("system"));
        Assert.True(cfg.HasChannel("dingtalk"));
        Assert.False(cfg.HasChannel("wecom"));
    }

    [Fact]
    public void 缺timing默认onEnter_缺channels默认system()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}]}""");
        Assert.NotNull(cfg);
        Assert.Equal("onEnter", cfg!.Timing);
        Assert.True(cfg.HasChannel("system"));
        Assert.False(cfg.HasChannel("dingtalk"));
    }

    [Fact]
    public void ShouldFire匹配timing或always()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"onApprove"}""");
        Assert.True(cfg!.ShouldFire("onApprove"));
        Assert.False(cfg.ShouldFire("onEnter"));
        Assert.False(cfg.ShouldFire("onReject"));

        var always = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"always"}""");
        Assert.True(always!.ShouldFire("onEnter"));
        Assert.True(always.ShouldFire("onApprove"));
        Assert.True(always.ShouldFire("onReject"));
    }
}
```

- [ ] **Step 3: 跑测试绿 + build**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj --filter "FullyQualifiedName~CcNotifyConfigTests"` → PASS；`build-filter cardflow` → clean。

- [ ] **Step 4: Commit**

```bash
git add src/STOTOP.Module.CardFlow/Models/CcNotifyConfig.cs tests/STOTOP.Module.CardFlow.Tests/Rules/CcNotifyConfigTests.cs
git commit -m "feat(cardflow): CcNotifyConfig 模型+解析器(timing/channels/向后兼容) (M8-B)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: FireStageCcAsync 引擎 helper + 3 调用点 + 测试

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（新增 private `FireStageCcAsync`；3 处调用点）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/StageCcNotificationTests.cs`（Create）

**Interfaces:**
- Consumes: `CcNotifyConfig.Parse`（Task 1）、`_todoService.CreateTodoAsync`、`_notificationDispatcher.DispatchCreateTodoAsync`
- Produces: `FireStageCcAsync(card, stageInstanceId, stageDefinitionId, currentTiming)` — 引擎内部 private helper

- [ ] **Step 1: 写失败测试**（mirror `FlowActionNoTrackingPersistenceTests` 的 CreateNoTrackingDb/CreateEngine/SeedFlowAsync）

三用例：
1. `人工节点入口时onEnter_抄送触发创建ccTodo`：流程 A(human) 有 `FCcConfigJson={"users":[{userId:51,...}],"timing":"onEnter","channels":["system"]}`；提交卡片→进入 A→断言 `CfTodoItem` 存在 type="cc" userId=51。
2. `审批通过onApprove_抄送触发`：A(human) `timing:"onApprove"`；approve A→断言 cc-todo 被创建。
3. `timing不匹配_不触发`：A(human) `timing:"onReject"`；approve A→断言**无** cc-todo。

- [ ] **Step 2: 跑测试确认红**（当前引擎不读 FCcConfigJson，cc-todo 不会被创建）

- [ ] **Step 3: 实现 FireStageCcAsync**

在 `FlowEngineService.cs` 末尾（private helpers 区域）加：

```csharp
/// <summary>按人工节点的 FCcConfigJson 配置在指定 timing 触发抄送通知（创建 cc-todo + 按渠道推送）。</summary>
private async Task FireStageCcAsync(CfCard card, long stageInstanceId, long stageDefinitionId, string currentTiming)
{
    var stageDef = await _dbContext.Set<CfStageDefinition>().AsNoTracking()
        .FirstOrDefaultAsync(s => s.FID == stageDefinitionId);
    var ccConfig = CcNotifyConfig.Parse(stageDef?.FCcConfigJson);
    if (ccConfig == null || !ccConfig.ShouldFire(currentTiming)) return;

    foreach (var user in ccConfig.Users)
    {
        var todoId = await _todoService.CreateTodoAsync(
            card.FID, stageInstanceId, user.UserId, user.UserName,
            card.FTitle ?? "抄送通知", "cc");

        if (ccConfig.HasChannel("dingtalk") && todoId > 0)
        {
            await _notificationDispatcher.DispatchCreateTodoAsync(todoId);
        }
    }
}
```

> 注意：`_todoService.CreateTodoAsync` 的返回值——先 Read 确认它返回 `long`(todoId) 还是 void。若返回 void，需改为先创建再查回 ID，或修改 `CreateTodoAsync` 签名返回 ID。执行时核实。若 `DbTodoService.CreateTodoAsync` 不返回 ID，改为在 `FireStageCcAsync` 内直接构建 `CfTodoItem` 并 `_dbContext.Set<CfTodoItem>().Add()` + `SaveChangesAsync()` 取自增 ID。

- [ ] **Step 4: 3 调用点**

1. **onEnter**（`AssignStageHandlersAsync` 末尾，约 :2753 后）——在 "分配处理人 + 创建待办" 完成后加：
   ```csharp
   // 人工节点入口抄送
   if (string.Equals(stageDef.FType, "human", StringComparison.OrdinalIgnoreCase))
       await FireStageCcAsync(card, stageInstance.FID, stageDef.FID, "onEnter");
   ```
   需确认 `AssignStageHandlersAsync` 的参数列表中有 `card` 可达（Read 确认签名；可能需透传）。

2. **onApprove**（`ApproveAsync` 内，节点标 completed 后、推进前，约 :680-690）：
   ```csharp
   await FireStageCcAsync(card, stageInstance.FID, stageInstance.FStageDefinitionId, "onApprove");
   ```

3. **onReject**（`RejectAsync` 内，普通 reject 完成后，约 :800-810）：
   ```csharp
   await FireStageCcAsync(card, stageInstance.FID, stageInstance.FStageDefinitionId, "onReject");
   ```

- [ ] **Step 5: 跑测试绿 + 回归**

Run: `dotnet test ... --filter "FullyQualifiedName~StageCcNotificationTests"` → PASS；`FlowActionNoTrackingPersistenceTests` 仍绿（多跑 2 次）；`build-filter cardflow` clean。

- [ ] **Step 6: Commit**

```bash
git add src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs tests/STOTOP.Module.CardFlow.Tests/Approval/StageCcNotificationTests.cs
git commit -m "feat(cardflow): 人工节点 cc 消费 FCcConfigJson(onEnter/onApprove/onReject + system/dingtalk) (M8-B)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: 前端 StageConfigPanel 恢复 cc 配置段

**Files:**
- Create: `web/src/components/cardflow/ccConfigShared.ts`（+ `.spec.ts`）
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（恢复 cc 输入段）
- Modify: `web/src/components/cardflow/stageDefinitionShared.ts`（TS 类型，若需要）

**Interfaces:**
- Consumes: `CfStageDefinition` 的 `ccConfigJson` 字段（前端已有 round-trip 管线通过 stage save）
- Produces: `CcNotifyConfig` TS 类型 + parseCcConfig / serializeCcConfig helpers；UI segment in StageConfigPanel

- [ ] **Step 1: 写 shared + vitest**（`ccConfigShared.ts`）

```ts
export interface CcUser { userId: number; userName: string }
export interface CcNotifyConfig { users: CcUser[]; timing: string; channels: string[] }

export const CC_TIMING_OPTIONS = [
  { value: 'onEnter', label: '进入节点时' },
  { value: 'onApprove', label: '审批通过时' },
  { value: 'onReject', label: '审批驳回时' },
  { value: 'always', label: '全部时机' },
] as const

export const CC_CHANNEL_OPTIONS = [
  { value: 'system', label: '应用内待办', disabled: false },
  { value: 'dingtalk', label: '钉钉', disabled: false },
  { value: 'wecom', label: '企微', disabled: true },
  { value: 'bot', label: 'Bot/Webhook', disabled: true },
] as const

export function emptyCcConfig(): CcNotifyConfig {
  return { users: [], timing: 'onEnter', channels: ['system'] }
}

export function parseCcConfig(json?: string | null): CcNotifyConfig {
  if (!json) return emptyCcConfig()
  try {
    const raw = JSON.parse(json)
    return {
      users: Array.isArray(raw?.users) ? raw.users.filter((u: any) => u?.userId > 0) : [],
      timing: typeof raw?.timing === 'string' ? raw.timing : 'onEnter',
      channels: Array.isArray(raw?.channels) ? raw.channels : ['system'],
    }
  } catch { return emptyCcConfig() }
}

export function serializeCcConfig(cfg: CcNotifyConfig): string | undefined {
  return cfg.users.length > 0 ? JSON.stringify(cfg) : undefined
}
```

`.spec.ts`：parse round-trip、empty→undefined、缺字段默认、非法JSON。

- [ ] **Step 2: StageConfigPanel 恢复 cc 段**

删除 "ccConfigJson 运行时零消费故不再出输入框" 注释；在适当 Tab（通知/抄送）加入：
- 抄送对象（`a-select mode="multiple"` + `useUserSearch` remote search）
- 抄送时机（`a-radio-group` with `CC_TIMING_OPTIONS`）
- 通知渠道（`a-checkbox-group` with `CC_CHANNEL_OPTIONS`，企微/bot disabled）

绑定到 `stage.ccConfigJson`（parse on load, serialize on save —— 现有 stage 保存管线已 round-trip ccConfigJson 字段）。

- [ ] **Step 3: type-check + lint + vitest**

Run: `cd web && npm run type-check && npm run lint:style && npx vitest run src/components/cardflow/ccConfigShared.spec.ts`。

- [ ] **Step 4: Commit**

```bash
git add web/src/components/cardflow/ccConfigShared.ts web/src/components/cardflow/ccConfigShared.spec.ts web/src/components/cardflow/StageConfigPanel.vue
git commit -m "feat(cardflow): 设计器恢复 cc 配置段(时机+渠道+对象选择) (M8-B)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: 收口——终审 + 回归

- [ ] **Step 1: 子代理对抗性终审**：核 `FireStageCcAsync` 在 3 处调用点的事务安全（在已有 transaction 内、不自建）；核 `CcAsync` 手动路径未受影响；核企微/bot 灰置不可选。
- [ ] **Step 2: 全量回归**：`test-dotnet cardflow`（多跑 3 次）；`type-check` + `vitest` + `lint:style`。
- [ ] **Step 3: 更新记忆**。
- [ ] **Step 4: 提交修补（若有）**。不 push。
