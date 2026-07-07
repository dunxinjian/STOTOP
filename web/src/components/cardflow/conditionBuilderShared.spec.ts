import { describe, it, expect } from 'vitest'
import {
  OPERATORS_BY_TYPE,
  OPERATOR_LABELS,
  VALUELESS_OPERATORS,
  SYSTEM_CONDITION_FIELDS,
  getOperatorsForType,
  isTypeConditionable,
  getDisabledReason,
  sanitizeConditionGroup,
} from '@/components/cardflow/conditionBuilderShared'
import type { ConditionGroup } from '@/components/cardflow/ConditionBuilder.vue'

/** 引擎（ConditionRuleEvaluator.EvaluateItem）实现的算子全集——UI 只允许出现这些 */
const ENGINE_SUPPORTED = new Set([
  'eq', 'neq', 'contains', 'startsWith', 'in', 'inOrgChain', 'between',
  'gt', 'gte', 'lt', 'lte', 'exists', 'notExists', 'empty', 'notEmpty',
])

describe('OPERATORS_BY_TYPE 算子-类型映射锁定', () => {
  it('金额/数字：eq neq gt gte lt lte between + 存在性', () => {
    const expected = ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'empty', 'notEmpty']
    expect(OPERATORS_BY_TYPE.money.map((o) => o.value)).toEqual(expected)
    expect(OPERATORS_BY_TYPE.number.map((o) => o.value)).toEqual(expected)
  })

  it('enum：eq neq in + 存在性（引擎无 notIn，绝不出现「不属于」）', () => {
    const values = OPERATORS_BY_TYPE.enum.map((o) => o.value)
    expect(values).toEqual(['eq', 'neq', 'in', 'empty', 'notEmpty'])
    expect(values).not.toContain('notIn')
  })

  it('text：eq neq contains startsWith + 存在性', () => {
    expect(OPERATORS_BY_TYPE.text.map((o) => o.value))
      .toEqual(['eq', 'neq', 'contains', 'startsWith', 'empty', 'notEmpty'])
  })

  it('user：无 inOrgChain（引擎 IsInOrgChain 语义是发起人组织链，对人员字段错位）', () => {
    const values = OPERATORS_BY_TYPE.user.map((o) => o.value)
    expect(values).toEqual(['eq', 'neq', 'empty', 'notEmpty'])
    expect(values).not.toContain('inOrgChain')
  })

  it('org：含 inOrgChain', () => {
    expect(OPERATORS_BY_TYPE.org.map((o) => o.value))
      .toEqual(['eq', 'neq', 'inOrgChain', 'empty', 'notEmpty'])
  })

  it('date：eq neq gt(晚于) lt(早于) between + 存在性', () => {
    const values = OPERATORS_BY_TYPE.date.map((o) => o.value)
    expect(values).toEqual(['eq', 'neq', 'gt', 'lt', 'between', 'empty', 'notEmpty'])
    const labels = Object.fromEntries(OPERATORS_BY_TYPE.date.map((o) => [o.value, o.label]))
    expect(labels.gt).toBe('晚于')
    expect(labels.lt).toBe('早于')
  })

  it('file：仅 exists notExists', () => {
    expect(OPERATORS_BY_TYPE.file.map((o) => o.value)).toEqual(['exists', 'notExists'])
  })

  it('全表所有算子都在引擎支持集内（防未实现算子渗入 UI）', () => {
    for (const [type, operators] of Object.entries(OPERATORS_BY_TYPE)) {
      for (const op of operators) {
        expect(ENGINE_SUPPORTED.has(op.value), `${type}.${op.value} 引擎未实现`).toBe(true)
        expect(OPERATOR_LABELS[op.value], `${type}.${op.value} 缺展示词`).toBeTruthy()
      }
    }
  })

  it('无值算子集合与映射表一致', () => {
    expect([...VALUELESS_OPERATORS].sort())
      .toEqual(['empty', 'exists', 'notEmpty', 'notExists'].sort())
  })
})

describe('字段类型可用性裁定', () => {
  it('可判定类型均可作条件', () => {
    for (const type of ['text', 'money', 'number', 'enum', 'date', 'user', 'org', 'file']) {
      expect(isTypeConditionable(type), type).toBe(true)
    }
  })

  it('引用/财务类字段不可作条件且给出原因', () => {
    for (const type of ['cardRef', 'account', 'auxiliary', 'bankAccount', 'voucherRef']) {
      expect(isTypeConditionable(type), type).toBe(false)
      expect(getDisabledReason(type)).toContain('不可作条件')
    }
    expect(getOperatorsForType('cardRef')).toEqual([])
  })
})

describe('SYSTEM_CONDITION_FIELDS 系统字段（键名对齐 ConditionContextFactory）', () => {
  it('发起人 initiator.id (user) / 发起组织 initiatorOrg.id (org)；无 createdTime（上下文不存在）', () => {
    expect(SYSTEM_CONDITION_FIELDS.map((f) => f.key)).toEqual(['initiator.id', 'initiatorOrg.id'])
    expect(SYSTEM_CONDITION_FIELDS[0].type).toBe('user')
    expect(SYSTEM_CONDITION_FIELDS[1].type).toBe('org')
  })
})

describe('sanitizeConditionGroup 试算前清洗', () => {
  it('剔除未选字段/算子的半成品行', () => {
    const group: ConditionGroup = {
      logic: 'and',
      conditions: [
        { field: 'amount', operator: 'gte', value: 5000 },
        { field: '', operator: '', value: null },
        { field: 'type', operator: '', value: null },
      ],
    }
    const result = sanitizeConditionGroup(group)
    expect(result).toEqual({
      logic: 'and',
      conditions: [{ field: 'amount', operator: 'gte', value: 5000 }],
    })
  })

  it('嵌套空组被整体剔除；全空返回 null', () => {
    const group: ConditionGroup = {
      logic: 'or',
      conditions: [
        { logic: 'and', conditions: [{ field: '', operator: '', value: null }] },
      ],
    }
    expect(sanitizeConditionGroup(group)).toBeNull()
  })

  it('嵌套有效组保留结构', () => {
    const group: ConditionGroup = {
      logic: 'or',
      conditions: [
        { logic: 'and', conditions: [{ field: 'a', operator: 'eq', value: 1 }] },
        { field: 'b', operator: 'notEmpty', value: null },
      ],
    }
    expect(sanitizeConditionGroup(group)).toEqual(group)
  })
})
