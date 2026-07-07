/**
 * 竖向流程图投影纯函数（plan M1-1，设计总体屏 3 / E0 投影规则）。
 *
 * 投影规则（锁死）：
 * ① 同源节点多条 active 出边 = 一个 branchGroup；条件列按 priority 升序，default 兜底列恒最右。
 * ② 分支在"全组共同后继"（汇合点）收拢回主干；无共同汇合点则各支展开到各自终点。
 * ③ 环 / 交叉边（节点被多支到达但非全组汇合点）→ 该节点只渲染一次并记入 complex（复杂区段，
 *    UI 降级提示到只读画布查看），不阻塞其余区段。
 * ④ 无任何 active route = legacy 线性模式（对齐引擎 StageRouteResolver 的 legacy linear fallback），
 *    按 sortOrder 排线性链。
 * ⑤ 不可达节点进 orphans（诊断消费）。
 *
 * 守恒不变量（M1-6 冒烟判据）：collectStages(tree) + orphans = stages 全集（complex 不重复计入）。
 */
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import type { StageRouteRuleRequest } from '@/types/cardflow'

export interface FlowTreeNode {
  kind: 'stage' | 'branchGroup'
  /** kind=stage */
  stageId?: string
  /** kind=branchGroup */
  branches?: FlowTreeBranch[]
}

export interface FlowTreeBranch {
  /** 对应 route 的 edgeKey（诊断 target 同键） */
  routeEdgeKey: string
  isDefault: boolean
  priority: number
  children: FlowTreeNode[]
}

export interface FlowTreeResult {
  tree: FlowTreeNode[]
  /** 不可达节点 id（诊断消费） */
  orphans: string[]
  /** 复杂区段节点 id（环/交叉边降级，只渲染一次） */
  complex: string[]
}

export type InsertAnchor =
  | { afterStageId: string }
  | { branchEdgeKey: string }

interface ProjectionContext {
  outgoing: Map<string, StageRouteRuleRequest[]>
  rendered: Set<string>
  complex: Set<string>
  stageById: Map<string, StageDefinition>
}

function isActive(route: StageRouteRuleRequest): boolean {
  return !route.status || route.status === 'active'
}

/** 出边排序：条件列按 priority 升序，default 恒最后（引擎语义：default 只在无命中时兜底） */
function sortOutgoing(routes: StageRouteRuleRequest[]): StageRouteRuleRequest[] {
  return [...routes].sort((a, b) => {
    if (a.isDefault !== b.isDefault) return a.isDefault ? 1 : -1
    return a.priority - b.priority || a.edgeKey.localeCompare(b.edgeKey)
  })
}

function buildOutgoingMap(routes: StageRouteRuleRequest[]): Map<string, StageRouteRuleRequest[]> {
  const map = new Map<string, StageRouteRuleRequest[]>()
  for (const route of routes.filter(isActive)) {
    const list = map.get(route.fromStageKey) ?? []
    list.push(route)
    map.set(route.fromStageKey, list)
  }
  for (const [key, list] of map) map.set(key, sortOutgoing(list))
  return map
}

/** 沿"唯一出边"展开可达节点序（分支汇合点计算用；多出边/环处停止） */
function reachableChain(start: string, ctx: ProjectionContext): string[] {
  const chain: string[] = []
  const seen = new Set<string>()
  let cursor: string | undefined = start
  while (cursor && !seen.has(cursor) && ctx.stageById.has(cursor)) {
    chain.push(cursor)
    seen.add(cursor)
    const outs: StageRouteRuleRequest[] = ctx.outgoing.get(cursor) ?? []
    cursor = outs.length === 1 ? outs[0].toStageKey || undefined : undefined
  }
  return chain
}

