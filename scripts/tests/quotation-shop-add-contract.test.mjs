#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = file => fs.readFileSync(path.join(root, file), 'utf8')

const shopPanel = read('web/src/views/express/quotation/components/ShopPanel.vue')
const batchShopModal = read('web/src/views/express/components/BatchShopModal.vue')
const shopService = read('src/STOTOP.Module.Express/Services/ShopService.cs')

function assertFreeTextShopAdder(source, label, modelName) {
  assert.ok(
    source.includes('<a-auto-complete') && source.includes(`v-model:value="${modelName}"`),
    `${label} should use auto-complete so typed shop names become the model value`
  )
  assert.ok(
    source.includes('placeholder="输入或搜索店铺"'),
    `${label} should communicate that typing a new shop name is valid`
  )
  assert.ok(
    source.includes('candidateShopName') && source.includes(':disabled="!candidateShopName"'),
    `${label} add button should be enabled from trimmed typed input, not only an existing selected option`
  )
  assert.equal(
    source.includes(`:disabled="!${modelName}"`),
    false,
    `${label} add button should not stay disabled just because no dropdown option was selected`
  )
  assert.ok(
    source.includes('isShopAlreadyLinked') && source.includes('该店铺已关联当前报价'),
    `${label} should warn and block before adding a shop that is already linked to the quotation`
  )
}

assertFreeTextShopAdder(shopPanel, 'quotation edit quick shop panel', 'selectedShopName')
assertFreeTextShopAdder(batchShopModal, 'batch shop modal', 'addShopName')

const addShopsMethod = shopService.match(/public async Task<int> AddShopsToQuotationAsync[\s\S]*?\n    public async Task<bool> RemoveShopFromQuotationAsync/)?.[0] || ''
assert.ok(addShopsMethod.includes('EnsureShopMastersAsync'), 'Adding quotation shops should ensure missing EXP shop masters exist')
assert.ok(addShopsMethod.includes('duplicated') && addShopsMethod.includes('店铺已关联当前报价'), 'Backend should reject duplicate quotation shop links with a clear message')
assert.ok(shopService.includes('private async Task EnsureShopMastersAsync'), 'ShopService should have an explicit shop master upsert helper')
for (const token of ['new ExpShop', 'FNeedsAssignment = false', 'FStatus = 1']) {
  assert.ok(shopService.includes(token), `Shop master creation/update should include ${token}`)
}

console.log('Quotation shop add contract ok.')
