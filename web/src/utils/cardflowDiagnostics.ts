import type { CardComponentDefinition, DynamicStagePolicyRequest, SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import { resolveComponentCapability } from '@/components/cardflow/designer/cardComponentCapabilities'

/**
 * CardFlow 设计期诊断单一真源：规则健康检查（默认分支 / 分支完整性 / 规则重叠 /
 * 死路·循环·不可达 / 处理人兜底 / 字段类型匹配）。
 * 抽自 RuleHealthPanel，纯函数、无 Vue 依赖，供面板渲染与（后续）发布校验共用，
 * 杜绝两处诊断实现漂移。校验口径对齐后端 FlowDefinitionService/RouteGraphValidator。
 */

export type HealthLevel = 'error' | 'warning' | 'ok'

export interface HealthItem {
  level: HealthLevel
  title: string
  detail: string
}

export interface RuleHealthContext {
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  fields: SchemaFieldDefinition[]
}

function parseCondition(json?: string | null): any {
  if (!json) return null
  try { return JSON.parse(json) } catch { return null }
}

function flattenConditions(condition: any): any[] {
  if (!condition) return []
  if (Array.isArray(condition.conditions)) {
    return condition.conditions.flatMap(flattenConditions)
  }
  return condition.field ? [condition] : []
}

// 从叶子条件求某字段的数值区间 [lo, hi]（gt/gte/lt/lte/eq/between），无数值约束返回 null
function numericIntervalFor(conds: any[], field: string): { lo: number; hi: number } | null {
  let lo = -Infinity
  let hi = Infinity
  let has = false
  for (const c of conds) {
    if (c.field !== field) continue
    if (c.operator === 'between' && Array.isArray(c.value) && c.value.length === 2) {
      const a = Number(c.value[0])
      const b = Number(c.value[1])
      if (!isNaN(a)) { lo = Math.max(lo, a); has = true }
      if (!isNaN(b)) { hi = Math.min(hi, b); has = true }
      continue
    }
    const v = Number(c.value)
    if (isNaN(v)) continue
    switch (c.operator) {
      case 'gt': case 'gte': lo = Math.max(lo, v); has = true; break
      case 'lt': case 'lte': hi = Math.min(hi, v); has = true; break
      case 'eq': lo = Math.max(lo, v); hi = Math.min(hi, v); has = true; break
    }
  }
  return has ? { lo, hi } : null
}

// 从叶子条件求某字段允许的枚举值集合（eq/in 交集），无枚举约束返回 null
function enumSetFor(conds: any[], field: string): Set<any> | null {
  let values: any[] | null = null
  for (const c of conds) {
    if (c.field !== field) continue
    let s: any[] | null = null
    if (c.operator === 'eq') s = [c.value]
    else if (c.operator === 'in' && Array.isArray(c.value)) s = [...c.value]
    if (!s) continue
    const cur = s
    values = values === null ? cur : values.filter((x: any) => cur.includes(x))
  }
  return values === null ? null : new Set(values)
}

function conditionFields(conds: any[]): string[] {
  return Array.from(new Set(conds.map(c => c.field).filter(Boolean)))
}

// 两条规则是否可能同时命中：任一共享字段互斥（枚举取值不相交 / 数值区间不相交）→ 不重叠；
// 全部共享字段约束都可同时满足 → 潜在重叠（通用区间相交 + enum 互斥）
function rulesOverlap(left: any[], right: any[]): boolean {
  const shared = conditionFields(left).filter(f => conditionFields(right).includes(f))
  if (shared.length === 0) return false // 无共享约束字段：靠优先级即可，不判为重叠（避免噪声）
  for (const field of shared) {
    const lSet = enumSetFor(left, field)
    const rSet = enumSetFor(right, field)
    if (lSet && rSet && ![...lSet].some(x => rSet.has(x))) return false // 枚举互斥
    const lInt = numericIntervalFor(left, field)
    const rInt = numericIntervalFor(right, field)
    if (lInt && rInt && (lInt.lo > rInt.hi || rInt.lo > lInt.hi)) return false // 数值区间不相交
  }
  return true
}

/** 运行全部规则健康检查，返回诊断项（空则返回一条 ok）。纯函数，供面板与校验复用。 */
export function runRuleHealthChecks(ctx: RuleHealthContext): HealthItem[] {
  const { stages, routes, dynamicPolicies, fields } = ctx
  const stageKeys = new Set(stages.map(stage => stage.id))
  const fieldMap = new Map(fields.map(field => [field.key, field]))
  // 后端只对 active 路由做校验与运行时求值（FlowDefinitionService.ValidateRouteRulesAsync）
  const activeRoutes = routes.filter(route => (route.status ?? 'active') === 'active')
  // 规则模式判定：版本存在任一 active 路由即进入规则模式，否则按 sortOrder 线性推进
  const isRuleMode = activeRoutes.length > 0
  const stageName = (stageKey: string) => stages.find(stage => stage.id === stageKey)?.name || stageKey

  function checkDanglingRefs(): HealthItem[] {
    const result: HealthItem[] = []
    routes.forEach(route => {
      if (!stageKeys.has(route.fromStageKey) || !stageKeys.has(route.toStageKey)) {
        result.push({ level: 'error', title: '流转规则引用失效', detail: `「${route.routeName}」引用了已删除的节点，保存草稿会被后端拒绝。` })
      }
    })
    dynamicPolicies.forEach(policy => {
      if (!stageKeys.has(policy.sourceStageKey)) {
        result.push({ level: 'error', title: '动态策略引用失效', detail: `「${policy.policyName}」的来源节点已删除。` })
      }
    })
    return result
  }

  function checkDefaultRoutes(): HealthItem[] {
    const result: HealthItem[] = []
    const fromKeys = new Set(activeRoutes.map(route => route.fromStageKey))
    fromKeys.forEach(fromStageKey => {
      const defaults = activeRoutes.filter(route => route.fromStageKey === fromStageKey && route.isDefault)
      if (defaults.length === 0) {
        result.push({ level: 'error', title: '缺少默认分支', detail: `节点「${stageName(fromStageKey)}」配置了条件流转，但没有“其他情况”默认分支。` })
      } else if (defaults.length > 1) {
        result.push({ level: 'error', title: '默认分支重复', detail: `节点「${stageName(fromStageKey)}」有 ${defaults.length} 条默认分支，发布要求恰好一条。` })
      }
    })
    return result
  }

  function checkRouteCompleteness(): HealthItem[] {
    const result: HealthItem[] = []
    activeRoutes.forEach(route => {
      if (!route.isDefault && !route.conditionJson) {
        result.push({ level: 'error', title: '分支缺少条件', detail: `「${route.routeName}」不是默认分支但未配置流转条件，发布会被拒绝。` })
      }
    })
    const byFrom = new Map<string, number[]>()
    activeRoutes.forEach(route => {
      byFrom.set(route.fromStageKey, [...(byFrom.get(route.fromStageKey) || []), route.priority])
    })
    byFrom.forEach((priorities, fromStageKey) => {
      if (new Set(priorities).size !== priorities.length) {
        result.push({ level: 'warning', title: '优先级重复', detail: `节点「${stageName(fromStageKey)}」的条件分支存在重复优先级（保存时会自动按序重编）。` })
      }
    })
    return result
  }

  function checkOverlap(): HealthItem[] {
    const result: HealthItem[] = []
    const groups = new Map<string, StageRouteRuleRequest[]>()
    activeRoutes.filter(route => !route.isDefault).forEach(route => {
      groups.set(route.fromStageKey, [...(groups.get(route.fromStageKey) || []), route])
    })
    groups.forEach(group => {
      for (let i = 0; i < group.length; i++) {
        for (let j = i + 1; j < group.length; j++) {
          const left = flattenConditions(parseCondition(group[i].conditionJson))
          const right = flattenConditions(parseCondition(group[j].conditionJson))
          if (left.length === 0 || right.length === 0) continue
          if (rulesOverlap(left, right)) {
            result.push({ level: 'warning', title: '规则重叠', detail: `「${group[i].routeName}」和「${group[j].routeName}」的条件区间存在交集，可能同时命中，请确认优先级。` })
          }
        }
      }
    })
    return result
  }

  function checkGraph(): HealthItem[] {
    // 线性模式（无 active 路由）下运行时按 sortOrder 顺序推进，不存在死路/不可达问题
    if (!isRuleMode) return []
    const result: HealthItem[] = []
    const outgoing = new Map<string, string[]>()
    activeRoutes.forEach(route => {
      outgoing.set(route.fromStageKey, [...(outgoing.get(route.fromStageKey) || []), route.toStageKey])
    })
    stages.slice(0, -1).forEach(stage => {
      if (!outgoing.has(stage.id)) {
        result.push({ level: 'warning', title: '死路节点', detail: `节点「${stage.name || stage.id}」没有后续条件边，规则模式下运行到此会直接结束。` })
      }
    })
    const first = stages[0]?.id
    if (first) {
      const visited = new Set<string>()
      const visiting = new Set<string>()
      const dfs = (key: string) => {
        if (visiting.has(key)) {
          result.push({ level: 'error', title: '循环路径', detail: `检测到从「${stageName(key)}」回到已访问路径的循环。` })
          return
        }
        if (visited.has(key)) return
        visiting.add(key)
        ;(outgoing.get(key) || []).forEach(next => dfs(next))
        visiting.delete(key)
        visited.add(key)
      }
      dfs(first)
      stages.forEach(stage => {
        if (!visited.has(stage.id)) {
          // 后端 RouteGraphValidator 将不可达视为发布阻断错误，与其保持一致
          result.push({ level: 'error', title: '无法到达', detail: `节点「${stage.name || stage.id}」不在起点可达路径上，发布会被拒绝。` })
        }
      })
    }
    return result
  }

  function checkHandlers(): HealthItem[] {
    const result: HealthItem[] = []
    dynamicPolicies.forEach(policy => {
      const fallback = parseCondition(policy.fallbackJson)
      if (!fallback?.type) {
        result.push({ level: 'error', title: '处理人兜底缺失', detail: `动态策略「${policy.policyName}」没有 fallback，处理人解析失败时无法安全兜底。` })
      }
      // 后端发布校验：afterRouteBeforeTarget 时机必须配置续接节点
      if ((policy.triggerTiming || 'afterRouteBeforeTarget') === 'afterRouteBeforeTarget' && !policy.continuationStageKey) {
        result.push({ level: 'error', title: '缺少继续节点', detail: `动态策略「${policy.policyName}」触发时机为"路由后、目标前"，必须选择继续节点，否则发布会被拒绝。` })
      }
    })
    stages.filter(stage => stage.type === 'manual').forEach(stage => {
      if (!stage.assigneeStrategy) {
        result.push({ level: 'error', title: '处理人策略缺失', detail: `人工节点「${stage.name || stage.id}」没有配置处理人策略。` })
      }
    })
    return result
  }

  function checkTypeMismatch(): HealthItem[] {
    const result: HealthItem[] = []
    const numericOperators = new Set(['gt', 'gte', 'lt', 'lte'])
    // 后端 ConditionRuleEvaluator 的合法寻址前缀，这些字段不在卡片 schema 里但运行时可解析
    const runtimePrefixes = ['detailSummary.', 'source.', 'initiator.', 'orgChain', 'roles.']
    const inspect = (owner: string, json?: string | null) => {
      flattenConditions(parseCondition(json)).forEach(condition => {
        const rawField = String(condition.field || '')
        if (runtimePrefixes.some(prefix => rawField.startsWith(prefix))) return
        // card. 前缀与裸字段名等价，剥掉前缀后查 schema
        const key = rawField.startsWith('card.') ? rawField.slice(5) : rawField
        const field = fieldMap.get(key)
        if (!field) {
          result.push({ level: 'error', title: '字段不存在', detail: `${owner} 引用了不存在的字段「${rawField}」。` })
          return
        }
        // 后端 CompareOrdered 同时支持数值与日期比较
        if (numericOperators.has(condition.operator) && !['money', 'number', 'date'].includes(field.type)) {
          result.push({ level: 'error', title: '字段类型不匹配', detail: `${owner} 使用 ${condition.operator} 比较非金额/数字/日期字段「${field.label}」。` })
        }
      })
    }
    activeRoutes.forEach(route => inspect(`流转规则「${route.routeName}」`, route.conditionJson))
    dynamicPolicies.forEach(policy => inspect(`动态策略「${policy.policyName}」`, policy.conditionJson))
    return result
  }

  const result: HealthItem[] = []
  result.push(...checkDanglingRefs())
  result.push(...checkDefaultRoutes())
  result.push(...checkRouteCompleteness())
  result.push(...checkOverlap())
  result.push(...checkGraph())
  result.push(...checkHandlers())
  result.push(...checkTypeMismatch())
  if (!result.length) {
    result.push({ level: 'ok', title: '规则健康', detail: '默认分支、条件完整性、死路节点、循环路径、无法到达节点和处理人策略均未发现明显问题。' })
  }
  return result
}

// ==================== 发布校验（CardFlow2 配置）====================
// 抽自 FlowDefinitionEditPage.validateCardFlow2Config，纯函数，返回中文风险文案（string[]）。
// 与 runRuleHealthChecks 同源，避免"节点链校验"与"规则健康面板"两处逐步漂移。

export interface PublishValidationContext {
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  cardSchema: SchemaFieldDefinition[]
  detailSchema: SchemaFieldDefinition[]
  cardComponents: CardComponentDefinition[]
  approvalAdminUserIds: number[]
}

function tryParseObject(json?: string | null): any {
  if (!json) return null
  try {
    const parsed = JSON.parse(json)
    return parsed && typeof parsed === 'object' ? parsed : null
  } catch {
    return null
  }
}

function collectConditionFields(condition: any, fields: Set<string>) {
  if (!condition) return
  if (Array.isArray(condition.conditions)) {
    for (const item of condition.conditions) collectConditionFields(item, fields)
    return
  }
  if (typeof condition.field === 'string' && condition.field) {
    fields.add(condition.field)
  }
}

/** 发布/预览前的配置风险校验，返回中文风险文案；空数组表示可发布。 */
export function validatePublishConfig(ctx: PublishValidationContext): string[] {
  const { stages, routes, dynamicPolicies, cardSchema, detailSchema, cardComponents, approvalAdminUserIds } = ctx

  const validateStageReferenceKeys = (stage: StageDefinition, index: number): string[] => {
    const msgs: string[] = []
    const cardKeys = new Set(cardSchema.map(field => field.key))
    const detailKeys = new Set(detailSchema.map(field => `default.${field.key}`))
    const stageName = stage.name || `第 ${index + 1} 个节点`

    for (const key of stage.inputFields || []) {
      if (!cardKeys.has(key)) msgs.push(`节点[${stageName}]补充字段[${key}]不存在`)
    }

    for (const [key, rule] of Object.entries(stage.viewProfile?.fieldAccess || {})) {
      const accessRule = rule as any
      if (!cardKeys.has(key)) msgs.push(`节点[${stageName}]字段权限[${key}]不存在`)
      if ((accessRule.access === 'hidden' || accessRule.access === 'masked') && accessRule.required) {
        msgs.push(`节点[${stageName}]字段权限[${key}]不能同时隐藏/脱敏且必填`)
      }
    }

    for (const [key, rule] of Object.entries(stage.viewProfile?.detailAccess || {})) {
      const accessRule = rule as any
      if (!detailKeys.has(key)) msgs.push(`节点[${stageName}]明细字段权限[${key}]不存在`)
      if ((accessRule.access === 'hidden' || accessRule.access === 'masked') && accessRule.required) {
        msgs.push(`节点[${stageName}]明细字段权限[${key}]不能同时隐藏/脱敏且必填`)
      }
    }

    for (const key of stage.viewProfile?.summary?.fields || []) {
      if (!cardKeys.has(key)) msgs.push(`节点[${stageName}]摘要字段[${key}]不存在`)
    }

    if (stage.conditionJson) {
      const condition = tryParseObject(stage.conditionJson)
      const fields = new Set<string>()
      collectConditionFields(condition, fields)
      for (const key of fields) {
        if (!cardKeys.has(key)) msgs.push(`节点[${stageName}]进入条件字段[${key}]不存在`)
      }
    }

    return msgs
  }

  const validateCardComponentPublishability = (): string[] => {
    const msgs: string[] = []
    cardComponents.forEach((component, index) => {
      const componentName = component.title || `第 ${index + 1} 个组件`
      const capability = resolveComponentCapability(component.type, component.props || {})
      const componentStatus = component.props?.componentStatus || (capability.publishable ? 'ready' : 'deferred')
      const requiresRuntimeIntegration = !!(component.props?.requiresRuntimeIntegration || capability.requiresRuntimeIntegration)

      if (!capability.publishable || component.props?.publishable === false || componentStatus === 'deferred' || requiresRuntimeIntegration) {
        msgs.push(`组件[${componentName}]暂未支持发布：${capability.unsupportedReason || component.props?.unsupportedReason || '缺少运行态集成能力'}`)
      }

      if (component.binding?.source && !capability.supportedBindings.includes(component.binding.source)) {
        msgs.push(`组件[${componentName}]绑定来源[${component.binding.source}]不符合该组件能力边界`)
      }
    })
    return msgs
  }

  const msgs: string[] = []
  const stageKeys = new Set(stages.map(stage => stage.id))
  msgs.push(...validateCardComponentPublishability())
  stages.forEach((stage, index) => {
    const stageName = stage.name || `第 ${index + 1} 个节点`
    if (!stage.name?.trim()) msgs.push(`节点[${stageName}]名称不能为空`)

    if (stage.type === 'manual') {
      const config = tryParseObject(stage.assigneeConfigJson)
      if (!stage.assigneeStrategy) {
        msgs.push(`节点[${stageName}]处理人策略未配置`)
      } else if (stage.assigneeStrategy === 'role' && !config?.roleCode) {
        msgs.push(`节点[${stageName}]按角色处理人未选择角色`)
      } else if (stage.assigneeStrategy === 'fixed' && !(config?.users || []).length) {
        msgs.push(`节点[${stageName}]指定人员未选择处理人`)
      } else if (stage.assigneeStrategy === 'fieldUsers' && !config?.fieldKey) {
        msgs.push(`节点[${stageName}]按字段取人未选择人员字段`)
      }

      if (config?.fallback?.type === 'flowAdmin' && approvalAdminUserIds.length === 0) {
        msgs.push(`节点[${stageName}]使用审批管理员兜底，但流程配置未选择审批管理员`)
      }

      if (!stage.actionPolicy?.allowedActions?.length) {
        msgs.push(`节点[${stageName}]允许动作不能为空`)
      }

      if (stage.ccConfigJson && !tryParseObject(stage.ccConfigJson)) {
        msgs.push(`节点[${stageName}]抄送配置不是合法的 JSON 对象`)
      }

      msgs.push(...validateStageReferenceKeys(stage, index))
    }

    if (stage.type === 'auto' && !stage.pluginRegistryId) {
      msgs.push(`节点[${stageName}]自动插件未选择`)
    }
  })

  const routeSourceKeys = new Set(routes.map(route => route.fromStageKey))
  routeSourceKeys.forEach(sourceKey => {
    if (!routes.some(route => route.fromStageKey === sourceKey && route.isDefault)) {
      msgs.push(`节点[${stages.find(stage => stage.id === sourceKey)?.name || sourceKey}]条件流转缺少默认分支`)
    }
  })
  routes.forEach(route => {
    if (!stageKeys.has(route.fromStageKey)) msgs.push(`流转规则[${route.routeName}]来源节点不存在`)
    if (!stageKeys.has(route.toStageKey)) msgs.push(`流转规则[${route.routeName}]目标节点不存在`)
    if (!route.isDefault && !route.conditionJson) msgs.push(`流转规则[${route.routeName}]未配置流转条件`)
  })
  dynamicPolicies.forEach(policy => {
    if (!stageKeys.has(policy.sourceStageKey)) msgs.push(`动态策略[${policy.policyName}]来源节点不存在`)
    if (!policy.fallbackJson) msgs.push(`动态策略[${policy.policyName}]未配置处理人 fallback`)
    if ((policy.maxInsertCount || 20) > 20) msgs.push(`动态策略[${policy.policyName}]最大插入数不能超过 20`)
    // 后端发布校验：afterRouteBeforeTarget 时机必须配置续接节点，否则发布必失败
    const timing = policy.triggerTiming || 'afterRouteBeforeTarget'
    if (timing === 'afterRouteBeforeTarget' && !policy.continuationStageKey) {
      msgs.push(`动态策略[${policy.policyName}]触发时机为"路由后、目标前"时必须选择继续节点`)
    } else if (policy.continuationStageKey && !stageKeys.has(policy.continuationStageKey)) {
      msgs.push(`动态策略[${policy.policyName}]的继续节点不存在`)
    }
  })
  return msgs
}