/** 递归展开一段链：从 startKey 走到 stopKey（不含）或终点 */
function walkChain(startKey: string | undefined, stopKey: string | undefined, ctx: ProjectionContext): FlowTreeNode[] {
  const nodes: FlowTreeNode[] = []
  let cursor = startKey
  while (cursor && cursor !== stopKey) {
    const stage = ctx.stageById.get(cursor)
    if (!stage) break
    if (ctx.rendered.has(cursor)) {
      // 交叉边/环：已渲染过 → 标复杂区段并停止本支展开（只渲染一次）
      ctx.complex.add(cursor)
      break
    }
    ctx.rendered.add(cursor)
    nodes.push({ kind: 'stage', stageId: cursor })

    const outs = ctx.outgoing.get(cursor) ?? []
    if (outs.length === 0) break
    if (outs.length === 1) {
      cursor = outs[0].toStageKey || undefined
      continue
    }

    // 多出边 → 分支组
    const merge = findMergePoint(outs, ctx)
    const branches: FlowTreeBranch[] = outs.map((route) => ({
      routeEdgeKey: route.edgeKey,
      isDefault: route.isDefault,
      priority: route.priority,
      children: route.toStageKey && route.toStageKey !== merge
        ? walkChain(route.toStageKey, merge, ctx)
        : [],
    }))
    nodes.push({ kind: 'branchGroup', branches })
    cursor = merge
  }
  return nodes
}

/** 全组共同汇合点：以首支可达序为基准，取所有支可达集的交集中最早者 */
function findMergePoint(outs: StageRouteRuleRequest[], ctx: ProjectionContext): string | undefined {
  const chains = outs.map((r) => (r.toStageKey ? reachableChain(r.toStageKey, ctx) : []))
  if (chains.some((c) => c.length === 0)) return undefined
  const [base, ...rest] = chains
  for (const candidate of base) {
    if (rest.every((c) => c.includes(candidate))) return candidate
  }
  return undefined
}

export function buildFlowTree(
  stages: StageDefinition[],
  routes: StageRouteRuleRequest[],
): FlowTreeResult {
  const sorted = [...stages].sort((a, b) => a.sortOrder - b.sortOrder)
  const activeRoutes = routes.filter(isActive)

  // legacy 线性模式（引擎同款 fallback）
  if (activeRoutes.length === 0) {
    return {
      tree: sorted.map((s) => ({ kind: 'stage' as const, stageId: s.id })),
      orphans: [],
      complex: [],
    }
  }

  const ctx: ProjectionContext = {
    outgoing: buildOutgoingMap(routes),
    rendered: new Set(),
    complex: new Set(),
    stageById: new Map(sorted.map((s) => [s.id, s])),
  }

  // 起点：无入边的节点（按 sortOrder 最早者）；全有入边（环）则退回 sortOrder 首节点
  const hasIncoming = new Set(activeRoutes.map((r) => r.toStageKey).filter(Boolean))
  const start = sorted.find((s) => !hasIncoming.has(s.id)) ?? sorted[0]
  const tree = start ? walkChain(start.id, undefined, ctx) : []

  const orphans = sorted.filter((s) => !ctx.rendered.has(s.id)).map((s) => s.id)
  return { tree, orphans, complex: [...ctx.complex] }
}

// ==================== 反向写回 ====================

function genEdgeKey(): string {
  // 与编辑页 genStableKey('edge') 同款格式，保持 edgeKey 前缀一致
  return `edge_${Math.random().toString(36).slice(2, 10)}`
}

/** stages 数组在 afterId 之后插入 newStage 并重编 sortOrder */
function spliceStage(stages: StageDefinition[], afterId: string | undefined, newStage: StageDefinition): StageDefinition[] {
  const sorted = [...stages].sort((a, b) => a.sortOrder - b.sortOrder)
  const idx = afterId ? sorted.findIndex((s) => s.id === afterId) : sorted.length - 1
  sorted.splice((idx < 0 ? sorted.length - 1 : idx) + 1, 0, newStage)
  return sorted.map((s, i) => ({ ...s, sortOrder: i + 1 }))
}

