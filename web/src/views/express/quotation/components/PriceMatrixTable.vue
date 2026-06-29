<template>
  <a-table
    class="price-matrix-table"
    :columns="tableColumns"
    :data-source="matrix"
    :pagination="false"
    :scroll="{ x: scrollX }"
    row-key="provinceId"
    bordered
    size="small"
    :custom-row="() => ({})"
  >
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
      <template v-else-if="typeof column.dataIndex === 'string' && column.dataIndex.startsWith('seg_')">
        <PriceCellInput
          :value="getCellForRender(record as ProvinceRow, (column as MatrixColumn)._segIdx ?? 0)"
          :editing="isEditing(record.provinceId, (column as MatrixColumn)._segIdx ?? 0)"
          :disabled="disabled"
          :has-override="hasCellOverrideForRender(record as ProvinceRow, (column as MatrixColumn)._segIdx ?? 0)"
          @update:value="(cell: PriceCell) => handleCellUpdate(record.provinceId, (column as MatrixColumn)._segIdx ?? 0, cell)"
          @start-edit="startEdit(record.provinceId, (column as MatrixColumn)._segIdx ?? 0)"
          @end-edit="endEdit()"
          @tab-next="moveNext(record.provinceId, (column as MatrixColumn)._segIdx ?? 0)"
          @openOverride="emit('openOverride', record.provinceId, (column as MatrixColumn)._segIdx ?? 0)"
        />
      </template>
    </template>
  </a-table>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ColumnType } from 'ant-design-vue/es/table/interface'
import type { PriceCell, WeightSegment, ProvinceRow } from '../composables/usePriceMatrix'
import { hasCellOverride } from '../composables/usePriceMatrix'
import PriceCellInput from './PriceCellInput.vue'

// 价格矩阵动态列：除标准 ColumnType 外携带 _segIdx 标记段下标
type MatrixColumn = ColumnType<ProvinceRow> & { _segIdx?: number }

interface Props {
  segments: WeightSegment[]
  matrix: ProvinceRow[]
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
})

const emit = defineEmits<{
  cellChange: [provinceId: number, segmentIndex: number, cell: PriceCell]
  openOverride: [provinceId: number, segmentIndex: number]
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
    basePrice: 0,
    firstWeight: 0,
    continuePrice: 0,
    continueStep: 1,
    roundingMethodOverride: null,
    truncParamOverride: null,
    ceilParamOverride: null,
  }
}

function hasCellOverrideForRender(row: ProvinceRow, segmentIndex: number): boolean {
  const cell = row.prices[segmentIndex]
  return cell ? hasCellOverride(cell) : false
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

  // Tab / Enter: 尝试移到同行右侧下一个单元格
  if (colPos < segIndices.length - 1) {
    // 右移
    editingCell.value = { provinceId, segmentIndex: segIndices[colPos + 1] }
  } else if (rowIndex < props.matrix.length - 1) {
    // 换行到下一行第一列
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
  const map = new Map<number, number>() // provinceId -> rowSpan
  let i = 0
  while (i < props.matrix.length) {
    const region = props.matrix[i].region
    let count = 0
    for (let j = i; j < props.matrix.length && props.matrix[j].region === region; j++) {
      count++
    }
    // 第一行占 count, 后续行占 0
    map.set(props.matrix[i].provinceId, count)
    for (let j = i + 1; j < i + count; j++) {
      map.set(props.matrix[j].provinceId, 0)
    }
    i += count
  }
  return map
})

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

  const segCols = props.segments.map((seg) => {
    // 构建列头文本
    let title: string
    if (seg.endWeight != null) {
      title = `${seg.startWeight}-${seg.endWeight}kg`
    } else {
      title = `${seg.startWeight}kg以上`
    }

    // 列宽：统一为 100 （首续重内容稍宽）
    return {
      title,
      dataIndex: `seg_${seg.sortOrder}`,
      width: 100,
      align: 'center' as const,
      _segIdx: seg.sortOrder,
    }
  })

  return [...fixed, ...segCols]
})

const scrollX = computed(() => {
  return 60 + 80 + props.segments.reduce((sum, _seg) => sum + 100, 0)
})
</script>

<style scoped lang="scss">
.price-matrix-table {
  :deep(.ant-table-cell) {
    padding: 2px 4px !important;
  }

  :deep(.region-cell) {
    background-color: var(--bg-muted);
    font-weight: 500;
    border-right: 1px solid var(--border);
  }

  :deep(.ant-table-thead > tr > th) {
    white-space: pre-line;
    text-align: center;
    font-size: 12px;
    line-height: 1.3;
    padding: 4px 4px !important;
  }

  :deep(.ant-table-tbody > tr:last-child > td) {
    border-bottom: 1px solid var(--border);
  }
}

.region-text {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-1);
}
</style>
