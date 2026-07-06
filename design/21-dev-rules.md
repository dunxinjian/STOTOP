# 开发规则（STOTOP）

> 提炼自 `design/` 设计文档与实际代码约定（**以代码为准**）。技术栈：后端 .NET 10 / ASP.NET Core / EF Core / SQL Server / Hangfire / SignalR；前端 Vue 3 / Vite / TypeScript / Pinia / Ant Design Vue（PC）+ Vant（移动端）。
> **本文档是开发规范的单一真源（single source of truth）。** 仓库根 `CLAUDE.md` 是面向 AI/快速参考的精简索引，只保留最易违反的红线与 AI 专属指引，不再逐字复制本文；细则一律以本文为准。
> 最后核对：2026-07-06 / 对应分支 `feat/platform-admin-console`（多租户 stage0-4 + 平台管理台已实施）。

## 0. 运行与边界

- 后端 `:9000`，前端 `:9001`；前端代理 `/api`、`/hangfire`、`/hubs` 到后端。健康检查 `/health`（就绪探针 `/health/ready`），版本 `/api/version`。
- 启动用 `scripts/dev/`（`backend.ps1` / `frontend.ps1` / `check-health.ps1`；另有 `doctor.ps1` / `check-env.ps1` / `setup.ps1` 等），不要手敲 dotnet/vite。日常优先用 slash 命令 `/module` `/build` `/test` `/check` `/precommit` `/scaffold` 与子代理 `rule-reviewer` / `module-explorer` 走既定闭环（见 `design/22-claude-workflow.md`）。
- 系统数据库连接的运行时来源是 `db-connections.json`（`DbConnectionsHelper` 解析），不是 `appsettings.json`。
- **大库提速（feedback loop）**：日常只改单模块时用 `src/*.slnf` 工作区过滤器，只加载/编译该模块的依赖闭包而非整个解决方案项目图（当前约 30 个 `.csproj`：23 生产 + 8 测试）——IDE 直接打开对应 `.slnf`，或 `scripts/dev/build-filter.ps1 <name>`（现有 `cardflow`/`express`/`finance`/`crm`/`task`/`core`/`dormitory`；其余模块暂无专用 slnf，按需回落全图或新增）。跑测试用 `scripts/dev/test-dotnet.ps1 [filter]`，自动发现 `tests/` 下全部 `*.Tests.csproj`（新增测试项目零改动纳入）。注：`cardflow.slnf` 只含生产闭包不含 `CardFlow.Tests`（后者引用 `WebAPI` 会拉入全图）。
- **提交门禁是两道独立机制**：① `.husky/pre-commit` 只拦 `web/src/**.{vue,scss}` **新增行**里的裸 hex；② 改动 `.cs` 的**编译门禁**是 Claude Code PreToolUse hook `scripts/dev/hook-precommit-gate.ps1`（配在 `.claude/settings.local.json`）——把每个暂存 `.cs` 映射到其 `.csproj` 并 `dotnet build`，编译失败即拒绝提交。门禁**不跑测试**（留给 `/test`）、**不跑前端 type-check**。
- **后端运行时锁 `STOTOP.WebAPI.dll`**：需全图 `dotnet build` 时用 `-o <scratch目录>` 或 `/p:UseSharedCompilation=false` 绕锁，或先停后端。改单模块用 `.slnf` 闭包不触碰 WebAPI 输出。
- **无 EF Core Migrations**：库结构/参考数据靠后端启动时的 seeder + canonical baseline（`Data/Seeders/Baseline/baseline-reference-data.json` 逐行 upsert 对齐）；改 schema/规则数据走**版本化 seeder（V 编号）**，不要 `dotnet ef migrations`。大块规则体（导入/凭证规则 JSON）放 `src/STOTOP.WebAPI/Data/Seeders/Resources/*.json`，由对应 seeder 参数化读取，别硬编码进 `.cs`。**两个高频坑**：① seeder 里的原生 SQL 用 `SeederHelper.ExecuteRawSql`（纯 ADO），别用 EF `ExecuteSqlRaw`——它即使无参数也会做 `String.Format`，SQL 里的 `{}`（JSON 值、`{yyyy}{MM}` 模板）会抛 `FormatException`；② 改了 baseline JSON 后须重建/拷贝到 bin——`BaselineReferenceDataSeeder.ResolveBaselinePath` 首选 `AppContext.BaseDirectory` 下的 bin 副本，否则 seeder 跑的是旧文件（症状=「库又漂移了」的假象）。
- **模块级 CLAUDE.md**：进某模块前先看 `src/STOTOP.Module.<X>/CLAUDE.md`（目前 CardFlow 有），其硬约束/已知坑优先于本文的泛化规则。
- **架构边界（硬约束）**：
  - 新审批 / 动态表单 / 节点流转 / 卡片待办一律进 **CardFlow**。
  - Workflow 只做事件、派发、质量处理等底层能力，不复制 CardFlow 运行时。
  - `STOTOP.Module.OA` 与 `OASeeder` 仅用于历史数据兼容与退役清理，不注册 OA 控制器（`Program.cs` 主动移除其 MVC ApplicationPart）、不作新入口。
  - 不要新建 `STOTOP.Module.DataCenter`（导入/校验集中在 CardFlow / Express），也不要新建 `STOTOP.Module.Platform`——**平台超管/租户/套餐/订阅/IDP/开通能力全部内聚在 System 模块**。

