<template>
  <div class="segment-toolbar">
    <!-- 定价模式 -->
    <div class="toolbar-section">
      <span class="section-label">定价模式:</span>
      <a-radio-group
        :value="matrix.pricingScope"
        size="small"
        :disabled="readonly"
        button-style="solid"
        @change="onScopeChange"
      >
        <a-radio-button value="national">全国统一</a-radio-button>
        <a-radio-button value="province">按省份</a-radio-button>
      </a-radio-group>
    </div>

    <!-- 重量段管理 -->
    <div class="toolbar-section segment-section">
      <span class="section-label">段:</span>
      <div class="segment-list">
        <a-popover
          v-for="(seg, idx) in matrix.segments"
          :key="seg.index"
          trigger="click"
          placement="bottom"
          :open="editingSegIdx === idx"
          @openChange="(visible: boolean) => { if (!visible && editingSegIdx === idx) editingSegIdx = null }"
        >
          <template #content>
            <div class="seg-edit-form" @click.stop>
              <a-form layout="vertical" :colon="false" size="small">
                <a-form-item label="进位方式">
                  <a-select
                    v-model:value="segEditForm.roundingMethod"
                    :options="roundingMethodOptions"
                    style="width: 100%"
                  />
                </a-form-item>
                <template v-if="segEditForm.roundingMethod === 4 || segEditForm.roundingMethod === 6 || segEditForm.roundingMethod === 7">
                  <div style="display: flex; gap: 8px">
                    <a-form-item label="舍位" style="flex: 1">
                      <a-input-number
                        v-model:value="segEditForm.truncParam"
                        :precision="2"
                        :min="0"
                        :step="0.1"
                        placeholder="舍位参数"
                        style="width: 100%"
                      />
                    </a-form-item>
                    <a-form-item label="进位" style="flex: 1">
                      <a-input-number
                        v-model:value="segEditForm.ceilParam"
                        :precision="2"
                        :min="0"
                        :step="0.1"
                        placeholder="进位参数"
                        style="width: 100%"
                      />
                    </a-form-item>
                  </div>
                </template>
                <div class="seg-edit-actions">
                  <a-button size="small" @click="editingSegIdx = null">取消</a-button>
                  <a-button size="small" type="primary" @click="confirmSegEdit(idx)">确认</a-button>
                </div>
              </a-form>
            </div>
          </template>
          <a-tag
            color="blue"
            class="segment-tag"
            @click="openSegEdit(idx)"
          >
            {{ formatSegment(seg) }}
            <span class="seg-rounding-hint">{{ formatRoundingHint(seg) }}</span>
          </a-tag>
        </a-popover>

        <a-popover
          v-if="!readonly"
          v-model:open="addPopoverVisible"
          trigger="click"
          placement="bottomLeft"
          :destroy-tooltip-on-hide="true"
          @open-change="onPopoverOpenChange"
        >
          <template #title>添加重量段</template>
          <template #content>
            <div class="add-seg-form">
              <div class="form-row">
                <span class="form-label">起始重量(kg):</span>
                <a-input-number
                  v-model:value="newSegForm.weightFrom"
                  :min="0"
                  :precision="2"
                  size="small"
                  style="width: 110px"
                />
              </div>
              <div class="form-row">
                <span class="form-label">截止重量(kg):</span>
                <a-input-number
                  v-model:value="newSegForm.weightTo"
                  :min="0"
                  :precision="2"
                  size="small"
                  style="width: 110px"
                  placeholder="留空=开放段"
                />
              </div>
              <div class="form-row">
                <span class="form-label">进位方式:</span>
                <a-select
                  v-model:value="newSegForm.roundingMethod"
                  :options="roundingMethodOptions"
                  size="small"
                  style="width: 140px"
                />
              </div>
              <template v-if="newSegForm.roundingMethod === 4 || newSegForm.roundingMethod === 6 || newSegForm.roundingMethod === 7">
                <div class="form-row">
                  <span class="form-label">舍位:</span>
                  <a-input-number
                    v-model:value="newSegForm.truncParam"
                    :precision="2"
                    :min="0"
                    :step="0.1"
                    size="small"
                    style="width: 110px"
                    placeholder="舍位参数"
                  />
                </div>
                <div class="form-row">
                  <span class="form-label">进位:</span>
                  <a-input-number
                    v-model:value="newSegForm.ceilParam"
                    :precision="2"
                    :min="0"
                    :step="0.1"
                    size="small"
                    style="width: 110px"
                    placeholder="进位参数"
                  />
                </div>
              </template>
              <div v-if="addError" class="form-error">{{ addError }}</div>
              <div class="form-actions">
                <a-button size="small" @click="addPopoverVisible = false">取消</a-button>
                <a-button type="primary" size="small" @click="handleConfirmAdd">确认</a-button>
              </div>
            </div>
          </template>
          <a-button size="small" type="dashed">
            <template #icon><PlusOutlined /></template>
            添加段
          </a-button>
        </a-popover>
      </div>
    </div>

    <!-- 批量填充 -->
    <div v-if="!readonly" class="toolbar-section toolbar-right">
      <a-button size="small" @click="emit('batchFill')">
        <template #icon><EditOutlined /></template>
        批量填充
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { EditOutlined, PlusOutlined } from '@ant-design/icons-vue'
import type { RadioChangeEvent } from 'ant-design-vue'
import type { CostItemMatrix, CostWeightSegment } from '../composables/useCostMatrix'
import { roundingMethodOptions } from '@/views/express/quotation/composables/useQuotationForm'

