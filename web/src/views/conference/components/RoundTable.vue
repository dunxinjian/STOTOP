<template>
  <div class="round-table-view">
    <component
      :is="sortable ? draggableComponent : 'div'"
      v-bind="sortable ? draggableProps : {}"
      :class="{ 'round-table-view__grid': true }"
    >
      <template v-if="sortable" #item="{ element: table }">
        <div
          class="round-table-view__table-wrapper round-table-card"
          @dragover.prevent="onContainerDragOver($event, table)"
          @dragleave="onContainerDragLeave($event)"
          @drop="onContainerDrop($event, table)"
        >
          <div
            class="round-table-view__table"
            :class="{ 'drop-target-active': dropTargetTableId === table.id }"
            :style="{ width: getTableSize(table) + 'px', height: getTableSize(table) + 'px' }"
          >
            <!-- Center: table name + count + drag handle -->
            <div class="round-table-view__center">
              <DragOutlined v-if="sortable" class="table-drag-handle" />
              <div class="round-table-view__name">{{ table.name }}</div>
              <div class="round-table-view__count">{{ table.seats.length }}/{{ table.maxSeats }}</div>
            </div>
            <!-- Seats around the circle -->
            <a-tooltip
              v-for="(seat, seatIdx) in table.seats"
              :key="seat.id"
              :title="`${seat.attendeeName}${seat.company ? ' · ' + seat.company : ''}`"
              placement="top"
            >
              <div
                class="round-table-view__seat"
                :class="{
                  'round-table-view__seat--highlight': isHighlighted(seat),
                  'round-table-view__seat--dragging': draggingSeatId === seat.id,
                }"
                :style="getSeatStyle(seatIdx, table.maxSeats, getTableSize(table))"
                :draggable="props.draggable"
                @dragstart="onSeatDragStart($event, seat, table)"
                @dragend="onSeatDragEnd"
                @click="handleSeatClick(seat, table)"
              >
                {{ getInitial(seat.attendeeName) }}
              </div>
            </a-tooltip>
          </div>
          <div class="round-table-view__table-label">{{ table.name }}</div>
        </div>
      </template>

      <template v-if="!sortable">
        <div
          v-for="table in localTables"
          :key="table.id"
          class="round-table-view__table-wrapper round-table-card"
          @dragover.prevent="onContainerDragOver($event, table)"
          @dragleave="onContainerDragLeave($event)"
          @drop="onContainerDrop($event, table)"
        >
          <div
            class="round-table-view__table"
            :class="{ 'drop-target-active': dropTargetTableId === table.id }"
            :style="{ width: getTableSize(table) + 'px', height: getTableSize(table) + 'px' }"
          >
            <div class="round-table-view__center">
              <div class="round-table-view__name">{{ table.name }}</div>
              <div class="round-table-view__count">{{ table.seats.length }}/{{ table.maxSeats }}</div>
            </div>
            <a-tooltip
              v-for="(seat, seatIdx) in table.seats"
              :key="seat.id"
              :title="`${seat.attendeeName}${seat.company ? ' · ' + seat.company : ''}`"
              placement="top"
            >
              <div
                class="round-table-view__seat"
                :class="{
                  'round-table-view__seat--highlight': isHighlighted(seat),
                  'round-table-view__seat--dragging': draggingSeatId === seat.id,
                }"
                :style="getSeatStyle(seatIdx, table.maxSeats, getTableSize(table))"
                :draggable="props.draggable"
                @dragstart="onSeatDragStart($event, seat, table)"
                @dragend="onSeatDragEnd"
                @click="handleSeatClick(seat, table)"
              >
                {{ getInitial(seat.attendeeName) }}
              </div>
            </a-tooltip>
          </div>
          <div class="round-table-view__table-label">{{ table.name }}</div>
        </div>
      </template>
    </component>
    <EmptyState v-if="!localTables.length" size="small" title="暂无桌次数据" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { DragOutlined } from '@ant-design/icons-vue'
import draggable from 'vuedraggable'

export interface SeatData {
  id: number
  attendeeId: number
  attendeeName: string
  company?: string
  position?: number
}

export interface TableData {
  id: number
  name: string
  seats: SeatData[]
  maxSeats: number
}

interface Props {
  /** 桌次数据 */
  tables: TableData[]
  /** 搜索高亮关键词 */
  searchKeyword?: string
  /** 是否允许座位拖拽换桌 */
  draggable?: boolean
  /** 是否允许桌次排序拖拽 */
  sortable?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  searchKeyword: '',
  draggable: false,
  sortable: false,
})

const emit = defineEmits<{
  'seat-click': [seat: SeatData, table: TableData]
  'seat-drop': [payload: { seat: SeatData; sourceTableId: number; targetTableId: number }]
  'table-reorder': [tableIds: number[]]
}>()

// Local copy for vuedraggable v-model
const localTables = ref<TableData[]>([...props.tables])
watch(
  () => props.tables,
  (val) => {
    localTables.value = [...val]
  },
  { deep: true }
)

// Draggable component reference
const draggableComponent = draggable

