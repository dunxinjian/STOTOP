<template>
  <div class="project-detail-page">
    <PageHeader :title="project?.name || '项目详情'">
      <template #left>
        <a-button type="text" @click="router.back()"><ArrowLeftOutlined /></a-button>
      </template>
      <template #right>
        <a-tag :color="statusColor(project?.status ?? 0)" v-if="project">{{ statusText(project.status) }}</a-tag>
      </template>
    </PageHeader>

    <a-spin :spinning="detailLoading">
      <a-tabs v-model:activeKey="activeTab" class="detail-tabs">
        <!-- 概览 Tab -->
        <a-tab-pane key="overview" tab="概览">
          <a-row :gutter="16" class="stats-row">
            <a-col :span="6" v-for="stat in statCards" :key="stat.label">
              <a-card :bordered="false" class="stat-card">
                <a-statistic :title="stat.label" :value="stat.value" :value-style="{ color: stat.color }" />
              </a-card>
            </a-col>
          </a-row>

          <a-row :gutter="16">
            <a-col :span="12">
              <a-card :bordered="false" class="section-card" title="项目信息">
                <a-descriptions :column="1" :label-style="{ width: '100px' }">
                  <a-descriptions-item label="项目名称">{{ project?.name }}</a-descriptions-item>
                  <a-descriptions-item label="项目描述">{{ project?.description || '暂无' }}</a-descriptions-item>
                  <a-descriptions-item label="负责人">{{ project?.managerName || '未指定' }}</a-descriptions-item>
                  <a-descriptions-item label="关联目标">{{ project?.goalTitle || '无' }}</a-descriptions-item>
                  <a-descriptions-item label="开始日期">{{ project?.startDate || '未设置' }}</a-descriptions-item>
                  <a-descriptions-item label="结束日期">{{ project?.endDate || '未设置' }}</a-descriptions-item>
                  <a-descriptions-item label="创建人">{{ project?.creatorName }}</a-descriptions-item>
                  <a-descriptions-item label="创建时间">{{ formatTime(project?.createTime) }}</a-descriptions-item>
                </a-descriptions>
              </a-card>
            </a-col>
            <a-col :span="12">
              <a-card :bordered="false" class="section-card" title="任务完成进度">
                <div class="progress-ring">
                  <a-progress
                    type="circle"
                    :percent="completionPercent"
                    :size="140"
                    :stroke-color="'var(--color-info)'"
                  />
                </div>
                <a-row :gutter="8" class="progress-detail">
                  <a-col :span="8" class="progress-detail__item">
                    <div class="progress-detail__value" style="color: var(--color-info)">{{ project?.inProgressTaskCount ?? 0 }}</div>
                    <div class="progress-detail__label">进行中</div>
                  </a-col>
                  <a-col :span="8" class="progress-detail__item">
                    <div class="progress-detail__value" style="color: var(--color-success)">{{ project?.completedTaskCount ?? 0 }}</div>
                    <div class="progress-detail__label">已完成</div>
                  </a-col>
                  <a-col :span="8" class="progress-detail__item">
                    <div class="progress-detail__value" style="color: var(--color-danger)">{{ project?.overdueTaskCount ?? 0 }}</div>
                    <div class="progress-detail__label">已逾期</div>
                  </a-col>
                </a-row>
              </a-card>
            </a-col>
          </a-row>
        </a-tab-pane>

        <!-- 任务 Tab -->
        <a-tab-pane key="tasks" tab="任务">
          <a-card :bordered="false" class="section-card">
            <div class="toolbar-bar" style="margin-bottom: 12px">
              <div class="toolbar-left">
                <a-select
                  v-model:value="taskFilters.status"
                  placeholder="状态"
                  allow-clear
                  style="width: 120px"
                  @change="() => handleTaskSearch()"
                >
                  <a-select-option :value="0">待处理</a-select-option>
                  <a-select-option :value="1">进行中</a-select-option>
                  <a-select-option :value="2">已完成</a-select-option>
                  <a-select-option :value="3">已取消</a-select-option>
                </a-select>
                <a-select
                  v-model:value="taskFilters.priority"
                  placeholder="优先级"
                  allow-clear
                  style="width: 120px; margin-left: 8px"
                  @change="() => handleTaskSearch()"
                >
                  <a-select-option :value="0">低</a-select-option>
                  <a-select-option :value="1">中</a-select-option>
                  <a-select-option :value="2">高</a-select-option>
                  <a-select-option :value="3">紧急</a-select-option>
                </a-select>
                <a-input
                  v-model:value="taskFilters.keyword"
                  placeholder="搜索任务"
                  allow-clear
                  style="width: 200px; margin-left: 8px"
                  @keyup.enter="handleTaskSearch"
                  @change="(e: any) => { if (!e.target.value) handleTaskSearch() }"
                >
                  <template #prefix><SearchOutlined /></template>
                </a-input>
              </div>
              <a-button type="primary" @click="showTaskForm = true"><PlusOutlined /> 新建任务</a-button>
            </div>
            <a-table
              :columns="taskColumns"
              :data-source="tasks"
              :loading="taskLoading"
              :pagination="taskPaginationConfig"
              row-key="id"
              size="middle"
              @change="handleTaskTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'title'">
                  <span>{{ record.title }}</span>
                </template>
                <template v-if="column.dataIndex === 'priority'">
                  <PriorityTag :priority="record.priority" />
                </template>
                <template v-if="column.dataIndex === 'status'">
                  <a-tag :color="taskStatusColor(record.status)">{{ taskStatusText(record.status) }}</a-tag>
                </template>
                <template v-if="column.dataIndex === 'planEnd'">
                  {{ record.planEnd || '—' }}
                </template>
              </template>
            </a-table>
          </a-card>

          <!-- 快速创建任务弹窗 -->
          <a-modal v-model:open="showTaskForm" title="新建任务" :width="640" :footer="null" destroy-on-close>
            <TaskForm :project-id="projectId" @submit="onTaskCreated" @cancel="showTaskForm = false" />
          </a-modal>
        </a-tab-pane>

        <!-- 成员 Tab -->
        <a-tab-pane key="members" tab="成员">
          <a-card :bordered="false" class="section-card">
            <div class="toolbar-bar" style="margin-bottom: 12px">
              <span class="section-title">项目成员（{{ members.length }}人）</span>
              <a-button type="primary" @click="showAddMember = true"><PlusOutlined /> 添加成员</a-button>
            </div>
            <a-table
              :columns="memberColumns"
              :data-source="members"
              :loading="memberLoading"
              :pagination="false"
              row-key="id"
              size="middle"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'userName'">
                  <a-space>
                    <a-avatar :size="28" style="background-color: var(--color-info)">
                      {{ (record.userName || '?').charAt(0) }}
                    </a-avatar>
                    {{ record.userName || '未知' }}
                  </a-space>
                </template>
                <template v-if="column.dataIndex === 'role'">
                  <a-select
                    :value="record.role"
                    style="width: 110px"
                    @change="(val: SelectValue) => handleChangeRole(record as ProjectMemberDto, val as number)"
                  >
                    <a-select-option :value="0">成员</a-select-option>
                    <a-select-option :value="1">管理员</a-select-option>
                    <a-select-option :value="2">观察者</a-select-option>
                  </a-select>
                </template>
                <template v-if="column.dataIndex === 'joinTime'">
                  {{ formatTime(record.joinTime) }}
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <a-popconfirm title="确定移除该成员？" @confirm="handleRemoveMember(record as ProjectMemberDto)">
                    <a style="color: var(--color-danger)">移除</a>
                  </a-popconfirm>
                </template>
              </template>
            </a-table>
          </a-card>

          <!-- 添加成员弹窗 -->
          <a-modal v-model:open="showAddMember" title="添加成员" @ok="handleAddMember" :confirm-loading="addMemberLoading">
            <a-form layout="vertical" style="margin-top: 16px">
              <a-form-item label="搜索用户">
                <a-select
                  v-model:value="newMemberUserId"
                  placeholder="搜索用户姓名"
                  show-search
                  allow-clear
                  :filter-option="false"
                  @search="handleMemberSearch"
                  style="width: 100%"
                >
                  <a-select-option v-for="u in memberSearchOptions" :key="u.id" :value="u.id">
                    {{ u.name }}
                  </a-select-option>
                </a-select>
              </a-form-item>
              <a-form-item label="角色">
                <a-select v-model:value="newMemberRole" style="width: 100%">
                  <a-select-option :value="0">成员</a-select-option>
                  <a-select-option :value="1">管理员</a-select-option>
                  <a-select-option :value="2">观察者</a-select-option>
                </a-select>
              </a-form-item>
            </a-form>
          </a-modal>
        </a-tab-pane>

        <!-- 设置 Tab -->
        <a-tab-pane key="settings" tab="设置">
          <a-card :bordered="false" class="section-card" title="编辑项目信息">
            <a-form ref="settingsFormRef" :model="settingsForm" :rules="settingsRules" layout="vertical" style="max-width: 600px">
              <a-form-item label="项目名称" name="name">
                <a-input v-model:value="settingsForm.name" placeholder="项目名称" :maxlength="100" />
              </a-form-item>
              <a-form-item label="项目描述" name="description">
                <a-textarea v-model:value="settingsForm.description" :rows="3" :maxlength="500" show-count />
              </a-form-item>
              <a-row :gutter="16">
                <a-col :span="12">
                  <a-form-item label="开始日期" name="startDate">
                    <a-date-picker v-model:value="settingsForm.startDate" style="width: 100%" value-format="YYYY-MM-DD" />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item label="结束日期" name="endDate">
                    <a-date-picker v-model:value="settingsForm.endDate" style="width: 100%" value-format="YYYY-MM-DD" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-form-item label="负责人" name="managerId">
                <a-select
                  v-model:value="settingsForm.managerId"
                  placeholder="请选择负责人"
                  show-search
                  allow-clear
                  :filter-option="false"
                  @search="handleSettingsUserSearch"
                >
                  <a-select-option v-for="u in settingsUserOptions" :key="u.id" :value="u.id">
                    {{ u.name }}
                  </a-select-option>
                </a-select>
              </a-form-item>
              <a-form-item>
                <a-space>
                  <a-button type="primary" :loading="settingsSaving" @click="handleSaveSettings">保存修改</a-button>
                  <a-popconfirm
                    :title="project?.status === 2 ? '确定取消归档？' : '确定归档此项目？'"
                    @confirm="handleArchiveProject"
                  >
                    <a-button :danger="project?.status !== 2">
                      {{ project?.status === 2 ? '取消归档' : '归档项目' }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </a-form-item>
            </a-form>
          </a-card>
        </a-tab-pane>
      </a-tabs>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type { SelectValue } from 'ant-design-vue/es/select'
import {
  ArrowLeftOutlined, SearchOutlined, PlusOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import PriorityTag from './components/PriorityTag.vue'
import TaskForm from './components/TaskForm.vue'
import {
  getProject, updateProject, getProjectTasks,
  getProjectMembers, addProjectMember, removeProjectMember,
} from '@/api/task'
import { get } from '@/api/request'
import type {
  ProjectDetailDto, ProjectMemberDto, TaskListDto,
  UpdateProjectRequest,
} from '@/types/task'

interface UserOption { id: number; name: string }

const route = useRoute()
const router = useRouter()
const projectId = computed(() => Number(route.params.projectId))

// 详情
const project = ref<ProjectDetailDto | null>(null)
const detailLoading = ref(false)
const activeTab = ref('overview')

// 任务
const tasks = ref<TaskListDto[]>([])
const taskLoading = ref(false)
const taskTotal = ref(0)
const taskPageIndex = ref(1)
const taskPageSize = ref(15)
const showTaskForm = ref(false)

const taskFilters = reactive({
  keyword: '',
  status: undefined as number | undefined,
  priority: undefined as number | undefined,
})

// 成员
const members = ref<ProjectMemberDto[]>([])
const memberLoading = ref(false)
const showAddMember = ref(false)
const addMemberLoading = ref(false)
const newMemberUserId = ref<number | undefined>(undefined)
const newMemberRole = ref(0)
const memberSearchOptions = ref<UserOption[]>([])

// 设置
const settingsFormRef = ref<FormInstance>()
const settingsSaving = ref(false)
const settingsUserOptions = ref<UserOption[]>([])
const settingsForm = reactive({
  name: '',
  description: undefined as string | undefined,
  startDate: undefined as string | undefined,
  endDate: undefined as string | undefined,
  managerId: undefined as number | undefined,
})
const settingsRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
  managerId: [{ required: true, message: '请选择负责人', trigger: 'change' }],
}

const completionPercent = computed(() => {
  if (!project.value || project.value.taskCount === 0) return 0
  return Math.round((project.value.completedTaskCount / project.value.taskCount) * 100)
})

const statCards = computed(() => {
  const p = project.value
  return [
    { label: '总任务数', value: p?.taskCount ?? 0, color: 'var(--color-info)' },
    { label: '已完成', value: p?.completedTaskCount ?? 0, color: 'var(--color-success)' },
    { label: '进行中', value: p?.inProgressTaskCount ?? 0, color: 'var(--color-warning)' },
    { label: '已逾期', value: p?.overdueTaskCount ?? 0, color: 'var(--color-danger)' },
  ]
})

const taskColumns = [
  { title: '任务标题', dataIndex: 'title', ellipsis: true },
  { title: '优先级', dataIndex: 'priority', width: 90 },
  { title: '状态', dataIndex: 'status', width: 90 },
  { title: '负责人', dataIndex: 'assigneeName', width: 100 },
  { title: '截止日期', dataIndex: 'planEnd', width: 120 },
]

const memberColumns = [
  { title: '姓名', dataIndex: 'userName' },
  { title: '角色', dataIndex: 'role', width: 140 },
  { title: '加入时间', dataIndex: 'joinTime', width: 160 },
  { title: '操作', dataIndex: 'action', width: 80 },
]

const taskPaginationConfig = computed(() => ({
  current: taskPageIndex.value,
  pageSize: taskPageSize.value,
  total: taskTotal.value,
  showTotal: (t: number) => `共 ${t} 条`,
  showSizeChanger: true,
}))

function statusText(status: number) {
  const map: Record<number, string> = { 0: '进行中', 1: '已完成', 2: '已归档' }
  return map[status] ?? '未知'
}
function statusColor(status: number) {
  const map: Record<number, string> = { 0: 'processing', 1: 'success', 2: 'default' }
  return map[status] ?? 'default'
}
function taskStatusText(status: number) {
  const map: Record<number, string> = { 0: '待处理', 1: '进行中', 2: '已完成', 3: '已取消' }
  return map[status] ?? '未知'
}
function taskStatusColor(status: number) {
  const map: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }
  return map[status] ?? 'default'
}
function formatTime(t?: string | null) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 16)
}

