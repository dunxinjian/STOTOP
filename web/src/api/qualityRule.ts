import { get, post, put, del } from './request'

// ==================== 质量规则 API ====================

export interface QualityRuleDto {
  id: number
  ruleName: string
  ruleCode: string
  pipelineId: number | null
  pipelineName: string | null
  targetTable: string | null
  orgId: number | null
  ruleLevel: string       // Field / Row / Batch
  checkType: string       // NotNull / Format / Range / Expression / SqlCondition
  targetField: string | null
  parameters: string | null
  errorTypeCode: string | null
  severityLevel: string   // Error / Warning
  qualityDimension: string | null
  messageTemplate: string | null
  suggestedFix: string | null
  isBlocking: boolean
  isEnabled: boolean
  sortOrder: number
  createTime: string
  updateTime: string | null
}

export interface QualityRuleCreateDto {
  ruleName: string
  ruleCode: string
  pipelineId?: number | null
  targetTable?: string | null
  orgId?: number | null
  ruleLevel: string
  checkType: string
  targetField?: string | null
  parameters?: string | null
  errorTypeCode?: string | null
  severityLevel: string
  qualityDimension?: string | null
  messageTemplate?: string | null
  suggestedFix?: string | null
  isBlocking?: boolean
  isEnabled?: boolean
  sortOrder?: number
}

export interface QualityRuleTestDto {
  ruleLevel: string
  checkType: string
  targetField?: string | null
  parameters?: string | null
  targetTable?: string | null
  testData?: string | null
}

export interface QualityRuleTestResult {
  success: boolean
  passed: boolean
  message: string | null
  errors: string[]
}

/** 查询质量规则列表 */
export const getQualityRules = (params?: {
  pipelineId?: number
  targetTable?: string
  orgId?: number
  isEnabled?: boolean
}) => get<QualityRuleDto[]>('/cardflow/quality-rules', params)

/** 获取质量规则详情 */
export const getQualityRule = (id: number) =>
  get<QualityRuleDto>(`/cardflow/quality-rules/${id}`)

/** 创建质量规则 */
export const createQualityRule = (data: QualityRuleCreateDto) =>
  post('/cardflow/quality-rules', data)

/** 更新质量规则 */
export const updateQualityRule = (id: number, data: Partial<QualityRuleCreateDto>) =>
  put(`/cardflow/quality-rules/${id}`, data)

/** 删除质量规则 */
export const deleteQualityRule = (id: number) =>
  del(`/cardflow/quality-rules/${id}`)

/** 测试质量规则 */
export const testQualityRule = (data: QualityRuleTestDto) =>
  post<QualityRuleTestResult>('/cardflow/quality-rules/test', data)
