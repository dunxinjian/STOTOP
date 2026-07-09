<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { PlusOutlined, DeleteOutlined, SubnodeOutlined } from '@ant-design/icons-vue'
import type { RouteHitEstimateDto, SchemaEnumOption } from '@/types/cardflow'
import { estimateRouteHit } from '@/api/cardflow'
import { normalizeOptionList } from '@/utils/cardflowFieldFormat'
import UserSelect from '@/components/cardflow/fields/UserSelect.vue'
import OrgSelect from '@/components/cardflow/fields/OrgSelect.vue'
import {
  OPERATOR_LABELS,
  CONDITION_TYPE_LABELS,
  CONDITION_TYPE_ICONS,
  VALUELESS_OPERATORS,
  SYSTEM_CONDITION_FIELDS,
  getOperatorsForType,
  getDisabledReason,
  isTypeConditionable,
  isGroupNode,
  sanitizeConditionGroup,
} from './conditionBuilderShared'

// ==================== 类型定义 ====================

export interface FieldOption {
  key: string
  label: string
  type: string
  /** enum 类型字段的可选值（用于渲染值选择器，兼容 string[] 与 {label,value}[]） */
  options?: Array<string | SchemaEnumOption>
  /** 是否为必填字段（C8：仅当父级显式提供时，非必填字段禁选并给"去设为必填"链接） */
  required?: boolean
}

export interface ConditionItem {
  field: string
  operator: string
  value: any
}

export interface ConditionGroup {
  logic: 'and' | 'or'
  conditions: (ConditionItem | ConditionGroup)[]
}

interface Props {
  modelValue: ConditionGroup
  fields: FieldOption[]
  disabled?: boolean
  /** 流程定义 ID：传入后条件变化时防抖调用命中率试算端点（B4/E6） */
  definitionId?: number
  /** 内部使用：嵌套层级 */
  _depth?: number
}

// ==================== Props & Emits ====================

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  _depth: 0,
})

const emit = defineEmits<{
  'update:modelValue': [val: ConditionGroup]
  /** 跳转请求：去卡片设计设为必填（mock C8 "去卡片设计设为必填 →"链接） */
  'navigate-field': [fieldKey: string]
}>()

// ==================== 响应式数据 ====================

const group = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

// ==================== 显示块（条件组容器）====================
// 数据结构不变（ConditionGroup 递归）；渲染时把顶层 children 归并为「条件组」块：
// 子组 = 独立组容器；顶层散落的 ConditionItem（既有扁平数据）连续段合并为一个隐式组（单组渲染）。

interface DisplayRow {
  node: ConditionItem | ConditionGroup
  /** implicit 块 = 顶层 conditions 下标；group 块 = 子组 conditions 内下标 */
  index: number
}

interface DisplayBlock {
  kind: 'implicit' | 'group'
  /** group 块在顶层 conditions 中的下标 */
  topIndex?: number
  /** 块内条件的连接逻辑（隐式块 = 顶层 logic；子组 = 自身 logic） */
  logic: 'and' | 'or'
  rows: DisplayRow[]
}

const blocks = computed<DisplayBlock[]>(() => {
  const result: DisplayBlock[] = []
  let implicit: DisplayBlock | null = null
  group.value.conditions.forEach((node, i) => {
    if (isGroupNode(node)) {
      implicit = null
      result.push({
        kind: 'group',
        topIndex: i,
        logic: node.logic,
        rows: node.conditions.map((child, j) => ({ node: child, index: j })),
      })
    } else {
      if (!implicit) {
        implicit = { kind: 'implicit', logic: group.value.logic, rows: [] }
        result.push(implicit)
      }
      implicit.rows.push({ node, index: i })
    }
  })
  return result
})

// ==================== 字段下拉三组（C8：表单字段 / 系统字段；派生字段暂无来源不渲染） ====================
// 系统字段键名 initiator.id / initiatorOrg.id 已对 ConditionContextFactory 核实（见 conditionBuilderShared）。

interface FieldChoice extends FieldOption {
  disabled: boolean
  disabledReason: string
  /** 禁用原因类型：type=类型不支持，required=非必填可去修复 */
  disabledKind?: 'type' | 'required'
}

