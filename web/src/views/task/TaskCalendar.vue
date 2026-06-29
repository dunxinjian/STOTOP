<template>
  <div class="task-calendar-page" :class="{ 'task-calendar-page--embedded': embedded }">
    <div v-if="!embedded" class="task-calendar-page__header">
      <h2>任务日历</h2>
      <a-radio-group v-model:value="calendarMode" @change="handleModeChange">
        <a-radio-button value="month">月视图</a-radio-button>
        <a-radio-button value="year">年视图</a-radio-button>
      </a-radio-group>
    </div>
    <div v-else class="task-calendar-page__mode-switch">
      <a-radio-group v-model:value="calendarMode" size="small" @change="handleModeChange">
        <a-radio-button value="month">月视图</a-radio-button>
        <a-radio-button value="year">年视图</a-radio-button>
      </a-radio-group>
    </div>

    <a-spin :spinning="loading">
      <a-calendar v-model:value="currentDate" :mode="calendarMode" @panelChange="onPanelChange">
        <template #dateCellRender="{ current }">
          <div class="task-calendar-page__cell">
            <div
              v-for="task in getTasksForDate(current)"
              :key="task.id"
              class="task-calendar-page__task"
              :class="[`task-calendar-page__task--p${task.priority}`]"
              @click.stop="goToDetail(task.id)"
            >
              <PriorityTag :priority="task.priority" />
              <span class="task-calendar-page__task-title">{{ task.title }}</span>
            </div>
            <div v-if="getTasksForDate(current).length > 3" class="task-calendar-page__more">
              +{{ getTasksForDate(current).length - 3 }} 更多
            </div>
          </div>
        </template>
        <template #monthCellRender="{ current }">
          <div class="task-calendar-page__month-cell">
            <span v-if="getTasksForMonth(current).length > 0" class="task-calendar-page__month-count">
              {{ getTasksForMonth(current).length }} 个任务
            </span>
          </div>
        </template>
      </a-calendar>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { getTasks } from '@/api/task'
import type { TaskListDto } from '@/types/task'
import PriorityTag from './components/PriorityTag.vue'

const props = withDefaults(defineProps<{
  embedded?: boolean
}>(), {
  embedded: false,
})

const router = useRouter()
const loading = ref(false)
const tasks = ref<TaskListDto[]>([])
const currentDate = ref<Dayjs>(dayjs())
const calendarMode = ref<'month' | 'year'>('month')

// Build a map: dateStr -> tasks for quick lookup
const taskDateMap = ref<Record<string, TaskListDto[]>>({})

function buildDateMap() {
  const map: Record<string, TaskListDto[]> = {}
  for (const t of tasks.value) {
    if (!t.planEnd) continue
    const dateStr = dayjs(t.planEnd).format('YYYY-MM-DD')
    if (!map[dateStr]) map[dateStr] = []
    map[dateStr].push(t)
  }
  taskDateMap.value = map
}

function getTasksForDate(date: Dayjs): TaskListDto[] {
  const key = date.format('YYYY-MM-DD')
  const list = taskDateMap.value[key] || []
  return list.slice(0, 3) // show max 3 per cell
}

function getTasksForMonth(date: Dayjs): TaskListDto[] {
  const prefix = date.format('YYYY-MM')
  return tasks.value.filter((t) => t.planEnd && t.planEnd.startsWith(prefix))
}

function goToDetail(taskId: number) {
  router.push({ name: 'TaskDetail', params: { taskId } })
}

async function loadTasks() {
  loading.value = true
  try {
    let startDate: string
    let endDate: string
    if (calendarMode.value === 'month') {
      startDate = currentDate.value.startOf('month').subtract(7, 'day').format('YYYY-MM-DD')
      endDate = currentDate.value.endOf('month').add(7, 'day').format('YYYY-MM-DD')
    } else {
      startDate = currentDate.value.startOf('year').format('YYYY-MM-DD')
      endDate = currentDate.value.endOf('year').format('YYYY-MM-DD')
    }
    const res = await getTasks({
      pageSize: 500,
      planEndFrom: startDate,
      planEndTo: endDate,
      parentTaskId: 0,
    })
    tasks.value = res.items
    buildDateMap()
  } catch {
    message.error('加载任务失败')
  } finally {
    loading.value = false
  }
}

function onPanelChange(date: string | Dayjs, mode: 'month' | 'year') {
  currentDate.value = dayjs(date)
  calendarMode.value = mode
  loadTasks()
}

function handleModeChange(_e: any) {
  loadTasks()
}

watch(currentDate, () => {
  loadTasks()
})

onMounted(loadTasks)
</script>

<style scoped lang="scss">
.task-calendar-page {
  padding: 20px;

  &--embedded {
    padding: 0;
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
    }
  }

  &__mode-switch {
    margin-bottom: 12px;
  }

  &__cell {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  &__task {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 2px 6px;
    border-radius: 4px;
    cursor: pointer;
    font-size: 12px;
    overflow: hidden;
    transition: background 0.2s;

    &:hover {
      background: var(--color-primary-light);
    }

    &--p3 { border-left: 3px solid var(--color-danger); }
    &--p2 { border-left: 3px solid var(--color-warning); }
    &--p1 { border-left: 3px solid var(--color-info); }
    &--p0 { border-left: 3px solid #d9d9d9; }
  }

  &__task-title {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: #333;
  }

  &__more {
    font-size: 11px;
    color: #8c8c8c;
    padding: 0 6px;
  }

  &__month-cell {
    text-align: center;
  }

  &__month-count {
    font-size: 13px;
    color: var(--color-info);
    font-weight: 500;
  }
}
</style>
