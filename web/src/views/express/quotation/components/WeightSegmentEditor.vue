<template>
  <div
    class="weight-segment-panel"
    :class="{
      'mode-cards-only': mode === 'cards-only',
      'mode-ruler-only': mode === 'ruler-only',
    }"
  >
    <div v-if="mode !== 'ruler-only' && mode !== 'dialog-only'" class="segment-label">重量段</div>
    <div class="segment-content">
      <!-- ===== 重量段信息条（单行紧凑，点击打开属性编辑 Modal） ===== -->
      <div
        v-if="mode !== 'ruler-only' && mode !== 'dialog-only'"
        class="segment-tags"
        :class="{ 'with-margin': mode === 'full' }"
      >
        <div
          v-for="(seg, index) in segments"
          :key="seg.id ?? `new-${index}`"
          class="segment-card"
          :class="{ active: editingIndex === index }"
          @click="openPropertiesDialog(index)"
        >
          <a-tooltip
            v-if="segments.length > 1"
            title="与相邻段合并（列头点击 ‹ / › 可指定合并方向）"
          >
            <span
              class="segment-close"
              @click.stop="emit('merge', index)"
            >×</span>
          </a-tooltip>
          <span class="segment-range">{{ formatRange(seg) }}</span>
          <span class="segment-sep">·</span>
          <span class="segment-method-text">{{ formatMethod(seg) }}</span>
          <span class="segment-sep">·</span>
          <span class="segment-rounding-text">{{ formatRounding(seg) }}</span>
          <span class="segment-edit-hint">✏</span>
        </div>
      </div>

      <!-- ===== 拖动标尺：在标尺上拖动到目标位置松开即可分割 ===== -->
      <div
        v-if="mode !== 'cards-only' && mode !== 'dialog-only'"
        class="ruler-wrapper"
        :class="{ 'sticky-bottom': rulerStickyBottom, 'embedded': mode === 'ruler-only' }"
      >
        <div class="ruler-hint" :style="leftOffsetPx ? { paddingLeft: leftOffsetPx + 'px' } : undefined">
          <CaretDownFilled />
          <span>在标尺上按住拖动到目标位置松开即可分割重量段</span>
        </div>
        <div
          class="ruler-positioner"
          :style="rulerPositionerStyle"
        >
          <div
            ref="rulerEl"
            class="ruler"
            @mousedown="onRulerMouseDown"
            @mousemove="onRulerMouseMove"
            @mouseleave="onRulerMouseLeave"
          >
            <div
              v-for="(seg, idx) in displaySegments"
              :key="seg.key"
              class="ruler-seg"
              :class="{ infinity: seg.isInfinity }"
              :style="{ flex: seg.flex }"
            >
              <span
                v-if="idx === displaySegments.length - 1 && seg.isInfinity"
                class="ruler-infinity"
              >∞</span>
            </div>

            <!-- 关键刻度节点标注 -->
            <div class="ruler-marks">
              <div
                v-for="t in keyTicksOnRuler"
                :key="t.value"
                class="ruler-mark"
                :style="{ left: t.leftPercent + '%' }"
              >
                <div class="ruler-mark-line"></div>
                <div
                  v-if="visibleLabelValues.has(t.value)"
                  class="ruler-mark-label"
                >{{ t.value }}</div>
              </div>
            </div>

            <!-- 密度分界提示（3kg处虚线，仅 standalone 模式） -->
            <div
              v-if="showDensityDivider"
              class="ruler-density-divider"
              :style="{ left: densityDividerLeftPercent + '%' }"
            >
              <a-tooltip title="前 3kg 占标尺 50% 宽度（小重量可精细操作），3kg 以上压缩显示">
                <div class="ruler-density-divider-line"></div>
              </a-tooltip>
            </div>

            <!-- 拖动预览游标（hover：灰色轻量提示；drag：红/绿色手柄） -->
            <div
              v-show="dragPreview.visible"
              class="ruler-cursor"
              :class="{
                snapped: dragPreview.snapped,
                hovering: dragPreview.mode === 'hover',
                dragging: dragPreview.mode === 'drag',
              }"
              :style="{ left: dragPreview.leftPx + 'px' }"
            >
              <div class="ruler-cursor-line"></div>
              <div class="ruler-cursor-bubble">
                <template v-if="dragPreview.mode === 'hover'">
                  当前 {{ dragPreview.weight.toFixed(2) }}kg
                </template>
                <template v-else>
                  <span v-if="dragPreview.snapped" class="snap-icon">⚡</span>
                  {{ dragPreview.weight.toFixed(2) }}kg
                </template>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ===== 段属性编辑 Modal（从列头或卡片点击触发） ===== -->
    <a-modal
      :open="editingIndex !== null"
      :title="editingIndex !== null && segments[editingIndex]
        ? `编辑重量段属性 · ${formatRange(segments[editingIndex])}`
        : '编辑重量段属性'"
      width="480px"
      ok-text="确认"
      cancel-text="取消"
      destroy-on-close
      @ok="confirmEdit"
      @cancel="cancelEdit"
    >
      <a-form layout="vertical" :colon="false">
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
      </a-form>
    </a-modal>

    <!-- ===== 分割确认弹窗 ===== -->
    <a-modal
      v-model:open="splitDialog.visible"
      title="确认分割重量段"
      width="500px"
      :ok-text="splitOkText"
      cancel-text="取消"
      :ok-button-props="{ disabled: !splitPointValid }"
      @ok="confirmSplit"
    >
      <a-form
        v-if="splitDialog.segIdx >= 0 && splitTargetSeg"
        layout="vertical"
        :colon="false"
      >
        <a-form-item label="待分割段">
          <span class="dialog-readonly">
            {{ formatRange(splitTargetSeg) }}（{{ formatMethod(splitTargetSeg) }}）
          </span>
        </a-form-item>
        <a-form-item
          label="分割点 (kg)"
          :validate-status="splitPointValid ? '' : 'error'"
          :help="splitPointError || '可手动微调。快捷选项：点击下方按钮一键填充'"
        >
          <a-input-number
            :value="(splitDialog.point as any)"
            @update:value="splitDialog.point = ($event as any)"
            :precision="2"
            :min="splitTargetSeg.startWeight + 0.01"
            :max="splitTargetSeg.endWeight != null ? (splitTargetSeg.endWeight as number) - 0.01 : undefined"
            :step="0.1"
            style="width: 100%"
          />
          <div v-if="splitDialogSnapOptions.length > 0" class="snap-shortcuts">
            <span class="snap-shortcuts-label">常用：</span>
            <a-tag
              v-for="sp in splitDialogSnapOptions"
              :key="sp"
              :color="splitDialog.point === sp ? 'green' : 'default'"
              class="snap-tag"
              @click="splitDialog.point = sp"
            >{{ sp }}kg</a-tag>
          </div>
        </a-form-item>
        <a-divider orientation="left" plain style="margin: 4px 0 12px">分割后预览</a-divider>
        <!-- 左段 -->
        <div class="split-row">
          <span class="split-tag left">
            左段 {{ splitTargetSeg.startWeight }}-{{ splitDialog.point ?? '?' }}kg
          </span>
          <span class="split-readonly">
            <span class="split-readonly">
              {{ splitDialog.leftFixedPrice != null ? splitDialog.leftFixedPrice.toFixed(2) : '无价格' }}
            </span>
          </span>
          <span class="split-tip">自动继承</span>
        </div>
        <!-- 右段 -->
        <div class="split-row">
          <span class="split-tag right">
            右段 {{ splitDialog.point ?? '?' }}-{{ splitTargetSeg.endWeight != null ? splitTargetSeg.endWeight : '∞' }}kg
          </span>
          <template v-if="splitTargetSeg">
            <a-input-number
              :value="(splitDialog.rightFixedPrice as any)"
              @update:value="splitDialog.rightFixedPrice = ($event as any)"
              :precision="2"
              :min="0"
              placeholder="快捷定价（可选）"
              style="flex: 1"
            />
          </template>
        </div>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import { CaretDownFilled } from '@ant-design/icons-vue'