function toChoice(f: FieldOption): FieldChoice {
  const typeOk = isTypeConditionable(f.type)
  if (!typeOk) return { ...f, disabled: true, disabledReason: getDisabledReason(f.type), disabledKind: 'type' }
  // 非必填字段（父级传 required=false 时）：灰显+可去修复链接；required 未提供时不限制
  if (f.required === false) return { ...f, disabled: true, disabledReason: '非必填字段不可作路由条件', disabledKind: 'required' }
  return { ...f, disabled: false, disabledReason: '' }
}

/** 字段类型 emoji 图标（mock C8 每行左侧） */
function fieldTypeIcon(type: string): string {
  return CONDITION_TYPE_ICONS[type] || '·'
}

const formFieldChoices = computed<FieldChoice[]>(() => props.fields.map(toChoice))
const systemFieldChoices = computed<FieldChoice[]>(() =>
  SYSTEM_CONDITION_FIELDS
    .filter((sf) => !props.fields.some((f) => f.key === sf.key))
    .map(toChoice),
)

/** 字段查找合并系统字段，摘要/类型/算子对系统字段同样生效 */
const allFields = computed<FieldOption[]>(() => [...props.fields, ...systemFieldChoices.value])

function fieldTypeTag(type: string): string {
  return CONDITION_TYPE_LABELS[type] || type
}

function filterFieldOption(input: string, option: any): boolean {
  const text = String(option?.title ?? '')
  return text.toLowerCase().includes(input.toLowerCase())
}

// ==================== 辅助方法 ====================

function asItem(node: ConditionItem | ConditionGroup): ConditionItem {
  return node as ConditionItem
}

function asGroup(node: ConditionItem | ConditionGroup): ConditionGroup {
  return node as ConditionGroup
}

function getFieldType(fieldKey: string): string {
  const field = allFields.value.find((f) => f.key === fieldKey)
  return field?.type || 'text'
}

function getFieldLabel(fieldKey: string): string {
  const field = allFields.value.find((f) => f.key === fieldKey)
  return field?.label || fieldKey
}

function getFieldEnumOptions(fieldKey: string) {
  const field = allFields.value.find((f) => f.key === fieldKey)
  return normalizeOptionList(field?.options).map((opt) => ({ value: opt.value, label: opt.label }))
}

function getOperators(fieldKey: string) {
  return getOperatorsForType(getFieldType(fieldKey))
}

function needsValueInput(fieldKey: string, operator: string): boolean {
  const type = getFieldType(fieldKey)
  if (type === 'file') return false
  if (VALUELESS_OPERATORS.has(operator)) return false
  return !!operator
}

function betweenPart(item: ConditionItem, pos: 0 | 1): number | undefined {
  return Array.isArray(item.value) ? (item.value[pos] ?? undefined) : undefined
}

// ==================== 数据操作 ====================

function emptyItem(): ConditionItem {
  return { field: '', operator: '', value: null }
}

function commitTop(conditions: (ConditionItem | ConditionGroup)[], logic?: 'and' | 'or') {
  emit('update:modelValue', { logic: logic ?? group.value.logic, conditions })
}

function replaceRow(block: DisplayBlock, ri: number, newNode: ConditionItem | ConditionGroup) {
  const conditions = [...group.value.conditions]
  if (block.kind === 'implicit') {
    conditions[block.rows[ri].index] = newNode
  } else {
    const sub = asGroup(conditions[block.topIndex!])
    const inner = [...sub.conditions]
    inner[block.rows[ri].index] = newNode
    conditions[block.topIndex!] = { ...sub, conditions: inner }
  }
  commitTop(conditions)
}

function updateRowField(block: DisplayBlock, ri: number, field: string) {
  const ops = getOperators(field)
  replaceRow(block, ri, { field, operator: ops[0]?.value || '', value: null })
}

function updateRowOperator(block: DisplayBlock, ri: number, operator: string) {
  const item = asItem(block.rows[ri].node)
  replaceRow(block, ri, { ...item, operator, value: null })
}

function updateRowValue(block: DisplayBlock, ri: number, value: any) {
  const item = asItem(block.rows[ri].node)
  replaceRow(block, ri, { ...item, value })
}

function updateRowBetween(block: DisplayBlock, ri: number, pos: 0 | 1, value: number | null) {
  const item = asItem(block.rows[ri].node)
  const pair: (number | null)[] = Array.isArray(item.value) ? [...item.value] : [null, null]
  pair[pos] = value
  replaceRow(block, ri, { ...item, value: pair })
}

