import { describe, it, expect } from 'vitest'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import type { SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import { runRuleHealthChecks, validatePublishConfig, type HealthItem } from './cardflowDiagnostics'

/**
 * cardflowDiagnostics 诊断"点击直达现场"门禁：校验每条诊断携带正确的跳转 target
 * （node=stage.id / edge=route.edgeKey；组件类与引用已删项无 target），供设计器选中节点/边并开抽屉。
 */

const mkStage = (o: Partial<StageDefinition>): StageDefinition =>
  ({ id: 's1', name: '节点', type: 'manual', sortOrder: 1, ...o })
const mkRoute = (o: Partial<StageRouteRuleRequest>): StageRouteRuleRequest =>
  ({ edgeKey: 'e1', fromStageKey: 's1', toStageKey: 's2', routeName: '边', conditionJson: null,
    priority: 1, isDefault: false, status: 'active', failurePolicyJson: null, ...o } as StageRouteRuleRequest)
const find = (items: HealthItem[], title: string) => items.find(i => i.title === title)
const cond = (op: string, v: number) =>
  JSON.stringify({ logic: 'and', conditions: [{ field: 'amount', operator: op, value: v }] })

describe('runRuleHealthChecks —— 诊断 target', () => {
  it('处理人策略缺失 → target 指向该节点', () => {
    const items = runRuleHealthChecks({
      stages: [mkStage({ id: 'n1', type: 'manual', assigneeStrategy: undefined })],
      routes: [], dynamicPolicies: [], fields: [],
    })
    expect(find(items, '处理人策略缺失')?.target).toEqual({ kind: 'node', key: 'n1' })
  })

  it('流转规则引用失效 → target 指向该边（边仍存在，可跳转去修/删）', () => {
    const items = runRuleHealthChecks({
      stages: [mkStage({ id: 'n1', assigneeStrategy: 'initiator' })],
      routes: [mkRoute({ edgeKey: 'edgeX', fromStageKey: 'n1', toStageKey: 'GONE' })],
      dynamicPolicies: [], fields: [],
    })
    expect(find(items, '流转规则引用失效')?.target).toEqual({ kind: 'edge', key: 'edgeX' })
  })

  it('规则重叠 → target 指向第一条重叠边', () => {
    const items = runRuleHealthChecks({
      stages: [mkStage({ id: 'n1', assigneeStrategy: 'initiator' }), mkStage({ id: 'n2', name: 'B', assigneeStrategy: 'initiator' })],
      routes: [
        mkRoute({ edgeKey: 'eA', fromStageKey: 'n1', toStageKey: 'n2', conditionJson: cond('gt', 10), priority: 1 }),
        mkRoute({ edgeKey: 'eB', fromStageKey: 'n1', toStageKey: 'n2', conditionJson: cond('gt', 20), priority: 2 }),
        mkRoute({ edgeKey: 'eD', fromStageKey: 'n1', toStageKey: 'n2', routeName: '默认', isDefault: true, priority: 3 }),
      ],
      dynamicPolicies: [],
      fields: [{ key: 'amount', label: '金额', type: 'money' } as SchemaFieldDefinition],
    })
    expect(find(items, '规则重叠')?.target).toEqual({ kind: 'edge', key: 'eA' })
  })

  it('缺少默认分支 → target 指向来源节点；ok 汇总项无 target', () => {
    const missing = runRuleHealthChecks({
      stages: [mkStage({ id: 'n1', assigneeStrategy: 'initiator' }), mkStage({ id: 'n2', name: 'B', assigneeStrategy: 'initiator' })],
      routes: [mkRoute({ edgeKey: 'eA', fromStageKey: 'n1', toStageKey: 'n2', conditionJson: cond('gt', 10), isDefault: false })],
      dynamicPolicies: [], fields: [{ key: 'amount', label: '金额', type: 'money' } as SchemaFieldDefinition],
    })
    expect(find(missing, '缺少默认分支')?.target).toEqual({ kind: 'node', key: 'n1' })

    const ok = runRuleHealthChecks({
      stages: [mkStage({ id: 'n1', assigneeStrategy: 'initiator' })],
      routes: [], dynamicPolicies: [], fields: [],
    })
    expect(ok).toHaveLength(1)
    expect(ok[0].level).toBe('ok')
    expect(ok[0].target).toBeUndefined()
  })
})

describe('validatePublishConfig —— PublishIssue target', () => {
  const baseCtx = { routes: [], dynamicPolicies: [], cardSchema: [], detailSchema: [], cardComponents: [], approvalAdminUserIds: [] }

  it('返回结构化项：message + target；节点问题指向该节点', () => {
    const issues = validatePublishConfig({
      ...baseCtx,
      stages: [mkStage({ id: 'n1', type: 'manual', assigneeStrategy: undefined, actionPolicy: { allowedActions: ['approve'] } })],
    })
    const hit = issues.find(i => i.message.includes('处理人策略未配置'))
    expect(hit?.target).toEqual({ kind: 'node', key: 'n1' })
  })

  it('流转规则未配置流转条件 → target 指向该边', () => {
    const issues = validatePublishConfig({
      ...baseCtx,
      stages: [mkStage({ id: 'n1', assigneeStrategy: 'initiator', actionPolicy: { allowedActions: ['approve'] } }),
               mkStage({ id: 'n2', name: 'B', assigneeStrategy: 'initiator', actionPolicy: { allowedActions: ['approve'] } })],
      routes: [mkRoute({ edgeKey: 'eX', fromStageKey: 'n1', toStageKey: 'n2', isDefault: false, conditionJson: null })],
    })
    expect(issues.find(i => i.message.includes('未配置流转条件'))?.target).toEqual({ kind: 'edge', key: 'eX' })
  })

  it('组件不可发布 → 有 message 但无 target（组件在组件抽屉编辑，非 node/edge 现场）', () => {
    const issues = validatePublishConfig({
      ...baseCtx,
      stages: [],
      cardComponents: [{ id: 'c1', type: 'formula', title: '公式', props: {} } as any],
    })
    const hit = issues.find(i => i.message.includes('暂未支持发布'))
    expect(hit).toBeTruthy()
    expect(hit?.target).toBeUndefined()
  })
})
