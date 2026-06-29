#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const exists = file => fs.existsSync(path.join(root, file))
const read = file => fs.readFileSync(path.join(root, file), 'utf8')

const requiredFiles = [
  'src/STOTOP.Module.CardFlow/Dtos/ImportValidationDtos.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/IImportCalculationValidationService.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/ImportCalculationValidationService.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/VoucherValidationAnalyzer.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/PricingValidationAnalyzer.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/CostValidationAnalyzer.cs',
  'src/STOTOP.Module.CardFlow/Services/Validation/ValidationAttributionClassifier.cs',
  'src/STOTOP.Module.CardFlow/Controllers/CfImportValidationController.cs',
  'tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj',
  'web/src/api/importValidation.ts',
  'web/src/views/cardflow/import-validation/ImportCalculationValidationWorkbench.vue',
]

for (const file of requiredFiles) {
  assert.equal(exists(file), true, `${file} should exist`)
}

const dto = read('src/STOTOP.Module.CardFlow/Dtos/ImportValidationDtos.cs')
for (const token of [
  'ImportValidationRunRequest',
  'ImportValidationReportDto',
  'ImportValidationFindingDto',
  'ImportValidationSampleRowDto',
  'ImportValidationSampleResultDto',
  'ImportValidationSampleCostItemDto',
  'ImportValidationEvidenceDto',
  'CalculationTraceStepDto',
  'SampleRows',
  'CostItems',
  'SourceFields',
  'OriginalValue',
  'SystemValue',
  'ExpectedValue',
  'AffectedRows',
  'ValidationAttribution',
  'ImportData',
  'Configuration',
  'CalculationLogic',
  'Persistence',
  'ValidationDomain',
  'Voucher',
  'Pricing',
  'Cost',
]) {
  assert.ok(dto.includes(token), `DTO contract should include ${token}`)
}

const controller = read('src/STOTOP.Module.CardFlow/Controllers/CfImportValidationController.cs')
for (const token of [
  '[Route("api/cardflow/import-validation")]',
  'GetSummary',
  'Run',
  'GetRowDetail',
  'IImportCalculationValidationService',
  'CardFlowPermissions.ImportValidation',
]) {
  assert.ok(controller.includes(token), `Controller should include ${token}`)
}

const cardFlowSeeder = read('src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs')
assert.ok(cardFlowSeeder.includes('cardflow:import:validation'), 'CardFlow seeder should register import validation permission')
assert.ok(cardFlowSeeder.includes('验证导入计算'), 'CardFlow seeder should name import validation permission')

const moduleExtensions = read('src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs')
for (const registration of [
  'IImportCalculationValidationService, ImportCalculationValidationService',
  'VoucherValidationAnalyzer',
  'PricingValidationAnalyzer',
  'CostValidationAnalyzer',
  'ValidationAttributionClassifier',
]) {
  assert.ok(moduleExtensions.includes(registration), `CardFlow module should register ${registration}`)
}

const validationService = read('src/STOTOP.Module.CardFlow/Services/Validation/ImportCalculationValidationService.cs')
for (const token of [
  'sourceWaybillNos',
  'BuildSampleWaybillNosAsync',
  'QueryCalculatedSampleWaybillNosAsync',
  'BuildRepresentativeFindings',
  'BuildFindingSignature',
  'AffectedRows',
  'QueryPricingSampleResultsAsync(context, sourceWaybillNos',
  'QueryCostSampleResultsAsync(context, sourceWaybillNos',
  '[F运单编号] IN @WaybillNos',
  '[F计算状态] = 1',
  'ChunkWaybillNos',
]) {
  assert.ok(validationService.includes(token), `Validation sampling should prioritize calculated results and deduplicate issue examples: ${token}`)
}

const pricingAnalyzer = read('src/STOTOP.Module.CardFlow/Services/Validation/PricingValidationAnalyzer.cs')
for (const token of [
  'QueryRepresentativeErrorRowsAsync',
  'QueryRepresentativeErrorRowsWithSourceAsync',
  'BuildShopScopedErrorPartitionSql',
  'F店铺账号',
  'ShopName',
  'ERR_NO_PRICE_PLAN',
  'ERR_NO_PRICE_CELL',
  'ROW_NUMBER() OVER',
  'PARTITION BY',
  'NormalizeErrorMessage',
]) {
  assert.ok(pricingAnalyzer.includes(token), `Pricing analyzer should return one representative row per shop for shop-scoped pricing errors: ${token}`)
}

const costAnalyzer = read('src/STOTOP.Module.CardFlow/Services/Validation/CostValidationAnalyzer.cs')
for (const token of [
  'BuildRepresentativeRows',
  'BuildCostIssueSignature',
  '成本明细缺失',
  '成本合计与明细不一致',
]) {
  assert.ok(costAnalyzer.includes(token), `Cost analyzer should return one representative row per cost issue type: ${token}`)
}

const priceFormula = read('src/STOTOP.Module.Express/Services/Billing/PriceFormula.cs')
assert.ok(priceFormula.includes('PriceFormulaExplainResult'), 'PriceFormula should expose explain result')
assert.ok(priceFormula.includes('Explain'), 'PriceFormula should expose Explain method')

const costPlanCache = read('src/STOTOP.Module.Express/Services/Billing/CostPlanCache.cs')
assert.ok(costPlanCache.includes('CostExplainResult'), 'CostPlanCache should expose cost explain result')
assert.ok(costPlanCache.includes('ExplainAllCosts'), 'CostPlanCache should expose ExplainAllCosts')

const pricingEngine = read('src/STOTOP.Module.Express/Services/Billing/PricingEngine.cs')
assert.ok(pricingEngine.includes('PricingEngineExplainResult'), 'PricingEngine should expose explain result')
assert.ok(pricingEngine.includes('ExplainAsync'), 'PricingEngine should expose single waybill ExplainAsync')

const api = read('web/src/api/importValidation.ts')
for (const token of ['getImportValidationSummary', 'runImportValidation', 'getImportValidationRowDetail']) {
  assert.ok(api.includes(`function ${token}`), `Frontend API should include ${token}`)
}
for (const token of [
  'ImportValidationSampleRowDto',
  'ImportValidationSampleResultDto',
  'sampleRows',
  'costItems',
  'sourceFields',
  'originalValue',
  'systemValue',
  'expectedValue',
  'affectedRows',
]) {
  assert.ok(api.includes(token), `Frontend API contract should include ${token}`)
}

const routes = read('web/src/router/routes.ts')
assert.ok(routes.includes('cardflow/import-validation/:batchId'), 'Router should include import validation route')

const page = read('web/src/views/cardflow/import-validation/ImportCalculationValidationWorkbench.vue')
for (const token of ['自动凭证', '价格计算', '成本计算', '导入数据问题', '配置问题', '计算逻辑问题', '写入链路问题']) {
  assert.ok(page.includes(token), `Workbench page should show ${token}`)
}
for (const token of [
  '抽样核对',
  '原始值',
  '计算结果值',
  '成本项',
  '成本项合计',
  'formatEvidenceLabel',
  '计费结果ID',
  '计算状态',
  '应收金额',
  '计费重量',
  '报价编号',
  '成本合计',
  '成本明细合计',
  '成本明细数',
  '成本项明细',
  'sampleRows',
  'sampleColumns',
  'openSampleRow',
  'affectedRows',
]) {
  assert.ok(page.includes(token), `Workbench sample review should include ${token}`)
}

console.log('Import calculation validation contract is covered.')
