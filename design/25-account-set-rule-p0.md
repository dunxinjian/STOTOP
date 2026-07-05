# 账套规则（FinAccountSetRule）P0 落地设计 · 最终定稿

> **[as-built 2026-07-04] 已按本设计实施完毕**，实际落地与定稿的偏差（以代码为准）：
> 1. **UpdateAsync 不校验凭证字**（设计原写 Create/Update 都校验）：`UpdateAsync` 根本不改 `FVoucherWord`，且编辑时前端回传旧字，校验会误拦历史凭证的无关编辑，违背"仅影响新建"——故只在 `CreateAsync` 校验。
> 2. **多改了第 7/8 处字面量**：`AccountPeriodController.GetClosingInfo` 里的 `3103/310405` 查询与警告文案（设计漏列），一并接 `GetClosingAccountCodesAsync`。
> 3. **新增轻量端点** `GET /api/finance/account-set-rules/enabled-voucher-words`（仅 `[Authorize]`）：凭证录入员无 `account-set-rule:view` 权限也需读启用凭证字，不能复用带权限的 GET。
> 4. 按钮权限 FID 实际取 **2129/2130**（非设计稿的 2123/2124——后者在 dev 库已被 quality:carrier 系菜单占用而 baseline JSON 里没有；**选 baseline 新 FID 必须同时查 JSON 与目标库两侧**）。`SYS功能权限` RowCount 428→430 同步。
> 5. 结转科目默认字面量收敛为 `AccountSetRuleDefaults`（`IAccountSetRuleService.cs`），Service/Controller 共用不再散写。
> 6. **激活已完成（2026-07-04，经 `--init-database` 单跑）**：V20 建表 + FID114 可见 + 2129/2130 落库 + 指纹更新，dev 端到端验证通过。激活过程中排掉两颗存量雷：
>    - baseline JSON 与 dev 库在 `FIN科目模板_明细` 上漂移（V11 重建模板3明细后 JSON 是旧快照）→ 已按"库为真源"把该表节重导为 dev 真实 1312 行，随本次改动一并提交；**并追加 FinanceSeeder V21 幂等清理旧快照残留行（FID 20431–20903）**——baseline 对齐只 upsert 不删除，V11 之后用旧 JSON 对齐过的库会新旧双份，V21 兜底自愈（dev 无残留=删 0 行）；
>    - seeder 读的是 **bin\Debug 输出副本**（`ResolveBaselinePath` 首选 `AppContext.BaseDirectory`）——改源 JSON 后须重建或手动拷贝到 bin，否则跑的还是旧文件。
> 7. **M10（admin 角色授权）最终不做**：`SystemSeeder.RegisterSystemPermissions` 只在 System V1 执行且早于 baseline 对齐（届时 2129/2130 尚未插入），存量库、全新库两侧都是死代码，已撤销该编辑。admin 用户靠 `RequirePermission` 短路放行，普通用户授权走角色管理界面。
>
> 版本：v2（终稿）· 面向：Finance 模块开发直接照做 · 基线分支：`feat/tenant-isolation-stage3`
> 技术前提：本仓**无 EF Core Migrations**，schema 走 seeder V-number 引擎（`MigrationRunner`，`ValidateSteps` 强制版本号从 1 连续 +1）；多租户隔离由 `STOTOPDbContext` 全局过滤器按接口自动施加。

---

## 1. 目标与范围

### 1.1 P0 三项（本次要做）

| 编号 | 项 | 一句话目标 |
|---|---|---|
| P0-1 | 制单人≠审核人 | 账套级开关：开则审核时校验 `voucher.FCreator == auditor` 即拒绝（防同一人自制自审）。 |
| P0-2 | 结转科目映射 | `AccountPeriodService.CloseAsync` 里硬编码的本年利润 `"3103"` / 未分配利润 `"310405"` 改为按账套读规则表，缺配置回退旧字面量。 |
| P0-3 | 凭证字白名单 | 「记/收/付/转」四值收敛为单一真源（代码级常量集合 + 账套级启用子集），导入校验、后端手工建/改、前端下拉都读同一真源。 |

三项共用同一张新表 `FIN账套规则`（`FinAccountSetRule`），一页面 `/finance/account-set-rules` 配置。

### 1.2 明确不做（Non-goals）

- **不改凭证的固定字生成语义**：自动凭证/结转/资产/日记账固定用 `VoucherWord.Ji`（"记"），结转固定 `"转"`，这些是"业务固定选字"，不纳入白名单收敛（`AccountPeriodService.cs:291/354` 的 `FVoucherWord="转"` 不动）。
- **不新增 `F审核时间` 列**：审核时间继续复用 `FUpdatedTime`。
- **不改 `FCreator`/`FAuditor` 存储口径**：仍存 `ClaimTypes.Name` 显示名字符串，P0-1 接受"按姓名字符串比对"（同名误判风险见 §10）。不新增 `FCreatorId`/`FAuditorId`。
- **不动 `FinAccountCategory.ProfitLossCategories`**（损益类科目集合已是单一真源常量）。
- **不用 EF Core Migrations**（禁用 `dotnet ef migrations add`）。
- **不做账套级细粒度授权**：权限走**菜单级** `finance:account-set-rule:view/:edit`，走 `SYS功能权限` 表 + `[RequirePermission]` 过滤器，**不引入 `accountset:*` 命名空间、不挂 `[RequireAccountSetPermission]`**（见 §5.5 权限决策）。

---

## 2. 数据模型

### 2.1 存储形态决策：一账套一行（宽表单行）

**决策：一账套一行（宽表单行），不用 KV 多行。** 理由：

1. P0 三项都是"账套级单例配置"（一个开关、一对结转科目、一组启用凭证字），天然是"每账套一份配置"，无枚举扩展需求。
2. 读取模式统一为 `FirstOrDefaultAsync(r => r.FAccountSetId == accountSetId)`，一次查询拿全部规则，`null` 即"无配置→回退现状"，fail-safe 语义最干净。
3. 与既有 `FinAccountSet`（一账套一行）、`FinAccountPeriod` 的账套级实体范式一致。
4. 凭证字白名单用 **JSON 列**（`nvarchar(max)` 存 `["记","收","付","转"]`），不建子表——四值封闭、无独立生命周期。

### 2.2 实体 ↔ DB 表字段全表

**表名 `FIN账套规则`**（模块前缀 `FIN` + 中文业务名）；C# 实体 `FinAccountSetRule : BaseEntity, IAccountSetScoped, ITenantScoped`。