/**
 * 在锚点后插入普通节点。
 * - afterStageId：原出边整体转移给新节点（保持既有分支结构随之下移），锚点新建 default 边指向新节点。
 * - branchEdgeKey：该边重定向到新节点，新节点以 default 边接原目标（分支内插入）。
 * - legacy（无 active route）：仅按 sortOrder 插入，不造边。
 */
export function insertStageAfter(
  stages: StageDefinition[],
  routes: StageRouteRuleRequest[],
  anchor: InsertAnchor,
  newStage: StageDefinition,
): { stages: StageDefinition[]; routes: StageRouteRuleRequest[] } {
  if ('branchEdgeKey' in anchor) {
    const edge = routes.find((r) => r.edgeKey === anchor.branchEdgeKey)
    if (!edge) return { stages, routes }
    const originalTo = edge.toStageKey
    const nextRoutes = routes.map((r) =>
      r.edgeKey === anchor.branchEdgeKey ? { ...r, toStageKey: newStage.id } : r,
    )
    if (originalTo) {
      nextRoutes.push({
        edgeKey: genEdgeKey(),
        fromStageKey: newStage.id,
        toStageKey: originalTo,
        routeName: '默认',
        conditionJson: null,
        priority: 1,
        isDefault: true,
        status: 'active',
      })
    }
    return { stages: spliceStage(stages, edge.fromStageKey, newStage), routes: nextRoutes }
  }

  const afterId = anchor.afterStageId
  const nextStages = spliceStage(stages, afterId, newStage)
  const activeRoutes = routes.filter(isActive)
  if (activeRoutes.length === 0) return { stages: nextStages, routes }

  // 原出边整体转移给新节点
  const nextRoutes = routes.map((r) =>
    isActive(r) && r.fromStageKey === afterId ? { ...r, fromStageKey: newStage.id } : r,
  )
  nextRoutes.push({
    edgeKey: genEdgeKey(),
    fromStageKey: afterId,
    toStageKey: newStage.id,
    routeName: '默认',
    conditionJson: null,
    priority: 1,
    isDefault: true,
    status: 'active',
  })
  return { stages: nextStages, routes: nextRoutes }
}

/**
 * 在锚点后插入条件分支组（branchCount ≥ 2，含恒定兜底列）。
 * 原出边（若有）保留为兜底列并置 priority 最大；新建 branchCount-1 条条件列，均指向原后继。
 * 无原出边（尾节点）：全部新建，指向空（各支即流程结束）。
 */
export function insertBranchGroup(
  stages: StageDefinition[],
  routes: StageRouteRuleRequest[],
  anchor: { afterStageId: string },
  branchCount: number,
): { stages: StageDefinition[]; routes: StageRouteRuleRequest[] } {
  const afterId = anchor.afterStageId
  const existing = sortOutgoing(routes.filter((r) => isActive(r) && r.fromStageKey === afterId))
  const originalTo = existing.find((r) => r.isDefault)?.toStageKey ?? existing[0]?.toStageKey ?? ''
  const conditionCount = Math.max(1, branchCount - 1)

  const nextRoutes = routes.filter((r) => !(isActive(r) && r.fromStageKey === afterId))
  for (let i = 0; i < conditionCount; i++) {
    nextRoutes.push({
      edgeKey: genEdgeKey(),
      fromStageKey: afterId,
      toStageKey: originalTo,
      routeName: `分支 ${i + 1}`,
      conditionJson: JSON.stringify({ logic: 'and', conditions: [] }),
      priority: i + 1,
      isDefault: false,
      status: 'active',
    })
  }
  // 兜底列：复用原 default 边（保 edgeKey 稳定）或新建
  const originalDefault = existing.find((r) => r.isDefault)
  nextRoutes.push(
    originalDefault
      ? { ...originalDefault, routeName: '其他情况', priority: conditionCount + 1 }
      : {
          edgeKey: genEdgeKey(),
          fromStageKey: afterId,
          toStageKey: originalTo,
          routeName: '其他情况',
          conditionJson: null,
          priority: conditionCount + 1,
          isDefault: true,
          status: 'active',
        },
  )
  return { stages, routes: nextRoutes }
}