import type { WeightSegment, ProvinceRow } from '../composables/usePriceMatrix'
import { roundingMethodOptions } from '@/views/express/quotation/composables/useQuotationForm'

const props = withDefaults(defineProps<{
  segments: WeightSegment[]
  matrix?: ProvinceRow[]
  /** 对齐到矩阵：每段对应的矩阵列宽（px）。传入后启用 aligned 模式，禁用密度映射 */
  columnWidths?: number[]
  /** 标尺左侧需要避让的固定列宽度（px），用于与矩阵的固定列对齐 */
  leftOffsetPx?: number
  /** 标尺总宽度（px）：aligned 模式下明确限定为 leftOffsetPx + sum(columnWidths)，避免标尺撑满容器 */
  rulerTotalWidth?: number
  /** 紧贴模式：移除标尺底部间距，由父组件控制与下方组件零间距 */
  rulerStickyBottom?: boolean
  /** 渲染模式：full = 卡片+标尺；cards-only = 仅卡片（列头点击编辑入口）；ruler-only = 仅标尺（嵌入矩阵头部）；dialog-only = 仅承载属性/拆分弹窗（无可见 UI） */
  mode?: 'full' | 'cards-only' | 'ruler-only' | 'dialog-only'
}>(), {
  leftOffsetPx: 0,
  rulerStickyBottom: false,
  mode: 'full',
})

