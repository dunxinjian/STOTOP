import { describe, it, expect } from 'vitest'
import {
  buildFlowTree,
  insertStageAfter,
  insertBranchGroup,
  deleteBranch,
  copyBranch,
  reorderBranch,
  collectBranchStages,
  deleteStage,
  type FlowTreeNode,
} from '@/utils/flowGraphProjection'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import type { StageRouteRuleRequest } from '@/types/cardflow'

function s(id: string, sortOrder: number, type: 'manual' | 'auto' = 'manual'): StageDefinition {
  return { id, name: `节点${id}`, type, sortOrder }
}

function r(edgeKey: string, from: string, to: string, opts: Partial<StageRouteRuleRequest> = {}): StageRouteRuleRequest {
  return {
    edgeKey,
    fromStageKey: from,
    toStageKey: to,
    routeName: opts.routeName ?? edgeKey,
    conditionJson: opts.conditionJson ?? null,
    priority: opts.priority ?? 1,
    isDefault: opts.isDefault ?? false,
    status: opts.status ?? 'active',
  }
}

/** 树中全部 stage 节点的 stageId（深度优先展开，守恒断言用） */
function collectStageIds(nodes: FlowTreeNode[]): string[] {
  const out: string[] = []
  for (const n of nodes) {
    if (n.kind === 'stage' && n.stageId) out.push(n.stageId)
    if (n.kind === 'branchGroup') {
      for (const b of n.branches ?? []) out.push(...collectStageIds(b.children))
    }
  }
  return out
}

