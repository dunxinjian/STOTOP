<template>
  <div class="task-detail-page">
    <a-spin :spinning="loading">
      <a-page-header :title="task?.title || '任务详情'" @back="handleBack">
        <template #extra>
          <a-button @click="showEditModal = true">编辑</a-button>
          <a-popconfirm title="确定删除此任务？" @confirm="handleDelete">
            <a-button danger>删除</a-button>
          </a-popconfirm>
        </template>
      </a-page-header>

      <template v-if="task">
        <a-row :gutter="24">
          <!-- 左侧主区域 -->
          <a-col :xs="24" :lg="16">
            <!-- 标题内联编辑 -->
            <div class="task-detail-page__section">
              <div class="task-detail-page__title-row">
                <template v-if="editingTitle">
                  <a-input
                    v-model:value="titleDraft"
                    @pressEnter="saveTitle"
                    @blur="saveTitle"
                    style="font-size: 20px; font-weight: 600"
                  />
                </template>
                <template v-else>
                  <h2 class="task-detail-page__title" @click="startEditTitle">
                    {{ task.title }}
                    <EditOutlined class="task-detail-page__edit-icon" />
                  </h2>
                </template>
                <a-tag :color="statusMap[task.status]?.color" style="margin-left: 12px">
                  {{ statusMap[task.status]?.label }}
                </a-tag>
              </div>
            </div>

            <!-- 描述 -->
            <div class="task-detail-page__section">
              <h3>描述</h3>
              <template v-if="editingDesc">
                <a-textarea
                  v-model:value="descDraft"
                  :rows="6"
                  :maxlength="2000"
                  show-count
                />
                <div class="task-detail-page__desc-actions">
                  <a-button size="small" @click="editingDesc = false">取消</a-button>
                  <a-button size="small" type="primary" @click="saveDescription">保存</a-button>
                </div>
              </template>
              <template v-else>
                <div
                  class="task-detail-page__desc"
                  @click="startEditDesc"
                >
                  {{ task.description || '点击添加描述...' }}
                </div>
              </template>
            </div>

            <!-- 子任务 -->
            <div class="task-detail-page__section">
              <h3>子任务</h3>
              <SubTaskList :parent-id="task.id" @select="handleSubTaskSelect" @change="refresh" />
            </div>

            <!-- 评论区 -->
            <div class="task-detail-page__section">
              <h3>评论</h3>
              <TaskComment :task-id="task.id" />
            </div>

            <!-- 附件区 -->
            <div class="task-detail-page__section">
              <h3>附件</h3>
              <AttachmentUpload :related-type="1" :related-id="task.id" />
            </div>
          </a-col>

          <!-- 右侧边栏 -->
          <a-col :xs="24" :lg="8">
            <div class="task-detail-page__sidebar">
              <!-- 状态流转 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">状态</div>
                <TaskStatusFlow
                  :current-status="task.status"
                  :task-id="task.id"
                  @change="handleStatusChange"
                />
              </div>

              <!-- 优先级 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">优先级</div>
                <div class="task-detail-page__sidebar-content">
                  <PriorityTag :priority="task.priority" />
                  <a-select
                    :value="task.priority"
                    size="small"
                    style="width: 100px; margin-left: 8px"
                    @change="(val: any) => handlePriorityChange(val)"
                  >
                    <a-select-option :value="0">低</a-select-option>
                    <a-select-option :value="1">中</a-select-option>
                    <a-select-option :value="2">高</a-select-option>
                    <a-select-option :value="3">紧急</a-select-option>
                  </a-select>
                </div>
              </div>

              <!-- 负责人 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">负责人</div>
                <a-select
                  :value="task.assigneeId ?? undefined"
                  placeholder="选择负责人"
                  allow-clear
                  show-search
                  :filter-option="false"
                  style="width: 100%"
                  @search="(val: any) => handleUserSearch(val)"
                  @change="(val: any) => handleAssigneeChange(val)"
                >
                  <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
                    {{ u.name }}
                  </a-select-option>
                </a-select>
              </div>

              <!-- 截止日期 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">截止日期</div>
                <a-date-picker
                  :value="task.planEnd ?? undefined"
                  value-format="YYYY-MM-DD"
                  style="width: 100%"
                  @change="(val: any) => handlePlanEndChange(val)"
                />
              </div>

              <!-- 标签 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">标签</div>
                <a-select
                  :value="task.tags.map(t => t.id)"
                  mode="multiple"
                  placeholder="选择标签"
                  style="width: 100%"
                  @change="(val: any) => handleTagsChange(val)"
                >
                  <a-select-option v-for="tag in tagOptions" :key="tag.id" :value="tag.id">
                    <a-tag :color="tag.color" style="margin-right: 0">{{ tag.name }}</a-tag>
                  </a-select-option>
                </a-select>
              </div>

              <!-- 项目归属 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">项目</div>
                <span>{{ task.projectName || '未关联项目' }}</span>
              </div>

              <!-- 可见范围 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">可见范围</div>
                <VisibilityConfig
                  :model-value="task.visibility"
                  :task-id="task.id"
                  @update:model-value="handleVisibilityChange"
                />
              </div>

              <!-- 钉钉推送 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">钉钉推送</div>
                <DingTalkPushBtn :task-id="task.id" :task-title="task.title" />
              </div>

              <!-- 进度上报 -->
              <div class="task-detail-page__sidebar-item">
                <ProgressReport :task-id="task.id" @success="handleProgressSuccess" />
              </div>

              <!-- 进度历史 -->
              <div class="task-detail-page__sidebar-item">
                <div class="task-detail-page__sidebar-label">进度历史</div>
                <ProgressTimeline ref="progressTimelineRef" :task-id="task.id" />
              </div>

              <!-- 任务信息 -->
              <div class="task-detail-page__sidebar-item task-detail-page__meta">
                <div><span class="label">创建人：</span>{{ task.creatorName }}</div>
                <div><span class="label">创建时间：</span>{{ task.createTime }}</div>
                <div><span class="label">更新时间：</span>{{ task.updateTime }}</div>
                <div v-if="task.estimatedHours"><span class="label">预估工时：</span>{{ task.estimatedHours }}h</div>
                <div v-if="task.actualHours"><span class="label">实际工时：</span>{{ task.actualHours }}h</div>
                <div><span class="label">进度：</span>
                  <a-progress :percent="task.progress" size="small" style="width: 120px" />
                </div>
              </div>
            </div>
          </a-col>
        </a-row>
      </template>
      <EmptyState v-else-if="!loading" title="任务不存在" />
    </a-spin>

    <!-- 编辑弹窗 -->
    <a-modal v-model:open="showEditModal" title="编辑任务" :footer="null" width="680px" destroy-on-close>
      <TaskForm :task="task!" @submit="handleEditSubmit" @cancel="showEditModal = false" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { EditOutlined } from '@ant-design/icons-vue'