// 加载详情
async function loadDetail() {
  detailLoading.value = true
  try {
    project.value = await getProject(projectId.value)
    members.value = project.value.members || []
    // 初始化设置表单
    settingsForm.name = project.value.name
    settingsForm.description = project.value.description ?? undefined
    settingsForm.startDate = project.value.startDate ?? undefined
    settingsForm.endDate = project.value.endDate ?? undefined
    settingsForm.managerId = project.value.managerId
    if (project.value.managerName) {
      settingsUserOptions.value = [{ id: project.value.managerId, name: project.value.managerName }]
    }
  } catch {
    message.error('加载项目详情失败')
  } finally {
    detailLoading.value = false
  }
}

// 任务
function handleTaskSearch() {
  taskPageIndex.value = 1
  loadTasks()
}
function handleTaskTableChange(pagination: any) {
  taskPageIndex.value = pagination.current
  taskPageSize.value = pagination.pageSize
  loadTasks()
}
async function loadTasks() {
  taskLoading.value = true
  try {
    const res = await getProjectTasks(projectId.value, {
      pageIndex: taskPageIndex.value,
      pageSize: taskPageSize.value,
      keyword: taskFilters.keyword || undefined,
      status: taskFilters.status ?? undefined,
      priority: taskFilters.priority ?? undefined,
    })
    tasks.value = res.items
    taskTotal.value = res.total
  } catch {
    message.error('加载任务列表失败')
  } finally {
    taskLoading.value = false
  }
}
function onTaskCreated() {
  showTaskForm.value = false
  loadTasks()
  loadDetail() // 刷新统计
}

