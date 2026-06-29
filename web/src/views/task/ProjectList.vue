<template>
  <div class="project-list-page">
    <PageHeader title="项目管理">
      <template #right>
        <a-button type="primary" @click="showCreateModal = true">
          <PlusOutlined /> 新增项目
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-radio-group v-model:value="filters.status" button-style="solid" @change="() => handleSearch()">
              <a-radio-button :value="undefined">全部</a-radio-button>
              <a-radio-button :value="0">进行中</a-radio-button>
              <a-radio-button :value="1">已完成</a-radio-button>
              <a-radio-button :value="2">已归档</a-radio-button>
            </a-radio-group>
            <a-input
              v-model:value="filters.keyword"
              placeholder="搜索项目名称"
              allow-clear
              style="width: 220px"
              @keyup.enter="handleSearch"
              @change="(e: any) => { if (!e.target.value) handleSearch() }"
            >
              <template #prefix><SearchOutlined /></template>
            </a-input>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-segmented v-model:value="viewMode" :options="viewOptions" />
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 卡片视图 -->
    <a-spin :spinning="loading">
      <template v-if="viewMode === 'card'">
        <EmptyState v-if="!loading && tableData.length === 0" title="暂无项目" />
        <a-row :gutter="[16, 16]" v-else>
          <a-col :xs="24" :sm="12" :lg="8" :xl="6" v-for="item in tableData" :key="item.id">
            <a-card hoverable class="project-card" @click="goDetail(item.id)">
              <template #actions>
                <a-tooltip :title="item.status === 2 ? '取消归档' : '归档'">
                  <InboxOutlined @click.stop="handleArchive(item)" />
                </a-tooltip>
                <a-tooltip title="编辑">
                  <EditOutlined @click.stop="handleEdit(item)" />
                </a-tooltip>
              </template>
              <div class="project-card__header">
                <span class="project-card__name">{{ item.name }}</span>
                <a-tag :color="statusColor(item.status)" class="project-card__status">{{ statusText(item.status) }}</a-tag>
              </div>
              <a-typography-paragraph
                :content="item.description || '暂无描述'"
                :ellipsis="{ rows: 2 }"
                type="secondary"
                class="project-card__desc"
              />
              <div class="project-card__meta">
                <div class="project-card__progress">
                  <a-progress
                    :percent="item.taskCount > 0 ? Math.round((item.completedTaskCount / item.taskCount) * 100) : 0"
                    :size="'small'"
                    :stroke-color="'var(--color-info)'"
                  />
                  <span class="project-card__task-count">{{ item.completedTaskCount }}/{{ item.taskCount }} 任务</span>
                </div>
                <div class="project-card__info">
                  <span><UserOutlined /> {{ item.managerName || '未指定' }}</span>
                  <span><TeamOutlined /> {{ item.memberCount }} 人</span>
                  <span class="project-card__time">{{ formatDate(item.createTime) }}</span>
                </div>
              </div>
            </a-card>
          </a-col>
        </a-row>
      </template>

      <!-- 列表视图 -->
      <template v-else>
        <a-table
          :columns="columns"
          :data-source="tableData"
          :loading="loading"
          :pagination="paginationConfig"
          row-key="id"
          bordered
          :scroll="{ x: 1000 }"
          class="project-table"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'name'">
              <a @click="goDetail(record.id)">{{ record.name }}</a>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'progress'">
              <a-progress
                :percent="record.taskCount > 0 ? Math.round((record.completedTaskCount / record.taskCount) * 100) : 0"
                :size="'small'"
              />
              <span style="font-size: 12px; color: #8c8c8c">{{ record.completedTaskCount }}/{{ record.taskCount }}</span>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ formatDate(record.createTime) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-space>
                <a @click="goDetail(record.id)">详情</a>
                <a @click="handleEdit(record)">编辑</a>
                <a-popconfirm
                  :title="record.status === 2 ? '确定取消归档？' : '确定归档此项目？'"
                  @confirm="handleArchive(record)"
                >
                  <a>{{ record.status === 2 ? '取消归档' : '归档' }}</a>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </template>

      <!-- 卡片视图分页 -->
      <div v-if="viewMode === 'card' && total > pageSize" class="card-pagination">
        <a-pagination
          v-model:current="pageIndex"
          v-model:pageSize="pageSize"
          :total="total"
          show-size-changer
          show-quick-jumper
          :show-total="(t: number) => `共 ${t} 个项目`"
          @change="loadData"
        />
      </div>
    </a-spin>

    <!-- 新增/编辑项目弹窗 -->
    <a-modal
      v-model:open="showCreateModal"
      :title="editingProject ? '编辑项目' : '新增项目'"
      :confirm-loading="submitLoading"
      @ok="handleCreateSubmit"
      @cancel="resetForm"
      :width="560"
    >
      <a-form ref="formRef" :model="formState" :rules="formRules" layout="vertical" style="margin-top: 16px">
        <a-form-item label="项目名称" name="name">
          <a-input v-model:value="formState.name" placeholder="请输入项目名称" :maxlength="100" />
        </a-form-item>
        <a-form-item label="项目描述" name="description">
          <a-textarea v-model:value="formState.description" placeholder="请输入项目描述" :rows="3" :maxlength="500" show-count />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="开始日期" name="startDate">
              <a-date-picker v-model:value="formState.startDate" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结束日期" name="endDate">
              <a-date-picker v-model:value="formState.endDate" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="项目负责人" name="managerId">
          <a-select
            v-model:value="formState.managerId"
            placeholder="请选择负责人"
            show-search
            allow-clear
            :filter-option="false"
            @search="handleUserSearch"
          >
            <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
              {{ u.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import {
  PlusOutlined, SearchOutlined,
  UserOutlined, TeamOutlined, EditOutlined, InboxOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getProjects, createProject, updateProject } from '@/api/task'
import { get } from '@/api/request'
import type { ProjectListDto, CreateProjectRequest, UpdateProjectRequest } from '@/types/task'

interface UserOption { id: number; name: string }

const router = useRouter()

// 数据
const loading = ref(false)
const submitLoading = ref(false)
const tableData = ref<ProjectListDto[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(12)
const viewMode = ref<string>('card')
const showCreateModal = ref(false)
const editingProject = ref<ProjectListDto | null>(null)
const userOptions = ref<UserOption[]>([])
const formRef = ref<FormInstance>()

const viewOptions = [
  { label: '卡片', value: 'card' },
  { label: '列表', value: 'list' },
]

const filters = reactive({
  keyword: '',
  status: undefined as number | undefined,
})

const formState = reactive({
  name: '',
  description: undefined as string | undefined,
  startDate: undefined as string | undefined,
  endDate: undefined as string | undefined,
  managerId: undefined as number | undefined,
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
  managerId: [{ required: true, message: '请选择负责人', trigger: 'change' }],
}

const columns = [
  { title: '项目名称', dataIndex: 'name', width: 200 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '负责人', dataIndex: 'managerName', width: 100 },
  { title: '成员', dataIndex: 'memberCount', width: 80 },
  { title: '进度', dataIndex: 'progress', width: 180 },
  { title: '创建时间', dataIndex: 'createTime', width: 120 },
  { title: '操作', dataIndex: 'action', width: 180, fixed: 'right' as const },
]

const paginationConfig = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: (t: number) => `共 ${t} 个项目`,
  showSizeChanger: true,
  showQuickJumper: true,
}))

function statusText(status: number) {
  const map: Record<number, string> = { 0: '进行中', 1: '已完成', 2: '已归档' }
  return map[status] ?? '未知'
}

function statusColor(status: number) {
  const map: Record<number, string> = { 0: 'processing', 1: 'success', 2: 'default' }
  return map[status] ?? 'default'
}

function formatDate(t: string) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 10)
}