// 标尺定位器样式：padding-left 避让固定列 + width 强制总宽（与 a-table maxWidth 严格一致）
const rulerPositionerStyle = computed<Record<string, string> | undefined>(() => {
  const style: Record<string, string> = {}
  if (props.leftOffsetPx) style.paddingLeft = props.leftOffsetPx + 'px'
  if (props.rulerTotalWidth && props.rulerTotalWidth > 0) {
    style.width = props.rulerTotalWidth + 'px'
    style.maxWidth = '100%'
  }
  return Object.keys(style).length > 0 ? style : undefined
})

const emit = defineEmits<{
  'split': [index: number, splitPoint: number]
  'merge': [index: number]
  'update': [index: number, data: Partial<WeightSegment>]
  'fillSegmentPrice': [segmentIndex: number, price: { basePrice?: number; continuePrice?: number }]
}>()

// ==================== 显示格式化 ====================

function formatRange(seg: WeightSegment): string {
  if (seg.endWeight == null) {
    return `${seg.startWeight}以上`
  }
  return `${seg.startWeight}-${seg.endWeight}kg`
}

function formatMethod(seg: WeightSegment): string {
  const opt = roundingMethodOptions.find((o: { value: number; label: string }) => o.value === seg.roundingMethod)
  return opt?.label ?? '未设置'
}

function formatRounding(seg: WeightSegment): string {
  const opt = roundingMethodOptions.find((o: { value: number; label: string }) => o.value === seg.roundingMethod)
  const baseLabel = opt?.label ?? '未设置'
  if ((seg.roundingMethod === 4 || seg.roundingMethod === 6 || seg.roundingMethod === 7)
    && (seg.truncParam != null || seg.ceilParam != null)) {
    const parts: string[] = []
    if (seg.truncParam != null) parts.push(`舍${seg.truncParam}`)
    if (seg.ceilParam != null) parts.push(`进${seg.ceilParam}`)
    return `${baseLabel}(${parts.join('/')})`
  }
  return baseLabel
}

// ==================== 属性编辑 Modal（点击列头或卡片触发） ====================

const editingIndex = ref<number | null>(null)

interface EditForm {
  roundingMethod: number
  truncParam: number | undefined
  ceilParam: number | undefined
}

const editForm = reactive<EditForm>({
  roundingMethod: 1,
  truncParam: undefined,
  ceilParam: undefined,
})

