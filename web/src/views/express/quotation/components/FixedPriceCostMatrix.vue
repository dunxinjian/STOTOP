<template>
  <div class="fixed-price-cost-matrix-wrapper">
    <!-- 精简工具栏：按区域填充 + 按列填充（含调价） + 清空 -->
    <div class="compact-toolbar">
      <!-- 按区域填充 -->
      <div class="fill-group">
        <span class="group-label">按区域:</span>
        <a-select
          v-model:value="regionFill.region"
          placeholder="选区域"
          style="width: 110px"
          allow-clear
        >
          <a-select-option v-for="r in regionOptions" :key="r.value" :value="r.value">{{ r.label }}</a-select-option>
        </a-select>
        <a-select
          v-model:value="regionFill.segmentIndex"
          placeholder="选重量段"
          style="width: 140px"
          allow-clear
          @change="regionFill.inputValue = ''"
        >
          <a-select-option v-for="(seg, idx) in segments" :key="seg.sortOrder" :value="idx">
            {{ formatSegLabel(seg) }}
          </a-select-option>
        </a-select>
        <a-input
          v-model:value="regionFill.inputValue"
          style="width: 120px"
          :placeholder="getInputPlaceholder(regionFill.segmentIndex, 'fill')"
          @pressEnter="handleRegionFill"
        />
        <a-button type="primary" @click="handleRegionFill">填充</a-button>
        <!-- 区域调价 Popover -->
        <a-popover v-model:open="regionAdjustOpen" trigger="click" placement="bottomLeft">
          <template #content>
            <div class="adjust-popover">
              <a-select
                v-model:value="regionAdjust.target"
                size="small"
                style="width: 90px"
                :disabled="regionFill.segmentIndex == null"
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
                style="width: 110px"
                placeholder="+/-调整值"
                @pressEnter="handleRegionAdjust"
              />
              <a-button type="primary" size="small" @click="handleRegionAdjust">调价</a-button>
            </div>
          </template>
          <a-button>调价▾</a-button>
        </a-popover>
      </div>

      <a-divider type="vertical" style="height: 24px; margin: 0 4px" />

      <!-- 按列填充 -->
      <div class="fill-group">
        <span class="group-label">按列:</span>
        <a-select
          v-model:value="columnFill.segmentIndex"
          placeholder="选重量段"
          style="width: 140px"
          allow-clear
          @change="columnFill.inputValue = ''"
        >
          <a-select-option v-for="(seg, idx) in segments" :key="seg.sortOrder" :value="idx">
            {{ formatSegLabel(seg) }}
          </a-select-option>
        </a-select>
        <a-input
          v-model:value="columnFill.inputValue"
          style="width: 120px"
          :placeholder="getInputPlaceholder(columnFill.segmentIndex, 'fill')"
          @pressEnter="handleColumnFill"
        />
        <a-button type="primary" @click="handleColumnFill">填充</a-button>
        <!-- 列调价 Popover -->
        <a-popover v-model:open="columnAdjustOpen" trigger="click" placement="bottomLeft">
          <template #content>
            <div class="adjust-popover">
              <a-select
                v-model:value="columnAdjust.target"
                size="small"
                style="width: 90px"
                :disabled="columnFill.segmentIndex == null"
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
                style="width: 110px"
                placeholder="+/-调整值"
                @pressEnter="handleColumnAdjust"
              />
              <a-button type="primary" size="small" @click="handleColumnAdjust">调价</a-button>
            </div>
          </template>
          <a-button>调价▾</a-button>
        </a-popover>
      </div>

      <!-- 右侧清空 -->
      <div style="margin-left: auto">
        <a-button @click="pasteModalVisible = true">粘贴填充</a-button>
        <a-popconfirm title="确定清空所有价格数据？" ok-text="确定" cancel-text="取消" @confirm="emit('clearAll')">
          <a-button danger>清空全部</a-button>
        </a-popconfirm>
      </div>
    </div>

    <!-- 主体布局：左侧（标尺 + 价格矩阵）+ 右侧（aside slot：由父组件注入内容，如关联店铺） -->
    <div class="matrix-with-tips" ref="matrixWithTipsRef">
      <div class="matrix-left" :style="{ width: scrollX + 'px', flex: '0 0 ' + scrollX + 'px' }">
        <!-- 工具栏与表格之间的嵌入区（如标尺），使其能与列头垂直对齐、零间距贴合 -->
        <!-- 不限定 slot 宽度，让 hint 文本可自由撜满容器；标尺本身由 ruler-positioner 内部限定 width -->
        <div
          v-if="$slots.beforeTable"
          class="before-table-slot"
        >
          <slot name="beforeTable" />
        </div>
    
        <!-- 价格矩阵表格：maxWidth 限定不被容器撞大，超出则水平滚动 -->
        <!-- sticky: 垂直滚动时，列头吸附在滚动容器顶部，使列头以上保持冻结 -->
        <a-table
          class="price-matrix-table"
          :style="{ maxWidth: scrollX + 'px' }"
          :columns="tableColumns"
          :data-source="matrix"
          :pagination="false"
          :scroll="{ x: scrollX }"
          :sticky="stickyConfig"
          row-key="provinceId"
          bordered
          size="small"
          :row-class-name="getRowClassName"
          :custom-row="() => ({})"
        >
          <template #headerCell="{ column }">
            <template v-if="(column as any).dataIndex?.startsWith('seg_')">
              <div class="seg-header-with-actions">
                <!-- 顶部一行：拆分按钮，居中独占，不与标题重叠 -->
                <div class="seg-header-top-row">
                  <a-tooltip title="从该重量段拆分为两段" placement="bottom">
                    <span
                      class="seg-header-split-btn"
                      @click.stop="emit('split', (column as any)._segIdx)"
                    >
                      <CaretDownFilled />
                    </span>
                  </a-tooltip>
                </div>
                <!-- 主行：左箭头 · 标题+进位 · 右箭头 -->
                <div class="seg-header-main-row">
                  <a-tooltip
                    v-if="segments.length > 1 && (column as any)._segArrayIdx > 0"
                    title="与左侧段合并（保留左段价格）"
                    placement="bottom"
                  >
                    <span
                      class="seg-header-merge-arrow merge-left"
                      @click.stop="emit('mergeLeft', (column as any)._segArrayIdx)"
                    >
                      <CaretLeftOutlined />
                    </span>
                  </a-tooltip>
                  <span v-else class="seg-header-merge-placeholder"></span>
                  <a-tooltip title="点击编辑该重量段的计价方式与进位方式" placement="bottom">
                    <div
                      class="seg-header-clickable"
                      @click="emit('headerClick', (column as any)._segIdx)"
                    >
                      <div class="seg-header-title">{{ (column as any)._titleMain }}</div>
                      <div
                        v-if="(column as any)._titleSub"
                        class="seg-header-subtitle"
                      >{{ (column as any)._titleSub }}</div>
                      <div
                        v-if="(column as any)._roundingLabel"
                        class="seg-header-rounding"
                      >{{ (column as any)._roundingLabel }}</div>
                      <span class="seg-header-edit-icon">✏</span>
                    </div>
                  </a-tooltip>
                  <a-tooltip
                    v-if="segments.length > 1 && (column as any)._segArrayIdx < segments.length - 1"
                    title="与右侧段合并（保留右段价格）"
                    placement="bottom"
                  >
                    <span
                      class="seg-header-merge-arrow merge-right"
                      @click.stop="emit('mergeRight', (column as any)._segArrayIdx)"
                    >
                      <CaretRightOutlined />
                    </span>
                  </a-tooltip>
                  <span v-else class="seg-header-merge-placeholder"></span>
                </div>
              </div>
            </template>
            <template v-else>
              {{ (column as any).title }}
            </template>
          </template>
          <template #bodyCell="{ column, record, index }">
            <!-- 区域列 -->
            <template v-if="column.dataIndex === 'region'">
              <span class="region-text">{{ record.region }}</span>
            </template>
            <!-- 省份列 -->
            <template v-else-if="column.dataIndex === 'provinceName'">
              <span>{{ record.provinceName }}</span>
            </template>
            <!-- 价格单元格 -->
            <template v-else-if="(column as any).dataIndex?.startsWith('seg_')">
              <PriceCellInput
                :value="getCellForRender(record as any, (column as any)._segIdx)"
                :editing="isEditing((record as any).provinceId, (column as any)._segIdx)"
                :has-override="hasCellOverrideForRender(record as any, (column as any)._segIdx)"
                @update:value="(cell) => handleCellUpdate((record as any).provinceId, (column as any)._segIdx, cell)"
                @start-edit="startEdit((record as any).provinceId, (column as any)._segIdx)"
                @end-edit="endEdit()"
                @tab-next="moveNext((record as any).provinceId, (column as any)._segIdx)"
                @openOverride="openOverride((record as any).provinceId, (column as any)._segIdx)"
              />
            </template>
          </template>
        </a-table>
      </div>
    
      <!-- 右侧 aside：由父组件通过 slot 注入（如编辑模式下的关联店铺面板）；新建模式下父组件不传内容则不渲染 -->
      <aside v-if="$slots.aside" class="matrix-aside">
        <slot name="aside" />
      </aside>
    </div>

    <!-- 单元格覆盖设置弹窗 -->
    <CellOverrideDialog
      v-model:visible="overrideDialogVisible"
      :data="overrideDialogData"
      @save="handleOverrideSave"
    />

    <!-- 粘贴填充矩阵弹窗 -->
    <a-modal
      v-model:open="pasteModalVisible"
      title="粘贴填充矩阵"
      width="600px"
      ok-text="填充"
      cancel-text="取消"
      @ok="handlePasteFill"
      @cancel="pasteText = ''; pasteResult = null"
    >
      <p style="color: var(--text-2); font-size: 12px; margin-bottom: 8px">
        从 Excel 复制矩阵数据（含列头行），粘贴到下方文本框中。第一列为省份名，后续列按顺序对应当前矩阵的重量段。
      </p>
      <a-textarea
        v-model:value="pasteText"
        :rows="12"
        placeholder="在此粘贴矩阵文本..."
        style="font-family: monospace; font-size: 12px"
      />
      <div v-if="pasteResult" style="margin-top: 8px; font-size: 12px">
        <span style="color: var(--color-success)">✓ 匹配 {{ pasteResult.matched }} 个省份</span>
        <span v-if="pasteResult.unmatched.length > 0" style="color: var(--color-warning); margin-left: 12px">
          ⚠ 未匹配: {{ pasteResult.unmatched.join('、') }}
        </span>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  CaretDownFilled,
  CaretLeftOutlined,
  CaretRightOutlined,
} from '@ant-design/icons-vue'
import type { WeightSegment, PriceCell, ProvinceRow } from '../composables/usePriceMatrix'
import { hasCellOverride, parseCellValue, getRegionList, validateCellInput } from '@/views/express/quotation/composables/usePriceMatrix'
import {
  normalizePastedMatrixLines,
  parsePastedPriceCellValue,
  parsePastedSegmentHeader,
} from '@/views/express/shared/pasteMatrixParser'
import { roundingMethodOptions } from '@/views/express/quotation/composables/useQuotationForm'
import PriceCellInput from '@/views/express/quotation/components/PriceCellInput.vue'
import CellOverrideDialog from '@/views/express/quotation/components/CellOverrideDialog.vue'
import type { CellOverrideData } from '@/views/express/quotation/components/CellOverrideDialog.vue'