function updateRowNestedGroup(block: DisplayBlock, ri: number, val: ConditionGroup) {
  replaceRow(block, ri, val)
}

function removeRow(block: DisplayBlock, ri: number) {
  const conditions = [...group.value.conditions]
  if (block.kind === 'implicit') {
    conditions.splice(block.rows[ri].index, 1)
  } else {
    const sub = asGroup(conditions[block.topIndex!])
    conditions[block.topIndex!] = {
      ...sub,
      conditions: sub.conditions.filter((_, i) => i !== block.rows[ri].index),
    }
  }
  commitTop(conditions)
}

function addRowToBlock(block: DisplayBlock) {
  const conditions = [...group.value.conditions]
  if (block.kind === 'implicit') {
    const insertAt = block.rows.length ? block.rows[block.rows.length - 1].index + 1 : 0
    conditions.splice(insertAt, 0, emptyItem())
  } else {
    const sub = asGroup(conditions[block.topIndex!])
    conditions[block.topIndex!] = { ...sub, conditions: [...sub.conditions, emptyItem()] }
  }
  commitTop(conditions)
}

function removeBlock(block: DisplayBlock) {
  if (block.kind === 'group') {
    commitTop(group.value.conditions.filter((_, i) => i !== block.topIndex))
  } else {
    const indexes = new Set(block.rows.map((row) => row.index))
    commitTop(group.value.conditions.filter((_, i) => !indexes.has(i)))
  }
}

/** 空态首条：直接生成规范单组 */
function addFirstCondition() {
  commitTop([{ logic: 'and', conditions: [emptyItem()] }])
}

/**
 * 「+ 或，添加条件组」：顶层追加或分支。
 * 顶层是 and 且已有内容时先把现有整组包成分支 1，语义不变（a∧b → (a∧b) ∨ 新组）。
 */
function addGroup() {
  const newGroup: ConditionGroup = { logic: 'and', conditions: [emptyItem()] }
  const cur = group.value
  if (!cur.conditions.length) {
    commitTop([newGroup], 'or')
    return
  }
  if (cur.logic === 'or') {
    commitTop([...cur.conditions, newGroup], 'or')
    return
  }
  commitTop([{ logic: cur.logic, conditions: cur.conditions }, newGroup], 'or')
}

// ==================== 底部算子说明条 ====================

function collectLeafItems(g: ConditionGroup): ConditionItem[] {
  const items: ConditionItem[] = []
  for (const child of g.conditions) {
    if (isGroupNode(child)) items.push(...collectLeafItems(child))
    else items.push(child)
  }
  return items
}

const usedTypeHints = computed(() => {
  const types = new Set<string>()
  collectLeafItems(group.value).forEach((item) => {
    if (item.field) types.add(getFieldType(item.field))
  })
  return [...types].map((type) => ({
    type,
    label: CONDITION_TYPE_LABELS[type] || type,
    ops: getOperatorsForType(type).map((op) => op.label).join(' '),
  }))
})

// ==================== 命中率试算（B4/E6，仅顶层且传入 definitionId 时启用） ====================

const ESTIMATE_DEBOUNCE_MS = 800

type EstimateState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: RouteHitEstimateDto }

const estimateState = ref<EstimateState>({ kind: 'idle' })
let estimateTimer: ReturnType<typeof setTimeout> | null = null
let estimateSeq = 0

const estimateEnabled = computed(() => props._depth === 0 && !!props.definitionId && !props.disabled)

watch(
  () => (estimateEnabled.value ? JSON.stringify(sanitizeConditionGroup(group.value)) : null),
  (json) => {
    if (estimateTimer) clearTimeout(estimateTimer)
    if (!estimateEnabled.value || !json || json === 'null') {
      estimateState.value = { kind: 'idle' }
      return
    }
    estimateTimer = setTimeout(() => void runEstimate(json), ESTIMATE_DEBOUNCE_MS)
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  if (estimateTimer) clearTimeout(estimateTimer)
})

async function runEstimate(conditionJson: string) {
  const seq = ++estimateSeq
  estimateState.value = { kind: 'loading' }
  try {
    const data = await estimateRouteHit(props.definitionId!, conditionJson)
    if (seq === estimateSeq) estimateState.value = { kind: 'ready', data }
  } catch {
    // 试算是辅助信息，失败静默回 idle 不打扰编辑
    if (seq === estimateSeq) estimateState.value = { kind: 'idle' }
  }
}

