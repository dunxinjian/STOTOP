import { get, put, post, del } from './request'

export interface CodeRuleDto {
  id: number
  ruleCode: string
  ruleName: string
  businessEntity: string
  codeField: string
  prefix: string | null
  dateFormat: string | null
  seqLength: number
  separator: string | null
  resetPeriod: string
  orgIsolation: boolean
  enabled: boolean
  description: string | null
}

export interface CodeRuleUpdateDto {
  prefix?: string | null
  dateFormat?: string | null
  seqLength?: number
  separator?: string | null
  resetPeriod?: string
  orgIsolation?: boolean
  enabled?: boolean
  description?: string | null
}

/** 获取编码规则列表 */
export function getCodeRules() {
  return get<CodeRuleDto[]>('/code-rules')
}

/** 获取单条编码规则 */
export function getCodeRule(id: number) {
  return get<CodeRuleDto>(`/code-rules/${id}`)
}

/** 更新编码规则 */
export function updateCodeRule(id: number, data: CodeRuleUpdateDto) {
  return put<CodeRuleDto>(`/code-rules/${id}`, data)
}

/** 预览编码 */
export function previewCodeRule(id: number) {
  return get<string>(`/code-rules/${id}/preview`)
}

export interface CodeRuleCreateDto {
  ruleCode: string
  ruleName: string
  businessEntity: string
  codeField: string
  prefix?: string | null
  dateFormat?: string | null
  seqLength?: number
  separator?: string | null
  resetPeriod?: string
  orgIsolation?: boolean
  description?: string | null
}

/** 新增编码规则 */
export function createCodeRule(data: CodeRuleCreateDto) {
  return post<CodeRuleDto>('/code-rules', data)
}

/** 删除编码规则 */
export function deleteCodeRule(id: number) {
  return del('/code-rules/' + id)
}
