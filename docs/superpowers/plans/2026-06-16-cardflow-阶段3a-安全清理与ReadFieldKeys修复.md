# CardFlow 阶段3a：死成员清理 + ReadFieldKeys bug 修复 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: 用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现。步骤用 `- [ ]` 复选框跟踪。
> 所有 commit 末尾追加：`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`；**绝不 push**。
> 工作区：worktree `cardflow-phase3a`（路径 `E:\STOTOP_Fable\.claude\worktrees\cardflow-phase3a`，分支 `cardflow-phase3a`，基线 98 passed / 1 skipped）。所有命令在此 worktree 内运行。

**Goal:** 删 3 个零调用死成员（3a）+ 修 `ReadFieldKeys` 让其处理 Object 形态卡片 schema（3b，修"审批节点字段权限全丢"的真 bug），不碰 FlowEngineService。

**Architecture:** 纯增量低风险。3a 三个删除各自独立 commit（删前 grep 复核零调用）。3b 走 TDD：先写 Object 形态失败测试，再内联补 Object 分支让其通过。worktree 有独立 bin/obj，`dotnet build/test` 干净无 MSB3021 锁。

**Tech Stack:** .NET（C#，`STOTOP.Module.CardFlow`）、xUnit + EFCore.InMemory、Windows / PowerShell。

---

## 通用验证命令

- 单测（每任务用，编译 CardFlow 模块 + 跑全部测试）：
  `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
  预期：`Passed!`，Failed: 0。Tasks 1-3 后 = 98 passed / 1 skipped；Task 4 后 = 99 passed / 1 skipped。
- 待删的 3 个文件均属 `STOTOP.Module.CardFlow`，测试项目引用该模块，故 `dotnet test` 即可验证编译。

---

## 文件结构

**修改（删成员）：**
- `src/STOTOP.Module.CardFlow/Services/BatchTriggerService.cs`（删 `MatchFlowDefinitionAsync` 实现）
- `src/STOTOP.Module.CardFlow/Services/Interfaces/IBatchTriggerService.cs`（删 `MatchFlowDefinitionAsync` 接口成员）
- `src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/QualityAnalysisPlugin.cs`（删 `ValidateRowBuiltin`）
- `src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/ITransformEngine.cs`（删 `Preview` 接口成员）
- `src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/JintTransformEngine.cs`（删 `Preview` 实现）

**修改（修 bug）+ 测试：**
- `src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs`（改 `ReadFieldKeys`）
- `tests/STOTOP.Module.CardFlow.Tests/Approval/StageViewProfileResolverTests.cs`（加 Object 形态测试）

> 注：接口 `IBatchTriggerService` 的真实路径在计划写就时按 grep 结果定位（`Services/Interfaces/IBatchTriggerService.cs`，含 `MatchFlowDefinitionAsync` 声明的那一行）。

---

## Task 1：删除死方法 `MatchFlowDefinitionAsync`（接口 + 实现）

`[Obsolete]` 一行转发方法，全库零调用方（仅接口声明 + 实现）。

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/Interfaces/IBatchTriggerService.cs`
- Modify: `src/STOTOP.Module.CardFlow/Services/BatchTriggerService.cs`

- [ ] **Step 1：Grep 复核零调用**

用 Grep 搜 `MatchFlowDefinitionAsync`（path: `src/` 与 `tests/`）。
预期命中仅 2 处：接口声明（`IBatchTriggerService.cs`）+ 实现（`BatchTriggerService.cs`）。**无第三处调用**。
注意区分 `MatchFlowDefinitionsAsync`（复数，**保留**，是真正在用的多流程匹配）——不要误删它。
若出现 `MatchFlowDefinitionAsync`（单数）的调用点 → 停止、保留、上报 BLOCKED。

- [ ] **Step 2：删接口成员**

在 `IBatchTriggerService.cs` 删除这行（含其上方的 XML 注释行，若紧邻）：
```csharp
    Task<long?> MatchFlowDefinitionAsync(IReadOnlyList<string> fileColumns, string? fileName, long orgId);
```