function handleSearch() {
  pageIndex.value = 1
  loadData()
}

function handleTableChange(pagination: any) {
  pageIndex.value = pagination.current
  pageSize.value = pagination.pageSize
  loadData()
}

function goDetail(id: number) {
  router.push({ path: `/task/project/${id}` })
}

// 用户搜索
let searchTimer: ReturnType<typeof setTimeout> | null = null
function handleUserSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      userOptions.value = await get<UserOption[]>('/system/users/search', { keyword })
    } catch { /* ignore */ }
  }, 300)
}

// 编辑项目
function handleEdit(project: Record<string, any>) {
  const p = project as ProjectListDto
  editingProject.value = p
  formState.name = p.name
  formState.description = p.description ?? undefined
  formState.startDate = p.startDate ?? undefined
  formState.endDate = p.endDate ?? undefined
  formState.managerId = p.managerId
  if (p.managerName) {
    userOptions.value = [{ id: p.managerId, name: p.managerName }]
  }
  showCreateModal.value = true
}

// 归档/取消归档
async function handleArchive(project: Record<string, any>) {
  const p = project as ProjectListDto
  try {
    const newStatus = p.status === 2 ? 0 : 2
    await updateProject(p.id, {
      name: p.name,
      description: p.description,
      managerId: p.managerId,
      startDate: p.startDate,
      endDate: p.endDate,
      status: newStatus,
    })
    message.success(newStatus === 2 ? '项目已归档' : '已取消归档')
    loadData()
  } catch {
    message.error('操作失败')
  }
}

function resetForm() {
  editingProject.value = null
  formState.name = ''
  formState.description = ''
  formState.startDate = undefined
  formState.endDate = undefined
  formState.managerId = undefined
  userOptions.value = []
  formRef.value?.resetFields()
}

async function handleCreateSubmit() {
  try {
    await formRef.value?.validateFields()
  } catch { return }
  submitLoading.value = true
  try {
    if (editingProject.value) {
      await updateProject(editingProject.value.id, {
        name: formState.name,
        description: formState.description,
        managerId: formState.managerId!,
        startDate: formState.startDate,
        endDate: formState.endDate,
        status: editingProject.value.status,
      } as UpdateProjectRequest)
      message.success('项目已更新')
    } else {
      await createProject({
        name: formState.name,
        description: formState.description,
        managerId: formState.managerId!,
        startDate: formState.startDate,
        endDate: formState.endDate,
      } as CreateProjectRequest)
      message.success('项目已创建')
    }
    showCreateModal.value = false
    resetForm()
    loadData()
  } catch {
    message.error(editingProject.value ? '更新失败' : '创建失败')
  } finally {
    submitLoading.value = false
  }
}

async function loadData() {
  loading.value = true
  try {
    const res = await getProjects({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: filters.keyword || undefined,
      status: filters.status ?? undefined,
    })
    tableData.value = res.items
    total.value = res.total
  } catch {
    message.error('加载项目列表失败')
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.project-list-page {
  padding: 0 4px;
}

.project-card {
  border-radius: 8px;
  height: 100%;

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;
  }

  &__name {
    font-size: 15px;
    font-weight: 600;
    color: #1a1a1a;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    flex: 1;
    margin-right: 8px;
  }

  &__status {
    flex-shrink: 0;
  }

  &__desc {
    margin-bottom: 12px !important;
    min-height: 44px;
  }

  &__progress {
    margin-bottom: 8px;
  }

  &__task-count {
    font-size: 12px;
    color: #8c8c8c;
  }

  &__info {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 12px;
    color: #8c8c8c;
  }

  &__time {
    margin-left: auto;
  }
}

.project-table {
  border-radius: 8px;
}

.card-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
