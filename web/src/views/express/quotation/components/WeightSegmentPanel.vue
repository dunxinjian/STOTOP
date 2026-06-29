<template>
  <div class="weight-segment-panel">
    <div class="segment-label">重量段</div>
    <div class="segment-tags">
      <a-popover
        v-for="(seg, index) in segments"
        :key="seg.id || `seg-${seg.sortOrder}-${seg.startWeight}`"
        trigger="click"
        placement="bottom"
        :open="editingIndex === index"
        @openChange="(visible: boolean) => handlePopoverChange(visible, index)"
      >
        <template #content>
          <div class="segment-edit-form" @click.stop>
            <a-form layout="vertical" :colon="false" size="small">
              <a-form-item label="重量进位方式">
                <a-select
                  v-model:value="editForm.roundingMethod"
                  :options="roundingMethodOptions"
                  style="width: 100%"
                />
              </a-form-item>
              <template v-if="editForm.roundingMethod === 4 || editForm.roundingMethod === 6 || editForm.roundingMethod === 7">
                <div style="display: flex; gap: 8px">
                  <a-form-item label="舍位" style="flex: 1">
                    <a-input-number
                      v-model:value="editForm.truncParam"
                      :precision="2"
                      :min="0"
                      :step="0.1"
                      placeholder="舍位参数"
                      style="width: 100%"
                    />
                  </a-form-item>
                  <a-form-item label="进位" style="flex: 1">
                    <a-input-number
                      v-model:value="editForm.ceilParam"
                      :precision="2"
                      :min="0"
                      :step="0.1"
                      placeholder="进位参数"
                      style="width: 100%"
                    />
                  </a-form-item>
                </div>
              </template>
              <a-divider style="margin: 8px 0" />
              <a-form-item label="分割当前段" style="margin-bottom: 4px">
                <div style="display: flex; gap: 8px">
                  <a-input-number
                    :value="(editForm.splitPoint as any)"
                    @update:value="editForm.splitPoint = ($event as any)"
                    :precision="2"
                    :min="seg.startWeight + 0.01"
                    :max="seg.endWeight != null ? seg.endWeight - 0.01 : undefined"
                    placeholder="输入分割点(kg)"
                    style="flex: 1"
                    @pressEnter="handleSplit(index)"
                  />
                  <a-button
                    size="small"
                    type="primary"
                    :disabled="!editForm.splitPoint"
                    @click="handleSplit(index)"
                  >
                    分割
                  </a-button>
                </div>
                <!-- 分割预览：左段自动继承当前段价格，右段可输入快捷定价 -->
                <template v-if="editForm.splitPoint">
                  <div class="split-preview">
                    <!-- 左段：只读展示当前段价格 -->
                    <div class="split-preview-row">
                      <span class="split-range-tag left">左段 {{ seg.startWeight }}-{{ editForm.splitPoint }}kg</span>
                      <span class="split-price-readonly">
                        {{ (editForm.splitLeftContinuePrice != null)
                          ? `首重 ${editForm.splitLeftFirstPrice?.toFixed(2) ?? '-'} + 续重 ${editForm.splitLeftContinuePrice?.toFixed(2) ?? '-'}`
                          : (editForm.splitLeftPrice != null ? editForm.splitLeftPrice.toFixed(2) : '无价格') }}
                      </span>
                      <span class="split-inherit-tip">自动继承</span>
                    </div>
                    <!-- 右段：可选输入快捷定价（单值=固定单价，首重+续重=首续重，由后端自动派生） -->
                    <div class="split-preview-row">
                      <span class="split-range-tag right">右段 {{ editForm.splitPoint }}-{{ seg.endWeight != null ? seg.endWeight : '∞' }}kg</span>
                      <a-input-number
                        :value="(editForm.splitRightFirstPrice as any)"
                        @update:value="editForm.splitRightFirstPrice = ($event as any)"
                        :precision="2" :min="0" size="small"
                        placeholder="首重价"
                        style="width: 80px"
                      />
                      <span style="color:var(--text-3);font-size:12px">+</span>
                      <a-input-number
                        :value="(editForm.splitRightContinuePrice as any)"
                        @update:value="editForm.splitRightContinuePrice = ($event as any)"
                        :precision="2" :min="0" size="small"
                        placeholder="续重价（可选）"
                        style="width: 80px"
                      />
                    </div>
                  </div>
                </template>
              </a-form-item>
              <div class="segment-edit-actions">
                <a-button size="small" @click="cancelEdit">取消</a-button>
                <a-button size="small" type="primary" @click="confirmEdit">确认</a-button>
              </div>
            </a-form>
          </div>
        </template>

        <div
          class="segment-card"
          :class="{ active: editingIndex === index, disabled }"
          @click="openEdit(index)"
        >
          <a-tooltip
            v-if="segments.length > 1 && !disabled"
            title="与相邻段合并"
          >
            <span
              class="segment-close"
              @click.stop="handleMerge(index)"
            >×</span>
          </a-tooltip>
          <div class="segment-range">{{ formatRange(seg) }}</div>
          <div class="segment-method">{{ formatMethod(seg) }}</div>
        </div>
      </a-popover>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { message } from 'ant-design-vue'
