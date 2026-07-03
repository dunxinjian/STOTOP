# 多租户隔离迁移 · 实施手册（拟议 · 配套 [23](23-multitenant-org-redesign.md)）

> **文档性质**：把 [23-multitenant-org-redesign.md](23-multitenant-org-redesign.md) 的目标态拆成可执行的迁移步骤，**尚未实施**。已对本仓库源码二次核验，命令/机制扣真实代码。
>
> **关键机制（已 Read 源码坐实）**：本仓库**不用 EF Core Migrations**（无 `Migrations/` 目录、启动不调 `Database.Migrate()`）。schema 演进靠 **Seeder 版本化迁移**——在 `src/STOTOP.WebAPI/Data/Seeders/{Module}Seeder.cs` 的 `steps` 列表末尾追加 `MigrationStep(int Version, string Description, Action<STOTOPDbContext> Execute)`，`MigrationRunner.RunMigrations` 启动期 `sp_getapplock` 排他锁、按 `SYS迁移历史` 跳过已执行、每步同事务原子。`ValidateSteps` 强制 `steps[0].Version==1` 且严格 +1 连续。新库建表/索引走 `DatabaseSeederAdapter.CreateMissingTables`/`CreateRelationalArtifacts`（非 SchemaAutoSync——后者只同步列、不碰索引/可空性）。

---

## 0. 总体策略与协作节奏

### 0.1 阶段依赖图

```
阶段0（地基·加列+回填+三重校验）   ← 不启用任何过滤器，纯数据准备
   │  产物：目标表有 F租户ID 列+索引，存量回填且三重校验通过
   ▼
阶段1（隔离硬墙切换）              ← M1+M2+M7；启用 ConfigureTenantFilter(fail-closed)
   │  产物：fail-closed 过滤器、写回填 throw、隔离自检(读+写+漏标)、admin 旁路收紧、seeder/任务豁免
   ▼
阶段2（组织模型重建）             ← M3+M4+M5；拆成员/任职、FKind 五值、闭包表、合法树 CHECK、网点出树、R8 ScopeGrant
   ▼
阶段3（财务对齐）                ← M6；账套双模、经营单元事件派生、双身份对账
   ▼
阶段4（身份/SaaS）              ← M8+M9；IdP 三表、待办分发、免登消歧、X-Tenant-Context、平台开通/计费/冻结
```

**硬依赖**：
- 阶段1 启用 fail-closed 过滤器**前**，阶段0 回填必须完成且三重校验通过——否则历史行 `F租户ID=null` 全部读空集（业务瘫痪）。
- 阶段0/1 改 `STOTOP.Core` + `STOTOP.Infrastructure` 底座 + WebAPI Seeder，被全模块依赖，每步全量回归。
- 阶段2 的 `FScopeRootId` 物化依赖阶段2 自建闭包表 + FKind 五值；**阶段0 回填只能依赖现有单组织邻接树 `SYS组织架构`**。

### 0.2 每阶段"可独立验证"产物

| 阶段 | 产物 | 验证 |
|---|---|---|
| 0 | `F租户ID` 列存在、索引建好、回填完成（含幂等标记）、三重校验全绿 | 启动跑管线 + 三个校验 SQL + 独立交叉源核对 |
| 1 | A 读不到 B、写不污染 B、无上下文 throw、漏标实体测试红、seeder/任务不炸启动 | `tests/STOTOP.Module.System.Tests`（新建）隔离自检 |
| 2 | 切换 O(1)、ResolveScopeRoot 范围根唯一、合法树 CHECK 拦非法父子 | 单测 + DB CHECK |
| 3 | 账套∈租户拒越权、经营单元随公司事件 1:1 派生 | Finance 单测 + IDOR 用例 |
| 4 | /api/platform/* 脱离过滤器、多租户 428、待办幂等键含租户 | 端到端 + IdP 单测 |

### 0.3 协作节奏（每阶段通用）

1. **先开特性分支**（除非已在特性分支）：`feat/tenant-isolation-stage0..4`。
2. **小步 build+test，注意编译边界**：
   - 改底座（`STOTOP.Core` 接口、`STOTOP.Infrastructure` 过滤器/回填）→ `scripts/dev/build-filter.ps1 core`（`core.slnf` = Core+Infrastructure+Module.System）。
   - 改实体属性 + Configuration（模块项目）→ 对应模块 `.slnf`。
   - **改 Seeder（在 WebAPI）→ 不能用 `.slnf`**（`core.slnf`/`finance.slnf` 均不含 WebAPI）。走 `scripts/dev/backend.ps1`（启动即编译 WebAPI）或全图 `dotnet build`。
3. **底座改动全量回归**：`scripts/dev/test-dotnet.ps1`（无 filter，自动发现全部 `*.Tests.csproj`）。`dotnet test` 用 x64 宿主；`CardFlow.Tests` flaky 多跑。
4. **验证后人工提交**；push 须用户点头。
5. 跨组件缝逐任务审 + 每阶段收尾整体终审。

---

## 1. 阶段0 实施手册（最详 · 可照着做）

> 在对应 `{Module}Seeder.cs` 的 `steps` 末尾追加 `MigrationStep`，版本号 = 当前末版本 +1；**同步在各模块项目改实体属性 + Configuration 的 `HasColumnName` 映射**（供全新库按模型建表）——一次跨两个项目的 split 改动。

### 步骤1：新增 `ITenantScoped` / `ISharedReference` 接口

新建（与现有 `IOrgScoped.cs`/`IOrgOwned.cs` 同目录同风格）：

```csharp
// src/STOTOP.Core/Models/ITenantScoped.cs
namespace STOTOP.Core.Models;

/// <summary>
/// 租户隔离接口（第 1 层硬墙）。实现此接口的实体进 fail-closed 过滤器：
/// 无租户上下文且非平台作用域 → 读空集 / 写 throw（不认 null、不认 0）。隔离键 = 区域公司（设计 §6.2）。
/// </summary>
public interface ITenantScoped
{
    /// <summary>F租户ID — R9 隔离根 = 区域公司。</summary>
    long FTenantId { get; set; }
}
```

```csharp
// src/STOTOP.Core/Models/ISharedReference.cs
namespace STOTOP.Core.Models;

/// <summary>
/// 跨租户共享参考数据标记接口（品牌、行政区划等）。走独立过滤器——
/// 根本不挂租户条件，绝不是 FTenantId==0（设计 §6.2 第2条）。仅作类型标记，无成员。
/// </summary>
public interface ISharedReference { }
```

**验证**：`scripts/dev/build-filter.ps1 core`。**风险**：低。
> ⚠️ 阶段0**只加列、不把 `ITenantScoped` 打到实体、不启用过滤器**。接口先就位供阶段1 引用。

### 步骤2：给需隔离的实体加 `F租户ID` 列 + 索引（先加列不启用过滤器）

**2a. 模型侧（各模块项目）**：每个需隔离实体加 `public long FTenantId { get; set; }`，在其 `IEntityTypeConfiguration<T>`：
```csharp
builder.Property(x => x.FTenantId).HasColumnName("F租户ID");
builder.HasIndex(x => new { x.FTenantId, x.FOrgId }).HasDatabaseName("IX_CRM客户_租户_组织");
```
> 新库 `CreateMissingTables` 按模型建表自带该列；`CreateRelationalArtifacts` 幂等补建索引（`IF NOT EXISTS sys.indexes` 守卫），二者在 `DatabaseSeederAdapter.cs`。索引交给声明式 `HasIndex` 避免模型漂移。

**【需实施时确认】需隔离实体精确清单**：Grep 现状 `IOrgScoped`/`IOrgOwned` 实现类作加列候选，再与设计 §4 目标表逐一映射、定全量 vs 分批。

**2b. 存量库侧（每模块在 WebAPI 的 Seeder 末尾追加版本步骤）**，N = 当前末版本 +1（如 Finance 现末版本 11 → 新步 12）：
```csharp
new(12, "阶段0: 加 F租户ID 列+回填 (2026-06-30)", MigrateV12),
```
```csharp
private static void MigrateV12(STOTOPDbContext ctx)
{
    if (!SeederHelper.IsSqlServer(ctx)) return;

    // batch① 幂等加列（先 NULL —— SchemaAutoSync 不改可空性，NOT NULL 留到回填+校验后另起步骤收紧）
    ExecSql(ctx, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = N'FIN账套' AND COLUMN_NAME = N'F租户ID')
    ALTER TABLE [FIN账套] ADD [F租户ID] bigint NULL;");
    // ……本模块每张需隔离表一段 IF NOT EXISTS ... ALTER ADD

    // batch② 回填（独立 ExecSql 调用 —— 见步骤3）
    ExecSql(ctx, @"/* 步骤3 的回填 UPDATE */");
}
```
> 加列与回填**必须拆成两个独立 `ExecSql` 调用**（SQL Server 延迟名称解析，同 batch ALTER ADD 后立即 UPDATE 新列会解析失败——`FinanceSeeder MigrateV7` 已踩过）。`ExecSql` 是各 Seeder 文件内私有静态助手：`private static void ExecSql(STOTOPDbContext ctx, string sql) => SeederHelper.ExecuteRawSql(ctx, sql);`。

**STG 前缀表例外**（`SchemaAutoSync`/`CreateMissingTables` 排除 `STG`/`HangFire`/`__EF`）：显式 ALTER + `COL_LENGTH` 守卫：
```sql
IF COL_LENGTH(N'STG申通派件日明细', N'F租户ID') IS NULL
    ALTER TABLE [STG申通派件日明细] ADD [F租户ID] bigint NULL;
