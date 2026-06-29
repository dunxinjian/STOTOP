#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'

const read = path => fs.readFileSync(path, 'utf8')
const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

const cardEntity = read('src/STOTOP.Module.CardFlow/Entities/CfCard.cs')
for (const token of [
  'FSourceModule',
  'FSourceType',
  'FSourceId',
  'FReturnUrl',
  'FInitialDataJson',
  'FSourceTitle',
]) {
  assert.match(cardEntity, new RegExp(token), `CfCard should expose ${token}`)
}

const cardConfig = read('src/STOTOP.Module.CardFlow/Configurations/CfCardConfiguration.cs')
for (const token of [
  'F来源模块',
  'F来源类型',
  'F来源ID',
  'F返回地址',
  'F初始数据JSON',
  'F来源标题',
  'IX_CF流程实例_来源',
]) {
  assert.match(cardConfig, new RegExp(token), `CfCardConfiguration should map ${token}`)
}

const requests = read('src/STOTOP.Module.CardFlow/Dtos/Requests.cs')
for (const token of [
  'public string? SourceModule',
  'public string? SourceType',
  'public long? SourceId',
  'public string? ReturnUrl',
  'public string? InitialDataJson',
  'public string? SourceTitle',
]) {
  assert.match(requests, new RegExp(escape(token)), `Requests.cs should include ${token}`)
}

const responses = read('src/STOTOP.Module.CardFlow/Dtos/Responses.cs')
for (const token of [
  'SourceModule',
  'SourceType',
  'SourceId',
  'ReturnUrl',
  'InitialDataJson',
  'SourceTitle',
]) {
  assert.match(responses, new RegExp(token), `Responses.cs should expose ${token}`)
}

const cardService = read('src/STOTOP.Module.CardFlow/Services/CardService.cs')
for (const token of [
  'ICardFlowSourceContextVerifier',
  'VerifyAsync(request)',
  'FSourceModule = NormalizeSourceContext(request.SourceModule)',
  'FSourceType = NormalizeSourceContext(request.SourceType)',
  'FSourceId = request.SourceId',
  'FReturnUrl = NormalizeReturnUrl(request.ReturnUrl)',
  'FInitialDataJson = sourceVerification.StoredInitialDataJson',
  'MergeTrustedDataJson',
]) {
  assert.match(cardService, new RegExp(escape(token)), `CardService should persist source context via ${token}`)
}

const sourceVerifier = read('src/STOTOP.Module.CardFlow/Services/CardFlowSourceContextVerifier.cs')
for (const token of ['OaExpenseRequest', 'OaLoanApplication', 'OaExpenseReimbursement', 'TrustedDataJson', 'StoredInitialDataJson']) {
  assert.match(sourceVerifier, new RegExp(token), `CardFlowSourceContextVerifier should provide trusted source facts via ${token}`)
}

const flowSelect = read('web/src/views/cardflow-mobile/FlowSelectPage.vue')
assert.match(flowSelect, /useOrgContextStore/, 'mobile flow select should read current org context')
assert.doesNotMatch(flowSelect, /getAvailableFlows\(1\)/, 'mobile flow select must not hard-code orgId=1')
assert.doesNotMatch(flowSelect, /getFlowGroups\(1\)/, 'mobile flow groups must not hard-code orgId=1')

const fillForm = read('web/src/views/cardflow-mobile/CardFillForm.vue')
assert.match(fillForm, /useOrgContextStore/, 'mobile card fill should read current org context')
assert.match(fillForm, /buildCreateCardPayload/, 'mobile card fill should centralize create payload with source context')
assert.doesNotMatch(fillForm, /orgId:\s*1/, 'mobile createCard payload must not hard-code orgId=1')
for (const token of ['sourceModule', 'sourceType', 'sourceId', 'returnUrl', 'initialDataJson', 'sourceTitle']) {
  assert.match(fillForm, new RegExp(token), `mobile card fill should include ${token} when present`)
}

console.log('CardFlow source context and mobile org launch contract is covered.')