import type { WeightSegment } from '../composables/usePriceMatrix'
import type { ProvinceRow } from '../composables/usePriceMatrix'
import { roundingMethodOptions } from '../composables/useQuotationForm'

interface Props {
  segments: WeightSegment[]
  matrix?: ProvinceRow[]
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
})

const emit = defineEmits<{
  'update:segments': [segments: WeightSegment[]]
  'split': [index: number, splitPoint: number]
  'merge': [index: number]
  'update': [index: number, data: Partial<WeightSegment>]
  'fillSegmentPrice': [segmentIndex: number, price: { basePrice?: number; continuePrice?: number }]
}>()

// ==================== 编辑状态 ====================

const editingIndex = ref<number | null>(null)

interface EditForm {
  roundingMethod: number
  truncParam: number | undefined
  ceilParam: number | undefined
  splitPoint: number | null
  // 左段价格（只读，当前段代表价格）
  splitLeftPrice: number | null
  splitLeftFirstPrice: number | null
  splitLeftContinuePrice: number | null
  // 右段价格（可输入）
  splitRightFirstPrice: number | null
  splitRightContinuePrice: number | null
}

const editForm = reactive<EditForm>({
  roundingMethod: 1,
  truncParam: undefined,
  ceilParam: undefined,
  splitPoint: null,
  splitLeftPrice: null,
  splitLeftFirstPrice: null,
  splitLeftContinuePrice: null,
  splitRightFirstPrice: null,
  splitRightContinuePrice: null,
})

// ==================== 显示格式化 ====================

function formatRange(seg: WeightSegment): string {
  if (seg.endWeight == null) {
    return `${seg.startWeight}以上`
  }
  return `${seg.startWeight}-${seg.endWeight}kg`
}

function formatMethod(seg: WeightSegment): string {
  // 展示进位方式名称
  const rm = roundingMethodOptions.find(o => o.value === seg.roundingMethod)
  return rm?.label ?? '标准'
}

// ==================== Popover 操作 ====================

function handlePopoverChange(visible: boolean, index: number) {
  if (!visible && editingIndex.value === index) {
    editingIndex.value = null
  }
}

function openEdit(index: number) {
  if (props.disabled) return
  const seg = props.segments[index]
  editForm.roundingMethod = seg.roundingMethod
  editForm.truncParam = seg.truncParam ?? undefined
  editForm.ceilParam = seg.ceilParam ?? undefined
  editForm.splitPoint = null
  // 自动读取当前段代表价格（取第一个非空行）回填左段
  let initLeft: { basePrice?: number; continuePrice?: number } = {}
  if (props.matrix && seg) {
    for (const row of props.matrix) {
      const cell = row.prices[seg.sortOrder]
      if (!cell) continue
      if (cell.basePrice != null || cell.continuePrice != null) {
        initLeft = { basePrice: cell.basePrice ?? undefined, continuePrice: cell.continuePrice ?? undefined }; break
      }
    }
  }
  editForm.splitLeftPrice = initLeft.basePrice ?? null
  editForm.splitLeftFirstPrice = initLeft.basePrice ?? null
  editForm.splitLeftContinuePrice = initLeft.continuePrice ?? null
  editForm.splitRightFirstPrice = null
  editForm.splitRightContinuePrice = null
  editingIndex.value = index
}

