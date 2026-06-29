<template>
  <div class="my-tasks">
    <div class="my-tasks__header">
      <h2>我的任务</h2>
      <a-button type="primary" @click="handleCreate">
        <PlusOutlined /> 新建任务
      </a-button>
    </div>

    <!-- 筛选区 -->
    <div class="my-tasks__filters">
      <a-select
        v-model:value="query.status"
        placeholder="任务状态"
        allow-clear
        style="width: 130px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="0">待处理</a-select-option>
        <a-select-option :value="1">进行中</a-select-option>
        <a-select-option :value="2">已完成</a-select-option>
        <a-select-option :value="3">已取消</a-select-option>
      </a-select>

      <a-select
        v-model:value="query.priority"
        placeholder="优先级"
        allow-clear
        style="width: 120px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="0">低</a-select-option>
        <a-select-option :value="1">中</a-select-option>
        <a-select-option :value="2">高</a-select-option>
        <a-select-option :value="3">紧急</a-select-option>
      </a-select>

      <a-input-search
        v-model:value="query.keyword"
        placeholder="搜索任务标题"
        style="width: 260px"
        allow-clear
        @search="() => handleSearch()"
      />
    </div>

    <!-- 任务表格 -->
    <a-table
      :columns="columns"
      :data-source="list"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'title'">
          <a @click="handleView(record)">{{ record.title }}</a>
        </template>

        <template v-else-if="column.key === 'priority'">
          <a-tag :color="priorityColor(record.priority)">{{ priorityLabel(record.priority) }}</a-tag>
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
        </template>

        <template v-else-if="column.key === 'progress'">
          <a-progress :percent="record.progress" size="small" style="width: 100px" />
        </template>

        <template v-else-if="column.key === 'planEnd'">
          <span :class="{ 'overdue': isOverdue(record) }">
            {{ record.planEnd ? formatDate(record.planEnd) : '-' }}
          </span>
        </template>

        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
            <a-dropdown :trigger="['click']">
              <a-button type="link" size="small">更多</a-button>
              <template #overlay>
                <a-menu>
                  <a-menu-item v-if="record.status === 0" @click="handleChangeStatus(record.id, 1)">
                    开始任务
                  </a-menu-item>
                  <a-menu-item v-if="record.status === 1" @click="handleChangeStatus(record.id, 2)">
                    标记完成
                  </a-menu-item>
                  <a-menu-item danger @click="handleDelete(record)">删除</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新建任务弹窗 -->
    <a-modal
      v-model:open="createVisible"
      title="新建任务"
      :confirm-loading="createLoading"
      @ok="submitCreate"
      width="560px"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="任务标题" required>
          <a-input v-model:value="form.title" placeholder="输入任务标题" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="3" placeholder="任务描述（可选）" />
        </a-form-item>
        <a-form-item label="优先级">
          <a-select v-model:value="form.priority" style="width: 160px">
            <a-select-option :value="0">低</a-select-option>
            <a-select-option :value="1">中</a-select-option>
            <a-select-option :value="2">高</a-select-option>
            <a-select-option :value="3">紧急</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="截止日期">
          <a-date-picker v-model:value="formDueDate" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { getMyTasks, createTask, changeTaskStatus, deleteTask } from '@/api/task'
import type { TaskListDto } from '@/types/task'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'

const router = useRouter()
const loading = ref(false)
const list = ref<TaskListDto[]>([])
const allData = ref<TaskListDto[]>([])

const query = reactive<{
  status: number | undefined
  priority: number | undefined
  keyword: string | undefined
}>({
  status: undefined,
  priority: undefined,
  keyword: undefined,
})

const pagination = reactive({
  current: 1,
  pageSize: 15,
  total: 0,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
})

