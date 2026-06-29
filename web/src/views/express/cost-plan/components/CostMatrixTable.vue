<template>
  <!-- 模式A: CalcMethod=1 + national —— 单一输入框 -->
  <div v-if="mode === 'A'" class="cost-matrix-mode-a">
    <a-form-item label="基础价格(元)" :colon="false" class="single-input-form">
      <a-input-number
        :value="(matrix.nationalCell?.basePrice ?? undefined) as any"
        :disabled="readonly"
        :min="0"
        :precision="2"
        placeholder="请输入基础价格"
        style="width: 240px"
        @update:value="(v: any) => onNationalBasePriceChange(v)"
      />
    </a-form-item>
  </div>

  <!-- 模式 B/C/D —— 原生表格 -->
  <div v-else class="cost-matrix-table-wrapper" :class="{ readonly }">
    <table class="cost-matrix-table" :style="{ minWidth: scrollX + 'px' }">
      <thead>
        <tr>
          <!-- 模式 B/D 左侧：大区 + 省份 -->
          <template v-if="hasProvinceCols">
            <th class="col-region sticky-col col-1">大区</th>
            <th class="col-province sticky-col col-2">省份</th>
          </template>
          <!-- 模式 C 左侧：占位 -->
          <template v-else>
            <th class="col-tag sticky-col col-1">范围</th>
          </template>

          <!-- 模式 B：单一 固定金额 列 -->
          <template v-if="mode === 'B'">
            <th class="col-cell">基础价格(元)</th>
          </template>
          <!-- 模式 C/D：动态段列 -->
          <template v-else-if="mode === 'C' || mode === 'D'">
            <th
              v-for="seg in matrix.segments"
              :key="seg.index"
              class="col-cell"
              :title="getSegmentTitle(seg)"
            >
              {{ getSegmentTitle(seg) }}
            </th>
          </template>
        </tr>
      </thead>
      <tbody>
        <!-- 模式 C：单行（虚拟全国行） -->
        <template v-if="mode === 'C'">
          <tr class="data-row">
            <td class="col-tag sticky-col col-1">全国</td>
            <td
              v-for="seg in matrix.segments"
              :key="seg.index"
              class="col-cell cell-td"
              :class="{ 'cell-empty': !cellHasValue(virtualRow.cells[seg.index]), 'cell-editing': isEditing(null, seg.index) }"
              @click="startEdit(null, seg.index)"
            >
              <a-input
                v-if="!readonly"
                :ref="(el: any) => registerInputRef(null, seg.index, el)"
                :value="getCellInputText(null, seg.index, virtualRow.cells[seg.index])"
                size="small"
                placeholder="价格 或 首重+续重"
                class="cell-input"
                @change="(e: any) => onCellInputChange(null, seg.index, e.target.value)"
                @blur="() => commitCellInput(null, seg.index)"
                @keydown.tab.prevent="(e: KeyboardEvent) => { commitCellInput(null, seg.index); onTab(e, null, seg.index) }"
                @keydown.enter.prevent="(e: KeyboardEvent) => { commitCellInput(null, seg.index); onEnter(e, null, seg.index) }"
                @keydown.esc="() => { cancelCellInput(null, seg.index); endEdit() }"
              />
              <span v-else class="cell-readonly">{{ formatCellDisplay(virtualRow.cells[seg.index]) }}</span>
            </td>
          </tr>
        </template>

        <!-- 模式 B/D：省份行 -->
        <template v-else>
          <tr
            v-for="(row, rowIndex) in matrix.rows"
            :key="row.provinceId"
            class="data-row"
            :class="{ 'region-first-row': isRegionFirstRow(rowIndex) }"
          >
            <!-- 大区单元格(rowSpan合并) -->
            <td
              v-if="getRegionRowSpan(row.provinceId) > 0"
              class="col-region sticky-col col-1 region-cell"
              :rowspan="getRegionRowSpan(row.provinceId)"
            >
              {{ row.region }}
            </td>
            <!-- 省份单元格 -->
            <td class="col-province sticky-col col-2">
              {{ row.shortName || row.provinceName }}
            </td>

            <!-- 模式 B：固定金额单列 -->
            <template v-if="mode === 'B'">
              <td
                class="col-cell cell-td"
                :class="{ 'cell-empty': !cellHasValue(row.cells[0]), 'cell-editing': isEditing(row.provinceId, 0) }"
                @click="startEdit(row.provinceId, 0)"
              >
                <a-input
                  v-if="!readonly"
                  :ref="(el: any) => registerInputRef(row.provinceId, 0, el)"
                  :value="getCellInputText(row.provinceId, 0, row.cells[0])"
                  size="small"
                  placeholder="价格 或 首重+续重"
                  class="cell-input"
                  @change="(e: any) => onCellInputChange(row.provinceId, 0, e.target.value)"
                  @blur="() => commitCellInput(row.provinceId, 0)"
                  @keydown.tab.prevent="(e: KeyboardEvent) => { commitCellInput(row.provinceId, 0); onTab(e, row.provinceId, 0) }"
                  @keydown.enter.prevent="(e: KeyboardEvent) => { commitCellInput(row.provinceId, 0); onEnter(e, row.provinceId, 0) }"
                  @keydown.esc="() => { cancelCellInput(row.provinceId, 0); endEdit() }"
                />
                <span v-else class="cell-readonly">{{ formatCellDisplay(row.cells[0]) }}</span>
              </td>
            </template>

            <!-- 模式 D：每段一列 -->
            <template v-else-if="mode === 'D'">
              <td
                v-for="seg in matrix.segments"
                :key="seg.index"
                class="col-cell cell-td"
                :class="{ 'cell-empty': !cellHasValue(row.cells[seg.index]), 'cell-editing': isEditing(row.provinceId, seg.index) }"
                @click="startEdit(row.provinceId, seg.index)"
              >
                <a-input
                  v-if="!readonly"
                  :ref="(el: any) => registerInputRef(row.provinceId, seg.index, el)"
                  :value="getCellInputText(row.provinceId, seg.index, row.cells[seg.index])"
                  size="small"
                  placeholder="价格 或 首重+续重"
                  class="cell-input"
                  @change="(e: any) => onCellInputChange(row.provinceId, seg.index, e.target.value)"
                  @blur="() => commitCellInput(row.provinceId, seg.index)"
                  @keydown.tab.prevent="(e: KeyboardEvent) => { commitCellInput(row.provinceId, seg.index); onTab(e, row.provinceId, seg.index) }"
                  @keydown.enter.prevent="(e: KeyboardEvent) => { commitCellInput(row.provinceId, seg.index); onEnter(e, row.provinceId, seg.index) }"
                  @keydown.esc="() => { cancelCellInput(row.provinceId, seg.index); endEdit() }"
                />
                <span v-else class="cell-readonly">{{ formatCellDisplay(row.cells[seg.index]) }}</span>
              </td>
            </template>
          </tr>
        </template>

        <!-- 空状态 -->
        <tr v-if="!hasAnyRow" class="empty-row">
          <td :colspan="totalCols" class="empty-cell">
            {{ mode === 'D' || mode === 'B' ? '暂无省份数据' : '暂无重量段配置' }}
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import type { CostItemMatrix, CostCell, CostWeightSegment, CostProvinceRow } from '../composables/useCostMatrix'
import { formatCostCell, hasCostCellValue, mergeCostCellInput } from '../utils/costPriceCell'

