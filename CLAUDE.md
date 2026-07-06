# CLAUDE.md

本文件供 Claude Code / AI 助手在本仓库工作时作为上下文加载。**完整开发规范以 [`design/21-dev-rules.md`](design/21-dev-rules.md) 为单一真源**；本文是其面向 AI 的精简索引，只保留最易违反的红线与 AI 专属的运行/提速/记忆指引。技术栈：后端 .NET 10 / ASP.NET Core / EF Core / SQL Server / Hangfire / SignalR；前端 Vue 3 / Vite / TypeScript / Pinia / Ant Design Vue（PC）+ Vant（移动端）。

默认用中文交流。**以代码为准**；本文若与代码冲突，先核对代码再更新本文与 `design/21`。最后核对：2026-07-06 / 分支 `feat/platform-admin-console`。

## TL;DR（30 秒）

- 后端 `:9000` / 前端 `:9001`；启动用 `scripts/dev/` 或 slash `/module` `/build` `/test` `/check` `/precommit` `/scaffold`（新建后端模块）`/rule-review`（规约审），别手敲 dotnet/vite。
- 只改单模块用 `.slnf` 闭包提速（`/build <name>`），别加载全图（生产 23 项 `.csproj`）。
- 提交前须过：`.cs` 编译门禁（hook）+ 前端 `type-check` + `lint:style` + 裸 hex 门禁。
- **多租户已上线**：`ITenantScoped` 实体走 fail-closed 硬墙——无租户上下文会读空集/写抛异常。写实体/查询/Job 前先读 [§多租户](#多租户与平台隔离硬约束)。
- 存疑先查 memory 索引与模块级 `src/STOTOP.Module.<X>/CLAUDE.md`；不擅自开分支/push。

## 0. 运行与边界

- 后端 `:9000`，前端 `:9001`；代理 `/api`、`/hangfire`、`/hubs`；健康检查 `/health`，版本 `/api/version`。启动用 `scripts/dev/`（`backend.ps1`/`frontend.ps1`/`check-health.ps1`）。
- 系统数据库连接的运行时来源是 `src/STOTOP.WebAPI/db-connections.json`（不是 `appsettings.json`，也不在仓库根）。
- **大库提速**：`src/*.slnf` 只编该模块依赖闭包（现有 `cardflow`/`express`/`finance`/`crm`/`task`/`core`/`dormitory`；`scripts/dev/build-filter.ps1 <name>` 或 `/build`）。测试用 `scripts/dev/test-dotnet.ps1 [filter]` 或 `/test`（自动发现 `tests/**/*.Tests.csproj`）。`cardflow.slnf` 不含 `CardFlow.Tests`（引 WebAPI 会拉全图）。
- **两道提交门禁**：`.husky/pre-commit` 只拦新增裸 hex；改动 `.cs` 的**编译门禁**是 Claude Code hook `scripts/dev/hook-precommit-gate.ps1`（编译失败即拒提交，不跑测试/type-check）。
- **后端运行时锁 `STOTOP.WebAPI.dll`**：全图 build 用 `-o <scratch>` 或 `/p:UseSharedCompilation=false` 绕锁，或先停后端。
- **无 EF Migrations**：schema/数据靠启动 seeder + `Data/Seeders/Baseline/baseline-reference-data.json` 对齐；改动走版本化 seeder（V 编号），规则体放 `Data/Seeders/Resources/*.json`。不要 `dotnet ef migrations`。两坑：原生 SQL 用 `SeederHelper.ExecuteRawSql`，别用 EF `ExecuteSqlRaw`（无参也 `String.Format`，SQL 里 `{}` 抛 FormatException）；改 baseline JSON 后须重建到 bin（`ResolveBaselinePath` 首选 `AppContext.BaseDirectory` 的 bin 副本，否则跑旧文件）。
- **进模块前看** `src/STOTOP.Module.<X>/CLAUDE.md`（目前 CardFlow 有），其坑位优先于泛化规则。
- **架构边界（硬约束）**：新审批/动态表单/节点流转/卡片待办进 **CardFlow**；Workflow 只做事件/派发/质量底层能力，不复制 CardFlow 运行时；OA 只作历史兼容（不注册控制器）；**不新建 `STOTOP.Module.DataCenter`，也不新建 `STOTOP.Module.Platform`——平台/租户能力全在 System 模块**。

## 多租户与平台隔离（硬约束）

> stage0-4 已落地并织入生产路径（`STOTOPDbContext`/`Program.cs`/中间件/System）。**新写实体/查询/Job/控制器前必读**。完整说明见 [`design/21` §1.5](design/21-dev-rules.md) 与 `design/23-25`。

- **租户过滤器 fail-closed 硬墙**：`ITenantScoped` 实体（键 `F租户ID`）——平台作用域放行，否则须当前租户非空且相等。**无租户上下文 → 读空集、写抛异常（不认 null、不认 0）**（对比组织过滤器 null 时放行）。
- **绕过唯一入口 = `IPlatformScopeFactory.Enter`**（平台/seeder/迁移）；业务 Service 注入它 = 越权后门。**非 HTTP 链路（后台/批次/CLI）**读写租户数据须显式 `ITenantScopeFactory.Enter(tid)` 或 `IPlatformScopeFactory.Enter(reason)`，否则读空/写崩。
- **组合过滤器**：同实体 `IOrgScoped+ITenantScoped` 由 DbContext 一次性 AND 组合；EF `HasQueryFilter` 覆盖非叠加，**绝不分轮各调一次**。
- **后台 Job** 不设根租户跑全库，用 `ITenantIterationService.ForEachActiveTenantAsync` 逐活跃租户；批次链用 `ITenantResolver.ResolveTenantForOrg(batchOrgId)`，**绝不一律 `GetRootTenantId`**。单客户下退化 1 次。
- **平台层** `/api/platform/*` 脱离租户上下文，**必须标 `[PlatformOnly]`**（校验 `SysUser.FIsPlatformAdmin`）；中间件顺序 `TenantFreezeMiddleware` 须在 `OrgContextMiddleware` 之后（欠费冻结返 402）。
- **R8 数据范围 fail-open**：`SysScopeGrant` 跨节点可视域**不在全局过滤器**，列表/报表须显式 `ApplyVisibilityScope(...)` opt-in，漏调只受租户墙+单节点组织约束。
- **新增受租户隔离实体**：实现 `ITenantScoped`（加列 `F租户ID`），建表/改列走版本化 seeder（V 编号）**不用 migrations**；参照 System 模块 `Sys*` 实体既有写法。

## 1. 后端红线速查（细则见 `design/21` §1）

- 分层：`Controllers/Services(+Interfaces)/Entities/Configurations/Dtos`（+ `EventHandlers/Events/Filters/Jobs`）。模块 `Add{Module}Module` 扩展；**CardFlow 必须早于 Express 注册**。
- Controller：`[Route("api/{module}/{resource}")]` 全小写（如 `api/crm/bonus`）；`[RequirePermission("module:resource:action")]`（如 `crm:bonus:view`；action 用 view/create/update/delete）；返回 `ApiResult`/`ApiResult<T>`（camelCase）。携带数据的成功是**泛型** `ApiResult<T>.Success(data)`；非泛型 `ApiResult` 只有 `Ok(message)` / `Fail(message, code)`。
- Service：`IXxxService` + `Async` 后缀；优先 `IRepository<T>`（`Query()`/`AddAsync/UpdateAsync/DeleteAsync` **即时**落库——`AddAsync` 立即 `SaveChanges` 取自增主键；多写/跨仓储事务直接用 DbContext）。两个"真库崩、InMemory 假绿"坑：① 真库事务须 `IExecutionStrategy.ExecuteAsync` 包裹（裸 `BeginTransaction` 撞 `EnableRetryOnFailure` 100% 崩）；② 全局 `NoTracking`——重查已跟踪实体再 `Update`/`Entry().State` 撞 identity conflict，须 `.AsTracking()`。
- 数据/命名：单一 `STOTOPDbContext`；`BaseEntity(long FID)`/`BaseGuidEntity(FUID)`。**DB 列名 `F+中文`，C# 属性 `F+PascalCase`，`HasColumnName` 映射**（属性 `FStatus`/`FOrgId` → 列 `F状态`/`F组织ID`）。表名 = 英文前缀（`CRM`/`CF`/`SYS`/`PLT`…）+ 中文名（如 `CRM客户`/`CF操作日志`/`CON合同`）。字符串编号属性 `FCode` 的中文列名 **`F编号`/`F编码` 双轨并存**（勿假定唯一，映射错=建表/查询错）。隔离接口 `IOrgScoped(FOrgId, null 放行)` / `ITenantScoped(F租户ID, fail-closed)` / `IAccountSetScoped(与组织互斥)`。固定系统字段含 `F版本号`(并发)/`F状态`。
- 异常：`GlobalExceptionMiddleware` 拦截（`Unauthorized→403`/`InvalidOperation→400` 透传消息/其余 500）。**消息/日志用中文，代码标识用英文 PascalCase**。
- Hangfire：Job 类放模块 `Jobs/`（参照 Contract/CardFlow），`RecurringJob.AddOrUpdate` 集中在 `Program.cs` 注册；跨租户 Job 用 `ITenantIterationService` 别 `GetRootTenantId`（见 §多租户）。SignalR `/hubs/*`。

## 2. 前端红线速查（细则见 `design/21` §2）

- API：单一 axios `web/src/api/request.ts`（`/api`，15s）；请求注入 5 头 `Authorization`/`X-Device-Fingerprint`/`X-Org-Context`/`X-Tenant-Context`/`X-AccountSet-Id`；响应 401 刷新队列/403→`/403`/428 提示选组织租户/Blob 直通。类型从 `@/types/{module}` 导入。
- 状态：setup store `defineStore('xxx', () => {...})`，`useXxxStore`。
- 路由：三层（静态 / `/m/*` / `Layout`），**无 `/admin` 路由层**；管理页是 `Layout` 子路由，门禁按 `getCurrentModuleMenus(moduleCode).length>0` 失败跳 `/403`；`platform/*` 仅平台超管。
- 样式：**禁裸 hex**，用 `var(--token)`（`stores/theme.ts`）/ SCSS `$`（`variables.scss`），真源 `web/docs/TOKENS.md`；豁免见 `.husky/pre-commit`。范式类 `.page-container/.page-card/.page-toolbar`。
- 组件：PascalCase 自动导入（`components.d.ts` **不手改**）；AntD `A` 前缀；CardFlow 字段组件在 `components/cardflow/fields/`；PC/移动端命名隔离。
- TS：`strict:true`，**禁裸 any**；`npm run type-check` 须过。多入口 `index/mobile/redirect.html`，别名 `@`→`src`。
- 新增页：照抄成熟模块四件套 `api/{m}.ts`+`types/{m}.ts`+`stores/useXStore.ts`+`views/{m}/*.vue`（如 crm）；路由由后端菜单动态注入，别手写 `addRoute`。

## 3. 测试（细则见 `design/21` §3）

- xUnit；`tests/STOTOP.Module.*.Tests`；中文 `[Fact]` 方法名。各模块用其 `TestDbContextFactory.Create(...)` + InMemory + `TestOrgContextAccessor` + `RegisterModuleAssembly(...)`。
- 租户/平台自检在 `System.Tests`（`Tenant*`/`Platform*`，含漏标门禁 `TenantLeakScanTests`）；`CardFlow.Tests`/`WebAPI.Tests` 拉近全图、flaky，多跑几次。

## 4. 协作约定 + 存疑时

- 缩进 `.cs`=4 / 其余=2 / `.sln`=Tab；utf-8/lf/文末换行。设计文档 `design/NN-*.md`，新增模块同步 `design/00-overview.md` 与 `Program.cs` 顺序。SDK 锁 `global.json`。
- **并发会话共用主工作树有风险**，隔离用 git worktree（`.claude/worktrees/`）；探索忽略该目录与 `bin/obj`。
- 探索某模块落点用子代理 `module-explorer`（只读定位实体/服务/控制器/前端）；提交前规约审用 `rule-reviewer`（或 `/rule-review` `/precommit`）。
- **存疑时**：不擅自开分支/push（须点头）；大改先出 plan+锁模块+小步 build/test；子代理逐任务后必做整体终审+回归；DB/模型不一致先据设计意图判哪侧对；先查 memory 索引复用既有结论。

## 绝不（Never）

手敲 dotnet/vite ・ 新建 DataCenter/Platform 模块 ・ 注册 OA 控制器 ・ 审批流写进 Workflow ・ 业务 Service 注入 `IPlatformScopeFactory` 绕租户墙 ・ 后台 Job 一律 `GetRootTenantId` 全库 ・ 同实体分轮 `HasQueryFilter` ・ `dotnet ef migrations` ・ 手改 `components.d.ts` ・ 裸 hex / 裸 any ・ 擅自开分支/push ・ 后端运行时全图 build 不加 `-o scratch`。

## 文档入口

- [系统总览](design/00-overview.md) ・ [开发规则（单一真源）](design/21-dev-rules.md) ・ [Claude 开发流程](design/22-claude-workflow.md) ・ [WebAPI 启动层](design/19-webapi.md) ・ [前端架构](design/20-frontend.md) ・ [多租户重设计](design/23-multitenant-org-redesign.md)
