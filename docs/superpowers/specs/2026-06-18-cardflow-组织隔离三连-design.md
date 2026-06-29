# CardFlow 组织隔离三连修复（设计稿）

> 日期：2026-06-18　状态：设计已确认（克隆目标组织口径已拍板：一律落当前组织）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md) 第 0 批，安全/组织隔离）。本文为方案文档，**不修改代码**；现状结论带真实 file:line（2026-06-18 复核）。

---

## 0. 背景与缺陷（已核实）

`FlowDefinitionService` 的克隆/模板链在组织隔离上被三处打穿，叠加一处 FillOrgId 机制冲突：

| 编号 | 缺陷 | 证据 |
|---|---|---|
| #1（P1 安全·跨组织写越权） | `CloneFlowDefinitionAsync` 用 `FOrgId = request.OrgId ?? 0` 直接信任客户端 OrgId；源读取用 `IgnoreQueryFilters` 可读任意组织/模板。任意认证用户可把流程定义克隆进无权组织、或以他组织私有流程为源。 | [FlowDefinitionService.cs:945](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:945)、[:927-929](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:927) |
| #2（P1 数据完整性/泄露） | `GetTemplatesAsync` 仅按 `FIsTemplate && published` 过滤，**不限 FOrgId==0**。叠加 #3 后，被 FillOrgId 盖成 org 的「模板」会无差别下发给所有组织（含 FAllowedRolesJson/FTriggerConfigJson 跨租户泄露）。 | [FlowDefinitionService.cs:1019-1021](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1019) |
| #3（P1 功能不可用） | `SaveAsTemplate` 新建模板分支传 `OrgId=0`，但 `FillOrgIdForNewEntities` 对任何 `IOrgScoped` 且 `FOrgId==0` 的 Added 实体覆写为当前组织 → 模板落不到全局 + `(FFlowCode,FOrgId)` 唯一索引与源冲突 → `DbUpdateException`（控制器 catch 不到）→ **500，save-as-template 全程不可用**。 | [FlowDefinitionService.cs:1105-1115](../../../src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1105)、[STOTOPDbContext.cs:171-183](../../../src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs:171)、唯一索引 [CfFlowDefinitionConfiguration.cs:32](../../../src/STOTOP.Module.CardFlow/Configurations/CfFlowDefinitionConfiguration.cs:32) |

**已核实的关键事实（影响设计）：**
- **仅 `CfFlowDefinition` 是 `IOrgScoped`**；`CfFlowVersion`/`CfStageDefinition`/`CfStageRouteRule`/`CfDynamicStagePolicy` 均 `: BaseEntity`（非 IOrgScoped）→ `FillOrgIdForNewEntities` 不触碰它们，唯一索引冲突也只在 `CfFlowDefinition`。故抑制作用域只需罩住 def 一次保存。
- `FlowDefinitionService` 注入**具体 `STOTOPDbContext`**（非 DbContext 基类）→ 可直接调用其上新增的抑制方法。
- 控制器仅 `GetUserId()`；当前组织是服务端 `IOrgContextAccessor.CurrentOrgId`（经 org-switch UX 切换），**不来自请求体**。
- `TestDbContextFactory.Create` 构造 `new STOTOPDbContext(options)` **不注入 IOrgContextAccessor** → 现有 InMemory 测试里 `FillOrgId` 是 no-op（#3 的 500 仅在 prod 真 accessor 下复现）。测 #3 需注入假 accessor。

---

## 1. 目标 / 非目标

### 目标
1. 克隆**一律落当前组织**（服务端 `CurrentOrgId`），忽略请求体 OrgId（解 #1 写越权）。
2. 源读取**隔离**：只能以本组织流程或全局模板为源，不能读他组织私有流程（解 #1 读越权）。
3. 模板**真正持久化 `FOrgId=0`**（解 #3，恢复 save-as-template）。
4. `GetTemplatesAsync` 只返 `FOrgId==0` 模板（解 #2 泄露）。
5. 新增**通用、默认关**的 `FillOrgId` 抑制开关，供模板创建这唯一内部场景使用。

