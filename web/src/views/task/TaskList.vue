<template>
  <div class="task-list-page" :class="{ 'task-list-page--embedded': embedded }">
    <!-- 顶部快速创建 -->
    <div class="task-list-page__quick-create">
      <a-input
        v-model:value="quickTitle"
        placeholder="快速创建任务，按回车确认"
        :disabled="quickCreating"
        @pressEnter="handleQuickCreate"
      >
        <template #prefix><PlusOutlined /></template>
        <template #suffix>
          <a-button
            v-if="quickTitle"
            type="link"
            size="small"
            :loading="quickCreating"
            @click="handleQuickCreate"
          >创建</a-button>
        </template>
      </a-input>
    </div>

    <!-- 筛选栏 -->
    <div class="task-list-page__filters">
      <a-row :gutter="[12, 12]">
        <a-col :span="5">
          <a-input
            v-model:value="filters.keyword"
            placeholder="搜索任务"
            allow-clear
            @pressEnter="handleSearch"
          >
            <template #prefix><SearchOutlined /></template>
          </a-input>
        </a-col>
        <a-col :span="3">
          <a-select
            v-model:value="filters.status"
            placeholder="状态"
            allow-clear
            style="width: 100%"
            @change="() => handleSearch()"
          >
            <a-select-option :value="0">待处理</a-select-option>
            <a-select-option :value="1">进行中</a-select-option>
            <a-select-option :value="2">已完成</a-select-option>
            <a-select-option :value="3">已取消</a-select-option>
            <a-select-option :value="4">已暂停</a-select-option>
          </a-select>
        </a-col>
        <a-col :span="3">
          <a-select
            v-model:value="filters.priority"
            placeholder="优先级"
            allow-clear
            style="width: 100%"
            @change="() => handleSearch()"
          >
            <a-select-option :value="0">低</a-select-option>
            <a-select-option :value="1">中</a-select-option>
            <a-select-option :value="2">高</a-select-option>
            <a-select-option :value="3">紧急</a-select-option>
          </a-select>
        </a-col>
        <a-col :span="4">
          <a-select
            v-model:value="filters.assigneeId"
            placeholder="负责人"
            allow-clear
            show-search
            :filter-option="false"
            style="width: 100%"
            @search="(val: any) => handleUserSearch(val)"
            @change="() => handleSearch()"
          >
            <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
              {{ u.name }}
            </a-select-option>
          </a-select>
        </a-col>
        <a-col :span="4">
          <a-select
            v-model:value="filters.projectId"
            placeholder="项目"
            allow-clear
            style="width: 100%"
            :options="projectOptions"
            :field-names="{ label: 'name', value: 'id' }"
            @change="() => handleSearch()"
          />
        </a-col>
        <a-col :span="3">
          <a-select
            v-model:value="filters.tagIds"
            placeholder="标签"
            mode="multiple"
            allow-clear
            :max-tag-count="1"
            style="width: 100%"
            @change="() => handleSearch()"
          >
            <a-select-option v-for="tag in tagOptions" :key="tag.id" :value="tag.id">
              <a-tag :color="tag.color" style="margin-right: 0">{{ tag.name }}</a-tag>
            </a-select-option>
          </a-select>
        </a-col>
        <a-col :span="2">
          <a-button @click="handleReset">重置</a-button>
        </a-col>
      </a-row>
      <a-row :gutter="[12, 12]" style="margin-top: 8px">
        <a-col :span="8">
          <a-range-picker
            v-model:value="dateRange"
            value-format="YYYY-MM-DD"
            style="width: 100%"
            :placeholder="['截止开始', '截止结束']"
            @change="() => handleSearch()"
          />
        </a-col>
        <a-col :span="4">
          <a-select
            v-model:value="sortConfig.field"
            placeholder="排序字段"
            style="width: 100%"
            @change="() => handleSearch()"
          >
            <a-select-option value="planEnd">截止日期</a-select-option>
            <a-select-option value="priority">优先级</a-select-option>
            <a-select-option value="createTime">创建时间</a-select-option>
          </a-select>
        </a-col>
        <a-col :span="3">
          <a-select
            v-model:value="sortConfig.order"
            style="width: 100%"
            @change="() => handleSearch()"
          >
            <a-select-option value="asc">升序</a-select-option>
            <a-select-option value="desc">降序</a-select-option>
          </a-select>
        </a-col>
      </a-row>
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedRowKeys.length > 0" class="task-list-page__batch">
      <span>已选择 {{ selectedRowKeys.length }} 项</span>
      <a-select
        v-model:value="batchStatus"
        placeholder="批量修改状态"
        style="width: 140px"
        allow-clear
      >
        <a-select-option :value="0">待处理</a-select-option>
        <a-select-option :value="1">进行中</a-select-option>
        <a-select-option :value="2">已完成</a-select-option>
        <a-select-option :value="3">已取消</a-select-option>
      </a-select>
      <a-select
        v-model:value="batchAssignee"
        placeholder="批量分配负责人"
        style="width: 160px"
        allow-clear
        show-search
        :filter-option="false"
        @search="handleUserSearch"
      >
        <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
          {{ u.name }}
        </a-select-option>
      </a-select>
      <a-select
        v-model:value="batchPriority"
        placeholder="批量修改优先级"
        allow-clear
        style="width: 150px"
      >
        <a-select-option :value="0">低</a-select-option>
        <a-select-option :value="1">中</a-select-option>
        <a-select-option :value="2">高</a-select-option>
        <a-select-option :value="3">紧急</a-select-option>
      </a-select>
      <a-popconfirm title="确认执行批量修改？" @confirm="handleBatchUpdate">
        <a-button type="primary" :loading="batchUpdating">执行</a-button>
      </a-popconfirm>
      <a-button @click="selectedRowKeys = []">取消选择</a-button>
    </div>

    <!-- 任务表格 -->
    <a-table
      :columns="columns"
      :data-source="tasks"
      :loading="loading"
      :pagination="pagination"
      :row-selection="rowSelection"
      row-key="id"
      size="middle"
      :scroll="{ x: 900 }"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'title'">
          <router-link :to="{ name: 'TaskDetail', params: { taskId: record.id } }" class="task-list-page__title-link">
            {{ record.title }}
          </router-link>
        </template>
        <template v-else-if="column.key === 'priority'">
          <PriorityTag :priority="record.priority" />
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="statusMap[record.status]?.color">{{ statusMap[record.status]?.label }}</a-tag>
        </template>
        <template v-else-if="column.key === 'assigneeName'">
          {{ record.assigneeName || '-' }}
        </template>
        <template v-else-if="column.key === 'planEnd'">
          <span :class="{ 'task-list-page__overdue': isOverdue(record as TaskListDto) }">
            {{ record.planEnd || '-' }}
          </span>
        </template>
        <template v-else-if="column.key === 'tags'">
          <a-tag v-for="tag in record.tags" :key="tag.id" :color="tag.color" style="margin-bottom: 2px">
            {{ tag.name }}
          </a-tag>
          <span v-if="!record.tags?.length">-</span>
        </template>
        <template v-else-if="column.key === 'projectName'">
          {{ record.projectName || '-' }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-popconfirm title="确定删除此任务？" @confirm="handleDelete(record.id)">
            <a-button type="text" size="small" danger>删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>

    <!-- 创建任务弹窗 -->
    <a-modal v-model:open="showCreateModal" title="创建任务" :footer="null" width="680px" destroy-on-close>
      <TaskForm @submit="handleCreateSubmit" @cancel="showCreateModal = false" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, SearchOutlined } from '@ant-design/icons-vue'
