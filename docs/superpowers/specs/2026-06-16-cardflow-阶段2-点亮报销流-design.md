# CardFlow 阶段2：点亮费用报销流（设计稿）

> 日期：2026-06-16　状态：设计已确认，待 writing-plans 细化
> 上游：三阶段简化路线 [`2026-06-16-cardflow-简化瘦身-design.md`](2026-06-16-cardflow-简化瘦身-design.md)；阶段1（清死代码）已完成。
> 本文为方案文档，**不修改任何代码**。所有现状结论均带真实 file:line 证据（2026-06-16 采样）。

---

## 0. 背景与目标

阶段1 清掉了死代码，但 CardFlow 仍"没真正用起来"。诊断主因是**最后一公里**：没有可克隆的种子流程，且"从模板创建"入口恒空（`GetTemplatesAsync` 过滤 `FOrgId==0` 而种子是 `FOrgId=192`）。阶段2 = 让 CardFlow **第一次端到端跑通一条真实审批流**。

### 目标
1. seed 一条可"从模板创建"的**费用报销审批流**（发起→部门负责人→财务→完成），人工审批。
2. 修"从模板创建恒空"——新增显式 `FIsTemplate` 标志（决策 D3）。
3. 补**第一条端到端测试**（发起→审批→完成）。
4. WF 触发动作新增 `cardflow.apply`，让"发起审批"成为一级入口。

### 非目标
- ❌ 不接自动凭证（决策 D5，留后续）。
- ❌ 不做 `role` 取人策略——角色种子是 GZip+Base64 压缩的（`SystemSeeder.cs:27-31`），读不到"部门负责人/财务"角色编码，也无挂角色的种子用户；模板默认用 `fixedUsers→管理员` 占位，留待角色体系明确后再升级。
- ❌ 模板不带动态总经理审批（动态策略是阶段1 桶B 冻结件）。
- ❌ 不动前端流程设计器。

### 已锁定决策
| 决策 | 结论 |
|---|---|
| 样板流构建方式 | **新建 FOrgId=0 全局模板，复用现有 FYBS 扁平 schema**（不动现有 192 流） |
| 模板审批取人策略 | **`fixedUsers→管理员(userId=1)` 占位**（克隆到组织后由用户改真实审批人） |
| 模板入口口径（D3） | **新增 `FIsTemplate` 列**，`GetTemplatesAsync` 改按其过滤 |
| E2E 测试形式 | **两者都要**：InMemory 服务级 E2E + SQL Server 冒烟（默认 Skip） |
| 自动凭证（D5） | 本阶段**不接** |

---

## 1. 现状事实（已核实）

**Schema 管理（无 EF Migrations）**：`DatabaseMigrator.RunInitializationPipeline()` 两步——`CreateMissingTables`（`DatabaseSeederAdapter.cs:489`，从 EF 模型建新库缺失表）+ `SchemaAutoSync`（`DatabaseSeederAdapter.cs:147`，开发环境列级 ALTER）。启动时 `Program.cs:462-476` 调 `seeder.MigrateAll(dbCtx)`。`CardFlowSeeder` 版本化迁移记录在 `[SYS迁移历史]`（`(F模块,F版本号)` 唯一，`MigrationRunner.cs:64-66`），按版本号顺序、每步独立事务（`MigrationRunner.cs:119-183`）。

**现有 FYBS 报销流**：`CF卡片流程` FID=1354（`FFlowCode=FYBS`、`FOrgId=192`）/ `CF流程版本` FID=1347。已有扁平 `F卡片SchemaJSON`（applicant/department/amount/category/description/attachments，`CardFlowSeeder.cs:116-117`）+ `F明细SchemaJSON`。节点 `expense_supervisor`(5011) / `expense_finance`(5018) 均为 `fixedUsers→userId:1` 占位、`single`；带动态策略 8003（amountMatrix ≥5000 总经理）+ 路由 7021/7022。

**角色/用户**：`SysRole.FCode` 是角色编码；`ApproverResolver` 的 role 策略匹配 `role.FCode==roleCode`（`ApproverResolver.cs:95`）。admin 用户 FID=1。无挂"部门负责人/财务"角色的种子用户。