- [ ] **Step 3：删实现方法**

在 `BatchTriggerService.cs` 删除整段（含 `[Obsolete]` 特性与方法体，原 615-620 行）：
```csharp
    [Obsolete("请使用 MatchFlowDefinitionsAsync（支持多流程匹配）")]
    public async Task<long?> MatchFlowDefinitionAsync(IReadOnlyList<string> fileColumns, string? fileName, long orgId)
    {
        var results = await MatchFlowDefinitionsAsync(fileColumns, fileName, orgId);
        return results.FirstOrDefault()?.FlowDefinitionId;
    }
```
并删除其上方那段专属于它的 XML 文档注释（`/// <summary>根据文件列头匹配流程定义（两轮策略...）` 到 `/// <returns>...`）。

- [ ] **Step 4：编译 + 单测验证**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
Expected: `Passed!`，Failed: 0（98 passed / 1 skipped）。
若报「接口未实现/找不到成员」→ 说明接口与实现没同步删或有调用方，回 Step 1 复核。

- [ ] **Step 5：Commit**
```powershell
git add "src/STOTOP.Module.CardFlow/Services/Interfaces/IBatchTriggerService.cs" `
        "src/STOTOP.Module.CardFlow/Services/BatchTriggerService.cs"
git commit -m @'
refactor(cardflow): 删除零调用的 [Obsolete] MatchFlowDefinitionAsync（保留复数版）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 2：删除死方法 `ValidateRowBuiltin`

`QualityAnalysisPlugin` 内 `private static` 死方法，零调用方。

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/QualityAnalysisPlugin.cs`

- [ ] **Step 1：Grep 复核零调用**

用 Grep 搜 `ValidateRowBuiltin`（path: `src/` 与 `tests/`）。
预期命中仅 1 处：定义本身（`QualityAnalysisPlugin.cs:365`）。**无调用方**。
若出现第 2 处 → 停止、保留、上报 BLOCKED。

- [ ] **Step 2：删整方法**

在 `QualityAnalysisPlugin.cs` 删除**整个** `ValidateRowBuiltin` 方法——从签名行：
```csharp
    private static List<QualityErrorRecord> ValidateRowBuiltin(
        Dictionary<string, object?> row, decimal maxAmount, decimal minAmount)
    {
```
到其匹配的方法结尾 `}`（方法较长，含空值/异常值/日期三类检查；删到该方法自身的闭合大括号为止，不要多删到下一个方法或类的大括号）。

> 注：该方法用到的私有辅助（`IsEmptyValue`/`LooksLikeDateField`/`IsValidDate` 等）**保留不动**——它们可能被本类其它方法使用；本任务只删 `ValidateRowBuiltin` 自身。若编译出现"私有方法未使用"类提示（一般 C# 默认不报此为错），忽略，不扩大范围。

- [ ] **Step 3：编译 + 单测验证**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
Expected: `Passed!`，Failed: 0（98 passed / 1 skipped）。

- [ ] **Step 4：Commit**
```powershell
git add "src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/QualityAnalysisPlugin.cs"
git commit -m @'
refactor(cardflow): 删除零调用的私有死方法 QualityAnalysisPlugin.ValidateRowBuiltin

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 3：删除死接口方法 `ITransformEngine.Preview`（接口 + 实现）