const props = defineProps<{
  segments: WeightSegment[]
  matrix: ProvinceRow[]
}>()

const emit = defineEmits<{
  cellChange: [provinceId: number, segmentIndex: number, cell: PriceCell]
  clearAll: []
  headerClick: [segmentIndex: number]
  mergeLeft: [segmentIndex: number]
  mergeRight: [segmentIndex: number]
  split: [segmentSortOrder: number]
  pasteSegments: [segments: WeightSegment[]]
}>()

// ==================== 编辑态追踪 ====================

const editingCell = ref<{ provinceId: number; segmentIndex: number } | null>(null)

function isEditing(provinceId: number, segmentIndex: number): boolean {
  if (!editingCell.value) return false
  return editingCell.value.provinceId === provinceId && editingCell.value.segmentIndex === segmentIndex
}

function startEdit(provinceId: number, segmentIndex: number) {
  editingCell.value = { provinceId, segmentIndex }
}

function endEdit() {
  editingCell.value = null
}

// ==================== 单元格操作 ====================

function getCellForRender(row: ProvinceRow, segmentIndex: number): PriceCell {
  return row.prices[segmentIndex] ?? {
    segmentIndex,
    provinceId: row.provinceId,
    basePrice: null,
    continuePrice: null,
    roundingMethodOverride: null,
    truncParamOverride: null,
    ceilParamOverride: null,
  }
}