**系统字段口径决策**：**对齐最贴近参照物 `FinAccountSet`**（`FinAccountSet.cs:5-27` 只有 `FCreatedTime`/`FUpdatedTime`，无 `F创建人`/`F更新人`/`F版本号`）。故本表**不加 `FCreatorName`/`FUpdaterName`/`FVersion`**——避免造出 Finance 现役表都没有的孤列破坏一致性。并发覆盖风险以"一账套一行 + 配置页低频写"接受（如后续确需并发令牌，另起任务统一给 Finance 账套级表补 `FVersion`）。

| C# 属性（F+PascalCase） | 类型 | DB 列（F+中文） | DB 类型/约束 | 说明 |
|---|---|---|---|---|
| `FID` | `long` | `FID` | `bigint IDENTITY PK` | 继承 `BaseEntity`，主键 |
| `FAccountSetId` | `long` | `F账套ID` | `bigint NOT NULL` | 账套隔离键（`IAccountSetScoped`），**手写 `.Where` 过滤** |
| `FTenantId` | `long` | `F租户ID` | `bigint NOT NULL DEFAULT 0` | 租户隔离键（`ITenantScoped`），DbContext 自动回填 + fail-closed |
| `FOrgId` | `long` | `F组织ID` | `bigint NOT NULL DEFAULT 0` | 组织列，**恒 0、无隔离语义**（见 §2.3） |
| `FRequireAuditSeparation` | `bool` | `F制单审核分离` | `bit NOT NULL DEFAULT 0` | **P0-1** 开关；默认 0=关（不校验，保持现状） |
| `FProfitAccountCode` | `string?` | `F本年利润科目编码` | `nvarchar(20) NULL` | **P0-2** 本年利润结转目标科目编码；`null`=回退 `"3103"` |
| `FRetainedAccountCode` | `string?` | `F未分配利润科目编码` | `nvarchar(20) NULL` | **P0-2** 未分配利润；`null`=回退 `"310405"` |
| `FEnabledVoucherWords` | `string?` | `F启用凭证字` | `nvarchar(max) NULL` | **P0-3** JSON 数组如 `["记","收","付","转"]`；`null`/空=回退全集 |
| `FStatus` | `int` | `F状态` | `int NOT NULL DEFAULT 1` | 软状态（1 启用） |
| `FCreatedTime` | `DateTime` | `F创建时间` | `datetime2 NOT NULL` | 系统字段（对齐 `FinAccountSet`） |
| `FUpdatedTime` | `DateTime` | `F更新时间` | `datetime2 NOT NULL` | 系统字段（对齐 `FinAccountSet`） |

**索引**：
- `IX_FIN账套规则_账套ID` = `HasIndex(e => e.FAccountSetId).IsUnique()`。
- `IX_FIN账套规则_租户ID` = `HasIndex(e => e.FTenantId)`（对齐 `FinAccountSetConfiguration.cs:23-24` 的租户索引写法）。

> **UNIQUE 取舍（显式记录）**：`FAccountSetId` 本身跨租户唯一（账套 ID 全局自增，`FinAccountSet.FID` 为 `BaseEntity long IDENTITY`），故 `UNIQUE(F账套ID)` 足够，无需 `UNIQUE(F账套ID, F租户ID)`。**该 UNIQUE 约束锁死"一账套一行"**——是当前 P0 宽表语义下的正确取舍（能防重复行）；若未来要扩为"一账套多行规则"（如按业务类型分行），需迁移去掉此 UNIQUE。此为可接受的设计取舍，非缺陷，此处显式标注。

### 2.3 隔离接口决策

- **实现 `ITenantScoped`（必须）**：`FTenantId` 进 DbContext 全局过滤器（`STOTOPDbContext.cs:125-179` 反射分派 + `205-286` 写入回填），读 fail-closed、写自动回填。**不在 Service 手赋值 `FTenantId`**。
- **实现 `IAccountSetScoped`（必须，但注意）**：**纯标记接口，无自动过滤器**（`IAccountSetScoped.cs:8-11`；不在 `STOTOPDbContext.cs:125-141` 分派链）。账套维度**每条查询必须手写 `.Where(r => r.FAccountSetId == accountSetId)`**，漏写=跨账套串数据（最大陷阱，见 §10）。
- **`IOrgScoped`：不实现**。理由：账套本身已随租户隔离，`FinAccountSet` 自身也只实现 `ITenantScoped`。**修正说明**：因不实现 `IOrgScoped`，`STOTOPDbContext.FillOrgIdForNewEntities` **不会**回填本表 `FOrgId`——该列将**恒为 `DEFAULT 0`、无任何隔离语义**，保留仅为与 `FinAccountSet` 列形对齐。**不要以为加了 `FOrgId` 属性/列组织隔离就生效**——那是误区。

---

## 3. 每项 P0 的服务端接线点（精确到 file:line + 方法名）

### 3.1 P0-1 制单人≠审核人

**注入点 A — 单笔审核** `VoucherService.AuditAsync(long id, string auditor)`（`VoucherService.cs:549-563`）：在 `voucher.FStatus = 2;`（**:554**）之前插入。

```csharp
// VoucherService.cs, AuditAsync 内, :553 GetOwnedVoucherAsync 之后、:554 设 FStatus 之前
var rule = await _accountSetRuleService.GetByAccountSetAsync(voucher.FAccountSetId); // null=无配置
if (rule?.FRequireAuditSeparation == true && voucher.FCreator == auditor)
    throw new InvalidOperationException("制单人不可审核本人凭证"); // GlobalExceptionMiddleware→400 透传
```

**注入点 B — 批量审核** `BatchAuditAsync(List<long> voucherIds, long auditorId, string auditorName)`（`VoucherService.cs:878-911`）：在 `voucher.FStatus = 2;`（**:895**）之前。批量语义定为 **skip 计数并分类留痕**（不整批失败，与"已审 skip"一致，但需与"已审"区分）：

```csharp
// :894 (FStatus==2 skip 分支之后) 、:895 设 FStatus 之前
var rule = await _accountSetRuleService.GetByAccountSetAsync(voucher.FAccountSetId);
if (rule?.FRequireAuditSeparation == true && voucher.FCreator == auditorName)
{
    selfAuditSkipCount++; // 与"已审核 skip"分开计数
    // 记操作日志：审计"谁的凭证因自审被拦"（见下）
    continue;
}
```

**返回文案必须改（补齐 missingPiece）**：`BatchAuditAsync` 现有返回文案硬编码为「成功审核 X 张，跳过 Y 张（已审核）」（`VoucherService.cs:908-910`）。若把"制单人自审"也并入同一 skip，文案「（已审核）」会误导用户。**改为区分两类跳过**：

