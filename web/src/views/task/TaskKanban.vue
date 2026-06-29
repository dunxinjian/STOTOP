<template>
  <div class="kanban-page" :class="{ 'kanban-page--embedded': embedded }">
    <!-- Header -->
    <div v-if="!embedded" class="kanban-header">
      <div class="kanban-header__left">
        <h2 class="kanban-header__title">
          <AppstoreOutlined />
          任务看板
        </h2>
        <a-badge :count="totalCount" :overflow-count="999" :number-style="{ backgroundColor: 'var(--color-info)' }">
          <span class="kanban-header__total">全部任务</span>
        </a-badge>
      </div>
      <div class="kanban-header__right">
        <router-link to="/task/tasks">
          <a-button>
            <UnorderedListOutlined />
            列表视图
          </a-button>
        </router-link>
        <a-button type="primary" @click="handleGlobalAdd">
          <PlusOutlined />
          新建任务
        </a-button>
      </div>
    </div>

    <!-- Filters -->
    <div class="kanban-filters">
      <a-select
        v-model:value="filters.projectId"
        placeholder="项目筛选"
        allow-clear
        style="width: 180px"
        @change="loadKanban"
      >
        <a-select-option v-for="p in projectOptions" :key="p.id" :value="p.id">
          {{ p.name }}
        </a-select-option>
      </a-select>
      <a-select
        v-model:value="filters.assigneeId"
        placeholder="负责人"
        allow-clear
        show-search
        :filter-option="false"
        style="width: 160px"
        @search="handleUserSearch"
        @change="loadKanban"
      >
        <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
          {{ u.name }}
        </a-select-option>
      </a-select>
      <a-select
        v-model:value="filters.priority"
        placeholder="优先级"
        allow-clear
        style="width: 140px"
        @change="loadKanban"
      >
        <a-select-option :value="3">紧急</a-select-option>
        <a-select-option :value="2">高</a-select-option>
        <a-select-option :value="1">中</a-select-option>
        <a-select-option :value="0">低</a-select-option>
      </a-select>
      <a-button @click="resetFilters">
        <ReloadOutlined />
        重置
      </a-button>
    </div>

    <!-- Kanban Board -->
    <div class="kanban-board" :class="{ 'is-loading': loading }">
      <a-spin :spinning="loading" size="large">
        <div class="kanban-columns">
          <div
            v-for="col in columns"
            :key="col.status"
            class="kanban-column"
            :class="{ 'drag-over': dragOverStatus === col.status }"
            @dragover.prevent="handleDragOver(col.status)"
            @dragleave="handleDragLeave"
            @drop="handleDrop($event, col.status)"
          >
            <!-- Column Header -->
            <div class="kanban-column__header">
              <div class="kanban-column__indicator" :style="{ backgroundColor: statusColors[col.status] }" />
              <span class="kanban-column__name">{{ col.statusName }}</span>
              <a-badge
                :count="col.count"
                :number-style="{
                  backgroundColor: statusColors[col.status] + '18',
                  color: statusColors[col.status],
                  fontWeight: 600,
                  boxShadow: 'none',
                }"
              />
            </div>

            <!-- Cards -->
            <div class="kanban-column__body">
              <TransitionGroup name="card-list" tag="div" class="kanban-column__cards">
                <div
                  v-for="(card, idx) in col.cards"
                  :key="card.id"
                  class="kanban-card"
                  draggable="true"
                  :class="{ 'is-dragging': draggingCard?.id === card.id }"
                  @dragstart="handleDragStart($event, card, col.status, idx)"
                  @dragend="handleDragEnd"
                >
                  <div class="kanban-card__top">
                    <span v-if="card.code" class="kanban-card__code">{{ card.code }}</span>
                    <PriorityTag :priority="card.priority" />
                  </div>
                  <div class="kanban-card__title">{{ card.title }}</div>

                  <!-- Progress -->
                  <div v-if="card.subTaskCount > 0" class="kanban-card__progress">
                    <a-progress
                      :percent="Math.round((card.completedSubTaskCount / card.subTaskCount) * 100)"
                      :stroke-color="statusColors[col.status]"
                      size="small"
                      :show-info="false"
                    />
                    <span class="kanban-card__subtask-count">
                      {{ card.completedSubTaskCount }}/{{ card.subTaskCount }}
                    </span>
                  </div>

                  <!-- Tags -->
                  <div v-if="card.tags.length" class="kanban-card__tags">
                    <a-tag v-for="tag in card.tags" :key="tag.id" :color="tag.color" size="small">
                      {{ tag.name }}
                    </a-tag>
                  </div>

                  <!-- Footer -->
                  <div class="kanban-card__footer">
                    <div class="kanban-card__assignee">
                      <a-avatar v-if="card.assigneeName" :size="22" :style="{ backgroundColor: getAvatarColor(card.assigneeName) }">
                        {{ card.assigneeName?.charAt(0) }}
                      </a-avatar>
                      <span v-if="card.assigneeName" class="kanban-card__assignee-name">{{ card.assigneeName }}</span>
                      <span v-else class="kanban-card__unassigned">未分配</span>
                    </div>
                    <div v-if="card.planEnd" class="kanban-card__due" :class="{ overdue: isOverdue(card.planEnd, col.status) }">
                      <CalendarOutlined />
                      {{ formatDate(card.planEnd) }}
                    </div>
                  </div>
                </div>
              </TransitionGroup>

              <!-- Empty State -->
              <div v-if="!col.cards.length && !loading" class="kanban-column__empty">
                <InboxOutlined />
                <span>暂无任务</span>
              </div>
            </div>

            <!-- Add Task Button -->
            <div class="kanban-column__footer">
              <a-button type="text" block class="kanban-column__add-btn" @click="handleAddTask(col.status)">
                <PlusOutlined />
                添加任务
              </a-button>
            </div>
          </div>
        </div>
      </a-spin>
    </div>

    <!-- Quick Add Modal -->
    <a-modal
      v-model:open="showQuickAdd"
      :title="quickAddTitle"
      :footer="null"
      width="640px"
      destroy-on-close
    >
      <TaskForm
        :project-id="filters.projectId ?? undefined"
        @submit="onTaskCreated"
        @cancel="showQuickAdd = false"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  AppstoreOutlined,
  UnorderedListOutlined,
  PlusOutlined,
  ReloadOutlined,
  CalendarOutlined,
  InboxOutlined,
} from '@ant-design/icons-vue'
import type {
  KanbanColumnDto,
  KanbanCardDto,
  ProjectListDto,
} from '@/types/task'
import {
  getKanbanData,
  kanbanMove,
  getProjects,
} from '@/api/task'
import { get } from '@/api/request'
import PriorityTag from './components/PriorityTag.vue'
import TaskForm from './components/TaskForm.vue'