interface Props {
  matrix: CostItemMatrix
  readonly?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  readonly: false,
})

const emit = defineEmits<{
  (e: 'update:cell', provinceId: number | null, segIndex: number, cell: CostCell): void
  (e: 'update:nationalCell', cell: CostCell): void
  (e: 'dirty'): void
}>()

// ==================== 模式判定 ====================

const mode = computed<'A' | 'B' | 'C' | 'D'>(() => {
  const { calcMethod, pricingScope } = props.matrix
  if (calcMethod === 1 && pricingScope === 'national') return 'A'
  if (calcMethod === 1 && pricingScope === 'province') return 'B'
  if (pricingScope === 'national') return 'C'
  return 'D'
})

const hasProvinceCols = computed(() => mode.value === 'B' || mode.value === 'D')

const virtualRow = computed<CostProvinceRow>(() => {
  return props.matrix.rows[0] ?? {
    provinceId: 0,
    provinceName: '全国',
    shortName: '全国',
    region: '全国',
    regionIndex: 0,
    cells: {} as Record<number, CostCell>,
  }
})

const hasAnyRow = computed(() => {
  if (mode.value === 'C') return props.matrix.segments.length > 0
  if (mode.value === 'B' || mode.value === 'D') return props.matrix.rows.length > 0
  return true
})

