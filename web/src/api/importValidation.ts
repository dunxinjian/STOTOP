import { get, post } from './request'

export enum ValidationDomain {
  Voucher = 1,
  Pricing = 2,
  Cost = 3,
}

export enum ValidationAttribution {
  None = 0,
  ImportData = 1,
  Configuration = 2,
  CalculationLogic = 3,
  Persistence = 4,
}

export enum ValidationSeverity {
  Low = 1,
  Medium = 2,
  High = 3,
  Blocker = 4,
}

export type ImportValidationMode = 'sample' | 'errorsOnly' | 'allLimited'

export interface ImportValidationRunRequest {
  domains: ValidationDomain[]
  mode: ImportValidationMode
  sampleSize: number
  includeEvidence: boolean
  tolerance: number
}

export interface ImportValidationSummaryDto {
  batchId: number
  batchNo?: string
  flowName?: string
  targetTable?: string
  totalRows: number
  /** 批次状态机：0=解析中, 1=已暂存, 2=质检中, 3=已创建卡片, 4=处理中, 5=已完成 */
  batchStatus: number
  batchStatusText: string
  /** 批次节点链仍在执行中（计费/成本结果可能尚未写入） */
  isBatchRunning: boolean
  isRevoked: boolean
  currentNodeName?: string
  progressPercent?: number
  batchErrorMessage?: string
  importStartTime?: string
  importEndTime?: string
  existingResultCounts: Record<string, number>
}

export interface ImportValidationReportDto {
  batchId: number
  batchStatusText: string
  /** 验证执行时批次仍在运行（结果可能不完整） */
  isBatchRunning: boolean
  generatedAt: string
  checkedRows: number
  sampleRows: ImportValidationSampleRowDto[]
  findings: ImportValidationFindingDto[]
  attributionCounts: Record<string, number>
  severityCounts: Record<string, number>
}

export interface ImportValidationSampleRowDto {
  sourceRowId?: number
  businessKey?: string
  waybillNo?: string
  sourceFields: Record<string, unknown>
  results: ImportValidationSampleResultDto[]
  findings: ImportValidationFindingDto[]
}

export interface ImportValidationSampleResultDto {
  domain: ValidationDomain | string
  label: string
  originalValue?: unknown
  systemValue?: unknown
  expectedValue?: unknown
  difference?: number
  status: string
  message: string
  costItems: ImportValidationSampleCostItemDto[]
  /** 凭证域行级核验明细（原始字段→规则配置→凭证结果三段证据链） */
  voucherDetail?: ImportValidationVoucherDetailDto | null
  persistedResult: Record<string, unknown>
  traceSteps: CalculationTraceStepDto[]
}

export interface ImportValidationVoucherDetailDto {
  passedFilter: boolean
  matched: boolean
  matchedLayer?: number
  matchReason?: string
  routedButNoOutput: boolean
  ruleGroupName?: string
  amountAggregation?: string
  sourceFieldValues: Record<string, unknown>
  ruleLines: ImportValidationVoucherRuleLineDto[]
  draftEntries: ImportValidationVoucherEntryDto[]
  draftDebitTotal: number
  draftCreditTotal: number
  draftBalanced?: boolean | null
  businessKey?: string
  actualVoucherId?: number
  actualVoucherNo?: string
  actualEntries: ImportValidationVoucherEntryDto[]
  actualDebitTotal?: number
  actualCreditTotal?: number
  actualBalanced?: boolean | null
  issues: string[]
}

export interface ImportValidationVoucherRuleLineDto {
  lineNo: number
  direction: string
  accountText: string
  amountField?: string
  summaryTemplate?: string
  conditionText?: string
  auxiliaryText?: string
  enabled: boolean
}

export interface ImportValidationVoucherEntryDto {
  lineNo: number
  direction: string
  accountId?: number
  accountCode?: string
  accountName?: string
  amount?: number
  amountField?: string
  summary?: string
  auxiliaryText?: string
  issue?: string
}

export interface ImportValidationSampleCostItemDto {
  costItemId: number
  costItemName: string
  amount: number
}

export interface ImportValidationFindingDto {
  domain: ValidationDomain | string
  attribution: ValidationAttribution | string
  severity: ValidationSeverity | string
  confidence: number
  affectedRows: number
  sourceRowId?: number
  businessKey?: string
  waybillNo?: string
  voucherId?: number
  title: string
  message: string
  systemValue?: number
  expectedValue?: number
  difference?: number
  suggestedAction: string
  evidence: ImportValidationEvidenceDto
}

export interface ImportValidationEvidenceDto {
  sourceFields: Record<string, unknown>
  matchedConfigurations: string[]
  configurationIssues: string[]
  traceSteps: CalculationTraceStepDto[]
  persistedResult: Record<string, unknown>
}

export interface CalculationTraceStepDto {
  step: string
  description: string
  inputValue?: number
  outputValue?: number
  formula?: string
}

export function getImportValidationSummary(batchId: number) {
  return get<ImportValidationSummaryDto>(`/cardflow/import-validation/batches/${batchId}/summary`)
}

export function runImportValidation(batchId: number, data: ImportValidationRunRequest) {
  // 验证 run 需跑凭证/价格/成本三域分析 + 逐行解释，重批次下远超全局 15s 默认超时，单独放宽到 5 分钟
  return post<ImportValidationReportDto>(`/cardflow/import-validation/batches/${batchId}/run`, data, {
    timeout: 300000,
  })
}

export function getImportValidationRowDetail(batchId: number, rowId: number) {
  return get<ImportValidationFindingDto>(`/cardflow/import-validation/batches/${batchId}/rows/${rowId}`)
}