const props = withDefaults(defineProps<{
  embedded?: boolean
}>(), {
  embedded: false,
})

interface UserOption {
  id: number
  name: string
}

// ==================== State ====================

const loading = ref(false)
const columns = ref<KanbanColumnDto[]>([])
const projectOptions = ref<ProjectListDto[]>([])
const userOptions = ref<UserOption[]>([])

const filters = reactive({
  projectId: undefined as number | undefined,
  assigneeId: undefined as number | undefined,
  priority: undefined as number | undefined,
})

const statusColors: Record<number, string> = {
  0: '#3A6FB0',
  1: '#E6A700',
  2: '#2BA471',
  3: '#E5484D',
}

const defaultColumns: { status: number; statusName: string }[] = [
  { status: 0, statusName: '待处理' },
  { status: 1, statusName: '进行中' },
  { status: 2, statusName: '已完成' },
  { status: 3, statusName: '已取消' },
]

// Drag state
const draggingCard = ref<KanbanCardDto | null>(null)
const dragSourceStatus = ref<number | null>(null)
const dragSourceIndex = ref<number>(-1)
const dragOverStatus = ref<number | null>(null)

// Quick add
const showQuickAdd = ref(false)
const addToStatus = ref<number>(0)

const totalCount = computed(() => columns.value.reduce((sum, col) => sum + col.count, 0))

const quickAddTitle = computed(() => {
  const statusName = defaultColumns.find(c => c.status === addToStatus.value)?.statusName ?? '新建'
  return `新建任务 - ${statusName}`
})

// ==================== Data Loading ====================