const totalCols = computed(() => {
  if (mode.value === 'C') return 1 + props.matrix.segments.length
  if (mode.value === 'B') return 3
  if (mode.value === 'D') return 2 + props.matrix.segments.length
  return 1
})

const scrollX = computed(() => {
  if (mode.value === 'B') return 70 + 90 + 160
  if (mode.value === 'C') return 90 + props.matrix.segments.length * 110
  if (mode.value === 'D') return 70 + 90 + props.matrix.segments.length * 110
  return 300
})

// ==================== 列头格式化 ====================

function getSegmentTitle(seg: CostWeightSegment): string {
  if (seg.weightTo == null) {
    return `${seg.weightFrom}kg以上`
  }
  return `${seg.weightFrom}-${seg.weightTo}kg`
}

// ==================== 大区 rowSpan ====================

const regionSpanMap = computed(() => {
  const map = new Map<number, number>()
  const rows = props.matrix.rows
  let i = 0
  while (i < rows.length) {
    const region = rows[i].region
    let count = 0
    for (let j = i; j < rows.length && rows[j].region === region; j++) count++
    map.set(rows[i].provinceId, count)
    for (let j = i + 1; j < i + count; j++) {
      map.set(rows[j].provinceId, 0)
    }
    i += count
  }
  return map
})

function getRegionRowSpan(provinceId: number): number {
  return regionSpanMap.value.get(provinceId) ?? 1
}

function isRegionFirstRow(rowIndex: number): boolean {
  if (rowIndex === 0) return false
  const cur = props.matrix.rows[rowIndex]
  const prev = props.matrix.rows[rowIndex - 1]
  return !!cur && !!prev && cur.region !== prev.region
}

// ==================== 单元格判空 / 显示 ====================

function cellHasValue(cell: CostCell | undefined): boolean {
  return hasCostCellValue(cell)
}

function formatCellDisplay(cell: CostCell | undefined): string {
  const text = formatCostCell(cell)
  return text || '--'
}

// ==================== 单输入框文本缓冲区 ====================

// key: "provinceId_segIndex"，value: 正在编辑的原始文本
const cellInputDraft = ref<Map<string, string>>(new Map())

function getCellInputText(provinceId: number | null, segIndex: number, cell: CostCell | undefined): string {
  const key = cellKey(provinceId, segIndex)
  if (cellInputDraft.value.has(key)) {
    return cellInputDraft.value.get(key)!
  }
  if (!cell || cell.basePrice == null) return ''
  return formatCostCell(cell)
}

function onCellInputChange(provinceId: number | null, segIndex: number, text: string) {
  const key = cellKey(provinceId, segIndex)
  const newMap = new Map(cellInputDraft.value)
  newMap.set(key, text)
  cellInputDraft.value = newMap
}

