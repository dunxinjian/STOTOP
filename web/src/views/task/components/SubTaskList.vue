<template>
  <div class="subtask-list">
    <!-- 进度条 -->
    <div v-if="tasks.length > 0" class="subtask-list__progress">
      <a-progress
        :percent="completionPercent"
        :stroke-color="completionPercent >= 100 ? 'var(--color-success)' : 'var(--color-info)'"
        size="small"
      />
      <span class="subtask-list__stats">{{ completedCount }} / {{ tasks.length }}</span>
    </div>

    <!-- 子任务列表 -->
    <a-spin :spinning="loading">
      <div class="subtask-list__items">
        <div
          v-for="task in tasks"
          :key="task.id"
          class="subtask-list__item"
        >
          <a-checkbox
            :checked="task.status === 2"
            @change="(e: any) => handleToggle(task, e.target.checked)"
          />
          <span
            class="subtask-list__title"
            :class="{ 'subtask-list__title--done': task.status === 2 }"
            @click="emit('select', task)"
          >
            {{ task.title }}
          </span>
          <PriorityTag :priority="task.priority" />
          <span v-if="task.assigneeName" class="subtask-list__assignee">
            {{ task.assigneeName }}
          </span>
        </div>
      </div>
      <a-empty v-if="!loading && tasks.length === 0" description="暂无子任务" :image="false" />
    </a-spin>

    <!-- 内联添加子任务 -->
    <div class="subtask-list__add">
      <a-input
        v-model:value="newTitle"
        placeholder="添加子任务，按回车确认"
        :disabled="adding"
        @pressEnter="handleAdd"
      >
        <template #prefix><PlusOutlined /></template>
        <template #suffix>
          <a-button
            v-if="newTitle"
            type="link"
            size="small"
            :loading="adding"
            @click="handleAdd"
          >添加</a-button>
        </template>
      </a-input>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { getSubtasks, createSubtask, changeTaskStatus } from '@/api/task'
import type { TaskListDto } from '@/types/task'
import PriorityTag from './PriorityTag.vue'

const props = defineProps<{
  parentId: number
}>()

const emit = defineEmits<{
  (e: 'select', task: TaskListDto): void
  (e: 'change'): void
}>()

const loading = ref(false)
const adding = ref(false)
const tasks = ref<TaskListDto[]>([])
const newTitle = ref('')

const completedCount = computed(() => tasks.value.filter((t) => t.status === 2).length)
const completionPercent = computed(() =>
  tasks.value.length > 0 ? Math.round((completedCount.value / tasks.value.length) * 100) : 0,
)

async function loadTasks() {
  loading.value = true
  try {
    tasks.value = await getSubtasks(props.parentId)
  } catch {
    message.error('加载子任务失败')
  } finally {
    loading.value = false
  }
}

async function handleAdd() {
  const title = newTitle.value.trim()
  if (!title) return
  adding.value = true
  try {
    await createSubtask(props.parentId, { title, parentTaskId: props.parentId })
    newTitle.value = ''
    message.success('子任务已添加')
    await loadTasks()
    emit('change')
  } catch {
    message.error('添加子任务失败')
  } finally {
    adding.value = false
  }
}

async function handleToggle(task: TaskListDto, checked: boolean) {
  const targetStatus = checked ? 2 : 0
  try {
    await changeTaskStatus(task.id, { status: targetStatus })
    task.status = targetStatus
    emit('change')
  } catch {
    message.error('状态变更失败')
  }
}

watch(() => props.parentId, loadTasks)
onMounted(loadTasks)

defineExpose({ refresh: loadTasks })
</script>

<style scoped lang="scss">
.subtask-list {
  &__progress {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;

    .ant-progress {
      flex: 1;
    }
  }

  &__stats {
    font-size: 13px;
    color: #8c8c8c;
    white-space: nowrap;
  }

  &__items {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  &__item {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 8px;
    border-radius: 6px;
    transition: background 0.2s;

    &:hover {
      background: #fafafa;
    }
  }

  &__title {
    flex: 1;
    cursor: pointer;
    font-size: 14px;
    color: #333;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;

    &:hover {
      color: var(--color-primary);
    }

    &--done {
      text-decoration: line-through;
      color: #bfbfbf;
    }
  }

  &__assignee {
    font-size: 12px;
    color: #8c8c8c;
    flex-shrink: 0;
  }

  &__add {
    margin-top: 8px;
  }
}
</style>