describe('buildFlowTree 投影', () => {
  it('legacy 模式（无 routes）→ 按 sortOrder 线性链（对齐引擎 legacy linear fallback）', () => {
    const { tree, orphans } = buildFlowTree([s('b', 2), s('a', 1), s('c', 3)], [])
    expect(collectStageIds(tree)).toEqual(['a', 'b', 'c'])
    expect(tree.every((n) => n.kind === 'stage')).toBe(true)
    expect(orphans).toEqual([])
  })

  it('规则模式线性链 a→b→c', () => {
    const { tree, orphans } = buildFlowTree(
      [s('a', 1), s('b', 2), s('c', 3)],
      [r('e1', 'a', 'b', { isDefault: true }), r('e2', 'b', 'c', { isDefault: true })],
    )
    expect(collectStageIds(tree)).toEqual(['a', 'b', 'c'])
    expect(orphans).toEqual([])
  })

  it('单分支组：条件列+兜底列，在共同后继汇合回主干', () => {
    const stages = [s('a', 1), s('b', 2), s('c', 3), s('d', 4)]
    const routes = [
      r('e1', 'a', 'b', { conditionJson: '{"logic":"and","conditions":[]}', priority: 1, routeName: '大额' }),
      r('e2', 'a', 'c', { isDefault: true, priority: 2, routeName: '其他情况' }),
      r('e3', 'b', 'd', { isDefault: true }),
      r('e4', 'c', 'd', { isDefault: true }),
    ]
    const { tree, orphans } = buildFlowTree(stages, routes)
    expect(orphans).toEqual([])
    // 结构：a → branchGroup([b],[c]) → d
    expect(tree[0]).toMatchObject({ kind: 'stage', stageId: 'a' })
    expect(tree[1].kind).toBe('branchGroup')
    const group = tree[1]
    expect(group.branches).toHaveLength(2)
    expect(group.branches![0]).toMatchObject({ routeEdgeKey: 'e1', isDefault: false })
    expect(collectStageIds(group.branches![0].children)).toEqual(['b'])
    expect(group.branches![1]).toMatchObject({ routeEdgeKey: 'e2', isDefault: true })
    expect(collectStageIds(group.branches![1].children)).toEqual(['c'])
    expect(tree[2]).toMatchObject({ kind: 'stage', stageId: 'd' })
    // 守恒：树内 stage 数 = stages 总数
    expect(collectStageIds(tree).sort()).toEqual(['a', 'b', 'c', 'd'])
  })

  it('分支按 priority 排序且兜底列恒最右（即使兜底 priority 更小）', () => {
    const stages = [s('a', 1), s('b', 2), s('c', 3), s('x', 4)]
    const routes = [
      r('cond2', 'a', 'c', { conditionJson: '{}', priority: 5 }),
      r('def', 'a', 'x', { isDefault: true, priority: 1 }),
      r('cond1', 'a', 'b', { conditionJson: '{}', priority: 2 }),
    ]
    const { tree } = buildFlowTree(stages, routes)
    const group = tree.find((n) => n.kind === 'branchGroup')!
    expect(group.branches!.map((b) => b.routeEdgeKey)).toEqual(['cond1', 'cond2', 'def'])
  })

  it('孤儿节点（不可达）记入 orphans 且按 sortOrder 追加渲染在尾部（保持可编辑）', () => {
    const { tree, orphans } = buildFlowTree(
      [s('a', 1), s('b', 2), s('ghost', 9)],
      [r('e1', 'a', 'b', { isDefault: true })],
    )
    expect(collectStageIds(tree)).toEqual(['a', 'b', 'ghost'])
    expect(orphans).toEqual(['ghost'])
  })

  it('空分支（两条边直指同一后继）→ 分支子链为空，汇合点继续主干', () => {
    const stages = [s('a', 1), s('x', 2)]
    const routes = [
      r('c1', 'a', 'x', { conditionJson: '{}', priority: 1 }),
      r('d1', 'a', 'x', { isDefault: true, priority: 2 }),
    ]
    const { tree } = buildFlowTree(stages, routes)
    const group = tree.find((n) => n.kind === 'branchGroup')!
    expect(group.branches![0].children).toEqual([])
    expect(group.branches![1].children).toEqual([])
    expect(tree[2]).toMatchObject({ kind: 'stage', stageId: 'x' })
  })

  it('无共同汇合点：各分支含完整子链，组后主干无延续', () => {
    const stages = [s('a', 1), s('b', 2), s('c', 3)]
    const routes = [
      r('c1', 'a', 'b', { conditionJson: '{}', priority: 1 }),
      r('d1', 'a', 'c', { isDefault: true, priority: 2 }),
    ]
    const { tree } = buildFlowTree(stages, routes)
    expect(tree).toHaveLength(2) // a + branchGroup，组后无节点
    const group = tree[1]
    expect(collectStageIds(group.branches![0].children)).toEqual(['b'])
    expect(collectStageIds(group.branches![1].children)).toEqual(['c'])
  })

  it('环（a→b→a）→ 降级 complex 不死循环', () => {
    const stages = [s('a', 1), s('b', 2)]
    const routes = [
      r('e1', 'a', 'b', { isDefault: true }),
      r('e2', 'b', 'a', { isDefault: true }),
    ]
    const { complex } = buildFlowTree(stages, routes)
    expect(complex.length).toBeGreaterThan(0)
  })

  it('交叉边（两分支汇到 x 但第三支不汇）→ 后到支渲染 stageRef 引用而非重复节点', () => {
    const stages = [s('a', 1), s('b', 2), s('c', 3), s('d', 4), s('x', 5)]
    const routes = [
      r('c1', 'a', 'b', { conditionJson: '{}', priority: 1 }),
      r('c2', 'a', 'c', { conditionJson: '{}', priority: 2 }),
      r('d1', 'a', 'd', { isDefault: true, priority: 3 }),
      r('e1', 'b', 'x', { isDefault: true }),
      r('e2', 'c', 'x', { isDefault: true }),
    ]
    const { tree } = buildFlowTree(stages, routes)
    // x 作为实体 stage 只出现一次；另一支以 stageRef 引用
    const ids = collectStageIds(tree)
    expect(ids.filter((id) => id === 'x')).toHaveLength(1)
    const group = tree.find((n) => n.kind === 'branchGroup')!
    const refCount = group.branches!.flatMap((b) => b.children).filter((c) => c.kind === 'stageRef' && c.stageId === 'x').length
    expect(refCount).toBe(1)
  })

  it('部分汇合形态（#2261 借款流）：两条件支同指一中间节点+兜底直指终点 → 引用渲染零孤儿', () => {
    // loan_approval → [cond1 → finance, cond2 → upper, cond3 → finance, DEF → payment]
    // finance → payment / upper → payment
    const stages = [s('approval', 1), s('upper', 2), s('finance', 3), s('payment', 4)]
    const routes = [
      r('r1', 'approval', 'finance', { conditionJson: '{}', priority: 1 }),
      r('r2', 'approval', 'upper', { conditionJson: '{}', priority: 2 }),
      r('r3', 'approval', 'finance', { conditionJson: '{}', priority: 3 }),
      r('r4', 'approval', 'payment', { isDefault: true, priority: 4 }),
      r('r5', 'finance', 'payment', { isDefault: true }),
      r('r6', 'upper', 'payment', { isDefault: true }),
    ]
    const { tree, orphans, complex } = buildFlowTree(stages, routes)
    expect(orphans).toEqual([])
    expect(complex).toEqual([])
    // 全部 4 节点可见（实体恰一次）
    const ids = collectStageIds(tree)
    expect([...ids].sort()).toEqual(['approval', 'finance', 'payment', 'upper'])
  })
})

