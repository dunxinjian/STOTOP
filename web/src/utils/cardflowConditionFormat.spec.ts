import { describe, it, expect } from 'vitest'
import {
  formatCondition,
  formatConditionGroup,
  summarizeConditions,
  summarizeMembers,
} from '@/utils/cardflowConditionFormat'
import type { ConditionGroup } from '@/components/cardflow/ConditionBuilder.vue'

const fields = [
  { key: 'amount', label: '报销金额', type: 'money' },
  { key: 'expenseType', label: '费用类型', type: 'enum', options: [
    { label: '差旅', value: 'travel' },
    { label: '办公用品', value: 'office' },
  ] },
  { key: 'applicant', label: '发起人', type: 'user' },
]

describe('formatCondition 单条条件格式化', () => {
  it('金额 gte → 中文算子符号', () => {
    expect(formatCondition({ field: 'amount', operator: 'gte', value: 10000 }, fields))
      .toBe('报销金额 ≥ 10000')
  })

  it('enum in → 属于 [显示名]（存码显名）', () => {
    expect(formatCondition({ field: 'expenseType', operator: 'in', value: ['travel', 'office'] }, fields))
      .toBe('费用类型 属于 [差旅、办公用品]')
  })

  it('未知字段 key 降级为 key 本身', () => {
    expect(formatCondition({ field: 'ghost', operator: 'eq', value: 1 }, fields))
      .toBe('ghost 等于 1')
  })

  it('空值条件 → em dash', () => {
    expect(formatCondition({ field: 'amount', operator: 'gte', value: null }, fields))
      .toBe('报销金额 ≥ —')
  })

  it('between 区间双值', () => {
    expect(formatCondition({ field: 'amount', operator: 'between', value: [1000, 5000] }, fields))
      .toBe('报销金额 介于 1000 ~ 5000')
  })

  it('无值算子（empty）不渲染值', () => {
    expect(formatCondition({ field: 'amount', operator: 'empty', value: undefined }, fields))
      .toBe('报销金额 为空')
  })
})

describe('formatConditionGroup 组格式化（组内且/组间或）', () => {
  it('and 组用 且 连接', () => {
    const g: ConditionGroup = { logic: 'and', conditions: [
      { field: 'amount', operator: 'gte', value: 10000 },
      { field: 'expenseType', operator: 'in', value: ['travel'] },
    ] }
    expect(formatConditionGroup(g, fields)).toBe('报销金额 ≥ 10000 且 费用类型 属于 [差旅]')
  })

  it('or 顶层组用 或 连接嵌套 and 组（括号包裹）', () => {
    const g: ConditionGroup = { logic: 'or', conditions: [
      { logic: 'and', conditions: [{ field: 'amount', operator: 'gte', value: 10000 }] },
      { logic: 'and', conditions: [
        { field: 'amount', operator: 'lt', value: 100 },
        { field: 'expenseType', operator: 'eq', value: 'office' },
      ] },
    ] }
    expect(formatConditionGroup(g, fields))
      .toBe('报销金额 ≥ 10000 或 (报销金额 小于 100 且 费用类型 等于 办公用品)')
  })

  it('空组 → em dash', () => {
    expect(formatConditionGroup({ logic: 'and', conditions: [] }, fields)).toBe('—')
  })
})

describe('summarizeConditions 摘要（首条 + 等N条）', () => {
  it('单条直接展示', () => {
    const g: ConditionGroup = { logic: 'and', conditions: [
      { field: 'amount', operator: 'gte', value: 10000 },
    ] }
    expect(summarizeConditions(g, fields)).toEqual({ head: '报销金额 ≥ 10000', rest: 0 })
  })

  it('多条折叠为首条+计数（含嵌套组内条数）', () => {
    const g: ConditionGroup = { logic: 'and', conditions: [
      { field: 'amount', operator: 'gte', value: 10000 },
      { field: 'expenseType', operator: 'in', value: ['travel'] },
      { logic: 'and', conditions: [{ field: 'applicant', operator: 'eq', value: 'u1' }] },
    ] }
    expect(summarizeConditions(g, fields)).toEqual({ head: '报销金额 ≥ 10000', rest: 2 })
  })

  it('空组 → em dash 无计数', () => {
    expect(summarizeConditions({ logic: 'and', conditions: [] }, fields)).toEqual({ head: '—', rest: 0 })
  })
})

describe('summarizeMembers 多人摘要（前N + 等M人）', () => {
  const members = [
    { id: '1', name: '王芳' }, { id: '2', name: '李强' },
    { id: '3', name: '周敏' }, { id: '4', name: '赵磊' }, { id: '5', name: '孙倩' },
  ]

  it('不超上限全展示', () => {
    expect(summarizeMembers(members.slice(0, 2))).toEqual({ shown: ['王芳', '李强'], more: 0 })
  })

  it('超上限折叠（默认前2）', () => {
    expect(summarizeMembers(members)).toEqual({ shown: ['王芳', '李强'], more: 3 })
  })

  it('自定义上限', () => {
    expect(summarizeMembers(members, 3)).toEqual({ shown: ['王芳', '李强', '周敏'], more: 2 })
  })

  it('空列表 → 空 shown', () => {
    expect(summarizeMembers([])).toEqual({ shown: [], more: 0 })
  })
})