```

**ValidateSteps 硬约束**：`steps[0].Version` 必须 == 1 且严格 +1 连续到 `Count`，否则整模块迁移抛异常。已有 Seeder 末尾追加安全；阶段2/3 **新建任何全新 Seeder**（`SYS租户成员`/`FIN经营单元` 等）其 `steps` 必须自带 V1..VN 完整序列。

**风险**：① prod 不能依赖 SchemaAutoSync 加列（只暂存到 `SYS_Schema同步记录`），**必须靠 Seeder 显式 ALTER 才在 prod 落地**；② **SystemSeeder 是 critical**（`CriticalModules=["System"]`，失败阻启动）——**【需确认】**SYS 表加列步骤放 SystemSeeder（失败阻启动、强一致）还是另起非 critical 业务 Seeder（失败仅警告）。

### 步骤3：回填（回溯"区域公司根" + 幂等键 + 事务/分批安全）

> 现状无 `FKind=区域公司`（五类节点是阶段2 目标态），阶段0 只能依赖现有邻接树 `SYS组织架构`（`F父ID` + `F可切换`）。

**3a. 先对真实 DB 导出树形核对**（源码 SQL 是 gzip+base64 压缩，读不出真实树形；解密 `db-connections.json` + 只读 `dotnet run`）：
```sql
SELECT FID, F名称 AS FName, F父ID AS FParentId, F类型ID AS FTypeId, F可切换 AS FCanSwitch
FROM [SYS组织架构] ORDER BY F父ID, FID;
```
**【需确认】**顶层可切换节点数、是否一一对应区域公司（单租户上线 N=1，太仓美申是否唯一根）、是否有"子公司下挂可切换分公司"。

**3b. 回填映射算法**：锚点 = **各自子树最高的 `F可切换=1` 节点**（取**最高**而非第一个——避免子公司下还有可切换节点把同一区域拆成多租户）。沿 `F父ID` 上溯取路径上最高可切换节点作租户根；visited 防环；`F父ID<=0` 停；整链无可切换 → 落树根并标记"需人工指派租户"。
> 该口径**有意偏离**现有 `FindSwitchableAncestor`（`OrgContextService.cs:41-57` 取最低/第一个），偏离已知且必要。

**3c. 回填 SQL — 修复两个阻断（幂等键 + 长事务锁表）**：

**阻断A：幂等不能靠 `WHERE F租户ID IS NULL`**（无法区分"未回填"与"裁决保留 NULL 的落空行"，重跑二次误填）。**用独立幂等水位列**：
```sql
IF COL_LENGTH(N'CRM客户', N'F租户回填状态') IS NULL
    ALTER TABLE [CRM客户] ADD [F租户回填状态] tinyint NULL;  -- NULL=未处理 1=已回填 2=落空待人工
```

**阻断B：回填 UPDATE 不能在持 applock 的迁移事务里对大表全表更新**（`MigrationRunner` 每步 `Execute` 包在事务内，长事务锁表卡死启动）。**大表把回填移出迁移事务、分批提交**：Seeder 步骤只做加列 + 加幂等列；大表回填用独立 `dotnet run` 脚本（关闭隐式事务、`TOP(5000)` 循环 + 每批独立提交）。**该回填须纳入 prod 发布 runbook**（database/*.sql / 外部脚本不被迁移管线自动跑）。

```sql
-- 分批回填（循环执行直到 0 行受影响）；递归 CTE 求"最高可切换祖先"
;WITH 路径 AS (
    SELECT o.FID AS 节点ID, o.FID AS 祖先ID, o.F父ID AS 父ID, o.F可切换 AS 可切换, 0 AS 层差
    FROM [SYS组织架构] o
    UNION ALL
    SELECT p.节点ID, a.FID, a.F父ID, a.F可切换, p.层差 + 1
    FROM 路径 p JOIN [SYS组织架构] a ON a.FID = p.父ID
    WHERE p.父ID > 0
),
租户根 AS (
    SELECT 节点ID,
           租户根ID = (SELECT TOP 1 祖先ID FROM 路径 x
                        WHERE x.节点ID = p.节点ID AND x.可切换 = 1
                        ORDER BY x.层差 DESC)        -- 最高（离根最近）
    FROM 路径 p GROUP BY 节点ID
)
UPDATE TOP (5000) c
SET c.[F租户ID] = r.租户根ID,
    c.[F租户回填状态] = CASE WHEN r.租户根ID IS NULL THEN 2 ELSE 1 END
