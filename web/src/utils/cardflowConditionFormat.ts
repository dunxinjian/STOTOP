/**
 * 条件与成员的展示格式化单一真源（设计 D3/M0-2）。
 * 消费方：ConditionSummary / MemberSummary / 字段权限矩阵 / 版本 diff（M6-1）。
 * 空值统一 em dash「—」；enum 存码显名。
 */
import type { ConditionGroup, ConditionItem, FieldOption } from '@/components/cardflow/ConditionBuilder.vue'
import { normalizeOptionList } from '@/utils/cardflowFieldFormat'

const EM_DASH = '—'

/** 与 ConditionBuilder.operatorLabelMap 同源的展示词（gte/lte 用符号，介于/属于用中文） */
const OPERATOR_LABELS: Record<string, string> = {
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

/** 无值算子：只渲染「字段 算子」 */
const VALUELESS_OPERATORS = new Set(['exists', 'notExists', 'empty', 'notEmpty'])

function isGroup(item: ConditionItem | ConditionGroup): item is ConditionGroup {
  return typeof item === 'object' && item !== null && 'logic' in item && 'conditions' in item
}

function fieldLabel(key: string, fields: FieldOption[]): string {
  return fields.find((f) => f.key === key)?.label || key
}

/** enum 值存码显名；数组值转显示名列表 */
function displayValue(item: ConditionItem, fields: FieldOption[]): string {
  const { value } = item
  if (value === null || value === undefined || value === '') return EM_DASH
  const field = fields.find((f) => f.key === item.field)
  const options = normalizeOptionList(field?.options)
  const nameOf = (v: unknown): string => {
    const hit = options.find((o) => o.value === v)
    return hit ? hit.label : String(v)
  }
  if (Array.isArray(value)) {
    if (value.length === 0) return EM_DASH
    if (item.operator === 'between' && value.length === 2) {
      return `${nameOf(value[0])} ~ ${nameOf(value[1])}`
    }
    return `[${value.map(nameOf).join('、')}]`
  }
  return nameOf(value)
}

/** 单条条件 → 「字段 算子 值」 */
export function formatCondition(item: ConditionItem, fields: FieldOption[]): string {
  const label = fieldLabel(item.field, fields)
  const op = OPERATOR_LABELS[item.operator] || item.operator
  if (VALUELESS_OPERATORS.has(item.operator)) return `${label} ${op}`
  return `${label} ${op} ${displayValue(item, fields)}`
}

/** 组 → 组内按 logic 连接；嵌套子组含多条时括号包裹 */
export function formatConditionGroup(group: ConditionGroup, fields: FieldOption[]): string {
  if (!group.conditions.length) return EM_DASH
  const joint = group.logic === 'or' ? ' 或 ' : ' 且 '
  const parts = group.conditions
    .map((c) => {
      if (!isGroup(c)) return formatCondition(c, fields)
      const inner = formatConditionGroup(c, fields)
      return c.conditions.length > 1 ? `(${inner})` : inner
    })
    .filter((s) => s !== EM_DASH)
  return parts.length ? parts.join(joint) : EM_DASH
}

/** 叶子条件总数（嵌套组展开计数） */
function countLeaves(group: ConditionGroup): number {
  return group.conditions.reduce<number>(
    (acc, c) => acc + (isGroup(c) ? countLeaves(c) : 1),
    0,
  )
}

/** 摘要：首条 + 等 N 条（设计 D3 条件表达式截断规则） */
export function summarizeConditions(
  group: ConditionGroup,
  fields: FieldOption[],
): { head: string; rest: number } {
  const total = countLeaves(group)
  if (total === 0) return { head: EM_DASH, rest: 0 }
  const first = findFirstLeaf(group)
  return { head: first ? formatCondition(first, fields) : EM_DASH, rest: total - 1 }
}

function findFirstLeaf(group: ConditionGroup): ConditionItem | null {
  for (const c of group.conditions) {
    if (!isGroup(c)) return c
    const inner = findFirstLeaf(c)
    if (inner) return inner
  }
  return null
}

/** 多人摘要：前 max 人 + 等 M 人（设计 D3 处理人多人规则） */
export function summarizeMembers(
  members: Array<{ id: string; name: string }>,
  max = 2,
): { shown: string[]; more: number } {
  if (members.length <= max) return { shown: members.map((m) => m.name), more: 0 }
  return { shown: members.slice(0, max).map((m) => m.name), more: members.length - max }
}
