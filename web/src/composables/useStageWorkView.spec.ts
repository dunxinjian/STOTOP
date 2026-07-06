import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import type { CardDetailDto, SchemaFieldDefinition, StageWorkView } from '@/types/cardflow'
import { useStageWorkView } from './useStageWorkView'

/**
 * useStageWorkView 两端合一「先写两套旧实现对照快照」测试（B9-2）。
 *
 * 目的：在把 CardFlowPanel（PC）与 MobileCardApprovalPage（移动端）两处逐字重复的
 * StageWorkView 消费逻辑收敛进 composable 前，先把两端旧实现【逐字转写】为参照函数、
 * 对固定 fixture 求值锁为金标准，再断言 composable（按 PC / 移动两套 options）字节复现之。
 * 由此保证本次是「行为保持」的结构合一：两端各自输出零变化，且 D1/D6 分歧被显式锁定为
 * 可见的差异断言——日后若统一到后端语义（改 D1），本测试会红，成为逐条拍板的强制关卡。
 */

// ── 逐字转写：CardFlowPanel.vue 旧实现（PC） ──────────────────────────────
function pcOldApplyFieldAccess(schema: SchemaFieldDefinition[], wv: StageWorkView | null): SchemaFieldDefinition[] {
  const access = wv?.fieldAccess
  if (!access) return schema
  return schema
    .filter((field) => access[field.key]?.access !== 'hidden')
    .map((field) => {
      const rule = access[field.key]
      if (!rule) return field
      const writable = rule.access === 'editable' || rule.access === 'required'
      return { ...field, readonly: !writable || field.readonly, required: rule.required ?? field.required }
    })
}
function pcOldStageInputFields(cardSchema: SchemaFieldDefinition[], wv: StageWorkView | null, mode: string): SchemaFieldDefinition[] {
  if (mode !== 'approval') return []
  const access = wv?.fieldAccess
  if (!access) return []
  return cardSchema
    .filter((field) => { const rule = access[field.key]; return rule?.access === 'editable' || rule?.access === 'required' })
    .map((field) => ({ ...field, readonly: false, required: access[field.key]?.required ?? (access[field.key]?.access === 'required') }))
}

// ── 逐字转写：MobileCardApprovalPage.vue 旧实现（移动端） ─────────────────
function mobileWritableAccess(access: string | undefined) { return access === 'editable' || access === 'required' }
function mobileOldApplyFieldAccess(schema: SchemaFieldDefinition[], wv: StageWorkView | null): SchemaFieldDefinition[] {
  const access = wv?.fieldAccess
  if (!access) return schema
  return schema
    .filter((field) => access[field.key]?.access !== 'hidden')
    .map((field) => {
      const rule = access[field.key]
      if (!rule) return field
      const writable = mobileWritableAccess(rule.access)
      return { ...field, readonly: !writable || field.readonly, required: rule.required ?? (rule.access === 'required') }
    })
}
function mobileOldStageInputFields(base: SchemaFieldDefinition[], wv: StageWorkView | null, legacyKeys: string[]): SchemaFieldDefinition[] {
  const access = wv?.fieldAccess
  if (access) {
    return base
      .filter((field) => mobileWritableAccess(access[field.key]?.access))
      .map((field) => ({ ...field, required: access[field.key]?.required ?? (access[field.key]?.access === 'required') }))
  }
  return base.filter((field) => legacyKeys.includes(field.key))
}

// detailAccess / canStageAction 两端逐字相同，转写一份既作 PC 也作移动端参照。
function oldApplyDetailAccess(schema: SchemaFieldDefinition[], wv: StageWorkView | null): SchemaFieldDefinition[] {
  const access = wv?.detailAccess
  if (!access) return schema
  return schema
    .filter((field) => {
      const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
      return rules.length === 0 || rules.some(([, rule]) => rule.access !== 'hidden')
    })
    .map((field) => {
      const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
      if (rules.length === 0) return field
      const editable = rules.some(([, rule]) => mobileWritableAccess(rule.access))
      const required = rules.some(([, rule]) => rule.required || rule.access === 'required')
      return { ...field, readonly: !editable || field.readonly, required: required || field.required }
    })
}
function oldCanStageAction(action: string, wv: StageWorkView | null): boolean {
  const allowed = wv?.actionPolicy?.allowedActions ?? null
  if (!allowed || allowed.length === 0) return true
  return allowed.some((item) => item.toLowerCase() === action.toLowerCase())
}