FROM [CRM客户] c
LEFT JOIN 租户根 r ON r.节点ID = c.[F组织ID]
WHERE c.[F租户回填状态] IS NULL;     -- 真幂等：靠水位列，不靠 F租户ID IS NULL
```
> 性能优化：一次性载 `SYS组织架构` 全表入内存预计算 `OrgId→TenantRootId` 临时映射表再 JOIN 回填，避免逐行递归 CTE。
> **落空行绝不填 0**：`F组织ID` 空/0/指向已删除节点的行保持 `F租户ID=NULL`、`F租户回填状态=2`，单列出来人工裁决——填 0 会复刻"写0搭便车"后门。

### 步骤4：三重校验（修复"校验②自我确认"）

**① 旧 `F组织ID` vs 新 `F租户ID` 结果集 diff**（闭包子树下 `F组织ID` 行集 vs `F租户ID=该根` 行集应一致）：
```sql
;WITH 闭包 AS (
    SELECT FID AS 根, FID AS 后代 FROM [SYS组织架构]
    UNION ALL
    SELECT c.根, o.FID FROM 闭包 c JOIN [SYS组织架构] o ON o.F父ID = c.后代
)
SELECT t.租户根,
       旧口径行数 = COUNT(DISTINCT CASE WHEN cl.根 = t.租户根 THEN tab.FID END),
       新口径行数 = COUNT(DISTINCT CASE WHEN tab.[F租户ID] = t.租户根 THEN tab.FID END)
FROM [CRM客户] tab
LEFT JOIN 闭包 cl ON cl.后代 = tab.[F组织ID]
CROSS APPLY (SELECT 租户根 = cl.根) t
GROUP BY t.租户根
HAVING COUNT(DISTINCT CASE WHEN cl.根 = t.租户根 THEN tab.FID END)
     <> COUNT(DISTINCT CASE WHEN tab.[F租户ID] = t.租户根 THEN tab.FID END);
```

**② 独立交叉源核对**（①②若都用同一条"沿树回溯"口径，**无法发现"两侧都错但一致"的方向错**）。引入**与组织树无关的独立金标准源**：
- `FIN账套.F租户ID` 与该账套已绑定的法人/公司是否同租户（账套-公司绑定是独立事实源）；
- 抽 ~100 行金标准样本，人工查 `F组织ID → 节点名 → 实际所属区域公司`，**人工签字**回溯方向正确，覆盖各租户/各层级。

**③ `F父ID` 断链/多挂/成环检测**：
```sql
-- 断链
SELECT o.FID, o.F名称, o.F父ID AS 失效父ID FROM [SYS组织架构] o
WHERE o.F父ID > 0 AND NOT EXISTS (SELECT 1 FROM [SYS组织架构] p WHERE p.FID = o.F父ID);
-- 多根（单租户期应=1）
SELECT COUNT(*) AS 根节点数 FROM [SYS组织架构] WHERE F父ID <= 0;
-- 成环（深度异常）
;WITH 链 AS (
    SELECT FID, F父ID, 1 AS 深度, CAST(FID AS varchar(max)) AS 路径
    FROM [SYS组织架构] WHERE F父ID > 0
    UNION ALL
    SELECT l.FID, o.F父ID, l.深度+1, l.路径 + '/' + CAST(o.FID AS varchar(20))
    FROM 链 l JOIN [SYS组织架构] o ON o.FID = l.F父ID
    WHERE l.深度 < 50 AND CHARINDEX('/'+CAST(o.FID AS varchar(20))+'/', '/'+l.路径+'/') = 0
)
SELECT FID, 路径 FROM 链 WHERE 深度 >= 50;
```

### 步骤5：验收 + 回滚 + 编译回归

**验收**：☐ 列存在（dev+prod 都验，prod 靠 Seeder 显式 ALTER）☐ 索引建好 ☐ 回填完成+幂等水位列+落空行人工清单 ☐ 三重校验全过（含独立交叉源②）☐ `SYS迁移历史` 记录新版本+二次启动幂等 ☐ **未启用任何过滤器**（`STOTOPDbContext` 未动）。

**回滚**：阶段0 纯加列+回填最安全。撤回用新迁移步骤 `DROP COLUMN`（`IF COL_LENGTH IS NOT NULL` 守卫），**勿** rollback `SYS迁移历史`。跳整管线：`SKIP_DB_MIGRATION=true`。

**编译/回归**：底座→`build-filter.ps1 core`；实体/Configuration→模块 `.slnf`；**Seeder→不可用 .slnf，走 `backend.ps1` 或全图**；全量回归→`test-dotnet.ps1`。

---

## 2. 阶段1–4 路线图

### 阶段1（隔离切换 · 稍详）— M1+M2+M7

**目标**：启用 `ConfigureTenantFilter`(fail-closed) + 写回填 throw + 隔离自检（读+写+漏标）+ admin 旁路收紧 + **seeder/后台任务豁免**。`F组织ID` 降为第 2 层范围输入。改动集中在 3 个文件，复用现有"反射挂载 + IDisposable 作用域"机制。

**(1) 接口打到实体**：`class CrmCustomer : BaseEntity, ITenantScoped`；共享数据打 `ISharedReference`。

**(2) `STOTOPDbContext` 加 `ScopeState`**（`src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs`，照抄 19-22 `CurrentOrgId` + 172-185 `SuppressOrgIdFillScope` 的"标志+IDisposable"范式）：
```csharp
public sealed class ScopeState {
    public long? CurrentTenantId { get; internal set; }
    public bool  IsPlatformScope { get; internal set; }  // 仅平台/批量/seeder 工厂可置 true
}
private readonly ScopeState _scopeState = new();
public ScopeState ScopeState => _scopeState;
// IsPlatformScope 唯一产生途径：类型受限工厂 IPlatformScopeFactory.Enter(...) 返回 IDisposable，离开复位。
```
> **【需确认】`CurrentTenantId` 来源**：过渡期复用 `IOrgContextAccessor.CurrentOrgId`（`X-Org-Context` 暂当租户），头改名归阶段4；**阶段1 只加 ScopeState/过滤器/回填，不改头**。

**(3) `ConfigureTenantFilter`（fail-closed）— 必须新增第三轮 foreach**（在 `OnModelCreating` 115-134 现有 `IOrgScoped`/`IOrgOwned` 两轮之后，缓存反射 MethodInfo 仿 136-137/149-150）：
```csharp
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
        _configureTenantFilterMethod.MakeGenericMethod(entityType.ClrType)
            .Invoke(null, new object[] { modelBuilder, this });