import {
  getTask, updateTask, deleteTask, changeTaskStatus,
  setTaskPriority, assignTask, setTaskTags, setTaskVisibility,
  getTags,
} from '@/api/task'
import { get } from '@/api/request'
import type { TaskDetailDto, TagListDto } from '@/types/task'
import SubTaskList from './components/SubTaskList.vue'
import TaskComment from './components/TaskComment.vue'
import AttachmentUpload from './components/AttachmentUpload.vue'
import TaskStatusFlow from './components/TaskStatusFlow.vue'
import PriorityTag from './components/PriorityTag.vue'
import VisibilityConfig from './components/VisibilityConfig.vue'
import DingTalkPushBtn from './components/DingTalkPushBtn.vue'
import ProgressReport from './components/ProgressReport.vue'
import ProgressTimeline from './components/ProgressTimeline.vue'
import TaskForm from './components/TaskForm.vue'

interface UserOption {
  id: number
  name: string
}

const statusMap: Record<number, { label: string; color: string }> = {
  0: { label: '待处理', color: 'default' },
  1: { label: '进行中', color: 'processing' },
  2: { label: '已完成', color: 'success' },
  3: { label: '已取消', color: 'error' },
  4: { label: '已暂停', color: 'warning' },
}

const route = useRoute()
const router = useRouter()
const taskId = computed(() => Number(route.params.taskId))

const loading = ref(false)
const task = ref<TaskDetailDto | null>(null)
const showEditModal = ref(false)
const userOptions = ref<UserOption[]>([])
const tagOptions = ref<TagListDto[]>([])
const progressTimelineRef = ref<InstanceType<typeof ProgressTimeline> | null>(null)

// Title inline edit
const editingTitle = ref(false)
const titleDraft = ref('')

// Description inline edit
const editingDesc = ref(false)
const descDraft = ref('')

async function loadTask() {
  loading.value = true
  try {
    task.value = await getTask(taskId.value)
    // Pre-fill user option
    if (task.value.assigneeId && task.value.assigneeName) {
      userOptions.value = [{ id: task.value.assigneeId, name: task.value.assigneeName }]
    }
  } catch {
    message.error('加载任务详情失败')
  } finally {
    loading.value = false
  }
}

async function refresh() {
  await loadTask()
}

function handleBack() {
  router.back()
}

// Title editing
function startEditTitle() {
  titleDraft.value = task.value?.title || ''
  editingTitle.value = true
}

async function saveTitle() {
  const newTitle = titleDraft.value.trim()
  if (!newTitle || !task.value || newTitle === task.value.title) {
    editingTitle.value = false
    return
  }
  try {
    await updateTask(task.value.id, {
      ...buildUpdatePayload(),
      title: newTitle,
    })
    task.value.title = newTitle
    message.success('标题已更新')
  } catch {
    message.error('更新失败')
  }
  editingTitle.value = false
}