const draggableProps = computed(() => ({
  list: localTables.value,
  itemKey: 'id',
  animation: 200,
  handle: '.table-drag-handle',
  ghostClass: 'table-ghost',
  onEnd: onTableDragEnd,
}))

// Seat drag state
const draggingType = ref<'seat' | null>(null)
const draggingSeatId = ref<number | null>(null)
const dragSourceTableId = ref<number | null>(null)
const dropTargetTableId = ref<number | null>(null)

function onSeatDragStart(e: DragEvent, seat: SeatData, table: TableData) {
  draggingType.value = 'seat'
  draggingSeatId.value = seat.id
  dragSourceTableId.value = table.id
  e.dataTransfer?.setData(
    'text/plain',
    JSON.stringify({ dragType: 'seat', seat, sourceTableId: table.id })
  )
}

function onSeatDragEnd() {
  draggingType.value = null
  draggingSeatId.value = null
  dragSourceTableId.value = null
  dropTargetTableId.value = null
}

function onContainerDragOver(e: DragEvent, table: TableData) {
  if (draggingType.value === 'seat' && dragSourceTableId.value !== table.id) {
    dropTargetTableId.value = table.id
  }
}

function onContainerDragLeave(_e: DragEvent) {
  dropTargetTableId.value = null
}

function onContainerDrop(e: DragEvent, table: TableData) {
  dropTargetTableId.value = null
  try {
    const raw = e.dataTransfer?.getData('text/plain')
    if (!raw) return
    const data = JSON.parse(raw)
    if (data.dragType === 'seat' && data.sourceTableId !== table.id) {
      emit('seat-drop', {
        seat: data.seat,
        sourceTableId: data.sourceTableId,
        targetTableId: table.id,
      })
    }
  } catch {
    // ignore parse errors
  }
}

// Table reorder handler
function onTableDragEnd() {
  const tableIds = localTables.value.map((t) => t.id)
  emit('table-reorder', tableIds)
}

function getTableSize(table: TableData): number {
  if (table.maxSeats <= 4) return 120
  if (table.maxSeats <= 8) return 160
  return 200
}

function getSeatStyle(index: number, totalSeats: number, tableSize: number) {
  const angle = (2 * Math.PI * index) / totalSeats - Math.PI / 2
  const radius = tableSize / 2 - 2
  const seatSize = 32
  const centerX = tableSize / 2 - seatSize / 2
  const centerY = tableSize / 2 - seatSize / 2

  return {
    left: `${centerX + radius * Math.cos(angle)}px`,
    top: `${centerY + radius * Math.sin(angle)}px`,
    width: `${seatSize}px`,
    height: `${seatSize}px`,
  }
}

function getInitial(name: string): string {
  return name ? name.charAt(name.length - 1) : '?'
}

function isHighlighted(seat: SeatData): boolean {
  if (!props.searchKeyword) return false
  const kw = props.searchKeyword.toLowerCase()
  return (
    seat.attendeeName.toLowerCase().includes(kw) ||
    (seat.company?.toLowerCase().includes(kw) ?? false)
  )
}

function handleSeatClick(seat: SeatData, table: TableData) {
  emit('seat-click', seat, table)
}
</script>

<style scoped lang="scss">
.round-table-view {
  overflow: auto;
  padding: 16px;

  &__grid {
    display: flex;
    flex-wrap: wrap;
    gap: 32px;
    justify-content: flex-start;
  }

  &__table-wrapper {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
  }

  &__table {
    position: relative;
    border-radius: 50%;
    background: #f5f5f5;
    border: 2px solid #d9d9d9;
    transition: border-color 0.2s, background-color 0.2s;

    &.drop-target-active {
      border: 2px dashed var(--color-primary);
      background-color: var(--color-primary-light);
    }
  }

  &__center {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    text-align: center;
    pointer-events: none;

    .table-drag-handle {
      pointer-events: auto;
    }
  }

  &__name {
    font-size: 13px;
    font-weight: 600;
    color: #434343;
    white-space: nowrap;
  }

  &__count {
    font-size: 11px;
    color: #8c8c8c;
  }

  &__table-label {
    font-size: 12px;
    color: #8c8c8c;
    text-align: center;
  }

  &__seat {
    position: absolute;
    border-radius: 50%;
    background: var(--text-2);
    color: #fff;
    font-size: 12px;
    font-weight: 500;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: transform 0.2s, box-shadow 0.2s, opacity 0.2s;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);

    &:hover {
      transform: scale(1.15);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);
    }

    &--highlight {
      border: 3px solid var(--color-primary);
      background: #fff;
      color: var(--color-primary);
      animation: seat-blink 1s ease-in-out infinite alternate;
    }

    &--dragging {
      opacity: 0.4;
    }
  }
}

.table-drag-handle {
  cursor: grab;
  color: #999;
  font-size: 14px;
  margin-bottom: 2px;

  &:hover {
    color: var(--color-primary);
  }
}

.table-ghost {
  opacity: 0.4;
}

@keyframes seat-blink {
  from {
    box-shadow: 0 0 0 0 var(--color-primary-border);
  }
  to {
    box-shadow: 0 0 0 6px transparent;
  }
}
</style>
