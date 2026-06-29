<template>
  <div class="page-container">
    <PageHeader title="改进跟踪" />

    <!-- Tab筛选 -->
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange" style="margin-bottom: 0">
      <a-tab-pane key="all" tab="全部" />
      <a-tab-pane key="pending">
        <template #tab>
          待执行
          <a-badge :count="pendingCount" :overflow-count="99" :offset="[6, -4]" />
        </template>
      </a-tab-pane>
      <a-tab-pane key="completed" tab="已完成" />
      <a-tab-pane key="overdue">
        <template #tab>
          已逾期
          <a-badge :count="overdueCount" :overflow-count="99" :offset="[6, -4]" />
        </template>
      </a-tab-pane>
    </a-tabs>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="filteredData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        :row-class-name="rowClassName"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'content'">
            <span :title="record.content">{{ record.content }}</span>
          </template>
          <template v-if="column.dataIndex === 'reviewTitle'">
            {{ record.reviewTitle || '-' }}
          </template>
          <template v-if="column.dataIndex === 'assigneeName'">
            {{ record.assigneeName || '-' }}
          </template>
          <template v-if="column.dataIndex === 'deadline'">
            <span :style="isOverdue(record) ? { color: 'var(--color-danger)', fontWeight: 600 } : {}">
              {{ record.deadline || '-' }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag v-if="record.completed" color="success">已完成</a-tag>
            <a-tag v-else-if="isOverdue(record)" color="error">已逾期</a-tag>
            <a-tag v-else color="warning">待执行</a-tag>
          </template>
          <template v-if="column.dataIndex === 'completedTime'">
            {{ record.completedTime || '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <template v-if="!record.completed">
              <a-button type="link" size="small" @click="handleComplete(record)">标记完成</a-button>
              <a-button type="link" size="small" @click="handleEditImprovement(record)">编辑</a-button>
            </template>
            <span v-else style="color: #999">-</span>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 标记完成弹窗 -->
    <a-modal
      v-model:open="completeVisible"
      title="标记完成"
      width="480px"
      :destroy-on-close="true"
      @cancel="completeVisible = false"
    >
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="效果描述">
          <a-textarea v-model:value="completeForm.effectDescription" placeholder="请描述改进效果（选填）" :rows="4" :maxlength="1000" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="completeVisible = false">取消</a-button>
        <a-button type="primary" :loading="completeLoading" @click="handleCompleteSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 编辑改进弹窗 -->
    <a-modal
      v-model:open="editVisible"
      title="编辑改进措施"
      width="480px"
      :destroy-on-close="true"
      @cancel="editVisible = false"
    >
      <a-form
        ref="editFormRef"
        :model="editForm"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="改进内容">
          <a-textarea v-model:value="editForm.content" placeholder="请输入改进内容" :rows="3" :maxlength="1000" />
        </a-form-item>
        <a-form-item label="负责人ID">
          <a-input-number v-model:value="editForm.assigneeId" placeholder="选填" style="width: 100%" :min="1" />
        </a-form-item>
        <a-form-item label="截止日期">
          <a-date-picker v-model:value="editForm.deadline" style="width: 100%" placeholder="选填" value-format="YYYY-MM-DD" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="editVisible = false">取消</a-button>
        <a-button type="primary" :loading="editLoading" @click="handleEditSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getImprovementList,
  updateImprovement,
  completeImprovement,
} from '@/api/quality'

// ========== Tab ==========
const activeTab = ref('all')
const pendingCount = ref(0)
const overdueCount = ref(0)

function handleTabChange() {
  pagination.pageIndex = 1
  fetchList()
}

function isOverdue(record: any): boolean {
  return !record.completed && record.deadline && new Date(record.deadline) < new Date()
}

// ========== 列表 ==========
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const filteredData = computed(() => {
  if (activeTab.value === 'overdue') {
    return tableData.value.filter(item => isOverdue(item))
  }
  return tableData.value
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: activeTab.value === 'overdue' ? filteredData.value.length : pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '改进内容', dataIndex: 'content', key: 'content', width: 240, ellipsis: true },
  { title: '所属复盘', dataIndex: 'reviewTitle', key: 'reviewTitle', width: 180, ellipsis: true },
  { title: '负责人', dataIndex: 'assigneeName', key: 'assigneeName', width: 100 },
  { title: '截止日期', dataIndex: 'deadline', key: 'deadline', width: 120 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '完成时间', dataIndex: 'completedTime', key: 'completedTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 160, align: 'center' as const, fixed: 'right' as const },
]

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function rowClassName(record: any) {
  if (isOverdue(record)) return 'row-overdue'
  return ''
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (activeTab.value === 'pending' || activeTab.value === 'overdue') {
      params.completed = false
    } else if (activeTab.value === 'completed') {
      params.completed = true
    }
    const res = await getImprovementList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total ?? res?.length ?? 0
      // 更新badge数
      if (activeTab.value === 'all') {
        pendingCount.value = tableData.value.filter((i: any) => !i.completed).length
        overdueCount.value = tableData.value.filter((i: any) => isOverdue(i)).length
      }
    }
  } finally {
    loading.value = false
  }
}

// ========== 标记完成 ==========
const completeVisible = ref(false)
const completeLoading = ref(false)
const completeTargetId = ref<number | null>(null)
const completeForm = reactive({ effectDescription: '' })

function handleComplete(record: any) {
  completeTargetId.value = record.id
  completeForm.effectDescription = ''
  completeVisible.value = true
}

async function handleCompleteSubmit() {
  completeLoading.value = true
  try {
    await completeImprovement(completeTargetId.value!, {
      effectDescription: completeForm.effectDescription || undefined,
    })
    message.success('已标记完成')
    completeVisible.value = false
    fetchList()
  } finally { completeLoading.value = false }
}

// ========== 编辑改进 ==========
const editVisible = ref(false)
const editLoading = ref(false)
const editTargetId = ref<number | null>(null)
const editForm = reactive({
  content: '',
  assigneeId: undefined as number | undefined,
  deadline: undefined as string | undefined,
})

function handleEditImprovement(record: any) {
  editTargetId.value = record.id
  editForm.content = record.content || ''
  editForm.assigneeId = record.assigneeId || undefined
  editForm.deadline = record.deadline || undefined
  editVisible.value = true
}

async function handleEditSubmit() {
  editLoading.value = true
  try {
    await updateImprovement(editTargetId.value!, {
      content: editForm.content || undefined,
      assigneeId: editForm.assigneeId || undefined,
      deadline: editForm.deadline || undefined,
    })
    message.success('更新成功')
    editVisible.value = false
    fetchList()
  } finally { editLoading.value = false }
}

// ========== 初始化 ==========
onMounted(() => {
  fetchList()
})
</script>

<style scoped>
:deep(.row-overdue td) {
  background-color: var(--color-danger-light) !important;
}
</style>
