<template>
  <div class="gantt-chart" ref="containerRef">
    <!-- Header: time scale -->
    <div class="gantt-chart__header" :style="{ paddingLeft: labelWidth + 'px' }">
      <div class="gantt-chart__time-axis" :style="{ width: timeAxisWidth + 'px' }">
        <div
          v-for="tick in timeTicks"
          :key="tick.offset"
          class="gantt-chart__tick"
          :style="{ left: tick.offset + 'px', width: tick.width + 'px' }"
        >
          <span class="gantt-chart__tick-label">{{ tick.label }}</span>
        </div>
      </div>
    </div>

    <!-- Body: rows + tasks -->
    <div class="gantt-chart__body">
      <div v-for="row in rows" :key="row.id" class="gantt-chart__row">
        <!-- Row label -->
        <div class="gantt-chart__label" :style="{ width: labelWidth + 'px' }">
          <div class="gantt-chart__label-text">{{ row.label }}</div>
          <div v-if="row.subLabel" class="gantt-chart__label-sub">{{ row.subLabel }}</div>
        </div>
        <!-- Row track -->
        <div class="gantt-chart__track" :style="{ width: timeAxisWidth + 'px' }">
          <!-- Grid lines -->
          <div
            v-for="tick in timeTicks"
            :key="'grid-' + tick.offset"
            class="gantt-chart__grid-line"
            :style="{ left: tick.offset + 'px' }"
          />
          <!-- Task bars -->
          <a-tooltip
            v-for="task in getRowTasks(row.id)"
            :key="task.id"
            :title="task.detail || task.title"
            placement="top"
          >
            <div
              class="gantt-chart__task"
              :style="getTaskStyle(task)"
              @click="handleTaskClick(task)"
              @mouseenter="handleTaskHover(task)"
            >
              <span class="gantt-chart__task-text">{{ task.title }}</span>
              <span v-if="task.passengers" class="gantt-chart__task-count">{{ task.passengers }}人</span>
            </div>
          </a-tooltip>
        </div>
      </div>
      <a-empty v-if="!rows.length" description="暂无数据" style="padding: 40px 0;" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

export interface GanttRow {
  id: number
  label: string
  subLabel?: string
}

export interface GanttTask {
  id: number
  rowId: number
  startTime: string
  endTime: string
  title: string
  type: string
  status: string
  passengers?: number
  detail?: string
}

interface Props {
  /** Y轴行数据 */
  rows: GanttRow[]
  /** 任务色块数据 */
  tasks: GanttTask[]
  /** X轴起始日期 */
  startDate: string
  /** X轴结束日期 */
  endDate: string
  /** 时间刻度 */
  timeScale?: 'hour' | 'halfday' | 'day'
}

const props = withDefaults(defineProps<Props>(), {
  timeScale: undefined,
})

const emit = defineEmits<{
  'task-click': [task: GanttTask]
  'task-hover': [task: GanttTask]
}>()

const containerRef = ref<HTMLDivElement>()
const labelWidth = 140

// Status color map
const statusColorMap: Record<string, string> = {
  '待安排': '#d9d9d9',
  '已安排': 'var(--color-info)',
  '进行中': 'var(--color-success)',
  '已完成': '#8c8c8c',
  '异常': 'var(--color-danger)',
}

// Calculate total duration in ms
const totalStartMs = computed(() => new Date(props.startDate).getTime())
const totalEndMs = computed(() => {
  const end = new Date(props.endDate)
  // Include the full end date
  end.setHours(23, 59, 59, 999)
  return end.getTime()
})
const totalDurationMs = computed(() => totalEndMs.value - totalStartMs.value)

// Determine time scale
const effectiveScale = computed<'hour' | 'halfday' | 'day'>(() => {
  if (props.timeScale) return props.timeScale
  const days = totalDurationMs.value / (1000 * 60 * 60 * 24)
  if (days <= 1) return 'hour'
  if (days <= 3) return 'halfday'
  return 'day'
})

// Pixel per ms
const pixelPerTick = computed(() => {
  switch (effectiveScale.value) {
    case 'hour': return 60
    case 'halfday': return 80
    case 'day': return 120
  }
})

