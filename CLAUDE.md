# CLAUDE.md

本文件供 Claude Code / AI 助手在本仓库工作时作为上下文加载，也是团队开发规则的快速参考。内容与 `design/21-dev-rules.md` 一致（正式规范并入 `design/` 体系）。技术栈：后端 .NET 10 / ASP.NET Core / EF Core / SQL Server / Hangfire / SignalR；前端 Vue 3 / Vite / TypeScript / Pinia / Ant Design Vue（PC）+ Vant（移动端）。

默认用中文交流。以代码为准；本文若与代码冲突，先核对代码再更新本文。

## 0. 运行与边界

- 后端 `:9000`，前端 `:9001`；前端代理 `/api`、`/hangfire`、`/hubs` 到后端。健康检查 `/health`，版本 `/api/version`。
- 启动用 `scripts/dev/`（`backend.ps1` / `frontend.ps1` / `check-health.ps1`），不要手敲 dotnet/vite。
- 系统数据库连接的运行时来源是 `db-connections.json`，不是 `appsettings.json`。
- **大库提速（feedback loop）**：日常只改单模块时用 `src/*.slnf` 工作区过滤器，只加载/编译该模块的依赖闭包而非整个 60 项目图——IDE 直接打开对应 `.slnf`，或 `scripts/dev/build-filter.ps1 <name>`（现有 `cardflow`/`express`/`finance`/`crm`/`task`/`core`/`dormitory`）。跑测试用 `scripts/dev/test-dotnet.ps1 [filter]`，自动发现 `tests/` 下全部 `*.Tests.csproj`（新增测试项目零改动纳入）。注：`cardflow.slnf` 只含生产闭包不含 `CardFlow.Tests`（后者引用 `WebAPI` 会拉入全图）。
- **架构边界（硬约束）**：
  - 新审批 / 动态表单 / 节点流转 / 卡片待办一律进 **CardFlow**。
  - Workflow 只做事件、派发、质量处理等底层能力，不复制 CardFlow 运行时。
  - `STOTOP.Module.OA` 与 `OASeeder` 仅用于历史数据兼容与退役清理，不注册 OA 控制器、不作新入口。
  - 不要新建 `STOTOP.Module.DataCenter`，导入/校验能力集中在 CardFlow / Express。

## 1. 后端规则

### 分层
每个 `STOTOP.Module.*` 标准目录：`Controllers/`、`Services/` + `Services/Interfaces/`、`Entities/`、`Configurations/`（`IEntityTypeConfiguration<T>`）、`Dtos/`。基础层：`STOTOP.Core`（`BaseEntity`、`ApiResult`、接口契约）、`STOTOP.Infrastructure`（DbContext、Repository、Middleware、Events）、`STOTOP.WebAPI`（组合根，不写业务逻辑）。

### 模块注册
每模块提供 `Add{Module}Module(this IServiceCollection)` 扩展（`{Module}ModuleExtensions.cs`）：逐个 `AddScoped<IXxxService, XxxService>()` + `ApplyConfiguration(...)` + 事件处理器注册。`Program.cs` 按既定顺序组合，**CardFlow 必须早于 Express 注册**（Express 依赖其导入服务/自动插件进度）。

### Controller
- 路由 `[Route("api/{module}/{resource}")]`，模块/资源名全小写（如 `api/crm/bonus`）。
- HTTP 动词语义：`GET` 查 / `POST` 增 / `PUT` 改 / `DELETE` 删。
- 权限用 `[RequirePermission("module:resource:action")]`（自定义 filter），如 `crm:bonus:view`。
- 返回统一包装 `ApiResult` / `ApiResult<T>`：`{ code, message, data }`；`ApiResult.Success(data)` / `ApiResult.Fail(msg, code)`。JSON 输出 camelCase。

### Service
- `IXxxService`（接口在 `Services/Interfaces/`）+ `XxxService`，构造函数注入。
- 所有 I/O 方法加 `Async` 后缀、返回 `Task` / `Task<T>`。
- 数据访问优先 `IRepository<T>`：`Query()` 返回 `IQueryable<T>` 做 LINQ；`Add/Update/Delete` 内部自动 `SaveChangesAsync`；跨多仓储事务才直接用 DbContext。

### 数据访问与实体
- 单一 `STOTOPDbContext`；实体继承 `BaseEntity`（`long FID`）或 `BaseGuidEntity`，EF 配置走 Fluent API 配置类。
- 实现 `IOrgScoped` 的实体由全局查询过滤器按 `FOrgId` 自动隔离，保存时自动回填当前组织；跨组织需显式 `SuppressOrgIdFill()` / `IgnoreQueryFilters()`。
- **数据库命名（强约束）**：
  - 库名小写。
  - 表名 = 模块前缀（大写英文，如 `CRM`/`CF`/`CON`/`CONF`/`HR`）+ 中文业务名，如 `CRM客户`、`CF操作日志`、`CON合同`、`HR员工`（`builder.ToTable(...)`）。
  - **数据库列名用中文 `F+中文`（`F状态`、`F组织ID`、`F创建人`），C# 实体属性用英文 `F+PascalCase`（`FStatus`、`FOrgId`、`FCreatorName`），由配置类 `HasColumnName("F中文")` 映射**——两层不要混淆。
  - 主键：自增数字 `FID`（`long`，`BaseEntity`）；字符串编号 `F编号`（属性 `FCode`）；GUID `FUID`（`BaseGuidEntity`）。
  - 每表固定系统字段：`F组织ID`（`FOrgId`，组织隔离）、`F创建人/F创建时间/F更新人/F更新时间`、常见并发令牌 `F版本号`（`IsConcurrencyToken`）、软状态 `F状态`（`FStatus`）。