/** 三态降级视图模型（E6）：full 青 / partial 黄 / empty 灰；0%/100% 极值标红 */
const estimateView = computed(() => {
  if (estimateState.value.kind !== 'ready') return null
  const { total, withValue, hit } = estimateState.value.data
  if (total === 0) {
    return { tone: 'empty' as const, text: '暂无历史数据 · 发布后累积 30 天流量即可显示命中率', extreme: '' }
  }
  const base = withValue > 0 ? withValue : total
  const percent = Math.round((hit / base) * 100)
  const extreme =
    percent === 0 ? '本分支可能永不命中，请检查条件'
    : percent === 100 ? '本分支可能总是命中，请检查条件'
    : ''
  if (withValue < total) {
    return {
      tone: 'partial' as const,
      text: `按 ${withValue}/${total} 张有值卡片试算：${percent}% 命中（历史覆盖不全，仅供参考）`,
      extreme,
    }
  }
  return { tone: 'full' as const, text: `按近 30 天 ${total} 张卡片试算：${percent}% 命中本分支`, extreme }
})

// ==================== 条件摘要 ====================

function describeItem(item: ConditionItem): string {
  const label = getFieldLabel(item.field) || '?'
  const opLabel = OPERATOR_LABELS[item.operator] || item.operator
  const type = getFieldType(item.field)
  if (type === 'file') return `${label} ${opLabel}`
  if (VALUELESS_OPERATORS.has(item.operator)) return `${label} ${opLabel}`
  if (!item.value && item.value !== 0) return `${label} ${opLabel} ?`
  const valStr = Array.isArray(item.value) ? item.value.join(', ') : String(item.value)
  return `${label} ${opLabel} ${valStr}`
}

function describeGroup(g: ConditionGroup): string {
  if (!g.conditions.length) return ''
  const parts = g.conditions.map((c) => {
    if (isGroupNode(c)) return `(${describeGroup(c)})`
    return describeItem(c)
  })
  const connector = g.logic === 'and' ? ' 且 ' : ' 或 '
  return parts.filter(Boolean).join(connector)
}

/** 条件摘要文本 */
const conditionSummary = computed(() => describeGroup(group.value))

defineExpose({ conditionSummary })
</script>