const tickCount = computed(() => {
  const ms = totalDurationMs.value
  switch (effectiveScale.value) {
    case 'hour': return Math.ceil(ms / (1000 * 60 * 60))
    case 'halfday': return Math.ceil(ms / (1000 * 60 * 60 * 12))
    case 'day': return Math.ceil(ms / (1000 * 60 * 60 * 24))
  }
})

const timeAxisWidth = computed(() => tickCount.value * pixelPerTick.value)

// Generate time ticks
const timeTicks = computed(() => {
  const ticks: { offset: number; width: number; label: string }[] = []
  const start = new Date(props.startDate)
  const scale = effectiveScale.value
  const pxPerTick = pixelPerTick.value

  for (let i = 0; i < tickCount.value; i++) {
    const tickDate = new Date(start.getTime())
    let label = ''

    switch (scale) {
      case 'hour':
        tickDate.setHours(tickDate.getHours() + i)
        label = `${String(tickDate.getHours()).padStart(2, '0')}:00`
        break
      case 'halfday':
        tickDate.setHours(tickDate.getHours() + i * 12)
        label = `${tickDate.getMonth() + 1}/${tickDate.getDate()} ${tickDate.getHours() < 12 ? '上午' : '下午'}`
        break
      case 'day':
        tickDate.setDate(tickDate.getDate() + i)
        label = `${tickDate.getMonth() + 1}/${tickDate.getDate()}`
        break
    }

    ticks.push({ offset: i * pxPerTick, width: pxPerTick, label })
  }
  return ticks
})

function getRowTasks(rowId: number): GanttTask[] {
  return props.tasks.filter(t => t.rowId === rowId)
}

function getTaskStyle(task: GanttTask) {
  const taskStart = new Date(task.startTime).getTime()
  const taskEnd = new Date(task.endTime).getTime()
  const startOffset = totalDurationMs.value > 0
    ? ((taskStart - totalStartMs.value) / totalDurationMs.value) * timeAxisWidth.value
    : 0
  const width = totalDurationMs.value > 0
    ? ((taskEnd - taskStart) / totalDurationMs.value) * timeAxisWidth.value
    : 40

  return {
    left: `${Math.max(0, startOffset)}px`,
    width: `${Math.max(24, width)}px`,
    backgroundColor: statusColorMap[task.status] || 'var(--color-info)',
  }
}

function handleTaskClick(task: GanttTask) {
  emit('task-click', task)
}

function handleTaskHover(task: GanttTask) {
  emit('task-hover', task)
}
</script>

<style scoped lang="scss">
.gantt-chart {
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  overflow: auto;
  background: #fff;

  &__header {
    border-bottom: 1px solid #f0f0f0;
    background: #fafafa;
    position: sticky;
    top: 0;
    z-index: 2;
  }

  &__time-axis {
    display: flex;
    height: 36px;
    position: relative;
  }

  &__tick {
    display: flex;
    align-items: center;
    justify-content: center;
    position: absolute;
    top: 0;
    height: 100%;
    border-right: 1px solid #f0f0f0;
    box-sizing: border-box;
  }

  &__tick-label {
    font-size: 12px;
    color: #8c8c8c;
    white-space: nowrap;
  }

  &__body {
    min-height: 100px;
  }

  &__row {
    display: flex;
    border-bottom: 1px solid #f5f5f5;
    height: 48px;

    &:last-child {
      border-bottom: none;
    }
  }

  &__label {
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 4px 12px;
    border-right: 1px solid #f0f0f0;
    background: #fafafa;
    position: sticky;
    left: 0;
    z-index: 1;
  }

  &__label-text {
    font-size: 13px;
    font-weight: 500;
    color: #262626;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  &__label-sub {
    font-size: 11px;
    color: #8c8c8c;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  &__track {
    position: relative;
    flex: none;
  }

  &__grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: #f5f5f5;
  }

  &__task {
    position: absolute;
    top: 8px;
    height: 32px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    padding: 0 8px;
    cursor: pointer;
    overflow: hidden;
    transition: opacity 0.2s;
    z-index: 1;

    &:hover {
      opacity: 0.85;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
    }
  }

  &__task-text {
    color: #fff;
    font-size: 12px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    flex: 1;
    min-width: 0;
  }

  &__task-count {
    color: rgba(255, 255, 255, 0.85);
    font-size: 11px;
    margin-left: 4px;
    flex-shrink: 0;
  }
}
</style>