// 成员管理
async function loadMembers() {
  memberLoading.value = true
  try {
    members.value = await getProjectMembers(projectId.value)
  } catch {
    message.error('加载成员失败')
  } finally {
    memberLoading.value = false
  }
}

let memberSearchTimer: ReturnType<typeof setTimeout> | null = null
function handleMemberSearch(keyword: string) {
  if (memberSearchTimer) clearTimeout(memberSearchTimer)
  memberSearchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      memberSearchOptions.value = await get<UserOption[]>('/system/users/search', { keyword })
    } catch { /* ignore */ }
  }, 300)
}

async function handleAddMember() {
  if (!newMemberUserId.value) {
    message.warning('请选择用户')
    return
  }
  addMemberLoading.value = true
  try {
    await addProjectMember(projectId.value, { userId: newMemberUserId.value, role: newMemberRole.value })
    message.success('成员已添加')
    showAddMember.value = false
    newMemberUserId.value = undefined
    newMemberRole.value = 0
    memberSearchOptions.value = []
    loadMembers()
  } catch {
    message.error('添加成员失败')
  } finally {
    addMemberLoading.value = false
  }
}

async function handleRemoveMember(member: ProjectMemberDto) {
  try {
    await removeProjectMember(projectId.value, member.userId)
    message.success('已移除成员')
    loadMembers()
  } catch {
    message.error('移除失败')
  }
}

