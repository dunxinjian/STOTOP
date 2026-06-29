<template>
  <div class="batch-toolbar-wrapper">
  <div class="batch-fill-toolbar">
    <div class="fill-groups">
      <!-- 操作组1: 按区域填充 -->
      <div class="fill-group">
        <span class="group-label">按区域:</span>
        <a-select
          v-model:value="regionFill.region"
          placeholder="选择区域"
          size="small"
          style="width: 110px"
          allowClear
          :disabled="disabled"
        >
          <a-select-option v-for="r in regions" :key="r.value" :value="r.value">
            {{ r.label }}
          </a-select-option>
        </a-select>
        <a-select
          v-model:value="regionFill.segmentIndex"
          placeholder="选择重量段"
          size="small"
          style="width: 150px"
          allowClear
          :disabled="disabled"
          @change="onRegionSegmentChange"
        >
          <a-select-option v-for="(seg, idx) in segments" :key="seg.sortOrder" :value="idx">
            {{ seg.endWeight != null ? `${seg.startWeight}-${seg.endWeight}kg` : `${seg.startWeight}kg+` }}
          </a-select-option>
        </a-select>
        <a-input
          v-model:value="regionFill.inputValue"
          size="small"
          style="width: 130px"
          :placeholder="regionFillPlaceholder"
          :disabled="disabled"
          @pressEnter="handleRegionFill"
        />
        <a-button type="primary" size="small" :disabled="disabled" @click="handleRegionFill">填充</a-button>
        <!-- 区域调价 Popover -->
        <a-popover v-model:open="regionAdjustOpen" trigger="click" placement="bottomLeft">
          <template #content>
            <div class="adjust-popover">
              <a-select
                v-model:value="regionAdjust.target"
                size="small"
                style="width: 90px"
                :disabled="regionAdjustTargetDisabled"
              >
                <a-select-option value="all">全部</a-select-option>
                <a-select-option value="first">首重</a-select-option>
                <a-select-option value="continue">续重</a-select-option>
              </a-select>
              <a-input-number
                :value="(regionAdjust.delta as any)"
                @update:value="regionAdjust.delta = ($event as any)"
                size="small"
                :precision="2"
                style="width: 120px"
                placeholder="+/-调整值"
                :disabled="disabled"
                @pressEnter="handleRegionAdjust"
              />
              <a-button type="primary" size="small" :disabled="disabled" @click="handleRegionAdjust">调价</a-button>
            </div>
          </template>
          <a-button size="small" :disabled="disabled">调价▾</a-button>
        </a-popover>
      </div>

      <a-divider type="vertical" style="height: 24px; margin: 0 4px" />

      <!-- 操作组2: 按列填充 -->
      <div class="fill-group">
        <span class="group-label">按列:</span>
        <a-select
          v-model:value="columnFill.segmentIndex"
          placeholder="选择重量段"
          size="small"
          style="width: 150px"
          allowClear
          :disabled="disabled"
          @change="onColumnSegmentChange"
        >
          <a-select-option v-for="(seg, idx) in segments" :key="seg.sortOrder" :value="idx">
            {{ seg.endWeight != null ? `${seg.startWeight}-${seg.endWeight}kg` : `${seg.startWeight}kg+` }}
          </a-select-option>
        </a-select>
        <a-input
          v-model:value="columnFill.inputValue"
          size="small"
          style="width: 130px"
          :placeholder="columnFillPlaceholder"
          :disabled="disabled"
          @pressEnter="handleColumnFill"
        />
        <a-button type="primary" size="small" :disabled="disabled" @click="handleColumnFill">填充</a-button>
        <!-- 列调价 Popover -->
        <a-popover v-model:open="columnAdjustOpen" trigger="click" placement="bottomLeft">
          <template #content>
            <div class="adjust-popover">
              <a-select
                v-model:value="columnAdjust.target"
                size="small"
                style="width: 90px"
                :disabled="columnAdjustTargetDisabled"
              >
                <a-select-option value="all">全部</a-select-option>
                <a-select-option value="first">首重</a-select-option>
                <a-select-option value="continue">续重</a-select-option>
              </a-select>
              <a-input-number
                :value="(columnAdjust.delta as any)"
                @update:value="columnAdjust.delta = ($event as any)"
                size="small"
                :precision="2"
                style="width: 120px"
                placeholder="+/-调整值"
                :disabled="disabled"
                @pressEnter="handleColumnAdjust"
              />
              <a-button type="primary" size="small" :disabled="disabled" @click="handleColumnAdjust">调价</a-button>
            </div>
          </template>
          <a-button size="small" :disabled="disabled">调价▾</a-button>
        </a-popover>
      </div>
    </div>

    <!-- 右侧清空按鈕 -->
    <div class="toolbar-right">
      <a-popconfirm title="确定清空所有价格数据？" ok-text="确定" cancel-text="取消" @confirm="emit('clear')">
        <a-button danger size="small" :disabled="disabled">清空全部</a-button>
      </a-popconfirm>
    </div>
  </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { WeightSegment } from '../composables/usePriceMatrix'