// ── Fixtures ────────────────────────────────────────────────────────────
const CARD_SCHEMA: SchemaFieldDefinition[] = [
  { key: 'title', label: '标题', type: 'text' },                    // 无规则 → 透传
  { key: 'amt', label: '金额', type: 'money', required: false },     // access=required, required 省略 → D1
  { key: 'note', label: '备注', type: 'textarea', required: true },  // access=editable, required 省略 → D1
  { key: 'secret', label: '密', type: 'text' },                      // access=hidden → 过滤
  { key: 'ro', label: '只读', type: 'text', required: true },        // access=readonly → 非写, required 兜底 → D1
  { key: 'exp', label: '显式', type: 'text', required: false },      // access=editable + required:true → 两端一致
]
const FIELD_ACCESS = {
  amt: { access: 'required' as const },
  note: { access: 'editable' as const },
  secret: { access: 'hidden' as const },
  ro: { access: 'readonly' as const },
  exp: { access: 'editable' as const, required: true },
}
const DETAIL_SCHEMA: SchemaFieldDefinition[] = [
  { key: 'qty', label: '数量', type: 'number' },
  { key: 'price', label: '单价', type: 'money' },
  { key: 'hiddenCol', label: '隐藏列', type: 'text' },
  { key: 'note', label: '备注', type: 'text' },   // 经后缀匹配 t2.note
  { key: 'free', label: '无规则', type: 'text' },  // 无匹配规则 → 透传
]
const DETAIL_ACCESS = {
  'default.qty': { access: 'required' as const },
  'default.price': { access: 'editable' as const },
  'default.hiddenCol': { access: 'hidden' as const },
  't2.note': { access: 'readonly' as const, required: true },
}
const WORK_VIEW = {
  sections: [],
  fieldAccess: FIELD_ACCESS,
  detailAccess: DETAIL_ACCESS,
  components: [{ id: 'c1' }, { id: 'c2' }],
  detailSummary: {},
  actionPolicy: { allowedActions: ['approve', 'Reject'] },
} as unknown as StageWorkView

const detailRef = (wv: StageWorkView | null) =>
  ref<CardDetailDto | null>({ currentStageWorkView: wv } as unknown as CardDetailDto)