```csharp
// :908-910 返回文案改为：
return ApiResult<object>.Success(new {
    successCount, skippedCount = alreadyAuditedCount + selfAuditSkipCount,
    message = $"成功审核 {successCount} 张，跳过 {alreadyAuditedCount} 张（已审核），" +
              $"{selfAuditSkipCount} 张（制单人不可自审）"
});
```

**操作日志（补齐 missingPiece）**：现有代码在设 `FStatus` **之后**才写 `OperationLog`（`:558`/`:900`）。**命中"制单人自审跳过"时应记一条操作日志**（便于审计"谁的凭证因自审被拦"）；单笔 `throw` 分支由 `GlobalExceptionMiddleware` 统一记录异常即可，不必额外补日志。

**构造注入**：`VoucherService` 构造函数（`VoucherService.cs:32-52`）增加 `IAccountSetRuleService accountSetRuleService` 参数并存字段。规则读取用 `voucher.FAccountSetId`（`GetOwnedVoucherAsync` 已带账套过滤，直接可用），**不再另解析账套 Id**。

**兜底**：`rule == null` 或 `FRequireAuditSeparation == false` → 不校验，完全保持现状放行（零行为变更）。

### 3.2 P0-2 结转科目映射

**文件**：`AccountPeriodService.cs`，方法 `CloseAsync(long periodId, long accountSetId = 0)`。`accountSetId` 已是方法参数（来自 `AccountPeriodController` `[FromQuery]`），**天然可用，无需读头**。

**⚠️ 账套过滤硬约束（补齐 missingPiece，重申）**：`GetByAccountSetAsync(accountSetId)` 内部**必须手写 `.Where(r => r.FAccountSetId == accountSetId)`**——`FinAccountSetRule` 是 `IAccountSetScoped`（无自动过滤器），漏写会跨账套取错规则。此约束在 §5.4 Service 实现要点已列，此处调用点再次重申，防止实现者误以为"实现了接口就自动隔离"。

**改造点（共 6 处，必须一起改，否则双真源）**：

| 行 | 现状 | 改为 |
|---|---|---|
| **:192** | `a.FCode == "3103"` 查本年利润 | 用 `profitCode` 变量 |
| **:314** | `a.FCode == "310405"` 查未分配利润 | 用 `retainedCode` 变量 |
| **:343** | `FAccountCode="3103", FAccountName="本年利润"` | `profitAccount.FCode / .FName` |
| **:344** | `FAccountCode="310405", FAccountName="利润分配-未分配利润"` | `retainedAccount.FCode / .FName` |
| **:348** | `FAccountCode="310405"...` | `retainedAccount.FCode / .FName` |
| **:349** | `FAccountCode="3103"...` | `profitAccount.FCode / .FName` |

**推荐做法**：在方法早期（:190 附近，`profitAccount` 解析前）一次性解析编码：

```csharp
// CloseAsync 内, :190 附近
var rule = await _accountSetRuleService.GetByAccountSetAsync(accountSetId); // 内部手写 .Where(FAccountSetId==accountSetId)
var profitCode   = string.IsNullOrWhiteSpace(rule?.FProfitAccountCode)   ? "3103"   : rule.FProfitAccountCode;
var retainedCode = string.IsNullOrWhiteSpace(rule?.FRetainedAccountCode) ? "310405" : rule.FRetainedAccountCode;

// :191-192 改：
var profitAccount = await _accountRepository.Query()
    .FirstOrDefaultAsync(a => a.FCode == profitCode && a.FAccountSetId == accountSetId);
if (profitAccount == null)
    return (false, $"未找到{profitCode}(本年利润)科目，无法结账");

// :314 改：a.FCode == retainedCode（同样带 && a.FAccountSetId == accountSetId）
// :343/344/348/349：FAccountCode/FAccountName 全部改用 profitAccount.*/retainedAccount.*
```

**构造注入**：`AccountPeriodService` 构造函数（`:31-57`）增加 `IAccountSetRuleService`。复用现有 `IRepository<FinAccount> _accountRepository` 做"编码→FID/FName"解析（规则表只存编码，仍需 Query 拿 `FID/FName`）。

**兜底**：`rule == null` 或编码为空 → 回退 `"3103"/"310405"`。`ReopenAsync`（靠 `FSource=="system:closing"`）与 `PreCloseCheckAsync` 不改。

### 3.3 P0-3 凭证字白名单单一真源

**三层真源收敛**：

1. **代码级真源**（补齐常量集合）— `VoucherConstants.cs:4-8`：
   ```csharp
   public static class VoucherWord
   {
       public const string Ji = "记";
       public const string Shou = "收";
       public const string Fu = "付";
       public const string Zhuan = "转";
       public static readonly string[] AllWords = { Ji, Shou, Fu, Zhuan }; // 全集=默认回退
   }
   ```
2. **账套级子集** — `FinAccountSetRule.FEnabledVoucherWords`（JSON）。服务提供 `GetEnabledVoucherWordsAsync(long accountSetId)`：读规则→解析 JSON→为空/null 回退 `VoucherWord.AllWords`。**保证至少含 `"记"`**（系统默认字，见 `STOTOP.Core/Interfaces/IVoucherService.cs` 中 `VoucherCreateDto.FVoucherWord` 默认 `"记"`）——解析后若不含 `Ji` 则并入 `Ji`。
3. **消费方改为读集合（4 处）**：
   - **导入校验** `VoucherExcelService.cs:298`：把 `voucherWord != "记" && ... != "转"` 四字面量替换为 `!enabledWords.Contains(voucherWord)`，报错文案改 `凭证字只能是 {string.Join("/", enabledWords)}`。**保持"收集错误不阻断解析"（`errors.Add` 后继续）风格**，不改为抛异常。`enabledWords` 在解析开始前按当前账套取一次。
   - **手工建/改凭证后端校验（补齐 missingPiece）** `VoucherService` 的 `CreateAsync`/`UpdateAsync`（`:296`/`:431` 调 `ValidateVoucher`）：**决策——补后端校验**。在 `ValidateVoucher` 之外、`Create`/`Update` 主路径新增一句 `FVoucherWord ∈ 启用集合` 校验（`GetEnabledVoucherWordsAsync(request.FAccountSetId)` 取集合，不含则 `throw new InvalidOperationException($"凭证字只能是 {...}")`）。理由：仅靠前端下拉 + 导入校验，直接调 API 传非启用字可绕过；补后端校验闭合此缺口。`SaveDraftAsync`（`:577`）草稿保存**不校验**（草稿允许中间态）。
   - **导出样例** `VoucherExcelService.cs:176` sampleRow 继续用 `VoucherWord.Ji`（模板样例保持"记"）。
   - **前端下拉** `VoucherEntry.vue:66-69`：删静态四 `<a-select-option>`，改 `v-for` 渲染从新 api 拉取的启用集合；`onMounted`/账套切换时拉取；`form.voucherWord` 默认 `'记'`（:726），空集合/未启用时回退 `'记'`；`:1095 getNextVoucherNumber(form.voucherWord,...)` 保证所选字在启用集合内。

