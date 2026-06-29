# FIN科目去组织作用域重构 — 设计文档（v2：物理删列）

- 日期：2026-06-16
- 范围：`STOTOP.Core`、`STOTOP.Module.Finance`、`STOTOP.WebAPI`（种子/迁移）
- 类型：重构 + 表结构变更（删列）

> v2 变更：用户决定**物理删除** `F组织ID` 列（方案 B），不再保留为审计字段。
> 这覆盖了原任务"FOrgId 字段保留、不要直接删"的约束——用户明确确认承担其代价。

## 1. 背景与问题

`FinAccount`（FIN科目）实现 `IOrgScoped`，被 `STOTOPDbContext` 的"组织全局查询过滤器
（按 `FOrgId`）"约束（[STOTOPDbContext.cs:115-146](../../../src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs)）。
但科目本质是**账套级共享**：账套已按组织授权，科目只按 `FAccountSetId` 限定，且账套可跨组织共享。

错误的作用域导致：满地 `.IgnoreQueryFilters()` 绕过（AccountService 12 + AmoebaPLService 5）；
`FOrgId` 沦为不参与任何过滤、**全程无索引**的残留字段；未加绕过的查询点（VoucherService、
ReportService、AccountSetService 等十余处）在跨组织共享账套下存在隐性漏查 bug。

同问题存在于兄弟实体 `FinAccountBalance`、`FinAuxiliaryBalance`（结构一致、`FOrgId` 无索引）。

## 2. 目标与非目标

### 目标
- 三实体 `FinAccount` / `FinAccountBalance` / `FinAuxiliaryBalance` 不再受组织过滤器约束
  （改为账套作用域标记 `IAccountSetScoped`）。
- 移除多余的 `.IgnoreQueryFilters()`（17 处），使意图清晰。
- **物理删除三张表的 `F组织ID` 列**（含属性、配置映射、写入代码、种子列、实库列）。
- `dotnet build` 通过；全新建库成功；科目/凭证/余额查询无回归。

### 非目标（明确排除）
- 不改科目结构（除删 `FOrgId` 外）、不改其它业务逻辑。
- 不动 `FinVoucher` / `FinVoucherEntry` 的组织作用域——凭证确是组织级。
- 不动 `FinAccountPeriod`、`FinAccountSet`、`FinAccountTemplate`、`FinAuxiliaryItem` 等其它实体的
  `FOrgId`（它们genuinely组织级或全局共享，不在范围）。**种子里 `FIN账套` / `FIN科目模板` 的
  `F组织ID` 保持不动。**
- 不动 `AuxiliaryService` 对 `FinAuxiliaryItem` 的绕过（另一回事：全局品牌 `FOrgId=0`）。

## 3. 已确认的决策

| 决策 | 选择 |
|---|---|
| D1 实体范围 | 三个全改：`FinAccount` + `FinAccountBalance` + `FinAuxiliaryBalance` |
| D2 实现方式 | 新增 `IAccountSetScoped` 标记接口（不挂全局过滤器） |
| D3 AmoebaPL | 一并清理 AmoebaPLService 的 5 处冗余绕过 |
| **D4 F组织ID** | **方案 B：物理删列**（删属性/映射/909 条种子列/写入 + V5 迁移 DropColumnSafe；连带余额表） |

## 4. 设计

### 4.1 新增标记接口 `IAccountSetScoped`

文件：`src/STOTOP.Core/Models/IAccountSetScoped.cs`

```csharp
namespace STOTOP.Core.Models;

/// <summary>
/// 标记"账套级共享"实体：按 FAccountSetId 限定，不随组织隔离。
/// 与 IOrgScoped 互斥——实现本接口的实体【不】被组织全局查询过滤器约束。
/// 账套本身已按组织授权（FinAccountSetAuthorization），账套即访问边界。
/// </summary>
public interface IAccountSetScoped
{
    long FAccountSetId { get; set; }
}
```

纯标记，不在 `STOTOPDbContext` 注册任何过滤器。

### 4.2 三个实体改造

`FinAccount` / `FinAccountBalance` / `FinAuxiliaryBalance`：
- `: BaseEntity, IOrgScoped` → `: BaseEntity, IAccountSetScoped`
- **删除 `public long FOrgId { get; set; }` 属性及其注释**
- 其余成员不变（三者均已有 `FAccountSetId`，满足新接口）。

### 4.3 配置类删除 FOrgId 映射

