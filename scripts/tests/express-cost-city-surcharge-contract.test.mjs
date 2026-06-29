import { readFileSync } from 'node:fs'
import assert from 'node:assert/strict'

const costPlugin = readFileSync('src/STOTOP.Module.Express/Services/Billing/CostPlugin.cs', 'utf8')

const readSuccessSection = costPlugin.slice(
  costPlugin.indexOf('private async Task<List<CostWaybillInput>> ReadSuccessBillingResults'),
  costPlugin.indexOf('private async Task<BillingInputDiagnostics>')
)

assert.ok(readSuccessSection.includes('LEFT JOIN [{sourceTable}] s'), 'CostPlugin should join the configured staging source table')
assert.ok(readSuccessSection.includes('s.[F运单编号] = r.[F运单编号]'), 'CostPlugin should join staging rows by waybill number')
assert.ok(readSuccessSection.includes('s.[F批次ID] = @batchId'), 'CostPlugin should limit staging city lookup to the current batch')
assert.match(readSuccessSection, /s\.\[F目的城市\]\s+AS\s+\[F目的城市\]/, 'CostPlugin should pass STG.F目的城市 into CostWaybillInput')
assert.ok(!readSuccessSection.includes('CAST(NULL AS NVARCHAR(100)) AS [F目的城市]'), 'CostPlugin must not null out destination city')

console.log('express cost city surcharge contract passed')