describe('insertStageAfter 反向写回', () => {
  it('规则模式线性：a→b 插入 n → a→n(default) + n→b(继承原 default)', () => {
    const stages = [s('a', 1), s('b', 2)]
    const routes = [r('e1', 'a', 'b', { isDefault: true })]
    const out = insertStageAfter(stages, routes, { afterStageId: 'a' }, s('n', 0))
    expect(out.stages.map((x) => x.id)).toEqual(['a', 'n', 'b'])
    const aOut = out.routes.filter((x) => x.fromStageKey === 'a')
    const nOut = out.routes.filter((x) => x.fromStageKey === 'n')
    expect(aOut).toHaveLength(1)
    expect(aOut[0].toStageKey).toBe('n')
    expect(aOut[0].isDefault).toBe(true)
    expect(nOut).toHaveLength(1)
    expect(nOut[0].toStageKey).toBe('b')
    expect(nOut[0].isDefault).toBe(true)
    // 投影闭环：插入后仍是合法线性链
    expect(collectStageIds(buildFlowTree(out.stages, out.routes).tree)).toEqual(['a', 'n', 'b'])
  })

  it('legacy 模式（无 routes）：仅按 sortOrder 插入，不造边', () => {
    const out = insertStageAfter([s('a', 1), s('b', 2)], [], { afterStageId: 'a' }, s('n', 0))
    expect(out.routes).toEqual([])
    expect(collectStageIds(buildFlowTree(out.stages, out.routes).tree)).toEqual(['a', 'n', 'b'])
  })

  it('分支内插入（anchor=branchEdgeKey）：边重定向到新节点，新节点接原目标', () => {
    const stages = [s('a', 1), s('b', 2), s('x', 3)]
    const routes = [
      r('c1', 'a', 'b', { conditionJson: '{}', priority: 1 }),
      r('d1', 'a', 'x', { isDefault: true, priority: 2 }),
      r('e1', 'b', 'x', { isDefault: true }),
    ]
    const out = insertStageAfter(stages, routes, { branchEdgeKey: 'd1' }, s('n', 0))
    const d1 = out.routes.find((x) => x.edgeKey === 'd1')!
    expect(d1.toStageKey).toBe('n')
    const nOut = out.routes.filter((x) => x.fromStageKey === 'n')
    expect(nOut).toHaveLength(1)
    expect(nOut[0].toStageKey).toBe('x')
    // 投影：n 落入兜底分支内
    const { tree } = buildFlowTree(out.stages, out.routes)
    const group = tree.find((t) => t.kind === 'branchGroup')!
    expect(collectStageIds(group.branches!.find((b) => b.isDefault)!.children)).toEqual(['n'])
  })

  it('尾节点后插入（无出边）：仅加 a→n 默认边', () => {
    const stages = [s('a', 1), s('b', 2)]
    const routes = [r('e1', 'a', 'b', { isDefault: true })]
    const out = insertStageAfter(stages, routes, { afterStageId: 'b' }, s('n', 0))
    const bOut = out.routes.filter((x) => x.fromStageKey === 'b')
    expect(bOut).toHaveLength(1)
    expect(bOut[0].toStageKey).toBe('n')
    expect(bOut[0].isDefault).toBe(true)
    expect(collectStageIds(buildFlowTree(out.stages, out.routes).tree)).toEqual(['a', 'b', 'n'])
  })
})

describe('insertBranchGroup 反向写回', () => {
  it('a→b 处插入 2 列分支组：1 条件列 + 1 兜底列，均指向原后继 b', () => {
    const stages = [s('a', 1), s('b', 2)]
    const routes = [r('e1', 'a', 'b', { isDefault: true })]
    const out = insertBranchGroup(stages, routes, { afterStageId: 'a' }, 2)
    const aOut = out.routes.filter((x) => x.fromStageKey === 'a')
    expect(aOut).toHaveLength(2)
    expect(aOut.filter((x) => x.isDefault)).toHaveLength(1)
    expect(aOut.every((x) => x.toStageKey === 'b')).toBe(true)
    // 兜底 priority 恒最大
    const def = aOut.find((x) => x.isDefault)!
    expect(Math.max(...aOut.map((x) => x.priority))).toBe(def.priority)
    // 投影：a → group(空列×2) → b
    const { tree } = buildFlowTree(out.stages, out.routes)
    expect(tree[1].kind).toBe('branchGroup')
    expect(tree[1].branches).toHaveLength(2)
    expect(tree[2]).toMatchObject({ kind: 'stage', stageId: 'b' })
  })

  it('尾节点处插入分支组：列指向空（各支即流程结束）', () => {
    const stages = [s('a', 1)]
    const out = insertBranchGroup(stages, [], { afterStageId: 'a' }, 3)
    const aOut = out.routes.filter((x) => x.fromStageKey === 'a')
    expect(aOut).toHaveLength(3)
    expect(aOut.filter((x) => x.isDefault)).toHaveLength(1)
    expect(aOut.every((x) => x.toStageKey === '')).toBe(true)
  })
})

