/**
 * AutoVoucher 规则配置向导 — API 接口定义
 * 封装字段分析、DryRun 预演、规则 CRUD、规则校验等后端接口
 */
import { get, post, put } from '@/api/request'
import type { DryRunResult } from '@/api/cardflow'

// ==================== 字段分析相关类型 ====================

/** 单个字段值的覆盖率统计 */
export interface FieldValueStat {
  value: string
  count: number
  matched: boolean       // 是否已被规则覆盖
  matchedGroup?: string  // 匹配到的规则组名称
  matchedLineId?: string // 命中的分录行 ID（后端可选下发，用于自动展开）
}

/** 单层的覆盖率分析结果 */
export interface LayerAnalysis {
  layer: number
  layerName: string      // Layer1 / Layer2 / Layer3
  fieldName: string      // 对应的暂存表字段名
  totalRows: number      // 总行数
  coveredRows: number    // 已覆盖行数
  unmatchedValues: FieldValueStat[]  // 未匹配值列表
  matchedValues: FieldValueStat[]    // 已匹配值列表
  distinctKeywords?: Array<{ keyword: string; count: number; matchedGroup?: string }>
  unmatchedSamples?: string[]
}

/** 完整的字段分析结果 */
export interface FieldAnalysisResult {
  batchId: number
  ruleId?: number
  totalRows: number
  filteredTotalRows: number
  filterExcludedRows: number
  matchingLayers: {
    exactMatchField?: string
    categoryField?: string
    summaryField?: string
  }
  layers: LayerAnalysis[]
  summary: {
    matchedRows: number
    unmatchedRows: number
    coverageRate: number
  }
  unmatchedRowSample: Array<Record<string, any>>  // 未匹配行摘要样本
}

/** 字段分析请求体 */
export interface FieldAnalysisRequest {
  ruleId?: number
  configJson?: string
  layer1Field?: string
  layer2Field?: string
  layer3Field?: string
}

// ==================== 规则配置相关类型 ====================

/** 规则验证结果 */
export interface RuleValidationResult {
  valid: boolean
  errors: Array<{ path: string; message: string }>
  warnings: Array<{ path: string; message: string }>
}

/** 规则配置保存请求体 */
export interface RuleConfigSaveRequest {
  typeCode: string
  ruleName: string
  description?: string
  configJson: string
  status?: number
}

// ==================== API 函数 ====================

/** 获取字段覆盖率分析（GET 方式，使用已有规则配置） */
export function getFieldAnalysis(batchId: number, ruleId?: number) {
  return get<FieldAnalysisResult>(`/cardflow/auto-voucher/field-analysis/${batchId}`, ruleId != null ? { ruleId } : undefined)
}

/** 提交字段覆盖率分析（POST 方式，使用临时配置） */
export function postFieldAnalysis(batchId: number, body: FieldAnalysisRequest) {
  return post<FieldAnalysisResult>(`/cardflow/auto-voucher/field-analysis/${batchId}`, body)
}

/** 执行 DryRun 预演 */
export function postDryRun(body: { batchId: number; configJson?: string; ruleId?: number }) {
  return post<DryRunResult>('/cardflow/auto-voucher/dry-run', body)
}

/** 获取规则详情（静默模式，不弹全局错误） */
export function getRule(ruleId: number) {
  return get<any>(`/cardflow/auto-voucher/rules/${ruleId}`, undefined, { silent: true } as any)
}

/** 更新规则配置（含乐观锁） */
export function putRule(ruleId: number, configJson: string, etag?: string) {
  const parsed = JSON.parse(configJson)
  // 展平 V2 wrapper 格式：后端期望所有字段在同一层级
  const body = parsed.ruleConfig
    ? { version: parsed.version, accountSetId: parsed.accountSetId, ...parsed.ruleConfig }
    : parsed
  return put(`/cardflow/auto-voucher/rules/${ruleId}`,
    body,
    etag ? { headers: { 'If-Match': etag } } : undefined)
}

/** 校验规则配置 */
export function validateRule(ruleId: number, configJson: string) {
  return post<RuleValidationResult>(`/cardflow/auto-voucher/rules/${ruleId}/validate`, { configJson })
}