### 非目标（本轮明确不做）
- ❌ 不给 `FlowDefinitionController` 加 `RequirePermission`（单独硬化项，另排）。
- ❌ 不删 `CloneFlowDefinitionRequest.OrgId` 字段（保留兼容前端，仅服务端忽略）。
- ❌ 不做"克隆到指定他组织"能力（决策：要进别组织先切组织再克隆）。
- ❌ 不改流程引擎/脱敏等无关逻辑。

### 已锁定决策
| 编号 | 决策 | 结论 |
|---|---|---|
| D1 | 克隆/克隆到组织 的目标组织 | **一律落当前组织**（服务端 CurrentOrgId）；忽略请求体 OrgId |
| D2 | 模板（FOrgId=0）如何持久化 | **新增 `SuppressOrgIdFill()` 抑制作用域**，模板创建时显式置 0 并抑制自动填充 |
| D3 | 源读取范围 | 本组织流程 **或** 全局模板（FOrgId=0）；不可读他组织私有流程 |

---

## 2. 架构与改动

### 2.1 基建：`STOTOPDbContext.SuppressOrgIdFill()`（STOTOP.Infrastructure）
新增一个**作用域式、默认关**的抑制开关，纯增量、不影响其它模块：
```csharp
private bool _suppressOrgIdFill;

/// <summary>在返回的作用域内，SaveChanges 不对 FOrgId==0 的新实体自动填充当前组织。
/// 仅用于「有意创建全局(FOrgId=0)记录」这类受控内部场景。</summary>
public IDisposable SuppressOrgIdFill()
{
    _suppressOrgIdFill = true;
    return new SuppressScope(() => _suppressOrgIdFill = false);
}

private sealed class SuppressScope(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
```
`FillOrgIdForNewEntities()` 开头加 `if (_suppressOrgIdFill) return;`。

### 2.2 克隆收敛为共享内部方法 `CloneInternalAsync`
把现有 `CloneFlowDefinitionAsync` 的"建 def + 克隆当前版本 + 节点 + 路由 + 动态策略"主体抽为私有：
```csharp
private async Task<CfFlowDefinition> CloneInternalAsync(
    CfFlowDefinition source, string flowName, string flowCode, string? description,
    bool asGlobalTemplate, long operatorId)
```
- 建 `newDefinition`（不再读 `request.OrgId`）：
  - `asGlobalTemplate == false`（用户克隆）：**不设 FOrgId**（默认 0）→ `Add` + `SaveChanges` → `FillOrgIdForNewEntities` 填当前组织。
  - `asGlobalTemplate == true`（模板）：`newDefinition.FOrgId = 0;` 且把 `Add` + 该次 `SaveChanges` 包进 `using (_dbContext.SuppressOrgIdFill()) { ... }` → 持久化 0。
- 其后克隆版本/节点/路由/动态策略（这些非 IOrgScoped，照常 SaveChanges，无需抑制）——沿用现有逻辑，仅去重为一处。

公开 `CloneFlowDefinitionAsync(sourceId, request, operatorId)`：
- **源读取隔离**：先按当前组织过滤读 `var source = await Set<CfFlowDefinition>().FirstOrDefaultAsync(x=>x.FID==sourceId);`，读不到再回退全局模板 `?? await Set<CfFlowDefinition>().IgnoreQueryFilters().FirstOrDefaultAsync(x=>x.FID==sourceId && x.FOrgId==0);`；仍 null → `throw InvalidOperationException("源流程定义不存在")`。
- 调 `CloneInternalAsync(source, request.FlowName, request.FlowCode, request.Description, asGlobalTemplate:false, operatorId)`。
- `request.OrgId` **不再使用**（保留字段，加 `// 忽略：克隆一律落当前组织，目标组织由服务端 CurrentOrgId 决定` 注释）。

### 2.3 `SaveAsTemplateAsync`
- 源读取保持本组织过滤（模板从本组织流程产生，合理）。
- 「新建模板」分支（现 `else` 分支调 `CloneFlowDefinitionAsync(OrgId=0)`）改为调 `CloneInternalAsync(source, source.FFlowCode..., asGlobalTemplate:true, operatorId)`，随后置 `FIsTemplate=true`/`FStatus=published`/当前版本 published（沿用现有后处理）。
- 「更新已存在模板」分支（`existingTemplate`）逻辑不变（它更新的是已存在的 FOrgId=0 行，无新建、不触发 FillOrgId）。