**测试基建**：`TestDbContextFactory.cs` 用 `UseInMemoryDatabase`（每测试 Guid 隔离）。`FlowEngineReturnToStageTests.cs:939-967` 的 `CreateEngine(db)` 给出 `FlowEngineService` 全依赖装配；`:777-913` 给出"seed 版本+节点+路由+卡片→驱动 ApproveAsync"的集成模式（可照抄）。无任何 SQL Server/LocalDB/SQLite 测试基建。

---

## 2. 组件 1：`FIsTemplate` 列 + 模板发现修复

**实体与映射（新库自动建列）：**
- `CfFlowDefinition.cs`：加 `public bool FIsTemplate { get; set; } = false;`
- `CfFlowDefinitionConfiguration.cs`：加 `builder.Property(e => e.FIsTemplate).HasColumnName("F是否模板").HasDefaultValue(false);`

**存量库补列（CardFlowSeeder MigrateV21）：**
```sql
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = N'CF卡片流程' AND COLUMN_NAME = N'F是否模板')
ALTER TABLE [CF卡片流程] ADD [F是否模板] bit NOT NULL DEFAULT 0;
```
（在 `CardFlowSeeder.cs:19-44` 的 steps 列表末尾注册 `new(21, "...", MigrateV21)`，方法体用 `SeederHelper.IsSqlServer` 守卫 + `ExecSql`。）

**模板过滤口径（`FlowDefinitionService.GetTemplatesAsync`，约 1017-1021 行）：**
```csharp
.IgnoreQueryFilters()
.Where(x => x.FIsTemplate && x.FStatus == "published")   // 原: x.FOrgId == 0 && ...
```

**自建模板也标记（`SaveAsTemplateAsync`）：** 创建模板副本时置 `FIsTemplate = true`（保持 FOrgId=0）。

**DTO（可选小改）：** `FlowDefinitionDto` 加 `bool IsTemplate`，`MapToDto` 填充，供前端显示"模板"标记。

---

## 3. 组件 2：费用报销全局模板种子（并入 MigrateV21）

**`CF卡片流程`（稳定种子 FID，实现时取未占用值，如 1360）：**
`FFlowCode='FYBS_TEMPLATE'`、`FFlowName='费用报销（模板）'`、`FOrgId=0`、`F是否模板=1`、`FStatus='published'`、`F标题模板='{applicant}-费用报销-{amount}元'`、`F触发配置JSON` = human（**不含 fileUpload**，确保出现在"我要发起"）。

**`CF流程版本`（current、published）：**
- `F卡片SchemaJSON` = 照搬已验证扁平字段：applicant(user,required)/department(department,required)/amount(amount,required,detailSum)/category(select：差旅费/办公费/招待费/交通费/通讯费/其他)/description(textarea,required)/attachments(attachment)。
- `F明细SchemaJSON` = 明细行：expenseDate/expenseType/description/amount/invoiceNo。

**2 个干净人工节点（线性，靠 `F排序号` 推进，无需路由规则、无动态策略）：**
| 节点键 | 名称 | 排序 | 类型 | 审批模式 | 取人 |
|---|---|---|---|---|---|
| `tpl_expense_dept` | 部门负责人审批 | 1 | human/card | single | `fixedUsers→{userId:1,管理员}` |
| `tpl_expense_finance` | 财务审批 | 2 | human/card | single | `fixedUsers→{userId:1,管理员}` |

财务节点通过 → 卡片置 `completed`。占位审批人为模板默认，克隆到组织后由用户改真实人。

> 幂等：种子用 `IF NOT EXISTS` 守卫，FID 固定，重复执行不重插。

---

## 4. 组件 3：WF 触发动作 `cardflow.apply`

`WorkflowSeeder` 增一条触发动作 `cardflow.apply` → 指向"我要发起"页（默认 `/cardflow/home`，实现时确认确切路由），使"发起审批"成为一级入口（现状唯一入口 `cardflow.start`→`/cardflow/upload` 偏"上传"语义）。沿用 `WorkflowSeeder` 既有触发动作的 INSERT 格式与幂等守卫。