### 异常与日志
- 统一由 `GlobalExceptionMiddleware` 拦截：`UnauthorizedAccessException → 403`、`InvalidOperationException → 400`（透传消息，业务错误就抛它，无独立 BusinessException）、其余 `→ 500`（开发环境透出 inner 链，生产仅返回"服务器内部错误"）。
- `ILogger<T>` 注入分级记录。**异常消息/日志/业务数据用中文，类名/方法/属性/权限码用英文 PascalCase**。

### 后台任务 / 实时
- Hangfire：`RecurringJob.AddOrUpdate<Job>(id, j => j.ExecuteAsync(), cron)`，可加 `[AutomaticRetry]`，集中在 `Program.cs` 注册。
- SignalR Hub 在 `/hubs/*`（progress、notification、workhub、cardflow 等）。

## 2. 前端规则

### 目录职责
`api/`（按模块一文件）、`stores/`（Pinia）、`router/`、`views/{module}/`、`components/`（PC）+ `components/form-widgets/`、`mobile/`（移动端独立子应用）、`styles/`、`utils/`、`composables/`、`types/{module}.ts`。

### API 层
单一 axios 实例 `web/src/api/request.ts`（baseURL `/api`，15s 超时）：
- 请求拦截自动注入 `Authorization` / `X-Device-Fingerprint` / `X-Org-Context` / `X-AccountSet-Id`。
- 响应拦截解析 `{ code, data, message }`、401 触发刷新队列、403 跳 `/403`、Blob 直通。
- 业务函数用 `get/post/put/del<T>` 包装；类型从 `@/types/{module}` 导入，不在 api 文件里定义；命名 `get*/create*/update*/delete*`。

### 状态
统一 **setup store**：`defineStore('xxx', () => { ref/computed; return {...} })`，命名 `useXxxStore`。

### 路由与权限
四层：静态 / 移动端 `/m/*` / PC 主布局 `Layout` / 管理后台 `/admin`。PC 用 history 模式，移动端用 hash 独立 router 实例。动态路由由后端菜单经 `permission` store 的 `generateRoutes()` 生成并 `addRoute('Layout', ...)`；全局守卫校验 token，admin 路由校验 `roles.includes('admin')`。

### 样式 / 设计令牌（强约束 + 门禁）
- **禁止裸 hex 颜色**，必须用设计令牌 `var(--token)`（运行时 `stores/theme.ts` 注入）或 SCSS `$变量`（`styles/variables.scss` 桥接）；真源是 `web/docs/TOKENS.md`。
- `.husky/pre-commit` 是 diff 门禁：只拦 `web/src/**.{vue,scss}` **新增行**里的裸 hex（存量不阻塞）。豁免：`styles/{variables,ant-override,layout,button-styles}.scss`，或该行加注释 `/* hex-ok: 原因 */`（ECharts/SVG/打印导出场景）。`npm run lint:style` 用 stylelint 校验。
- 全局范式类：`.page-container` / `.page-card` / `.page-toolbar`。

### 组件
PascalCase；PC 组件自动导入（`components.d.ts` 自动生成，不手改）；AntD 组件 `A` 前缀。表单输入统一放 `components/form-widgets/` 并经 `registerFormWidgets()` 注册到 form-create。同名 PC/移动端组件用**命名隔离**（如 `EmployeeSelect` vs `DormEmployeeSelect`），避免 `components.d.ts` 抖动。

### TypeScript
`strict: true`，禁裸 `any`（用 `unknown` + 类型守卫）；DTO/Request 类型集中在 `types/`。`npm run type-check`（vue-tsc）须通过。

### 多入口
`index.html`（PC）/ `mobile.html`（移动端）/ `redirect.html`，Vite rollup 三入口；路径别名 `@` → `src`。

## 3. 测试规则

- 框架 **xUnit**；项目 `tests/STOTOP.Module.*.Tests`；类 `XxxTests`，方法可用中文描述名（`[Fact]`）。
- DbContext 用 `TestDbContextFactory.Create(...)` + InMemory 库 + 模拟组织上下文（`TestOrgContextAccessor`），需 `RegisterModuleAssembly(...)` 注册涉及模块。

## 4. 协作约定

- 缩进：`.cs`/csproj = 4 空格，其余（ts/vue/scss/json/md）= 2 空格，`.sln` = Tab；`charset=utf-8`、`lf`、文末换行。
- 设计文档放 `design/NN-*.md`，默认中文，记录当前运行边界而非历史计划；新增模块同步更新 `design/00-overview.md` 索引与 `Program.cs` 注册顺序。
- SDK 锁定 `global.json`（10.0.300，latestFeature）；macOS 本地禁用并行构建（`src/Directory.Build.props`）。

## 文档入口

- [系统总览](design/00-overview.md) ・ [WebAPI 启动层](design/19-webapi.md) ・ [前端架构](design/20-frontend.md) ・ [开发规则](design/21-dev-rules.md)