function commitCellInput(provinceId: number | null, segIndex: number) {
  const key = cellKey(provinceId, segIndex)
  if (!cellInputDraft.value.has(key)) return
  const text = cellInputDraft.value.get(key)!
  const existingCell = getCell(provinceId, segIndex)
  const parsed = mergeCostCellInput(existingCell, text)
  if (!parsed) return
  const newMap = new Map(cellInputDraft.value)
  newMap.delete(key)
  cellInputDraft.value = newMap
  const cell: CostCell = {
    basePrice: parsed.basePrice,
    firstWeight: parsed.firstWeight,
    continuePrice: parsed.continuePrice,
    continueStep: parsed.continueStep,
    roundingMethodOverride: parsed.roundingMethodOverride,
    truncParamOverride: parsed.truncParamOverride,
    ceilParamOverride: parsed.ceilParamOverride,
    isConfigured: true,
  }
  onCellChange(provinceId, segIndex, cell)
}

function getCell(provinceId: number | null, segIndex: number): CostCell | undefined {
  if (provinceId == null) return virtualRow.value.cells[segIndex]
  return props.matrix.rows.find(r => r.provinceId === provinceId)?.cells[segIndex]
}

function cancelCellInput(provinceId: number | null, segIndex: number) {
  const key = cellKey(provinceId, segIndex)
  const newMap = new Map(cellInputDraft.value)
  newMap.delete(key)
  cellInputDraft.value = newMap
}

// ==================== 编辑态 ====================

const editingCell = ref<{ provinceId: number | null; segIndex: number } | null>(null)
const inputRefs = new Map<string, any>()

function cellKey(provinceId: number | null, segIndex: number): string {
  return `${provinceId ?? 'N'}_${segIndex}`
}

function registerInputRef(provinceId: number | null, segIndex: number, el: any) {
  const key = cellKey(provinceId, segIndex)
  if (el) inputRefs.set(key, el)
  else inputRefs.delete(key)
}

function isEditing(provinceId: number | null, segIndex: number): boolean {
  if (!editingCell.value) return false
  return editingCell.value.provinceId === provinceId && editingCell.value.segIndex === segIndex
}

function startEdit(provinceId: number | null, segIndex: number) {
  if (props.readonly) return
  if (isEditing(provinceId, segIndex)) return
  editingCell.value = { provinceId, segIndex }
  nextTick(() => {
    const el = inputRefs.get(cellKey(provinceId, segIndex))
    el?.focus?.()
  })
}

function endEdit() {
  editingCell.value = null
}

// ==================== 单元格变更 ====================

function onNationalBasePriceChange(val: number | null | undefined) {
  if (props.readonly) return
  const cell: CostCell = {
    basePrice: val == null ? 0 : Number(val),
    continuePrice: 0,
    firstWeight: 0,
    continueStep: 1,
    isConfigured: val != null,
  }
  emit('update:nationalCell', cell)
  emit('dirty')
}

function onCellChange(provinceId: number | null, segIndex: number, cell: CostCell) {
  if (props.readonly) return
  emit('update:cell', provinceId, segIndex, cell)
  emit('dirty')
}

// ==================== 键盘导航 ====================

function moveTo(provinceId: number | null, segIndex: number) {
  editingCell.value = { provinceId, segIndex }
  nextTick(() => {
    const el = inputRefs.get(cellKey(provinceId, segIndex))
    el?.focus?.()
  })
}

function onTab(e: KeyboardEvent, provinceId: number | null, segIndex: number) {
  // 模式 B：纵向到下一行
  if (mode.value === 'B') {
    const rows = props.matrix.rows
    const rowIdx = rows.findIndex(r => r.provinceId === provinceId)
    if (rowIdx >= 0 && rowIdx < rows.length - 1) {
      moveTo(rows[rowIdx + 1].provinceId, 0)
    } else {
      endEdit()
    }
    return
  }
  // C/D：横向移动
  const segs = props.matrix.segments
  const segPos = segs.findIndex(s => s.index === segIndex)
  if (segPos < 0) return
  if (segPos < segs.length - 1) {
    moveTo(provinceId, segs[segPos + 1].index)
    return
  }
  // 末段：D 模式下移到下一行第一段
  if (mode.value === 'D') {
    const rows = props.matrix.rows
    const rowIdx = rows.findIndex(r => r.provinceId === provinceId)
    if (rowIdx >= 0 && rowIdx < rows.length - 1) {
      moveTo(rows[rowIdx + 1].provinceId, segs[0].index)
    } else {
      endEdit()
    }
  } else {
    endEdit()
  }
}

