import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const exists = (file) => fs.existsSync(path.join(root, file))

for (const file of [
  'src/STOTOP.Module.CardFlow/Models/Rules/ConditionRuleModels.cs',
  'src/STOTOP.Module.CardFlow/Models/Rules/ConditionEvaluationContext.cs',
  'src/STOTOP.Module.CardFlow/Models/Rules/StageRouteModels.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/IConditionRuleEvaluator.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/IConditionEvaluationContextBuilder.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/IStageRouteResolver.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/ICardFlowPathPreviewService.cs',
  'src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs',
  'src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs',
  'src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs',
  'src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs',
  'src/STOTOP.Module.CardFlow/Entities/CfRouteDecisionSnapshot.cs',
  'src/STOTOP.Module.CardFlow/Configurations/CfRouteDecisionSnapshotConfiguration.cs',
]) {
  assert.equal(exists(file), true, `${file} should exist`)
}

const routeConfig = read('src/STOTOP.Module.CardFlow/Configurations/CfStageRouteRuleConfiguration.cs')
assert(routeConfig.includes('CF节点流转规则'), 'route rules should use the agreed table name')

const evaluator = read('src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs')
for (const token of ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'contains', 'startsWith', 'in', 'between', 'exists', 'notExists', 'empty', 'notEmpty', 'inOrgChain']) {
  assert(evaluator.includes(token), `condition evaluator should support ${token}`)
}
assert(evaluator.includes('TypeErrors'), 'condition evaluator should report type errors')
assert(evaluator.includes('ConsumedFields'), 'condition evaluator should report consumed fields')

const legacy = read('src/STOTOP.Module.CardFlow/Services/ConditionEvaluator.cs')
assert(legacy.includes('IConditionRuleEvaluator'), 'legacy ConditionEvaluator should delegate JSON rules')
assert(legacy.includes('TrimStart().StartsWith("{", StringComparison.Ordinal)'), 'legacy adapter should detect JSON object conditions')

const resolver = read('src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs')
for (const token of ['ResolveNextStageAsync', 'FStageKey', 'FEdgeKey', 'FIsDefault', 'RuleMode', 'legacy']) {
  assert(resolver.includes(token), `stage route resolver should include ${token}`)
}

const engine = read('src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs')
assert(engine.includes('IStageRouteResolver'), 'flow engine should depend on route resolver')
assert(engine.includes('ResolveNextStageAsync'), 'flow engine should resolve conditional next stage')
assert(engine.includes('CfRouteDecisionSnapshot'), 'flow engine should persist route decision snapshots')

const moduleExtensions = read('src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs')
for (const service of ['IConditionRuleEvaluator', 'IConditionEvaluationContextBuilder', 'IStageRouteResolver', 'IAuditSnapshotPolicyService', 'ICardFlowPathPreviewService']) {
  assert(moduleExtensions.includes(service), `${service} should be registered`)
}

const controller = read('src/STOTOP.Module.CardFlow/Controllers/FlowDefinitionController.cs')
assert(controller.includes('preview-path'), 'FlowDefinitionController should expose path preview endpoint')
assert(controller.includes('CardFlowPathPreviewRequest'), 'path preview endpoint should accept typed preview request')

const webApi = read('web/src/api/cardflow.ts')
assert(webApi.includes('previewFlowDraftPath'), 'web API should expose draft path preview function')

console.log('CardFlow conditional routing runtime contract is covered.')