/** 外部调用入口：打开指定段的属性编辑 Modal */
function openPropertiesDialog(index: number) {
  const seg = props.segments[index]
  if (!seg) return
  editForm.roundingMethod = seg.roundingMethod
  editForm.truncParam = seg.truncParam ?? undefined
  editForm.ceilParam = seg.ceilParam ?? undefined
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

// ==================== 标尺布局 ====================

const rulerEl = ref<HTMLElement | null>(null)

// 分段密度刻度：前 3kg 占标尺 50% 宽度，3kg 以上压缩占另 50%（仅 standalone 模式生效）
const DENSITY_THRESHOLD_KG = 3
const DENSITY_LEFT_RATIO = 0.5

/** aligned 模式：当传入 columnWidths 且与段数一致时启用，按列宽线性分配，禁用密度映射 */
const isAlignedMode = computed<boolean>(() => {
  return !!(
    props.columnWidths &&
    props.columnWidths.length === props.segments.length &&
    props.columnWidths.length > 0
  )
})

/**
 * 密度映射：重量 → 标尺占比（[0, 1]）的非线性映射
 * - [0, 3kg] 线性平铺到 [0, 0.5]
 * - [3kg, maxWeight] 线性平铺到 [0.5, 1]
 * - 如 maxWeight ≤ 3kg，全范围线性映射到 [0, 1]
 */
function weightToRatioDensity(w: number, maxWeight: number): number {
  if (maxWeight <= 0) return 0
  if (maxWeight <= DENSITY_THRESHOLD_KG) {
    return Math.max(0, Math.min(1, w / maxWeight))
  }
  if (w <= DENSITY_THRESHOLD_KG) {
    return (w / DENSITY_THRESHOLD_KG) * DENSITY_LEFT_RATIO
  }
  const denom = maxWeight - DENSITY_THRESHOLD_KG
  return DENSITY_LEFT_RATIO + ((w - DENSITY_THRESHOLD_KG) / denom) * (1 - DENSITY_LEFT_RATIO)
}

/**
 * 统一的重量 → 标尺占比映射：根据当前模式分发
 * - aligned 模式：在 displaySegments 中段内线性插值
 * - standalone 模式：使用密度映射
 */
function weightToRatio(w: number, maxWeight: number): number {
  if (isAlignedMode.value) {
    const segs = displaySegments.value
    for (const seg of segs) {
      const segEnd = seg.isInfinity ? seg.startWeight + seg.span : (seg.endWeight as number)
      if (w >= seg.startWeight && w <= segEnd) {
        const denom = segEnd - seg.startWeight
        const inSegRatio = denom <= 0 ? 0 : (w - seg.startWeight) / denom
        return seg.ratioStart + inSegRatio * (seg.ratioEnd - seg.ratioStart)
      }
    }
    // 落在末段右侧或外侧
    return w <= segs[0].startWeight ? 0 : 1
  }
  return weightToRatioDensity(w, maxWeight)
}

interface DisplaySeg {
  key: string | number
  startWeight: number
  endWeight: number | null
  span: number
  flex: number
  isInfinity: boolean
  /** 本段在标尺上的起始占比（0~1） */
  ratioStart: number
  /** 本段在标尺上的结束占比（0~1） */
  ratioEnd: number
}

// 标尺上的虚拟最大重量（末段 ∞ 时取 startWeight + 虚拟跨度）
const rulerMaxWeight = computed<number>(() => {
  const segs = props.segments
  if (segs.length === 0) return DENSITY_THRESHOLD_KG * 2
  const finiteSpans = segs
    .filter(s => s.endWeight != null)
    .map(s => (s.endWeight as number) - s.startWeight)
  const maxFiniteSpan = finiteSpans.length ? Math.max(...finiteSpans) : 10
  const last = segs[segs.length - 1]
  if (last.endWeight != null) {
    return last.endWeight as number
  }
  return last.startWeight + Math.max(maxFiniteSpan * 1.2, 5)
})

const displaySegments = computed<DisplaySeg[]>(() => {
  const segs = props.segments
  if (segs.length === 0) return []
  const maxW = rulerMaxWeight.value

  // aligned 模式：按矩阵列宽分配 flex（每段宽度比例 = 列宽 / 总列宽）
  if (isAlignedMode.value) {
    const widths = props.columnWidths!
    const total = widths.reduce((s, w) => s + w, 0)
    if (total <= 0) return []
    let cumRatio = 0
    return segs.map((s, idx) => {
      const isInfinity = s.endWeight == null
      const start = s.startWeight
      const end = isInfinity ? maxW : (s.endWeight as number)
      const flex = widths[idx] / total
      const ratioStart = cumRatio
      const ratioEnd = idx === segs.length - 1 ? 1 : cumRatio + flex
      cumRatio = ratioEnd
      return {
        key: s.id ?? `new-${idx}`,
        startWeight: start,
        endWeight: s.endWeight ?? null,
        span: end - start,
        flex: Math.max(ratioEnd - ratioStart, 0.001),
        isInfinity,
        ratioStart,
        ratioEnd,
      }
    })
  }

  // standalone 模式：密度映射
  return segs.map((s, idx) => {
    const isInfinity = s.endWeight == null
    const start = s.startWeight
    const end = isInfinity ? maxW : (s.endWeight as number)
    const ratioStart = weightToRatioDensity(start, maxW)
    const ratioEnd = weightToRatioDensity(end, maxW)
    const flex = Math.max(ratioEnd - ratioStart, 0.001)
    return {
      key: s.id ?? `new-${idx}`,
      startWeight: start,
      endWeight: s.endWeight ?? null,
      span: end - start,
      flex,
      isInfinity,
      ratioStart,
      ratioEnd,
    }
  })
})

// ==================== 关键刻度节点标注 ====================

const KEY_TICKS = [0, 0.3, 0.5, 1, 2, 3, 4, 5, 10, 15, 30]

const keyTicksOnRuler = computed<{ value: number; leftPercent: number }[]>(() => {
  const segs = displaySegments.value
  if (segs.length === 0) return []
  const maxW = rulerMaxWeight.value

  const list: { value: number; leftPercent: number }[] = []
  for (let i = 0; i < segs.length; i++) {
    const startW = segs[i].startWeight
    const endW = segs[i].isInfinity ? maxW : (segs[i].endWeight as number)
    const isLast = i === segs.length - 1

    for (const tick of KEY_TICKS) {
      // 首段起点仅计一次；其余节点 startW < tick 且（末段右闭、其他右开）
      const include =
        (i === 0 && tick === startW) ||
        (tick > startW && (isLast ? tick <= endW : tick < endW))
      if (include) {
        const leftPercent = weightToRatio(tick, maxW) * 100
        list.push({ value: tick, leftPercent })
      }
    }
  }

  const seen = new Set<number>()
  return list
    .filter(t => {
      if (seen.has(t.value)) return false
      seen.add(t.value)
      return true
    })
    .sort((a, b) => a.value - b.value)
})

// 密度分界线（3kg 处）仅 standalone 模式 + maxWeight > 3kg 时显示
const showDensityDivider = computed<boolean>(() => {
  return !isAlignedMode.value && rulerMaxWeight.value > DENSITY_THRESHOLD_KG
})
const densityDividerLeftPercent = computed<number>(() => {
  return weightToRatioDensity(DENSITY_THRESHOLD_KG, rulerMaxWeight.value) * 100
})

// 数字标签碰撞避让：相邻 < 4% 时仅保留靠左那个的文字（刻度线仍保留）
const visibleLabelValues = computed<Set<number>>(() => {
  const visible = new Set<number>()
  let lastPercent = -100
  for (const t of keyTicksOnRuler.value) {
    if (t.leftPercent - lastPercent >= 4) {
      visible.add(t.value)
      lastPercent = t.leftPercent
    }
  }
  return visible
})

// ==================== 标尺拖拽 ====================

// 拖动时自动吸附的常用重量点（在像素阈值内贴合）
const SNAP_POINTS = [0.3, 0.5, 1, 1.5, 2, 3, 4, 5, 7, 10, 15, 20, 25, 30, 50]
const SNAP_PX_THRESHOLD = 8 // 距离吸附点 ≤ 8px 时贴合

const dragging = ref(false)
const dragPreview = reactive({
  visible: false,
  leftPx: 0,
  weight: 0,
  segIdx: -1,
  snapped: false,
  /** 游标模式：hover=悬停预览（灰色）；drag=拖拽中（红/绿） */
  mode: 'hover' as 'hover' | 'drag',
})

function getRulerPosFromEvent(e: MouseEvent): { weight: number; segIdx: number; xPx: number; snapped: boolean } | null {
  if (!rulerEl.value) return null
  const rect = rulerEl.value.getBoundingClientRect()
  const xPx = Math.max(0, Math.min(e.clientX - rect.left, rect.width))
  const segs = displaySegments.value
  if (segs.length === 0) return null
  const totalFlex = segs.reduce((s, x) => s + x.flex, 0)
  if (totalFlex <= 0) return null

  let cumPx = 0
  for (let i = 0; i < segs.length; i++) {
    const segPxWidth = (segs[i].flex / totalFlex) * rect.width
    if (xPx >= cumPx && xPx <= cumPx + segPxWidth + 0.5) {
      const ratio = segPxWidth > 0 ? (xPx - cumPx) / segPxWidth : 0
      const segOriginal = props.segments[i]
      const segEnd = segOriginal.endWeight ?? (segOriginal.startWeight + segs[i].span)
      const rawWeight = segOriginal.startWeight + Math.max(0, Math.min(1, ratio)) * (segEnd - segOriginal.startWeight)

      // 吸附阈值按“本段在标尺上的 px/kg”计算 — 密度处理使不同段阈值不同
      const segmentKgPerPx = segPxWidth > 0
        ? (segEnd - segOriginal.startWeight) / segPxWidth
        : 0
      const snapKgThreshold = SNAP_PX_THRESHOLD * segmentKgPerPx

      // 最近吸附点（必须严格在当前段内部）
      let snappedWeight: number | null = null
      let minDelta = Infinity
      for (const sp of SNAP_POINTS) {
        if (sp <= segOriginal.startWeight) continue
        if (segOriginal.endWeight != null && sp >= segOriginal.endWeight) continue
        const delta = Math.abs(rawWeight - sp)
        if (delta < snapKgThreshold && delta < minDelta) {
          snappedWeight = sp
          minDelta = delta
        }
      }

      let finalWeight = snappedWeight ?? Math.round(rawWeight * 100) / 100
      // 只保留两位小数
      finalWeight = Math.round(finalWeight * 100) / 100

      // 吸附后重算游标像素位置，让线也贴合到吸附点
      let finalXPx = xPx
      if (snappedWeight != null) {
        const inSegRatio = (segEnd - segOriginal.startWeight) === 0
          ? 0
          : (snappedWeight - segOriginal.startWeight) / (segEnd - segOriginal.startWeight)
        finalXPx = cumPx + inSegRatio * segPxWidth
      }

      return { weight: finalWeight, segIdx: i, xPx: finalXPx, snapped: snappedWeight != null }
    }
    cumPx += segPxWidth
  }
  return null
}

function updateDragPreview(pos: { weight: number; segIdx: number; xPx: number; snapped: boolean }, mode: 'hover' | 'drag' = 'drag') {
  dragPreview.visible = true
  dragPreview.leftPx = pos.xPx
  dragPreview.weight = pos.weight
  dragPreview.segIdx = pos.segIdx
  dragPreview.snapped = pos.snapped
  dragPreview.mode = mode
}

function onRulerMouseDown(e: MouseEvent) {
  if (e.button !== 0) return
  const pos = getRulerPosFromEvent(e)
  if (!pos) return
  const seg = props.segments[pos.segIdx]
  // 紧贴段边界时不触发拖拽（避免与相邻段歧义）
  if (pos.weight <= seg.startWeight + 0.05) return
  if (seg.endWeight != null && pos.weight >= seg.endWeight - 0.05) return
  dragging.value = true
  updateDragPreview(pos, 'drag')
  window.addEventListener('mousemove', onWindowMouseMove)
  window.addEventListener('mouseup', onWindowMouseUp)
  e.preventDefault()
}

function onRulerMouseMove(e: MouseEvent) {
  if (dragging.value) return // 由全局 listener 接管
  const pos = getRulerPosFromEvent(e)
  if (pos) updateDragPreview(pos, 'hover')
}

function onRulerMouseLeave() {
  if (!dragging.value) {
    dragPreview.visible = false
  }
}

function onWindowMouseMove(e: MouseEvent) {
  if (!dragging.value) return
  const pos = getRulerPosFromEvent(e)
  if (pos) updateDragPreview(pos, 'drag')
}

function onWindowMouseUp(e: MouseEvent) {
  window.removeEventListener('mousemove', onWindowMouseMove)
  window.removeEventListener('mouseup', onWindowMouseUp)
  if (!dragging.value) return
  dragging.value = false
  dragPreview.visible = false
  const pos = getRulerPosFromEvent(e)
  if (!pos) return
  const seg = props.segments[pos.segIdx]
  if (pos.weight <= seg.startWeight + 0.01) return
  if (seg.endWeight != null && pos.weight >= seg.endWeight - 0.01) return
  openSplitDialog(pos.segIdx, pos.weight)
}

onBeforeUnmount(() => {
  window.removeEventListener('mousemove', onWindowMouseMove)
  window.removeEventListener('mouseup', onWindowMouseUp)
})

// ==================== 外部接口 ====================
// 暴露给父组件：从列头点击中打开属性编辑 Modal
defineExpose({ openPropertiesDialog, openSplitDialog })

// ==================== 分割确认弹窗 ====================

interface SplitDialogState {
  visible: boolean
  segIdx: number
  point: number | null
  // 左段（只读，回填用）
  leftFixedPrice: number | null
  // 右段（可输入）
  rightFixedPrice: number | null
}

const splitDialog = reactive<SplitDialogState>({
  visible: false,
  segIdx: -1,
  point: null,
  leftFixedPrice: null,
  rightFixedPrice: null,
})

const splitTargetSeg = computed<WeightSegment | null>(() => {
  if (splitDialog.segIdx < 0) return null
  return props.segments[splitDialog.segIdx] ?? null
})

// 弹窗快捷选项：仅列出落入当前段内部的常用重量点
const splitDialogSnapOptions = computed<number[]>(() => {
  const seg = splitTargetSeg.value
  if (!seg) return []
  return SNAP_POINTS.filter(sp => {
    if (sp <= seg.startWeight) return false
    if (seg.endWeight != null && sp >= seg.endWeight) return false
    return true
  })
})

const splitPointError = computed(() => {
  const seg = splitTargetSeg.value
  if (!seg) return ''
  const p = splitDialog.point
  if (p == null) return '请输入分割点'
  if (p <= seg.startWeight) return `分割点必须大于 ${seg.startWeight}kg`
  if (seg.endWeight != null && p >= seg.endWeight) return `分割点必须小于 ${seg.endWeight}kg`
  return ''
})

const splitPointValid = computed(() => splitPointError.value === '')

const splitOkText = computed(() => {
  if (splitDialog.point != null && splitPointValid.value) {
    return `确认分割于 ${splitDialog.point.toFixed(2)}kg`
  }
  return '确认分割'
})

function openSplitDialog(segIdx: number, point: number) {
  const seg = props.segments[segIdx]
  if (!seg) return
  splitDialog.segIdx = segIdx
  splitDialog.point = Math.round(point * 100) / 100
  splitDialog.rightFixedPrice = null

  // 自动读取当前段代表价格回填左段（只读展示）
  let leftFixed: number | null = null
  if (props.matrix) {
    for (const row of props.matrix) {
      const cell = row.prices[seg.sortOrder]
      if (!cell) continue
      if (cell.basePrice != null) {
        leftFixed = cell.basePrice
        break
      }
    }
  }
  splitDialog.leftFixedPrice = leftFixed
  splitDialog.visible = true
}

function confirmSplit() {
  const seg = splitTargetSeg.value
  if (!seg) return
  if (!splitPointValid.value || splitDialog.point == null) {
    message.warning(splitPointError.value || '分割点不合法')
    return
  }
  const point = splitDialog.point
  const segIdx = splitDialog.segIdx

  emit('split', segIdx, point)

  // 右段快捷定价：fillSegmentPrice 约定传数组下标（与 WeightSegmentPanel 一致），分割后右段位于 segIdx + 1
  if (splitDialog.rightFixedPrice != null) {
    emit('fillSegmentPrice', segIdx + 1, { basePrice: splitDialog.rightFixedPrice })
  }

  splitDialog.visible = false
}
</script>

<style scoped lang="scss">
.weight-segment-panel {
  display: flex;
  align-items: flex-start;
  gap: 12px;

  // ruler-only 嵌入模式：取消双栏布局，让标尺独占宽度
  &.mode-ruler-only {
    display: block;
    gap: 0;
  }
}

.segment-label {
  flex-shrink: 0;
  line-height: 32px;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
  white-space: nowrap;
}

.segment-content {
  flex: 1;
  min-width: 0;
}

// ===== 标尺 =====

.ruler-wrapper {
  margin-bottom: 0;

  // sticky-bottom 模式：贴紧下方组件（如矩阵列头）零间距
  &.sticky-bottom {
    margin-bottom: 0;
  }

  // embedded （ruler-only）模式：嵌入矩阵头部，极致紧凑布局
  &.embedded {
    padding: 0;
    background: transparent;

    .ruler-hint {
      margin-bottom: 2px;
      font-size: 10px;
      line-height: 1.2;
    }

    .ruler {
      height: 14px;
      border-radius: 2px;
    }

    .ruler-mark-line {
      height: 2px;
    }

    // embedded 模式下隐藏刷度数字标签，避免错位、减少赘余
    .ruler-mark-label {
      display: none;
    }
  }
}

.ruler-positioner {
  // 标尺定位器：允许从父接收 padding-left 以避让矩阵的固定列
  width: 100%;
  box-sizing: border-box;
}

.ruler-hint {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  color: rgba(0, 0, 0, 0.45);
  margin-bottom: 4px;

  :deep(.anticon) {
    color: var(--color-info);
  }
}

.ruler {
  position: relative;
  display: flex;
  height: 24px;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  cursor: ew-resize;
  user-select: none;
  overflow: hidden;
}

.ruler-seg {
  position: relative;
  border-right: 2px solid var(--color-primary);
  background: var(--bg-muted);
  min-width: 0;

  &:last-child {
    border-right: none;
  }

  &.infinity {
    background: var(--bg-muted);
  }

  &:hover {
    background: var(--color-primary-light);
  }

  &.infinity:hover {
    background: var(--color-primary-light);
  }
}

.ruler-marks {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  pointer-events: none;
  z-index: 1;
}

// 密度分界线（背景层，低于刻度与游标）
.ruler-density-divider {
  position: absolute;
  top: 0;
  bottom: 0;
  pointer-events: auto;
  z-index: 0;
  transform: translateX(-50%);
  width: 8px;
  display: flex;
  justify-content: center;
  cursor: help;
}

.ruler-density-divider-line {
  width: 1px;
  height: 100%;
  border-left: 1px dashed var(--color-primary-border);
  pointer-events: none;
}

.ruler-density-divider:hover .ruler-density-divider-line {
  border-left-color: var(--color-primary);
}

.ruler-mark {
  position: absolute;
  top: 0;
  bottom: 0;
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;

  // 首末刷度修正：避免 label 被 .ruler 的 overflow: hidden 裁掉半边
  // 第一个刷度 (left: 0%) 向右对齐，最后一个 (left: 100%) 向左对齐
  &:first-child {
    transform: translateX(0);
    align-items: flex-start;
  }

  &:last-child {
    transform: translateX(-100%);
    align-items: flex-end;
  }
}

.ruler-mark-line {
  width: 1px;
  height: 5px;
  background: rgba(0, 0, 0, 0.45);
  flex-shrink: 0;
}

.ruler-mark-label {
  font-size: 10px;
  color: rgba(0, 0, 0, 0.6);
  margin-top: 1px;
  line-height: 1;
  white-space: nowrap;
  user-select: none;
}

.ruler-infinity {
  position: absolute;
  bottom: 1px;
  right: 4px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.6);
  pointer-events: none;
  font-weight: 500;
  z-index: 1;
}

