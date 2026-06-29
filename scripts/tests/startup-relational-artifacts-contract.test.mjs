#!/usr/bin/env node
import assert from 'node:assert/strict'
import fs from 'node:fs'

const read = path => fs.readFileSync(path, 'utf8')
const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

function assertRelationshipUsesNoAction(path, startToken, endToken) {
  const source = read(path)
  const start = source.indexOf(startToken)
  assert.notEqual(start, -1, `${path} should contain ${startToken}`)
  const end = source.indexOf(endToken, start)
  assert.notEqual(end, -1, `${path} should contain ${endToken} after ${startToken}`)
  const relationshipEnd = source.indexOf(';', end)
  assert.notEqual(relationshipEnd, -1, `${path} relationship ${startToken} should end with a semicolon`)
  const block = source.slice(start, relationshipEnd + 1)
  assert.match(
    block,
    /OnDelete\(DeleteBehavior\.NoAction\)/,
    `${path} relationship ${startToken} should use DeleteBehavior.NoAction to avoid SQL Server multiple cascade paths`,
  )
}

for (const [path, startToken, endToken] of [
  ['src/STOTOP.Module.CRM/Configurations/CrmServiceOrderConfiguration.cs', 'builder.HasMany(e => e.Logs)', '.HasForeignKey(e => e.FOrderId)'],
  ['src/STOTOP.Module.CRM/Configurations/CrmBonusPlanConfiguration.cs', 'builder.HasMany(e => e.Details)', '.HasForeignKey(e => e.FPlanId)'],
  ['src/STOTOP.Module.CRM/Configurations/CrmCustomerConfiguration.cs', 'builder.HasMany(e => e.Contacts)', '.HasForeignKey(e => e.FCustomerId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpPriceSurchargeConfiguration.cs', 'builder.HasMany(e => e.Items)', '.HasForeignKey(i => i.FSurchargeId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpPriceSurchargeItemConfiguration.cs', 'builder.HasMany(e => e.Destinations)', '.HasForeignKey(d => d.FSurchargeItemId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationConfiguration.cs', 'builder.HasMany(e => e.Shops)', '.HasForeignKey(s => s.FQuotationId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationConfiguration.cs', 'builder.HasMany(e => e.Aliases)', '.HasForeignKey(a => a.FQuotationId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationConfiguration.cs', 'builder.HasOne(e => e.Commission)', '.HasForeignKey<ExpQuotationCommission>(c => c.FQuotationId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationShopConfiguration.cs', 'builder.HasOne(e => e.Quotation)', '.HasForeignKey(e => e.FQuotationId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationAliasConfiguration.cs', 'builder.HasOne(e => e.Quotation)', '.HasForeignKey(e => e.FQuotationId)'],
  ['src/STOTOP.Module.Express/Configurations/ExpQuotationCommissionConfiguration.cs', 'builder.HasOne(e => e.Quotation)', '.HasForeignKey<ExpQuotationCommission>(e => e.FQuotationId)'],
  ['src/STOTOP.Module.Workflow/Configurations/WfWorkItemConfiguration.cs', 'builder.HasMany(e => e.Logs)', '.HasForeignKey(e => e.FWorkItemId)'],
  ['src/STOTOP.Module.Workflow/Configurations/WfIssuePackConfiguration.cs', 'builder.HasMany(e => e.Details)', '.HasForeignKey(e => e.FIssuePackId)'],
]) {
  assertRelationshipUsesNoAction(path, startToken, endToken)
}

const expressSeeder = read('src/STOTOP.WebAPI/Data/Seeders/ExpressSeeder.cs')
assert.match(expressSeeder, /new\(17,\s*"修正承包区孤儿组织引用/, 'ExpressSeeder should add V17 for orphan franchise-area org ids')
for (const token of ['[dbo].[EXP承包区]', '[F组织ID] = 192', 'SYS组织架构', 'NOT EXISTS']) {
  assert.match(expressSeeder, new RegExp(escape(token)), `ExpressSeeder V17 should repair EXP承包区.F组织ID via ${token}`)
}

const cardFlowSeeder = read('src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs')
assert.match(cardFlowSeeder, /new\(19,\s*"补齐历史流程节点稳定键/, 'CardFlowSeeder should add V19 for historical blank stage keys')
for (const token of ['F节点键', 'ROW_NUMBER()', 'stage_', 'F流程版本ID']) {
  assert.match(cardFlowSeeder, new RegExp(escape(token)), `CardFlowSeeder V19 should include ${token}`)
}

const migrationRunner = read('src/STOTOP.WebAPI/Data/Seeders/MigrationRunner.cs')
assert.match(
  migrationRunner,
  /SqlQueryRaw<int>\([\s\S]*?\)\s*\.AsEnumerable\(\)\s*\.FirstOrDefault\(/,
  'MigrationRunner scalar startup checks should enumerate SqlQueryRaw<int> before FirstOrDefault',
)

// 启动性能契约：关系对象补建必须「先读后写」，不允许退回逐条 IF NOT EXISTS 往返
const seederAdapter = read('src/STOTOP.WebAPI/Data/DatabaseSeederAdapter.cs')
assert.match(
  seederAdapter,
  /QueryExistingArtifactNames/,
  'CreateRelationalArtifacts should prefetch existing artifact names instead of per-object round trips',
)
for (const token of ['sys.key_constraints', 'sys.foreign_keys', 'sys.indexes']) {
  assert.match(
    seederAdapter,
    new RegExp(escape(token)),
    `QueryExistingArtifactNames should read ${token} in the prefetch query`,
  )
}
assert.match(
  seederAdapter,
  /existingArtifacts\.Contains\(ArtifactKey\(/,
  'CreateRelationalArtifacts should skip artifacts already present in the prefetched set',
)
assert.match(
  seederAdapter,
  /BaselineReferenceDataSeeder\.Seed\(ctx,\s*force:\s*strictRelationalArtifacts\)/,
  'MigrateAll should gate baseline seeding by fingerprint while --init-database forces it',
)

// 启动性能契约：baseline 数据按文件指纹跳过，避免每次启动逐行 upsert
const baselineSeeder = read('src/STOTOP.WebAPI/Data/Seeders/BaselineReferenceDataSeeder.cs')
assert.match(
  baselineSeeder,
  /SHA256\.HashData/,
  'BaselineReferenceDataSeeder should fingerprint the baseline file',
)
assert.match(
  baselineSeeder,
  /SYS基线数据同步记录/,
  'BaselineReferenceDataSeeder should persist the applied fingerprint',
)
assert.match(
  baselineSeeder,
  /SaveAppliedFingerprint\(ctx,\s*fingerprint\);\s*\n\s*transaction\.Commit\(\)/,
  'BaselineReferenceDataSeeder should save the fingerprint inside the seeding transaction',
)