删除以下行：
- [FinAccountConfiguration.cs:26](../../../src/STOTOP.Module.Finance/Configurations/FinAccountConfiguration.cs)
- [FinAccountBalanceConfiguration.cs:23](../../../src/STOTOP.Module.Finance/Configurations/FinAccountBalanceConfiguration.cs)
- [FinAuxiliaryBalanceConfiguration.cs:24](../../../src/STOTOP.Module.Finance/Configurations/FinAuxiliaryBalanceConfiguration.cs)

（均为 `builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);`）

删除后 EF 模型不再含该列，全新建库（`CreateTablesAsync` 按模型建表）不会创建 `F组织ID`。

### 4.4 `STOTOPDbContext` 无需改动

组织过滤器循环与 `FillOrgIdForNewEntities` 都按 `IOrgScoped` 收集；三实体退出该接口后自动
不被处理。`IAccountSetScoped` 不挂过滤器。DbContext **零改动**。

### 4.5 清理 `IgnoreQueryFilters`（删 17 留 1）+ 删 2 处写入

**`AccountService.cs`**：删除以下 `IgnoreQueryFilters`（HEAD 行号，实施按方法名+实体定位）：
46/105/127/259/283/320/386/448/462（FinAccount，9 处）、269/328/409（FinAccountBalance，3 处）。
**保留** 249（`FinVoucherEntry`，凭证组织级，跨组织查引用合理）。
另删除两处 `FOrgId = GetCurrentOrgId()` 写入：**188**（FinAccount）、**429**（FinAccountBalance）。
清理同行误导性注释。

**`AmoebaPLService.cs`**：删 1870/2696/2835/3208/3694（FinAccount，5 处）。

> `dotnet build` 会报出任何遗漏的 `.FOrgId` 读写（编译错误兜底）。已知触及三实体 FOrgId 的
> 写入仅 AccountService:188/429；`AccountPeriodService:136` 是 `FinAccountPeriod`（范围外，不动）。

### 4.6 种子数据：改 909 条 `FIN科目` INSERT

[FinanceSeeder.cs](../../../src/STOTOP.WebAPI/Data/Seeders/FinanceSeeder.cs) 的 `MigrateV1` 含
**909 条** `INSERT INTO [FIN科目] (…, [F组织ID], …)`。删列后全新建库表无此列，这些 INSERT 会失败，
故必须同步去列：
- 列清单：`[F账套ID], [F组织ID], [F创建时间]` → `[F账套ID], [F创建时间]`（全文件统一替换）。
- VALUES：删除第 14 列（`F组织ID`）对应的整数值——位于"日期字面量 `N'2026…'` 前紧邻的第三个
  连续整数"（顺序为 `…启用状态, 账套ID, 组织ID, N'创建时间'…`）。用脚本（PowerShell 正则）做
  **按位删除**，因各账套 `F组织ID` 取值不同（0 / 192 等），不可按值匹配。
- **不动** `FIN账套`、`FIN科目模板` 的 INSERT（范围外实体，保留其 `F组织ID`）。
- 余额表无种子 INSERT（0 条），无需处理。

> 风险点：`dotnet build` 查不出 SQL 字符串错误。必须靠**全新建库实测**或**每行"列数=值数"校验**
> 验证；详见 §6。

### 4.7 Schema 迁移：FinanceSeeder 加 V5 步骤删实库列

在 `FinanceSeeder.Migrate` 的 steps 末尾追加（当前最高 V4）：

```csharp
new(5, "删除 FIN科目/科目余额/辅助余额 的 F组织ID 列 (2026-06-16)", MigrateV5),
```

```csharp
private static void MigrateV5(STOTOPDbContext ctx)
{
    if (!SeederHelper.IsSqlServer(ctx)) return;
    SeederHelper.DropColumnSafe(ctx, "FIN科目", "F组织ID");
    SeederHelper.DropColumnSafe(ctx, "FIN科目余额", "F组织ID");
    SeederHelper.DropColumnSafe(ctx, "FIN辅助核算余额", "F组织ID");
}
```

`DropColumnSafe`（[SeederHelper.cs:113](../../../src/STOTOP.WebAPI/Data/SeederHelper.cs)）会先删依赖
索引/约束与 DEFAULT 约束再 `DROP COLUMN`，且 `IF EXISTS 列` 幂等——既存库删列、全新库（列本就
不存在）跳过。版本号严格递增、记录于 `SYS迁移历史`。

### 4.8 顺手：`ReportService.GetAccountBalanceAsync`

