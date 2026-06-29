import fs from 'node:fs'
import assert from 'node:assert/strict'

const sourcePath = new URL('../../src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs', import.meta.url)
const source = fs.readFileSync(sourcePath, 'utf8')
const cardServicePath = new URL('../../src/STOTOP.Module.CardFlow/Services/CardService.cs', import.meta.url)
const cardService = fs.readFileSync(cardServicePath, 'utf8')
const cardFlowPanelPath = new URL('../../web/src/components/cardflow/CardFlowPanel.vue', import.meta.url)
const cardFlowPanel = fs.readFileSync(cardFlowPanelPath, 'utf8')
const schemaFieldEditorPath = new URL('../../web/src/components/cardflow/SchemaFieldEditor.vue', import.meta.url)
const schemaFieldEditor = fs.readFileSync(schemaFieldEditorPath, 'utf8')
const cardDetailTablePath = new URL('../../web/src/components/cardflow/CardDetailTable.vue', import.meta.url)
const cardDetailTable = fs.readFileSync(cardDetailTablePath, 'utf8')

function assertIncludes(needle, message) {
  assert.ok(source.includes(needle), message)
}

function assertMatches(pattern, message) {
  assert.ok(pattern.test(source), message)
}

assertIncludes('MigrateV15', 'CardFlowSeeder should include the expense reimbursement usability migration')
assertIncludes('MigrateV16', 'CardFlowSeeder should include a CardFlow 2.0 finance-flow migration')
assertIncludes('MigrateV17', 'CardFlowSeeder should include a CardFlow 2.0 finance reference template migration')
assertMatches(
  /new\(16,\s*"费用资金类流程升级为 CardFlow 2\.0 参考模板 \(2026-06-07\)",\s*MigrateV16\)/,
  'CardFlowSeeder should register the finance CardFlow 2.0 migration'
)
assertMatches(
  /new\(17,\s*"费用资金类参考模板补齐条件路由、动态审批和组件展示 \(2026-06-09\)",\s*MigrateV17\)/,
  'CardFlowSeeder should register the P4 finance reference-template migration'
)

{
  const fybsSchemaUpdate = source.match(/UPDATE\s+\[CF流程版本\][\s\S]*?WHERE\s+FID\s*=\s*1347;/)?.[0] ?? ''
  for (const fieldType of ['money', 'enum', 'org', 'file']) {
    assert.ok(
      fybsSchemaUpdate.includes(`""type"":""${fieldType}""`),
      `FYBS schema should use renderer-supported field type ${fieldType}`
    )
  }
  for (const fieldKey of ['applicant', 'department']) {
    assert.ok(
      fybsSchemaUpdate.includes(`""key"":""${fieldKey}"",""label""`) &&
        fybsSchemaUpdate.match(new RegExp(`""key"":""${fieldKey}""[\\s\\S]*?""readonly"":true[\\s\\S]*?""dataSource"":""auto""`)),
      `FYBS ${fieldKey} field should be auto-filled and readonly`
    )
  }
  assert.ok(
    fybsSchemaUpdate.match(/""key"":""department""[\s\S]*?""autoSource"":""currentUserDepartment""/),
    'FYBS department field should auto-fill from the current user department, not just current org context'
  )
}

{
  const updates = [...source.matchAll(/UPDATE\s+\[CF流程版本\][\s\S]*?WHERE\s+FID\s*=\s*1350;/g)]
  const qksqSchemaUpdate = updates.at(-1)?.[0] ?? ''
  for (const field of [
    '""key"":""applicant"",""label"":""申请人"",""type"":""user""',
    '""key"":""department"",""label"":""申请部门"",""type"":""org""',
    '""key"":""expenseType"",""label"":""费用类型"",""type"":""enum""',
    '""key"":""amount"",""label"":""申请金额"",""type"":""money""',
    '""key"":""availableAmount"",""label"":""可报销余额"",""type"":""money"",""readonly"":true',
    '""key"":""payeeAccountNo"",""label"":""收款账号""',
    '""key"":""expectedPayDate"",""label"":""期望付款日期"",""type"":""date""',
    '""key"":""attachments"",""label"":""申请附件"",""type"":""file""',
  ]) {
    assert.ok(qksqSchemaUpdate.includes(field), `QKSQ 2.0 card schema should include ${field}`)
  }
}