**下游固定用法不动**（改造严禁触碰）：`VoucherTemplateService/JournalService/AssetService` 引用 `VoucherWord.Ji`；`AccountPeriodService.cs:291/354` 的 `FVoucherWord="转"`；CardFlow 自动凭证 `config.VoucherWord` 默认 `'记'`。

**兜底**：规则无配置/JSON 空 → 返回 `AllWords` 全集，导入/建改/下拉行为与现状完全一致。

---

## 4. 后端改动清单

### 4.1 实体 `Entities/FinAccountSetRule.cs`（新建）

照 `FinVoucherRule` 骨架，但**必须** `implements ITenantScoped, IAccountSetScoped` 并加 3 隔离列（`FinVoucherRule` 是 pre-tenant 表，这点不能抄）：

```csharp
public class FinAccountSetRule : BaseEntity, IAccountSetScoped, ITenantScoped
{
    public long FAccountSetId { get; set; }
    public long FTenantId { get; set; }
    public long FOrgId { get; set; }                           // 恒 0、无隔离语义
    public bool FRequireAuditSeparation { get; set; }          // P0-1
    public string? FProfitAccountCode { get; set; }            // P0-2
    public string? FRetainedAccountCode { get; set; }          // P0-2
    public string? FEnabledVoucherWords { get; set; }          // P0-3 JSON
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
```

### 4.2 配置类 `Configurations/FinAccountSetRuleConfiguration.cs`（新建）

照 `FinAccountSetConfiguration.cs:11-33`；**无需**在 ModuleExtensions 里 `ApplyConfiguration`——DbContext `ApplyConfigurationsFromAssembly` 按 Finance 程序集自动发现（`Program.cs:425`）。

```csharp
builder.ToTable("FIN账套规则");
builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
builder.Property(e => e.FRequireAuditSeparation).HasColumnName("F制单审核分离").HasDefaultValue(false);
builder.Property(e => e.FProfitAccountCode).HasColumnName("F本年利润科目编码").HasMaxLength(20);
builder.Property(e => e.FRetainedAccountCode).HasColumnName("F未分配利润科目编码").HasMaxLength(20);
builder.Property(e => e.FEnabledVoucherWords).HasColumnName("F启用凭证字"); // nvarchar(max)
builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
builder.HasIndex(e => e.FAccountSetId).IsUnique().HasDatabaseName("IX_FIN账套规则_账套ID");
builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_FIN账套规则_租户ID");
```

> `HasDefaultValue(0L)` 隐式把非主键 long 列设为 IDENTITY，但 `STOTOPDbContext OnModelCreating:103-115` 已全局把这类列 `ValueGenerated=Never`，照抄安全。

### 4.3 DTO `Dtos/AccountSetRuleDto.cs`（新建）

**决策——保留 `F` 前缀**（对齐既有 `AccountSetDto`：`FinAccountSet` 的 DTO 用 `FName/FCode/FStatus`，序列化 camelCase 得 `fName/fCode`；前端 `finance.ts:387` 亦是 `fName/fCode`）。故本表 DTO 字段带 `F` 前缀，序列化后前端得 `fAccountSetId` 等，与 `AccountSetDto` 一致。

```csharp
public class AccountSetRuleDto {
    public long FAccountSetId { get; set; }
    public bool FRequireAuditSeparation { get; set; }
    public string? FProfitAccountCode { get; set; }
    public string? FRetainedAccountCode { get; set; }
    public List<string> FEnabledVoucherWords { get; set; } = new(); // 前端拿数组，服务端 JSON 序列化到 FEnabledVoucherWords 列
}
public class UpdateAccountSetRuleRequest {
    public bool FRequireAuditSeparation { get; set; }
    public string? FProfitAccountCode { get; set; }
    public string? FRetainedAccountCode { get; set; }
    public List<string> FEnabledVoucherWords { get; set; } = new();
}
```

### 4.4 服务 `Services/Interfaces/IAccountSetRuleService.cs` + `Services/AccountSetRuleService.cs`（新建）

构造注入 `IRepository<FinAccountSetRule>`。

```csharp
public interface IAccountSetRuleService {
    Task<FinAccountSetRule?> GetByAccountSetAsync(long accountSetId);      // 供 Voucher/AccountPeriod 复用；null=无配置
    Task<AccountSetRuleDto> GetDtoAsync(long accountSetId);                // 前端读；无行时返回默认值 DTO（全集/关/空编码）
    Task<string[]> GetEnabledVoucherWordsAsync(long accountSetId);        // P0-3；空→AllWords，保证含"记"
    Task<AccountSetRuleDto> UpsertAsync(long accountSetId, UpdateAccountSetRuleRequest req, string operatorName);
}
```

实现要点：
- 所有查询 **手写 `.Where(r => r.FAccountSetId == accountSetId)`**（`IAccountSetScoped` 无自动过滤器）。一账套一行用 `FirstOrDefaultAsync`。
- **IDOR 防护（配合 §5.5 权限决策）**：因不挂 `[RequireAccountSetPermission]`，跨租户账套的隔离改由**租户全局过滤器兜底**——查询 `.Where(FAccountSetId==id)` 若 `id` 属其他租户账套，租户过滤器使其查不到（读到 `null` 即回退默认）。写路径同理由 `FillTenantIdForNewEntities` 硬墙拒绝跨租户写入。
- `Upsert`：查不到则 `new` + `AddAsync`（**不手赋 `FTenantId/FOrgId`**，DbContext 回填）；查到则 **`.AsTracking().FirstOrDefaultAsync` 再改再 `UpdateAsync`**（全局 NoTracking，见 `VoucherAutoService.UpdateRuleAsync:141-143`）。
- `FEnabledVoucherWords` 用 `System.Text.Json` 序列化/反序列化。
- **不注入 `IPlatformScopeFactory`**（业务层严禁绕租户墙）。

### 4.5 控制器 `Controllers/AccountSetRuleController.cs`（新建）

