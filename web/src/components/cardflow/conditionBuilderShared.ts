import type { ConditionGroup, ConditionItem, FieldOption } from './ConditionBuilder.vue'

/**
 * ConditionBuilder 的纯逻辑与常量单一真源（M3-2/M3-3）。
 * 仅 import type（运行时无环）：与 stageDefinitionShared.ts 同一模式。
 *
 * ===== 引擎算子核实结果（真源 ConditionRuleEvaluator.EvaluateItem / NormalizeOperator）=====
 * 引擎实现的算子全集：
 *   exists / notExists / empty / notEmpty（存在性，四类均可用于任意字段）
 *   eq / neq / contains / startsWith(→startswith) / in / inOrgChain(→inorgchain)
 *   between（数字与日期双边界）/ gt / gte / lt / lte（数字与日期）
 * 明确不支持（EvaluateItem 兜底 `_ => false`，绝不暴露到 UI）：
 *   notIn —— mock B4 的「不属于」在引擎落地前不提供；
 *   user + inOrgChain —— IsInOrgChain 校验的是"发起人组织链"而非人员字段所属部门，
 *     对 user 字段语义错位，仅对 org 字段暴露。
 */

export interface OperatorOption {
  value: string
  label: string
}

/** 算子中文展示词（与 utils/cardflowConditionFormat.OPERATOR_LABELS 同口径） */
export const OPERATOR_LABELS: Record<string, string> = {
  eq: '等于',
  neq: '不等于',
  contains: '包含',
  startsWith: '开头是',
  gt: '大于',
  gte: '≥',
  lt: '小于',
  lte: '≤',
  in: '属于',
  between: '介于',
  exists: '有附件',
  notExists: '无附件',
  empty: '为空',
  notEmpty: '不为空',
  inOrgChain: '属于组织链',
}

function ops(...values: string[]): OperatorOption[] {
  return values.map((value) => ({ value, label: OPERATOR_LABELS[value] || value }))
}

/**
 * 字段类型 → 可用算子（只含引擎已实现算子；date 的 gt/lt 展示为 晚于/早于）。
 * 该表被 vitest 锁定，改动前先核实 ConditionRuleEvaluator。
 */
export const OPERATORS_BY_TYPE: Record<string, OperatorOption[]> = {
  text: ops('eq', 'neq', 'contains', 'startsWith', 'empty', 'notEmpty'),
  money: ops('eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'empty', 'notEmpty'),
  number: ops('eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'empty', 'notEmpty'),
  enum: ops('eq', 'neq', 'in', 'empty', 'notEmpty'),
  date: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'gt', label: '晚于' },
    { value: 'lt', label: '早于' },
    { value: 'between', label: '介于' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  user: ops('eq', 'neq', 'empty', 'notEmpty'),
  org: ops('eq', 'neq', 'inOrgChain', 'empty', 'notEmpty'),
  file: ops('exists', 'notExists'),
}

/** 无值算子：选中后不渲染值输入 */
export const VALUELESS_OPERATORS = new Set(['exists', 'notExists', 'empty', 'notEmpty'])

/** 字段类型徽标文案（C8 下拉右侧小 tag） */
export const CONDITION_TYPE_LABELS: Record<string, string> = {
  text: '文本',
  money: '金额',
  number: '数字',
  enum: '单选',
  date: '日期',
  user: '人员',
  org: '组织',
  file: '附件',
  cardRef: '卡片引用',
  account: '会计科目',
  auxiliary: '辅助核算',
  bankAccount: '银行账户',
  voucherRef: '凭证引用',
}

/** 字段类型→emoji 图标前缀（mock C8 字段下拉每行左侧类型图标） */
export const CONDITION_TYPE_ICONS: Record<string, string> = {
  text: '📝',
  money: '💰',
  number: '#️⃣',
  enum: '◉',
  date: '📅',
  user: '👤',
  org: '🏢',
  file: '📎',
}

/** 不可作路由条件的类型 → 禁用原因（C8 灰显说明；按引擎算子支持裁定） */
const NON_CONDITION_TYPE_REASONS: Record<string, string> = {
  cardRef: '卡片引用不可作条件',
  account: '会计科目不可作条件',
  auxiliary: '辅助核算不可作条件',
  bankAccount: '银行账户不可作条件',
  voucherRef: '凭证引用不可作条件',
}

/**
 * 系统字段（C8 第二组）。键名核实自 ConditionContextFactory.Build：
 *   initiator.id（发起人用户ID，user）/ initiatorOrg.id（发起组织ID，org）。
 * mock 中的「发起时间 createdTime」在 ConditionEvaluationContext 中不存在
 * （ConditionContextInputs 无创建时间入参），不造假字段，引擎支持后再补。
 */
export const SYSTEM_CONDITION_FIELDS: FieldOption[] = [
  { key: 'initiator.id', label: '发起人', type: 'user' },
  { key: 'initiatorOrg.id', label: '发起组织', type: 'org' },
]

export function getOperatorsForType(type: string): OperatorOption[] {
  return OPERATORS_BY_TYPE[type] || []
}

export function isTypeConditionable(type: string): boolean {
  return Boolean(OPERATORS_BY_TYPE[type])
}

export function getDisabledReason(type: string): string {
  return NON_CONDITION_TYPE_REASONS[type] || '该字段类型不可作条件'
}

export function isGroupNode(node: ConditionItem | ConditionGroup): node is ConditionGroup {
  return typeof node === 'object' && node !== null && 'logic' in node && 'conditions' in node
}

/**
 * 试算前清洗：剔除未选字段/算子的半成品行与空组；全空返回 null（跳过试算）。
 * 纯函数，vitest 锁定。
 */
export function sanitizeConditionGroup(group: ConditionGroup): ConditionGroup | null {
  const conditions: (ConditionItem | ConditionGroup)[] = []
  for (const child of group.conditions) {
    if (isGroupNode(child)) {
      const inner = sanitizeConditionGroup(child)
      if (inner) conditions.push(inner)
    } else if (child.field && child.operator) {
      conditions.push(child)
    }
  }
  return conditions.length ? { logic: group.logic, conditions } : null
}