---

## 5. 组件 4：端到端测试（两者都要）

**(A) InMemory 服务级 E2E —— `ExpenseApprovalE2ETests`：**
照搬 `FlowEngineReturnToStageTests` 的 `CreateEngine(db)` 与 seed 模式：
1. seed `SysUser`(admin userId=1) + `CfFlowVersion` + 2 个 `CfStageDefinition`（`tpl_expense_dept` 排序1 → `tpl_expense_finance` 排序2，均 fixedUsers→1、single）。
2. 驱动全生命周期：`SubmitAsync`（建卡+进第一节点）→ `ApproveAsync`（部门负责人，userId=1）→ `ApproveAsync`（财务，userId=1）。
3. 断言：卡片 `FStatus=completed`；两节点实例 `completed`；待办随节点流转（部门待办关闭、财务待办生成、最终无未决待办）。
- 同时保留既有 96 测试全绿。

**(B) SQL Server 冒烟 —— `SqlServerSchemaSmokeTests`：**
- `[Fact]` 但读环境变量 `STOTOP_TEST_CONNECTION`，**未设则 Skip**（不进常规 CI）。
- 设置后：`UseSqlServer(connStr)` 建临时库 → 跑 `DatabaseMigrator` 建表 → 查 `INFORMATION_SCHEMA.COLUMNS` 断言 `[CF卡片流程]` 含 `[F是否模板]` 列 → 清理。
- 不引入新 EF provider（项目已有 `UseSqlServer`）。

---

## 6. 验证 / 回滚

- `dotnet test tests/STOTOP.Module.CardFlow.Tests`：新 InMemory E2E 绿 + 既有 96 仍绿。
- `dotnet build src/STOTOP.WebAPI/STOTOP.WebAPI.csproj`：0 错误。
- 可选：设 `STOTOP_TEST_CONNECTION` 跑 SQL 冒烟。
- 人工验收：启动应用 → "从模板创建"返回"费用报销（模板）"→ 克隆到当前组织 → "我要发起"可发起 → 部门负责人/财务两级审批走完 → 卡片完成。
- 回滚：每改动独立 commit；`FIsTemplate` 列与种子均幂等，可安全重跑；过滤口径改动可单独 revert。

---

## 7. 待实现时确认的小项（impl-time）

| 编号 | 待确认 | 默认 |
|---|---|---|
| I1 | `cardflow.apply` 的确切目标路由 | `/cardflow/home`（"我要发起"入口；实现时核 `routes.ts` 与 `TriggerActionPanel`） |
| I2 | `FlowEngineService.SubmitAsync` 的精确签名（E2E 用） | 实现时按真实签名构造；若 Submit 路径复杂，退化为"seed 卡片于第一节点 active 态 + 两次 ApproveAsync" |
| I3 | 模板种子 FID（流程/版本/节点）取值 | 取未占用区间（核对 CardFlowSeeder 现有 FID 占用，如流程 1360 / 版本 1361 / 节点 5030+） |
| I4 | `category` 等字段 type 是否落在 SchemaRenderer 支持枚举内 | 照搬 FID1347 已验证 schema 的同名 type（已被渲染器接受） |

---

## 8. 任务分解预览（供 writing-plans）

1. `FIsTemplate` 列：实体属性 + Configuration 映射 + MigrateV21 ALTER（幂等）。
2. `GetTemplatesAsync` 过滤口径改 + `SaveAsTemplateAsync` 置标记 + DTO 字段。
3. 费用报销全局模板种子（流程+版本+2 节点，并入 MigrateV21，幂等）。
4. WF 触发动作 `cardflow.apply` 种子。
5. InMemory 服务级 E2E 测试。
6. SQL Server 冒烟测试（Skip 保护）。
7. 收尾验证（全量单测 + WebAPI 编译 + 人工验收清单）。

> 转入 writing-plans 后逐项细化为可执行步骤。