```csharp
[ApiController]
[Route("api/finance/account-set-rules")]
public class AccountSetRuleController : ControllerBase
{
    // GET  /api/finance/account-set-rules?accountSetId=  → ApiResult<AccountSetRuleDto>
    //   [RequirePermission(FinancePermissions.AccountSetRuleView)]
    // PUT  /api/finance/account-set-rules                → ApiResult<AccountSetRuleDto>
    //   [RequirePermission(FinancePermissions.AccountSetRuleEdit)]
    // 头优先解析账套：[FromHeader(Name="X-AccountSet-Id")] long accountSetId = 0，query 兜底（照 VoucherController.ResolveAccountSetId:33-38）
    // operatorName = User.Identity?.Name
}
```

全部 `ApiResult<T>.Success(...)/Fail(...)` 包装。

### 4.6 模块注册 `FinanceModuleExtensions.cs`（:47-55 追加）

```csharp
services.AddScoped<IAccountSetRuleService, AccountSetRuleService>();
```

> 无需 `ApplyConfiguration`（Finance 模块靠 `ApplyConfigurationsFromAssembly`）。

### 4.7 迁移（seeder V-number 引擎）`FinanceSeeder.cs`

steps 列表**当前末版本 V19**，追加 **V20**（`ValidateSteps` 强制紧接 V19，跳号/重复启动抛异常）：

```csharp
new(20, "P0(M6): 建 FIN账套规则 表(账套级规则 含 F租户ID 多租户隔离键)+索引", MigrateV20),
// ...
private static void MigrateV20(STOTOPDbContext ctx)
{
    if (!SeederHelper.IsSqlServer(ctx)) return;
    ExecSql(ctx, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'FIN账套规则')
CREATE TABLE [FIN账套规则](
    [FID] bigint IDENTITY(1,1) PRIMARY KEY,
    [F账套ID] bigint NOT NULL,
    [F租户ID] bigint NOT NULL DEFAULT 0,
    [F组织ID] bigint NOT NULL DEFAULT 0,
    [F制单审核分离] bit NOT NULL DEFAULT 0,
    [F本年利润科目编码] nvarchar(20) NULL,
    [F未分配利润科目编码] nvarchar(20) NULL,
    [F启用凭证字] nvarchar(max) NULL,
    [F状态] int NOT NULL DEFAULT 1,
    [F创建时间] datetime2 NOT NULL DEFAULT SYSDATETIME(),
    [F更新时间] datetime2 NOT NULL DEFAULT SYSDATETIME()
);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_FIN账套规则_账套ID')
CREATE UNIQUE INDEX [IX_FIN账套规则_账套ID] ON [FIN账套规则]([F账套ID]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_FIN账套规则_租户ID')
CREATE INDEX [IX_FIN账套规则_租户ID] ON [FIN账套规则]([F租户ID]);
");
}
```

> **建表 SQL 已同步删除 `F创建人`/`F更新人`/`F版本号` 列**（对齐 §2.2 实体决策，不留孤列）。**不预置任何数据行**：空表即"所有账套无配置→全部回退现状"，这是零行为变更上线的关键（见 §8）。

### 4.8 权限常量 `FinancePermissions.cs`（追加）

```csharp
public const string AccountSetRuleView = "finance:account-set-rule:view";
public const string AccountSetRuleEdit = "finance:account-set-rule:edit";
```

---

## 5. 前端改动清单

### 5.1 页面 `web/src/views/finance/AccountSetRuleConfig.vue`（新建）

**文件路径必须严格是 `web/src/views/finance/AccountSetRuleConfig.vue`**（与 baseline FID114 的 `F组件路径 finance/AccountSetRuleConfig` 一致，走 `permission store` 的 `import.meta.glob` 标准命中）。照抄 `AuxiliarySetting.vue` 范式：

```vue
<template>
  <div class="page-container">
    <PageHeader title="账套规则配置">
      <template #left><AccountSetSelector style="width:200px" /></template>
    </PageHeader>
    <!-- 表单：制单审核分离开关 / 本年利润科目编码 / 未分配利润科目编码 / 启用凭证字多选 -->
  </div>
</template>
<script setup lang="ts">
import { useAccountSetStore } from '@/stores/accountSet'
import { getAccountSetRule, updateAccountSetRule } from '@/api/finance'
const accountSetStore = useAccountSetStore()
async function loadData() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  const data = await getAccountSetRule({ accountSetId })
  // 填表
}
watch(() => accountSetStore.currentAccountSetId, loadData)
onMounted(loadData)
</script>
```

- 表单字段：`制单审核分离`（`a-switch`）、`本年利润科目编码` / `未分配利润科目编码`（`a-input`，留空=回退默认）、`启用凭证字`（`a-checkbox-group`，选项来自 `VoucherWord` 全集 `记/收/付/转`）。
- **锁定语义提示（补齐 missingPiece，见 §8.2）**：
  - 移除凭证字时，若该字已被历史凭证使用，提示"已有 N 张凭证使用该字，移除后仅影响新建"；`"记"` 复选框**设为不可取消**（disabled + 常勾）。
  - 修改结转科目编码时，若该账套本年度**已有结账期**，弹警告文案"该账套本年度已有结账期，修改仅影响下次结转，历史结转凭证需反结账后重结"。已结账期数据来源见 §5.2 `hasClosedPeriod`。
- 财务配置页范式**不用 `.page-card`**，用 `page-container` + 自定义 toolbar/main-content（照 `AuxiliarySetting.vue`）。
- `X-AccountSet-Id` 头由 `request.ts:69` 从 `localStorage('currentAccountSetId')` 自动注入；页面 api 仍把 `accountSetId` 作 query 传（双保险，后端头优先 query 兜底）。

### 5.2 API `web/src/api/finance.ts`（文件末尾新增分区）

**类型就地 export 在 `api/finance.ts`**（本仓无 `web/src/types/finance.ts`，勿臆造）；字段 `fXxx`（对齐 `AccountSetDto`）；前端 api 函数**不加 `Async` 后缀**。

```ts
// ==== 账套规则 ====
export interface FinAccountSetRuleDto {
  fAccountSetId: number
  fRequireAuditSeparation: boolean
  fProfitAccountCode: string | null
  fRetainedAccountCode: string | null
  fEnabledVoucherWords: string[]
}
export function getAccountSetRule(params: { accountSetId: number }) {
  return get<FinAccountSetRuleDto>('/finance/account-set-rules', params)
}
export function updateAccountSetRule(data: FinAccountSetRuleDto) {
  return put<FinAccountSetRuleDto>('/finance/account-set-rules', data)
}
// 已结账期探测（供配置页改结转科目时弹警告）——复用现有结账期查询接口
export function hasClosedPeriod(params: { accountSetId: number }) {
  return get<boolean>('/finance/account-periods/has-closed', params)
}
```