describe('分支操作三纯函数 (M1-4)', () => {
  /** 夹具：a → group(条件 c1→b→x / 兜底 d1→x) → x */
  function fixture() {
    const stages = [s('a', 1), s('b', 2), s('x', 3)]
    const routes = [
      r('c1', 'a', 'b', { conditionJson: '{}', priority: 1, routeName: '大额' }),
      r('d1', 'a', 'x', { isDefault: true, priority: 2, routeName: '其他情况' }),
      r('e1', 'b', 'x', { isDefault: true }),
    ]
    return { stages, routes }
  }

  it('collectBranchStages：列出支内独占节点（汇合点不算）', () => {
    const { stages, routes } = fixture()
    expect(collectBranchStages(stages, routes, 'c1')).toEqual(['b'])
    expect(collectBranchStages(stages, routes, 'd1')).toEqual([])
  })

  it('deleteBranch：删条件支连带支内独占节点及其边；剩单出边解散分支组', () => {
    const { stages, routes } = fixture()
    const out = deleteBranch(stages, routes, 'c1')
    expect(out.stages.map((x) => x.id)).toEqual(['a', 'x'])
    expect(out.routes.find((x) => x.edgeKey === 'c1')).toBeUndefined()
    expect(out.routes.find((x) => x.edgeKey === 'e1')).toBeUndefined()
    // 只剩兜底 d1：a→x 线性
    const { tree } = buildFlowTree(out.stages, out.routes)
    expect(collectStageIds(tree)).toEqual(['a', 'x'])
    expect(tree.every((n) => n.kind === 'stage')).toBe(true)
  })

  it('deleteBranch：兜底列拒绝删除（返回原值不变）', () => {
    const { stages, routes } = fixture()
    const out = deleteBranch(stages, routes, 'd1')
    expect(out.stages).toEqual(stages)
    expect(out.routes).toEqual(routes)
  })

  it('copyBranch：深拷贝条件与支内节点（新 id/edgeKey），插为下一优先级', () => {
    const { stages, routes } = fixture()
    const out = copyBranch(stages, routes, 'c1')
    const aOut = out.routes.filter((x) => x.fromStageKey === 'a')
    expect(aOut).toHaveLength(3)
    const copies = aOut.filter((x) => x.routeName?.includes('大额'))
    expect(copies).toHaveLength(2)
    const copy = copies.find((x) => x.edgeKey !== 'c1')!
    expect(copy.isDefault).toBe(false)
    // 支内节点被复制：stages 多一个（b 的副本），且副本链到汇合点 x
    expect(out.stages).toHaveLength(4)
    const copyStages = collectBranchStages(out.stages, out.routes, copy.edgeKey)
    expect(copyStages).toHaveLength(1)
    expect(copyStages[0]).not.toBe('b')
    // 兜底仍恒最右（priority 最大）
    const def = aOut.find((x) => x.isDefault)!
    expect(Math.max(...aOut.map((x) => x.priority))).toBe(def.priority)
  })

  it('reorderBranch：条件列左移/右移交换 priority；不可越过兜底', () => {
    const stages = [s('a', 1), s('b', 2), s('c', 3), s('x', 4)]
    const routes = [
      r('c1', 'a', 'b', { conditionJson: '{}', priority: 1 }),
      r('c2', 'a', 'c', { conditionJson: '{}', priority: 2 }),
      r('d1', 'a', 'x', { isDefault: true, priority: 3 }),
      r('e1', 'b', 'x', { isDefault: true }),
      r('e2', 'c', 'x', { isDefault: true }),
    ]
    const out = reorderBranch(stages, routes, 'c1', 'right')
    const p = (k: string) => out.routes.find((x) => x.edgeKey === k)!.priority
    expect(p('c1')).toBe(2)
    expect(p('c2')).toBe(1)
    // c1 再右移撞兜底 → 不变
    const out2 = reorderBranch(out.stages, out.routes, 'c1', 'right')
    expect(out2.routes.find((x) => x.edgeKey === 'c1')!.priority).toBe(2)
  })
})

