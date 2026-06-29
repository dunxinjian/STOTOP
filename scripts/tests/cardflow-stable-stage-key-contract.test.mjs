#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'

const read = path => fs.readFileSync(path, 'utf8')
const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

const stageEntity = read('src/STOTOP.Module.CardFlow/Entities/CfStageDefinition.cs')
assert.match(stageEntity, /FStageKey/, 'CfStageDefinition should persist immutable FStageKey')

const stageConfig = read('src/STOTOP.Module.CardFlow/Configurations/CfStageDefinitionConfiguration.cs')
for (const token of [
  'F节点键',
  'IX_CF流程节点_版本节点键',
]) {
  assert.match(stageConfig, new RegExp(token), `CfStageDefinitionConfiguration should map ${token}`)
}

const requests = read('src/STOTOP.Module.CardFlow/Dtos/Requests.cs')
for (const token of [
  'public string? StageKey',
  'public List<StageRouteRuleRequest> Routes',
  'public List<DynamicStagePolicyRequest> DynamicPolicies',
  'public string EdgeKey',
  'public string FromStageKey',
  'public string ToStageKey',
  'public string PolicyKey',
  'public string SourceStageKey',
]) {
  assert.match(requests, new RegExp(escape(token)), `Requests.cs should include ${token}`)
}

const responses = read('src/STOTOP.Module.CardFlow/Dtos/Responses.cs')
for (const token of [
  'public string StageKey',
  'public List<StageRouteRuleDto> Routes',
  'public List<DynamicStagePolicyDto> DynamicPolicies',
  'public string EdgeKey',
  'public string FromStageKey',
  'public string ToStageKey',
  'public string PolicyKey',
  'public string SourceStageKey',
]) {
  assert.match(responses, new RegExp(escape(token)), `Responses.cs should include ${token}`)
}

const routeEntityPath = 'src/STOTOP.Module.CardFlow/Entities/CfStageRouteRule.cs'
assert.ok(fs.existsSync(routeEntityPath), 'CfStageRouteRule entity should exist')
const routeEntity = read(routeEntityPath)
for (const token of ['FEdgeKey', 'FFromStageKey', 'FToStageKey']) {
  assert.match(routeEntity, new RegExp(token), `CfStageRouteRule should expose ${token}`)
}

const policyEntityPath = 'src/STOTOP.Module.CardFlow/Entities/CfDynamicStagePolicy.cs'
assert.ok(fs.existsSync(policyEntityPath), 'CfDynamicStagePolicy entity should exist')
const policyEntity = read(policyEntityPath)
for (const token of ['FPolicyKey', 'FSourceStageKey', 'FTriggerTiming', 'FContinuationStageKey']) {
  assert.match(policyEntity, new RegExp(token), `CfDynamicStagePolicy should expose ${token}`)
}

const service = read('src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs')
for (const token of [
  'EnsureStageKey',
  'BuildStageKeyMapAsync',
  'SaveRouteRulesAsync',
  'SaveDynamicPoliciesAsync',
  'CloneRouteRulesAsync',
  'CloneDynamicPoliciesAsync',
  '必须携带稳定 StageKey',
  'StageKey 重复',
]) {
  assert.match(service, new RegExp(token), `FlowDefinitionService should implement stable key workflow via ${token}`)
}

const webTypes = read('web/src/types/cardflow.ts')
for (const token of [
  'stageKey?: string',
  'routes: StageRouteRuleRequest[]',
  'dynamicPolicies: DynamicStagePolicyRequest[]',
  'edgeKey: string',
  'policyKey: string',
]) {
  assert.match(webTypes, new RegExp(escape(token)), `web cardflow types should include ${token}`)
}

const flowPage = read('web/src/views/cardflow/FlowDefinitionEditPage.vue')
assert.match(flowPage, /id:\s*s\.stageKey\s*\|\|/, 'FlowDefinitionEditPage should preserve backend stageKey as local id')

console.log('CardFlow stable graph key contract is covered.')