## 1. 后端规则

### 分层
每个 `STOTOP.Module.*` 标准目录：`Controllers/`、`Services/` + `Services/Interfaces/`、`Entities/`、`Configurations/`（`IEntityTypeConfiguration<T>`）、`Dtos/`；常见附加目录 `EventHandlers/`（`IEventHandler` 实现）、`Events/`（模块内事件契约），部分模块另有 `Filters/`、`Middleware/`、`Jobs/`。基础层：`STOTOP.Core`（`BaseEntity`、`ApiResult`、隔离接口/服务契约）、`STOTOP.Infrastructure`（DbContext、Repository、Middleware、Events）、`STOTOP.WebAPI`（组合根，不写业务逻辑）。

### 模块注册
每模块提供 `Add{Module}Module(this IServiceCollection)` 扩展（`{Module}ModuleExtensions.cs`）：逐个 `AddScoped<IXxxService, XxxService>()` + `ApplyConfiguration(...)` + 事件处理器注册。`Program.cs` 按既定顺序组合，**CardFlow 必须早于 Express 注册**（Express 依赖其 `IImportService` / 自动插件进度）。平台/租户地基服务（`IPlatformScopeFactory` / `ITenantResolver` / `ITenantScopeFactory` / `ITenantIterationService`）在所有业务模块之前注册（`Program.cs:313-317`）。WorkHub 不是独立模块，是 WebAPI 组合根内的聚合服务（裸 `AddScoped<IWorkHubService,…>`）。

### Controller
- 路由 `[Route("api/{module}/{resource}")]`，模块/资源名全小写（如 `api/crm/bonus`）。
- HTTP 动词语义：`GET` 查 / `POST` 增 / `PUT` 改 / `DELETE` 删。
- 权限用 `[RequirePermission("module:resource:action")]`（自定义 `IAsyncActionFilter`），如 `crm:bonus:view`：未认证 401、管理员旁路、否则查 `SYS用户角色`/`SYS角色权限`/`SYS功能权限` 命中与否，缺权限返 403。
- 返回统一包装 `ApiResult` / `ApiResult<T>`：`{ code, message, data }`，JSON 输出 camelCase。**注意 API 面**：携带数据的成功工厂是泛型 `ApiResult<T>.Success(data, message="操作成功")`（Code=200）；非泛型 `ApiResult` 只有无数据成功 `ApiResult.Ok(message)` 与失败 `ApiResult.Fail(message, code=400)`。别写 `ApiResult.Success(data)`（非泛型上不存在）。

