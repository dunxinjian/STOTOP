import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (path) => fs.readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8')
const exists = (path) => fs.existsSync(new URL(`../../${path}`, import.meta.url))

const requiredFiles = [
  'src/STOTOP.Module.Finance/Entities/FinBudgetVersion.cs',
  'src/STOTOP.Module.Finance/Entities/FinBudgetLine.cs',
  'src/STOTOP.Module.Finance/Entities/FinBudgetExpenseMapping.cs',
  'src/STOTOP.Module.Finance/Entities/FinTreasuryAccountBinding.cs',
  'src/STOTOP.Module.Finance/Entities/FinTreasuryPlanLine.cs',
  'src/STOTOP.Module.Finance/Entities/FinBudgetOccupation.cs',
  'src/STOTOP.Module.Finance/Services/BudgetService.cs',
  'src/STOTOP.Module.Finance/Services/BudgetExpenseMappingService.cs',
  'src/STOTOP.Module.Finance/Services/TreasuryPlanService.cs',
  'src/STOTOP.Module.Finance/Services/BudgetOccupationService.cs',
  'src/STOTOP.Module.Finance/Controllers/BudgetController.cs',
  'src/STOTOP.Module.Finance/Controllers/TreasuryPlanController.cs',
  'src/STOTOP.Module.Finance/Controllers/BudgetControlController.cs',
  'web/src/views/finance/BudgetVersionManage.vue',
  'web/src/views/finance/BudgetLineEditor.vue',
  'web/src/views/finance/BudgetExpenseMapping.vue',
  'web/src/views/finance/TreasuryAccountBinding.vue',
  'web/src/views/finance/TreasuryRollingForecast.vue',
]

for (const path of requiredFiles) {
  assert.ok(exists(path), `${path} should exist`)
}

const financeModule = read('src/STOTOP.Module.Finance/FinanceModuleExtensions.cs')
for (const registration of [
  'IBudgetService, BudgetService',
  'IBudgetExpenseMappingService, BudgetExpenseMappingService',
  'ITreasuryPlanService, TreasuryPlanService',
  'IBudgetOccupationService, BudgetOccupationService',
]) {
  assert.ok(financeModule.includes(registration), `Finance module should register ${registration}`)
}

for (const configuration of [
  'FinBudgetVersionConfiguration',
  'FinBudgetLineConfiguration',
  'FinBudgetExpenseMappingConfiguration',
  'FinTreasuryAccountBindingConfiguration',
  'FinTreasuryPlanLineConfiguration',
  'FinBudgetOccupationConfiguration',
]) {
  assert.ok(financeModule.includes(configuration), `Finance module should apply ${configuration}`)
}

const financeApi = read('web/src/api/finance.ts')
for (const apiName of [
  'getBudgetVersions',
  'createBudgetVersion',
  'getBudgetLines',
  'batchUpsertBudgetLines',
  'getBudgetExpenseMappings',
  'saveBudgetExpenseMapping',
  'getTreasuryAccountBindings',
  'saveTreasuryAccountBinding',
  'getRolling13WeekTreasuryPlan',
  'previewBudgetControl',
]) {
  assert.ok(financeApi.includes(`function ${apiName}`), `finance API should expose ${apiName}`)
}

const routes = read('web/src/router/routes.ts')
for (const route of [
  'finance/budget/versions',
  'finance/budget/editor/:versionId',
  'finance/budget/expense-mapping',
  'finance/treasury/account-bindings',
  'finance/treasury/rolling-13-weeks',
]) {
  assert.ok(routes.includes(`path: '${route}'`), `router should include ${route}`)
}

console.log('Finance planning contract is aligned.')
