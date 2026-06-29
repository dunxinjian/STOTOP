import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const exists = (file) => fs.existsSync(path.join(root, file))

for (const file of [
  'src/STOTOP.Module.CardFlow/Models/Rules/DynamicStagePolicyModels.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/IDynamicStagePolicyResolver.cs',
  'src/STOTOP.Module.CardFlow/Services/DynamicStagePolicyResolver.cs',
]) {
  assert.equal(exists(file), true, `${file} should exist`)
}

const resolver = read('src/STOTOP.Module.CardFlow/Services/DynamicStagePolicyResolver.cs')
for (const token of [
  'ResolveAsync',
  'ResolveBeforeTargetAsync',
  'afterSourceBeforeRoute',
  'afterRouteBeforeTarget',
  'afterTarget',
  'replaceTargetHandlers',
  'FPolicyKey',
  'FMaxInsertCount',
  'fallback',
  'fixedUsers',
  'fieldUsers',
  'orgChain',
  'amountMatrix',
  'feeTypeBp',
]) {
  assert(resolver.includes(token), `dynamic policy resolver should include ${token}`)
}

const engine = read('src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs')
assert(engine.includes('IDynamicStagePolicyResolver'), 'flow engine should depend on dynamic policy resolver')
assert(engine.includes('TryStartDynamicStageAsync'), 'flow engine should try dynamic insert by policy timing')
assert(engine.includes('dynamic-policy-replace-handlers'), 'flow engine should support replaceTargetHandlers without dynamic stage insertion')
assert(engine.includes('continuationStageKey'), 'dynamic insert context should record continuationStageKey')

const approverResolver = read('src/STOTOP.Module.CardFlow/Services/ApproverResolver.cs')
for (const token of ['ResolveOrgChainAsync', 'ResolveAmountMatrixAsync', 'ResolveFeeTypeBpAsync']) {
  assert(approverResolver.includes(token), `approver resolver should include ${token}`)
}

const flowDefinitionService = read('src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs')
assert(flowDefinitionService.includes('policy.MaxInsertCount ?? 20'), 'dynamic policy default max insert count should be 20')
assert(flowDefinitionService.includes('NormalizeDynamicPolicyTriggerTiming'), 'dynamic policy trigger timing should be normalized on save and publish')
assert(flowDefinitionService.includes('null or "" => "afterSourceBeforeRoute"'), 'dynamic policy default trigger timing should be afterSourceBeforeRoute')
assert(flowDefinitionService.includes('ValidateDynamicPoliciesAsync'), 'flow publish should validate dynamic policies')
assert(flowDefinitionService.includes('必须配置处理人兜底'), 'dynamic policy publish validation should require fallback')

const moduleExtensions = read('src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs')
assert(moduleExtensions.includes('IDynamicStagePolicyResolver'), 'dynamic policy resolver should be registered')

console.log('CardFlow dynamic approval runtime contract is covered.')
