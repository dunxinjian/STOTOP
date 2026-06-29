# CardFlow 组织链/角色路由接通（设计稿）

> 日期：2026-06-19　状态：设计已确认（用户拍板「接通」方向 + 三项口径）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）③ 节点路由，子项 **#1（组织链/角色/关系四字段运行时永不赋值，相关路由条件恒判 false；UI 还主动诱导配置）**——③ backlog 最后一项。
> 同批前序已完成：脱敏链、①v2、②组织隔离三连、③ 空条件 catch-all、③#4 TypeError 选边、③ 预演≠运行时收敛、③ 退回拓扑收敛、③-sub2 发布图级校验。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-19 复核）。

---

## 0. 背景与缺陷（已核实）

求值器 `ConditionRuleEvaluator.ResolveField` 把 `OrgChain`/`RoleCodes`/`RoleNames`/`Relations` 当真源读取（[:168-173](../../../src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:168)、`relations` :185、`inorgchain`→`IsInOrgChain` :303-315），但 `ConditionEvaluationContextBuilder.BuildAsync` **从不给这四字段赋值**（全 src 无写入点），它们恒为空集合。于是 `inOrgChain`、`roles.code contains`、`roles.name`、`relations.*` 条件**运行期恒判 false**，卡片静默走默认/兜底，零报错。前端 `ConditionBuilder.vue` 还专门提供「属于组织链」操作符 + 输入框（[:96-102/384-391](../../../web/src/components/cardflow/ConditionBuilder.vue:96)），把死功能当活功能卖。审计结论：**这不是合理冻结，而是「UI 已上线、运行时未接通」的危险半成品，必须优先接通或下架。用户已拍板「接通」。**

### 已核实的关键事实
- **组织实体** `SysOrganization`（`src/STOTOP.Module.System/Entities/SysOrganization.cs`）：`FID`(long 主键)、`FParentId`(long 父组织)、`FCode`(string 编码)、`FName`、`FStatus`(int，1=启用)、`FManagerId`。
- **无可复用的「祖先组织链」服务**：`ApproverResolver.ResolveOrgChainAsync`（[:153-211](../../../src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs:153)）是 private 且**输出各级负责人(用户)**、非组织链；上溯逻辑内嵌其中。需自建（逻辑简单：投影加载 active orgs，从起点沿 `FParentId` 上溯）。
- **角色实体**：`SysUserRole{FUserId,FRoleId}` JOIN `SysRole{FID,FCode,FName}`。`AuthService.GetUserRoleCodesAsync`（[:274-283](../../../src/STOTOP.Module.System/Services/AuthService.cs:274)）是 private static、不在 `IAuthService` 接口；查询简单，可在本模块直接用 `_dbContext` 复刻。
- **CardFlow 已跨模块访问 System 实体**：`ApproverResolver` 已 `dbContext.Set<SysOrganization>()`，故 `Set<SysUserRole>()`/`Set<SysRole>()` 同样可用（`STOTOPDbContext` 跨模块共享）。
- **上下文构造器** `ConditionEvaluationContextBuilder(STOTOPDbContext dbContext)`（[:14](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:14)）**仅注入 DbContext**；DI 注册 Scoped（`CardFlowModuleExtensions.cs:36`），但 `FlowEngineService` 有 3 处手动 `new ConditionEvaluationContextBuilder(dbContext)`（:83/:85/:1859）。**故不能加构造器依赖**（否则破这 3 处）——用既有 `_dbContext` 直接查即可。
- **预演上下文** `CardFlowPathPreviewService.BuildPreviewContext`（[:234](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:234)）当前是 `private static`、不访问 DB；预演 service 有 `_dbContext` 字段。
- **inOrgChain 取值口径（用户拍板）**：前端组织选择器产出 **FID**，无遗留编码型条件需兼容（功能一直恒 false），故 OrgChain **只放 FID.ToString()**；求值器 `IsInOrgChain`→`CompareEquality` 数值/字符串通配比较，用户输 `192` 命中 `"192"`。

---

## 1. 目标 / 非目标

### 目标
1. 抽 `OrgRoleContextResolver`（运行时/预演**单一真源**）：纯函数 `BuildOrgChain`（从起点沿 FParentId 上溯、含本组织、放 FID 字符串、防环）+ DB 面 `ResolveAsync`（投影加载 active orgs → BuildOrgChain；查用户角色 → RoleCodes/RoleNames）。
2. 运行时 `BuildAsync`（用 `card.FOrgId`/`card.FInitiatorId`）与预演 `BuildPreviewContextAsync`（用 `request.OrgId`/`request.InitiatorId`）**共用 `ResolveAsync`** 填 `OrgChain`/`RoleCodes`/`RoleNames`。
3. `inOrgChain`/`roles.code`/`roles.name` 路由条件运行期与预演期**真正生效**；运行时/预演同源。
4. 单测钉死：纯 `BuildOrgChain` 各分支 + 运行时/预演集成填充与命中。

