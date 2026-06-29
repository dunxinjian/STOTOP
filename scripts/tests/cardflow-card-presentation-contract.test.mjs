import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const exists = (file) => fs.existsSync(path.join(root, file))

for (const file of [
  'src/STOTOP.Module.CardFlow/Models/Schema/CardPresentationModels.cs',
  'src/STOTOP.Module.CardFlow/Services/Interfaces/ICardPresentationResolver.cs',
  'src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs',
  'tests/STOTOP.Module.CardFlow.Tests/Presentation/CardPresentationResolverTests.cs',
  'web/src/components/cardflow/runtime/CardComponentRenderer.vue',
  'web/src/components/cardflow/runtime/components/AmountSummaryComponent.vue',
  'web/src/components/cardflow/runtime/components/DetailTableComponent.vue',
  'web/src/components/cardflow/runtime/components/RouteDecisionComponent.vue',
  'web/src/components/cardflow/runtime/components/DynamicApproverComponent.vue',
]) {
  assert.equal(exists(file), true, `${file} should exist`)
}

const models = read('src/STOTOP.Module.CardFlow/Models/Schema/CardPresentationModels.cs')
for (const token of [
  'CardComponentDefinition',
  'CardComponentBinding',
  'CardComponentRuntimeDto',
  'CardPresentationRuntimeView',
  'DetailSummary',
  'Snapshots',
  'Relations',
  'Aggregation',
]) {
  assert(models.includes(token), `Card presentation models should include ${token}`)
}

const schemaModels = read('src/STOTOP.Module.CardFlow/Models/Schema/CardSchemaV2Models.cs')
assert(schemaModels.includes('Components'), 'CardSchemaV2 should expose component definitions')

const profileModels = read('src/STOTOP.Module.CardFlow/Models/Schema/StageViewProfileModels.cs')
assert(profileModels.includes('ComponentAccess'), 'StageViewProfile should expose component-level access rules')
assert(profileModels.includes('StageComponentRef'), 'StageViewProfile should expose node component references')

const responses = read('src/STOTOP.Module.CardFlow/Dtos/Responses.cs')
assert(responses.includes('List<CardComponentRuntimeDto> Components'), 'StageWorkViewDto should return runtime components')
assert(responses.includes('Dictionary<string, object?> DetailSummary'), 'StageWorkViewDto should return detail summary')

const resolver = read('src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs')
for (const token of ['detailSum.amount', 'BuildLegacyComponents', 'ApplyComponentAccess', 'snapshot', 'ResolveRelations']) {
  assert(resolver.includes(token), `CardPresentationResolver should include ${token}`)
}

const stageViewResolver = read('src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs')
assert(stageViewResolver.includes('ICardPresentationResolver'), 'StageViewProfileResolver should delegate to card presentation resolver')
assert(stageViewResolver.includes('Presentation'), 'StageViewResolutionResult should carry presentation runtime view')
assert(stageViewResolver.includes('Snapshots = snapshots?.ToList()'), 'StageViewProfileResolver should pass runtime snapshots into presentation resolver')

const stageViewResolverInterface = read('src/STOTOP.Module.CardFlow/Services/Interfaces/IStageViewProfileResolver.cs')
assert(stageViewResolverInterface.includes('IReadOnlyCollection<CardPresentationSnapshot>? snapshots'), 'stage view resolver should accept presentation snapshots')

const cardService = read('src/STOTOP.Module.CardFlow/Services/CardService.cs')
assert(cardService.includes('LoadPresentationSnapshotsAsync'), 'CardService should load presentation snapshots for the current work view')
assert(cardService.includes('LoadPresentationRelationsAsync'), 'CardService should load card relations for relation components')
assert(cardService.includes('CfRouteDecisionSnapshot'), 'CardService should load route decision snapshots')
assert(cardService.includes('FIsDynamicInsert'), 'CardService should expose dynamic inserted approval snapshots')
assert(cardService.includes('SnapshotType = "routeDecision"'), 'CardService should expose routeDecision snapshots')
assert(cardService.includes('SnapshotType = "dynamicApprover"'), 'CardService should expose dynamicApprover snapshots')

const moduleExtensions = read('src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs')
assert(moduleExtensions.includes('ICardPresentationResolver'), 'Card presentation resolver should be registered')

const stageFieldAccess = read('src/STOTOP.Module.CardFlow/Services/StageFieldAccessService.cs')
assert(stageFieldAccess.includes('ComponentAccess'), 'StageFieldAccessService should enforce component access')

const schemaRenderer = read('web/src/components/cardflow/SchemaRenderer.vue')
assert(schemaRenderer.includes('CardComponentRenderer'), 'SchemaRenderer should delegate runtime components to CardComponentRenderer')
assert(schemaRenderer.includes('components?: CardComponentRuntime[]'), 'SchemaRenderer should accept runtime components')

const cardFlowPanel = read('web/src/components/cardflow/CardFlowPanel.vue')
assert(cardFlowPanel.includes('stageRuntimeComponents'), 'CardFlowPanel should derive runtime components from current stage work view')
assert(cardFlowPanel.includes(':components="stageRuntimeComponents"'), 'CardFlowPanel should pass runtime components to SchemaRenderer')

const mobileApproval = read('web/src/views/cardflow-mobile/MobileCardApprovalPage.vue')
assert(mobileApproval.includes('runtimeComponents'), 'mobile approval page should use current stage runtime components')
assert(mobileApproval.includes(':components="runtimeComponents"'), 'mobile approval page should pass runtime components')

console.log('CardFlow card presentation runtime contract is covered.')