import { validateCellInput } from '../composables/usePriceMatrix'

const props = withDefaults(defineProps<{
  segments: WeightSegment[]
  regions: { label: string; value: string }[]
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  (e: 'fill', params: {
    type: 'region' | 'segment' | 'all'
    regionIndex?: number
    segmentIndex?: number
    value: string
  }): void
  (e: 'clear'): void
  (e: 'adjust', params: {
    segmentIndex: number
    delta: number
    target: 'all' | 'first' | 'continue'
  }): void
}>()

// ---- 按区域填充状态 ----
const regionFill = reactive({
  region: undefined as string | undefined,
  segmentIndex: undefined as number | undefined,
  inputValue: '',
})

// ---- 按列填充状态 ----
const columnFill = reactive({
  segmentIndex: undefined as number | undefined,
  inputValue: '',
})

// ---- 区域调价 Popover ----
const regionAdjustOpen = ref(false)
const regionAdjust = reactive({
  target: 'all' as 'all' | 'first' | 'continue',
  delta: null as number | null,
})

// ---- 列调价 Popover ----
const columnAdjustOpen = ref(false)
const columnAdjust = reactive({
  target: 'all' as 'all' | 'first' | 'continue',
  delta: null as number | null,
})

// ---- 调价目标禁用状态 ----
const regionAdjustTargetDisabled = computed(() => {
  return regionFill.segmentIndex == null
})

const columnAdjustTargetDisabled = computed(() => {
  return columnFill.segmentIndex == null
})

// ---- 计算 placeholder ----
function getPlaceholder(segmentIndex: number | undefined): string {
  if (segmentIndex == null) return '输入价格'
  const seg = props.segments[segmentIndex]
  if (!seg) return '输入价格'
  return '格式: 单价 或 单价+续重'
}

const regionFillPlaceholder = computed(() => getPlaceholder(regionFill.segmentIndex))
const columnFillPlaceholder = computed(() => getPlaceholder(columnFill.segmentIndex))

function onRegionSegmentChange() {
  regionFill.inputValue = ''
}

function onColumnSegmentChange() {
  columnFill.inputValue = ''
}

// ---- 校验并触发填充 ----
function doFill(
  segmentIndex: number | undefined,
  inputValue: string,
  type: 'region' | 'segment',
  regionValue?: string,
) {
  if (segmentIndex == null) {
    message.warning('请选择重量段')
    return
  }
  if (type === 'region' && !regionValue) {
    message.warning('请选择区域')
    return
  }
  const seg = props.segments[segmentIndex]
  if (!seg) return

  const trimmed = inputValue.trim()
  if (!trimmed) {
    message.warning('请输入填充值')
    return
  }

  const validation = validateCellInput(trimmed)
  if (!validation.valid) {
    message.warning(validation.error || '输入格式不正确')
    return
  }

  emit('fill', {
    type,
    regionIndex: type === 'region' ? props.regions.findIndex(r => r.value === regionValue) : undefined,
    segmentIndex,
    value: trimmed,
  })
}

function handleRegionFill() {
  doFill(regionFill.segmentIndex, regionFill.inputValue, 'region', regionFill.region)
}

function handleColumnFill() {
  doFill(columnFill.segmentIndex, columnFill.inputValue, 'segment')
}

// ---- 调价 ----
function doAdjust(
  segmentIndex: number | undefined,
  delta: number | null,
  target: 'all' | 'first' | 'continue',
  regionValue?: string,
) {
  if (segmentIndex == null) {
    message.warning('请选择重量段')
    return
  }
  if (delta == null || delta === 0) {
    message.warning('请输入调整值')
    return
  }
  const seg = props.segments[segmentIndex]
  if (!seg) return

  emit('adjust', {
    segmentIndex,
    delta,
    target,
    ...(regionValue ? { regionValue } : {}),
  } as any)
}

function handleRegionAdjust() {
  doAdjust(regionFill.segmentIndex, regionAdjust.delta, regionAdjust.target, regionFill.region)
  regionAdjustOpen.value = false
}

function handleColumnAdjust() {
  doAdjust(columnFill.segmentIndex, columnAdjust.delta, columnAdjust.target)
  columnAdjustOpen.value = false
}
</script>

<style scoped lang="scss">
.batch-fill-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 0;
  flex-wrap: wrap;
  gap: 4px;
}

.fill-groups {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.fill-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.group-label {
  font-size: 13px;
  color: var(--text-2);
  white-space: nowrap;
}

.toolbar-right {
  flex-shrink: 0;
}

.adjust-popover {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 0;
}
</style>