### 非目标（明确不做）
- ❌ **Relations.\***：代码库无 relations 业务模型/语义，留空、标非目标，待业务定义后另做。
- ❌ **前端不动**：`inOrgChain` 输入框已存在；那行「组织链编码或组织ID」placeholder 微调（→「组织ID」）留可选 trivial 后续。`roles.*` 的可视化字段配置（字段下拉不枚举 org/role 命名空间）是另一 UX 项，本轮只后端接通（roles 条件经手改 JSON 或后续 UX 项配）。
- ❌ 不动求值器读取逻辑、不改 `ConditionEvaluationContext` 模型、**不加 DI 注册/构造器依赖**（保 3 处 `new` 不破）。
- ❌ 不动 `ApproverResolver`（其 org 上溯输出的是负责人、口径不同，不复用其方法，仅自建简单上溯）。

---

## 2. 设计

### 2.1 `OrgRoleContextResolver`（交付物 1，单一真源）

新建 `src/STOTOP.Module.CardFlow/Services/OrgRoleContextResolver.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

public sealed record OrgRoleContext(List<string> OrgChain, List<string> RoleCodes, List<string> RoleNames);

public static class OrgRoleContextResolver
{
    /// <summary>
    /// 从 startOrgId 沿 FParentId 上溯，含本组织，每级放 FID.ToString()。
    /// visited 防环；startOrgId 不在 orgs（停用/缺失）→ 空链。纯函数。
    /// </summary>
    public static List<string> BuildOrgChain(
        IReadOnlyCollection<(long Id, long ParentId)> orgs,
        long startOrgId)
    {
        var parentOf = new Dictionary<long, long>();
        foreach (var (id, parentId) in orgs)
            parentOf[id] = parentId;   // 容忍重复 id：后者覆盖

        var chain = new List<string>();
        var visited = new HashSet<long>();
        var node = startOrgId;
        while (parentOf.TryGetValue(node, out var parent) && visited.Add(node))
        {
            chain.Add(node.ToString());
            node = parent;
        }
        return chain;
    }

    /// <summary>
    /// 加载 active 组织上溯 startOrgId 的 OrgChain；查 userId 的角色编码/名称。
    /// userId 空/≤0 → 角色空。运行时与预演共用。
    /// </summary>
    public static async Task<OrgRoleContext> ResolveAsync(
        STOTOPDbContext db, long orgId, long? userId, CancellationToken cancellationToken = default)
    {
        var orgRows = await db.Set<SysOrganization>()
            .Where(o => o.FStatus == 1)
            .Select(o => new { o.FID, o.FParentId })
            .ToListAsync(cancellationToken);
        var orgChain = BuildOrgChain(
            orgRows.Select(o => (o.FID, o.FParentId)).ToList(), orgId);

        var roleCodes = new List<string>();
        var roleNames = new List<string>();
        if (userId is long uid && uid > 0)
        {
            var roles = await db.Set<SysUserRole>()
                .Where(ur => ur.FUserId == uid)
                .Join(db.Set<SysRole>(), ur => ur.FRoleId, r => r.FID, (ur, r) => new { r.FCode, r.FName })
                .ToListAsync(cancellationToken);
            roleCodes = roles.Select(x => x.FCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            roleNames = roles.Select(x => x.FName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }

        return new OrgRoleContext(orgChain, roleCodes, roleNames);
    }
}
```

> 投影 `{FID,FParentId}` 避免加载整实体（规避 F 非空 long 列读整实体的 SqlNullValueException 类陷阱）。`BuildOrgChain` 纯函数可穷举单测；`ResolveAsync` 经集成测试覆盖。性能口径同 `ApproverResolver`（每次加载 active orgs）。

### 2.2 运行时 `BuildAsync` 接线（交付物 2a）

`ConditionEvaluationContextBuilder.BuildAsync`（[:19-67](../../../src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:19)）在工厂调用 + `completedStageKey` 块**之后、return 之前**追加：

```csharp
        var orgRole = await OrgRoleContextResolver.ResolveAsync(
            _dbContext, card.FOrgId, card.FInitiatorId, cancellationToken);
        context.OrgChain = orgRole.OrgChain;
        context.RoleCodes = orgRole.RoleCodes;
        context.RoleNames = orgRole.RoleNames;
```

`card.FInitiatorId` 是 long（非空），隐式转 long? 传入。**不加构造器依赖**，3 处 `new` 不破。

### 2.3 预演 `BuildPreviewContextAsync` 接线（交付物 2b）

`CardFlowPathPreviewService.BuildPreviewContext` 由 `private static ConditionEvaluationContext BuildPreviewContext(CardFlowPathPreviewRequest)` 改为 `private async Task<ConditionEvaluationContext> BuildPreviewContextAsync(CardFlowPathPreviewRequest request, CancellationToken cancellationToken)`，访问实例 `_dbContext`：工厂调用产出 context 后追加：

