---
description: 按 STOTOP 分层约定脚手架一个新后端模块（目录结构 / 实体命名映射 / ModuleExtensions / Program.cs 注册顺序）。
argument-hint: <模块英文名 如 Asset> [中文业务名]
---

为新模块 **$1**（中文业务名：**$2**）生成符合 @design/21-dev-rules.md 的骨架。

⚠️ **先判边界**：新审批 / 动态表单 / 节点流转 / 卡片待办 **一律进 CardFlow**，不要建独立审批；导入/校验集中在 CardFlow / Express，**不要新建 DataCenter**；不新增 OA 入口。若需求落在这些边界内，先提示我改方案，不要建新模块。

边界 OK 后，先和我确认要建的**资源/实体**，再按下面逐条照做：

1. **项目**：`src/STOTOP.Module.$1/`，标准目录 `Controllers/`、`Services/` + `Services/Interfaces/`、`Entities/`、`Configurations/`、`Dtos/`。
2. **实体**：继承 `BaseEntity`(long `FID`) 或 `BaseGuidEntity`(`FUID`)；需组织隔离则实现 `IOrgScoped`（`FOrgId`）。固定系统字段：`FOrgId`/`FCreatorName`/`FCreateTime`/`FUpdaterName`/`FUpdateTime`/`FVersion`(并发令牌)/`FStatus`。
3. **配置类**：实现 `IEntityTypeConfiguration<T>`，`builder.ToTable("$1前缀+中文业务名")`（前缀大写英文）；**每个属性** `HasColumnName("F中文")`——C# 属性是 `F+PascalCase`，数据库列是 `F+中文`，两层别混。
4. **Controller**：`[Route("api/<模块小写>/<资源小写>")]`；GET 查 / POST 增 / PUT 改 / DELETE 删；`[RequirePermission("module:resource:action")]`；返回 `ApiResult` / `ApiResult<T>`。
5. **Service**：`IXxxService`（放 `Services/Interfaces/`）+ `XxxService`，构造函数注入；I/O 方法加 `Async` 后缀；数据访问优先 `IRepository<T>`，跨多仓储事务才直接用 DbContext。
6. **注册扩展**：`$1ModuleExtensions.cs` 提供 `Add$1Module(this IServiceCollection)`——逐个 `AddScoped<IXxx, Xxx>()` + `ApplyConfiguration(...)` + 事件处理器注册。
7. **组合根**：在 `src/STOTOP.WebAPI/Program.cs` 按既定顺序注册（**CardFlow 必须早于 Express**），并同步更新 @design/00-overview.md 的模块注册表与文档索引。
8. **异常**：业务错误抛 `InvalidOperationException`(→400) / `UnauthorizedAccessException`(→403)，消息用中文；无独立 BusinessException。
9. **提速配套（可选但推荐）**：加 `src/$1小写.slnf` 工作区过滤器与 `tests/STOTOP.Module.$1.Tests`（test-dotnet 脚本会自动发现）。

生成后跑 `/build` 验证编译通过再交付。
