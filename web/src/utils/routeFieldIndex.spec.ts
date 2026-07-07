import { describe, it, expect } from 'vitest'
import { buildRouteFieldIndex } from '@/utils/routeFieldIndex'
import type { StageRouteRuleRequest } from '@/types/cardflow'

function r(edgeKey: string, conditionJson: string | null, opts: Partial<StageRouteRuleRequest> = {}): StageRouteRuleRequest {
  return {
    edgeKey,
    fromStageKey: opts.fromStageKey ?? 'a',
    toStageKey: opts.toStageKey ?? 'b',
    routeName: opts.routeName ?? edgeKey,
    conditionJson,
    priority: opts.priority ?? 1,
    isDefault: opts.isDefault ?? false,
    status: opts.status ?? 'active',
  }
}

describe('buildRouteFieldIndex 路由引用索引 (M4-1)', () => {
  it('扁平条件组：字段与枚举选项均入索引', () => {
    const cond = JSON.stringify({
      logic: 'and',
      conditions: [
        { field: 'amount', operator: 'gte', value: 10000 },
        { field: 'expenseType', operator: 'in', value: ['travel', 'office'] },
      ],
    })
    const idx = buildRouteFieldIndex([r('e1', cond)])
    expect(idx.fields.get('amount')).toEqual(['e1'])
    expect(idx.fields.get('expenseType')).toEqual(['e1'])
    expect(idx.options.get('expenseType.travel')).toEqual(['e1'])
    expect(idx.options.get('expenseType.office')).toEqual(['e1'])
  })

  it('嵌套条件组递归收集', () => {
    const cond = JSON.stringify({
      logic: 'or',
      conditions: [
        { logic: 'and', conditions: [{ field: 'amount', operator: 'gt', value: 1 }] },
        { logic: 'and', conditions: [{ field: 'org', operator: 'eq', value: 5 }] },
      ],
    })
    const idx = buildRouteFieldIndex([r('e1', cond)])
    expect(idx.fields.get('amount')).toEqual(['e1'])
    expect(idx.fields.get('org')).toEqual(['e1'])
  })

  it('多条 route 引用同字段：edgeKey 聚合去重', () => {
    const cond1 = JSON.stringify({ logic: 'and', conditions: [{ field: 'amount', operator: 'gte', value: 1 }] })
    const cond2 = JSON.stringify({ logic: 'and', conditions: [
      { field: 'amount', operator: 'lt', value: 1 },
      { field: 'amount', operator: 'gt', value: 0 },
    ] })
    const idx = buildRouteFieldIndex([r('e1', cond1), r('e2', cond2)])
    expect(idx.fields.get('amount')).toEqual(['e1', 'e2'])
  })

  it('单值 eq 条件的枚举值也入选项索引（选项删除保护要覆盖 eq）', () => {
    const cond = JSON.stringify({ logic: 'and', conditions: [{ field: 'type', operator: 'eq', value: 'travel' }] })
    const idx = buildRouteFieldIndex([r('e1', cond)])
    expect(idx.options.get('type.travel')).toEqual(['e1'])
  })

  it('数值/布尔值不入选项索引（只索引字符串候选值）', () => {
    const cond = JSON.stringify({ logic: 'and', conditions: [{ field: 'amount', operator: 'eq', value: 100 }] })
    const idx = buildRouteFieldIndex([r('e1', cond)])
    expect(idx.options.size).toBe(0)
  })

  it('disabled route 不参与索引；非法 JSON 容错跳过', () => {
    const cond = JSON.stringify({ logic: 'and', conditions: [{ field: 'amount', operator: 'gt', value: 1 }] })
    const idx = buildRouteFieldIndex([
      r('e1', cond, { status: 'disabled' }),
      r('e2', '{broken json'),
      r('e3', null),
    ])
    expect(idx.fields.size).toBe(0)
  })
})
