<script setup lang="ts">
import { computed, watch } from 'vue'
import { PlusOutlined, DeleteOutlined, SubnodeOutlined } from '@ant-design/icons-vue'

// ==================== 类型定义 ====================

export interface FieldOption {
  key: string
  label: string
  type: string
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
}>()

// ==================== 运算符映射 ====================

const operatorMap: Record<string, { value: string; label: string }[]> = {
  text: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'contains', label: '包含' },
    { value: 'startsWith', label: '开头是' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  money: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'gt', label: '大于' },
    { value: 'gte', label: '≥' },
    { value: 'lt', label: '小于' },
    { value: 'lte', label: '≤' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  number: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'gt', label: '大于' },
    { value: 'gte', label: '≥' },
    { value: 'lt', label: '小于' },
    { value: 'lte', label: '≤' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  enum: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'in', label: '属于' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  date: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'gt', label: '晚于' },
    { value: 'lt', label: '早于' },
    { value: 'between', label: '区间' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  user: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  org: [
    { value: 'eq', label: '等于' },
    { value: 'neq', label: '不等于' },
    { value: 'inOrgChain', label: '属于组织链' },
    { value: 'empty', label: '为空' },
    { value: 'notEmpty', label: '不为空' },
  ],
  file: [
    { value: 'exists', label: '有附件' },
    { value: 'notExists', label: '无附件' },
  ],
}

const operatorLabelMap: Record<string, string> = {
  eq: '等于',
  neq: '不等于',
  contains: '包含',
  startsWith: '开头是',
  gt: '大于',
  gte: '≥',
  lt: '小于',
  lte: '≤',
  in: '属于',
  between: '区间',
  exists: '有附件',
  notExists: '无附件',
  empty: '为空',
  notEmpty: '不为空',
  inOrgChain: '属于组织链',
}

// ==================== 响应式数据 ====================

const group = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

// ==================== 辅助方法 ====================

function isConditionGroup(item: ConditionItem | ConditionGroup): item is ConditionGroup {
  return 'logic' in item && 'conditions' in item
}

function getFieldType(fieldKey: string): string {
  const field = props.fields.find((f) => f.key === fieldKey)
  return field?.type || 'text'
}

function getFieldLabel(fieldKey: string): string {
  const field = props.fields.find((f) => f.key === fieldKey)
  return field?.label || fieldKey
}

function getOperators(fieldKey: string) {
  const type = getFieldType(fieldKey)
  return operatorMap[type] || operatorMap.text
}

function needsValueInput(fieldKey: string, operator: string): boolean {
  const type = getFieldType(fieldKey)
  if (type === 'file') return false
  if (['exists', 'notExists', 'empty', 'notEmpty'].includes(operator)) return false
  return !!operator
}

// ==================== 数据操作 ====================

function emitUpdate() {
  emit('update:modelValue', { ...group.value })
}

function setLogic(logic: 'and' | 'or') {
  emit('update:modelValue', { ...group.value, logic })
}

function addCondition() {
  const newConditions = [...group.value.conditions, { field: '', operator: '', value: null }]
  emit('update:modelValue', { ...group.value, conditions: newConditions })
}

function addNestedGroup() {
  const newGroup: ConditionGroup = { logic: 'and', conditions: [] }
  const newConditions = [...group.value.conditions, newGroup]
  emit('update:modelValue', { ...group.value, conditions: newConditions })
}

function removeCondition(index: number) {
  const newConditions = group.value.conditions.filter((_, i) => i !== index)
  emit('update:modelValue', { ...group.value, conditions: newConditions })
}

function updateConditionField(index: number, field: string) {
  const conditions = [...group.value.conditions]
  const item = conditions[index] as ConditionItem
  const type = getFieldType(field)
  const ops = operatorMap[type] || operatorMap.text
  conditions[index] = { field, operator: ops[0]?.value || '', value: null }
  emit('update:modelValue', { ...group.value, conditions })
}

function updateConditionOperator(index: number, operator: string) {
  const conditions = [...group.value.conditions]
  const item = conditions[index] as ConditionItem
  conditions[index] = { ...item, operator, value: null }
  emit('update:modelValue', { ...group.value, conditions })
}