function hasCellOverrideForRender(row: ProvinceRow, segmentIndex: number): boolean {
  const cell = row.prices[segmentIndex]
  return cell ? hasCellOverride(cell) : false
}

function getSegmentMethod(segmentIndex: number): number {
  // 统一返回 1（固定单价），不再区分 pricingMethod
  return 1
}

function handleCellUpdate(provinceId: number, segmentIndex: number, cell: PriceCell) {
  emit('cellChange', provinceId, segmentIndex, cell)
}

// ==================== 键盘导航 ====================

function moveNext(provinceId: number, segmentIndex: number) {
  const rowIndex = props.matrix.findIndex(r => r.provinceId === provinceId)
  if (rowIndex === -1) return

  const segIndices = props.segments.map(s => s.sortOrder)
  const colPos = segIndices.indexOf(segmentIndex)

  if (colPos < segIndices.length - 1) {
    editingCell.value = { provinceId, segmentIndex: segIndices[colPos + 1] }
  } else if (rowIndex < props.matrix.length - 1) {
    editingCell.value = {
      provinceId: props.matrix[rowIndex + 1].provinceId,
      segmentIndex: segIndices[0],
    }
  } else {
    editingCell.value = null
  }
}

// ==================== 区域 rowSpan 计算 ====================

const regionSpanMap = computed(() => {
  const map = new Map<number, number>()
  let i = 0
  while (i < props.matrix.length) {
    const region = props.matrix[i].region
    let count = 0
    for (let j = i; j < props.matrix.length && props.matrix[j].region === region; j++) {
      count++
    }
    map.set(props.matrix[i].provinceId, count)
    for (let j = i + 1; j < i + count; j++) {
      map.set(props.matrix[j].provinceId, 0)
    }
    i += count
  }
  return map
})

// 区域交替序号映射（用于行背景色交替）
const regionIndexMap = computed(() => {
  const map = new Map<number, number>()
  let regionIdx = 0
  let prevRegion = ''
  for (const row of props.matrix) {
    if (row.region !== prevRegion) {
      if (prevRegion) regionIdx++
      prevRegion = row.region
    }
    map.set(row.provinceId, regionIdx)
  }
  return map
})

function getRowClassName(record: ProvinceRow) {
  const idx = regionIndexMap.value.get(record.provinceId) ?? 0
  return idx % 2 === 0 ? 'region-even' : 'region-odd'
}

