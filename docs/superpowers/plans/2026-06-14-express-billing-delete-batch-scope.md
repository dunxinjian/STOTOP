# Express 计费结果删除补加批次/状态限定 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给计费结果三处 DELETE 加 `[F批次ID]` 限定（Phase B 再加失败状态限定），消除「同批两阶段互删」「跨批次误删」导致成功行丢失的风险。

**Architecture:** SQL 保持内联在原两个方法内（只在 WHERE 追加谓词），以保留现有契约测试 `express-billing-delete-cascade-contract.test.mjs` 的全部断言、只做加法。另抽出一个纯判定函数 `BillingDeleteScope.WouldDeleteRow` 作为删除语义的可执行规格，用 C# 单测复现「丢成功行」。`DeleteExistingResults` 加 `batchId`（必填）与 `calcStatus`（可选）参数：Phase A 传 `batchId`、全状态；Phase B 传 `batchId` + `calcStatus:2`，只删失败行从而碰不到 Phase A 刚写入的成功行。`DeleteOldBillingResultsAsync` 加 `batchId`、全状态（引擎前一次性预清，无两阶段问题）。

**Tech Stack:** C# / .NET (net10.0)、xUnit、Microsoft.Data.SqlClient；契约测试为 Node.js `node:test`/`assert`（`.mjs`，切取 C# 源文本断言）。

**对 spec 的修正说明：** spec 原写「抽 `BillingDeleteSqlBuilder.Build`」。实现时发现现有 `.mjs` 契约测试切取的是两方法体内联 SQL，抽离会使既有断言失配、需大改契约测试。故本计划保持 SQL 内联、只抽 `BillingDeleteScope.WouldDeleteRow`。修复目标与对外行为不变。

---

## 文件结构

| 文件 | 责任 | 操作 |
|------|------|------|
| `src/STOTOP.Module.Express/Services/Billing/BillingDeleteScope.cs` | 删除判定的纯函数（语义规格，供测试） | 新建 |
| `tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs` | 行为复现：成功行在 Phase B / 跨批次下必须存活 | 新建 |
| `src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs` | `DeleteExistingResults` 加 `batchId`/`calcStatus` 参数与 WHERE 谓词 | 改 `DeleteExistingResults`（296–343 行） |
| `src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs` | Phase A 传 `batchId`；Phase B 传 `batchId`+`calcStatus:2` | 改两处调用（228–229、267–268 行） |
| `src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs` | `DeleteOldBillingResultsAsync` 加 `batchId` 参数与 WHERE；调用方传 `batchId` | 改方法（1064–1126 行）与调用（236 行） |
| `scripts/tests/express-billing-delete-cascade-contract.test.mjs` | 源级契约：DELETE 现含 `[F批次ID]=@batchId`、Phase B 含状态限定 | 扩充断言 |

---

## Task 1：纯判定函数 `BillingDeleteScope.WouldDeleteRow` + 失败单测

**Files:**
- Create: `tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs`
- Create: `src/STOTOP.Module.Express/Services/Billing/BillingDeleteScope.cs`

- [ ] **Step 1：先写失败测试**

新建 `tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs`：

```csharp
using System.Collections.Generic;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 计费结果删除范围回归测试。
/// 锁定：重跑去重的 DELETE 必须限定本批次本运单（Phase B 还需限定失败状态），
/// 否则会发生「同批两阶段互删」「跨批次误删」导致成功行丢失。
/// </summary>
public class BillingDeleteScopeTests
{
    private static readonly IReadOnlySet<string> WaybillSto123 =
        new HashSet<string> { "STO123" };

    // 同批互删：Phase A 写入的成功行(批次82, 状态1)，在 Phase B 删失败行时必须存活。
    [Fact]
    public void PhaseB_delete_must_not_remove_phaseA_success_row()
    {
        // 旧语义：Phase B 不限定状态 → 成功行被命中删除（数据丢失）
        Assert.True(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: null));

        // 新语义：Phase B 限定状态=2 → 成功行(状态1)不被命中，存活
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }

    // 跨批次误删：批次81的历史成功行，不应被批次82的删除命中（修复后行为）。
    [Fact]
    public void Delete_must_not_cross_batch()
    {
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 81, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: null));
    }

    // 本批次本运单的失败行：Phase B 正常清理（重跑去重的预期行为）。
    [Fact]
    public void PhaseB_delete_removes_same_batch_failure_row()
    {
        Assert.True(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 2,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }

    // 不在待删名单的运单：永不命中。
    [Fact]
    public void Delete_skips_waybills_not_in_set()
    {
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO999", rowCalcStatus: 2,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }
}
```