describe('useStageWorkView —— 与两端旧实现字节对照', () => {
  it('PC 配置（cardRequiredFallback=schema）复现 CardFlowPanel 旧 applyFieldAccess', () => {
    const sv = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' })
    expect(sv.applyFieldAccess(CARD_SCHEMA)).toEqual(pcOldApplyFieldAccess(CARD_SCHEMA, WORK_VIEW))
  })

  it('移动配置（cardRequiredFallback=access）复现 MobileCardApprovalPage 旧 applyFieldAccess', () => {
    const sv = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'access' })
    expect(sv.applyFieldAccess(CARD_SCHEMA)).toEqual(mobileOldApplyFieldAccess(CARD_SCHEMA, WORK_VIEW))
  })

  it('D1 分歧被显式锁定：required 省略时 PC 落 field.required、移动落 access===required', () => {
    const pc = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' }).applyFieldAccess(CARD_SCHEMA)
    const mobile = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'access' }).applyFieldAccess(CARD_SCHEMA)
    const pcOf = (k: string) => pc.find((f) => f.key === k)
    const mbOf = (k: string) => mobile.find((f) => f.key === k)
    // amt: access=required, field.required=false → PC=false（旧 bug）, 移动=true（后端语义）
    expect(pcOf('amt')?.required).toBe(false)
    expect(mbOf('amt')?.required).toBe(true)
    // note: access=editable, field.required=true → PC=true, 移动=false
    expect(pcOf('note')?.required).toBe(true)
    expect(mbOf('note')?.required).toBe(false)
    // ro: access=readonly（非写）, field.required=true → 两端 readonly:true；required PC=true / 移动=false
    expect(pcOf('ro')?.readonly).toBe(true)
    expect(mbOf('ro')?.readonly).toBe(true)
    expect(pcOf('ro')?.required).toBe(true)
    expect(mbOf('ro')?.required).toBe(false)
    // exp: 显式 required:true → 两端一致
    expect(pcOf('exp')?.required).toBe(true)
    expect(mbOf('exp')?.required).toBe(true)
    // secret hidden → 两端均过滤
    expect(pcOf('secret')).toBeUndefined()
    expect(mbOf('secret')).toBeUndefined()
    // title 无规则 → 两端透传
    expect(pcOf('title')).toEqual({ key: 'title', label: '标题', type: 'text' })
  })

  it('applyDetailAccess 两端一致，且 composable 复现旧实现', () => {
    const pc = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' })
    const mobile = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'access' })
    const golden = oldApplyDetailAccess(DETAIL_SCHEMA, WORK_VIEW)
    expect(pc.applyDetailAccess(DETAIL_SCHEMA)).toEqual(golden)
    expect(mobile.applyDetailAccess(DETAIL_SCHEMA)).toEqual(golden)
    // hidden 列过滤、无规则列透传
    expect(golden.find((f) => f.key === 'hiddenCol')).toBeUndefined()
    expect(golden.find((f) => f.key === 'free')).toEqual({ key: 'free', label: '无规则', type: 'text' })
  })

  it('buildStageInputFields —— PC 配置复现旧 stageInputFields（含 mode 门控与 readonly:false）', () => {
    const sv = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' })
    // 审批态：enabled + forceEditable
    expect(sv.buildStageInputFields(CARD_SCHEMA, { enabled: true, forceEditable: true }))
      .toEqual(pcOldStageInputFields(CARD_SCHEMA, WORK_VIEW, 'approval'))
    // 非审批态：mode!=='approval' → []
    expect(sv.buildStageInputFields(CARD_SCHEMA, { enabled: false, forceEditable: true }))
      .toEqual(pcOldStageInputFields(CARD_SCHEMA, WORK_VIEW, 'view'))
  })

  it('buildStageInputFields —— 移动配置复现旧 stageInputFields（无 readonly、含 legacy 回退）', () => {
    const sv = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'access' })
    const legacy = ['title', 'amt']
    // fieldAccess 存在：legacy 忽略
    expect(sv.buildStageInputFields(CARD_SCHEMA, { legacyKeys: () => legacy }))
      .toEqual(mobileOldStageInputFields(CARD_SCHEMA, WORK_VIEW, legacy))
    // D6：PC 命中项带 readonly:false，移动端不带
    const pcFirst = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' })
      .buildStageInputFields(CARD_SCHEMA, { enabled: true, forceEditable: true })[0]
    const mbFirst = sv.buildStageInputFields(CARD_SCHEMA, { legacyKeys: () => legacy })[0]
    expect(pcFirst).toHaveProperty('readonly', false)
    expect(Object.prototype.hasOwnProperty.call(mbFirst, 'readonly')).toBe(false)
  })

  it('buildStageInputFields —— 无 fieldAccess 时 PC 返回 []、移动端走 legacyKeys', () => {
    const sv = useStageWorkView(detailRef(null), { cardRequiredFallback: 'schema' })
    // PC：无 access 且无 legacyKeys → []
    expect(sv.buildStageInputFields(CARD_SCHEMA, { enabled: true, forceEditable: true }))
      .toEqual(pcOldStageInputFields(CARD_SCHEMA, null, 'approval'))
    // 移动端：无 access → 按 legacy 键过滤 base
    const legacy = ['title', 'amt']
    expect(sv.buildStageInputFields(CARD_SCHEMA, { legacyKeys: () => legacy }))
      .toEqual(mobileOldStageInputFields(CARD_SCHEMA, null, legacy))
    expect(sv.buildStageInputFields(CARD_SCHEMA, { legacyKeys: () => legacy }).map((f) => f.key)).toEqual(['title', 'amt'])
  })

  it('runtimeComponents / hasRuntimeComponents / canStageAction 两端一致', () => {
    const sv = useStageWorkView(detailRef(WORK_VIEW), { cardRequiredFallback: 'schema' })
    expect(sv.runtimeComponents.value.length).toBe(2)
    expect(sv.hasRuntimeComponents.value).toBe(true)
    for (const a of ['approve', 'reject', 'REJECT', 'transfer', 'cc']) {
      expect(sv.canStageAction(a)).toBe(oldCanStageAction(a, WORK_VIEW))
    }
    // allowedActions=['approve','Reject'] → approve/reject（大小写不敏感）放行，transfer 拦
    expect(sv.canStageAction('approve')).toBe(true)
    expect(sv.canStageAction('REJECT')).toBe(true)
    expect(sv.canStageAction('transfer')).toBe(false)

    // 空 work view → 全放行
    const empty = useStageWorkView(detailRef(null), { cardRequiredFallback: 'schema' })
    expect(empty.canStageAction('transfer')).toBe(true)
    expect(empty.hasRuntimeComponents.value).toBe(false)
    expect(empty.runtimeComponents.value).toEqual([])
  })
})
