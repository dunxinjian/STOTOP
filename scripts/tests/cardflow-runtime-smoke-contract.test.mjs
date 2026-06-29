import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')

const responses = read('src/STOTOP.Module.CardFlow/Dtos/Responses.cs')
assert(responses.includes('List<CardComponentRuntimeDto> Components'), 'work view dto should expose runtime components')
assert(responses.includes('Dictionary<string, object?> DetailSummary'), 'work view dto should expose detail summary')

const cardService = read('src/STOTOP.Module.CardFlow/Services/CardService.cs')
assert(cardService.includes('Components = resolved.Presentation.Components'), 'CardService should map runtime components into work view dto')
assert(cardService.includes('DetailSummary = resolved.Presentation.DetailSummary'), 'CardService should map detail summary into work view dto')
assert(cardService.includes('LoadPresentationSnapshotsAsync'), 'CardService should load snapshot explanations for runtime components')
assert(cardService.includes('LoadPresentationRelationsAsync'), 'CardService should load card relations for runtime relation components')

const resolver = read('src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs')
assert(resolver.includes('BuildLegacyComponents'), 'presentation resolver should synthesize legacy components')
assert(resolver.includes('Binding.Source'), 'presentation resolver should bind runtime values by source')
assert(resolver.includes('component.Snapshots = request.Snapshots'), 'presentation resolver should bind snapshot explanation components')
assert(resolver.includes('ResolveRelations'), 'presentation resolver should bind relation components')

const types = read('web/src/types/cardflow.ts')
assert(types.includes('components: CardComponentRuntime[]'), 'frontend StageWorkView type should include runtime components')
assert(types.includes('detailSummary: Record<string, any>'), 'frontend StageWorkView type should include detail summary')
assert(types.includes('interface CardPresentationSnapshot'), 'frontend should type snapshot explanations')

const schemaRenderer = read('web/src/components/cardflow/SchemaRenderer.vue')
assert(schemaRenderer.includes('CardComponentRenderer'), 'SchemaRenderer should render runtime components')
assert(schemaRenderer.includes('hasRuntimeComponents'), 'SchemaRenderer should prefer runtime components when available')

const pcPanel = read('web/src/components/cardflow/CardFlowPanel.vue')
assert(pcPanel.includes('stageRuntimeComponents'), 'PC panel should derive runtime components from stage work view')
assert(pcPanel.includes(':components="stageRuntimeComponents"'), 'PC panel should pass runtime components into SchemaRenderer')
assert(pcPanel.includes('!hasStageRuntimeComponents'), 'PC panel should avoid duplicate legacy detail display when runtime components exist')

for (const file of [
  'web/src/views/cardflow-mobile/MobileCardApprovalPage.vue',
  'web/src/views/cardflow-mobile/CardApprovalView.vue',
  'web/src/views/cardflow-mobile/CardDetailView.vue',
]) {
  const source = read(file)
  assert(source.includes('runtimeComponents'), `${file} should derive runtime components`)
  assert(source.includes(':components="runtimeComponents"'), `${file} should pass runtime components into SchemaRenderer`)
}

console.log('CardFlow runtime presentation smoke contract is covered.')
