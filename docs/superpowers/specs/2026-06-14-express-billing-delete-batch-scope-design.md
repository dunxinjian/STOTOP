# Express 计费结果删除：补加批次/状态限定，防丢成功行

- 日期：2026-06-14
- 范围：
  - `src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs`（`DeleteExistingResults`）
  - `src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs`（Phase A / Phase B 两处调用）
  - `src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs`（`DeleteOldBillingResultsAsync` 及其调用）
  - 测试：新增 `tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs`；扩展 `scripts/tests/express-billing-delete-cascade-contract.test.mjs`
- 优先级：中（预防性，非线上正在发生的故障）

## 背景与缺陷

计费结果表 `[EXP出港运单_计费结果]` 由 `PricingEngine` 分两阶段写入：Phase A 写成功行（`F计算状态=1`）、Phase B 写失败行（`F计算状态=2`），各自在写入前调用 `DeleteExistingResults` 按运单号删旧结果（重跑去重）。

两处 DELETE 当前**只通过临时表 JOIN `[F运单编号]` 删除，WHERE 完全没有 `[F批次ID]` / 状态限定**：

- `BillingBulkWriter.DeleteExistingResults`（约 326–337 行）
- `PricingPlugin.DeleteOldBillingResultsAsync`（约 1101–1112 行，步骤 6.5 重试前清理）

由此两个风险：

1. **同批两阶段互删**：同一运单号若在 STG 一票多行、部分成功部分失败，Phase B 删失败行时会把 Phase A 刚写入的成功行一起删掉，成功行丢失。
2. **跨批次误删**：删除不带批次限定，会删掉其它批次同运单号的历史计费结果。申通运单号跨批次可重复。

实测批次 82 无重复运单号、无跨批同号，当前未触发，故为预防性修复。

## 方案：非对称的「批次 + 状态」限定

给三处 DELETE 全部加 `[F批次ID] = @batchId`（解决风险 2）；并对 Phase B 额外加 `[F计算状态] = 2`（解决风险 1）。

### 1. `BillingBulkWriter.DeleteExistingResults` 签名扩展

```
DeleteExistingResults(IReadOnlyList<string> waybillNos, string resultTable,
                      long batchId,                       // 新增，必填
                      SqlConnection connection, SqlTransaction transaction,
                      int? calcStatus = null)             // 新增，可选状态限定
```

- `DELETE c`（成本明细）与 `DELETE r`（结果行）两条语句都追加 `WHERE r.[F批次ID] = @batchId`。
- `calcStatus` 非空时再追加 `AND r.[F计算状态] = @calcStatus`。
- 成本明细无状态列，靠 JOIN 到 `r` 后用 `r` 上的同一组谓词过滤，保证两条 DELETE 范围一致。

### 2. `PricingEngine` 两阶段调用（关键的非对称设计）

- **Phase A（成功）**：`DeleteExistingResults(successNos, resultTable, batchId, conn, txn)` —— `calcStatus = null`，**清掉本批次该运单的所有旧行**（含上一轮遗留的旧失败行）。Phase A 先执行，此刻本轮尚未写入任何行，删全状态安全且有益。
- **Phase B（失败）**：`DeleteExistingResults(failureNos, resultTable, batchId, errConn, errTxn, calcStatus: 2)` —— **只删 `F计算状态=2` 的失败行**，因此碰不到 Phase A 刚写入的成功行（`F计算状态=1`）。风险 1 由此堵死。

**为什么非对称（而非两阶段都按各自状态删）**：风险 1 的本质是「Phase B 删掉 Phase A 的成功行」，只要 Phase B 限定状态=2 即可堵死。Phase A 保持删全状态，反而能顺带清理「上一轮失败、本轮成功」的旧行，产生的孤儿更少（见下）。

### 3. `PricingPlugin.DeleteOldBillingResultsAsync`（步骤 6.5 重试前清理）

- 签名加 `long batchId`：`DeleteOldBillingResultsAsync(string resultTable, long batchId, IReadOnlyList<string> waybillNos)`。
- 两条 DELETE 加 `WHERE r.[F批次ID] = @batchId`（**全状态**，不加状态限定）。它是引擎前对重试运单的一次性预清，单次执行、无两阶段互删问题；只需解决跨批次误删。
- 调用方（约 236 行）改为传入 `batchId`。

## 残留边角（已知、极少见、暂不处理）

非对称方案堵死了「同批互删」与「跨批误删」，但留一个极少见的洞：

> 某运单上一轮**全部成功**（状态=1）、本轮重跑变成**全部失败** —— Phase A 不处理它（不在成功名单），Phase B 只删状态=2，于是上一轮的旧成功行成为孤儿残留。

