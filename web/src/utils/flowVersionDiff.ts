/**
 * 流程版本结构 diff（plan M6-1，mock B6 变更清单 / E1 条件值级 diff）。
 * 纯函数、零项目内耦合（VersionSnapshot 自带最小形状），供发布确认弹窗与版本对比复用。
 */

export interface DiffStage {
  id: string
  name?: string
  type?: string
}

export interface DiffRoute {
  edgeKey: string
  routeName?: string
  conditionJson?: string | null
  priority?: number
  isDefault?: boolean
}

export interface DiffField {
  key: string
  label?: string
  type?: string
  required?: boolean
}

export interface VersionSnapshot {
  stages: DiffStage[]
  routes: DiffRoute[]
  fields: DiffField[]
}

export interface DiffChange {
  /** 变更维度标签，如「名称」「类型」「条件」「优先级」 */
  label: string
  /** 旧值（渲染为删除线） */
  before: string
  /** 新值（渲染为加粗） */
  after: string
}

export interface ChangeItem {
  kind: 'add' | 'modify' | 'remove'
  scope: 'stage' | 'route' | 'field'
  label: string
  /** 修改类的「旧 → 新」内联说明（纯文本，向后兼容） */
  detail?: string
  /** 修改类的结构化变更（条件值级 diff：旧删除线→新加粗，E1/P-2） */
  changes?: DiffChange[]
}

/** 条件表达式简写：取 conditionJson 内首条叶子条件的 `字段 算子 值`（diff 展示用，不追求完整） */
function briefCondition(json?: string | null): string {
  if (!json) return '无条件'
  try {
    const parsed = JSON.parse(json)
    const first = firstLeaf(parsed)
    if (!first) return '无条件'
    const val = Array.isArray(first.value) ? `[${first.value.join(',')}]` : String(first.value ?? '—')
    return `${first.field} ${first.operator} ${val}`
  } catch {
    return '条件'
  }
}

function firstLeaf(node: unknown): { field: string; operator: string; value: unknown } | null {
  if (!node || typeof node !== 'object') return null
  const obj = node as Record<string, unknown>
  if ('field' in obj && 'operator' in obj) {
    return { field: String(obj.field), operator: String(obj.operator), value: obj.value }
  }
  const conds = obj.conditions
  if (Array.isArray(conds)) {
    for (const c of conds) {
      const leaf = firstLeaf(c)
      if (leaf) return leaf
    }
  }
  return null
}

function typeLabel(type?: string): string {
  return type === 'auto' ? '自动' : type === 'manual' ? '人工' : (type ?? '')
}

/** 结构化变更 → 纯文本 detail（向后兼容旧渲染） */
function joinDetail(changes: DiffChange[]): string {
  return changes.map((c) => `${c.label} ${c.before} → ${c.after}`).join('；')
}

/** 两版本快照结构 diff：stage 按 id / route 按 edgeKey / field 按 key 匹配 */
export function diffFlowVersions(oldVer: VersionSnapshot, newVer: VersionSnapshot): ChangeItem[] {
  const out: ChangeItem[] = []

  // ── stages ──
  const oldStages = new Map(oldVer.stages.map((s) => [s.id, s]))
  const newStages = new Map(newVer.stages.map((s) => [s.id, s]))
  for (const s of newVer.stages) {
    const prev = oldStages.get(s.id)
    if (!prev) {
      out.push({ kind: 'add', scope: 'stage', label: `节点「${s.name || s.id}」` })
    } else {
      const changes: DiffChange[] = []
      if ((prev.name || '') !== (s.name || '')) changes.push({ label: '名称', before: prev.name || '—', after: s.name || '—' })
      if ((prev.type || '') !== (s.type || '')) changes.push({ label: '类型', before: typeLabel(prev.type), after: typeLabel(s.type) })
      if (changes.length) out.push({ kind: 'modify', scope: 'stage', label: `节点「${s.name || s.id}」`, changes, detail: joinDetail(changes) })
    }
  }
  for (const s of oldVer.stages) {
    if (!newStages.has(s.id)) out.push({ kind: 'remove', scope: 'stage', label: `节点「${s.name || s.id}」` })
  }

  // ── routes ──
  const oldRoutes = new Map(oldVer.routes.map((r) => [r.edgeKey, r]))
  const newRoutes = new Map(newVer.routes.map((r) => [r.edgeKey, r]))
  for (const r of newVer.routes) {
    const prev = oldRoutes.get(r.edgeKey)
    const name = r.routeName || (r.isDefault ? '其他情况' : '条件分支')
    if (!prev) {
      out.push({ kind: 'add', scope: 'route', label: `分支「${name}」` })
    } else {
      const changes: DiffChange[] = []
      if ((prev.conditionJson || '') !== (r.conditionJson || '')) {
        changes.push({ label: '条件', before: briefCondition(prev.conditionJson), after: briefCondition(r.conditionJson) })
      }
      if ((prev.priority ?? 0) !== (r.priority ?? 0)) changes.push({ label: '优先级', before: String(prev.priority ?? '—'), after: String(r.priority ?? '—') })
      if (changes.length) out.push({ kind: 'modify', scope: 'route', label: `分支「${name}」`, changes, detail: joinDetail(changes) })
    }
  }
  for (const r of oldVer.routes) {
    if (!newRoutes.has(r.edgeKey)) {
      out.push({ kind: 'remove', scope: 'route', label: `分支「${r.routeName || (r.isDefault ? '其他情况' : '条件分支')}」` })
    }
  }

  // ── fields ──
  const oldFields = new Map(oldVer.fields.map((f) => [f.key, f]))
  const newFields = new Map(newVer.fields.map((f) => [f.key, f]))
  for (const f of newVer.fields) {
    const prev = oldFields.get(f.key)
    if (!prev) {
      out.push({ kind: 'add', scope: 'field', label: `字段「${f.label || f.key}」` })
    } else {
      const changes: DiffChange[] = []
      if ((prev.label || '') !== (f.label || '')) changes.push({ label: '名称', before: prev.label || '—', after: f.label || '—' })
      if ((prev.type || '') !== (f.type || '')) changes.push({ label: '类型', before: prev.type || '—', after: f.type || '—' })
      if (!!prev.required !== !!f.required) changes.push({ label: '必填', before: prev.required ? '必填' : '非必填', after: f.required ? '必填' : '非必填' })
      if (changes.length) out.push({ kind: 'modify', scope: 'field', label: `字段「${f.label || f.key}」`, changes, detail: joinDetail(changes) })
    }
  }
  for (const f of oldVer.fields) {
    if (!newFields.has(f.key)) out.push({ kind: 'remove', scope: 'field', label: `字段「${f.label || f.key}」` })
  }

  return out
}