.ruler-cursor {
  position: absolute;
  top: 0;
  bottom: 0;
  pointer-events: none;
  z-index: 3;
}

.ruler-cursor-line {
  position: absolute;
  top: 0;
  bottom: 0;
  left: -1px;
  width: 2px;
  background: var(--color-danger);
  box-shadow: 0 0 4px rgba(255, 77, 79, 0.5);
  transition: background-color 0.12s, box-shadow 0.12s;
}

.ruler-cursor-bubble {
  position: absolute;
  top: -22px;
  left: 50%;
  transform: translateX(-50%);
  background: var(--color-danger);
  color: var(--text-on-accent);
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 3px;
  white-space: nowrap;
  font-weight: 500;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.15);
  display: flex;
  align-items: center;
  gap: 3px;
  transition: background-color 0.12s;

  &::after {
    content: '';
    position: absolute;
    top: 100%;
    left: 50%;
    transform: translateX(-50%);
    border: 4px solid transparent;
    border-top-color: var(--color-danger);
    transition: border-top-color 0.12s;
  }
}

.snap-icon {
  font-size: 11px;
  line-height: 1;
}

// 吸附态：游标变绿色，提示用户已贴合到常用值
.ruler-cursor.snapped {
  .ruler-cursor-line {
    background: var(--color-success);
    box-shadow: 0 0 6px rgba(82, 196, 26, 0.6);
  }
  .ruler-cursor-bubble {
    background: var(--color-success);
    &::after {
      border-top-color: var(--color-success);
    }
  }
}

