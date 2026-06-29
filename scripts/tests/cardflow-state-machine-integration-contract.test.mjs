import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')

const engineTests = read('tests/STOTOP.Module.CardFlow.Tests/Approval/FlowEngineReturnToStageTests.cs')
for (const token of [
  'DynamicPolicy_AfterSourceBeforeRoute_InsertsBeforeRouteAndContinuesFromOriginalSource',
  'DynamicPolicy_ReplaceTargetHandlers_UsesPolicyApproversWithoutDynamicStage',
  'DynamicPolicy_AfterTarget_InsertsAfterSelectedTargetCompletesAndContinuesFromTarget',
]) {
  assert(engineTests.includes(token), `state-machine integration test should cover ${token}`)
}

const resolverTests = read('tests/STOTOP.Module.CardFlow.Tests/Rules/DynamicStagePolicyResolverTests.cs')
assert(
  resolverTests.includes('ResolveBeforeTarget_UsesPolicyFallbackWhenPrimaryStrategyHasNoHandlers'),
  'dynamic policy resolver tests should cover fallback handler resolution',
)

const engine = read('src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs')
for (const token of [
  'AdvanceToNextStageCoreAsync',
  'afterSourceBeforeRoute',
  'afterTarget',
  'replaceTargetHandlers',
  'dynamic-policy-replace-handlers',
]) {
  assert(engine.includes(token), `flow engine should include ${token}`)
}

const definitionService = read('src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs')
assert(definitionService.includes('ValidateDynamicPoliciesAsync'), 'publish should validate dynamic policies before activation')

console.log('CardFlow state-machine integration contract is covered.')