function cancelEdit() {
  editingIndex.value = null
}

function confirmEdit() {
  const index = editingIndex.value
  if (index == null) return

  const data: Partial<WeightSegment> = {
    roundingMethod: editForm.roundingMethod,
    truncParam: (editForm.roundingMethod === 4 || editForm.roundingMethod === 6 || editForm.roundingMethod === 7)
      ? (editForm.truncParam ?? null)
      : null,
    ceilParam: (editForm.roundingMethod === 4 || editForm.roundingMethod === 6 || editForm.roundingMethod === 7)
      ? (editForm.ceilParam ?? null)
      : null,
  }

  emit('update', index, data)
  editingIndex.value = null
}

// ==================== 分割/合并 ====================

function handleSplit(index: number) {
  const seg = props.segments[index]
  const point = editForm.splitPoint
  if (point == null) {
    message.warning('请输入分割点')
    return
  }
  if (point <= seg.startWeight) {
    message.warning('分割点必须大于起始重量')
    return
  }
  if (seg.endWeight != null && point >= seg.endWeight) {
    message.warning('分割点必须小于截止重量')
    return
  }
  emit('split', index, point)

  // 分割后快捷定价：左段自动继承无需处理，右段可选填充
  const rightPayload: { basePrice?: number; continuePrice?: number } = {}
  if (editForm.splitRightFirstPrice != null) rightPayload.basePrice = editForm.splitRightFirstPrice
  if (editForm.splitRightContinuePrice != null) rightPayload.continuePrice = editForm.splitRightContinuePrice
  if (Object.keys(rightPayload).length > 0) emit('fillSegmentPrice', index + 1, rightPayload)

  editingIndex.value = null
}

function handleMerge(index: number) {
  emit('merge', index)
}
</script>

<style scoped lang="scss">
.weight-segment-panel {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.segment-label {
  flex-shrink: 0;
  line-height: 32px;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
  white-space: nowrap;
}

.segment-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.segment-card {
  position: relative;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  padding: 6px 16px;
  border: 1px solid var(--border);
  border-radius: 4px;
  background: var(--bg-muted);
  cursor: pointer;
  min-width: 80px;
  transition: all 0.2s;
  user-select: none;

  &:hover {
    border-color: var(--color-primary);
  }

  &.active {
    border-color: var(--color-primary);
    background: var(--color-primary-light);
  }

  &.disabled {
    cursor: not-allowed;
    opacity: 0.6;
  }
}

.segment-range {
  font-size: 13px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
  white-space: nowrap;
}

.segment-method {
  font-size: 11px;
  color: rgba(0, 0, 0, 0.45);
  white-space: nowrap;
  margin-top: 2px;
}

.segment-close {
  position: absolute;
  top: -6px;
  right: -6px;
  width: 16px;
  height: 16px;
  line-height: 14px;
  text-align: center;
  font-size: 12px;
  color: var(--text-on-accent);
  background: var(--color-danger);
  border-radius: 50%;
  cursor: pointer;
  display: none;
  z-index: 1;

  .segment-card:hover & {
    display: block;
  }
}

.segment-edit-form {
  width: 320px;
  padding: 4px 0;
}

.segment-edit-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
}

.split-preview {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.split-preview-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.split-price-readonly {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.65);
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 1px 8px;
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.split-inherit-tip {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--color-info);
  background: var(--color-info-light);
  border: 1px solid var(--color-info);
  border-radius: 3px;
  padding: 1px 5px;
  white-space: nowrap;
}

.split-range-tag {
  flex-shrink: 0;
  font-size: 12px;
  font-weight: 500;
  padding: 1px 6px;
  border-radius: 3px;
  white-space: nowrap;

  &.left {
    background: var(--color-info-light);
    color: var(--color-info);
    border: 1px solid var(--color-info);
  }

  &.right {
    background: var(--color-success-light);
    color: var(--color-success);
    border: 1px solid var(--color-success);
  }
}
</style>