// 粗略估算文本渲染宽度（px）：中文 13px / 数字 8px / 字母 7px / 符号 6px
function estimateTextWidth(text: string): number {
  if (!text) return 0
  let w = 0
  for (const ch of text) {
    const code = ch.charCodeAt(0)
    if (code > 0x7f) w += 13
    else if (/\d/.test(ch)) w += 8
    else if (/[a-zA-Z]/.test(ch)) w += 7
    else w += 6
  }
  return Math.ceil(w)
}

// ==================== 动态列 ====================

const tableColumns = computed(() => {
  const fixed: any[] = [
    {
      title: '区域',
      dataIndex: 'region',
      width: 60,
      fixed: 'left',
      align: 'center',
      customCell: (record: ProvinceRow) => {
        const span = regionSpanMap.value.get(record.provinceId) ?? 1
        return {
          rowSpan: span,
          class: span > 0 ? 'region-cell' : undefined,
        }
      },
    },
    {
      title: '省份',
      dataIndex: 'provinceName',
      width: 80,
      fixed: 'left',
      align: 'center',
    },
  ]

  const segCols = props.segments.map((seg, index) => {
    // 主标题：0-0.5kg / 5kg以上
    const titleMain = seg.endWeight != null
      ? `${seg.startWeight}-${seg.endWeight}kg`
      : `${seg.startWeight}kg以上`
    // 副标题已移除，进位方式独立显示
    const roundingLabel = roundingMethodOptions.find(r => r.value === seg.roundingMethod)?.label ?? ''
    const titleSub = roundingLabel
    const roundingLabelForCol = roundingLabel

    // 列宽动态计算：取三行中最宽者 + 左右合并箭头及内边距
    const contentWidth = Math.max(
      estimateTextWidth(titleMain),
      estimateTextWidth(roundingLabel),
    )
    // 左 16 + gap 2 + gap 2 + 右 16 = 36；两侧内边距 16
    const minWidth = 80
    const dynamicWidth = Math.max(minWidth, contentWidth + 36 + 16)

    return {
      title: titleSub ? `${titleMain} ${titleSub}` : titleMain, // a-table title 备用（被headerCell接管）
      dataIndex: `seg_${seg.sortOrder}`,
      width: dynamicWidth,
      align: 'center' as const,
      _segIdx: seg.sortOrder,        // sortOrder，用于单元格/列头点击/错误检查等与业务逻辑联动
      _segArrayIdx: index,            // 数组下标（0-based），用于邻段合并箭头判断
      _titleMain: titleMain,          // 主标题（单行）
      _titleSub: '',                   // 副标题已合并到 roundingLabel
      _roundingLabel: roundingLabelForCol,  // 进位方式文案
    }
  })

  return [...fixed, ...segCols]
})

const scrollX = computed(() => {
  const segWidthSum = props.segments.reduce((sum, seg) => {
    const titleMain = seg.endWeight != null
      ? `${seg.startWeight}-${seg.endWeight}kg`
      : `${seg.startWeight}kg以上`
    const titleSub = '' // 不再显示首续信息
    const roundingLabel = roundingMethodOptions.find(r => r.value === seg.roundingMethod)?.label ?? ''
    const contentWidth = Math.max(
      estimateTextWidth(titleMain),
      estimateTextWidth(roundingLabel),
    )
    const minWidth = 80
    return sum + Math.max(minWidth, contentWidth + 36 + 16)
  }, 0)
  return 60 + 80 + segWidthSum
})

// ==================== 表头粘性吸顶（垂直滚动时冻结列头）与右侧 aside 宿主 ====================
// matrixWithTipsRef 同时作为（1）查找 sticky 表头的滚动祖先；（2）右侧 aside slot 的宿主容器
const matrixWithTipsRef = ref<HTMLDivElement | null>(null)
const stickyContainer = ref<HTMLElement | null>(null)

function findScrollParent(el: HTMLElement | null): HTMLElement | null {
  let cur: HTMLElement | null = el?.parentElement ?? null
  while (cur && cur !== document.body) {
    const style = getComputedStyle(cur)
    if (/(auto|scroll)/.test(style.overflowY)) return cur
    cur = cur.parentElement
  }
  return null
}

const stickyConfig = computed<any>(() => ({
  offsetHeader: 0,
  // getContainer 必须返回 HTMLElement，未找到时返回 document.body 作为退化
  getContainer: () => stickyContainer.value || document.body,
}))

onMounted(() => {
  if (matrixWithTipsRef.value) {
    // 解析 sticky 表头所依赖的滚动容器
    stickyContainer.value = findScrollParent(matrixWithTipsRef.value)
  }
})

// ==================== 精简工具栏状态 ====================

// 按区域填充
const regionFill = reactive({
  region: undefined as string | undefined,
  segmentIndex: undefined as number | undefined,
  inputValue: '',
})
// 按列填充
const columnFill = reactive({
  segmentIndex: undefined as number | undefined,
  inputValue: '',
})
// 区域调价 Popover
const regionAdjustOpen = ref(false)
const regionAdjust = reactive({
  target: 'all' as 'all' | 'first' | 'continue',
  delta: null as number | null,
})
// 列调价 Popover
const columnAdjustOpen = ref(false)
const columnAdjust = reactive({
  target: 'all' as 'all' | 'first' | 'continue',
  delta: null as number | null,
})

