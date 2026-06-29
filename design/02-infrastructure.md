# 基础设施层设计文档（STOTOP.Infrastructure）

## 1. 模块职责与边界

基础设施层负责所有技术实现细节，包括：

- 数据访问（EF Core DbContext、仓储实现）
- 中间件管道（异常处理、API 响应包装、数据库初始化引导）
- 数据库初始化（DatabaseMigrator 幂等迁移与种子）
- 跨模块基础服务（动态数据库工厂、事件分发、Excel 解析）

**边界原则**：实现Core层定义的接口，不包含业务逻辑，仅提供技术能力。

---

## 2. 核心组件设计

### 2.1 STOTOPDbContext

**文件路径**：`src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs`

| 特性 | 说明 |
|------|------|
| 动态配置程序集 | RegisterModuleAssembly()（实例方法 AddConfigurationAssembly() 为兼容别名）允许各业务模块注册自己的EntityTypeConfiguration |
| 组织隔离全局过滤器 | 自动为实现IOrgScoped的实体添加 `WHERE CurrentOrgId IS NULL OR FOrgId = CurrentOrgId`（不再放行 FOrgId = 0 的全局共享数据） |
| SaveChanges自动填充 | 新增/修改实体时自动设置FOrgId为当前组织ID |
| 查询行为 | 默认NoTrackingWithIdentityResolution，兼顾性能与引用一致性 |

```mermaid
graph TB
    A[STOTOPDbContext] --> B[OnModelCreating]
    B --> C[加载各模块EntityTypeConfiguration]
    B --> D[注册IOrgScoped全局过滤器]
    A --> E[SaveChangesAsync Override]
    E --> F[检测新增实体]
    F --> G[自动填充FOrgId]
```

### 2.2 Repository\<T\> 泛型仓储

**文件路径**：`src/STOTOP.Infrastructure/Repositories/Repository.cs`

| 方法 | 实现细节 |
|------|----------|
| GetByIdAsync | FindAsync(id) |
| GetAllAsync | ToListAsync() |
| AddAsync | Add + 立即SaveChangesAsync |
| UpdateAsync | Update + SaveChangesAsync |
| DeleteAsync | Remove + SaveChangesAsync |
| Query() | Set\<T\>().AsQueryable()，返回IQueryable供上层组合 |

**设计要点**：AddAsync立即持久化，确保调用方获得数据库生成的主键值。

### 2.3 GlobalExceptionMiddleware

**文件路径**：`src/STOTOP.Infrastructure/Middleware/GlobalExceptionMiddleware.cs`

| 异常类型 | HTTP状态码 | 响应行为 |
|----------|-----------|----------|
| UnauthorizedAccessException | 403 | 对象级授权失败，透传消息 |
| InvalidOperationException | 400 | 返回业务错误信息，透传消息 |
| 其他Exception | 500 | 生产环境返回通用错误，开发环境透出异常类型+消息（DbUpdateException/DbException 额外透出 inner 链） |

```mermaid
graph TB
    A[请求进入] --> B[try执行下一个中间件]
    B --> C{是否抛出异常?}
    C -->|否| D[正常响应]
    C -->|是| E{异常类型判断}
    E -->|UnauthorizedAccessException| G[403 Forbidden]
    E -->|InvalidOperationException| F[400 Bad Request]
    E -->|其他| H{环境检测}
    H -->|Development| I[500 + 详细堆栈]
    H -->|Production| J[500 + 通用消息]
```

### 2.4 DynamicDbContextFactory

**文件路径**：`src/STOTOP.Infrastructure/Data/DynamicDbContextFactory.cs`

| 配置项 | 值 |
|--------|-----|
| 数据库提供者 | 仅 SQL Server（UseSqlServer，连接串为空即抛异常） |
| 重试次数 | 5次 |
| 最大延迟 | 10秒 |
| 重试策略 | EnableRetryOnFailure |

### 2.5 OrgContextMiddleware

**文件路径**：`src/STOTOP.Module.System/Middleware/OrgContextMiddleware.cs`（位于 System 模块，非 Infrastructure）