<template>
  <div class="condition-builder" :class="[`depth-${_depth % 2}`]">
    <!-- 空状态 -->
    <div v-if="blocks.length === 0" class="cb-empty">
      <span class="cb-empty-text">添加条件以控制流转</span>
    </div>

    <!-- 条件组容器列表 -->
    <template v-for="(block, bi) in blocks" :key="`b-${bi}`">
      <!-- 组间分隔（顶层 or 时为大「或」徽标） -->
      <div v-if="bi > 0" class="cfd-orsep">
        <span class="cfd-orsep__badge">{{ group.logic === 'or' ? '或' : '且' }}</span>
      </div>

      <div class="cfd-cgroup" :class="{ 'is-plain': bi > 0 }">
        <div class="cfd-cgroup__head">
          <b class="cb-group-title" :class="{ 'is-plain': bi > 0 }">条件组 {{ bi + 1 }}</b>
          <span class="cb-group-sub">
            {{ block.logic === 'and' ? '组内条件同时满足（且）' : '组内任一条件满足（或）' }}
          </span>
          <a-popconfirm
            title="确定删除此条件组？"
            :disabled="disabled"
            @confirm="removeBlock(block)"
          >
            <button type="button" class="cb-group-close" :disabled="disabled" aria-label="删除条件组">✕</button>
          </a-popconfirm>
        </div>

        <div class="cb-conditions">
          <template v-for="(row, ri) in block.rows" :key="`r-${bi}-${ri}`">
            <!-- 嵌套条件组（历史数据兼容，递归渲染） -->
            <div v-if="isGroupNode(row.node)" class="cb-nested-group">
              <ConditionBuilder
                :model-value="asGroup(row.node)"
                :fields="fields"
                :disabled="disabled"
                :_depth="_depth + 1"
                @update:model-value="(val: ConditionGroup) => updateRowNestedGroup(block, ri, val)"
              />
              <a-popconfirm
                title="确定删除此条件组？"
                :disabled="disabled"
                @confirm="removeRow(block, ri)"
              >
                <a-button type="text" danger size="small" class="cb-nested-delete" :disabled="disabled">
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </a-popconfirm>
            </div>

            <!-- 条件行：字段 / 算子 / 值 三段 -->
            <div v-else class="cb-rule">
              <!-- 字段选择（C8：表单/系统分组 + 类型徽标 + 禁用说明 + 搜索） -->
              <a-select
                :value="asItem(row.node).field || undefined"
                placeholder="选择字段"
                class="cb-field-select"
                show-search
                :filter-option="filterFieldOption"
                :disabled="disabled"
                @change="(val: any) => updateRowField(block, ri, val)"
              >
                <a-select-opt-group label="表单字段">
                  <a-select-option
                    v-for="f in formFieldChoices"
                    :key="f.key"
                    :value="f.key"
                    :title="f.label"
                    :disabled="f.disabled"
                  >
                    <span class="cb-field-opt">
                      <span class="cb-field-opt__icon">{{ fieldTypeIcon(f.type) }}</span>
                      <span class="cb-field-opt__label">{{ f.label }}</span>
                      <span v-if="f.disabled && f.disabledKind === 'required'" class="cb-field-opt__link" @click.stop="emit('navigate-field', f.key)">去设为必填 →</span>
                      <span v-else-if="f.disabled" class="cb-field-opt__reason">{{ f.disabledReason }}</span>
                      <span v-else class="cb-field-opt__type">{{ fieldTypeTag(f.type) }}</span>
                    </span>
                  </a-select-option>
                </a-select-opt-group>
                <a-select-opt-group v-if="systemFieldChoices.length" label="系统字段">
                  <a-select-option
                    v-for="f in systemFieldChoices"
                    :key="f.key"
                    :value="f.key"
                    :title="f.label"
                  >
                    <span class="cb-field-opt">
                      <span class="cb-field-opt__icon">{{ fieldTypeIcon(f.type) }}</span>
                      <span class="cb-field-opt__label">{{ f.label }}</span>
                      <span class="cb-field-opt__type">{{ fieldTypeTag(f.type) }}</span>
                    </span>
                  </a-select-option>
                </a-select-opt-group>
              </a-select>

              <!-- 运算符选择 -->
              <a-select
                :value="asItem(row.node).operator || undefined"
                placeholder="运算符"
                class="cb-op-select"
                :disabled="disabled || !asItem(row.node).field"
                @change="(val: any) => updateRowOperator(block, ri, val)"
              >
                <a-select-option
                  v-for="op in getOperators(asItem(row.node).field)"
                  :key="op.value"
                  :value="op.value"
                >
                  {{ op.label }}
                </a-select-option>
              </a-select>

              <!-- 值输入 - 根据字段类型和运算符动态渲染 -->
              <template v-if="needsValueInput(asItem(row.node).field, asItem(row.node).operator)">
                <!-- money / number + between → 双数值输入 -->
                <div
                  v-if="(getFieldType(asItem(row.node).field) === 'money' || getFieldType(asItem(row.node).field) === 'number') && asItem(row.node).operator === 'between'"
                  class="cb-value-input cb-between"
                >
                  <a-input-number
                    :value="betweenPart(asItem(row.node), 0)"
                    placeholder="最小值"
                    class="cb-between-input"
                    :disabled="disabled"
                    @change="(val: any) => updateRowBetween(block, ri, 0, val)"
                  />
                  <span class="cb-between-sep">~</span>
                  <a-input-number
                    :value="betweenPart(asItem(row.node), 1)"
                    placeholder="最大值"
                    class="cb-between-input"
                    :disabled="disabled"
                    @change="(val: any) => updateRowBetween(block, ri, 1, val)"
                  />
                </div>

                <!-- money / number 单值 -->
                <a-input-number
                  v-else-if="getFieldType(asItem(row.node).field) === 'money' || getFieldType(asItem(row.node).field) === 'number'"
                  :value="asItem(row.node).value"
                  placeholder="输入数值"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(val: any) => updateRowValue(block, ri, val)"
                />

                <!-- enum + in 运算符 → 多选（tags 兜底：字段未配可选值时仍可手输，存码显名） -->
                <a-select
                  v-else-if="getFieldType(asItem(row.node).field) === 'enum' && asItem(row.node).operator === 'in'"
                  :value="asItem(row.node).value || []"
                  mode="tags"
                  :options="getFieldEnumOptions(asItem(row.node).field)"
                  placeholder="选择多个值"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(val: any) => updateRowValue(block, ri, val)"
                />

                <!-- enum + eq/neq → 单选（存码显名） -->
                <a-select
                  v-else-if="getFieldType(asItem(row.node).field) === 'enum'"
                  :value="asItem(row.node).value"
                  :options="getFieldEnumOptions(asItem(row.node).field)"
                  placeholder="选择值"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(val: any) => updateRowValue(block, ri, val)"
                />

                <!-- date + between → 范围选择（value-format 保证与卡片 date-only 数据可比，且回显合法） -->
                <a-range-picker
                  v-else-if="getFieldType(asItem(row.node).field) === 'date' && asItem(row.node).operator === 'between'"
                  :value="asItem(row.node).value"
                  value-format="YYYY-MM-DD"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(val: any) => updateRowValue(block, ri, val)"
                />

                <!-- date 单值 -->
                <a-date-picker
                  v-else-if="getFieldType(asItem(row.node).field) === 'date'"
                  :value="asItem(row.node).value"
                  value-format="YYYY-MM-DD"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(val: any) => updateRowValue(block, ri, val)"
                />

                <!-- user 类型 → 人员选择器（远端搜索，存 ID） -->
                <UserSelect
                  v-else-if="getFieldType(asItem(row.node).field) === 'user'"
                  :model-value="asItem(row.node).value"
                  class="cb-value-input"
                  :disabled="disabled"
                  placeholder="选择人员"
                  @update:model-value="(v: any) => updateRowValue(block, ri, v?.id ?? null)"
                />

                <!-- org 类型 → 组织选择器（含 inOrgChain：组织链条目为组织 ID，选组织即可） -->
                <OrgSelect
                  v-else-if="getFieldType(asItem(row.node).field) === 'org'"
                  :model-value="asItem(row.node).value"
                  class="cb-value-input"
                  :disabled="disabled"
                  placeholder="选择组织"
                  @update:model-value="(v: any) => updateRowValue(block, ri, v?.id ?? null)"
                />

                <!-- text / 默认 → 文本输入 -->
                <a-input
                  v-else
                  :value="asItem(row.node).value"
                  placeholder="输入值"
                  class="cb-value-input"
                  :disabled="disabled"
                  @change="(e: any) => updateRowValue(block, ri, e.target.value)"
                />
              </template>

              <!-- file 类型/无值算子无需值输入，填充空白 -->
              <div v-else class="cb-value-input cb-value-placeholder" />

              <!-- 删除按钮 -->
              <a-popconfirm
                title="确定删除此条件？"
                :disabled="disabled"
                @confirm="removeRow(block, ri)"
              >
                <a-button type="text" danger size="small" :disabled="disabled">
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </a-popconfirm>
            </div>
          </template>
        </div>

        <!-- 组内追加条件（且） -->
        <a-button type="dashed" size="small" class="cb-add-and" :disabled="disabled" @click="addRowToBlock(block)">
          <template #icon><PlusOutlined /></template>
          且，添加条件
        </a-button>
      </div>
    </template>

    <!-- 底部操作 -->
    <div class="cb-actions">
      <a-button
        v-if="blocks.length === 0"
        type="dashed"
        size="small"
        :disabled="disabled"
        @click="addFirstCondition"
      >
        <template #icon><PlusOutlined /></template>
        且，添加条件
      </a-button>
      <a-button
        v-if="_depth === 0"
        type="dashed"
        size="small"
        :disabled="disabled"
        @click="addGroup"
      >
        <template #icon><SubnodeOutlined /></template>
        或，添加条件组
      </a-button>
    </div>

    <!-- 底部说明条（B4：左=算子随字段类型变化，右=命中率试算三态 E6） -->
    <div v-if="_depth === 0 && (usedTypeHints.length || estimateView || estimateState.kind === 'loading')" class="cb-footbar">
      <div v-if="usedTypeHints.length" class="cb-ophint">
        <div class="cb-ophint__title">算子随字段类型变化：</div>
        <div class="cb-ophint__tags">
          <span v-for="hint in usedTypeHints" :key="hint.type" class="cb-ophint__tag">
            {{ hint.label }}：{{ hint.ops }}
          </span>
        </div>
      </div>
      <div v-if="estimateState.kind === 'loading'" class="cb-estimate is-empty">
        <a-spin size="small" /> 命中率试算中…
      </div>
      <div v-else-if="estimateView" class="cb-estimate" :class="`is-${estimateView.tone}`">
        <div>📊 {{ estimateView.text }}</div>
        <div v-if="estimateView.extreme" class="cb-estimate__extreme">{{ estimateView.extreme }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.condition-builder {
  position: relative;
}

.cb-empty {
  padding: 12px 0 4px;
  text-align: center;

  .cb-empty-text {
    color: var(--text-3);
    font-size: 12.5px;
  }
}

.cb-group-title {
  color: var(--color-primary);
  font-size: 12.5px;
  font-weight: 600;

  &.is-plain {
    color: var(--text-2);
  }
}

.cb-group-sub {
  color: var(--text-3);
  font-size: 12px;
}

.cb-group-close {
  margin-left: auto;
  padding: 0 2px;
  color: var(--text-disabled);
  font-size: 13px;
  line-height: 1;
  background: none;
  border: 0;
  cursor: pointer;

  &:hover:not(:disabled) {
    color: var(--color-danger);
  }

  &:disabled {
    cursor: not-allowed;
  }
}

.cb-conditions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cb-rule {
  display: flex;
  gap: 8px;
  align-items: center;
}

.cb-field-select {
  flex: 1.2;
  min-width: 0;
}

.cb-field-opt {
  display: flex;
  gap: 8px;
  align-items: center;

  .cb-field-opt__icon {
    flex: none;
    width: 18px;
    text-align: center;
    font-size: 13px;
  }

  .cb-field-opt__label {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .cb-field-opt__type {
    flex: none;
    padding: 0 6px;
    color: var(--text-3);
    font-size: 10px;
    line-height: 16px;
    background: var(--bg-muted);
    border: 1px solid var(--border);
    border-radius: 4px;
  }

  .cb-field-opt__reason {
    flex: none;
    color: var(--text-disabled);
    font-size: 10px;
  }

  .cb-field-opt__link {
    flex: none;
    color: var(--color-primary);
    font-size: 10px;
    cursor: pointer;

    &:hover { text-decoration: underline; }
  }
}

.cb-op-select {
  flex: none;
  width: 110px;
}

.cb-value-input {
  flex: 1;
  min-width: 0;
}

.cb-value-placeholder {
  // 占位，保持对齐
}

.cb-between {
  display: flex;
  gap: 4px;
  align-items: center;

  .cb-between-input {
    flex: 1;
    min-width: 0;
  }

  .cb-between-sep {
    color: var(--text-3);
    font-size: 12px;
  }
}

.cb-add-and {
  margin-top: 8px;
}

.cb-nested-group {
  position: relative;
  padding: 8px;
  border: 1px dashed var(--border);
  border-radius: 8px;

  .cb-nested-delete {
    position: absolute;
    top: 4px;
    right: 4px;
    z-index: 1;
  }
}

.cb-actions {
  display: flex;
  gap: 8px;
  margin-top: 10px;
}

.cb-footbar {
  display: flex;
  gap: 14px;
  align-items: center;
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid var(--border-faint);
}

.cb-ophint {
  flex: 1;
  min-width: 0;
  color: var(--text-2);
  font-size: 12.5px;

  .cb-ophint__title {
    margin-bottom: 4px;
    font-weight: 600;
  }

  .cb-ophint__tags {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  .cb-ophint__tag {
    display: inline-block;
    padding: 1px 8px;
    color: var(--text-3);
    font-size: 12px;
    line-height: 18px;
    background: var(--bg-muted);
    border: 1px solid var(--border);
    border-radius: 4px;
  }
}

// 命中率试算三态（E6）：full 信息青调→info 令牌 / partial 黄→warning / empty 灰
.cb-estimate {
  flex: none;
  max-width: 60%;
  padding: 9px 13px;
  font-size: 12px;
  border-radius: 8px;

  &.is-full {
    color: var(--color-info-text);
    background: var(--color-info-light);
    border: 1px solid var(--color-info-border);
  }

  &.is-partial {
    color: var(--color-warning-text);
    background: var(--color-warning-light);
    border: 1px solid var(--color-warning-border);
  }

  &.is-empty {
    color: var(--text-3);
    background: var(--bg-muted);
    border: 1px solid var(--border);
  }

  .cb-estimate__extreme {
    margin-top: 3px;
    color: var(--color-danger);
    font-weight: 600;
  }
}
</style>