async function loadKanban() {
  loading.value = true
  try {
    const data = await getKanbanData({
      projectId: filters.projectId ?? null,
      assigneeId: filters.assigneeId ?? null,
      priority: filters.priority ?? null,
    })
    // Ensure all 4 columns exist even if backend skips empty ones
    columns.value = defaultColumns.map(dc => {
      const found = data.columns.find(c => c.status === dc.status)
      return found ?? { status: dc.status, statusName: dc.statusName, count: 0, cards: [] }
    })
  } catch {
    message.error('加载看板数据失败')
    columns.value = defaultColumns.map(dc => ({ ...dc, count: 0, cards: [] }))
  } finally {
    loading.value = false
  }
}

async function loadOptions() {
  try {
    const projects = await getProjects({ pageSize: 200 })
    projectOptions.value = projects.items
  } catch {
    // ignore
  }
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function handleUserSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      userOptions.value = await get<UserOption[]>('/system/users/search', { keyword })
    } catch {
      // ignore
    }
  }, 300)
}

function resetFilters() {
  filters.projectId = undefined
  filters.assigneeId = undefined
  filters.priority = undefined
  loadKanban()
}

// ==================== Drag & Drop ====================

function handleDragStart(e: DragEvent, card: KanbanCardDto, sourceStatus: number, idx: number) {
  draggingCard.value = card
  dragSourceStatus.value = sourceStatus
  dragSourceIndex.value = idx
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', String(card.id))
  }
  // Make ghost semi-transparent
  const target = e.target as HTMLElement
  requestAnimationFrame(() => {
    target.classList.add('is-dragging')
  })
}

function handleDragOver(status: number) {
  dragOverStatus.value = status
}

function handleDragLeave() {
  dragOverStatus.value = null
}

function handleDragEnd() {
  draggingCard.value = null
  dragSourceStatus.value = null
  dragSourceIndex.value = -1
  dragOverStatus.value = null
}

async function handleDrop(_e: DragEvent, targetStatus: number) {
  dragOverStatus.value = null
  const card = draggingCard.value
  const sourceStatus = dragSourceStatus.value

  if (!card || sourceStatus === null) return

  // Calculate target sort (append to end)
  const targetCol = columns.value.find(c => c.status === targetStatus)
  const targetSort = targetCol ? targetCol.cards.length : 0

  // Optimistic update: move the card immediately
  const sourceCol = columns.value.find(c => c.status === sourceStatus)
  if (sourceCol) {
    sourceCol.cards = sourceCol.cards.filter(c => c.id !== card.id)
    sourceCol.count = sourceCol.cards.length
  }
  if (targetCol) {
    const movedCard = { ...card, status: targetStatus, sort: targetSort }
    targetCol.cards.push(movedCard)
    targetCol.count = targetCol.cards.length
  }

  // Reset drag state
  draggingCard.value = null
  dragSourceStatus.value = null
  dragSourceIndex.value = -1

  // Call API
  try {
    await kanbanMove({
      taskId: card.id,
      targetStatus,
      targetSort,
    })
    if (sourceStatus !== targetStatus) {
      message.success('任务状态已更新')
    }
  } catch {
    message.error('移动失败，正在恢复...')
    await loadKanban()
  }
}

// ==================== Task Actions ====================

function handleAddTask(status: number) {
  addToStatus.value = status
  showQuickAdd.value = true
}

function handleGlobalAdd() {
  addToStatus.value = 0
  showQuickAdd.value = true
}

function onTaskCreated() {
  showQuickAdd.value = false
  loadKanban()
}

// ==================== Helpers ====================

function formatDate(dateStr: string): string {
  const d = new Date(dateStr)
  const month = d.getMonth() + 1
  const day = d.getDate()
  return `${month}/${day}`
}

function isOverdue(planEnd: string, status: number): boolean {
  if (status === 2 || status === 3) return false
  return new Date(planEnd) < new Date()
}

function getAvatarColor(name: string): string {
  const colors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5']
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  return colors[Math.abs(hash) % colors.length]
}

// ==================== Init ====================

onMounted(() => {
  loadKanban()
  loadOptions()
})
</script>

<style scoped lang="scss">
.kanban-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 20px 24px;
  background: #f5f7fa;

  &--embedded {
    padding: 0;
  }
}