无调用方（`JintTransformEngine` 仅 `Execute` 被用；`Preview` 是为前端预览预留但从未接通）。

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/ITransformEngine.cs`
- Modify: `src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/JintTransformEngine.cs`

- [ ] **Step 1：Grep 复核零调用**

用 Grep 搜 `.Preview(`（path: `src/` 与 `tests/`）。确认**没有**对 `ITransformEngine`/`_transformEngine`/`JintTransformEngine` 实例的 `.Preview(` 调用（grep 到的其它 `Preview` 均为无关 HTTP 端点如 `CfImportController.Preview`、`CodeRuleController.Preview`、Finance 各 Controller 的 `Preview`，与转换引擎无关）。
若出现对转换引擎实例的 `.Preview(` 调用 → 停止、保留、上报 BLOCKED。

- [ ] **Step 2：删接口成员**

在 `ITransformEngine.cs` 删除（含其上方注释）：
```csharp
    /// <summary>对样本数据执行转换，用于前端预览</summary>
    List<Dictionary<string, object?>> Preview(
        List<Dictionary<string, string>> sampleRows,
        List<TransformRule> rules);
```

- [ ] **Step 3：删实现方法**

在 `JintTransformEngine.cs` 删除整段（原 49-59 行）：
```csharp
    public List<Dictionary<string, object?>> Preview(
        List<Dictionary<string, string>> sampleRows,
        List<TransformRule> rules)
    {
        var results = new List<Dictionary<string, object?>>();
        foreach (var row in sampleRows)
        {
            results.Add(Execute(row, rules));
        }
        return results;
    }
```

- [ ] **Step 4：编译 + 单测验证**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
Expected: `Passed!`，Failed: 0（98 passed / 1 skipped）。

- [ ] **Step 5：Commit**
```powershell
git add "src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/ITransformEngine.cs" `
        "src/STOTOP.Module.CardFlow/Services/Import/TransformEngine/JintTransformEngine.cs"
git commit -m @'
refactor(cardflow): 删除零调用的 ITransformEngine.Preview（接口+JintTransformEngine 实现）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 4：修 `ReadFieldKeys` 处理 Object 形态（TDD）

`StageViewProfileResolver.ReadFieldKeys` 遇 Object 形态 schema 返回空 → 审批节点 `fieldAccess` 全空。v2 信封 `{"version":2,"fields":[...]}` 的字段数组在 `fields` 属性下（`CardSchemaV2.Fields` 无 JsonPropertyName，序列化为小写 `fields`）。

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs`
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/StageViewProfileResolverTests.cs`

- [ ] **Step 1：写失败测试（Object 形态）**

在 `StageViewProfileResolverTests.cs` 类内新增（与既有 `ResolveLegacyFallback_UsesInputFieldsAndDefaultActions` 同款，仅把 schema 换成 Object 形态）：
```csharp
    [Fact]
    public void ResolveLegacyFallback_ObjectSchema_ExtractsFieldKeys()
    {
        var resolver = new StageViewProfileResolver();
        var card = new CfCard { FDataJson = """{"accountCode":"1001","amount":88}""" };
        var config = new StageConfigEnvelope
        {
            Version = 1,
            InputFields = new List<string> { "accountCode" }
        };

        var result = resolver.Resolve(
            """{"version":2,"fields":[{"key":"accountCode","label":"科目"},{"key":"amount","label":"金额"}]}""",
            null,
            new CfStageDefinition { FStageName = "财务复核" },
            card,
            new List<CfCardDetail>(),
            operatorId: 1,
            config);

        // 修复前：ReadFieldKeys 对 Object 形态返回空 → FieldAccess 不含这些键（取值抛 KeyNotFound）
        Assert.Equal("editable", result.FieldAccess["accountCode"].Access);
        Assert.Equal("readonly", result.FieldAccess["amount"].Access);
    }
```
> 既有 `ResolveLegacyFallback_UsesInputFieldsAndDefaultActions` 已覆盖 Array 形态（回归保护），本任务只补 Object 形态。

- [ ] **Step 2：跑测试确认失败**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj --filter "FullyQualifiedName~ResolveLegacyFallback_ObjectSchema_ExtractsFieldKeys"`
Expected: **FAIL**（`result.FieldAccess["accountCode"]` 抛 `KeyNotFoundException`，因 Object 形态下 fieldKeys 为空 → FieldAccess 不含该键）。
若此步意外 PASS → 说明 bug 不复现或测试没命中，停下核对再继续。

- [ ] **Step 3：修 `ReadFieldKeys`**

把 `StageViewProfileResolver.cs` 的整个 `ReadFieldKeys` 方法替换为（仅"取出字段数组"那段加了 Object 分支，取 key 循环不变）：
```csharp
    private static List<string> ReadFieldKeys(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new List<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            var root = document.RootElement;

            // 字段数组：v1 扁平为顶层 Array；v2 信封为 { "fields": [...] }
            JsonElement fieldsArray;
            if (root.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("fields", out var fieldsProp)
                && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = fieldsProp;
            }
            else
            {
                return new List<string>();
            }

            var result = new List<string>();
            foreach (var field in fieldsArray.EnumerateArray())
            {
                if (field.ValueKind == JsonValueKind.Object
                    && field.TryGetProperty("key", out var keyProperty)
                    && keyProperty.ValueKind == JsonValueKind.String)
                {
                    var key = keyProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        result.Add(key);
                    }
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
```

- [ ] **Step 4：跑测试确认通过 + 全量无回归**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj --filter "FullyQualifiedName~ResolveLegacyFallback_ObjectSchema_ExtractsFieldKeys"`
Expected: **PASS**。
Run（全量）: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
Expected: `Passed!`，Failed: 0，通过数 = 99（98 既有 + 1 新），Skipped: 1。

- [ ] **Step 5：Commit**
```powershell
git add "src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs" `
        "tests/STOTOP.Module.CardFlow.Tests/Approval/StageViewProfileResolverTests.cs"
git commit -m @'
fix(cardflow): ReadFieldKeys 支持 Object 形态 schema（修审批节点字段权限丢失）+ 测试

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 5：收尾验证

**Files:** 无（仅验证）

- [ ] **Step 1：WebAPI 整体编译**

Run: `dotnet build src/STOTOP.WebAPI/STOTOP.WebAPI.csproj`
Expected: `Build succeeded`，0 Error（worktree 独立 bin/obj，应无 MSB3021 锁）。

- [ ] **Step 2：全量单测**

Run: `dotnet test tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
Expected: `Passed!`，Failed: 0，Passed: 99，Skipped: 1。

- [ ] **Step 3：确认 4 个死符号零残留**

用 Grep 在 `src/` 搜：`MatchFlowDefinitionAsync`（单数，应零命中）、`ValidateRowBuiltin`（应零命中）、`ITransformEngine` 的 `Preview`（接口里应已无 Preview 声明）。
确认保留物仍在：`MatchFlowDefinitionsAsync`（复数）、`ITransformEngine.Execute`、`ReadFieldKeys`（已含 Object 分支）。

- [ ] **Step 4：（可选）收尾 commit**
```powershell
git commit --allow-empty -m @'
chore(cardflow): 阶段3a 收尾——死成员清理+ReadFieldKeys 修复，99 passed/1 skipped

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## 自检（spec 覆盖 / 占位符 / 类型一致性）

- **spec 覆盖**：3a 三死成员 → Task 1/2/3；3b ReadFieldKeys 修复 + 两形态测试 → Task 4（Object 新测试 + 既有 Array 测试作回归）；验收 → Task 5。
- **占位符**：无 TBD/TODO；ReadFieldKeys 新旧实现、删除范围、测试代码、命令均为实测确值。`fields` 属性名已据 `CardSchemaV2.Fields`（无 JsonPropertyName → camelCase `fields`）+ bug 示例核实。
- **类型一致性**：`ReadFieldKeys` 签名不变（`private static List<string> ReadFieldKeys(string?)`）；测试用 `resolver.Resolve(...)` 七参签名与既有测试一致；`StageConfigEnvelope`/`CfStageDefinition`/`CfCard`/`CfCardDetail` 均与既有测试同款使用。

---

## 完成后

阶段3a 落地、作者复核（99 passed/1 skipped + 4 死符号零残留 + bug 已修）后：用 superpowers:finishing-a-development-branch 决定 worktree 分支 `cardflow-phase3a` 的去向（合并 master / PR / 保留）；再回阶段3 路线图选下一个子项（3c 撤销双份 / 3d 处理器工厂 / 3e 前端冻结 / 3f 字段模型 / 3g 条件 evaluator）。