import { getTasks, createTask, deleteTask, batchUpdateTasks, getTags, getProjects } from '@/api/task'
import { get } from '@/api/request'
import type { TaskListDto, TaskDetailDto, TagListDto, ProjectListDto, TaskPagedRequest } from '@/types/task'
import PriorityTag from './components/PriorityTag.vue'
import TaskForm from './components/TaskForm.vue'
import type { TablePaginationConfig } from 'ant-design-vue'
import type { TableRowSelection, Key } from 'ant-design-vue/es/table/interface'

const props = withDefaults(defineProps<{
  embedded?: boolean
}>(), {
  embedded: false,
})

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

const loading = ref(false)
const tasks = ref<TaskListDto[]>([])
const total = ref(0)
const quickTitle = ref('')
const quickCreating = ref(false)
const showCreateModal = ref(false)
const selectedRowKeys = ref<number[]>([])
const batchStatus = ref<number | undefined>(undefined)
const batchAssignee = ref<number | undefined>(undefined)
const batchPriority = ref<number | undefined>(undefined)
const batchUpdating = ref(false)
const userOptions = ref<UserOption[]>([])
const tagOptions = ref<TagListDto[]>([])
const projectOptions = ref<ProjectListDto[]>([])
const dateRange = ref<[string, string] | undefined>(undefined)

const filters = reactive({
  keyword: '' as string,
  status: undefined as number | undefined,
  priority: undefined as number | undefined,
  assigneeId: undefined as number | undefined,
  projectId: undefined as number | undefined,
  tagIds: [] as number[],
})