> `hasClosedPeriod`（补齐 missingPiece）：**决策——复用 `AccountPeriodService` 现有已结账期查询**（判断该账套本年度是否存在已结账 period 或 `FSource=="system:closing"` 凭证），后端在 `AccountPeriodController` 加一个只读 `GET /api/finance/account-periods/has-closed?accountSetId=` 动作，内部 `.Where(p => p.FAccountSetId==id && p.FStatus==已结账)` 返回 `bool`。配置页据此决定是否弹警告，DB 层**不加硬锁**（P0 不做强锁，见 §8.2）。

### 5.3 P0-3 前端下拉改造 `VoucherEntry.vue`

- 删 `:66-69` 静态四 option，改 `v-for` 渲染 `enabledVoucherWords`（`onMounted` + 账套切换时调 `getAccountSetRule` 拉取 `fEnabledVoucherWords`）。
- `form.voucherWord` 默认 `'记'`（:726）；空集合/未启用时回退 `'记'`。
- `:1095 getNextVoucherNumber(form.voucherWord,...)` 保证所选字在启用集合内。

### 5.4 路由 `web/src/router/routes.ts`

`/finance/*` 是 `Layout.children` 静态子路由（**非动态菜单生成**），在 finance 段落（约 :348 `auxiliary-setting` 后）追加：

```ts
{
  path: 'finance/account-set-rules',
  name: 'AccountSetRuleConfig',
  component: () => import('@/views/finance/AccountSetRuleConfig.vue'),
  meta: { title: '账套规则', icon: 'ControlOutlined', module: 'finance' }
}
```

> `AdminConfigCenter.vue:101` 入口已存在，路由建好自动点亮，**无需改此文件**。

### 5.5 权限决策（修正 high 问题：`RequirePermission` vs `RequireAccountSetPermission` 混淆）

**决策——走菜单级 `[RequirePermission]`，绝不叠加 `[RequireAccountSetPermission]`。**

二者是**不同的授权系统**，不能叠加：
- `[RequirePermission("finance:account-set-rule:view")]`（`RequirePermissionAttribute.cs:30-56`）查 `SYS用户角色 JOIN SYS角色权限 JOIN SYS功能权限` 匹配 `F编码`——菜单/按钮级。
- `[RequireAccountSetPermission(...)]`（`RequireAccountSetPermissionAttribute.cs:44-86`）经 `IAccountSetAuthorizationService.HasPermissionAsync(userId, accountSetId, code)` 查 `accountset:*` **账套授权表**——账套级细粒度。

**若把菜单级码 `finance:account-set-rule:view` 传进 `[RequireAccountSetPermission]`，非 admin 用户即使在 `SYS功能权限` 挂了该按钮权限也会被拒**（账套授权表里没有该 grant）。且账套级授权与 §1.2「不做账套级细粒度授权」自相矛盾。

**最终做法**：
- Controller 只挂 `[RequirePermission(FinancePermissions.AccountSetRuleView/Edit)]`。
- 「账套∈租户」的 IDOR 防护**不靠 `[RequireAccountSetPermission]`**，改由 **Service 内 `.Where(FAccountSetId==id)` + 租户全局过滤器兜底**（跨租户账套查不到→读到 `null`→回退默认；写入由 `FillTenantIdForNewEntities` 硬墙拒绝）。这与 §1.2 一致，也不额外接账套授权 UI。

---

## 6. 菜单/权限激活

菜单/权限存 `SYS功能权限` 表（`SysPermission`），由 `baseline-reference-data.json` 经 `BaselineReferenceDataSeeder` 逐行 upsert（key=FID）。

| 改动 | 文件:行 | 动作 |
|---|---|---|
| 激活菜单 | `baseline-reference-data.json:1187` | FID114 的 `F是否可见` **0 → 1**（**不是改 F状态**，F状态已=1）。确认父 `FID137` 存在。 |
| 新增按钮权限 view | `baseline-reference-data.json` `SYS功能权限` 表内追加行 | 见下完整字段 |
| 新增按钮权限 edit | 同上 | 见下完整字段 |
| 权限常量 | `FinancePermissions.cs` | 追加 `AccountSetRuleView`/`AccountSetRuleEdit`（§4.8） |
| 角色授予（可选） | `SystemSeeder.cs:458` | 仿现有幂等 `INSERT ... WHERE F编码 IN(...) AND NOT EXISTS` 给 admin 角色授予新码 |

**两条按钮权限行完整字段（补齐 missingPiece——baseline 以 FID 为 upsert key，必须分配未占用新 FID）**：

> **FID 分配前置动作**：开工时先在 `baseline-reference-data.json` 的 `SYS功能权限` 表内 grep 现有最大 FID，取两个未占用值（下例用占位 `{FID_view}`/`{FID_edit}`，实现者按实际最大值 +1、+2 填入；确认全表无重复）。

| 列 | view 行 | edit 行 |
|---|---|---|
| `FID` | `{FID_view}`（未占用） | `{FID_edit}`（未占用） |
| `F名称` | `查看账套规则` | `编辑账套规则` |
| `F编码` | `finance:account-set-rule:view` | `finance:account-set-rule:edit` |
| `F类型` | `按钮` | `按钮` |
| `F父ID` | `114` | `114` |
| `F路由` | `null` | `null` |
| `F组件路径` | `null` | `null` |
| `F图标` | `null` | `null` |
| `F排序` | `1` | `2` |
| `F状态` | `1` | `1` |
| `F是否可见` | `1` | `1` |
| `F创建时间` | `2026-07-04 00:00:00`（与 baseline 其余行格式一致） | 同 |
| `F更新时间` | 同上 | 同 |

**注意**：
- baseline 改动靠 SHA256 指纹触发重对齐（`force=false`）；疑未生效用 `--init-database force`。
- `admin` 用户经 `RequirePermissionAttribute` 短路放行——**验证非 admin 用户前别被 admin 误导为已生效**。
- `F是否可见=0` 时前端路由仍注册（`orgContext.menus` 不过滤可见性 + `buildRoutes` 不看 `isVisible`），改 1 才进侧边栏。`PermissionService.GetMenuTreeAsync` 会过滤可见性但前端主路由不走它，勿据它误判。
- baseline JSON 缩进=2 空格，中文列名直接写。

---

## 7. 运营交接（补齐 missingPiece）

V20 只建空表 → 上线瞬间所有账套走回退分支（零行为变更）。**但这意味着功能上线后若无人配置=永远走回退=等于没做**。故须交接一份运营 checklist：