const columns = [
  { title: '任务标题', key: 'title', ellipsis: true },
  { title: '优先级', key: 'priority', width: 90 },
  { title: '状态', key: 'status', width: 90 },
  { title: '进度', key: 'progress', width: 140 },
  { title: '截止日期', key: 'planEnd', width: 120 },
  { title: '项目', dataIndex: 'projectName', width: 140, ellipsis: true },
  { title: '操作', key: 'action', width: 140 },
]

const priorityLabels: Record<number, string> = { 0: '低', 1: '中', 2: '高', 3: '紧急' }
const priorityColors: Record<number, string> = { 0: 'default', 1: 'blue', 2: 'orange', 3: 'red' }
const statusLabels: Record<number, string> = { 0: '待处理', 1: '进行中', 2: '已完成', 3: '已取消' }
const statusColors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }

function priorityLabel(p: number) { return priorityLabels[p] ?? `${p}` }
function priorityColor(p: number) { return priorityColors[p] ?? 'default' }
function statusLabel(s: number) { return statusLabels[s] ?? `${s}` }
function statusColor(s: number) { return statusColors[s] ?? 'default' }
function formatDate(d: string) { return d ? d.substring(0, 10) : '' }

function isOverdue(record: any) {
  return record.planEnd && record.status < 2 && dayjs(record.planEnd).isBefore(dayjs(), 'day')
}

async function loadData() {
  loading.value = true
  try {
    const res = await getMyTasks()
    allData.value = res.items
    applyFilters()
  } catch {
    message.error('获取我的任务失败')
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  let filtered = [...allData.value]
  if (query.status !== undefined) filtered = filtered.filter(t => t.status === query.status)
  if (query.priority !== undefined) filtered = filtered.filter(t => t.priority === query.priority)
  if (query.keyword) {
    const kw = query.keyword.toLowerCase()
    filtered = filtered.filter(t => t.title.toLowerCase().includes(kw))
  }
  pagination.total = filtered.length
  const start = (pagination.current - 1) * pagination.pageSize
  list.value = filtered.slice(start, start + pagination.pageSize)
}

function handleSearch() {
  pagination.current = 1
  applyFilters()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  applyFilters()
}

function handleView(record: Record<string, any>) {
  router.push({ name: 'TaskDetail', params: { taskId: record.id } })
}

async function handleChangeStatus(taskId: number, status: number) {
  try {
    await changeTaskStatus(taskId, { status })
    message.success('状态更新成功')
    loadData()
  } catch {
    message.error('状态更新失败')
  }
}

function handleDelete(record: Record<string, any>) {
  Modal.confirm({
    title: '确认删除',
    content: `确定删除任务「${record.title}」吗？`,
    okText: '删除',
    okType: 'danger',
    async onOk() {
      try {
        await deleteTask(record.id)
        message.success('删除成功')
        loadData()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

// ---------- 新建任务 ----------
const createVisible = ref(false)
const createLoading = ref(false)
const formDueDate = ref<Dayjs | undefined>(undefined)
const form = reactive({
  title: '',
  description: '',
  priority: 1,
})

function handleCreate() {
  form.title = ''
  form.description = ''
  form.priority = 1
  formDueDate.value = undefined
  createVisible.value = true
}

async function submitCreate() {
  if (!form.title.trim()) return message.warning('请输入任务标题')
  createLoading.value = true
  try {
    await createTask({
      title: form.title.trim(),
      description: form.description || undefined,
      priority: form.priority,
      planEnd: formDueDate.value ? formDueDate.value.format('YYYY-MM-DD') : undefined,
    } as any)
    message.success('创建成功')
    createVisible.value = false
    loadData()
  } catch {
    message.error('创建失败')
  } finally {
    createLoading.value = false
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.my-tasks {
  padding: 24px;

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
    }
  }

  &__filters {
    display: flex;
    gap: 12px;
    margin-bottom: 16px;
    flex-wrap: wrap;
  }
}

.overdue {
  color: var(--color-danger);
  font-weight: 500;
}
</style>