彻底封堵需要「两阶段插入前按 batchId 对全部受影响运单做一次性预删除」的重构，但那会打破现有「失败行独立事务、可重试持久化」的设计，回归面较大。鉴于中优先级 + 预防性，**本设计仅文档注明此边角，暂不处理**，并在代码注释中标注。

## 角色限定说明（为何不加）

结果表一票多行也包含同一运单不同 `F参与方角色`/层级的多行；重跑时这些行都要重算重写，因此删除应覆盖该运单在本批次的**所有角色行**。故**不**加 `F参与方角色` 限定——加了反而漏删。

## 测试（TDD，纯函数 + C#）

抽纯静态 helper 作单一真相源，遵循项目「别用真 DbContext 驱动、抽纯函数」约定，先写失败测试再改代码。

### 抽取

> 实现修正：原设想抽 `BillingDeleteSqlBuilder.Build(...)` 统一构造 DELETE SQL，但现有 `.mjs` 契约测试切取的是两方法体内联 SQL 文本来断言，抽离会使既有断言失配、需大改契约测试。故**保持 SQL 内联**（只在两处 WHERE 各加谓词），**只抽纯判定函数 `BillingDeleteScope.WouldDeleteRow`**。SQL 形状由 `.mjs` 源级断言守护，删除语义由纯函数守护。

- 纯判定函数 `BillingDeleteScope.WouldDeleteRow(rowBatchId, rowWaybillNo, rowCalcStatus, deleteBatchId, deleteWaybillNos, deleteCalcStatus)`：把 SQL 的 WHERE 语义在 C# 里等价表达（`rowBatchId == deleteBatchId && deleteWaybillNos.Contains(rowWaybillNo) && (deleteCalcStatus == null || rowCalcStatus == deleteCalcStatus)`）。改动任一侧 SQL 谓词时须同步本函数。

### C# 测试 `tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs`

- **行为复现（先 RED）**：成功行 `(批次82, "STO123", 状态1)` 经 Phase A 写入后，Phase B 删 `failureNos={"STO123"}`（一票多行混合）。
  - 旧语义（无批次/状态）：`WouldDeleteRow` 返回 `true` —— 成功行被删 = 丢数据。
  - 修复语义（`deleteBatchId=82, deleteCalcStatus=2`）：返回 `false` —— 成功行存活。
- **跨批次**：成功行属批次 81，Phase B 在批次 82 删 → 修复后 `false`（不波及历史批次）。
- 共 5 个 `[Fact]`（正路径命中、Phase B 放过成功行、跨批不命中、Phase B 删失败行、不在名单不命中）。SQL 形状不在 C# 测试断言，改由下方 `.mjs` 契约测试守护。
- 注意 xUnit 下 `async Task` 命名空间被遮蔽时用 `global::System.Threading.Tasks.Task`（项目既有坑）。本批测试为纯同步断言，预计不涉及，但保留此约定。

### 扩展 `scripts/tests/express-billing-delete-cascade-contract.test.mjs`

- 新增正向断言：`DeleteExistingResults` 与 `DeleteOldBillingResultsAsync` 的 DELETE 现含 `[F批次ID] = @batchId`。
- 保留既有断言：`DeleteOldBillingResultsAsync` 仍**不得**含 `[F批次ID] IN`（历史另一 bug 的回归护栏；本次用标量 `= @batchId`，不冲突）。

## 改动清单

| 文件 | 改动 |
|------|------|
| `BillingDeleteScope.cs`（新增） | 纯判定函数 `WouldDeleteRow`（删除语义可执行规格） |
| `BillingBulkWriter.cs` | `DeleteExistingResults` 加 `batchId`/`calcStatus` 参数与内联 WHERE 谓词 |
| `PricingEngine.cs` | Phase A 传 `batchId`；Phase B 传 `batchId` + `calcStatus:2` |
| `PricingPlugin.cs` | `DeleteOldBillingResultsAsync` 加 `batchId` 参数与 WHERE；调用方传 `batchId` |
| `BillingDeleteScopeTests.cs`（新增） | 行为复现（5 用例） |
| `express-billing-delete-cascade-contract.test.mjs` | 加批次/状态限定正向断言 |

## 非目标（YAGNI）

- 单次预删除重构（封堵成功→失败翻转的孤儿边角）。
- `F参与方角色` 限定。
- 抽 `BillingDeleteSqlBuilder.Build` 统一 SQL（保留内联以护住既有契约测试；两处 DELETE 模板的轻度重复由纯函数 + 契约断言防漂移）。
- 针对真实 SQL Server 的集成测试（无 CI 数据库，超出本次范围）。
