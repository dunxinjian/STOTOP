import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')

function assertIncludes(source, token, message) {
  assert.ok(source.includes(token), message || `Expected source to include ${token}`)
}

const auditDtos = read('src/STOTOP.Module.CardFlow/Dtos/AuditDtos.cs')
for (const token of [
  'CardFlowRuntimeAuditDto',
  'CardFlowRuntimeMonitoringRequest',
  'CardFlowRuntimeMonitoringDto',
  'CardFlowRuntimeMonitoringBucketDto',
  'FlowCode',
  'FlowVersionId',
  'StageKey',
  'EdgeKey',
  'PolicyKey',
  'SourceModule',
  'SourceType',
  'OrgId',
  'DateBucket',
  'RouteHitCount',
  'FallbackCount',
  'HandlerUnresolvedCount',
  'DynamicInsertCount',
  'RuleWarningCount',
]) {
  assertIncludes(auditDtos, token, `AuditDtos.cs should define ${token}`)
}

const responses = read('src/STOTOP.Module.CardFlow/Dtos/Responses.cs')
assertIncludes(responses, 'List<CardFlowRuntimeAuditDto> AuditTrail', 'CardDetailDto should expose runtime audit trail')

const cardService = read('src/STOTOP.Module.CardFlow/Services/CardService.cs')
for (const token of [
  'LoadRuntimeAuditTrailAsync',
  'GetRuntimeMonitoringAsync',
  'BuildMonitoringBuckets',
  'FDecisionSnapshotJson',
  'FIsDynamicInsert',
  'SanitizeDynamicInsertContext',
  'handler-unresolved',
]) {
  assertIncludes(cardService, token, `CardService should include ${token}`)
}

const cardController = read('src/STOTOP.Module.CardFlow/Controllers/CardController.cs')
assertIncludes(cardController, 'runtime-monitoring', 'CardFlow audit monitoring endpoint should be exposed')
assertIncludes(cardController, 'GetRuntimeMonitoringAsync', 'CardController should delegate monitoring queries to CardService')

const timeline = read('web/src/components/cardflow/CardTimeline.vue')
for (const token of ['auditTrail', 'routeDecision', 'dynamicApprover', 'getStageAudit']) {
  assertIncludes(timeline, token, `CardTimeline should render ${token}`)
}

const detailPage = read('web/src/views/cardflow/CardDetailPage.vue')
assertIncludes(detailPage, ':audit-trail="card.auditTrail"', 'Card detail page should pass runtime audit trail into timeline')

const cardflowTypes = read('web/src/types/cardflow.ts')
for (const token of [
  'CardFlowRuntimeAuditDto',
  'CardFlowRuntimeMonitoringDto',
  'CardFlowRuntimeMonitoringBucketDto',
  'auditTrail: CardFlowRuntimeAuditDto[]',
]) {
  assertIncludes(cardflowTypes, token, `frontend CardFlow types should include ${token}`)
}

const cardflowApi = read('web/src/api/cardflow.ts')
assertIncludes(cardflowApi, 'getRuntimeMonitoring', 'frontend CardFlow API should expose runtime monitoring')

const workHubService = read('src/STOTOP.WebAPI/Services/WorkHubService.cs')
for (const token of [
  'ITodoService',
  'GetCardFlowTodosFromServiceAsync',
  'Source = "cardflow"',
  'DetailRoute = $"/cardflow/cards/{todo.CardId}"',
]) {
  assertIncludes(workHubService, token, `WorkHubService should include ${token}`)
}

const workhubApi = read('web/src/api/workhub.ts')
assertIncludes(workhubApi, "| 'cardflow'", 'WorkHub frontend source type should include cardflow')

const workItemCard = read('web/src/views/workhub/WorkItemCard.vue')
assertIncludes(workItemCard, 'cardflow:', 'WorkItemCard should render CardFlow source items')
assertIncludes(workItemCard, 'CardFlow审批', 'WorkItemCard should label CardFlow inbox items')

console.log('CardFlow audit, monitoring, and WorkHub visibility contract is covered.')