function formatSegLabel(seg: WeightSegment): string {
  return seg.endWeight != null ? `${seg.startWeight}-${seg.endWeight}kg` : `${seg.startWeight}kg以上`
}

function getInputPlaceholder(segmentIndex: number | undefined, _mode: 'fill' | 'adjust'): string {
  return '输入价格'
}

function handleRegionFill() {
  const { segmentIndex, inputValue, region } = regionFill
  if (segmentIndex == null) { message.warning('请选择重量段'); return }
  if (!region) { message.warning('请选择区域'); return }
  const seg = props.segments[segmentIndex]
  if (!seg) return
  const trimmed = inputValue.trim()
  if (!trimmed) { message.warning('请输入填充值'); return }
  const v = validateCellInput(trimmed)
  if (!v.valid) { message.warning(v.error || '输入格式不正确'); return }
  const parsed = parseCellValue(trimmed)
  const targetRows = props.matrix.filter(r => r.region === region)
  for (const row of targetRows) {
    emit('cellChange', row.provinceId, seg.sortOrder, { ...getCellForRender(row, seg.sortOrder), ...parsed })
  }
  message.success(`已填充 ${targetRows.length} 个省份`)
}

function handleColumnFill() {
  const { segmentIndex, inputValue } = columnFill
  if (segmentIndex == null) { message.warning('请选择重量段'); return }
  const seg = props.segments[segmentIndex]
  if (!seg) return
  const trimmed = inputValue.trim()
  if (!trimmed) { message.warning('请输入填充值'); return }
  const v = validateCellInput(trimmed)
  if (!v.valid) { message.warning(v.error || '输入格式不正确'); return }
  const parsed = parseCellValue(trimmed)
  for (const row of props.matrix) {
    emit('cellChange', row.provinceId, seg.sortOrder, { ...getCellForRender(row, seg.sortOrder), ...parsed })
  }
  message.success(`已填充 ${props.matrix.length} 个省份`)
}

function doAdjust(
  segmentIndex: number | undefined,
  delta: number | null,
  target: 'all' | 'first' | 'continue',
  region?: string,
) {
  if (segmentIndex == null) { message.warning('请先选择重量段'); return }
  if (delta == null || delta === 0) { message.warning('请输入调整值'); return }
  const seg = props.segments[segmentIndex]
  if (!seg) return
  const effectiveTarget: 'all' | 'first' | 'continue' = target
  const targetRows = region ? props.matrix.filter(r => r.region === region) : props.matrix
  for (const row of targetRows) {
    const cell = { ...getCellForRender(row, seg.sortOrder) }
    // A3' 统一：basePrice 必调；continuePrice 仅首续重存在时调整
    if ((effectiveTarget === 'all' || effectiveTarget === 'first') && cell.basePrice != null) {
      cell.basePrice = Math.max(0, cell.basePrice + delta)
    }
    if ((effectiveTarget === 'all' || effectiveTarget === 'continue') && cell.continuePrice != null) {
      cell.continuePrice = Math.max(0, cell.continuePrice + delta)
    }
    emit('cellChange', row.provinceId, seg.sortOrder, cell)
  }
  message.success('调价完成')
}

function handleRegionAdjust() {
  doAdjust(regionFill.segmentIndex, regionAdjust.delta, regionAdjust.target, regionFill.region)
  regionAdjustOpen.value = false
}

function handleColumnAdjust() {
  doAdjust(columnFill.segmentIndex, columnAdjust.delta, columnAdjust.target)
  columnAdjustOpen.value = false
}

const regionOptions = computed(() => {
  return getRegionList(props.matrix).map(r => ({ label: r, value: r }))
})

// ==================== 单元格覆盖设置 ====================

const overrideDialogVisible = ref(false)
const overrideDialogData = ref<CellOverrideData | null>(null)

function openOverride(provinceId: number, segmentIndex: number) {
  const row = props.matrix.find(r => r.provinceId === provinceId)
  if (!row) return
  const seg = props.segments.find(s => s.sortOrder === segmentIndex)
  const cell = row.prices[segmentIndex] ?? getCellForRender(row, segmentIndex)
  const segLabel = seg
    ? (seg.endWeight != null ? `${seg.startWeight}-${seg.endWeight}kg` : `${seg.startWeight}kg以上`)
    : String(segmentIndex)

  overrideDialogData.value = {
    provinceId,
    segmentIndex,
    provinceName: row.provinceName,
    segmentRange: segLabel,
    roundingMethodOverride: cell.roundingMethodOverride,
    truncParamOverride: cell.truncParamOverride,
    ceilParamOverride: cell.ceilParamOverride,
  }
  overrideDialogVisible.value = true
}