.kanban-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  flex-shrink: 0;

  &__left {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  &__title {
    margin: 0;
    font-size: 20px;
    font-weight: 600;
    color: #1d2129;
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__total {
    font-size: 13px;
    color: #86909c;
    padding: 2px 4px;
  }

  &__right {
    display: flex;
    gap: 8px;
  }
}

.kanban-filters {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  flex-shrink: 0;
}

.kanban-board {
  flex: 1;
  min-height: 0;
  overflow: hidden;

  :deep(.ant-spin-nested-loading),
  :deep(.ant-spin-container) {
    height: 100%;
  }
}

.kanban-columns {
  display: flex;
  gap: 16px;
  height: 100%;
  overflow-x: auto;
  padding-bottom: 8px;

  &::-webkit-scrollbar {
    height: 6px;
  }
  &::-webkit-scrollbar-thumb {
    background: #c9cdd4;
    border-radius: 3px;
  }
}

.kanban-column {
  flex: 0 0 300px;
  width: 300px;
  display: flex;
  flex-direction: column;
  background: #fff;
  border-radius: 12px;
  border: 2px solid transparent;
  transition: border-color 0.2s, box-shadow 0.2s;
  overflow: hidden;

  &.drag-over {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px var(--color-primary-border);
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 16px 16px 12px;
    flex-shrink: 0;
  }

  &__indicator {
    width: 4px;
    height: 18px;
    border-radius: 2px;
  }

  &__name {
    font-size: 14px;
    font-weight: 600;
    color: #1d2129;
    flex: 1;
  }

  &__body {
    flex: 1;
    overflow-y: auto;
    padding: 0 12px 12px;
    min-height: 80px;

    &::-webkit-scrollbar {
      width: 4px;
    }
    &::-webkit-scrollbar-thumb {
      background: #e5e6eb;
      border-radius: 2px;
    }
  }

  &__cards {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  &__empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 32px 0;
    color: #c9cdd4;
    font-size: 13px;

    .anticon {
      font-size: 28px;
    }
  }

  &__footer {
    flex-shrink: 0;
    padding: 8px 12px 12px;
  }

  &__add-btn {
    color: #86909c;
    font-size: 13px;
    border: 1px dashed #e5e6eb;
    border-radius: 8px;
    height: 36px;

    &:hover {
      color: var(--color-primary);
      border-color: var(--color-primary);
      background: var(--color-primary-light);
    }
  }
}

.kanban-card {
  padding: 12px;
  background: #fff;
  border: 1px solid #f0f1f3;
  border-radius: 10px;
  cursor: grab;
  transition: box-shadow 0.2s, opacity 0.2s, transform 0.15s;
  user-select: none;

  &:hover {
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
    transform: translateY(-1px);
  }

  &:active {
    cursor: grabbing;
  }

  &.is-dragging {
    opacity: 0.4;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
    transform: rotate(2deg);
  }

  &__top {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 6px;
  }

  &__code {
    font-size: 11px;
    color: #86909c;
    font-family: 'SF Mono', 'JetBrains Mono', Consolas, monospace;
    letter-spacing: 0.3px;
  }

  &__title {
    font-size: 14px;
    font-weight: 500;
    color: #1d2129;
    line-height: 1.5;
    margin-bottom: 8px;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    word-break: break-all;
  }

  &__progress {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;

    :deep(.ant-progress) {
      flex: 1;
    }
  }

  &__subtask-count {
    font-size: 11px;
    color: #86909c;
    white-space: nowrap;
  }

  &__tags {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin-bottom: 8px;

    :deep(.ant-tag) {
      margin: 0;
      font-size: 11px;
      line-height: 18px;
      padding: 0 6px;
      border-radius: 4px;
    }
  }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 4px;
  }

  &__assignee {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  &__assignee-name {
    font-size: 12px;
    color: #4e5969;
  }

  &__unassigned {
    font-size: 12px;
    color: #c9cdd4;
  }

  &__due {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 12px;
    color: #86909c;

    &.overdue {
      color: var(--color-danger);
      font-weight: 500;
    }
  }
}

// Transition animations
.card-list-enter-active,
.card-list-leave-active {
  transition: all 0.25s ease;
}
.card-list-enter-from {
  opacity: 0;
  transform: translateY(-12px);
}
.card-list-leave-to {
  opacity: 0;
  transform: translateX(20px);
}
.card-list-move {
  transition: transform 0.25s ease;
}
</style>