### Service
- `IXxxService`（接口在 `Services/Interfaces/`）+ `XxxService`，构造函数注入。
- 所有 I/O 方法加 `Async` 后缀、返回 `Task` / `Task<T>`。
- 数据访问优先 `IRepository<T>`：`Query()` 返回 `IQueryable<T>` 做 LINQ；`AddAsync/UpdateAsync/DeleteAsync` 内部**即时** `SaveChangesAsync`（`AddAsync` 立即落库以取自增主键，不是仅入跟踪——别指望攒批后一次提交）；跨多仓储事务才直接用 DbContext（真库事务须经 `IExecutionStrategy`，与 `EnableRetryOnFailure` 冲突时用 `strategy.ExecuteAsync(...)` 包裹）。
- **全局 `NoTrackingWithIdentityResolution`**：查询默认不跟踪。若在同一 DbContext 内 Add/已跟踪某实体后，又用默认（不跟踪）方式重查同一实体并 `Update`/`Entry(x).State=Modified`，会抛 identity conflict（"another instance with the same key is already being tracked"），批次可能永久卡住；重查须显式 `.AsTracking()`。**单测默认 `TrackAll` 会掩盖此坑（假绿）**，复现须把测试 DbContext 设为 `NoTrackingWithIdentityResolution`。

### 数据访问与实体
- 单一 `STOTOPDbContext`；实体继承 `BaseEntity`（`long FID`）或 `BaseGuidEntity`（`string FUID`），EF 配置走 Fluent API 配置类。
- **三类行级隔离接口**（全局查询过滤器在 `OnModelCreating` 按接口自动织入）：
  - `IOrgScoped`（`FOrgId`）：按当前组织隔离，保存时自动回填当前组织。过滤器 **null-permissive**——`CurrentOrgId == null` 时放行。跨组织需显式 `SuppressOrgIdFill()`（抑制写侧回填）/ `IgnoreQueryFilters()`（逃逸读侧过滤器）。`IOrgOwned`（`FOwnerOrgId`）是其变体（放行 `FOwnerOrgId==0`）。
  - `ITenantScoped`（`F租户ID` / `FTenantId`）：客户级隔离，见 §1.5，**fail-closed 硬墙**（比组织隔离严格）。
  - `IAccountSetScoped`（`FAccountSetId`）：账套隔离，**与组织隔离互斥**、不受组织过滤器约束，须手写 `Where`。
- **数据库命名（强约束）**：
  - 库名小写。
  - 表名 = 模块前缀（大写英文，如 `CRM`/`CF`/`CON`/`CONF`/`HR`/`SYS`/`PLT`）+ 中文业务名，如 `CRM客户`、`CF操作日志`、`CON合同`、`HR员工`、`SYS角色`、`PLT租户`（`builder.ToTable(...)`）。
  - **数据库列名用中文 `F+中文`（`F状态`、`F组织ID`、`F创建人`），C# 实体属性用英文 `F+PascalCase`（`FStatus`、`FOrgId`、`FCreatorName`），由配置类 `HasColumnName("F中文")` 映射**——两层不要混淆。
  - 主键：自增数字 `FID`（`long`，`BaseEntity`）；字符串编号属性 `FCode`（中文列名按业务语义为 `F编号` 或 `F编码`，二者并存，勿假定唯一，如 `SYS角色` 用 `F编码`）；GUID `FUID`（`BaseGuidEntity`）。
  - 每表固定系统字段：`F组织ID`（`FOrgId`，组织隔离）、多租户实体另有 `F租户ID`（`FTenantId`）、`F创建人/F创建时间/F更新人/F更新时间`、常见并发令牌 `F版本号`（`IsConcurrencyToken`）、软状态 `F状态`（`FStatus`）。

### 异常与日志
- 统一由 `GlobalExceptionMiddleware` 拦截：`UnauthorizedAccessException → 403`、`InvalidOperationException → 400`（透传消息，业务错误就抛它，无独立 BusinessException）、其余 `→ 500`（开发环境透出 inner 链，生产仅返回"服务器内部错误"）。
- `ILogger<T>` 注入分级记录。**异常消息/日志/业务数据用中文，类名/方法/属性/权限码用英文 PascalCase**。

