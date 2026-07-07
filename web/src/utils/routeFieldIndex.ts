/**
 * 路由条件对字段/枚举选项的引用索引（plan M4-1，设计屏 2「路由依赖锁定」）。
 * 消费方：
 * - SchemaFieldEditor：删字段/改类型/取消必填/删被引用选项 → 拦截并列出引用分支（E0-M2 强度）；
 * - StageConfigPanel 字段权限：🔗 路由字段锁只读；
 * - FieldPermissionMatrix（M4-2）：行头 🔗 标记。
 */
import type { StageRouteRuleRequest } from '@/types/cardflow'

export interface RouteFieldIndex {
  /** fieldKey → 引用它的 route edgeKey 列表（有序去重） */
  fields: Map<string, string[]>
  /** `${fieldKey}.${optionValue}` → 引用该枚举值的 edgeKey 列表（仅字符串候选值） */
  options: Map<string, string[]>
}

interface RawCondition {
  field?: unknown
  operator?: unknown
  value?: unknown
  logic?: unknown
  conditions?: unknown
}

function push(map: Map<string, string[]>, key: string, edgeKey: string) {
  const list = map.get(key) ?? []
  if (!list.includes(edgeKey)) list.push(edgeKey)
  map.set(key, list)
}

function collect(node: RawCondition, edgeKey: string, idx: RouteFieldIndex) {
  if (Array.isArray(node.conditions)) {
    for (const child of node.conditions) {
      if (child && typeof child === 'object') collect(child as RawCondition, edgeKey, idx)
    }
    return
  }
  if (typeof node.field !== 'string' || !node.field) return
  push(idx.fields, node.field, edgeKey)
  const values = Array.isArray(node.value) ? node.value : [node.value]
  for (const v of values) {
    if (typeof v === 'string' && v) push(idx.options, `${node.field}.${v}`, edgeKey)
  }
}

export function buildRouteFieldIndex(routes: StageRouteRuleRequest[]): RouteFieldIndex {
  const idx: RouteFieldIndex = { fields: new Map(), options: new Map() }
  for (const route of routes) {
    if ((route.status ?? 'active') !== 'active') continue
    if (!route.conditionJson) continue
    try {
      const parsed = JSON.parse(route.conditionJson)
      if (parsed && typeof parsed === 'object') collect(parsed, route.edgeKey, idx)
    } catch {
      // 非法 JSON 由诊断面板负责报告，索引容错跳过
    }
  }
  return idx
}