| 步骤 | 说明 |
|------|------|
| 1 | 跳过 auth/setup/health/swagger/hubs 等无需组织上下文的路径，未认证请求直接放行 |
| 2 | 从请求头读取 `X-Org-Context`，解析为组织ID (long) |
| 3 | admin 跳过归属校验；普通用户校验其是否属于该组织，不属于返回 403 |
| 4 | 无请求头值时自动推断：单组织直接选用，多组织取主组织，多组织无主组织返回 428（需先选择组织） |
| 5 | 解析结果写入 `context.Items["CurrentOrgId"]`，供 DbContext 全局过滤器读取 |

### 2.6 DatabaseMigrator

实现 `IDatabaseSeeder` 接口（注册为 Singleton），统一调度新库初始化、启动幂等迁移与基线校验，供依赖注入容器统一管理。

---

## 3. DatabaseMigrator 机制

**文件路径**：`src/STOTOP.WebAPI/Data/DatabaseSeederAdapter.cs`（类名 `DatabaseMigrator`），各模块具体 Seeder 在 `src/STOTOP.WebAPI/Data/Seeders/`

### 3.1 设计原则

| 原则 | 实现方式 |
|------|----------|
| 幂等性 | 所有操作使用 `IF NOT EXISTS` 检查 |
| 增量迁移 | 按需补建新表和新字段，已存在则跳过 |
| IDENTITY_INSERT | 需要指定ID的种子数据使用 SET IDENTITY_INSERT ON/OFF |
| 执行顺序 | 先建表 → 再补字段 → 最后插数据 |

### 3.2 种子数据内容

| 类别 | 数据内容 |
|------|----------|
| 管理员账户 | admin / admin123 (SHA256哈希) |
| 管理员角色 | 系统管理员角色，关联全部权限 |
| 默认组织 | 总公司（ID=1），作为根组织节点 |
| 系统权限菜单 | 模块/页面/按钮三级菜单权限 |
| 财务初始化 | 会计期间、默认账套、凭证模板、辅助核算类型 |
| 组织类型 | 9种：集团/子公司/中心/分公司/部门/团组/快递网点/承包区/末端驿站 |

### 3.3 执行流程

```mermaid
graph TB
    A[应用启动] --> B[DatabaseMigrator]
    B --> C[模块版本化迁移]
    C --> D[Schema / Column 幂等补齐]
    D --> E[基础种子数据]
    E --> F[BaselineReferenceDataSeeder]
    F --> G[OASeeder.PurgeRetiredReferenceData]
    G --> H[基线校验]
    H --> I[启动完成]
```

---

## 4. 数据库表设计

基础设施层本身不定义业务表，但管理以下基础设施相关配置：

| 表名 | 用途 | 管理方式 |
|------|------|----------|
| 各业务表 | 由各模块定义 | DatabaseMigrator 按 EF 模型幂等创建 |

---

## 5. 数据库提供者

仅支持 SQL Server（运行时无多 Provider 切换；MySQL/SQLite 已退役）。

| 提供者 | 用途 | 配置方式 |
|--------|------|----------|
| SQL Server | 生产/开发统一主库 | 运行时来源 `db-connections.json`（非 appsettings.json） |
| InMemory | 单元测试 | 测试项目专用（TestDbContextFactory） |

---

## 6. 文件结构

```
src/STOTOP.Infrastructure/
├── Data/
│   ├── STOTOPDbContext.cs
│   ├── DynamicDbContextFactory.cs
│   └── IDatabaseSeeder.cs
├── Repositories/
│   └── Repository.cs
├── Middleware/
│   ├── GlobalExceptionMiddleware.cs
│   ├── ApiResponseWrapperMiddleware.cs
│   └── DatabaseSetupMiddleware.cs
├── Events/                         # 业务事件与进程内分发
└── Services/
    └── ExcelParserService.cs

src/STOTOP.WebAPI/
└── Data/
    ├── DatabaseSeederAdapter.cs    # 类名 DatabaseMigrator（IDatabaseSeeder 实现）
    ├── SeederHelper.cs
    └── Seeders/                    # SystemSeeder / FinanceSeeder / MigrationRunner / SchemaAutoSync / BaselineReferenceDataSeeder ...

# OrgContextMiddleware 位于 src/STOTOP.Module.System/Middleware/（非 Infrastructure）
```