interface Props {
  matrix: CostItemMatrix
  readonly?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  readonly: false,
})

const emit = defineEmits<{
  (e: 'switchPricingScope', scope: 'national' | 'province'): void
  (e: 'addSegment', segment: { weightFrom: number; weightTo: number | null; roundingMethod: number; truncParam: number | null; ceilParam: number | null }): void
  (e: 'removeSegment', index: number): void
  (e: 'updateSegment', index: number, updates: Partial<CostWeightSegment>): void
  (e: 'batchFill'): void
}>()

const addPopoverVisible = ref(false)
const addError = ref('')

const newSegForm = reactive({
  weightFrom: 0 as number,
  weightTo: undefined as number | undefined,
  roundingMethod: 1 as number,
  truncParam: undefined as number | undefined,
  ceilParam: undefined as number | undefined,
})

// ==================== 段属性编辑 ====================

const editingSegIdx = ref<number | null>(null)

const segEditForm = reactive({
  roundingMethod: 1 as number,
  truncParam: undefined as number | undefined,
  ceilParam: undefined as number | undefined,
})

function openSegEdit(idx: number) {
  if (props.readonly) return
  const seg = props.matrix.segments[idx]
  if (!seg) return
  segEditForm.roundingMethod = seg.roundingMethod
  segEditForm.truncParam = seg.truncParam ?? undefined
  segEditForm.ceilParam = seg.ceilParam ?? undefined
  editingSegIdx.value = idx
}

function confirmSegEdit(idx: number) {
  emit('updateSegment', idx, {
    roundingMethod: segEditForm.roundingMethod,
    truncParam: (segEditForm.roundingMethod === 4 || segEditForm.roundingMethod === 6 || segEditForm.roundingMethod === 7)
      ? (segEditForm.truncParam ?? null) : null,
    ceilParam: (segEditForm.roundingMethod === 4 || segEditForm.roundingMethod === 6 || segEditForm.roundingMethod === 7)
      ? (segEditForm.ceilParam ?? null) : null,
  })
  editingSegIdx.value = null
}

// ==================== 显示格式化 ====================

function formatSegment(seg: CostWeightSegment): string {
  if (seg.weightTo == null) {
    return `${seg.weightFrom}kg以上`
  }
  return `${seg.weightFrom}-${seg.weightTo}kg`
}

