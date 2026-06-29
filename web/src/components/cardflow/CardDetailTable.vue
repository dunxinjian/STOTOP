<script setup lang="ts">
import { computed, ref } from 'vue'
import type { AuxiliaryType, SchemaFieldDefinition } from '@/types/cardflow'
import AccountSelector from './fields/AccountSelector.vue'
import AuxiliarySelector from './fields/AuxiliarySelector.vue'
import BankAccountSelector from './fields/BankAccountSelector.vue'
import {
  SwipeCell as VanSwipeCell,
  Field as VanField,
  Button as VanButton,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  DatePicker as VanDatePicker,
  Picker as VanPicker,
  Popup as VanPopup,
} from 'vant'
import 'vant/es/swipe-cell/style'
import 'vant/es/field/style'
import 'vant/es/button/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/date-picker/style'
import 'vant/es/picker/style'
import 'vant/es/popup/style'

// ==================== 类型定义 ====================

export interface DetailRow {
  _id: string
  [key: string]: any
}

interface Props {
  schema: SchemaFieldDefinition[]
  modelValue: DetailRow[]
  mode: 'edit' | 'view'
  platform?: 'pc' | 'mobile'
  /** PC 紧凑模式：适用于窄容器（如右侧抽屉 400px），按“每行一张卡，字段竖排”呈现 */
  compact?: boolean
  accountSetId?: number | null
  orgId?: number | null
}

const props = withDefaults(defineProps<Props>(), {
  platform: 'pc',
  compact: false,
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: DetailRow[]): void
}>()

// ==================== 状态 ====================

const expanded = ref(false)
const datePickerVisible = ref<Record<string, boolean>>({})
const pickerVisible = ref<Record<string, boolean>>({})

// ==================== 计算属性 ====================

const rows = computed({
  get: () => props.modelValue || [],
  set: (val) => emit('update:modelValue', val),
})

/** 数值类型字段（money/amount/number） */
const numericFields = computed(() =>
  props.schema.filter((f) => ['money', 'amount', 'number'].includes(f.type as string))
)

/** 金额类型字段（money/amount） */
const moneyFields = computed(() =>
  props.schema.filter((f) => ['money', 'amount'].includes(f.type as string))
)

/** 各数值字段的合计 */
const summaryMap = computed(() => {
  const map: Record<string, number> = {}
  for (const field of numericFields.value) {
    map[field.key] = rows.value.reduce((sum, row) => {
      const val = Number(row[field.key])
      return sum + (isNaN(val) ? 0 : val)
    }, 0)
  }
  return map
})

/** money 字段总合计（用于摘要） */
const totalMoney = computed(() => {
  return moneyFields.value.reduce((sum, f) => sum + (summaryMap.value[f.key] || 0), 0)
})

/** 表格列定义（PC） */
const tableColumns = computed(() => {
  const cols = props.schema.map((field) => ({
    title: field.label,
    dataIndex: field.key,
    key: field.key,
    width: getColumnWidth(field.type),
  }))
  if (props.mode === 'edit') {
    cols.push({ title: '操作', dataIndex: '_action', key: '_action', width: 60 })
  }
  return cols
})

// ==================== 方法 ====================

function generateId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

function getColumnWidth(type: string): number {
  switch (type) {
    case 'account':
    case 'bankAccount':
      return 220
    case 'auxiliary':
      return 180
    case 'money':
    case 'amount':
      return 140
    case 'date':
      return 130
    case 'number':
      return 100
    case 'enum':
    case 'select':
      return 120
    default:
      return 150
  }
}

