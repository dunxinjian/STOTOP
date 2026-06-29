import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (path) => fs.readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8')

const cardService = read('src/STOTOP.Module.CardFlow/Services/CardService.cs')
const flowEngine = read('src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs')
const moduleExtensions = read('src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs')
const cardPanel = read('web/src/components/cardflow/CardFlowPanel.vue')

assert.ok(
  moduleExtensions.includes('IBudgetOccupationService'),
  'CardFlow module should be able to resolve budget occupation service'
)

for (const method of ['PreviewAsync', 'OccupyAsync', 'LockAsync', 'ConsumeAsync', 'ReleaseAsync']) {
  assert.ok(
    cardService.includes(method) || flowEngine.includes(method),
    `CardFlow lifecycle should call budget ${method}`
  )
}

assert.ok(
  cardService.includes('transitionKey') || flowEngine.includes('transitionKey'),
  'CardFlow budget calls should pass transitionKey for idempotency'
)

assert.ok(
  cardPanel.includes('previewBudgetControl') &&
    cardPanel.includes('预算') &&
    cardPanel.includes('超预算'),
  'CardFlow panel should show budget preview and over-budget state before submit'
)

console.log('CardFlow budget control contract is aligned.')