function formatRoundingHint(seg: CostWeightSegment): string {
  const opt = roundingMethodOptions.find(o => o.value === seg.roundingMethod)
  if (!opt) return ''
  if ((seg.roundingMethod === 4 || seg.roundingMethod === 6 || seg.roundingMethod === 7)
    && (seg.truncParam != null || seg.ceilParam != null)) {
    const parts: string[] = []
    if (seg.truncParam != null) parts.push(`舍${seg.truncParam}`)
    if (seg.ceilParam != null) parts.push(`进${seg.ceilParam}`)
    return `·${opt.label}(${parts.join('/')})`
  }
  return `·${opt.label}`
}

// ==================== 定价模式 ====================

function onScopeChange(e: RadioChangeEvent) {
  const value = e.target.value as 'national' | 'province'
  if (value === props.matrix.pricingScope) return
  emit('switchPricingScope', value)
}

// ==================== 添加段 ====================

function onPopoverOpenChange(open: boolean) {
  if (!open) {
    addError.value = ''
    return
  }
  // 打开时根据最后一段填充默认值
  const lastSeg = props.matrix.segments[props.matrix.segments.length - 1]
  const defaultFrom = lastSeg
    ? (lastSeg.weightTo ?? (lastSeg.weightFrom + 1))
    : 0
  newSegForm.weightFrom = defaultFrom
  newSegForm.weightTo = undefined
  newSegForm.roundingMethod = lastSeg?.roundingMethod ?? 1
  newSegForm.truncParam = lastSeg?.truncParam ?? undefined
  newSegForm.ceilParam = lastSeg?.ceilParam ?? undefined
  addError.value = ''
}

function handleConfirmAdd() {
  addError.value = ''
  const from = newSegForm.weightFrom
  const to = newSegForm.weightTo
  if (from == null || from < 0) {
    addError.value = '起始重量不能为空且需≥0'
    return
  }
  if (to != null && to <= from) {
    addError.value = '截止重量必须大于起始重量'
    return
  }

  // 检查是否在开放段之后再添加
  const lastSeg = props.matrix.segments[props.matrix.segments.length - 1]
  if (lastSeg && lastSeg.weightTo == null) {
    message.warning('开放段之后无法继续添加段，请先调整最后一段')
    return
  }

  emit('addSegment', {
    weightFrom: from,
    weightTo: to ?? null,
    roundingMethod: newSegForm.roundingMethod,
    truncParam: (newSegForm.roundingMethod === 4 || newSegForm.roundingMethod === 6 || newSegForm.roundingMethod === 7)
      ? (newSegForm.truncParam ?? null) : null,
    ceilParam: (newSegForm.roundingMethod === 4 || newSegForm.roundingMethod === 6 || newSegForm.roundingMethod === 7)
      ? (newSegForm.ceilParam ?? null) : null,
  })
  addPopoverVisible.value = false
}
</script>

<style scoped lang="scss">
.segment-toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  padding: 8px 12px;
  background-color: #fafafa;
  border-bottom: 1px solid #f0f0f0;
}

.toolbar-section {
  display: flex;
  align-items: center;
  gap: 6px;
  padding-right: 12px;
  border-right: 1px solid #f0f0f0;

  &:last-child {
    border-right: none;
    padding-right: 0;
  }

  & + .toolbar-section {
    padding-left: 0;
  }
}

.section-label {
  font-size: 13px;
  color: #666;
  white-space: nowrap;
}

.segment-section {
  flex: 1;
  min-width: 0;
}

.segment-list {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}

.segment-tag {
  margin-right: 0;
  font-size: 12px;
  line-height: 22px;
  padding: 0 8px;
  user-select: none;
  cursor: pointer;
}

.seg-rounding-hint {
  font-size: 11px;
  color: rgba(0, 0, 0, 0.45);
}

.toolbar-right {
  margin-left: auto;
  border-right: none;
  padding-right: 0;
}

.add-seg-form {
  width: 260px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.seg-edit-form {
  width: 280px;
  padding: 4px 0;
}

.seg-edit-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
}

.form-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.form-label {
  font-size: 13px;
  color: #555;
  white-space: nowrap;
}

.form-error {
  color: var(--color-danger);
  font-size: 12px;
  line-height: 1.4;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 4px;
}
</style>