function updateConditionValue(index: number, value: any) {
  const conditions = [...group.value.conditions]
  const item = conditions[index] as ConditionItem
  conditions[index] = { ...item, value }
  emit('update:modelValue', { ...group.value, conditions })
}

function updateNestedGroup(index: number, val: ConditionGroup) {
  const conditions = [...group.value.conditions]
  conditions[index] = val
  emit('update:modelValue', { ...group.value, conditions })
}

// ==================== 条件摘要 ====================

function describeItem(item: ConditionItem): string {
  const label = getFieldLabel(item.field) || '?'
  const opLabel = operatorLabelMap[item.operator] || item.operator
  const type = getFieldType(item.field)
  if (type === 'file') return `${label} ${opLabel}`
  if (['empty', 'notEmpty', 'exists', 'notExists'].includes(item.operator)) return `${label} ${opLabel}`
  if (!item.value && item.value !== 0) return `${label} ${opLabel} ?`
  const valStr = Array.isArray(item.value) ? item.value.join(', ') : String(item.value)
  return `${label} ${opLabel} ${valStr}`
}

function describeGroup(g: ConditionGroup): string {
  if (!g.conditions.length) return ''
  const parts = g.conditions.map((c) => {
    if (isConditionGroup(c)) return `(${describeGroup(c)})`
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
  <div
    class="condition-builder"
    :class="[`depth-${_depth % 2}`]"
  >
    <!-- 逻辑切换头部 -->
    <div class="cb-header">
      <a-segmented
        :value="group.logic"
        :options="[
          { value: 'and', label: '且 AND' },
          { value: 'or', label: '或 OR' },
        ]"
        size="small"
        :disabled="disabled"
        @change="(val: any) => setLogic(val as 'and' | 'or')"
      />
    </div>

    <!-- 空状态 -->
    <div v-if="group.conditions.length === 0" class="cb-empty">
      <span class="cb-empty-text">添加条件以控制节点进入</span>
    </div>

    <!-- 条件列表 -->
    <div class="cb-conditions">
      <template v-for="(item, idx) in group.conditions" :key="idx">
        <!-- 嵌套条件组（递归） -->
        <div v-if="isConditionGroup(item)" class="cb-nested-group">
          <ConditionBuilder
            :model-value="item"
            :fields="fields"
            :disabled="disabled"
            :_depth="_depth + 1"
            @update:model-value="(val: ConditionGroup) => updateNestedGroup(idx, val)"
          />
          <a-popconfirm
            title="确定删除此条件组？"
            :disabled="disabled"
            @confirm="removeCondition(idx)"
          >
            <a-button
              type="text"
              danger
              size="small"
              class="cb-nested-delete"
              :disabled="disabled"
            >
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </a-popconfirm>
        </div>

        <!-- 条件行 -->
        <div v-else class="cb-rule">
          <!-- 字段选择 -->
          <a-select
            :value="(item as ConditionItem).field || undefined"
            placeholder="选择字段"
            class="cb-field-select"
            :disabled="disabled"
            @change="(val: any) => updateConditionField(idx, val)"
          >
            <a-select-option v-for="f in fields" :key="f.key" :value="f.key">
              {{ f.label }}
            </a-select-option>
          </a-select>

          <!-- 运算符选择 -->
          <a-select
            :value="(item as ConditionItem).operator || undefined"
            placeholder="运算符"
            class="cb-op-select"
            :disabled="disabled || !(item as ConditionItem).field"
            @change="(val: any) => updateConditionOperator(idx, val)"
          >
            <a-select-option
              v-for="op in getOperators((item as ConditionItem).field)"
              :key="op.value"
              :value="op.value"
            >
              {{ op.label }}
            </a-select-option>
          </a-select>

          <!-- 值输入 - 根据字段类型和运算符动态渲染 -->
          <template v-if="needsValueInput((item as ConditionItem).field, (item as ConditionItem).operator)">
            <!-- text 类型 -->
            <a-input
              v-if="getFieldType((item as ConditionItem).field) === 'text'"
              :value="(item as ConditionItem).value"
              placeholder="输入值"
              class="cb-value-input"
              :disabled="disabled"
              @change="(e: any) => updateConditionValue(idx, e.target.value)"
            />

            <!-- money / number 类型 -->
            <a-input-number
              v-else-if="getFieldType((item as ConditionItem).field) === 'money' || getFieldType((item as ConditionItem).field) === 'number'"
              :value="(item as ConditionItem).value"
              placeholder="输入数值"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- enum + in 运算符 → 多选 -->
            <a-select
              v-else-if="getFieldType((item as ConditionItem).field) === 'enum' && (item as ConditionItem).operator === 'in'"
              :value="(item as ConditionItem).value || []"
              mode="multiple"
              placeholder="选择多个值"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- enum + eq/neq → 单选 -->
            <a-select
              v-else-if="getFieldType((item as ConditionItem).field) === 'enum'"
              :value="(item as ConditionItem).value"
              placeholder="选择值"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- date + between → 范围选择 -->
            <a-range-picker
              v-else-if="getFieldType((item as ConditionItem).field) === 'date' && (item as ConditionItem).operator === 'between'"
              :value="(item as ConditionItem).value"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- org + 属于组织链 → 组织链编码/ID -->
            <a-input
              v-else-if="getFieldType((item as ConditionItem).field) === 'org' && (item as ConditionItem).operator === 'inOrgChain'"
              :value="(item as ConditionItem).value"
              placeholder="组织链编码或组织ID"
              class="cb-value-input"
              :disabled="disabled"
              @change="(e: any) => updateConditionValue(idx, e.target.value)"
            />

            <!-- date 单值 -->
            <a-date-picker
              v-else-if="getFieldType((item as ConditionItem).field) === 'date'"
              :value="(item as ConditionItem).value"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- user / org 类型 → select placeholder -->
            <a-select
              v-else-if="getFieldType((item as ConditionItem).field) === 'user' || getFieldType((item as ConditionItem).field) === 'org'"
              :value="(item as ConditionItem).value"
              placeholder="选择"
              class="cb-value-input"
              :disabled="disabled"
              @change="(val: any) => updateConditionValue(idx, val)"
            />

            <!-- 默认 text input -->
            <a-input
              v-else
              :value="(item as ConditionItem).value"
              placeholder="输入值"
              class="cb-value-input"
              :disabled="disabled"
              @change="(e: any) => updateConditionValue(idx, e.target.value)"
            />
          </template>

          <!-- file 类型无需值输入，填充空白 -->
          <div v-else class="cb-value-input cb-value-placeholder" />

          <!-- 删除按钮 -->
          <a-popconfirm
            title="确定删除此条件？"
            :disabled="disabled"
            @confirm="removeCondition(idx)"
          >
            <a-button type="text" danger size="small" :disabled="disabled">
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </a-popconfirm>
        </div>
      </template>
    </div>

    <!-- 底部操作 -->
    <div class="cb-actions">
      <a-button type="dashed" size="small" :disabled="disabled" @click="addCondition">
        <template #icon><PlusOutlined /></template>
        添加条件
      </a-button>
      <a-button type="dashed" size="small" :disabled="disabled" @click="addNestedGroup">
        <template #icon><SubnodeOutlined /></template>
        添加条件组
      </a-button>
    </div>
  </div>
</template>

<style scoped lang="scss">
.condition-builder {
  position: relative;
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px;
  border-left: 3px solid var(--color-info);

  &.depth-0 {
    background: var(--bg-muted);
    border-left-color: var(--color-info);
  }

  &.depth-1 {
    background: var(--bg-muted);
    border-left-color: var(--color-success);
  }
}

.cb-header {
  margin-bottom: 12px;
}

.cb-empty {
  padding: 16px 0;
  text-align: center;

  .cb-empty-text {
    color: var(--text-3);
    font-size: 13px;
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
  flex: 1;
  min-width: 0;
}

.cb-op-select {
  flex: 1;
  min-width: 0;
}

.cb-value-input {
  flex: 1;
  min-width: 0;
}

.cb-value-placeholder {
  // 占位，保持对齐
}

.cb-nested-group {
  position: relative;

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
  margin-top: 12px;
}
</style>