- [ ] **Step 2：运行测试，确认失败（RED）**

Run：`dotnet test tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj --filter "FullyQualifiedName~BillingDeleteScopeTests" -m:1 /p:UseSharedCompilation=false`

Expected：编译失败，报 `BillingDeleteScope` 不存在（`CS0103`/`CS0246`）。

- [ ] **Step 3：写最小实现**

新建 `src/STOTOP.Module.Express/Services/Billing/BillingDeleteScope.cs`：

```csharp
namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 计费结果删除范围的纯判定函数——把删除 SQL 的 WHERE 语义在 C# 里等价表达，
/// 作为删除范围的可执行规格供单测使用（DELETE 依赖临时表，无法脱离真实库直测）。
/// 与 BillingBulkWriter.DeleteExistingResults / PricingPlugin.DeleteOldBillingResultsAsync
/// 的 DELETE r 谓词保持一致；改动 SQL 谓词时须同步本函数。
/// </summary>
public static class BillingDeleteScope
{
    /// <summary>
    /// 判定某计费结果行是否会被「按运单号 + 批次（+ 可选状态）」的删除命中。
    /// </summary>
    /// <param name="rowBatchId">结果行所属批次</param>
    /// <param name="rowWaybillNo">结果行运单编号</param>
    /// <param name="rowCalcStatus">结果行计算状态（1=成功，2=失败）</param>
    /// <param name="deleteBatchId">本次删除限定的批次</param>
    /// <param name="deleteWaybillNos">本次删除限定的运单编号集合</param>
    /// <param name="deleteCalcStatus">本次删除限定的计算状态；null 表示不限状态</param>
    public static bool WouldDeleteRow(
        long rowBatchId,
        string rowWaybillNo,
        int rowCalcStatus,
        long deleteBatchId,
        IReadOnlySet<string> deleteWaybillNos,
        int? deleteCalcStatus)
    {
        if (rowBatchId != deleteBatchId) return false;
        if (!deleteWaybillNos.Contains(rowWaybillNo)) return false;
        if (deleteCalcStatus.HasValue && rowCalcStatus != deleteCalcStatus.Value) return false;
        return true;
    }
}
```

- [ ] **Step 4：运行测试，确认通过（GREEN）**

Run：`dotnet test tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj --filter "FullyQualifiedName~BillingDeleteScopeTests" -m:1 /p:UseSharedCompilation=false`

Expected：4 个测试全部 PASS。

- [ ] **Step 5：提交**

```bash
git add src/STOTOP.Module.Express/Services/Billing/BillingDeleteScope.cs tests/STOTOP.Module.Express.Tests/Billing/BillingDeleteScopeTests.cs
git commit -m "test: 计费结果删除范围纯函数 BillingDeleteScope 及行为复现测试"
```

---

## Task 2：`DeleteExistingResults` 加批次/状态限定 + PricingEngine 两阶段接入

**Files:**
- Modify: `scripts/tests/express-billing-delete-cascade-contract.test.mjs`
- Modify: `src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs:296-343`
- Modify: `src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs:227-229,266-268`

- [ ] **Step 1：先扩充契约测试（源级 RED）**

在 `scripts/tests/express-billing-delete-cascade-contract.test.mjs` 的第 48 行（`'DeleteExistingResults should limit both deletes to the retried waybill numbers'` 那条 `assert.ok(...)` 之后、空行 `49` 之前）插入：

```js
assert.ok(
  deleteExistingResults.includes('[F批次ID] = @batchId'),
  'DeleteExistingResults must scope deletes to the current batch'
)
assert.ok(
  deleteExistingResults.includes('AND r.[F计算状态] = @calcStatus'),
  'DeleteExistingResults must support restricting deletes to a calc status (Phase B failure rows)'
)

const pricingEngine = read('src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs')
assert.ok(
  /DeleteExistingResults\(\s*\r?\n?\s*successWaybillNos[\s\S]*?batchId/.test(pricingEngine),
  'PricingEngine Phase A must pass the batch id to scope deletion'
)
assert.ok(
  /DeleteExistingResults\(\s*\r?\n?\s*failureWaybillNos[\s\S]*?calcStatus:\s*2/.test(pricingEngine),
  'PricingEngine Phase B must restrict deletion to failure rows (calcStatus: 2) so it cannot remove Phase A success rows'
)
```