function handleOverrideSave(data: {
  provinceId: number
  segmentIndex: number
  roundingMethodOverride: number | null
  truncParamOverride: number | null
  ceilParamOverride: number | null
}) {
  const row = props.matrix.find(r => r.provinceId === data.provinceId)
  if (!row) return
  const existing = getCellForRender(row, data.segmentIndex)
  emit('cellChange', data.provinceId, data.segmentIndex, {
    ...existing,
    roundingMethodOverride: data.roundingMethodOverride,
    truncParamOverride: data.truncParamOverride,
    ceilParamOverride: data.ceilParamOverride,
  })
}

// ==================== 粘贴填充 ====================

const pasteModalVisible = ref(false)
const pasteText = ref('')
const pasteResult = ref<{ matched: number; unmatched: string[] } | null>(null)

function handlePasteFill() {
  const lines = normalizePastedMatrixLines(pasteText.value)
  if (lines.length === 0) {
    message.warning('请粘贴矩阵数据')
    return
  }

  if (lines.length < 2) {
    message.warning('数据至少需要列头行和一行数据')
    return
  }

  // 解析列头（第一行）— 确定列数和列顺序
  const headerCells = lines[0].split('\t')
  // 第一列是省份名标题，跳过；后续列对应重量段
  const colCount = headerCells.length - 1

  // 从列头解析重量段并通知父组件更新（sortOrder 全局约定 1 基，0 基会与后端归一化逻辑冲突产生重复段号）
  const parsedSegments: WeightSegment[] = []
  const failedHeaders: string[] = []
  for (let c = 1; c <= colCount; c++) {
    const parsed = parsePastedSegmentHeader(headerCells[c])
    if (parsed) {
      parsedSegments.push({
        sortOrder: c,
        startWeight: parsed.startWeight,
        endWeight: parsed.endWeight,
        roundingMethod: 1,
        truncParam: null,
        ceilParam: null,
      })
    } else {
      failedHeaders.push(headerCells[c]?.trim() || `第${c + 1}列`)
    }
  }

  // 列头部分解析失败时中止：跳过失败列会让后续列整体错位，价格落进错误的重量段
  if (failedHeaders.length > 0 && parsedSegments.length > 0) {
    message.error(`以下列头无法识别为重量段：${failedHeaders.join('、')}，请修正后重新粘贴`)
    return
  }

  // 仅当解析出的段范围与当前不一致时才重建重量段——
  // 重建会清空整个矩阵（含未粘贴省份的价格、单元格覆盖值与段进位方式），
  // 列头一致的"增量粘贴"必须保留现有数据
  const sameAsCurrent = parsedSegments.length > 0
    && parsedSegments.length === props.segments.length
    && parsedSegments.every((p, i) => {
      const cur = props.segments[i]
      return cur != null
        && cur.startWeight === p.startWeight
        && (cur.endWeight ?? null) === (p.endWeight ?? null)
    })
  const rebuildSegments = parsedSegments.length > 0 && !sameAsCurrent
  if (rebuildSegments) {
    emit('pasteSegments', parsedSegments)
  }

  // 目标段号：重建后为 1 基连续；保留现有段时按真实 sortOrder（拆分产生的段号可能非连续）
  const targetSortOrders = rebuildSegments
    ? parsedSegments.map(s => s.sortOrder)
    : props.segments.map(s => s.sortOrder)
  const segCount = Math.min(colCount, targetSortOrders.length)

  let matched = 0
  const unmatched: string[] = []

  // 逐行解析数据
  for (let i = 1; i < lines.length; i++) {
    const cells = lines[i].split('\t')
    if (cells.length < 2) continue

    const provinceName = cells[0].trim()
    // 模糊匹配省份：支持简称（如"黑龙"匹配"黑龙江"，"内蒙"匹配"内蒙古"）
    const row = props.matrix.find(r =>
      r.provinceName === provinceName ||
      r.provinceName.startsWith(provinceName) ||
      provinceName.startsWith(r.provinceName)
    )

    if (!row) {
      unmatched.push(provinceName)
      continue
    }

    matched++

    // 填充价格到各重量段
    for (let col = 0; col < segCount; col++) {
      const priceStr = cells[col + 1]?.trim()
      const segIdx = targetSortOrders[col]

      const existingCell = row.prices[segIdx]
      const newCell = parsePastedPriceCellValue(priceStr, segIdx, row.provinceId, existingCell)
      if (!newCell) continue

      emit('cellChange', row.provinceId, segIdx, newCell)
    }
  }

  pasteResult.value = { matched, unmatched }

  if (matched > 0) {
    message.success(`已填充 ${matched} 个省份的价格`)
    // 延迟关闭弹窗让用户看到结果
    setTimeout(() => {
      pasteModalVisible.value = false
      pasteText.value = ''
      pasteResult.value = null
    }, 1500)
  } else {
    message.error('未匹配到任何省份')
  }
}

// 暴露给父组件调用（用于 Ctrl+双击 打开弹窗）
defineExpose({ openOverride })
</script>

<style scoped lang="scss">
.fixed-price-cost-matrix-wrapper {
  width: 100%;
}

// 主体：左侧表格 + 右侧操作指引（中间留白，指引靠页面右侧）
.matrix-with-tips {
  display: flex;
  align-items: flex-start;
  width: 100%;
}