### 后台任务 / 实时
- Hangfire：Job 类放各模块 `Jobs/` 目录（参照 `Contract`/`CardFlow` 现有 Job），`RecurringJob.AddOrUpdate<Job>(id, j => j.ExecuteAsync(), cron)` 可加 `[AutomaticRetry]`，集中在 `Program.cs` 注册。
- **多租户下后台 Job 不设根租户处理全库**，而是经 `ITenantIterationService.ForEachActiveTenantAsync` 逐活跃租户各跑一遍（每租户独立 scope + try/catch 隔离）。批次/按组织链路用 `ITenantResolver.ResolveTenantForOrg(batchOrgId)` **按批次组织解析租户，绝不一律 `GetRootTenantId`**（多客户下会串租户/漏处理）。单客户下退化为循环 1 次，行为不变。
- SignalR Hub 在 `/hubs/*`（`progress`、`database-progress`、`notification`、`workhub`、`cardflow`）。

## 1.5 多租户与平台隔离（硬约束）

> 区域公司 = 租户。stage0-4 已落地并织入生产运行路径（`STOTOPDbContext` / `Program.cs` / 中间件 / System 模块）。**新写实体/查询/Job/控制器前必读本节**，否则极易写出越权或"读空集/写崩溃"的代码。原始目标态设计见 `design/23-multitenant-org-redesign.md`、迁移手册 `design/24`。

- **租户过滤器是 fail-closed 硬墙**（`STOTOPDbContext.ConfigureTenantFilter`）：`平台作用域放行 || (当前租户非空 && F租户ID==当前租户)`。**无租户上下文且非平台作用域 → 过滤器恒 false → 读空集，既不认 null 也不认 0**（对比组织过滤器 null 时放行）。
- **写侧硬墙**（`FillTenantIdForNewEntities`）：新增 `ITenantScoped` 实体时——无租户上下文抛 `无租户上下文下禁止写入租户隔离数据`；`F租户ID==0` 回填当前租户；写入他租户抛 `跨租户写入被拒绝`。平台作用域整段放行。
- **组合过滤器**：同时实现 `IOrgScoped+ITenantScoped`（或 `IOrgOwned+ITenantScoped`）的实体，DbContext 一次性应用**组合 AND 过滤器**。EF `HasQueryFilter` 是**覆盖非叠加**——**绝不对同一实体分轮各调一次**，否则后者悄悄丢掉前者的隔离。
- **绕过租户墙的唯一受控入口是 `IPlatformScopeFactory.Enter(reason)`**（平台层 / seeder / 迁移）。业务 Service 注入它即越权后门。非 HTTP 链路（后台/批次/CLI）要读写租户数据，必须显式 `ITenantScopeFactory.Enter(tid)` 或 `IPlatformScopeFactory.Enter(reason)` 固化上下文，否则读空/写崩。
- **平台层**：`/api/platform/*` 脱离租户/组织上下文（`OrgContextMiddleware` SkipPaths），**必须标 `[PlatformOnly]`**（校验 `SysUser.FIsPlatformAdmin` 平台超管 + 进平台作用域跨租户读写）；漏标则 fail-closed 读空自曝。平台三表 `PLT租户`/`PLT套餐`/`PLT订阅` 及 IDP/开通服务均在 System 模块（无独立 Platform 模块）。
- **中间件顺序硬约束**：`TenantFreezeMiddleware` 必须在 `OrgContextMiddleware` 之后（`CurrentTenantId` 已就绪）。租户欠费冻结（`PLT租户.FStatus=4`）时按白名单拒业务写与批量导出（返 402）；单客户正式态恒放行 = 休眠能力。
- **R8 数据范围（fail-open 陷阱）**：`SysScopeGrant` 跨节点可视域**刻意不进全局过滤器**（可视域随用户/动作变）——列表/报表须显式 `query.ApplyVisibilityScope(IScopeGrantService.GetVisibleNodeIdsAsync(...))` opt-in；**漏调 = fail-open**，只受租户墙 + 单节点组织过滤器约束。
- **前端**：`X-Tenant-Context` 头（见 §2 API 层，加性头，缺失时后端回退根租户）；`platform/*` 路由仅平台超管经用户下拉进入；`tenantContext` store / `TenantSwitcher`；428 响应 → 提示先选组织/租户。
- **新增受租户隔离实体的写法**：实体实现 `ITenantScoped`（配置类映射 `F租户ID` 列）；因无 EF Migrations，建表/加列走**版本化 seeder（V 编号，`NOT NULL DEFAULT 0` + 回填根租户）**，不要 `dotnet ef migrations`；DbContext 会自动织入 fail-closed 过滤器与写回填，无需手写 `HasQueryFilter`。参照 System 模块既有 `Sys*` 租户隔离实体。