function formatMoney(val: any): string {
  const num = Number(val)
  if (isNaN(num) || val === null || val === undefined || val === '') return '-'
  return `¥${num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function formatNumber(val: any): string {
  const num = Number(val)
  if (isNaN(num) || val === null || val === undefined || val === '') return '-'
  return num.toLocaleString('zh-CN')
}

function formatDate(val: any): string {
  if (!val) return '-'
  const d = new Date(val)
  if (isNaN(d.getTime())) return String(val)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatAccountValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  const code = val.code || val.accountCode
  const name = val.name || val.accountName
  if (code && name) return `${code} ${name}`
  return name || code || '-'
}

function formatAuxiliaryValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  if (val.code && val.name) return `${val.code} ${val.name}`
  return val.name || val.code || '-'
}

function formatBankAccountValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  const accountNo = val.accountNo || val.bankAccountNo
  const bankName = val.bankName
  const accountName = val.accountName
  return [accountNo, bankName, accountName].filter(Boolean).join(' · ') || '-'
}

function getDisplayValue(field: SchemaFieldDefinition, val: any): string {
  if (val === null || val === undefined || val === '') return '-'
  switch (field.type as string) {
    case 'money':
    case 'amount':
      return formatMoney(val)
    case 'number':
      return formatNumber(val)
    case 'date':
      return formatDate(val)
    case 'account':
      return formatAccountValue(val)
    case 'auxiliary':
      return formatAuxiliaryValue(val)
    case 'bankAccount':
      return formatBankAccountValue(val)
    default:
      return String(val)
  }
}

function addRow() {
  const newRow: DetailRow = { _id: generateId() }
  for (const field of props.schema) {
    const t = field.type as string
    newRow[field.key] = (t === 'number' || t === 'money' || t === 'amount') ? null : ''
  }
  emit('update:modelValue', [...rows.value, newRow])
}

function removeRow(id: string) {
  emit('update:modelValue', rows.value.filter((r) => r._id !== id))
}

function updateRowField(rowId: string, fieldKey: string, value: any) {
  const newRows = rows.value.map((r) => {
    if (r._id === rowId) {
      return { ...r, [fieldKey]: value }
    }
    return r
  })
  emit('update:modelValue', newRows)
}

function toggleExpand() {
  expanded.value = !expanded.value
}

function onDateConfirm(rowId: string, fieldKey: string, { selectedValues }: any) {
  updateRowField(rowId, fieldKey, selectedValues.join('-'))
  datePickerVisible.value[`${rowId}_${fieldKey}`] = false
}

function onPickerConfirm(rowId: string, fieldKey: string, { selectedOptions }: any) {
  updateRowField(rowId, fieldKey, selectedOptions[0]?.value ?? '')
  pickerVisible.value[`${rowId}_${fieldKey}`] = false
}

function getFieldType(dataIndex: string): string {
  return getField(dataIndex)?.type || 'text'
}

function getField(dataIndex: string): SchemaFieldDefinition | undefined {
  return props.schema.find((f) => f.key === dataIndex)
}

function getAuxType(dataIndex: string): AuxiliaryType {
  return (getField(dataIndex)?.auxType || 'employee') as AuxiliaryType
}

/** 将 a-table column.dataIndex 转为字符串（避免 DataIndex 联合类型报错） */
function cdi(dataIndex: any): string {
  return typeof dataIndex === 'string' ? dataIndex : String(dataIndex ?? '')
}

/** 合计行数据（供 a-table summary 使用） */
function getSummaryText(fieldKey: string): string {
  if (fieldKey === '_action') return ''
  const field = props.schema.find((f) => f.key === fieldKey)
  if (!field) return ''
  const t = field.type as string
  if (['money', 'amount'].includes(t)) {
    return formatMoney(summaryMap.value[fieldKey] || 0)
  }
  if (t === 'number') {
    return formatNumber(summaryMap.value[fieldKey] || 0)
  }
  return ''
}
</script>

<template>
  <div class="card-detail-table" :class="[`platform-${platform}`, `mode-${mode}`]">
    <!-- ==================== VIEW 模式 ==================== -->
    <template v-if="mode === 'view'">
      <!-- 折叠摘要 -->
      <div v-if="!expanded" class="detail-summary" @click="toggleExpand">
        <span class="summary-text">
          {{ rows.length }}行明细
          <template v-if="moneyFields.length > 0">
            <span class="summary-dot">·</span>
            <span class="summary-amount">合计 {{ formatMoney(totalMoney) }}</span>
          </template>
        </span>
        <span class="expand-btn">展开∨</span>
      </div>

      <!-- 展开内容 -->
      <div class="detail-expand-wrapper" :class="{ 'is-expanded': expanded }">
        <div v-if="expanded" class="detail-expanded">
          <!-- 收起按钮 -->
          <div class="collapse-header">
            <span class="collapse-title">明细行（{{ rows.length }}）</span>
            <span class="collapse-btn" @click="toggleExpand">收起∧</span>
          </div>

          <!-- PC 表格 -->
          <template v-if="platform === 'pc' && !compact">
            <a-table
              :columns="tableColumns"
              :data-source="rows"
              :pagination="false"
              :row-key="(r: DetailRow) => r._id"
              size="small"
              bordered
              class="detail-table"
            >
              <template #bodyCell="{ column, record }">
                <span>{{ getDisplayValue(
                  schema.find(f => f.key === cdi(column.dataIndex)) || { key: '', label: '', type: 'text', required: false, readonly: false },
                  record[cdi(column.dataIndex)]
                ) }}</span>
              </template>
              <template #summary>
                <a-table-summary fixed>
                  <a-table-summary-row class="summary-row">
                    <a-table-summary-cell
                      v-for="(col, idx) in tableColumns"
                      :key="col.key"
                      :index="idx"
                    >
                      <template v-if="idx === 0">
                        <strong>合计</strong>
                      </template>
                      <template v-else>
                        <span class="summary-value">{{ getSummaryText(cdi(col.dataIndex)) }}</span>
                      </template>
                    </a-table-summary-cell>
                  </a-table-summary-row>
                </a-table-summary>
              </template>
            </a-table>
          </template>

          <!-- PC 紧凑只读卡片（适用于窄抽屉） -->
          <template v-else-if="platform === 'pc' && compact">
            <div class="compact-list">
              <div v-for="(row, idx) in rows" :key="row._id" class="compact-card">
                <div class="compact-card__header">
                  <span class="compact-card__index">#{{ idx + 1 }}</span>
                </div>
                <div class="compact-card__body">
                  <div
                    v-for="field in schema"
                    :key="field.key"
                    class="compact-card__row"
                  >
                    <span class="compact-card__label">{{ field.label }}</span>
                    <span class="compact-card__value">{{ getDisplayValue(field, row[field.key]) }}</span>
                  </div>
                </div>
              </div>
            </div>
            <!-- 紧凑模式合计 -->
            <div v-if="numericFields.length > 0" class="compact-summary">
              <div
                v-for="field in numericFields"
                :key="field.key"
                class="compact-summary__row"
              >
                <span class="compact-summary__label">{{ field.label }} 合计</span>
                <span class="compact-summary__value">{{ getSummaryText(field.key) }}</span>
              </div>
            </div>
          </template>

          <!-- 移动端卡片列表 -->
          <template v-else-if="platform === 'mobile'">
            <div class="mobile-card-list">
              <div v-for="row in rows" :key="row._id" class="mobile-card">
                <VanCellGroup inset>
                  <VanCell
                    v-for="field in schema"
                    :key="field.key"
                    :title="field.label"
                    :value="getDisplayValue(field, row[field.key])"
                  />
                </VanCellGroup>
              </div>
            </div>
            <!-- 移动端合计 -->
            <div v-if="numericFields.length > 0" class="mobile-summary">
              <VanCellGroup inset>
                <VanCell title="合计" class="mobile-summary-title" />
                <VanCell
                  v-for="field in numericFields"
                  :key="field.key"
                  :title="field.label"
                  :value="getSummaryText(field.key)"
                />
              </VanCellGroup>
            </div>
          </template>
        </div>
      </div>
    </template>

    <!-- ==================== EDIT 模式 ==================== -->
    <template v-else>
      <!-- PC 可编辑表格 -->
      <template v-if="platform === 'pc' && !compact">
        <a-table
          v-if="rows.length > 0"
          :columns="tableColumns"
          :data-source="rows"
          :pagination="false"
          :row-key="(r: DetailRow) => r._id"
          size="small"
          bordered
          class="detail-table editable"
        >
          <template #bodyCell="{ column, record }">
            <!-- 操作列 -->
            <template v-if="cdi(column.dataIndex) === '_action'">
              <a-popconfirm
                title="确定删除此行？"
                ok-text="删除"
                cancel-text="取消"
                @confirm="removeRow(record._id)"
              >
                <a-button type="link" danger size="small">×</a-button>
              </a-popconfirm>
            </template>
            <!-- 编辑列 -->
            <template v-else>
              <!-- text -->
              <a-input
                v-if="getFieldType(cdi(column.dataIndex)) === 'text'"
                :value="record[cdi(column.dataIndex)]"
                size="small"
                placeholder="请输入"
                @change="(e: any) => updateRowField(record._id, cdi(column.dataIndex), e.target.value)"
              />
              <!-- money/amount -->
              <a-input-number
                v-else-if="['money', 'amount'].includes(getFieldType(cdi(column.dataIndex)))"
                :value="record[cdi(column.dataIndex)]"
                size="small"
                :precision="2"
                :controls="false"
                placeholder="0.00"
                style="width: 100%"
                :formatter="(v: any) => v ? Number(v).toLocaleString('zh-CN') : ''"
                @change="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- number -->
              <a-input-number
                v-else-if="getFieldType(cdi(column.dataIndex)) === 'number'"
                :value="record[cdi(column.dataIndex)]"
                size="small"
                :controls="false"
                placeholder="0"
                style="width: 100%"
                @change="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- enum/select -->
              <a-select
                v-else-if="['enum', 'select'].includes(getFieldType(cdi(column.dataIndex)))"
                :value="record[cdi(column.dataIndex)]"
                size="small"
                placeholder="请选择"
                style="width: 100%"
                @change="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- date -->
              <a-date-picker
                v-else-if="getFieldType(cdi(column.dataIndex)) === 'date'"
                :value="record[cdi(column.dataIndex)]"
                size="small"
                style="width: 100%"
                value-format="YYYY-MM-DD"
                @change="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- account -->
              <AccountSelector
                v-else-if="getFieldType(cdi(column.dataIndex)) === 'account'"
                :model-value="record[cdi(column.dataIndex)]"
                :account-set-id="accountSetId"
                @update:model-value="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- auxiliary -->
              <AuxiliarySelector
                v-else-if="getFieldType(cdi(column.dataIndex)) === 'auxiliary'"
                :model-value="record[cdi(column.dataIndex)]"
                :aux-type="getAuxType(cdi(column.dataIndex))"
                :account-set-id="accountSetId"
                @update:model-value="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- bankAccount -->
              <BankAccountSelector
                v-else-if="getFieldType(cdi(column.dataIndex)) === 'bankAccount'"
                :model-value="record[cdi(column.dataIndex)]"
                :org-id="orgId"
                @update:model-value="(v: any) => updateRowField(record._id, cdi(column.dataIndex), v)"
              />
              <!-- fallback -->
              <a-input
                v-else
                :value="record[cdi(column.dataIndex)]"
                size="small"
                placeholder="请输入"
                @change="(e: any) => updateRowField(record._id, cdi(column.dataIndex), e.target.value)"
              />
            </template>
          </template>
          <template #summary>
            <a-table-summary fixed>
              <a-table-summary-row class="summary-row">
                <a-table-summary-cell
                  v-for="(col, idx) in tableColumns"
                  :key="col.key"
                  :index="idx"
                >
                  <template v-if="idx === 0">
                    <strong>合计</strong>
                  </template>
                  <template v-else>
                    <span class="summary-value">{{ getSummaryText(col.dataIndex) }}</span>
                  </template>
                </a-table-summary-cell>
              </a-table-summary-row>
            </a-table-summary>
          </template>
        </a-table>

        <!-- 空状态 -->
        <div v-if="rows.length === 0" class="empty-state" @click="addRow">
          <span class="empty-text">暂无明细，点击添加</span>
        </div>

        <!-- 添加按钮 -->
        <a-button class="add-row-btn" type="dashed" block @click="addRow">
          + 添加明细行
        </a-button>
      </template>

      <!-- PC 紧凑可编辑卡片（适用于窄抽屉） -->
      <template v-else-if="platform === 'pc' && compact">
        <div v-if="rows.length > 0" class="compact-list">
          <div v-for="(row, idx) in rows" :key="row._id" class="compact-card compact-card--editable">
            <div class="compact-card__header">
              <span class="compact-card__index">#{{ idx + 1 }}</span>
              <a-popconfirm
                title="确定删除此行？"
                ok-text="删除"
                cancel-text="取消"
                :overlay-style="{ minWidth: '120px' }"
                @confirm="removeRow(row._id)"
              >
                <a-button type="link" danger size="small" class="compact-card__remove">删除</a-button>
              </a-popconfirm>
            </div>
            <div class="compact-card__body">
              <div
                v-for="field in schema"
                :key="field.key"
                class="compact-card__row"
              >
                <span class="compact-card__label">
                  <span v-if="field.required" class="compact-card__required">*</span>
                  {{ field.label }}
                </span>
                <span class="compact-card__control">
                  <a-input
                    v-if="(field.type as string) === 'text'"
                    :value="row[field.key]"
                    size="small"
                    placeholder="请输入"
                    @change="(e: any) => updateRowField(row._id, field.key, e.target.value)"
                  />
                  <a-input-number
                    v-else-if="['money', 'amount'].includes(field.type as string)"
                    :value="row[field.key]"
                    size="small"
                    :precision="2"
                    :controls="false"
                    placeholder="0.00"
                    style="width: 100%"
                    :formatter="(v: any) => v ? Number(v).toLocaleString('zh-CN') : ''"
                    @change="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <a-input-number
                    v-else-if="(field.type as string) === 'number'"
                    :value="row[field.key]"
                    size="small"
                    :controls="false"
                    placeholder="0"
                    style="width: 100%"
                    @change="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <a-select
                    v-else-if="['enum', 'select'].includes(field.type as string)"
                    :value="row[field.key]"
                    size="small"
                    placeholder="请选择"
                    style="width: 100%"
                    :options="((field as any).options || []).map((o: string) => ({ label: o, value: o }))"
                    @change="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <a-date-picker
                    v-else-if="field.type === 'date'"
                    :value="row[field.key]"
                    size="small"
                    style="width: 100%"
                    value-format="YYYY-MM-DD"
                    @change="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <AccountSelector
                    v-else-if="(field.type as string) === 'account'"
                    :model-value="row[field.key]"
                    :account-set-id="accountSetId"
                    @update:model-value="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <AuxiliarySelector
                    v-else-if="(field.type as string) === 'auxiliary'"
                    :model-value="row[field.key]"
                    :aux-type="getAuxType(field.key)"
                    :account-set-id="accountSetId"
                    @update:model-value="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <BankAccountSelector
                    v-else-if="(field.type as string) === 'bankAccount'"
                    :model-value="row[field.key]"
                    :org-id="orgId"
                    @update:model-value="(v: any) => updateRowField(row._id, field.key, v)"
                  />
                  <a-input
                    v-else
                    :value="row[field.key]"
                    size="small"
                    placeholder="请输入"
                    @change="(e: any) => updateRowField(row._id, field.key, e.target.value)"
                  />
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- 紧凑模式合计 -->
        <div v-if="rows.length > 0 && numericFields.length > 0" class="compact-summary">
          <div
            v-for="field in numericFields"
            :key="field.key"
            class="compact-summary__row"
          >
            <span class="compact-summary__label">{{ field.label }} 合计</span>
            <span class="compact-summary__value">{{ getSummaryText(field.key) }}</span>
          </div>
        </div>

        <!-- 空状态 -->
        <div v-if="rows.length === 0" class="empty-state" @click="addRow">
          <span class="empty-text">暂无明细，点击添加</span>
        </div>

        <!-- 添加按钮 -->
        <a-button class="add-row-btn" type="dashed" block @click="addRow">
          + 添加明细行
        </a-button>
      </template>

      <!-- 移动端可编辑卡片 -->
      <template v-else-if="platform === 'mobile'">
        <div class="mobile-edit-list">
          <VanSwipeCell v-for="row in rows" :key="row._id">
            <div class="mobile-edit-card">
              <VanCellGroup inset>
                <template v-for="field in schema" :key="field.key">
                  <!-- text -->
                  <VanField
                    v-if="['text', 'string'].includes(field.type as string)"
                    :label="field.label"
                    :model-value="row[field.key] || ''"
                    :required="field.required"
                    placeholder="请输入"
                    @update:model-value="(v: string) => updateRowField(row._id, field.key, v)"
                  />
                  <!-- money/amount -->
                  <VanField
                    v-else-if="['money', 'amount'].includes(field.type as string)"
                    :label="field.label"
                    :model-value="row[field.key]?.toString() || ''"
                    :required="field.required"
                    type="number"
                    placeholder="0.00"
                    @update:model-value="(v: string) => updateRowField(row._id, field.key, v ? Number(v) : null)"
                  >
                    <template #left-icon>
                      <span style="color: var(--color-danger); font-weight: bold">¥</span>
                    </template>
                  </VanField>
                  <!-- number -->
                  <VanField
                    v-else-if="(field.type as string) === 'number'"
                    :label="field.label"
                    :model-value="row[field.key]?.toString() || ''"
                    :required="field.required"
                    type="number"
                    placeholder="0"
                    @update:model-value="(v: string) => updateRowField(row._id, field.key, v ? Number(v) : null)"
                  />
                  <!-- date -->
                  <VanField
                    v-else-if="field.type === 'date'"
                    :label="field.label"
                    :model-value="formatDate(row[field.key])"
                    :required="field.required"
                    readonly
                    is-link
                    placeholder="请选择日期"
                    @click="datePickerVisible[`${row._id}_${field.key}`] = true"
                  />
                  <!-- enum/select -->
                  <VanField
                    v-else-if="['enum', 'select'].includes(field.type as string)"
                    :label="field.label"
                    :model-value="row[field.key] || ''"
                    :required="field.required"
                    readonly
                    is-link
                    placeholder="请选择"
                    @click="pickerVisible[`${row._id}_${field.key}`] = true"
                  />
                  <!-- structured finance fields -->
                  <VanField
                    v-else-if="['account', 'auxiliary', 'bankAccount'].includes(field.type as string)"
                    :label="field.label"
                    :model-value="getDisplayValue(field, row[field.key])"
                    :required="field.required"
                    readonly
                    placeholder="-"
                  />
                  <!-- fallback -->
                  <VanField
                    v-else
                    :label="field.label"
                    :model-value="row[field.key] || ''"
                    :required="field.required"
                    placeholder="请输入"
                    @update:model-value="(v: string) => updateRowField(row._id, field.key, v)"
                  />
                </template>
              </VanCellGroup>
            </div>
            <template #right>
              <VanButton
                square
                type="danger"
                text="删除"
                class="swipe-delete-btn"
                @click="removeRow(row._id)"
              />
            </template>
          </VanSwipeCell>
        </div>

        <!-- 移动端合计 -->
        <div v-if="rows.length > 0 && numericFields.length > 0" class="mobile-summary">
          <VanCellGroup inset>
            <VanCell
              v-for="field in numericFields"
              :key="field.key"
              :title="`${field.label} 合计`"
              :value="getSummaryText(field.key)"
            />
          </VanCellGroup>
        </div>

        <!-- 空状态 -->
        <div v-if="rows.length === 0" class="empty-state" @click="addRow">
          <span class="empty-text">暂无明细，点击添加</span>
        </div>

        <!-- 添加按钮 -->
        <div class="mobile-add-btn-wrapper">
          <VanButton
            type="default"
            block
            plain
            hairline
            icon="plus"
            class="mobile-add-btn"
            @click="addRow"
          >
            添加明细行
          </VanButton>
        </div>

        <!-- 日期弹窗 -->
        <template v-for="row in rows" :key="'dp_group_' + row._id">
          <VanPopup
            v-for="field in schema.filter(f => f.type === 'date')"
            :key="'dp_' + row._id + '_' + field.key"
            v-model:show="datePickerVisible[`${row._id}_${field.key}`]"
            position="bottom"
            round
          >
            <VanDatePicker
              title="选择日期"
              @confirm="(val: any) => onDateConfirm(row._id, field.key, val)"
              @cancel="datePickerVisible[`${row._id}_${field.key}`] = false"
            />
          </VanPopup>
        </template>

        <!-- 选择器弹窗 -->
        <template v-for="row in rows" :key="'pk_group_' + row._id">
          <VanPopup
            v-for="field in schema.filter(f => ['enum', 'select'].includes(f.type))"
            :key="'pk_' + row._id + '_' + field.key"
            v-model:show="pickerVisible[`${row._id}_${field.key}`]"
            position="bottom"
            round
          >
            <VanPicker
              :title="field.label"
              :columns="[]"
              @confirm="(val: any) => onPickerConfirm(row._id, field.key, val)"
              @cancel="pickerVisible[`${row._id}_${field.key}`] = false"
            />
          </VanPopup>
        </template>
      </template>
    </template>
  </div>
</template>

<style scoped lang="scss">
.card-detail-table {
  width: 100%;
}

// ==================== 折叠摘要 ====================
.detail-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  background: var(--bg-muted);
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s;

  &:hover {
    background: var(--color-primary-light);
  }

  .summary-text {
    font-size: 14px;
    color: var(--text-1);
  }

  .summary-dot {
    margin: 0 6px;
    color: var(--text-3);
  }

  .summary-amount {
    color: var(--color-info);
    font-weight: 500;
  }

  .expand-btn {
    color: var(--text-1);
    font-size: 13px;
    cursor: pointer;
    user-select: none;

    &:hover {
      color: var(--color-primary);
    }
  }
}

// ==================== 展开/折叠动画 ====================
.detail-expand-wrapper {
  overflow: hidden;
  max-height: 0;
  transition: max-height 0.2s ease-in-out;

  &.is-expanded {
    max-height: 2000px;
  }
}

.detail-expanded {
  padding-top: 8px;
}

// ==================== 收起头部 ====================
.collapse-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 12px;
  margin-bottom: 8px;

  .collapse-title {
    font-size: 14px;
    font-weight: 500;
    color: var(--text-1);
  }

  .collapse-btn {
    color: var(--text-1);
    font-size: 13px;
    cursor: pointer;
    user-select: none;

    &:hover {
      color: var(--color-primary);
    }
  }
}

// ==================== PC 表格 ====================
.detail-table {
  :deep(.ant-table) {
    font-size: 13px;
  }

  :deep(.ant-table-tbody > tr > td) {
    padding: 6px 8px;
    height: 40px;
  }

  :deep(.ant-table-thead > tr > th) {
    padding: 8px;
    height: 40px;
  }

  &.editable {
    :deep(.ant-table-tbody > tr > td) {
      padding: 4px 6px;
    }
  }
}

.summary-row {
  background: var(--bg-muted);

  .summary-value {
    font-weight: 500;
    color: var(--color-info);
  }
}

// ==================== 添加按钮 ====================
.add-row-btn {
  margin-top: 8px;
  border-style: dashed;
  color: var(--text-2);

  &:hover {
    color: var(--color-primary);
    border-color: var(--color-primary);
  }
}

// ==================== 空状态 ====================
.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 80px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.2s;

  &:hover {
    border-color: var(--color-primary);
  }

  .empty-text {
    color: var(--text-3);
    font-size: 14px;
  }
}

// ==================== 移动端卡片 ====================
.mobile-card-list {
  .mobile-card {
    margin-bottom: 8px;
  }
}

.mobile-edit-list {
  :deep(.van-swipe-cell) {
    margin-bottom: 8px;
  }

  .mobile-edit-card {
    background: var(--bg-card);
  }

  .swipe-delete-btn {
    height: 100%;
  }
}

.mobile-summary {
  margin-top: 8px;

  .mobile-summary-title {
    font-weight: 500;
  }
}

.mobile-add-btn-wrapper {
  margin-top: 12px;
  padding: 0 12px;

  .mobile-add-btn {
    border-style: dashed;
    color: var(--text-2);
  }
}

// ==================== PC 紧凑模式（适用于窄抽屉） ====================
.compact-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.compact-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  overflow: hidden;
  transition: border-color 0.2s;

  &--editable:hover {
    border-color: var(--border-strong);
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 6px 10px;
    background: var(--bg-muted);
    border-bottom: 1px solid var(--border);
    min-height: 28px;
  }

  &__index {
    font-size: 12px;
    color: var(--text-3);
    font-weight: 500;
  }

  &__remove {
    padding: 0;
    height: auto;
    line-height: 1;
    font-size: 12px;
  }

  &__body {
    padding: 6px 10px;
  }

  &__row {
    display: grid;
    grid-template-columns: 76px 1fr;
    align-items: center;
    gap: 8px;
    padding: 4px 0;
    min-height: 32px;

    & + & {
      border-top: 1px dashed var(--border);
    }
  }

  &__label {
    font-size: 13px;
    color: var(--text-2);
    line-height: 1.4;
    word-break: break-all;
  }

  &__required {
    color: var(--color-danger);
    margin-right: 2px;
  }

  &__value {
    font-size: 13px;
    color: var(--text-1);
    word-break: break-all;
  }

  &__control {
    width: 100%;
    min-width: 0;

    :deep(.ant-input),
    :deep(.ant-input-number),
    :deep(.ant-select),
    :deep(.ant-picker) {
      width: 100% !important;
    }
  }
}

.compact-summary {
  margin-top: 10px;
  padding: 8px 12px;
  background: var(--bg-muted);
  border-radius: 6px;

  &__row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 3px 0;
    font-size: 13px;
  }

  &__label {
    color: var(--text-2);
  }

  &__value {
    color: var(--color-info);
    font-weight: 500;
  }
}
</style>
