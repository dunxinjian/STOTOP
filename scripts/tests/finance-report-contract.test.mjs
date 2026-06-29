import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (path) => fs.readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8')

const api = read('web/src/api/finance.ts')
const reportService = read('src/STOTOP.Module.Finance/Services/ReportService.cs')
const reportDto = read('src/STOTOP.Module.Finance/Dtos/ReportDto.cs')
const reportController = read('src/STOTOP.Module.Finance/Controllers/ReportController.cs')
const amoebaController = read('src/STOTOP.Module.Finance/Controllers/AmoebaController.cs')
const voucherAutoService = read('src/STOTOP.Module.Finance/Services/VoucherAutoService.cs')
const financeHome = read('web/src/views/finance/FinanceHome.vue')
const cashFlow = read('web/src/views/finance/CashFlowReport.vue')
const taxPayable = read('web/src/views/finance/TaxPayableReport.vue')
const profitStatement = read('web/src/views/finance/ProfitStatement.vue')
const assetBalance = read('web/src/views/finance/AssetBalanceReport.vue')
const auxiliaryBalance = read('web/src/views/finance/AuxiliaryBalanceReport.vue')

assert.match(
  api,
  /getAccountBalance\(params:\s*\{[^}]*accountSetId\?:\s*number/s,
  'account-balance API should accept accountSetId'
)
assert.match(
  api,
  /getAuxiliaryBalance\(params:\s*\{[^}]*accountSetId\?:\s*number/s,
  'auxiliary-balance API should accept accountSetId'
)
assert.match(
  api,
  /getAssetBalance\(params:\s*\{[^}]*accountSetId\?:\s*number/s,
  'asset-balance API should accept accountSetId'
)
assert.match(
  api,
  /getTaxPayable\(params:\s*\{[^}]*accountSetId\?:\s*number/s,
  'tax-payable API should accept accountSetId'
)
assert.match(
  api,
  /getCashFlowReport\(params:\s*\{[^}]*accountSetId\?:\s*number/s,
  'cash-flow API should accept accountSetId'
)

assert.match(
  reportService,
  /GetAuxiliaryBalanceAsync[\s\S]*Where\(b\s*=>\s*b\.FPeriodId\s*==\s*periodId\s*&&\s*b\.FAccountSetId\s*==\s*accountSetId\)/,
  'auxiliary balance query should filter by account set'
)
assert.match(
  reportService,
  /GetAssetBalanceAsync[\s\S]*Where\(c\s*=>\s*c\.FStatus\s*==\s*1\s*&&\s*c\.FAccountSetId\s*==\s*accountSetId\)/,
  'asset balance query should filter cards by account set'
)
assert.match(
  reportService,
  /GetCashFlowAsync\(long startPeriodId,\s*long endPeriodId,\s*long accountSetId\s*=\s*0\)/,
  'cash-flow service should accept accountSetId'
)
assert.match(
  reportController,
  /GetCashFlowReport[\s\S]*\[FromQuery\]\s*long accountSetId\s*=\s*0[\s\S]*GetCashFlowAsync\(startPeriodId,\s*endPeriodId,\s*accountSetId\)/,
  'cash-flow controller should pass accountSetId to service'
)

for (const field of ['OpeningBalance', 'PeriodIncrease', 'PeriodDecrease', 'ClosingBalance']) {
  assert.ok(reportDto.includes(field), `TaxPayableDto should expose ${field} for the frontend table`)
}
for (const field of ['AssetCode', 'AssetName', 'OriginalValue', 'AccumulatedDepreciation', 'NetValue']) {
  assert.ok(reportDto.includes(field), `AssetBalanceDto should expose ${field} for the frontend table`)
}
for (const field of ['Id', 'Level', 'CurrentAmount', 'PreviousAmount']) {
  assert.ok(reportDto.includes(field), `CashFlowDto should expose ${field} for the frontend table`)
}

for (const [name, source] of [
  ['cash-flow report', cashFlow],
  ['tax-payable report', taxPayable],
  ['profit statement', profitStatement],
]) {
  assert.doesNotMatch(source, /const\s+tableData\s*=\s*ref\(\s*\[/, `${name} should not initialize with mock financial data`)
}
assert.doesNotMatch(
  profitStatement,
  /const\s+enterpriseTableData\s*=\s*ref\(\s*\[/,
  'profit statement enterprise table should not initialize with mock financial data'
)

for (const [name, source] of [
  ['cash-flow report', cashFlow],
  ['tax-payable report', taxPayable],
  ['asset-balance report', assetBalance],
  ['auxiliary-balance report', auxiliaryBalance],
]) {
  assert.match(source, /onMounted\(async\s*\(\)\s*=>\s*\{[\s\S]*await\s+loadPeriods\(\)[\s\S]*loadData\(\)/, `${name} should wait for periods before first data load`)
}

assert.doesNotMatch(financeHome, /quantity-balance|auxiliary-detail|asset-detail|quantity-detail/, 'finance home should not link to undefined report routes')
assert.match(financeHome, /finance\/reports\/account-detail\/\$\{accountId\}/, 'finance home should open account detail with a concrete account id')

assert.match(amoebaController, /HttpGet\("units\/tree"\)/, 'Amoeba controller should expose units/tree for allocation config')

assert.doesNotMatch(voucherAutoService, /TODO:\s*调用 VoucherService 创建实际凭证/, 'voucher draft generation should not be a placeholder')
assert.match(
  voucherAutoService,
  /result\.Errors\.Add\([^)]*(账套|会计期间|上下文)/s,
  'voucher draft generation should report missing account-set/period context instead of silently succeeding'
)
assert.doesNotMatch(
  voucherAutoService,
  /transaction\.FUpdaterName[\s\S]*result\.GeneratedCount\+\+/,
  'voucher draft generation should not count matched bank transactions as generated without creating a voucher'
)

console.log('Finance report contracts are aligned.')