async function handleChangeRole(member: ProjectMemberDto, newRole: number) {
  try {
    // Re-add with new role (backend handles upsert)
    await addProjectMember(projectId.value, { userId: member.userId, role: newRole })
    message.success('角色已更新')
    loadMembers()
  } catch {
    message.error('更新角色失败')
  }
}

// 设置
let settingsSearchTimer: ReturnType<typeof setTimeout> | null = null
function handleSettingsUserSearch(keyword: string) {
  if (settingsSearchTimer) clearTimeout(settingsSearchTimer)
  settingsSearchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      settingsUserOptions.value = await get<UserOption[]>('/system/users/search', { keyword })
    } catch { /* ignore */ }
  }, 300)
}

async function handleSaveSettings() {
  try {
    await settingsFormRef.value?.validateFields()
  } catch { return }
  settingsSaving.value = true
  try {
    await updateProject(projectId.value, {
      name: settingsForm.name,
      description: settingsForm.description,
      managerId: settingsForm.managerId!,
      startDate: settingsForm.startDate,
      endDate: settingsForm.endDate,
      status: project.value!.status,
    } as UpdateProjectRequest)
    message.success('项目信息已更新')
    loadDetail()
  } catch {
    message.error('保存失败')
  } finally {
    settingsSaving.value = false
  }
}

