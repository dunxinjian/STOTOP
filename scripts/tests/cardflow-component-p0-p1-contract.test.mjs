import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const exists = (file) => fs.existsSync(path.join(root, file))

const capabilityFile = 'web/src/components/cardflow/designer/cardComponentCapabilities.ts'
assert.equal(exists(capabilityFile), true, 'Card component capabilities should have a shared source of truth')

const capabilities = read(capabilityFile)
for (const token of [
  'CARD_COMPONENT_CAPABILITIES',
  'CardComponentCapability',
  'publishable: true',
  'publishable: false',
  "tier: 'P0'",
  "tier: 'P1'",
  "tier: 'deferred'",
  'templateOnly',
  'experimental',
  'requiresRuntimeIntegration',
  'defaultProps',
  'configSections',
  'cardField',
  'detailTable',
  'detailSummary',
  'relation',
  'snapshot',
]) {
  assert(capabilities.includes(token), `capability registry should include ${token}`)
}

for (const type of [
  'text',
  'textarea',
  'number',
  'money',
  'date',
  'dateRange',
  'radio',
  'checkbox',
  'idCard',
  'phone',
  'attachment',
  'detailTable',
  'amountSummary',
  'budgetStatus',
  'invoiceStatus',
  'loanOffset',
  'paymentInfo',
  'riskAlert',
  'relationCards',
  'relationLookup',
  'routeDecision',
  'dynamicApprover',
]) {
  const block = capabilities.match(new RegExp(`${type}:\\s*\\{[\\s\\S]*?\\n\\s*\\}`))?.[0] || ''
  assert(block.includes('publishable: true'), `${type} should be publishable in P0`)
  assert(block.includes("tier: 'P0'"), `${type} should be marked as P0`)
}

for (const type of [
  'sectionTitle',
  'textBlock',
  'imageList',
  'signature',
  'rating',
  'placeholderControl',
]) {
  const block = capabilities.match(new RegExp(`${type}:\\s*\\{[\\s\\S]*?\\n\\s*\\}`))?.[0] || ''
  assert(block.includes('publishable: true'), `${type} should be publishable in P1`)
  assert(block.includes("tier: 'P1'"), `${type} should be marked as P1`)
}

for (const type of ['ocrText', 'componentSuite', 'formula', 'columnLayout']) {
  const block = capabilities.match(new RegExp(`${type}:\\s*\\{[\\s\\S]*?\\n\\s*\\}`))?.[0] || ''
  assert(block.includes('publishable: false'), `${type} should not be marked production-ready yet`)
  assert(block.includes("tier: 'deferred'"), `${type} should be marked deferred`)
}

const catalog = read('web/src/components/cardflow/designer/CardComponentCatalog.vue')
for (const token of [
  'CARD_COMPONENT_CAPABILITIES',
  'resolveComponentCapability',
  'capabilityKey',
  'publishable',
  'componentTier',
  'componentStatus',
  'requiresRuntimeIntegration',
  '暂缓',
  '模板',
  'experimental',
  'templateOnly',
]) {
  assert(catalog.includes(token), `component catalog should expose capability token ${token}`)
}
assert(!catalog.includes('限时免费'), 'component catalog should not show commercialization tags')

const config = read('web/src/components/cardflow/designer/CardComponentConfigDrawer.vue')
for (const token of [
  'componentCapability',
  'configWarnings',
  'showOptionConfig',
  'showFieldFormatConfig',
  'showAttachmentConfig',
  'showRatingConfig',
  'showBusinessStatusConfig',
  'showRelationConfig',
  'setNestedProp',
  'optionList',
  'addOption',
  'removeOption',
  '选项配置',
  '添加选项',
  '默认值',
  '占位提示',
  '文件数量',
  '允许文件类型',
  '评分上限',
  '状态字段',
  '金额字段',
  '汇总字段',
  '关联类型',
  '数据源类型',
]) {
  assert(config.includes(token), `component config drawer should expose ${token}`)
}

const renderer = read('web/src/components/cardflow/runtime/CardComponentRenderer.vue')
for (const token of [
  'fieldControlKind',
  'previewVariant',
  'isDesignerPreview',
  'isDesignerFieldControl',
  'normalizedOptions',
  'formatFieldValue',
  'renderFieldControl',
  'updateCheckboxField',
  'isAttachmentField',
  'isChoiceField',
  'isDateRangeField',
  'isTextareaField',
  'maskedValue',
  'cf-runtime-field--textarea',
  'cf-runtime-field--attachment',
  'cf-runtime-field--choice',
  'cf-runtime-field--date-range',
  'cf-runtime-field--designer-control',
  'cf-runtime-choice-control',
  'cf-runtime-choice-control--radio',
  'cf-runtime-choice-dot',
  'cf-runtime-attachment-list',
  'cf-runtime-choice-list',
  'cf-runtime-rating__star',
  'type=\"file\"',
]) {
  assert(renderer.includes(token), `runtime renderer should support P0/P1 token ${token}`)
}

const flowEdit = read('web/src/views/cardflow/FlowDefinitionEditPage.vue')
for (const token of [
  'validateCardComponentPublishability',
  'preview-variant="designer"',
  'componentStatus',
  'requiresRuntimeIntegration',
  '暂未支持发布',
]) {
  assert(flowEdit.includes(token), `publish validation should include ${token}`)
}

console.log('cardflow P0/P1 component contract ok')