- **P0-1 制单审核分离**：财务负责人逐账套决定是否开启（默认关）。建议对有内控要求的正式账套开启。
- **P0-2 结转科目**：仅当账套的本年利润/未分配利润科目编码**不是**标准 `3103/310405` 时才需配置；标准科目账套留空即可（回退正确）。
- **P0-3 启用凭证字**：默认全集（记/收/付/转）。仅当某账套要限制可用凭证字时才收窄；`"记"` 不可移除。

配置入口：`/finance/account-set-rules`，切账套逐个配置。

---

## 8. 锁定语义与零行为变更保证

### 8.1 零行为变更上线（核心保证）

**"无配置 = 现状"** 是 fail-safe 铁律，缺行绝不更严格或更宽松：

| 项 | 无配置（`rule==null` 或字段空）时的行为 |
|---|---|
| P0-1 | `FRequireAuditSeparation` 默认 `false` → **不校验，放行** |
| P0-2 | 编码为空 → 回退 `"3103"/"310405"` 字面量 |
| P0-3 | JSON 空/null → 回退 `VoucherWord.AllWords` 全集（导入/建改/下拉一致） |

V20 迁移**只建空表、不插数据**，上线瞬间所有账套走回退分支，行为与当前完全一致。启用是运营在配置页逐账套显式打开。

### 8.2 结转科目/凭证字锁定语义

- **结转科目改配置**：账套已有 `FSource=="system:closing"` 结转凭证后，**修改结转科目编码给前端警告但不硬禁**（P0 不做强锁）。改了只影响**下次**结转；已生成的结转凭证需先 `ReopenAsync` 反结账再改再重结。配置页警告数据源=`hasClosedPeriod`（§5.2）。DB 层不加锁。
- **凭证字启用子集缩小**：把某已被历史凭证使用的字移除，**不回溯校验历史凭证**（历史 `FVoucherWord` 不变，只影响新建/导入/手工建改）。配置页移除时提示"已有 N 张凭证使用该字，移除后仅影响新建"。**必须保证 `"记"` 不可移除**（服务端 `GetEnabledVoucherWordsAsync` 强制并入 `Ji`，前端复选框 disabled 常勾）。

---

## 9. 分步实施 + 验证点 + 测试点（xUnit）

用 `scripts/dev/build-filter.ps1 finance` 只编译 Finance 闭包；`scripts/dev/test-dotnet.ps1 Finance` 跑测试。

| 步 | 内容 | 验证点 | xUnit 测试点（`tests/STOTOP.Module.Finance.Tests`，`TestDbContextFactory` + `RegisterModuleAssembly(Finance)`） |
|---|---|---|---|
| 1 | 实体+配置类+DTO | `build-filter finance` 通过；`ApplyConfigurationsFromAssembly` 发现新配置 | `账套规则表映射正确_列名为F中文`（InMemory 插一行读回，断言属性往返；断言无 F创建人/F版本号列） |
| 2 | V20 迁移 | dev 库建表成功；`ValidateSteps` 不报错（V20 紧接 V19）；`SYS迁移历史` 有 Finance V20 | 迁移引擎在集成套件跑通即可 |
| 3 | Service | 手工 `Upsert` 后 `Get` 往返一致 | `无配置时启用凭证字回退全集且含记`；`Upsert后按账套读回一致`；`跨账套不串数据`（两账套各插一行，查 A 只得 A——验证手写 `.Where`） |
| 4 | P0-1 接线（`AuditAsync`/`BatchAuditAsync` + 构造注入 + 返回文案 + 自审留痕） | 开开关后同一人自审被拒；批量文案区分两类跳过 | `开关开_制单人审核本人凭证抛InvalidOperationException`；`开关关_制单人可审核本人凭证`；`批量审核_制单等于审核时计入selfAuditSkip不整批失败`；`批量返回文案区分已审核与制单人自审`；`不同人审核放行` |
| 5 | P0-2 接线（`CloseAsync` 6 处 + 构造注入） | 配自定义结转科目后分录用新编码；无配置回退 | `结账_有规则时用配置的结转科目编码`；`结账_无规则时回退3103和310405`；`结转分录FAccountCode与解析科目一致_无双真源` |
| 6 | P0-3 常量集合 + 导入校验 + 建改后端校验 + 导出样例 | 导入非启用字报错文案动态；手工建非启用字被拒；导出样例仍"记" | `导入校验_值不在启用集合时收集错误不阻断`；`导入校验_默认全集时四字均通过`；`Create_凭证字不在启用集合时抛异常`；`SaveDraft_不校验凭证字`；`常量AllWords含记收付转` |
| 7 | 控制器 + 模块注册 | Swagger 出现 `GET/PUT /api/finance/account-set-rules`；`[RequirePermission]` 生效 | `非admin无view权限时403`（若集成套件支持）；`跨租户账套ID读到null回退`（验证租户过滤器兜底 IDOR） |
| 8 | 前端页面+api+类型+路由+VoucherEntry 下拉 | `npm run type-check` 全绿；`npm run lint:style` 无裸 hex；切账套读回、下拉动态渲染 | 前端无 xUnit；靠 type-check + 手工点验 |
| 9 | 菜单激活（FID114 可见=1 + 两按钮权限行含新 FID + 权限常量） | seeder 重对齐后菜单进侧边栏；非 admin 授权后可见按钮 | **`PlatformBypassAuditTests` / 租户隔离门禁必须仍绿**（新表 `ITenantScoped` 标注正确，漏标会被抓） |
| 10 | 整体回归 | `scripts/dev/check-health.ps1`；结账/审核/导入/建改端到端手工验证 | 全量 `test-dotnet.ps1`；`CardFlow.Tests` flaky 需多跑判定 |

> 关键门禁：步 9 后 `PlatformBypassAuditTests` 与租户隔离测试必须仍绿——这是新表 `ITenantScoped` 标注是否正确的守卫。

---

## 10. 风险与回滚