- [ ] **Step 2：运行契约测试，确认失败（RED）**

Run：`node scripts/tests/express-billing-delete-cascade-contract.test.mjs`

Expected：以 `AssertionError` 退出，提示 `DeleteExistingResults must scope deletes to the current batch`（当前 SQL 无 `[F批次ID] = @batchId`）。

- [ ] **Step 3：改 `DeleteExistingResults`（加参数与 WHERE 谓词）**

把 `src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs` 的整个 `DeleteExistingResults` 方法（含其 `<summary>` 注释，约 292–343 行）替换为：

```csharp
    /// <summary>
    /// 删除指定运单编号的已有计费结果（用于重跑前清理，防止重复累积）
    /// 采用临时表+JOIN高性能模式。
    /// WHERE 限定 [F批次ID]=@batchId 防止跨批次误删（运单号跨批次可重复）；
    /// calcStatus 非空时再限定 [F计算状态]，供 Phase B 只删失败行、
    /// 不误删 Phase A 刚写入的同批次成功行。
    /// </summary>
    public async Task DeleteExistingResults(
        IReadOnlyList<string> waybillNos,
        string resultTable,
        long batchId,
        SqlConnection connection,
        SqlTransaction transaction,
        int? calcStatus = null)
    {
        if (waybillNos.Count == 0) return;
        ValidateTableName(resultTable);
        var costTable = $"{resultTable}_成本明细";
        ValidateTableName(costTable);

        // 1. 创建临时表
        var createTempSql = "CREATE TABLE #TmpDeleteWaybillNos ([FWaybillNo] NVARCHAR(50) NOT NULL);";
        using (var cmd = new SqlCommand(createTempSql, connection, transaction))
            await cmd.ExecuteNonQueryAsync();

        // 2. SqlBulkCopy 批量写入运单编号到临时表
        var dt = new DataTable();
        dt.Columns.Add("FWaybillNo", typeof(string));
        foreach (var no in waybillNos)
            dt.Rows.Add(no);

        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
        {
            bulkCopy.DestinationTableName = "#TmpDeleteWaybillNos";
            bulkCopy.BatchSize = 5000;
            await bulkCopy.WriteToServerAsync(dt);
        }

        // 3. 先删除成本明细，再删除旧结果，避免留下孤儿成本明细。
        //    WHERE 限定本批次（@batchId）；calcStatus 非空时追加状态限定。
        var statusClause = calcStatus.HasValue ? " AND r.[F计算状态] = @calcStatus" : "";
        var deleteSql = $@"
            IF OBJECT_ID(N'{costTable}', N'U') IS NOT NULL
            BEGIN
                DELETE c FROM [{costTable}] c
                INNER JOIN [{resultTable}] r ON c.[F计费结果ID] = r.[FID]
                INNER JOIN #TmpDeleteWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                WHERE r.[F批次ID] = @batchId{statusClause};
            END;

            DELETE r FROM [{resultTable}] r
            INNER JOIN #TmpDeleteWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
            WHERE r.[F批次ID] = @batchId{statusClause};

            DROP TABLE #TmpDeleteWaybillNos;";
        using (var cmd = new SqlCommand(deleteSql, connection, transaction))
        {
            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@batchId", batchId);
            if (calcStatus.HasValue)
                cmd.Parameters.AddWithValue("@calcStatus", calcStatus.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
```

- [ ] **Step 4：改 PricingEngine Phase A 调用**

`src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs` 第 228–229 行：

原：
```csharp
                await _bulkWriter.DeleteExistingResults(
                    successWaybillNos, resultTable, connection, dbTransaction);
```
改为（插入 `batchId,`）：
```csharp
                await _bulkWriter.DeleteExistingResults(
                    successWaybillNos, resultTable, batchId, connection, dbTransaction);
```

- [ ] **Step 5：改 PricingEngine Phase B 调用**

同文件第 267–268 行：