### 2.4 `GetTemplatesAsync`
Where 改为 `x.FIsTemplate && x.FStatus == "published" && x.FOrgId == 0`（保留 `IgnoreQueryFilters` 以跨组织取全局模板）。

---

## 3. 测试

新增 `tests/STOTOP.Module.CardFlow.Tests/Rules/FlowDefinitionOrgIsolationTests.cs`：

**(A) 无需 org accessor（FillOrgId no-op）即可测：**
- `GetTemplatesAsync_ReturnsOnlyGlobalTemplates`：seed 一条 `FIsTemplate && published && FOrgId=0` + 一条 `FIsTemplate && published && FOrgId=192` → 仅返回 FOrgId=0 那条。
- `CloneFlowDefinitionAsync_IgnoresRequestOrgId`：accessor=null（FillOrgId no-op，FOrgId 保持 0），传 `request.OrgId=999` → 新 def `FOrgId==0`（**不等于 999**），证明请求体 OrgId 被忽略。
- `CloneFlowDefinitionAsync_OtherOrgPrivateSource_NotFound`：seed 一条 FOrgId=888 的**非模板**流程；当前组织过滤器（需 accessor=本组织，见下）下以它为源 → 抛"源流程定义不存在"。（若 accessor 注入成本高，可退化为：源 FOrgId=888 非模板、回退分支只认 FOrgId==0 → 读不到 → throw。）
- `CloneFlowDefinitionAsync_GlobalTemplateSource_Works`：源为 FOrgId=0 模板 → 克隆成功。

**(B) 需假 `IOrgContextAccessor`（证 FillOrgId 与抑制）：**
- 新增 `TestDbContextFactory.Create(databaseName, long? currentOrgId)` 重载：注入一个返回 `currentOrgId` 的假 `IOrgContextAccessor`（用既有构造器 `STOTOPDbContext(options, accessor)`；计划阶段核对真实构造器签名）。
- `SaveAsTemplate_PersistsGlobalOrgZero_DespiteOrgFill`：accessor=org192，源为 org192 流程 → SaveAsTemplate 产出的模板 `FOrgId==0`（证抑制生效，且不撞唯一索引、不 500）。
- `Clone_NonTemplate_GetsCurrentOrg`：accessor=org192 → 普通克隆新 def `FOrgId==192`（证 FillOrgId 对非模板仍正常工作）。

回归：既有全量 `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64`（x64 宿主必带）绿。WebAPI 编译 0 错（dev 后端占 DLL 时退化为模块编译 `dotnet build src/STOTOP.Module.CardFlow/...`）。

---

## 4. 兼容性与风险

- **基建改动跨模块**：`SuppressOrgIdFill` 纯增量、默认关，不改任何现有 SaveChanges 行为；唯一调用方是模板创建。低风险。
- **源读取收紧**：原来能以"他组织私有流程"为源克隆（越权）现在会"源不存在"——这是**有意收紧**；正常用法（克隆本组织流程 / 全局模板）不受影响。
- **`request.OrgId` 被忽略**：前端 clone-to-org 现传 OrgId（多半即当前组织），忽略后行为对正常用法不变；跨组织克隆需先切组织（既有 UX）。
- **存量被污染的"伪模板"**（历史 FillOrgId 把 FOrgId 盖成 org 的 FIsTemplate 行）：GetTemplates 加 `FOrgId==0` 后将不再下发它们——属正确行为（它们本就不该是全局模板）；如需清理是单独数据订正，不在本轮。

---

## 5. 任务分解预览（供 writing-plans）
1. 基建：`STOTOPDbContext.SuppressOrgIdFill()` + `FillOrgIdForNewEntities` 抑制早返回。
2. `CloneInternalAsync` 抽取 + 公开 `CloneFlowDefinitionAsync` 改（源读取隔离 + 忽略 request.OrgId + asGlobalTemplate=false）。
3. `SaveAsTemplateAsync` 新建分支改调 `CloneInternalAsync(asGlobalTemplate:true)`。
4. `GetTemplatesAsync` 加 `FOrgId==0`。
5. `TestDbContextFactory` 假 accessor 重载 + 组织隔离测试（A 组无 accessor + B 组有 accessor）。
6. 收尾验证（全量单测 + 模块编译）。

> 转 writing-plans 逐项细化为 TDD 步骤。