const sortConfig = reactive({
  field: 'createTime' as string,
  order: 'desc' as string,
})

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns = [
  { title: '任务标题', key: 'title', dataIndex: 'title', ellipsis: true },
  { title: '优先级', key: 'priority', dataIndex: 'priority', width: 90 },
  { title: '状态', key: 'status', dataIndex: 'status', width: 90 },
  { title: '负责人', key: 'assigneeName', dataIndex: 'assigneeName', width: 100 },
  { title: '截止日期', key: 'planEnd', dataIndex: 'planEnd', width: 110 },
  { title: '标签', key: 'tags', dataIndex: 'tags', width: 160 },
  { title: '项目', key: 'projectName', dataIndex: 'projectName', width: 120, ellipsis: true },
  { title: '操作', key: 'actions', width: 80, fixed: 'right' as const },
]

function isOverdue(record: TaskListDto) {
  if (!record.planEnd || record.status === 2 || record.status === 3) return false
  return new Date(record.planEnd) < new Date()
}

async function loadTasks() {
  loading.value = true
  try {
    const params: TaskPagedRequest = {
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      keyword: filters.keyword || undefined,
      status: filters.status,
      priority: filters.priority,
      assigneeId: filters.assigneeId,
      projectId: filters.projectId,
      tagIds: filters.tagIds.length > 0 ? filters.tagIds : undefined,
      sortField: sortConfig.field,
      sortOrder: sortConfig.order,
      planEndFrom: dateRange.value?.[0] || undefined,
      planEndTo: dateRange.value?.[1] || undefined,
      parentTaskId: 0,
    }
    const res = await getTasks(params)
    tasks.value = res.items
    total.value = res.total
    pagination.total = res.total
  } catch {
    message.error('加载任务列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  loadTasks()
}

function handleReset() {
  filters.keyword = ''
  filters.status = undefined
  filters.priority = undefined
  filters.assigneeId = undefined
  filters.projectId = undefined
  filters.tagIds = []
  dateRange.value = undefined
  sortConfig.field = 'createTime'
  sortConfig.order = 'desc'
  handleSearch()
}

function handleTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  loadTasks()
}

const rowSelection = computed<TableRowSelection<TaskListDto>>(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: Key[]) => { selectedRowKeys.value = keys.map((k) => Number(k)) },
  getCheckboxProps: (record: TaskListDto) => ({
    disabled: record.status === 3, // 已取消的任务不允许批量修改
  }),
}))

async function handleQuickCreate() {
  const title = quickTitle.value.trim()
  if (!title) return
  quickCreating.value = true
  try {
    await createTask({ title })
    message.success('任务已创建')
    quickTitle.value = ''
    await loadTasks()
  } catch {
    message.error('创建失败')
  } finally {
    quickCreating.value = false
  }
}

function handleCreateSubmit(_task: TaskDetailDto) {
  showCreateModal.value = false
  loadTasks()
}

async function handleDelete(id: number) {
  try {
    await deleteTask(id)
    message.success('任务已删除')
    await loadTasks()
  } catch {
    message.error('删除失败')
  }
}

async function handleBatchUpdate() {
  const updateData: Record<string, any> = {}
  if (batchStatus.value !== undefined && batchStatus.value !== null) {
    updateData.status = batchStatus.value
  }
  if (batchAssignee.value !== undefined && batchAssignee.value !== null) {
    updateData.assigneeId = batchAssignee.value
  }
  if (batchPriority.value !== undefined && batchPriority.value !== null) {
    updateData.priority = batchPriority.value
  }
  if (Object.keys(updateData).length === 0) {
    message.warning('请选择要批量修改的内容')
    return
  }
  batchUpdating.value = true
  try {
    await batchUpdateTasks(selectedRowKeys.value, updateData)
    message.success('批量更新成功')
    selectedRowKeys.value = []
    batchStatus.value = undefined
    batchAssignee.value = undefined
    batchPriority.value = undefined
    await loadTasks()
  } catch {
    message.error('批量更新失败')
  } finally {
    batchUpdating.value = false
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

async function loadOptions() {
  try {
    const [tags, projects] = await Promise.all([
      getTags(),
      getProjects({ pageSize: 200 }),
    ])
    tagOptions.value = tags
    projectOptions.value = projects.items
  } catch {
    // ignore
  }
}

onMounted(() => {
  loadOptions()
  loadTasks()
})
</script>

<style scoped lang="scss">
.task-list-page {
  padding: 20px;

  &--embedded {
    padding: 0;
  }

  &__quick-create {
    margin-bottom: 16px;
    max-width: 480px;
  }

  &__filters {
    margin-bottom: 16px;
    padding: 16px;
    background: #fafafa;
    border-radius: 8px;
  }

  &__batch {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
    padding: 10px 16px;
    background: var(--color-primary-light);
    border: 1px solid var(--color-primary-border);
    border-radius: 6px;
  }

  &__title-link {
    color: #333;
    font-weight: 500;

    &:hover {
      color: var(--color-primary);
    }
  }

  &__overdue {
    color: var(--color-danger);
    font-weight: 500;
  }
}
</style>
