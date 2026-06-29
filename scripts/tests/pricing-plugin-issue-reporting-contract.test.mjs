#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = file => fs.readFileSync(path.join(root, file), 'utf8')

const pricingPlugin = read('src/STOTOP.Module.Express/Services/Agents/PricingPlugin.cs')
const sqlBulkInsert = read('src/STOTOP.Module.CardFlow/Services/Import/SqlBulkInsertService.cs')
const billingDtos = read('src/STOTOP.Module.Express/Dtos/ExpBillingDtos.cs')
const pricingEngine = read('src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs')

for (const token of [
  'IBulkInsertService',
  'BuildRepresentativePricingIssues',
  'MaxPricingIssueRepresentativesPerGroup',
  'BulkInsertErrorsAsync(issueRecords',
  'QueueQualityIssueDispatch',
  'CreateAsyncScope',
]) {
  assert.ok(pricingPlugin.includes(token), `PricingPlugin should include non-blocking aggregated issue reporting token: ${token}`)
}

const reportMethod = pricingPlugin.match(/private async Task ReportPricingEngineErrorsAsync[\s\S]*?\n    private /)?.[0] || ''
assert.ok(reportMethod.includes('BuildRepresentativePricingIssues'), 'ReportPricingEngineErrorsAsync should build representative issue records')
assert.ok(reportMethod.includes('BulkInsertErrorsAsync(issueRecords'), 'ReportPricingEngineErrorsAsync should bulk insert representative issue records')
assert.ok(!reportMethod.includes('foreach (var error in group)'), 'ReportPricingEngineErrorsAsync should not save every billing error one by one')
assert.ok(!reportMethod.includes('ReportIssueAsync(batchId'), 'ReportPricingEngineErrorsAsync should not call per-row ReportIssueAsync')

assert.ok(billingDtos.includes('public string ShopName { get; set; }'), 'BillingError should carry ShopName for shop-level aggregation')
assert.ok(pricingEngine.includes('ShopName = waybill.ShopName'), 'PricingEngine should copy waybill ShopName into BillingError')

const representativeMethod = pricingPlugin.match(/private static List<CfBatchError> BuildRepresentativePricingIssues[\s\S]*?\n    private static CfBatchError/)?.[0] || ''
assert.ok(representativeMethod.includes('BuildPricingIssueGroupKey'), 'Representative pricing issues should use an explicit grouping key helper')
assert.ok(representativeMethod.includes('IsShopPricePlanMissError'), 'ERR_NO_PRICE_PLAN should have a special shop-level aggregation path')

const issueRecordMethod = pricingPlugin.match(/private static CfBatchError CreatePricingIssueRecord[\s\S]*?\n    private static string/)?.[0] || ''
assert.ok(issueRecordMethod.includes('shopName'), 'Pricing issue payload should include shopName when available')
assert.ok(issueRecordMethod.includes('affectedRows'), 'Pricing issue payload should include affectedRows')
assert.ok(issueRecordMethod.includes('店铺'), 'Shop price-plan miss issue message should show the shop name')

for (const awaitedDispatch of [
  'await DispatchQualityIssuesAsync(batchId, orgId)',
  'await _processingIssueService.DispatchBatchAsync(batchId, orgId)',
]) {
  assert.equal(pricingPlugin.includes(awaitedDispatch), false, `PricingPlugin should not synchronously block on quality issue dispatch: ${awaitedDispatch}`)
}

for (const token of [
  'F派发状态',
  'F派发方式',
  'F工作项ID',
  'F问题类型',
  'F处理结果',
  'F处理状态',
  'F处理载荷JSON',
  'F重跑状态',
]) {
  assert.ok(sqlBulkInsert.includes(token), `SqlBulkInsertService should preserve issue lifecycle column ${token}`)
}

console.log('Pricing plugin issue reporting contract ok.')