## 2. 前端规则

### 目录职责
`api/`（按模块一文件）、`stores/`（Pinia）、`router/`、`views/{module}/`、`components/`（PC）、`mobile/`（移动端独立子应用）、`styles/`、`utils/`、`composables/`、`types/{module}.ts`。新增一个前端页 = 照抄某成熟模块（如 `crm`）的四件套 `api/{m}.ts` + `types/{m}.ts` + `stores/useXStore.ts` + `views/{m}/*.vue`；路由由后端菜单动态注入，别手写 `addRoute`（类型不在 api 文件里定义，见 API 层）。

### API 层
单一 axios 实例 `web/src/api/request.ts`（baseURL `/api`，15s 超时）：
- 请求拦截按序自动注入 5 个头：`Authorization` / `X-Device-Fingerprint` / `X-Org-Context` / `X-Tenant-Context`（多租户，缺失回退根租户）/ `X-AccountSet-Id`。
- 响应拦截解析 `{ code, data, message }`、401 触发刷新队列、403 跳 `/403`、428（未选组织/租户）提示先选、Blob 直通。
- 业务函数用 `get/post/put/del<T>` 包装；类型从 `@/types/{module}` 导入，不在 api 文件里定义；命名 `get*/create*/update*/delete*`。

### 状态
统一 **setup store**：`defineStore('xxx', () => { ref/computed; return {...} })`，命名 `useXxxStore`。

### 路由与权限
三层：静态 / 移动端 `/m/*` / PC 主布局 `Layout`（系统管理、平台管理台等均为 `Layout` 子路由，**无独立 `/admin` 路由层**）。PC 用 history 模式，移动端用 hash 独立 router 实例。动态路由由后端菜单经 `permission` store 的 `generateRoutes()` 生成并 `addRoute('Layout', ...)`。全局守卫校验 token；**模块级门禁按 `getCurrentModuleMenus(moduleCode).length > 0` 判定，无权跳 `/403`**（`permission:'*'` 与 `/m/*` 豁免）。管理后台入口 = `hasAdminAccess`（`getCurrentModuleMenus('system').length>0`）。平台管理台（`platform/*`，`permission:'*'` 绕门禁）仅平台超管可见。

### 样式 / 设计令牌（强约束 + 门禁）
- **禁止裸 hex 颜色**，必须用设计令牌 `var(--token)`（运行时 `stores/theme.ts` 注入）或 SCSS `$变量`（`styles/variables.scss` 桥接）；真源是 `web/docs/TOKENS.md`。
- `.husky/pre-commit` 是 diff 门禁：只拦 `web/src/**.{vue,scss}` **新增行**里的裸 hex（存量不阻塞）。豁免：`styles/{variables,ant-override,layout,button-styles}.scss`，或该行加注释 `/* hex-ok: 原因 */`（ECharts/SVG/打印导出场景）。`npm run lint:style` 用 stylelint 校验；Write/Edit web 样式会被 PostToolUse hook 自动 `stylelint --fix`。
- 全局范式类：`.page-container` / `.page-card` / `.page-toolbar`。

### 组件
PascalCase；PC 组件自动导入（`web/src/components.d.ts` 由 Vite 插件**自动生成，不手改**）；AntD 组件 `A` 前缀（Resolver 注入）。CardFlow 动态表单的字段组件（schema 驱动）放 `components/cardflow/fields/`（`AccountSelector` / `UserSelect` / `OrgSelect` / `AuxiliarySelector` / `BankAccountSelector` 等），自动导入为 PascalCase 组件。同名 PC/移动端组件用**命名隔离**（如 `EmployeeSelect` vs `DormEmployeeSelect`），避免 `components.d.ts` 抖动。

### TypeScript
`strict: true`，禁裸 `any`（用 `unknown` + 类型守卫）；DTO/Request 类型集中在 `types/`。`npm run type-check`（vue-tsc）须通过。