[:74](../../../src/STOTOP.Module.Finance/Services/ReportService.cs) `Query().ToListAsync()` 无 Where，
补 `.Where(a => a.FAccountSetId == accountSetId)` 限定范围（小幅安全/效率改进，非回归）。

## 5. 行为变化分析（为何安全）

移除组织过滤器后，三实体查询从"按当前组织过滤"变为"不过滤"。逐类核实：

1. **已按 `FAccountSetId` 限定的读取**（绝大多数）：变为"返回该账套全部行（不分组织）"，即跨组织
   共享账套应有的正确行为。账套授权是真边界。
2. **按主键读取**（`Repository.GetByIdAsync`→`FindAsync`，[Repository.cs:20](../../../src/STOTOP.Infrastructure/Repositories/Repository.cs)）：
   `FindAsync` 套用过滤器。VoucherService 录入凭证按 `AccountId` 取科目时，今天若 `FOrgId` 不符即
   误报"科目不存在"；移除后**修复**（正是任务症状）。
3. **`AccountSetService` 初始化**：今天"是否已初始化"计数、强制清理只看当前组织，跨组织漏判漏删；
   移除后**修复**。
4. **Report/TrialBalance/Journal**：均按 `FAccountSetId` 或 `accountIds` 间接限定，行为正确。

> 安全前提（沿用现有代码已依赖）：账套访问经 `FinAccountSetAuthorization` 授权，调用方只在已授权
> 账套内传 `FAccountSetId`。本次把 17 处绕过已假设的前提推广到全部查询点，不引入新假设。

## 6. 验收与验证

- [ ] `dotnet build` 通过（兜底报出所有遗漏的 `.FOrgId` 引用）。
- [ ] grep 复核：三实体相关代码再无 `IgnoreQueryFilters`（`FinVoucherEntry` 那处保留）；
      `src` 内三实体再无 `FOrgId` 引用。
- [ ] **种子完整性**：`INSERT INTO [FIN科目]` 仍为 909 条，且每条"列数 = 值数"（脚本校验）。
- [ ] **全新建库实测**：跑一次 full init（`DatabaseService.FullInitializeAsync` 路径），
      `FIN科目` 909 条全部插入成功、表无 `F组织ID` 列、启动无 SchemaAutoSync"多余列"告警。
- [ ] 既存库：启动后 V5 迁移执行成功，三表 `F组织ID` 列被删，`SYS迁移历史` 记录 Finance V5。
- [ ] 功能回归：科目建/查/树/查重/辅助核算筛选；跨组织共用账套可见；按主键取科目不误报；
      凭证录入/更新/红冲/校验；期初余额、期间结转、试算平衡、阿米巴报表；账套初始化（含强制重置）。

## 7. 风险与缓解

| 风险 | 等级 | 缓解 |
|---|---|---|
| 909 条种子去列出错（列/值错位），build 查不出 | **高** | 脚本按位删除 + 每行列数=值数校验 + 全新建库实测（§6） |
| 改动已记录的基线迁移 V1（违反"只追加"惯例） | 中 | 末态收敛（新库走改后 V1，旧库走 V5 删列）；spec 记录权衡 |
| 删列不可逆（数据丢失） | 中 | 用户已确认；`FOrgId` 已无功能、无索引；删前可由 DBA 自行备份 |
| 漏改某 `.FOrgId` 引用 | 低 | `dotnet build` 编译错误兜底 |
| 误删合法的 `FinVoucherEntry` 绕过 | 中 | 清单标注保留，按方法名+实体定位 |

## 8. 涉及文件清单

- **新增**：`src/STOTOP.Core/Models/IAccountSetScoped.cs`
- **改实体**（去 IOrgScoped + 删 FOrgId 属性）：`FinAccount.cs`、`FinAccountBalance.cs`、`FinAuxiliaryBalance.cs`
- **改配置**（删 FOrgId 映射）：`FinAccountConfiguration.cs`、`FinAccountBalanceConfiguration.cs`、`FinAuxiliaryBalanceConfiguration.cs`
- **清绕过 + 删写入**：`AccountService.cs`（删 12 绕过 + 2 写入，留 1 绕过）、`AmoebaPLService.cs`（删 5 绕过）
- **改种子**：`FinanceSeeder.cs`（909 条 FIN科目 INSERT 去列 + 新增 V5 步骤 MigrateV5）
- **小修**：`ReportService.cs`（补账套限定）
- **不改**：`STOTOPDbContext.cs`、`Repository.cs`、`IOrgScoped.cs`、`SeederHelper.cs`、`MigrationRunner.cs`