// hover 态：轻量灰色提示（未按下鼠标）
.ruler-cursor.hovering {
  .ruler-cursor-line {
    width: 1px;
    left: 0;
    background: rgba(0, 0, 0, 0.45);
    box-shadow: none;
    border-left: 1px dashed rgba(0, 0, 0, 0.45);
    background: transparent; // 表现为虚线
  }
  .ruler-cursor-bubble {
    background: rgba(0, 0, 0, 0.75);
    color: var(--text-on-accent);
    font-weight: 400;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.18);
    &::after {
      border-top-color: rgba(0, 0, 0, 0.75);
    }
  }
}

// ===== 重量段卡片 =====

.segment-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;

  &.with-margin {
    margin-bottom: 10px;
  }
}

.segment-card {
  position: relative;
  display: inline-flex;
  flex-direction: row;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border: 1px solid var(--border);
  border-radius: 4px;
  background: var(--bg-muted);
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
  font-size: 12px;
  line-height: 1.4;

  &:hover {
    border-color: var(--color-primary);
    background: var(--bg-muted);

    .segment-edit-hint {
      opacity: 1;
    }
  }

  &.active {
    border-color: var(--color-primary);
    background: var(--color-primary-light);
  }
}

.segment-range {
  font-size: 13px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
  white-space: nowrap;
}