```csharp
        var orgRole = await OrgRoleContextResolver.ResolveAsync(
            _dbContext, request.OrgId ?? 0, request.InitiatorId, cancellationToken);
        context.OrgChain = orgRole.OrgChain;
        context.RoleCodes = orgRole.RoleCodes;
        context.RoleNames = orgRole.RoleNames;
        return context;
```

调用点 `PreviewDraftVersionAsync`（[:71](../../../src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:71)）`var context = BuildPreviewContext(request);` 改为 `var context = await BuildPreviewContextAsync(request, cancellationToken);`（该方法已有 `cancellationToken` 形参）。`ParseObject`/`ToPlainValue` 静态辅助仍从实例方法可调，保持不变。

---

## 3. 兼容性与风险

- **无 schema/迁移、无 DI 改动、无新构造器依赖**：`ConditionEvaluationContextBuilder` 仍 `(STOTOPDbContext)`，3 处 `new` 不破；`OrgRoleContextResolver` 是无状态静态类，无需注册。
- **行为变更（修正方向）**：`inOrgChain`/`roles.*` 条件由「恒 false」变「按真实组织链/角色判定」。凡按这些配置的路由分支开始真正生效——这正是接通目标。存量流程若**误配**了这类条件（以为不生效），接通后会改变路由；但审计立场是这本就该生效（UI 卖的就是活功能）。属预期内的「死功能转活」。
- **预演 `BuildPreviewContext` 变 async**：仅一处调用点改 `await`，无其它消费者。
- **性能**：运行时每次路由决策 + 每次预演各多一次「加载 active orgs + 查角色」。口径同 `ApproverResolver`（已全量加载 active orgs），可接受；投影只取两列。
- **跨模块**：查 System 实体经共享 `STOTOPDbContext`，与 `ApproverResolver` 既有做法一致。
- 风险整体中低：两处纯新增/接线 + 一处签名 async 化，纯函数可穷举测、集成测试钉端到端。

---

## 4. 测试（TDD，x64 必带）

### 4.1 `OrgRoleContextResolverTests`（纯 `BuildOrgChain`，无 DbContext）
新增 `tests/STOTOP.Module.CardFlow.Tests/Rules/OrgRoleContextResolverTests.cs`：
- 线性层级：orgs `(3,2),(2,1),(1,0)`，start 3 → `["3","2","1"]`（含本级、按上溯序）。
- start 不在 orgs → 空链。
- 父链成环 `(1,2),(2,1)`，start 1 → `["1","2"]`（不死循环）。
- 单组织 `(1,0)`，start 1 → `["1"]`。

### 4.2 运行时集成（`ConditionEvaluationContextBuilderTests` 追加）
- 种 `SysOrganization` 层级（叶→父→根）+ `SysUserRole`/`SysRole`（给 initiator 配两个角色）+ `CfCard{FOrgId=叶, FInitiatorId=user}` → `BuildAsync` → 断言 `context.OrgChain` 含叶+各级祖先 FID 字符串、`RoleCodes`/`RoleNames` 含配的角色编码/名称。

### 4.3 预演集成（`CardFlowPathPreviewServiceTests` 追加）
- 种 orgs（叶在某组织链）+ 一条 `inOrgChain` 路由规则（如 `{"field":"orgChain","operator":"inOrgChain","value":<祖先FID>}`）→ `PreviewDraftVersionAsync` 带 `OrgId=叶` → 预演命中该 inOrgChain 分支（修复前恒走默认）。

> 计划阶段确认 `TestDbContextFactory` 已注册 System 实体（`SysOrganization`/`SysUserRole`/`SysRole` 可 Set）——探查已知其注册 SysUser 程序集，应可用；若某实体不可 Set，计划阶段调整。

### 4.4 回归
- `dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64` 全绿（重点 `ConditionEvaluationContextBuilderTests`/`CardFlowPathPreviewServiceTests`/`ConditionRuleEvaluatorTests` 等不回归）。
- `STOTOP.Module.CardFlow` 编译 0 错。

---

## 5. 任务分解预览（供 writing-plans）
1. **`OrgRoleContextResolver`**（`BuildOrgChain` 纯函数 + `ResolveAsync`）+ `OrgRoleContextResolverTests`（4 case，TDD 先红后绿）。
2. **运行时 `BuildAsync` 接线** + 集成测试（OrgChain/RoleCodes/RoleNames 填充，红→绿）。
3. **预演 `BuildPreviewContextAsync` 接线**（static→instance async + 调用点 await）+ 预演 inOrgChain 命中集成测试（红→绿）。
4. **收尾验证**：全量模块单测 x64 + 编译 0 错。

> 转 writing-plans 逐项细化为 TDD 步骤（红→绿→重构），每步独立可验证。