describe('dev 库实测形态夹具 (M1-6)', () => {
  it('#1357 形态：迁移遗留重复死节点（交错孤儿）→ 全部可见渲染 + orphans 告警', () => {
    // stage_1350_N(type=approval, 迁移遗留) 与 expense_request_*(type=human) 同 sortOrder 并存，
    // routes 只引用 expense_request_*。投影须：路由链正常 + 死节点追加可见 + orphans 列出。
    const stages = [
      s('legacy_1', 1), s('approval', 1),
      s('legacy_2', 2), s('dept', 2),
      s('legacy_3', 3), s('region', 3),
      s('legacy_4', 4), s('budget', 4),
    ]
    const routes = [
      r('r1', 'approval', 'region', { conditionJson: '{}', priority: 1 }),
      r('r2', 'approval', 'dept', { conditionJson: '{}', priority: 2 }),
      r('r3', 'approval', 'budget', { isDefault: true, priority: 99 }),
      r('r4', 'dept', 'budget', { isDefault: true, priority: 99 }),
      r('r5', 'region', 'budget', { isDefault: true, priority: 99 }),
    ]
    const { tree, orphans, complex } = buildFlowTree(stages, routes)
    expect(complex).toEqual([])
    expect(orphans.sort()).toEqual(['legacy_1', 'legacy_2', 'legacy_3', 'legacy_4'])
    // 守恒：8 个节点全部可见（实体恰一次）
    expect(collectStageIds(tree)).toHaveLength(8)
  })

  it('#2331 形态：批次链尾段无路由（纯尾段孤儿）→ 追加渲染保持编辑可见', () => {
    const stages = [s('imp', 1, 'auto'), s('qa', 2, 'auto'), s('voucher', 3, 'auto'), s('summary', 4, 'auto'), s('confirm', 5)]
    const routes = [
      r('c1', 'imp', 'qa', { conditionJson: '{}', priority: 1 }),
      r('c2', 'imp', 'qa', { conditionJson: '{}', priority: 2 }),
    ]
    const { tree, orphans } = buildFlowTree(stages, routes)
    expect(orphans).toEqual(['voucher', 'summary', 'confirm'])
    expect(collectStageIds(tree)).toHaveLength(5)
  })
})

describe('deleteStage 节点删除 (E0-D1 修复)', () => {
  it('线性节点：入边重定向到其后继，出边删除', () => {
    const stages = [s('a', 1), s('n', 2), s('b', 3)]
    const routes = [
      r('e1', 'a', 'n', { isDefault: true }),
      r('e2', 'n', 'b', { isDefault: true }),
    ]
    const out = deleteStage(stages, routes, 'n')
    expect(out.stages.map((x) => x.id)).toEqual(['a', 'b'])
    const e1 = out.routes.find((x) => x.edgeKey === 'e1')!
    expect(e1.toStageKey).toBe('b')
    expect(out.routes.find((x) => x.edgeKey === 'e2')).toBeUndefined()
    expect(collectStageIds(buildFlowTree(out.stages, out.routes).tree)).toEqual(['a', 'b'])
  })

  it('尾节点：入边一并删除（前驱变尾）', () => {
    const stages = [s('a', 1), s('n', 2)]
    const routes = [r('e1', 'a', 'n', { isDefault: true })]
    const out = deleteStage(stages, routes, 'n')
    expect(out.stages.map((x) => x.id)).toEqual(['a'])
    expect(out.routes).toEqual([])
  })

  it('分支源节点：入边重定向到兜底目标，全部出边（分支组）删除', () => {
    const stages = [s('a', 1), s('n', 2), s('b', 3), s('x', 4)]
    const routes = [
      r('e0', 'a', 'n', { isDefault: true }),
      r('c1', 'n', 'b', { conditionJson: '{}', priority: 1 }),
      r('d1', 'n', 'x', { isDefault: true, priority: 2 }),
      r('e2', 'b', 'x', { isDefault: true }),
    ]
    const out = deleteStage(stages, routes, 'n')
    // 入边 e0 指向兜底目标 x；n 的出边全删；b 的出边保留（b 成孤儿由投影告警）
    expect(out.routes.find((x) => x.edgeKey === 'e0')!.toStageKey).toBe('x')
    expect(out.routes.filter((x) => x.fromStageKey === 'n')).toEqual([])
  })

  it('legacy 模式（无 routes）：仅移除节点', () => {
    const out = deleteStage([s('a', 1), s('n', 2), s('b', 3)], [], 'n')
    expect(out.stages.map((x) => x.id)).toEqual(['a', 'b'])
    expect(out.routes).toEqual([])
  })
})