.matrix-left {
  // 宽度由行内 style 动态设为 scrollX，使价格矩阵严格与标尺对齐
  min-width: 0;
  // 面板感：背景、圆角、阴影，让价格矩阵成为页面主视觉焦点
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow:
    0 1px 2px rgba(0, 0, 0, 0.04),
    0 4px 12px rgba(0, 0, 0, 0.06);
  // 注意：不能使用 overflow: hidden，否则会形成新的 sticky 容器并裁剪粘性表头。
  // 圆角通过内部 .ant-table 子元素继承 + 列头/底行的圆角裁切策略保持视觉效果。
  border: 1px solid var(--border);
}

// 右侧 aside（包裹父组件注入的内容，如关联店铺；与矩阵卡片保持一致的面板视觉语言）
.matrix-aside {
  flex: 1 1 280px;
  min-width: 240px;
  max-width: 420px;
  margin-left: 12px;        // 与矩阵之间预留间隙
  align-self: stretch;      // 与矩阵同高，但内部可独立滚动
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
  box-shadow:
    0 1px 2px rgba(0, 0, 0, 0.04),
    0 4px 12px rgba(0, 0, 0, 0.06);
  padding: 14px 16px;
  font-size: 13px;
  color: var(--text-2);
  overflow: auto;           // 店铺过多时面板内部纵向滚动，不抻顶级页面
}

// 小屏幕：宽度不足时 aside 自动换行到矩阵下方，不默隐藏（关联店铺是主要业务功能，不能丢失）
@media (max-width: 1280px) {
  .matrix-with-tips {
    flex-wrap: wrap;
  }
  .matrix-aside {
    flex: 1 1 100%;
    max-width: none;
    margin-left: 0;
    margin-top: 12px;
  }
}

.price-matrix-table {
  // 表格本体圆角（不使用 overflow: hidden，避免在 sticky 路径上引入新的包含块导致粘性表头失效）
  :deep(.ant-table) {
    border-radius: 8px;
  }

  // sticky 表头：垂直滚动到达列头时，表头吸附在滚动容器顶部
  :deep(.ant-table-sticky-holder) {
    z-index: 11;
    background: var(--bg-muted);
  }

  :deep(.ant-table-cell) {
    padding: 2px 4px !important;
  }

  // 错身隔离：Ant Design 用于测量列宽的隐藏度量行，不允许被 cell padding 规则覆盖（否则会出现 4-6px 间隙）
  :deep(.ant-table-measure-row),
  :deep(.ant-table-measure-row > td) {
    padding: 0 !important;
    border: 0 !important;
    height: 0 !important;
    line-height: 0 !important;
    font-size: 0 !important;
  }
  // 双保险：visibility: collapse 是 CSS3 专为 table-row 设计的值，让整行折叠为 0 高度，
  // 同时它仍会参与 colgroup 列宽测量，不会破坏 antd 表格布局
  :deep(.ant-table-measure-row) {
    visibility: collapse !important;
  }
  // 三保险：Chrome 对 visibility: collapse 并未完全折叠（会退化为 hidden），需额外隐藏内部 div
  // td 本身仍保留以不破坏 colgroup 列宽测量
  :deep(.ant-table-measure-row > td > div) {
    display: none !important;
  }

  :deep(.region-cell) {
    background-color: var(--bg-muted);
    font-weight: 500;
    border-right: 1px solid var(--border);
  }

  :deep(.region-even td) {
    background-color: var(--bg-card);
  }

  :deep(.region-odd td) {
    background-color: var(--bg-page);
  }

  :deep(.region-even td.region-cell) {
    background-color: var(--bg-muted);
  }

  :deep(.region-odd td.region-cell) {
    background-color: var(--bg-muted);
  }

  // 列头强化：加深背景 + 加重文字，提升列识别度
  :deep(.ant-table-thead > tr > th) {
    white-space: pre-line;
    text-align: center;
    font-size: 12px;
    line-height: 1.4;
    padding: 12px 4px !important;
    background: var(--bg-muted) !important;
    color: var(--text-1) !important;
    font-weight: 600 !important;
    border-bottom: 2px solid var(--border-strong) !important;
  }

  :deep(.ant-table-tbody > tr:last-child > td) {
    border-bottom: 1px solid var(--border);
  }

  // 行 hover 高亮，增强可交互感
  :deep(.ant-table-tbody > tr:hover > td) {
    background: var(--bg-muted) !important;
  }

  // 价格列头（seg_*）背景略微区分，强调主要数据列
  :deep(.ant-table-thead > tr > th[class*="seg_"]) {
    background: var(--color-primary-light) !important;
  }

  // 表格外边框去除（由 .matrix-left 提供外框）
  :deep(.ant-table-container) {
    border: none !important;
  }

  // 清除 thead/tbody 之间的默认间隙（horizontal scroll 模式下 header table 与 body table 分离时可能出现）
  :deep(.ant-table-header) {
    margin: 0 !important;
    padding: 0 !important;
    border-bottom: 0 !important;
    overflow: hidden !important; // 防止预留水平滚动条空间
  }
  :deep(.ant-table-header > table) {
    margin-bottom: 0 !important;
  }
  :deep(.ant-table-body) {
    margin-top: 0 !important;
    padding-top: 0 !important;
    border-top: 0 !important;
  }
  :deep(.ant-table-body > table) {
    margin-top: 0 !important;
    border-top: 0 !important;
  }
  // 首行 td 顶部边距清零，避免 border-collapse 在 thead/tbody 拆分后出现 1–2px 间隙
  :deep(.ant-table-tbody > tr:first-child > td) {
    border-top: 0 !important;
  }
  :deep(.ant-table) {
    margin: 0 !important;
  }
  // fixed 列（region/province）的 sticky 容器也可能带顶部间距
  :deep(.ant-table-cell-fix-left),
  :deep(.ant-table-cell-fix-right) {
    border-top: 0 !important;
  }
}