function onEnter(_e: KeyboardEvent, provinceId: number | null, segIndex: number) {
  if (mode.value === 'C') {
    endEdit()
    return
  }
  const rows = props.matrix.rows
  const rowIdx = rows.findIndex(r => r.provinceId === provinceId)
  if (rowIdx < 0) return
  if (rowIdx < rows.length - 1) {
    moveTo(rows[rowIdx + 1].provinceId, segIndex)
  } else {
    endEdit()
  }
}
</script>

<style scoped lang="scss">
.cost-matrix-mode-a {
  padding: 16px 8px;

  .single-input-form {
    margin-bottom: 0;
  }
}

.cost-matrix-table-wrapper {
  overflow-x: auto;
  border: 1px solid #e8e8e8;
  background: #fff;

  &.readonly {
    background: #fafafa;
  }
}

.cost-matrix-table {
  border-collapse: separate;
  border-spacing: 0;
  width: 100%;
  font-size: 12px;
  table-layout: fixed;

  thead th {
    position: sticky;
    top: 0;
    z-index: 3;
    background: #fafafa;
    font-weight: 600;
    color: #333;
    text-align: center;
    padding: 4px 6px;
    border-bottom: 1px solid #e8e8e8;
    border-right: 1px solid #f0f0f0;
    line-height: 1.3;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    height: 32px;

    &.sticky-col {
      z-index: 4;
    }
  }

  tbody td {
    padding: 0 4px;
    height: 32px;
    border-bottom: 1px solid #f0f0f0;
    border-right: 1px solid #f0f0f0;
    text-align: center;
    background: #fff;
    transition: background-color 0.15s;
    vertical-align: middle;
  }

  tbody tr.data-row:hover td {
    background: #f5faff;
  }

  tbody tr.data-row:hover td.cell-empty {
    background: #eef4fb;
  }

  tbody tr.region-first-row > td {
    border-top: 1px solid #d9d9d9;
  }

  td.cell-empty {
    background: #f5f5f5;
    color: #bfbfbf;
  }

  td.cell-editing {
    background: #fff !important;
    box-shadow: inset 0 0 0 1px var(--color-primary);
  }

  td.cell-td {
    cursor: text;
    position: relative;
  }

  td.region-cell {
    background: #fafafa;
    font-weight: 500;
    color: #595959;
    border-right: 1px solid #e8e8e8;
  }

  .sticky-col {
    position: sticky;
    background: #fff;
    z-index: 2;
  }

  thead .sticky-col {
    background: #fafafa;
  }

  .col-1 {
    left: 0;
    width: 70px;
    min-width: 70px;
    max-width: 70px;
  }

  .col-2 {
    left: 70px;
    width: 90px;
    min-width: 90px;
    max-width: 90px;
    border-right: 1px solid #e8e8e8;
  }

  .col-tag {
    width: 90px;
    min-width: 90px;
    max-width: 90px;
    border-right: 1px solid #e8e8e8;
    background: #fafafa;
    font-weight: 500;
    color: #595959;
  }

  .col-cell {
    min-width: 100px;
  }

  .empty-row td.empty-cell {
    height: 80px;
    color: #bfbfbf;
    text-align: center;
    background: #fff;
    font-size: 13px;
  }

  /* 输入框样式 */
  .cell-input {
    width: 100%;

    :deep(.ant-input) {
      border: none;
      box-shadow: none;
      background: transparent;
      text-align: center;
      padding: 0 4px;
      height: 28px;
      font-size: 12px;
    }
  }

  .cell-readonly {
    display: inline-block;
    width: 100%;
    text-align: center;
    color: #333;
    user-select: text;
  }

  td.cell-empty .cell-readonly {
    color: #bfbfbf;
  }
}
</style>