private static void ConfigureTenantFilter<TEntity>(ModelBuilder mb, STOTOPDbContext ctx)
    where TEntity : class, ITenantScoped
{
    mb.Entity<TEntity>().HasQueryFilter(e =>
        ctx.ScopeState.IsPlatformScope
        || (ctx.ScopeState.CurrentTenantId != null && e.FTenantId == ctx.ScopeState.CurrentTenantId));
    // null 且非平台 → 恒 false（fail-closed）
}
```
> **最危险的漏标**：只定义接口不加第三轮循环 = 过滤器静默不挂、裸读全租户。**必须显式加循环**，由漏标自检兜住。闭包必须引用实例成员 `ctx.ScopeState.X`（不能捕获局部快照）。

**(4) 写回填 fail-closed throw**（在 `SaveChangesAsync`(160)/`SaveChanges`(166) 加 `FillTenantIdForNewEntities()`，与 `FillOrgIdForNewEntities` 并列，语义与旧相反）：
```csharp
private void FillTenantIdForNewEntities() {
    foreach (var e in ChangeTracker.Entries<ITenantScoped>().Where(x => x.State == EntityState.Added)) {
        if (_scopeState.IsPlatformScope) continue;
        if (_scopeState.CurrentTenantId is null)
            throw new InvalidOperationException("无租户上下文下禁止写入业务数据");   // → 400
        if (e.Entity.FTenantId == 0) e.Entity.FTenantId = _scopeState.CurrentTenantId.Value;
        else if (e.Entity.FTenantId != _scopeState.CurrentTenantId)
            throw new InvalidOperationException("跨租户写入被拒绝");                  // → 400
    }
}
```

**(5) ⚠️ seeder/启动期写入必须豁免**（`BasicDataSeeder`/`ExpressSeeder` 等走 EF `AddRange + SaveChanges` 在无租户上下文下播种，上线即被新 throw 炸启动）：所有启动期 seeder 入口包 `IPlatformScopeFactory.Enter(...)`。**【需确认】**盘点所有经 EF `Add+SaveChanges` 的 seeder/初始化入口（纯 ADO `ExecSql` 迁移步骤不走 `SaveChanges`，不受影响）。

**(6) 仓储 throw（IDOR，M7）**：`Repository.cs` `GetByIdAsync` 裸 `_dbSet.FindAsync(id)`（绕过滤器）→ 改 `Query().FirstOrDefaultAsync(e => e.FID == id)`（注意全局 NoTracking 身份冲突坑，可能需 `.AsTracking()`）。

**(7) admin 旁路收紧（M7）**：取消散落 `account=="admin"` 硬旁路 → `FIsPlatformAdmin` + `FScope=platform` + `IPlatformScopeFactory.Enter` + 审计。**【需确认】admin 硬旁路全部散落点**。

**(8) 非 HTTP 入口同步改造**：裸赋值点改走工厂 `Enter(固化 tenantId)`：`FlowEngineService.cs:178`、`ShentongUnificationJob.cs:65`、`CfAutoPluginRuleController.cs:44,86`。

**风险**：漏第三轮循环（裸读全租户）；闭包捕获快照；**别顺手删 `IOrgScoped` 过滤器**（切换期两层并存）；seeder/非HTTP 入口不豁免则启动/后台任务炸。

#### 阶段1 as-built（2026-07-01 已实施 · 分支 `feat/tenant-isolation-stage0`）

过滤器/写硬墙在 1a/1b + fan-out 时已启用；本轮补齐硬化 + 自检门禁：
- **受控平台作用域**：新建 `IPlatformScopeFactory`(Core) + `PlatformScopeFactory`(System，Scoped，与 `IOrgContextAccessor` 共享实例、Enter 置位/Dispose 复位为进入前值 + 审计日志)。`Program.cs` 三处启动块包 `Enter(...)`：`startup-migration`(MigrateAll)、`voucher-accountset-backfill`(凭证账套回填——原在无上下文下被 fail-closed 读空、静默失效)、`cli-init-database`/`cli-validate-database`(`--init-database` baseline)。**唯一** `IsPlatformScope=true` 产生途径。
- **IDOR**：`Repository.GetByIdAsync` 裸 `_dbSet.FindAsync(id)` → `AsTracking().FirstOrDefault(EF.Property<long>(e, 主键名) == id)`；主键名从模型元数据解析(**不硬编码 "FID"**——有实体主键名为 `Id` 如 `ExpPriceSurchargeScope`)。
- **补标 4 漏标**（有 FOrgId 却漏 ITenantScoped）：`FinAmoebaManualData`/`CfQualityRule`/`CfPluginExecution`/`QlKnowledge` + Configuration + `FinanceSeeder V14`/`CardFlowSeeder V61`/`QualitySeeder V3`(NOT NULL DEFAULT 0 + IX + 回填根租户，dev 库实跑)。
- **admin 口径统一（仅 consumer 侧）**：6 处散落判定(`ClaimTypes.Name=="admin"` / `F账号=="admin"` / `IsInRole("admin")`)收敛到中心 `IAdminAuthorizationService`。
- **隔离自检门禁**：`tests/STOTOP.Module.System.Tests`——写硬墙(无上下文/跨租户 throw、平台放行)、`GetByIdAsync` 主键解析+IDOR、**漏标扫描**(有 FOrgId/FOwnerOrgId 或 IOrgScoped/IOrgOwned 却缺 ITenantScoped → 红；排除 IStagingRecord/FAccountSetId 传递/白名单；含"每模块代表实体在册"防假阴性护栏)。全模块自动发现。

**阶段1 遗留收紧清单（FIsPlatformAdmin 完整 M7 · 下一 pass）**：
1. **admin producer 侧 + FIsPlatformAdmin**：admin 判定仍是三来源(OA_ADMIN Claim / F角色ID=1 / 残留 `Program.cs` 默认口令自检、`OrganizationService.EnsureAdminOrgAssociation` 的 `F账号=="admin"` 身份查找)。落 `FIsPlatformAdmin` + `FScope=platform` + 平台作用域接管 + 审计。**钉钉移动端 `DingTalkAuthController.GenerateJwtToken` 不签发 OA_ADMIN**——与 PC token 口径分裂(Task 控制器改认 OA_ADMIN 后，钉钉 token 走那些端点会丢 admin 待遇；当前移动端未调用故无活跃回归)，统一时一并补。
2. **STG 37 张暂存表 + Finance 账套传递族(科目家族/会计期间/账套模板/资产/汇率)**：仍未挂 ITenantScoped(门禁经 IStagingRecord/FAccountSetId 豁免记录)，全覆盖收尾时补 F租户ID(经 FAccountSetId→账套租户传递)。
3. **ambiguous 待裁决**：`CfNumberSequence`/`SysCodeSequence`(序列是否按租户重置)、`ExpLastMileStation`(合作驿站无组织→租户映射)、`FinAccountTemplate`(预置+自建混合，推迟阶段4)——在门禁白名单挂起。
4. **只读后台 Job 无上下文读空**：`WorkItemTimeoutJob`/`PushRetryJob` 等未走 9ca8184 设租户，fail-closed 下读空集(单客户期功能退化非安全洞)；随多客户上线用 `IPlatformScopeFactory` 或按 Job 目标租户设上下文。

> **遗留清单进度（截至 2026-07-02）**：② STG 37 表 + Finance 账套传递族 已全覆盖补标（分支 `feat/tenant-isolation-stage1-coverage`，`CardFlowSeeder V62`/`FinanceSeeder V15`）；③ ambiguous 已逐项裁定（`CfNumberSequence`/`SysCodeSequence`/`HrEmployee`/`ExpAgent`/`ExpLastMileStation` 挂租户，配置/字典/注册表入白名单，`FinAccountTemplate` 推迟阶段4；`SystemSeeder V4`/`ExpressSeeder V21`/新建 `HrSeeder`）；④ 12 个破损后台 Job 已修（分支 `feat/tenant-isolation-stage1-followup`，非 HTTP 入口设根租户 + `HttpOrgContextAccessor` override 转静态 `AsyncLocal` 穿透子作用域）。

#### 阶段1 M7 as-built（admin 平台旁路【审计优先】硬化 · 2026-07-02 · 分支 `feat/tenant-isolation-stage1-m7`）

M7 收敛为**审计优先硬化**（经用户裁定两条口径）：
- **`FIsPlatformAdmin` 平台身份模型 + 钉钉 `DingTalkAuthController` token 签 `OA_ADMIN` 口径统一 → 推迟阶段4**。二者真正价值是"平台超管 vs 租户管理员"分离（多客户 SaaS 才需要）+ 属授权授予类变更；现 admin 身份 `OA_ADMIN` claim + `F角色ID=1` 已可用。
- **admin 保持"租户内"**：租户硬墙仍作用于 admin；admin 组织切换只采信组织、**不**进平台作用域（进则越权跨租户，与 design/23 独立平台层冲突）。`IPlatformScopeFactory` 只留给真·平台跨租户操作（现 = 启动/种子/CLI 三入口）。

本轮已实施（零 schema / 零权限 / 零隔离边界变更，只加审计 + 门禁）：
- **平台作用域审计**：`PlatformScopeFactory.Enter` 写 `PlatformScopeEnter` 安全审计（`reason`→`FExtraData`），复用 `SecurityAuditService`（Dapper 直插、绕 EF 过滤器，启动期亦可写）；抽 `ISecurityAuditService` 接口以可测。**best-effort**：审计失败（如全新库首启审计表未建）仅 `LogWarning`、绝不中断平台操作。
- **admin 组织覆盖审计**：`OrgContextMiddleware` admin 传 `X-Org-Context` 覆盖组织归属处，对**变更类方法**(POST/PUT/DELETE/PATCH)写 `AdminOrgOverride` 审计（GET 压噪不写）；行为保持不变（仅设 `CurrentOrgId`、不进平台作用域）。
- **灰度开关**：`Security:AuditPlatformBypass`（默认开）可不重发布关闭两处审计。
- **门禁**：`PlatformBypassAuditTests`（7 用例）——平台作用域进入/恢复 + 审计事件 + best-effort 不阻断 + 灰度关；**admin 组织覆盖 `_next` 期间读 `IsPlatformScope==false`** 锁死"admin 保持租户内"决策（防将来误把 admin 包进 `Enter`）+ 审计写/GET 压噪/抛异常放行。System.Tests 16/16 绿。
- **保留类旁路不改**（已认可平台身份合法旁路且租户硬墙兜底）：`RequirePermissionAttribute`/`RequireAccountSetPermissionAttribute`/`HangfireDashboardAuthorizationFilter`/`AuthService` 登录期全量权限·菜单/启动期 `IgnoreQueryFilters` 身份查找/`DatabaseService.GenerateSetupToken`。`CardService.GetByIdAsync` 的 `canViewAll` 未被查询链消费（潜在功能缺陷）记独立 follow-up（属扩权、非本轮硬化）。

### 阶段2（组织模型重建）— M3+M4+M5

| M | 现状锚点 | 动作 |
|---|---|---|
| M3 | `SysUserOrganization.cs`、`OrgContextService.cs`（`FindSwitchableAncestor` 全表载内存回溯） | 拆 `SYS租户成员`(`SysTenantMember`) + `SYS任职`(`SysAppointment`)；切换列表查表 O(1)；废 `FindSwitchableAncestor` |
| M4 | `BasicDataSeeder.cs:101-112`（`SysOrgType` 1-9） | `FKind` 五值；建 `SYS组织闭包`(`SysOrgClosure`)；`(父,子)FKind` DB CHECK（支持跳级）；`FScopeRootId`/`FScopeRootType`/`FPath` 物化（树变更同事务重算） |
| M5 | `ExpNetworkPoint`（`IOrgOwned`/`FExpressBrand`） | 网点迁 `EXP网点`(`ExpOutlet`)，补 `(F网点公司ID, F品牌ID)` 唯一索引（D4：不建可视节点、不建 OUTLET 范围）；网点移出组织树 |

R8 落地：`SYS数据范围授权`(`SysScopeGrant`) + `RecomputeScopeGrants`（§7.2）+ `VisibleNodeIds` 二次夹逼（§7.3，落 `ApplyVisibilityScope` 仓储扩展，不进全局过滤器）。
> 新建 Seeder 其 `steps` 必须从 V1 起连续。
**风险**：双身份 `SYS网点公司↔SYS组织(Company)` 事务联动+对账；物化写放大；范围根物化与树变更同事务一致性（`FRowVersion`+快照隔离）。

#### 阶段2A as-built（M4 组织 schema 地基 · 2026-07-02 · 分支 `feat/tenant-isolation-stage2`）

先落 M4 地基（下游 R8/网点公司归属/闭包上卷都依赖）。**据 P0 真实树实测（dev 库 320 节点、单根 `MDSTO(FID=1)`、类型只用 GROUP×1/SUBSIDIARY×3/NETWORK_POINT(7)×4/DEPT×312）+ 用户裁定**：

- **FKind 保守映射**（`SysOrganization.FKind` 单一真源，`OrgKind`：0集团/1区域公司/2网点公司/3中心/4部门/5班组）：`MDSTO→集团`、`3 个 SUBSIDIARY→区域公司`、`4 个 type-7→网点公司`（这 4 个"XX子公司"实为太仓美申的网点公司=阿米巴 business_unit）、**其余 312 DEPT 全→部门**；中心/班组细分留后续业务。恰好 = 按 typeCode→FKind 查找回填。
- **合法树放宽**：允许 `部门→部门` 深链（现实"中心→网点→团"皆扁平 DEPT），否则严格 CHECK 拒现有数据。合法父子（含放宽）由物化的 `F父类别`(FParentKind) 做**行内 DB CHECK**（跨行父子约束普通 CHECK 无法表达，本仓零触发器先例故弃触发器）。
- **范围根 4 级**（`OrgScopeType`：`FScopeRootType` = 1集团/2区域公司/3中心/4网点公司）——**修订 design/23 §4.3/§7 的 3 级(TENANT/CENTER/COMPANY, TENANT 实为"区域公司")**：v2 集团=租户下辖多区域公司，需区域公司作独立范围层（区域用户只看本区域、集团总部汇总）。`ResolveScopeRoot`：最近网点公司→网点公司；否则最近"子树含网点公司的中心"→中心；否则最近区域公司→区域公司；否则集团。
- **组织树刻意不挂 fail-closed 硬墙**：`SysOrganization`/`SysOrgClosure` 加 `F租户ID` 列并物化，但**不实现 `ITenantScoped`**——组织树是租户结构骨架，在登录/切换等 `OrgContextMiddleware` skip 的引导路径被读取（未确立租户上下文），进硬墙会读空自锁。多租户组织可视性靠 R8 + 服务层租户过滤。漏标门禁只查有 `FOrgId` 的实体，组织树无 `FOrgId` 故合规不误报。
- **实现**：`SysOrganization` 加 8 列（F租户ID/F组织类别/F父类别/F所属网点公司ID/F范围根ID/F范围根类型/F路径/F版本号）；新 `SYS组织闭包`(SysOrgClosure，复合 PK)；`OrgTreeMaterializer`（合法规则 + ResolveScopeRoot + RebuildAll 全量物化+重建闭包，provider-agnostic 可 InMemory 测；OrganizationService 建/改/删后调用）；`SysOrgType` 加 `F组织类别`；`ValidateOrgTypeLevelAsync`(FLevel==父+1) → `ValidateOrgKindAsync`(FKind 对合法性)；`isCompanyLevel` 由 FCode 字面量 → FKind∈{集团,区域公司}。`SystemSeeder V5-V8`：V5 加列+索引、V6 FKind 映射回填、V7 RebuildAll 物化+建闭包、V8 CHECK（组织类别域/范围根类型域/合法父子；**须排 V6/V7 之后**——给已填充表加 CHECK 同步全表校验）。因保留 SysOrgType 查找表+typeCode/typeName 不变，**前端 DTO 契约不动、2A 无前端改动**。
- **验证**：全图 0 错；System.Tests 22/22（+6 OrgModelTests）；全量回归绿（唯 CardFlow 6 Excel 真文件非回归 baseline）；**真实 dev 库 V5-V8 跑通**：F组织类别 0:1/1:3/2:4/4:312、范围根类型 集团71/区域203/网点公司46、闭包1334行/自反320、3 CHECK 就位、0 不一致行。
- **待办**：fresh-DB V1 压缩 INSERT 需为列限定式（对 SYS组织架构 加列后仍安全；dev/prod 已有 V1 不重跑，fresh 库须核）；跨版本——V8 CHECK 要求非根行 F父类别 非空，旧代码(V4)建 org 不设 FKind/FParentKind 会撞 CHECK（共享库须全员前进 stage2）。

#### 阶段2B as-built（M3 成员/任职拆分 · 2026-07-02 · 增量安全）

用户裁定 **增量安全**（新表 + 回填 + O(1) 切换 + 双写；10 读消费者暂留旧表）：

- **新表**：`SysTenantMember`(SYS租户成员，跨租户 R6 切换依据，**刻意不实现 `ITenantScoped`**——一个用户可属多客户，切换须见其全部租户成员；无 `FOrgId` 故漏标门禁不触发) + `SysAppointment`(SYS任职，喂 R8 的 `FScopeEligible`，**实现 `ITenantScoped`** 入硬墙；`FOrgId` 为任职节点普通列、非隔离键，故**不**实现 `IOrgScoped`——R8 重算须跨用户全部任职读，按当前组织单节点过滤会漏)。两者均新 EF 表，由 `CreateMissingTables` 自动建。
- **切换列表 O(1)**：`SysOrganization` 加物化 `F可切换根ID`(=最近 `FIsSwitchable` 祖先含自身，`OrgTreeMaterializer` 计算)；`GetUserOrganizationsAsync` 重写为单查询(SYS用户组织当前行 → join 组织 → join 可切换根 → 去重)，**退役 `FindSwitchableAncestor` + 全表载入**，语义与旧一致。
- **增量双写(best-effort)**：`OrgContextService` 注入 `ITenantResolver`；建/改/删任职后 `SyncNewTablesBestEffortAsync`；`DingTalkService` 注入 `IOrgContextService`，部门同步每用户后 `ReconcileUserMembershipBestEffortAsync`(按当前 SYS用户组织 全量调和)。**best-effort**：`try/catch`+`LogWarning`——`SysAppointment` 在无租户上下文时撞 fail-closed 写硬墙被吞、绝不破坏主 SYS用户组织 写入。**10 个读消费者(EmployeeOrgQueryService/DingTalk/PerformanceService/ApproverResolver/RankingService/AuxiliaryService 等)仍读旧 SYS用户组织**——旧表退役 + 读消费者迁移留收尾。
- **admin 收敛**：`SwitchOrganizationAsync` admin 不再要求成员行(与切换列表/中间件一致)。新增 `/api/system/org-context/my-tenants`(阶段4 前端多租户切换用)。
- **Seeder**：`SystemSeeder V9`(加 `F可切换根ID`+索引+`RebuildAll` 重物化) / `V10`(raw SQL 从 SYS用户组织 回填成员[每用户一行·已接受·主租户]+任职[每行·主任职→可放大]，幂等)。
- **坑**：`STOTOP.Module.Task` 命名空间遮蔽 `System.Threading.Tasks.Task`(测试命名空间在 `STOTOP.Module` 下，enclosing-namespace 查找先于 using)→ 测试用 `using STT = System.Threading.Tasks;` 写 `STT.Task`。
- **验证**：全图 0 错；System.Tests 27/27(+5)；全量回归绿(CardFlow 6 Excel 非回归)；真实 dev 库 V9/V10 跑通——租户成员 1965(=去重用户)、任职 1997(=当前行)、主任职 1966 可放大 / 非主 31 不放大、`F可切换根ID` 全 320 非 0、承包区197→太仓美申192、0 孤儿任职。
- **待办**：旧 SYS用户组织 退役 + 10 读消费者迁到 SYS任职；`FScopeEligible` 目前由 `FIsPrimaryOrg` 派生(挂名/借调精细化留后)；多客户上线后 `SysAppointment` 写入须保证租户上下文(现 best-effort 在无上下文时跳过)。

#### 阶段2C as-built（M5 网点出树 + SYS网点公司 · 2026-07-02）

**P0 澄清**：组织类型7"快递网点"的 4 个节点(城区/南郊/沙溪/浏河子公司)语义即**网点公司**(=阿米巴 business_unit)，**非**要退役的"网点当 org 节点"；品牌侧网点(`ExpNetworkPoint`/`EXP快递网点`)本就是独立 `F编号` 表(早已出树)。故 2C = 把 4 个 `FKind=网点公司` 节点 formalize 为 `SysOutletCompany`，并给 `ExpNetworkPoint` 补公司/品牌列，**不退役 type-7**。

- **新表 `SysOutletCompany`(SYS网点公司)**：`BaseEntity`+`ITenantScoped`(无 `FOrgId`，门禁不触发)；`FOrgNodeId` 与 `FKind=网点公司` org 节点 1:1(唯一索引)+`FName`/`FCreditCode?`/`FRowVersion`。`SystemSeeder V11` 从 `FKind=2` 节点回填(dev 4 行，与节点数一致)。阶段3 的 `FIN经营单元` 将由它 1:1 派生。
- **`ExpNetworkPoint`(表名不改，避免破坏 schema-contract 测试 + 成本方案 raw SQL join)**：删死字段 `FEntityCompany`/`FExpressBrand`(代码零引用)；加 `FCompanyId`(`F网点公司ID`→SysOutletCompany)+`FBrandCode`(`F品牌编码`→`EXP品牌.F编码`；注意 `EXP品牌` 主键是 NCHAR(2) 字符串码非 FID，故用 code)+`(FCompanyId,FBrandCode)` 过滤唯一索引(R1)。`ExpressSeeder V22`：加列→回填 `FBrandCode` 自旧 `F快递品牌`→显式建过滤唯一索引→`DropColumnSafe` 退役死字段(drop 在回填后)。
- **数据现实**：现网网点均挂区域公司节点、**无网点公司映射**→ `FCompanyId` 暂空(网点→网点公司归属分配属**业务任务**)，过滤唯一索引因此休眠。
- **prod-safety(已核实)**：正常启动 `MigrateAll`→`CreateRelationalArtifacts(failOnError=false)`，故对既有表新列的声明式索引在 prod(列由 seeder 后加)于建索引时 **fail-soft(仅告警)**，真正建索引靠 seeder 显式 `CreateIndexIfMissing`(V5/V9/V22)。`InitializeNewDatabase`(CLI，fresh)才 `failOnError=true`，但 fresh 库列已由 `CreateMissingTables` 建齐。→ **2A/2B/2C"给既有表加列+声明式索引"模式 prod 安全**。
- **验证**：全图 0 错；System.Tests 27/27、Express.Tests 74/74(schema-contract 绿)；真实 dev 库 V11/V22 跑通(SYS网点公司 4 行 1:1、`FBrandCode` 16 ST、死列已删、新列+索引就位)。
- **待办**：网点→网点公司归属分配(业务)；阶段3 `FIN经营单元` 由 SysOutletCompany 1:1 派生 + 阿米巴 `business_unit`/`GetUnitsTree` 迁到闭包上卷。

#### 阶段2D as-built（R8 数据范围地基 + 门禁 + 试点 · 2026-07-03）

**纯净新增**——此前 org 过滤器只夹单节点(`FOrgId==CurrentOrgId`、无子树)。本轮只落**地基/引擎/回填/测试**，**不铺开全模块** `ApplyVisibilityScope` 接入(单租户可视域退化为整棵树、当期零功能收益)。

- **新表 `SysScopeGrant`(SYS数据范围授权)**：`BaseEntity`+`ITenantScoped`(无 `FOrgId`，门禁不触发)；`FUserId`/`FTenantId`/`FScopeType`(1集团/2区域/3中心/4网点公司)/`FScopeNodeId`(=物化范围根)/`FScopeAction`(Read/Write/All)/`FGrantSource`(派生/手工)/`FApprovalId?`/`FExpireAt?`。
- **引擎 `ScopeGrantService`**：`RecomputeScopeGrantsAsync`(§7.2：删旧派生 → 当前可放大任职取物化 `FScopeRootId/Type` → **集团级归一** → 写 Read 派生授权)；`GetVisibleNodeIdsAsync`(§7.3：授权过硬墙 → 集团级=整租户树、否则经 `SYS组织闭包` 展开子树 → `FTenantId` **二次夹逼** → 空=fail-closed)；`AddManualGrantAsync`(D6：`(Write/All,集团)` 无 `FApprovalId` 拒)。
- **`ApplyVisibilityScope<T:IOrgScoped>` IQueryable 扩展**(Infrastructure，跨模块可复用)：**刻意不进全局过滤器**，逐查询 opt-in；注释明示 fail-open 风险。
- **hook**：`OrgContextService` best-effort 双写后调 `RecomputeScopeGrants`(`CurrentTenantId` 与硬墙同源；`DetachPendingMembershipEntities` 已含 `SysScopeGrant`)。`SystemSeeder V12` set-based 回填各用户派生授权 + 集团归一。
- **生产查询接入=有意 SKIP**：fail-closed 下无授权用户会被锁死、单租户零收益 → 本轮只提供扩展+引擎+回填+测试证端到端；生产逐查询 opt-in 留后续(admin/无授权兜底、别锁死)。
- **验证**：全图 0 错；System.Tests 34/34(+7)；全量回归绿；真实 dev 库 V12 跑通(授权 集团400/区域1275/网点公司290、1965 授权用户=1965 可放大任职用户、集团归一 0 违、0 非范围根授权、承包区用户→网点公司级)。

> **阶段2 收口**：四子阶段(M4/M3/M5/R8地基)全部实现 + dev 验证 + per-sub-phase rule-review。待跨子阶段整体终审(2A 物化 → 2B 任职 `FScopeEligible` → 2D 派生授权 的链路缝)+ 全量回归 + 用户提交。

### 阶段3（财务对齐）— M6

| 现状锚点 | 动作 |
|---|---|
| `FinAccountSet.cs:16`（`long FOrgId`） | `FOrgId → FCompanyId`(可空) + `FAccountSetBindMode`（D2） |
| `RequireAccountSetPermissionAttribute.cs` | 补"账套∈租户"校验；`X-AccountSet-Id` 消费前校验账套租户归属（堵已确认 IDOR） |
| `business_unit`（`BasicDataSeeder.cs:151-181`，6 行） | `FIN经营单元`(`FinOperatingUnit`) 物化表 + 领域事件，`FCompanyId` 1:1，公司停用联动停用 |

> **【需确认】`FinAccountSet.FOrgId` 现网指向**（公司节点 vs 树根；种子里账套1=2、账套2=192 似指具体节点，以现网为准）。

> **3A/3B/3C 已实施 + dev 验证**（分支 `feat/tenant-isolation-stage3`）：
> - **3A**（`FinanceSeeder V16`）：`FinAccountSet` 加 `FCompanyId`(可空)+`FAccountSetBindMode`(NOT NULL DEFAULT 1)；`RequireAccountSetPermissionAttribute` 补账套∈租户校验（经 `FIN账套` 硬墙 `AnyAsync`，他租户账套 403）。
> - **3B**（`FinanceSeeder V17`）：`FinOperatingUnit`(FIN经营单元) 从 `SysOutletCompany` 1:1 物化派生（`FinOperatingUnitDeriver`，dev 4 行 OU-1..4）。
> - **3C**（`FinanceSeeder V18` schema + `BasicDataSeeder V1` 建桥）：**方案A（最小改动、P&L 叶数中性）**。`FinOperatingUnit` 加 `F来源类型`/`F来源业务单元ID`(→`FIN辅助核算项目.FID`) 双向**交叉引用桥**：按 (租户, 公司名去"子"→规范名) 匹配 `business_unit` aux（名不一致"城区子公司"vs"城区公司"故禁纯名/纯码 join）——4 网点公司 aux 建桥，**出港业务(方向)/太仓美申(区域自身) 不桥**（避免区域重复计数）；反标被桥 aux 的 `F来源类型='FIN经营单元'`（消 `FSourceType` 恒 null 缺口）。阿米巴报表 `AmoebaPLService`：`business_unit` aux 仍作分组键（`MapToUnit`/映射规则/凭证**零改**→叶数逐行不变），新增 `BuildUnitRegionParentMapAsync` 经闭包最近区域公司(`FKind=Region`)填 `AmoebaUnitData.ParentId`（区域上卷，**纯附加**）。
>   - **建桥点时序（关键坑，首轮对抗审查 CONFIRMED）**：`business_unit` aux 由 `BasicDataSeeder`(BasicData tier) 播种，**晚于** Finance tier——故 Finance `V18` 的 Deriver 在 **fresh 库**上 aux 尚不存在→桥空。修复：`V18` 覆盖 **existing-DB 升级**（aux 已在），`BasicDataSeeder V1`(SeedBUAuxiliary 之后) 调 Deriver 覆盖 **fresh-DB 首建**，两处幂等各建一次。
>   - dev 实测：凭证引用 business_unit=0 行、KSF 全 0 行、CfPluginRule pin business_unit=0 行——迁移唯一活跃面 = 5 条 `FinAmoebaMappingRule` + 报表；故 KSF/凭证/Points **零改**（code path 保留，桥兼容）。**区域上卷取数**（`AmoebaReportScope.Region` 过滤 + 件量分摊分母 + 前端区域树）= **有意拆后续 part-2**（见 [[business-unit-vs-outlet-company-modeling]] "拆两段"）。

### 阶段4（身份/SaaS）— M8+M9

| M | 现状锚点 | 动作 |
|---|---|---|
| M8 | `SysUser` 钉钉字段、`AuthController.LoginAsync` | 钉钉字段迁 `IDP用户身份`，加企微 + `IDP企业租户映射`(N:N) + `IDP部门映射`；免登多租户强制 428；待办分发幂等键含租户；成员加入须邀请确认 |
| M9 | `X-Org-Context`、`web/src/api/request.ts` | 头语义改 `X-Tenant-Context`，428 保留；前端注入 + 组织树聚合层（组织切换 keep-alive 失效强制重挂载） |

平台层：`/api/platform/*` 物理脱离租户过滤器、`PlatformAuditMiddleware`、`PLT租户/PLT套餐/PLT订阅`、欠费冻结白名单（D7：放行结账类只读，禁批量导出）。

---

## 3. 贯穿事项

### 3.1 build+test 命令边界

| 场景 | 命令 |
|---|---|
| 改底座 Core/Infrastructure | `scripts/dev/build-filter.ps1 core` |
| 改实体/Configuration（模块项目） | 对应模块 `.slnf` |
| **改 Seeder（WebAPI）** | **不可用 .slnf**；`scripts/dev/backend.ps1` 或全图 |
| 底座改动全量回归（必做） | `scripts/dev/test-dotnet.ps1`（无 filter） |
| 单跑隔离自检 | `scripts/dev/test-dotnet.ps1 System` |
| 启动跑迁移管线 | `scripts/dev/backend.ps1` |

### 3.2 隔离自检纳门禁

**新建中性测试项目** `tests/STOTOP.Module.System.Tests/`（当前不存在）。两类测试：
- **(a) 读/写隔离自检**：拷 Finance 版 `TestDbContextFactory.Create(name, orgId)`（InMemory + `RegisterModuleAssembly`）；播他租户数据（`IgnoreQueryFilters()` + `SuppressOrgIdFill()` 或显式非 0 `FOrgId`）；断言读隔离 + 写不污染。阶段1 后新增"无上下文(null) → 读空集 / 写 throw"用例（`HasQueryFilter` 在 InMemory 生效，可复现）。
- **(b) 漏标扫描**：一个 `[Fact]`：`RegisterModuleAssembly` 所有业务模块 → `ctx.Model.GetEntityTypes()` → "继承 `BaseEntity` 且不在共享/平台白名单"却未实现 `ITenantScoped` 者 → 非空即 `Assert.True(false)`。**须有"关键模块已注册/注册数>阈值"防回退**（漏注册=假阴性全绿）。**【需确认】`ISharedReference` 白名单边界**。

**纳门禁**：① 轻量（先做）——`scripts/dev/hook-precommit-gate.ps1` 编译通过后，仅当改动触及 `Core`/`Infrastructure`/`*Entities*`/隔离接口 `.cs` 时追加 `test-dotnet.ps1 System`，失败 Deny；② 正式 CI（仓库现无 `.github/workflows/*.yml`，需新建）。
> 新建 `System.Tests` 考虑新增 `system.slnf`（`build-filter`/`.slnf` 不自动纳入新测试项目）。

### 3.3 回滚

| 阶段 | 回滚要点 |
|---|---|
| 0 | 最安全。撤回用新迁移步骤 `DROP COLUMN`（幂等守卫），勿 rollback `SYS迁移历史`。`SKIP_DB_MIGRATION=true` 跳整管线。 |
| 1 | 风险最高。回滚=移除第三轮 `ConfigureTenantFilter` + `FillTenantId` 调用（纯代码、无 schema 变更）。建议灰度：先小租户子集开过滤器观察。 |
| 2-4 | 物化写放大→回滚需同步清物化表；组织树重建不可逆性高，**务必先在副本库演练**。 |

---

## 4. 待实施时确认清单

1. 阶段0 需隔离实体精确清单（`IOrgScoped`/`IOrgOwned` 实现类 ↔ 设计 §4 目标表）。
2. 真实组织树形（`SYS组织架构` 顶层可切换节点数、是否一一对应区域公司、有无"子公司下挂可切换分公司"）。
3. SYS 表 `F租户ID` 步骤放 SystemSeeder（critical）vs 业务 Seeder。
4. 阶段1 租户上下文来源（复用 `IOrgContextAccessor.CurrentOrgId` 过渡 vs 新建 `ITenantContextAccessor`+中间件）。
5. admin 硬旁路全部散落点。
6. 所有经 EF `Add+SaveChanges` 的 seeder/初始化入口（阶段1 须包 `IPlatformScopeFactory.Enter` 豁免）。
7. `FinAccountSet.FOrgId` 现网指向（公司节点 vs 树根）。
8. `ISharedReference` 白名单边界（品牌/行政区划/平台级）。
9. 大表回填分批脚本是否纳入 prod 发布 runbook。
10. CI 形态、全量 `test-dotnet.ps1` 时长/资源预算。

---

## 5. 关键文件锚点（绝对路径，已核验）

- 接口：`src/STOTOP.Core/Models/{ITenantScoped,ISharedReference}.cs`（新建）、`IOrgScoped.cs`/`IOrgOwned.cs`（参照）
- 过滤器/回填：`src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs`（`OnModelCreating` 115-134 两轮循环、`SaveChanges(Async)` 160/166、`FillOrgIdForNewEntities` 187、`SuppressOrgIdFillScope` 172-185）
- 仓储 IDOR：`src/STOTOP.Infrastructure/Repositories/Repository.cs`（`GetByIdAsync` 18、`Query` 51）
- 迁移机制：Seeder 均在 `src/STOTOP.WebAPI/Data/Seeders/`；`MigrationStep`/`MigrationRunner`（`RunMigrations` 103、每步事务 145-163、`ValidateSteps` 251、`AcquireAppLock` 277）在 `MigrationRunner.cs`；`FinanceSeeder.cs`（`ExecSql` 16-19、`steps` 23-37 末版本11、`MigrateV7` 加列+回填分 batch）；`SeederHelper.IsSqlServer`/`ExecuteRawSql`
- 表/索引建立：`src/STOTOP.WebAPI/Data/DatabaseSeederAdapter.cs`（`CreateMissingTables`/`CreateRelationalArtifacts`，**非 SchemaAutoSync**）
- 回溯参照：`src/STOTOP.Module.System/Services/OrgContextService.cs`（`FindSwitchableAncestor` 41-57）
- 组织实体/种子：`SysOrganization.cs`、`BasicDataSeeder.cs`（`SysOrgType` 101-112、business_unit 151-181）
- 非 HTTP 裸赋值：`FlowEngineService.cs:178`、`ShentongUnificationJob.cs:65`、`CfAutoPluginRuleController.cs:44,86`
- 测试范式：`tests/STOTOP.Module.Finance.Tests/{TestDbContextFactory.cs,Voucher/VoucherServiceIsolationTests.cs}`、`tests/STOTOP.Module.CardFlow.Tests/{TestOrgContextAccessor.cs,Rules/FlowDefinitionOrgIsolationTests.cs}`
- 门禁/过滤器：`scripts/dev/hook-precommit-gate.ps1`、`src/core.slnf`/`finance.slnf`（**均不含 WebAPI**）、`scripts/dev/{build-filter,test-dotnet}.ps1`

---

## 6. 从这里开始

**动手前先 Read 两个文件坐实 split 改动模式**：① `src/STOTOP.WebAPI/Data/Seeders/CrmSeeder.cs`（确认 `ExecSql` 助手形态、`steps` 末版本号）；② `src/STOTOP.WebAPI/Data/DatabaseSeederAdapter.cs`（确认新列/新索引处理）。

**第一个动手的文件**：新建 `src/STOTOP.Core/Models/ITenantScoped.cs`（零风险、可立即 `build-filter.ps1 core` 验证），作为整个迁移的锚点接口。