原：
```csharp
                        await _bulkWriter.DeleteExistingResults(
                            failureWaybillNos, resultTable, errConnection, errTransaction);
```
改为（插入 `batchId,` 并追加 `, calcStatus: 2`）：
```csharp
                        await _bulkWriter.DeleteExistingResults(
                            failureWaybillNos, resultTable, batchId, errConnection, errTransaction, calcStatus: 2);
```

- [ ] **Step 6：运行契约测试 + 全量 C# 测试，确认通过（GREEN）**

Run：`node scripts/tests/express-billing-delete-cascade-contract.test.mjs`
Expected：输出 `express billing delete cascade contract passed`，退出码 0。

Run：`dotnet test tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj -m:1 /p:UseSharedCompilation=false`
Expected：编译通过（含 PricingEngine 改动），全部测试 PASS。

- [ ] **Step 7：提交**

```bash
git add src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs scripts/tests/express-billing-delete-cascade-contract.test.mjs
git commit -m "fix: 计费 DeleteExistingResults 限定本批次，Phase B 只删失败行防误删成功行"
```

---

## Task 3：`DeleteOldBillingResultsAsync` 加批次限定 + 调用方

**Files:**
- Modify: `scripts/tests/express-billing-delete-cascade-contract.test.mjs`
- Modify: `src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs:236,1064-1126`

- [ ] **Step 1：先扩充契约测试（源级 RED）**

在 `scripts/tests/express-billing-delete-cascade-contract.test.mjs` 末尾、`console.log('express billing delete cascade contract passed')` 这一行**之前**插入：

```js
assert.ok(
  deleteOldBillingResults.includes('long batchId'),
  'PricingPlugin.DeleteOldBillingResultsAsync must accept the batch id'
)
assert.ok(
  deleteOldBillingResults.includes('[F批次ID] = @batchId'),
  'PricingPlugin retry cleanup must scope deletes to the current batch'
)
assert.ok(
  /DeleteOldBillingResultsAsync\([^)]*batchId/.test(retrySelection),
  'PricingPlugin retry cleanup must pass batchId to DeleteOldBillingResultsAsync'
)
```

- [ ] **Step 2：运行契约测试，确认失败（RED）**

Run：`node scripts/tests/express-billing-delete-cascade-contract.test.mjs`
Expected：`AssertionError`，提示 `PricingPlugin.DeleteOldBillingResultsAsync must accept the batch id`。

- [ ] **Step 3：改 `DeleteOldBillingResultsAsync` 签名与 SQL**

`src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs`。

3a. 方法签名（第 1064 行）：

原：
```csharp
    private async Task DeleteOldBillingResultsAsync(string resultTable, IReadOnlyList<string> waybillNos)
```
改为：
```csharp
    private async Task DeleteOldBillingResultsAsync(string resultTable, long batchId, IReadOnlyList<string> waybillNos)
```

3b. DELETE SQL 与执行块（第 1101–1117 行）。

原：
```csharp
            var deleteSql = $@"
                IF OBJECT_ID(N'{costTable}', N'U') IS NOT NULL
                BEGIN
                    DELETE c FROM [{costTable}] c
                    INNER JOIN [{resultTable}] r ON c.[F计费结果ID] = r.[FID]
                    INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo];
                END;

                DELETE r FROM [{resultTable}] r
                INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo];

                DROP TABLE #TmpRetryWaybillNos;";
            using (var deleteCmd = new SqlCommand(deleteSql, connection, transaction))
            {
                deleteCmd.CommandTimeout = 120;
                await deleteCmd.ExecuteNonQueryAsync();
            }
```
改为（两条 DELETE 各加 `WHERE r.[F批次ID] = @batchId`，并加参数）：
```csharp
            // 限定本批次：避免删掉其它批次同运单号的历史计费结果（运单号跨批次可重复）。
            var deleteSql = $@"
                IF OBJECT_ID(N'{costTable}', N'U') IS NOT NULL
                BEGIN
                    DELETE c FROM [{costTable}] c
                    INNER JOIN [{resultTable}] r ON c.[F计费结果ID] = r.[FID]
                    INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                    WHERE r.[F批次ID] = @batchId;
                END;

                DELETE r FROM [{resultTable}] r
                INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                WHERE r.[F批次ID] = @batchId;

                DROP TABLE #TmpRetryWaybillNos;";
            using (var deleteCmd = new SqlCommand(deleteSql, connection, transaction))
            {
                deleteCmd.CommandTimeout = 120;
                deleteCmd.Parameters.AddWithValue("@batchId", batchId);
                await deleteCmd.ExecuteNonQueryAsync();
            }
```