### 多入口
`index.html`（PC）/ `mobile.html`（移动端）/ `redirect.html`，Vite rollup 三入口；路径别名 `@` → `src`（另有 `@shared` → `src/shared`）。

## 3. 测试规则

- 框架 **xUnit**；项目 `tests/STOTOP.Module.*.Tests`（及 `STOTOP.WebAPI.Tests`）；类 `XxxTests`，方法可用中文描述名（`[Fact]`）。
- **各模块**：DbContext 用该模块 `TestDbContextFactory.Create(...)` + InMemory 库 + 模拟组织上下文（`TestOrgContextAccessor`，现携 `CurrentOrgId`/`CurrentTenantId`/`IsPlatformScope` 三维），需 `RegisterModuleAssembly(...)` 注册涉及模块。
- **租户/平台自检**：在 `STOTOP.Module.System.Tests` 下用租户感知工厂 `TestDbContextFactory.Create(db, orgId, tenantId, platformScope)` + `TenantTestModules.RegisterAll()`（`[ModuleInitializer]` 全模块注册）+ 三维上下文。套件含 `Tenant*` / `Platform*`（如 `TenantLeakScanTests` 是跨模块漏标门禁，需全模块注册）。
- 跑测试用 `/test <filter>` 收敛；`CardFlow.Tests` / `WebAPI.Tests` 引 `WebAPI` 会拉近全图、较慢且负载下 flaky（判回归多跑几次）；x64 宿主避免 x86 testhost socket 耗尽。

## 4. 协作约定

- 缩进：`.cs`/csproj/props/targets = 4 空格，其余（ts/vue/scss/json/md）= 2 空格，`.sln` = Tab；`charset=utf-8`、`lf`、文末换行。
- 设计文档放 `design/NN-*.md`，默认中文，记录当前运行边界而非历史计划；新增模块同步更新 `design/00-overview.md` 索引与 `Program.cs` 注册顺序。
- SDK 锁定 `global.json`（10.0.300，latestFeature）；macOS 本地禁用并行构建（`src/Directory.Build.props`）。传递漏洞钉版（如 `System.Security.Cryptography.Xml`）须在 `src/` + `tests/` 的 `Directory.Build.props` 逐工程注入。
- **并发会话共用主工作树**有切分支/裹挟提交风险；隔离用 git worktree（副本落 `.claude/worktrees/`）；探索/审查时忽略 `.claude/worktrees/` 与 `bin/obj` 下的副本。
- **存疑时（when in doubt）**：不擅自开分支 / push（须用户明确点头）；大改先出 plan、锁模块、小步 build+test；子代理逐任务后必做整体终审 + 回归（逐任务审会漏跨组件缝）；DB/模型不一致先据设计意图判哪侧对，别默认模型即真理；排查/领域问题先查 memory 索引，命中既有记忆优先复用其结论。

## 绝不（Never）

- 绝不手敲 `dotnet`/`vite` 启动（用 `scripts/dev/` 或 `/module` `/build`）。
- 绝不新建 `STOTOP.Module.DataCenter` 或 `STOTOP.Module.Platform`；不注册 OA 控制器、不以 OA 作新入口。
- 绝不把新审批/动态表单/节点流转/卡片待办写进 Workflow（进 CardFlow）。
- 绝不在业务 Service 注入 `IPlatformScopeFactory` 绕租户墙；绝不让后台 Job 一律 `GetRootTenantId` 处理全库（须 per-tenant 迭代 / 按批次组织解析）。
- 绝不对同一实体分轮 `HasQueryFilter`（覆盖非叠加）。
- 绝不用 `dotnet ef migrations`（走 seeder + baseline JSON / 版本化 V 编号）。
- 绝不手改 `web/src/components.d.ts`（自动生成）。
- 绝不裸 hex 颜色 / 裸 `any`。
- 绝不擅自开分支 / push（须用户点头）。
- 绝不在后端运行时做全图 `dotnet build` 而不加 `-o scratch` 或 `/p:UseSharedCompilation=false`（锁 `WebAPI.dll`）。
