#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = file => fs.readFileSync(path.join(root, file), 'utf8')
const sliceBetween = (source, startToken, endToken, label) => {
  const start = source.indexOf(startToken)
  assert.ok(start >= 0, `${label} should contain ${startToken}`)
  const end = source.indexOf(endToken, start)
  assert.ok(end > start, `${label} should contain ${endToken} after ${startToken}`)
  return source.slice(start, end)
}

const bulkWriter = read('src/STOTOP.Module.Express/Services/Billing/BillingBulkWriter.cs')
const pricingPlugin = read('src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs')

const deleteExistingResults = sliceBetween(
  bulkWriter,
  'public async Task DeleteExistingResults',
  '/// <summary>\r\n    /// 获取连接字符串',
  'BillingBulkWriter.DeleteExistingResults'
)
assert.ok(
  deleteExistingResults.includes('var costTable = $"{resultTable}_成本明细"'),
  'DeleteExistingResults should derive the matching cost breakdown table from the billing result table'
)
assert.ok(
  deleteExistingResults.includes('ValidateTableName(costTable)'),
  'DeleteExistingResults should validate the derived cost breakdown table name'
)
const bulkCostDelete = deleteExistingResults.indexOf('DELETE c')
const bulkResultDelete = deleteExistingResults.indexOf('DELETE r')
assert.ok(bulkCostDelete >= 0, 'DeleteExistingResults should delete cost breakdown rows before deleting billing results')
assert.ok(bulkResultDelete >= 0, 'DeleteExistingResults should delete billing result rows')
assert.ok(
  bulkCostDelete < bulkResultDelete,
  'DeleteExistingResults should delete cost breakdown rows before parent billing result rows'
)
assert.ok(
  deleteExistingResults.includes('c.[F计费结果ID] = r.[FID]'),
  'DeleteExistingResults should join cost breakdown rows to billing results by F计费结果ID'
)
assert.ok(
  deleteExistingResults.includes('#TmpDeleteWaybillNos'),
  'DeleteExistingResults should limit both deletes to the retried waybill numbers'
)
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

const retrySelection = pricingPlugin.slice(
  pricingPlugin.indexOf('// 6.5 重试时清除旧计费失败记录'),
  pricingPlugin.indexOf('// 7. 调用 PricingEngine')
)
assert.ok(
  retrySelection.includes('retryWaybillNos'),
  'PricingPlugin retry cleanup should collect waybill numbers'
)
assert.ok(
  !retrySelection.includes('retryWaybillIds'),
  'PricingPlugin retry cleanup must not pass staging row IDs as billing batch IDs'
)

const deleteOldBillingResults = sliceBetween(
  pricingPlugin,
  'private async Task DeleteOldBillingResultsAsync',
  '/// <summary>删除同批次同类型的旧错误明细',
  'PricingPlugin.DeleteOldBillingResultsAsync'
)
assert.ok(
  deleteOldBillingResults.includes('IReadOnlyList<string> waybillNos'),
  'PricingPlugin.DeleteOldBillingResultsAsync should accept waybill numbers, not row IDs'
)
assert.ok(
  deleteOldBillingResults.includes('var costTable = $"{resultTable}_成本明细"'),
  'PricingPlugin retry cleanup should derive the cost breakdown table'
)
const pluginCostDelete = deleteOldBillingResults.indexOf('DELETE c')
const pluginResultDelete = deleteOldBillingResults.indexOf('DELETE r')
assert.ok(pluginCostDelete >= 0, 'PricingPlugin retry cleanup should delete old cost breakdown rows')
assert.ok(pluginResultDelete >= 0, 'PricingPlugin retry cleanup should delete old billing result rows')
assert.ok(
  pluginCostDelete < pluginResultDelete,
  'PricingPlugin retry cleanup should delete cost breakdown rows before billing results'
)
assert.ok(
  deleteOldBillingResults.includes('c.[F计费结果ID] = r.[FID]'),
  'PricingPlugin retry cleanup should join cost breakdown rows to billing results by F计费结果ID'
)
assert.ok(
  !deleteOldBillingResults.includes('[F批次ID] IN'),
  'PricingPlugin retry cleanup must not treat row IDs as batch IDs'
)

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

console.log('express billing delete cascade contract passed')