assertMatches(
  /N'费用申请审批'[\s\S]*""version"":2[\s\S]*""viewProfile""[\s\S]*""payeeAccountNo"":\{""access"":""masked""\}/s,
  'QKSQ 2.0 should configure requester-manager approval with masked payee account'
)

assertMatches(
  /N'财务预算确认'[\s\S]*""availableAmount"":\{""access"":""readonly""\}[\s\S]*""paymentAccount"":\{""access"":""required"",""required"":true\}/s,
  'QKSQ 2.0 should configure finance budget confirmation with available amount and payment account'
)

assertMatches(
  /UPDATE\s+\[CF流程版本\][\s\S]*?""version"":2[\s\S]*?""components""[\s\S]*?""budgetStatus""[\s\S]*?""routeDecision""[\s\S]*?""previewSamples""[\s\S]*?WHERE\s+FID\s*=\s*1350;/s,
  'QKSQ P4 should use card schema v2 with budget and route-decision components plus preview samples'
)

assertMatches(
  /VALUES\s*\(\s*2261,[\s\S]*N'借款申请',[\s\S]*N'JKSQ',[\s\S]*N'published'/s,
  'CardFlowSeeder should seed a published JKSQ loan application flow'
)

assertMatches(
  /VALUES\s*\(\s*2261,\s*0,[\s\S]*1,\s*2261,[\s\S]*1,\s*N'published'\s*\)/,
  'JKSQ should have a current published version'
)

{
  const jksqVersionInsert = source.match(/VALUES\s*\(\s*2261,\s*0,[\s\S]*?1,\s*2261,[\s\S]*?N'published'\s*\)/)?.[0] ?? ''
  for (const field of [
    '""key"":""loanAmount"",""label"":""借款金额"",""type"":""money""',
    '""key"":""loanReason"",""label"":""借款事由""',
    '""key"":""expectedReturnDate"",""label"":""预计归还日期"",""type"":""date""',
    '""key"":""payeeAccountNo"",""label"":""收款账号""',
    '""key"":""paymentAccount"",""label"":""放款银行账户"",""type"":""bankAccount""',
    '""key"":""reimburseOffsetAmount"",""label"":""已报销冲抵"",""type"":""money"",""readonly"":true',
    '""key"":""outstandingBalance"",""label"":""未还余额"",""type"":""money"",""readonly"":true',
  ]) {
    assert.ok(jksqVersionInsert.includes(field), `JKSQ 2.0 card schema should include ${field}`)
  }
}

assertMatches(
  /VALUES\s*\(\s*5022,\s*2261,\s*1,\s*N'借款审批',\s*N'human',\s*N'card',\s*N'single'/s,
  'JKSQ current version should have a loan approval stage'
)

assertMatches(
  /N'财务放款确认'[\s\S]*""paymentAccount"":\{""access"":""required"",""required"":true\}[\s\S]*""outstandingBalance"":\{""access"":""readonly""\}/s,
  'JKSQ 2.0 should configure finance disbursement confirmation with bank account and loan balance'
)

assertMatches(
  /UPDATE\s+\[CF流程版本\][\s\S]*?""version"":2[\s\S]*?""components""[\s\S]*?""loanOffset""[\s\S]*?""repaymentPlan""[\s\S]*?""routeDecision""[\s\S]*?""previewSamples""[\s\S]*?WHERE\s+FID\s*=\s*2261;/s,
  'JKSQ P4 should use card schema v2 with loan balance, repayment-plan and route-decision components plus preview samples'
)

{
  const updates = [...source.matchAll(/UPDATE\s+\[CF流程版本\][\s\S]*?WHERE\s+FID\s*=\s*1347;/g)]
  const fybsV2SchemaUpdate = updates.at(-1)?.[0] ?? ''
  for (const field of [
    '""key"":""category"",""label"":""报销场景""',
    '""key"":""requestRef"",""label"":""引用请款单""',
    '""key"":""loanRef"",""label"":""引用借款/备用金""',
    '""key"":""paymentMethod"",""label"":""收款方式""',
    '""key"":""payeeName"",""label"":""收款人名称""',
    '""key"":""payeeAccountNo"",""label"":""收款人账号""',
    '""key"":""payeeBankName"",""label"":""收款人开户行""',
    '""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount""',
    '""key"":""remarks"",""label"":""备注""',
  ]) {
    assert.ok(fybsV2SchemaUpdate.includes(field), `FYBS 2.0 card schema should include ${field}`)
  }
  assert.ok(
    fybsV2SchemaUpdate.match(/""key"":""attachments""[\s\S]*?""required"":true/),
    'FYBS 2.0 should require ticket attachments'
  )
  for (const detailField of [
    '""key"":""expenseAccount"",""label"":""费用科目"",""type"":""account"",""required"":true',
    '""key"":""auxDepartment"",""label"":""费用归属部门"",""type"":""auxiliary"",""auxType"":""department""',
    '""key"":""project"",""label"":""项目"",""type"":""auxiliary"",""auxType"":""project""',
    '""key"":""invoiceDate"",""label"":""发票日期"",""type"":""date""',
    '""key"":""invoiceAmount"",""label"":""发票金额"",""type"":""money""',
  ]) {
    assert.ok(fybsV2SchemaUpdate.includes(detailField), `FYBS 2.0 detail schema should include ${detailField}`)
  }
}

assertMatches(
  /UPDATE\s+\[CF流程节点\][\s\S]*N'主管审批'[\s\S]*""version"":2[\s\S]*""viewProfile""[\s\S]*""payeeAccountNo"":\{""access"":""masked""\}/s,
  'FYBS 2.0 should configure supervisor stage with masked payee account view profile'
)

assertMatches(
  /N'财务复核'[\s\S]*""default\.expenseAccount"":\{""access"":""required"",""required"":true\}[\s\S]*""paymentAccount"":\{""access"":""required"",""required"":true\}/s,
  'FYBS 2.0 should configure finance review to require expense account and payment account'
)

assertMatches(
  /N'付款确认'[\s\S]*""paymentAccount"":\{""access"":""readonly""\}[\s\S]*""payeeAccountNo"":\{""access"":""readonly""\}/s,
  'FYBS 2.0 should configure payment confirmation around readonly payment information'
)

assertMatches(
  /UPDATE\s+\[CF流程版本\][\s\S]*?""version"":2[\s\S]*?""components""[\s\S]*?""invoiceStatus""[\s\S]*?""budgetStatus""[\s\S]*?""loanOffset""[\s\S]*?""paymentInfo""[\s\S]*?""riskAlert""[\s\S]*?""routeDecision""[\s\S]*?""dynamicApprover""[\s\S]*?""previewSamples""[\s\S]*?WHERE\s+FID\s*=\s*1347;/s,
  'FYBS P4 should use card schema v2 with invoice, budget, loan offset, payment, risk, route and dynamic-approver components plus preview samples'
)

for (const [versionId, flowCode, sourceStage, defaultTarget] of [
  [1350, 'QKSQ', 'expense_request_approval', 'expense_request_budget'],
  [2261, 'JKSQ', 'loan_approval', 'loan_payment'],
  [1347, 'FYBS', 'expense_supervisor', 'expense_finance'],
]) {
  const routeBlockPattern = new RegExp(
    `INSERT INTO \\[CF节点流转规则\\][\\s\\S]*?${flowCode}[\\s\\S]*?${sourceStage}[\\s\\S]*?${defaultTarget}`,
    's'
  )
  assertMatches(routeBlockPattern, `${flowCode} should seed conditional route rules with stable source/target stage keys`)
  const defaultPattern = new RegExp(
    `\\[CF节点流转规则\\][\\s\\S]*?${flowCode}[\\s\\S]*?${sourceStage}[\\s\\S]*?F是否默认分支[\\s\\S]*?1`,
    's'
  )
  assertMatches(defaultPattern, `${flowCode} conditional route group should include a default branch`)
  assert.ok(source.includes(`""flowCode"":""${flowCode}""`), `${flowCode} route/policy config should keep an auditable flowCode marker`)
}

for (const [flowCode, policyKey, strategyType] of [
  ['QKSQ', 'qksq_amount_matrix', 'amountMatrix'],
  ['JKSQ', 'jksq_loan_amount_matrix', 'amountMatrix'],
  ['FYBS', 'fybs_large_amount_gm', 'amountMatrix'],
]) {
  const policyIndex = source.indexOf(policyKey)
  assert.ok(policyIndex >= 0, `${flowCode} should seed dynamic approval policy ${policyKey}`)
  const policyStart = source.indexOf('INSERT INTO [CF动态审批策略]', policyIndex)
  const policyEnd = source.indexOf(';', policyStart)
  const policyBlock = source.slice(policyStart, policyEnd)
  assert.ok(policyBlock.includes(strategyType), `${flowCode} dynamic approval policy should use ${strategyType}`)
  assert.ok(policyBlock.includes('F兜底JSON'), `${flowCode} dynamic approval policy should persist fallback JSON`)
  assert.ok(
    policyBlock.includes('flowAdmin'),
    `${flowCode} should seed a dynamic approval policy with ${strategyType} and flowAdmin fallback`
  )
}

for (const token of [
  'edge_fybs_zero_pay_skip_payment',
  'edge_fybs_request_reconcile',
  'edge_fybs_loan_offset_confirm',
  'actualPayAmount',
  'offsetLoanAmount',
  'requestRef',
  'loanRef',
]) {
  assertIncludes(token, `FYBS P4 should include ${token} rule marker`)
}

assertMatches(
  /VALUES\s*\(\s*5011,\s*1347,\s*1,\s*N'费用报销审批',\s*N'human',\s*N'card',\s*N'single'/s,
  'FYBS current version 1347 should have a human approval stage'
)

assertMatches(
  /VALUES\s*\(\s*5012,\s*1348,\s*1,\s*N'费用付款确认',\s*N'human',\s*N'card',\s*N'single'/s,
  'FYFK current version 1348 should have a human confirmation stage'
)

assertMatches(
  /N'凭证生成'[\s\S]*N'FYBS_VOUCHER'[\s\S]*N'published'/,
  'FYBS_VOUCHER flow definition should be seeded as a published CardFlow definition'
)

assertMatches(
  /VALUES\s*\(\s*2260,\s*0,[\s\S]*1,\s*2260,[\s\S]*1,\s*N'published'\s*\)/,
  'FYBS_VOUCHER should have a current published version'
)

assertMatches(
  /VALUES\s*\(\s*5013,\s*2260,\s*1,\s*N'凭证生成确认',\s*N'human',\s*N'card',\s*N'single'/s,
  'FYBS_VOUCHER current version should have a human confirmation stage'
)

assertMatches(
  /UPDATE\s+\[CF自动插件_规则\][\s\S]*\[FID\]\s*=\s*3003[\s\S]*STG费用支出记录/s,
  'Expense import ExcelInput rule 3003 should target STG费用支出记录'
)

{
  const expenseRuleUpdate = source.match(/UPDATE\s+\[CF自动插件_规则\][\s\S]*?WHERE\s+\[FID\]\s*=\s*3003;/)?.[0] ?? ''
  for (const dbColumn of ['F支出金额', 'F业务日期', 'F费用类别']) {
    assert.ok(
      expenseRuleUpdate.includes(`""dbColumn"":""${dbColumn}""`),
      `Expense import ExcelInput rule 3003 should map to ${dbColumn}`
    )
  }
}

assertMatches(
  /VALUES\s*\(\s*5014,\s*2256,\s*2,\s*N'费用支出质量分析',\s*N'auto',\s*N'batch',\s*N'single',\s*3,\s*NULL\s*\)/s,
  'PL_EXPENSE_REIMBURSE version 2256 should have a quality analysis stage'
)

assertMatches(
  /VALUES\s*\(\s*5015,\s*2256,\s*3,\s*N'费用支出自动凭证',\s*N'auto',\s*N'batch',\s*N'single',\s*5,\s*3005\s*\)/s,
  'PL_EXPENSE_REIMBURSE version 2256 should have an AutoVoucher stage with expense rule 3005'
)

assertMatches(
  /VALUES\s*\(\s*5016,\s*2256,\s*4,\s*N'批次汇总',\s*N'auto',\s*N'batch',\s*N'single',\s*14,\s*NULL\s*\)/s,
  'PL_EXPENSE_REIMBURSE version 2256 should have a batch summary stage'
)

assertMatches(
  /VALUES\s*\(\s*5017,\s*2256,\s*5,\s*N'确认通知',\s*N'human',\s*N'card',\s*N'single'/s,
  'PL_EXPENSE_REIMBURSE version 2256 should have a human confirmation stage'
)

assertMatches(
  /WHERE\s+FID\s*=\s*2256\s+AND\s+\(\[F卡片SchemaJSON\]\s+IS\s+NULL\s+OR\s+\[F卡片SchemaJSON\]\s*=\s*N''\)/s,
  'PL_EXPENSE_REIMBURSE schema update should target current version 2256'
)

assert.ok(
  /FTriggerConfigJson\s*==\s*null[\s\S]*!x\.FTriggerConfigJson\.Contains\("fileUpload"\)/.test(cardService),
  'Available CardFlow launch list should exclude file-upload-only flows'
)

assert.ok(
  /const\s+details\s*=\s*editDetailRows\.value\.map[\s\S]*JSON\.stringify[\s\S]*details,/.test(cardFlowPanel),
  'CardFlow fill payload should persist detail rows through UpdateCardRequest.details'
)

assert.ok(
  cardFlowPanel.includes('请至少添加一条费用明细') && /for\s*\(const\s+f\s+of\s+detailSchema\.value\)/.test(cardFlowPanel),
  'CardFlow fill validation should require and validate detail rows when detail schema exists'
)

assert.ok(
  /source\s*===\s*'detailSum'[\s\S]*editFormData\.value\[field\.key\]\s*=/.test(cardFlowPanel),
  'CardFlow fill validation should sync detailSum fields before validating the card schema'
)

assert.ok(
  cardFlowPanel.includes('useUserStore') &&
    cardFlowPanel.includes('applyAutoIdentityDefaults') &&
    cardFlowPanel.includes("field.dataSource === 'auto'") &&
    cardFlowPanel.includes('currentUserDepartmentDefault') &&
    cardFlowPanel.includes('fetchOrganizations'),
  'CardFlow fill mode should default department from current user organizations only when field dataSource is auto'
)

assert.ok(
  cardFlowPanel.includes('mainCardSchema') &&
    cardFlowPanel.includes('attachmentSchema') &&
    cardFlowPanel.indexOf('cf-panel__details') < cardFlowPanel.indexOf('cf-panel__attachments'),
  'Expense reimbursement attachments should render below detail rows instead of inside the top form'
)

assert.ok(
  cardFlowPanel.includes('cf-panel__expense-summary') &&
    cardFlowPanel.includes('cf-panel__amount-value') &&
    cardFlowPanel.includes('cf-panel__form-section'),
  'CardFlow panel should include expense-reimbursement oriented summary and section styling'
)

assert.ok(
  schemaFieldEditor.includes('identitySourceLabel') &&
    schemaFieldEditor.includes('当前用户') &&
    schemaFieldEditor.includes('当前用户所在部门') &&
    schemaFieldEditor.includes('currentUserDepartment') &&
    schemaFieldEditor.includes('用户选择'),
  'Schema field editor should let user/org fields choose between manual selection and current context defaults'
)

for (const type of ['account', 'auxiliary', 'bankAccount', 'voucherRef']) {
  assert.ok(
    schemaFieldEditor.includes(`value: '${type}'`),
    `Schema field editor should expose ${type} as a configurable CardFlow field type`
  )
}

assert.ok(
  schemaFieldEditor.includes('draft.auxType') &&
    schemaFieldEditor.includes('employee') &&
    schemaFieldEditor.includes('department') &&
    schemaFieldEditor.includes('project'),
  'Schema field editor should let auxiliary fields configure their auxiliary type'
)

assert.ok(
  cardDetailTable.includes("import AccountSelector from './fields/AccountSelector.vue'") &&
    cardDetailTable.includes("import AuxiliarySelector from './fields/AuxiliarySelector.vue'") &&
    cardDetailTable.includes("import BankAccountSelector from './fields/BankAccountSelector.vue'"),
  'CardDetailTable should import financial field selectors for detail rows'
)

assert.ok(
  /accountSetId\?:\s*number\s*\|\s*null/.test(cardDetailTable) &&
    /orgId\?:\s*number\s*\|\s*null/.test(cardDetailTable) &&
    /<AccountSelector[\s\S]*@update:model-value/.test(cardDetailTable) &&
    /<AuxiliarySelector[\s\S]*@update:model-value/.test(cardDetailTable) &&
    /<BankAccountSelector[\s\S]*@update:model-value/.test(cardDetailTable),
  'CardDetailTable should render account, auxiliary, and bankAccount editors in detail rows'
)

assert.ok(
  cardDetailTable.includes('formatAccountValue') &&
    cardDetailTable.includes('formatAuxiliaryValue') &&
    cardDetailTable.includes('formatBankAccountValue'),
  'CardDetailTable should format structured financial detail values instead of [object Object]'
)

assert.ok(
  /<CardDetailTable[\s\S]*:account-set-id="contextAccountSetId"[\s\S]*:org-id="contextOrgId"/.test(cardFlowPanel),
  'CardFlowPanel should pass finance context to detail row editors'
)

console.log('CardFlow expense reimbursement seed structure is usable.')