// Description editing
function startEditDesc() {
  descDraft.value = task.value?.description || ''
  editingDesc.value = true
}

async function saveDescription() {
  if (!task.value) return
  try {
    await updateTask(task.value.id, {
      ...buildUpdatePayload(),
      description: descDraft.value || null,
    })
    task.value.description = descDraft.value || null
    message.success('描述已更新')
  } catch {
    message.error('更新失败')
  }
  editingDesc.value = false
}

function buildUpdatePayload() {
  const t = task.value!
  return {
    title: t.title,
    description: t.description,
    projectId: t.projectId,
    type: t.type,
    priority: t.priority,
    assigneeId: t.assigneeId,
    planStart: t.planStart,
    planEnd: t.planEnd,
    estimatedHours: t.estimatedHours,
    visibility: t.visibility,
    tagIds: t.tags.map((tag) => tag.id),
  }
}

async function handleStatusChange(newStatus: number) {
  if (task.value) {
    task.value.status = newStatus
  }
}

async function handlePriorityChange(val: number) {
  if (!task.value) return
  try {
    await setTaskPriority(task.value.id, val)
    task.value.priority = val
    message.success('优先级已更新')
  } catch {
    message.error('更新优先级失败')
  }
}

async function handleAssigneeChange(val: number | undefined) {
  if (!task.value) return
  try {
    await assignTask(task.value.id, { assigneeId: val ?? null })
    task.value.assigneeId = val ?? null
    const u = userOptions.value.find((u) => u.id === val)
    task.value.assigneeName = u?.name ?? null
    message.success('负责人已更新')
  } catch {
    message.error('分配负责人失败')
  }
}

async function handlePlanEndChange(val: string | null) {
  if (!task.value) return
  try {
    await updateTask(task.value.id, {
      ...buildUpdatePayload(),
      planEnd: val,
    })
    task.value.planEnd = val
    message.success('截止日期已更新')
  } catch {
    message.error('更新截止日期失败')
  }
}

async function handleTagsChange(tagIds: number[]) {
  if (!task.value) return
  try {
    await setTaskTags(task.value.id, { tagIds })
    await loadTask()
    message.success('标签已更新')
  } catch {
    message.error('更新标签失败')
  }
}

async function handleVisibilityChange(val: number) {
  if (!task.value) return
  try {
    await setTaskVisibility(task.value.id, { visibility: val })
    task.value.visibility = val
    message.success('可见范围已更新')
  } catch {
    message.error('更新可见范围失败')
  }
}

function handleProgressSuccess() {
  progressTimelineRef.value?.refresh()
  loadTask()
}

async function handleDelete() {
  if (!task.value) return
  try {
    await deleteTask(task.value.id)
    message.success('任务已删除')
    router.back()
  } catch {
    message.error('删除失败')
  }
}

function handleEditSubmit(_task: TaskDetailDto) {
  showEditModal.value = false
  loadTask()
}

function handleSubTaskSelect(subTask: any) {
  router.push({ name: 'TaskDetail', params: { taskId: subTask.id } })
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

async function loadTags() {
  try {
    tagOptions.value = await getTags()
  } catch {
    // ignore
  }
}

onMounted(() => {
  loadTask()
  loadTags()
})
</script>

<style scoped lang="scss">
.task-detail-page {
  padding: 20px;

  &__title-row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__title {
    font-size: 20px;
    font-weight: 600;
    cursor: pointer;
    margin: 0;
    display: inline-flex;
    align-items: center;
    gap: 8px;
  }

  &__edit-icon {
    font-size: 14px;
    color: var(--text-disabled);
    opacity: 0;
    transition: opacity 0.2s;
  }

  &__title:hover &__edit-icon {
    opacity: 1;
  }

  &__section {
    margin-bottom: 24px;

    h3 {
      font-size: 16px;
      font-weight: 600;
      margin-bottom: 12px;
      color: var(--text-1);
    }
  }

  &__desc {
    font-size: 14px;
    color: var(--text-2);
    line-height: 1.8;
    white-space: pre-wrap;
    cursor: pointer;
    padding: 8px 12px;
    border-radius: var(--radius-md);
    min-height: 60px;
    transition: background 0.2s;

    &:hover {
      background: var(--bg-muted);
    }
  }

  &__desc-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 8px;
  }

  &__sidebar {
    position: sticky;
    top: 20px;
  }

  &__sidebar-item {
    margin-bottom: 16px;
  }

  &__sidebar-label {
    font-size: 13px;
    color: var(--text-3);
    margin-bottom: 6px;
  }

  &__sidebar-content {
    display: flex;
    align-items: center;
  }

  &__meta {
    padding: 12px;
    background: var(--bg-muted);
    border-radius: var(--radius-lg);
    font-size: 13px;
    color: var(--text-2);

    > div {
      margin-bottom: 6px;

      &:last-child {
        margin-bottom: 0;
      }
    }

    .label {
      color: var(--text-3);
    }
  }
}
</style>