async function handleArchiveProject() {
  const newStatus = project.value!.status === 2 ? 0 : 2
  try {
    await updateProject(projectId.value, {
      name: project.value!.name,
      description: project.value!.description,
      managerId: project.value!.managerId,
      startDate: project.value!.startDate,
      endDate: project.value!.endDate,
      status: newStatus,
    } as UpdateProjectRequest)
    message.success(newStatus === 2 ? '项目已归档' : '已取消归档')
    loadDetail()
  } catch {
    message.error('操作失败')
  }
}

// Tab 切换时按需加载
watch(activeTab, (tab) => {
  if (tab === 'tasks' && tasks.value.length === 0) loadTasks()
  if (tab === 'members') loadMembers()
})

onMounted(() => {
  loadDetail()
  loadTasks()
})
</script>

<style scoped lang="scss">
.project-detail-page {
  padding: 0 4px;
}

.detail-tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: 16px;
  }
}

.stats-row {
  margin-bottom: 16px;
}

.stat-card {
  border-radius: 8px;
  text-align: center;
}

.section-card {
  border-radius: 8px;
  margin-bottom: 16px;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
}

.toolbar-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
}

.progress-ring {
  display: flex;
  justify-content: center;
  padding: 16px 0;
}

.progress-detail {
  margin-top: 16px;

  &__item {
    text-align: center;
  }

  &__value {
    font-size: 22px;
    font-weight: 700;
  }

  &__label {
    font-size: 12px;
    color: #8c8c8c;
    margin-top: 4px;
  }
}
</style>