.segment-sep {
  color: rgba(0, 0, 0, 0.25);
  font-size: 11px;
}

.segment-method-text,
.segment-rounding-text {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.55);
  white-space: nowrap;
}

.segment-edit-hint {
  font-size: 11px;
  color: var(--color-primary);
  opacity: 0;
  transition: opacity 0.15s;
  margin-left: 2px;
}

.segment-close {
  position: absolute;
  top: -5px;
  right: -5px;
  width: 14px;
  height: 14px;
  line-height: 12px;
  text-align: center;
  font-size: 11px;
  color: var(--text-on-accent);
  background: rgba(255, 77, 79, 0.55);
  border-radius: 50%;
  cursor: pointer;
  display: none;
  z-index: 1;
  transition: all 0.15s;

  .segment-card:hover & {
    display: block;
  }

  &:hover {
    background: var(--color-danger);
    transform: scale(1.1);
  }
}

// ===== 分割确认弹窗 =====

.dialog-readonly {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.75);
}

.split-row {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
}

.split-tag {
  flex-shrink: 0;
  font-size: 12px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 3px;
  white-space: nowrap;

  &.left {
    background: var(--color-primary-light);
    color: var(--color-info);
    border: 1px solid var(--color-primary-border);
  }

  &.right {
    background: var(--color-success-light);
    color: var(--color-success);
    border: 1px solid var(--color-success);
  }
}

.split-readonly {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.65);
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 2px 8px;
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.split-tip {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--color-info);
  background: var(--color-primary-light);
  border: 1px solid var(--color-primary-border);
  border-radius: 3px;
  padding: 1px 5px;
  white-space: nowrap;
}

// 弹窗常用值快捷选项
.snap-shortcuts {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  margin-top: 6px;
}

.snap-shortcuts-label {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.snap-tag {
  cursor: pointer;
  user-select: none;
  margin-inline-end: 0;
  transition: all 0.15s;

  &:hover {
    color: var(--color-primary);
    border-color: var(--color-primary);
  }
}
</style>