- [ ] **Step 4：改调用方传 `batchId`**

同文件第 236 行：

原：
```csharp
                await DeleteOldBillingResultsAsync(resultTable!, retryWaybillNos);
```
改为：
```csharp
                await DeleteOldBillingResultsAsync(resultTable!, batchId, retryWaybillNos);
```

- [ ] **Step 5：运行契约测试 + 全量 C# 测试，确认通过（GREEN）**

Run：`node scripts/tests/express-billing-delete-cascade-contract.test.mjs`
Expected：`express billing delete cascade contract passed`，退出码 0。

Run：`dotnet test tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj -m:1 /p:UseSharedCompilation=false`
Expected：编译通过、全部 PASS。

- [ ] **Step 6：提交**

```bash
git add src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs scripts/tests/express-billing-delete-cascade-contract.test.mjs
git commit -m "fix: 计费重试清理 DeleteOldBillingResultsAsync 限定本批次防跨批误删"
```

---

## Task 4：全量回归 + 残留边角注释收尾

**Files:**
- Modify: `src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs`（仅加注释）

- [ ] **Step 1：在 Phase B 调用处加残留边角注释**

`src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs` 第 265 行原注释：
```csharp
                        // 重跑去重：先删除这批运单的旧失败记录
```
改为：
```csharp
                        // 重跑去重：先删除这批运单的旧失败记录（仅限本批次失败状态行）。
                        // 已知边角：某运单上一轮全部成功、本轮全部失败时，Phase A 不处理它、
                        // Phase B 只删状态=2，旧成功行会残留。极少见，详见
                        // docs/superpowers/specs/2026-06-14-express-billing-delete-batch-scope-design.md。
```

- [ ] **Step 2：全量契约测试**

Run：`pwsh scripts/dev/test-contracts.ps1`
Expected：`all selected contract tests passed`，退出码 0。

- [ ] **Step 3：全量 Express C# 测试**

Run：`pwsh scripts/dev/test-dotnet.ps1 Express`
Expected：`all selected dotnet test projects passed`，退出码 0。

- [ ] **Step 4：确认无遗漏调用点**

Run：`git grep -n "DeleteExistingResults\|DeleteOldBillingResultsAsync"`
Expected：仅 `BillingBulkWriter.cs`（定义）、`PricingEngine.cs`（两处调用，均含 `batchId`）、`PricingPlugin.cs`（定义 + 第 236 行调用，含 `batchId`）、`scripts/tests/express-billing-delete-cascade-contract.test.mjs`。无任何不带 `batchId` 的调用残留。

- [ ] **Step 5：提交**

```bash
git add src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs
git commit -m "docs: 标注计费删除非对称方案的残留边角（成功→失败翻转孤儿）"
```

---

## 自检（写计划后对照 spec）

- **批次限定（风险2）**：Task 2/3 给三处 DELETE 全加 `[F批次ID]=@batchId` ✓
- **状态限定（风险1）**：Task 2 Step 5 Phase B 传 `calcStatus:2`，`WouldDeleteRow` 测试（Task 1）锁定成功行存活 ✓
- **Phase A 全状态 / Phase B 状态2 / 6.5 全状态**：Task 2 Step 4/5、Task 3 Step 3 一致 ✓
- **角色不限定**：未加 `F参与方角色` 谓词，删除覆盖该运单本批次全部角色行 ✓
- **残留边角文档化**：spec 已记 + Task 4 Step 1 代码注释 ✓
- **TDD/纯函数/契约测试**：Task 1 纯函数行为复现、Task 2/3 契约源级 RED→GREEN ✓
- **既有契约断言保留**：SQL 内联未抽离，原 `.mjs` 断言（DELETE c/r 顺序、JOIN、`不得含 [F批次ID] IN`）不受影响 ✓
- **不擅自 push**：计划仅 `git commit`，无 `git push` ✓

## 非目标（YAGNI）

- 单次预删除重构（封堵成功→失败翻转孤儿）。
- `BillingDeleteSqlBuilder.Build` SQL 抽离（保留内联以护住既有契约测试）。
- 真实 SQL Server 集成测试。