| 风险 | 说明 | 缓解 |
|---|---|---|
| **`IAccountSetScoped` 无自动过滤器** | 以为实现接口就自动按账套隔离，漏写 `.Where(FAccountSetId==id)` → 跨账套串数据 | 每条查询强制手写 `.Where`；步 3 测 `跨账套不串数据`；§3.2 调用点已重申；code review 逐查询核对 |
| **权限系统混淆** | 误叠加 `[RequireAccountSetPermission]`（账套授权表）→ 非 admin 用户即使有菜单权限也 403 | 只挂 `[RequirePermission]`（§5.5）；IDOR 靠租户过滤器兜底；步 7 测非 admin 403 与跨租户读空 |
| **P0-1 同名误判** | `FCreator/FAuditor` 存显示名非 userId，同名误判 | P0 接受字符串比对（已声明）；精确比对需另起任务加 `FCreatorId/FAuditorId` |
| **P0-2 双真源残留** | 只改 `:192/:314`、漏改 `:343-349` 分录硬写 | 6 处一起改；步 5 测 `分录FAccountCode与解析科目一致` |
| **P0-3 后端绕过** | 手工调 API 传非启用字绕过前端 | Create/Update 主路径补后端校验（§3.3）；步 6 测 `Create_非启用字抛异常` |
| **凭证字移除"记"** | 运营误移"记"→自动凭证/默认字断裂 | 服务端强制并入 `Ji`；前端"记"复选框 disabled 常勾 |
| **后台 Job 租户上下文缺失** | 无 `CurrentTenantId` 读 `FinAccountSetRule` 读空 → 自动回退 | 期望行为（无配置=现状）；Job 需真读须显式设租户或平台层 `IPlatformScopeFactory.Enter`（仅平台层） |
| **seeder 版本号冲突** | 并发分支各加 V20 → 合并跳号，`ValidateSteps` 抛异常 | 合并前核对 `FinanceSeeder` 末版本，冲突重排为紧接末版本 |
| **按钮权限 FID 冲突** | 新增两行 FID 若撞已占用值 → upsert 覆盖错行 | 开工前 grep 全表最大 FID，取未占用值，确认无重复（§6） |
| **worktree 副本误改** | `.claude/worktrees/stage4/` 有同名文件 | 只改主树 `D:\STOTOP_Fable\src\|web`，勿动 worktree |

### 回滚

- **前端**：撤 `routes.ts`/`finance.ts`/`AccountSetRuleConfig.vue`/`VoucherEntry.vue` 改动即恢复静态下拉。
- **后端逻辑**：全部 fail-safe 回退现状，**即使表存在但无数据行，行为已等同回滚**；彻底回滚则还原 `AuditAsync`/`BatchAuditAsync`/`CloseAsync`/`VoucherExcelService`/`Create·Update` 校验点、移除 Service 注入。
- **表**：`FIN账套规则` 保留无害（空表不影响任何逻辑）；如需删表另起 seeder 步骤 `DROP TABLE`（不倒退版本号，追加新版本做 drop）。
- **菜单**：baseline FID114 `F是否可见` 改回 0 即从侧边栏隐藏。

---

## 11. 最小改动文件清单（开工用）

### 新增（后端 5 + 前端 1）

| # | 文件 | 内容 |
|---|---|---|
| N1 | `src/STOTOP.Module.Finance/Entities/FinAccountSetRule.cs` | 实体，`: BaseEntity, IAccountSetScoped, ITenantScoped`（§4.1） |
| N2 | `src/STOTOP.Module.Finance/Configurations/FinAccountSetRuleConfiguration.cs` | 配置类，`ToTable("FIN账套规则")` + HasColumnName（§4.2） |
| N3 | `src/STOTOP.Module.Finance/Dtos/AccountSetRuleDto.cs` | DTO（保留 F 前缀）+ `UpdateAccountSetRuleRequest`（§4.3） |
| N4 | `src/STOTOP.Module.Finance/Services/Interfaces/IAccountSetRuleService.cs` + `Services/AccountSetRuleService.cs` | 服务接口+实现（§4.4） |
| N5 | `src/STOTOP.Module.Finance/Controllers/AccountSetRuleController.cs` | 控制器，`[RequirePermission]`（§4.5、§5.5） |
| N6 | `web/src/views/finance/AccountSetRuleConfig.vue` | 配置页（路径须精确，§5.1） |

### 修改（后端 6 + 前端 3）

| # | 文件 | 改动 |
|---|---|---|
| M1 | `src/STOTOP.Module.Finance/Services/VoucherService.cs` | 构造注入 `IAccountSetRuleService`；`AuditAsync:554` 前加校验；`BatchAuditAsync:895` 前加 skip+分类计数、`:908-910` 改文案、自审留痕；`Create:296`/`Update:431` 主路径加凭证字启用集合校验（§3.1、§3.3） |
| M2 | `src/STOTOP.Module.Finance/Services/AccountPeriodService.cs` | 构造注入 `IAccountSetRuleService`；`CloseAsync` 6 处（:192/:314/:343/:344/:348/:349）改按规则读编码（§3.2） |
| M3 | `src/STOTOP.Module.Finance/Services/VoucherExcelService.cs` | `:298` 导入校验改读启用集合（导出样例 `:176` 不动，§3.3） |
| M4 | `src/STOTOP.Module.Finance/Constants/VoucherConstants.cs` | `:4-8` 补 `Shou/Fu/Zhuan` + `AllWords`（§3.3） |
| M5 | `src/STOTOP.Module.Finance/Controllers/AccountPeriodController.cs` | 加 `GET /has-closed` 只读探测动作（§5.2） |
| M6 | `src/STOTOP.Module.Finance/FinancePermissions.cs` | 追加 `AccountSetRuleView/Edit` 常量（§4.8） |
| M7 | `src/STOTOP.Module.Finance/FinanceModuleExtensions.cs` | `:47-55` `AddScoped<IAccountSetRuleService, AccountSetRuleService>()`（§4.6） |
| M8 | `src/STOTOP.WebAPI/Data/Seeders/FinanceSeeder.cs` | steps 追加 `new(20,...)` + `MigrateV20`（§4.7） |
| M9 | `src/STOTOP.WebAPI/Data/Seeders/Baseline/baseline-reference-data.json` | `:1187` FID114 `F是否可见 0→1`；`SYS功能权限` 追加两按钮权限行（新 FID，§6） |
| M10（可选） | `src/STOTOP.WebAPI/Data/Seeders/SystemSeeder.cs` | `:458` 仿幂等 INSERT 给 admin 授予新码（§6） |
| M11 | `web/src/api/finance.ts` | 末尾新增账套规则分区（interface `fXxx` + `getAccountSetRule`/`updateAccountSetRule`/`hasClosedPeriod`，§5.2） |
| M12 | `web/src/router/routes.ts` | Layout.children `:348` 附近追加路由（§5.4） |
| M13 | `web/src/views/finance/VoucherEntry.vue` | `:66-69` 静态 option 改动态；`:726` 默认值回退；`:1095` 校验（§5.3） |

> 注：`AdminConfigCenter.vue:101` 入口已存在，**无需改动**。