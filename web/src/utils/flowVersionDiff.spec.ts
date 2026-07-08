import { describe, it, expect } from 'vitest'
import { diffFlowVersions, type VersionSnapshot } from '@/utils/flowVersionDiff'

const base: VersionSnapshot = {
  stages: [
    { id: 's1', name: '部门审批', type: 'manual' },
    { id: 's2', name: '自动凭证', type: 'auto' },
  ],
  routes: [
    { edgeKey: 'e1', routeName: '大额', conditionJson: '{"logic":"and","conditions":[{"field":"amount","operator":"gte","value":5000}]}', priority: 1, isDefault: false },
    { edgeKey: 'e2', routeName: '其他情况', conditionJson: null, priority: 2, isDefault: true },
  ],
  fields: [
    { key: 'amount', label: '报销金额', type: 'money', required: true },
    { key: 'reason', label: '事由', type: 'text', required: false },
  ],
}

function clone(v: VersionSnapshot): VersionSnapshot {
  return JSON.parse(JSON.stringify(v))
}

describe('diffFlowVersions', () => {
  it('无变化 → 空数组', () => {
    expect(diffFlowVersions(base, clone(base))).toEqual([])
  })

  it('新增节点', () => {
    const next = clone(base)
    next.stages.push({ id: 's3', name: '总监会签', type: 'manual' })
    const diff = diffFlowVersions(base, next)
    expect(diff).toContainEqual({ kind: 'add', scope: 'stage', label: '节点「总监会签」' })
  })

  it('删除节点', () => {
    const next = clone(base)
    next.stages = next.stages.filter((s) => s.id !== 's2')
    const diff = diffFlowVersions(base, next)
    expect(diff).toContainEqual({ kind: 'remove', scope: 'stage', label: '节点「自动凭证」' })
  })

  it('节点改名+改类型 → modify 内联', () => {
    const next = clone(base)
    next.stages[0] = { id: 's1', name: '部门主管审批', type: 'auto' }
    const diff = diffFlowVersions(base, next).find((d) => d.scope === 'stage' && d.kind === 'modify')
    expect(diff?.detail).toContain('名称 部门审批 → 部门主管审批')
    expect(diff?.detail).toContain('类型 人工 → 自动')
  })

  it('条件值级 diff：阈值变化「旧 → 新」内联', () => {
    const next = clone(base)
    next.routes[0].conditionJson = '{"logic":"and","conditions":[{"field":"amount","operator":"gte","value":10000}]}'
    const diff = diffFlowVersions(base, next).find((d) => d.scope === 'route' && d.kind === 'modify')
    expect(diff?.detail).toBe('amount gte 5000 → amount gte 10000')
  })

  it('分支优先级变化', () => {
    const next = clone(base)
    next.routes[0].priority = 3
    const diff = diffFlowVersions(base, next).find((d) => d.scope === 'route' && d.kind === 'modify')
    expect(diff?.detail).toContain('优先级 1 → 3')
  })

  it('新增/删除分支', () => {
    const next = clone(base)
    next.routes.push({ edgeKey: 'e3', routeName: '中额', conditionJson: '{}', priority: 2, isDefault: false })
    const diff = diffFlowVersions(base, next)
    expect(diff).toContainEqual({ kind: 'add', scope: 'route', label: '分支「中额」' })
  })

  it('字段改必填/改类型/删除', () => {
    const next = clone(base)
    next.fields[1] = { key: 'reason', label: '事由', type: 'text', required: true }
    next.fields = next.fields.filter((f) => f.key !== 'amount')
    const diff = diffFlowVersions(base, next)
    expect(diff.find((d) => d.scope === 'field' && d.kind === 'modify')?.detail).toContain('改为必填')
    expect(diff).toContainEqual({ kind: 'remove', scope: 'field', label: '字段「报销金额」' })
  })
})