// 可点击的重量段列头：纵向两行结构【顶部 ⊕ / 主行 ‹ 标题 ›】
.seg-header-with-actions {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 4px;
  width: 100%;
  padding: 2px 0 0;
}

// 顶部一行：拆分按钮居中独占
.seg-header-top-row {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 16px;
  line-height: 1;
}

// 主行：左箭头 / 标题 / 右箭头
.seg-header-main-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 2px;
  width: 100%;
  min-height: 36px;

  .seg-header-clickable {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
  }
}

// 拆分按钮（顶行顺流，使用 Ant Design 矢量图标）
.seg-header-split-btn {
  width: 18px;
  height: 18px;
  line-height: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  color: var(--color-primary);
  background: var(--bg-card);
  border: 1px solid var(--color-primary);
  border-radius: 50%;
  cursor: pointer;
  user-select: none;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
  transition: all 0.15s;
  box-sizing: border-box;

  :deep(.anticon) {
    line-height: 1;
    font-size: 11px;
  }

  &:hover {
    color: var(--text-on-accent);
    background: var(--color-primary);
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px var(--color-primary-border);
    transform: scale(1.15);
  }
}

// 首段/末段不显示合并箭头时，用同宽占位保持标题居中
.seg-header-merge-placeholder {
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
}

// 列头下方进位方式标签
.seg-header-rounding {
  font-size: 11px;
  font-weight: 400;
  color: var(--text-3);
  line-height: 1.2;
  margin-top: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.seg-header-merge-arrow {
  position: relative;
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
  line-height: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  color: var(--color-primary);
  background: var(--bg-card);
  border: 1px solid var(--color-primary);
  border-radius: 50%;
  cursor: pointer;
  z-index: 5;
  user-select: none;
  opacity: 1;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
  transition: all 0.15s;
  box-sizing: border-box;

  &:hover {
    color: var(--text-on-accent);
    background: var(--color-primary);
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px var(--color-primary-border);
    transform: scale(1.1);
  }
}

.seg-header-clickable {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  cursor: pointer;
  padding: 4px 4px;
  margin: -12px -4px;
  min-height: 44px;
  border-radius: 3px;
  transition: background-color 0.15s;
  position: relative;

  &:hover {
    background-color: var(--color-primary-light);

    .seg-header-edit-icon {
      opacity: 1;
      color: var(--color-primary);
    }
  }
}

.seg-header-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  text-align: center;
  line-height: 1.3;
}

// 副标题（首续信息）：仅首续重计价显示；单行小灰字
.seg-header-subtitle {
  font-size: 11px;
  font-weight: 400;
  color: var(--text-2);
  line-height: 1.2;
  margin-top: 1px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.seg-header-edit-icon {
  position: absolute;
  top: 3px;
  right: 4px;
  font-size: 10px;
  color: rgba(0, 0, 0, 0.35);
  opacity: 0;
  transition: all 0.15s;
  line-height: 1;
}

.region-text {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-1);
}

.compact-toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  // 升级为独立白色卡片，与下方矩阵卡片视觉呈现一致的双卡布局
  padding: 10px 14px;
  margin-bottom: 8px;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
  box-shadow:
    0 1px 2px rgba(0, 0, 0, 0.04),
    0 4px 12px rgba(0, 0, 0, 0.06);
  // 细节对齐：强制所有程控件高度 32px（antd middle size），避免微差造成错位视觉
  :deep(.ant-select:not(.ant-select-customize-input) .ant-select-selector),
  :deep(.ant-input),
  :deep(.ant-input-number),
  :deep(.ant-btn) {
    height: 32px;
  }
  :deep(.ant-select-single .ant-select-selector .ant-select-selection-item),
  :deep(.ant-select-single .ant-select-selector .ant-select-selection-placeholder) {
    line-height: 30px;
  }
}

// 嵌入区：工具栏与表格之间，贴表格列头零间距
.before-table-slot {
  margin: 0;
  padding: 0;
  // 贴紧 a-table：抵消表格默认 margin-top（若有）
  + :deep(.price-matrix-table) {
    margin-top: 0;

    .ant-table {
      margin-top: 0 !important;
    }
  }
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

.adjust-popover {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 0;
}
</style>
