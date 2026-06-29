<template>
  <div class="page-container">
    <PageHeader title="异常管理">
      <template #actions>
        <a-button @click="handleExport">
          <template #icon><ExportOutlined /></template>导出
        </a-button>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增异常
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.type" size="small" placeholder="异常类型" allow-clear style="width: 120px" :options="typeOptions" />
          <a-select v-model:value="searchForm.priority" size="small" placeholder="优先级" allow-clear style="width: 100px" :options="priorityOptions" />
          <a-range-picker v-model:value="searchForm.dateRange" size="small" style="width: 240px" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 状态Tab栏 -->
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange" style="margin-bottom: 0">
      <a-tab-pane key="all">
        <template #tab>全部 ({{ statusCount.total }})</template>
      </a-tab-pane>
      <a-tab-pane key="pending">
        <template #tab>
          待处理
          <a-badge :count="statusCount.pending" :overflow-count="99" :offset="[6, -4]" />
        </template>
      </a-tab-pane>
      <a-tab-pane key="processing">
        <template #tab>
          处理中
          <a-badge :count="statusCount.processing" :overflow-count="99" :offset="[6, -4]" />
        </template>
      </a-tab-pane>
      <a-tab-pane key="overdue">
        <template #tab>
          已超时
          <a-badge :count="statusCount.overdue" :overflow-count="99" :offset="[6, -4]" />
        </template>
      </a-tab-pane>
      <a-tab-pane key="closed" tab="已关闭" />
    </a-tabs>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'title'">
            <a @click="handleViewDetail(record)" style="cursor: pointer">{{ record.title }}</a>
          </template>
          <template v-if="column.dataIndex === 'typeText'">
            <a-tag :color="typeColor(record.type)">{{ record.typeText }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'priorityText'">
            <a-tag :color="priorityColor(record.priority)">{{ record.priorityText }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'statusText'">
            <a-tag :color="exStatusColor(record.status)">{{ record.statusText }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'deadline'">
            {{ record.deadline || '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewDetail(record)">查看详情</a-button>
            <a-button
              v-if="record.status === 0"
              type="link"
              size="small"
              @click="handleDispatch(record)"
            >派发</a-button>
            <a-button
              v-if="record.status === 1"
              type="link"
              size="small"
              @click="handleClose(record)"
            >关闭</a-button>
            <a-button
              v-if="record.status === 1"
              type="link"
              size="small"
              @click="handleReassign(record)"
            >转派</a-button>
            <a-popconfirm
              v-if="record.status === 0"
              title="确定删除该异常单吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="formVisible"
      :title="formType === 'add' ? '新增异常' : '编辑异常'"
      width="640px"
      :destroy-on-close="true"
      @cancel="formVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <a-form-item label="标题" name="title">
          <a-input v-model:value="formData.title" placeholder="请输入异常标题" :maxlength="200" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="formData.description" placeholder="请输入异常描述" :rows="4" :maxlength="2000" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="类型" name="type">
              <a-select v-model:value="formData.type" placeholder="请选择类型" :options="typeOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="优先级" name="priority">
              <a-select v-model:value="formData.priority" placeholder="请选择优先级" :options="priorityOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="来源">
              <a-input v-model:value="formData.source" placeholder="选填" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="截止日期">
              <a-date-picker v-model:value="formData.deadline" style="width: 100%" placeholder="选填" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="formVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="detailVisible"
      title="异常详情"
      width="700px"
      :destroy-on-close="true"
    >
      <template v-if="detailData">
        <a-descriptions bordered :column="2" size="small" style="margin-bottom: 24px">
          <a-descriptions-item label="异常编号">{{ detailData.exceptionNo }}</a-descriptions-item>
          <a-descriptions-item label="标题">{{ detailData.title }}</a-descriptions-item>
          <a-descriptions-item label="类型">
            <a-tag :color="typeColor(detailData.type)">{{ detailData.typeText }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="优先级">
            <a-tag :color="priorityColor(detailData.priority)">{{ detailData.priorityText }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="exStatusColor(detailData.status)">{{ detailData.statusText }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="来源">{{ detailData.source || '-' }}</a-descriptions-item>
          <a-descriptions-item label="受派人">{{ detailData.assigneeName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="截止日期">{{ detailData.deadline || '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建人">{{ detailData.creatorName }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ detailData.createTime }}</a-descriptions-item>
          <a-descriptions-item label="更新时间">{{ detailData.updateTime || '-' }}</a-descriptions-item>
          <a-descriptions-item label="关闭时间">{{ detailData.closedTime || '-' }}</a-descriptions-item>
          <a-descriptions-item label="描述" :span="2">{{ detailData.description || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div style="font-weight: 600; margin-bottom: 12px">处理日志</div>
        <a-timeline v-if="detailData.logs && detailData.logs.length > 0">
          <a-timeline-item
            v-for="log in detailData.logs"
            :key="log.id"
            :color="logTimelineColor(log.action)"
          >
            <div><strong>{{ log.action }}</strong> — {{ log.operatorName }}</div>
            <div v-if="log.remark" style="color: #666">{{ log.remark }}</div>
            <div style="color: #999; font-size: 12px">{{ log.createTime }}</div>
          </a-timeline-item>
        </a-timeline>
        <EmptyState v-else size="small" title="暂无处理日志" />
      </template>
    </a-drawer>

    <!-- 派发弹窗 -->
    <a-modal
      v-model:open="dispatchVisible"
      title="派发异常"
      width="480px"
      :destroy-on-close="true"
      @cancel="dispatchVisible = false"
    >
      <a-form
        ref="dispatchFormRef"
        :model="dispatchForm"
        :rules="dispatchRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="受派人" name="assigneeId">
          <a-input-number v-model:value="dispatchForm.assigneeId" placeholder="请输入受派人ID" style="width: 100%" />
        </a-form-item>
        <a-form-item label="派发方式" name="dispatchMethod">
          <a-select v-model:value="dispatchForm.dispatchMethod" placeholder="请选择" :options="dispatchMethodOptions" />
        </a-form-item>
        <a-form-item label="超时时长">
          <a-input-number v-model:value="dispatchForm.timeoutHours" placeholder="小时" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="dispatchForm.remark" placeholder="选填" :rows="3" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dispatchVisible = false">取消</a-button>
        <a-button type="primary" :loading="dispatchLoading" @click="handleDispatchSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 关闭弹窗 -->
    <a-modal
      v-model:open="closeVisible"
      title="关闭异常"
      width="480px"
      :destroy-on-close="true"
      @cancel="closeVisible = false"
    >
      <a-form
        :model="closeForm"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="处理结果">
          <a-textarea v-model:value="closeForm.result" placeholder="请输入处理结果" :rows="3" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="closeForm.remark" placeholder="选填" :rows="3" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="closeVisible = false">取消</a-button>
        <a-button type="primary" :loading="closeLoading" @click="handleCloseSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 转派弹窗 -->
    <a-modal
      v-model:open="reassignVisible"
      title="转派异常"
      width="480px"
      :destroy-on-close="true"
      @cancel="reassignVisible = false"
    >
      <a-form
        ref="reassignFormRef"
        :model="reassignForm"
        :rules="reassignRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="新受派人" name="newAssigneeId">
          <a-input-number v-model:value="reassignForm.newAssigneeId" placeholder="请输入新受派人ID" style="width: 100%" />
        </a-form-item>
        <a-form-item label="转派原因">
          <a-textarea v-model:value="reassignForm.reason" placeholder="选填" :rows="3" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="reassignVisible = false">取消</a-button>
        <a-button type="primary" :loading="reassignLoading" @click="handleReassignSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { Dayjs } from 'dayjs'
import { PlusOutlined, ExportOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getExceptions,
  getExceptionDetail,
  createException,
  updateException,
  deleteException,
  dispatchException,
  closeException,
  reassignException,
  getExceptionCountByStatus,
} from '@/api/quality'

// ========== 枚举选项 ==========
const typeOptions = [
  { label: '数据异常', value: 0 },
  { label: '流程异常', value: 1 },
  { label: '业务异常', value: 2 },
]
const priorityOptions = [
  { label: '低', value: 0 },
  { label: '中', value: 1 },
  { label: '高', value: 2 },
  { label: '紧急', value: 3 },
]
const dispatchMethodOptions = [
  { label: '手动', value: 0 },
  { label: '自动', value: 1 },
  { label: '工作流', value: 2 },
]

function typeColor(t: number) { return ['blue', 'orange', 'red'][t] || 'default' }
function priorityColor(p: number) { return ['default', 'blue', 'orange', 'red'][p] || 'default' }
function exStatusColor(s: number) { return ['warning', 'processing', 'error', 'success'][s] || 'default' }
function logTimelineColor(action: string) {
  if (action.includes('关闭')) return 'green'
  if (action.includes('超时')) return 'red'
  if (action.includes('派发') || action.includes('转派')) return 'blue'
  return 'gray'
}

// ========== Tab & 状态统计 ==========
const activeTab = ref('all')
const statusCount = reactive({ total: 0, pending: 0, processing: 0, overdue: 0, closed: 0 })

function handleTabChange(key: string | number) {
  activeTab.value = String(key)
  pagination.pageIndex = 1
  fetchList()
}

async function fetchStatusCount() {
  try {
    const res = await getExceptionCountByStatus() as any
    if (res) {
      statusCount.total = res.total ?? 0
      statusCount.pending = res.pending ?? 0
      statusCount.processing = res.processing ?? 0
      statusCount.overdue = res.overdue ?? 0
      statusCount.closed = res.closed ?? 0
    }
  } catch (e) { console.error(e) }
}

// ========== 搜索 ==========
const searchForm = reactive({
  type: undefined as number | undefined,
  priority: undefined as number | undefined,
  dateRange: undefined as [Dayjs, Dayjs] | undefined,
})

function handleSearch() { pagination.pageIndex = 1; fetchList() }

function handleReset() {
  searchForm.type = undefined
  searchForm.priority = undefined
  searchForm.dateRange = undefined
  pagination.pageIndex = 1
  fetchList()
}

// ========== 表格 ==========
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '异常编号', dataIndex: 'exceptionNo', key: 'exceptionNo', width: 140 },
  { title: '标题', dataIndex: 'title', key: 'title', width: 200, ellipsis: true },
  { title: '类型', dataIndex: 'typeText', key: 'typeText', width: 100, align: 'center' as const },
  { title: '优先级', dataIndex: 'priorityText', key: 'priorityText', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'statusText', key: 'statusText', width: 90, align: 'center' as const },
  { title: '来源', dataIndex: 'source', key: 'source', width: 100, ellipsis: true },
  { title: '受派人', dataIndex: 'assigneeName', key: 'assigneeName', width: 90 },
  { title: '截止日期', dataIndex: 'deadline', key: 'deadline', width: 110 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 260, align: 'center' as const, fixed: 'right' as const },
]

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    // Tab筛选
    const tabStatusMap: Record<string, number> = { pending: 0, processing: 1, overdue: 2, closed: 3 }
    if (activeTab.value !== 'all') {
      params.status = tabStatusMap[activeTab.value]
    }
    if (searchForm.type !== undefined) params.type = searchForm.type
    if (searchForm.priority !== undefined) params.priority = searchForm.priority
    if (searchForm.dateRange) {
      params.startDate = searchForm.dateRange[0].format('YYYY-MM-DD')
      params.endDate = searchForm.dateRange[1].format('YYYY-MM-DD')
    }
    const res = await getExceptions(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total ?? res?.length ?? 0
    }
  } finally {
    loading.value = false
  }
}

function handleRefresh() {
  fetchList()
  fetchStatusCount()
}

function handleExport() {
  message.info('导出功能开发中')
}

// ========== 新增/编辑表单 ==========
const formVisible = ref(false)
const formType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  title: '',
  description: '',
  type: undefined as number | undefined,
  priority: undefined as number | undefined,
  source: '',
  deadline: undefined as Dayjs | undefined,
})

const formRules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入异常标题', trigger: 'blur' }],
  description: [{ required: true, message: '请输入异常描述', trigger: 'blur' }],
  type: [{ required: true, message: '请选择异常类型', trigger: 'change' }],
  priority: [{ required: true, message: '请选择优先级', trigger: 'change' }],
}

function resetForm() {
  formData.title = ''
  formData.description = ''
  formData.type = undefined
  formData.priority = undefined
  formData.source = ''
  formData.deadline = undefined
}

function handleAdd() {
  formType.value = 'add'
  currentId.value = null
  resetForm()
  formVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload: any = {
      title: formData.title,
      description: formData.description,
      type: formData.type,
      priority: formData.priority,
      source: formData.source || undefined,
      deadline: formData.deadline?.format('YYYY-MM-DD') || undefined,
    }
    if (formType.value === 'add') {
      await createException(payload)
      message.success('新增成功')
    } else {
      await updateException(currentId.value!, payload)
      message.success('更新成功')
    }
    formVisible.value = false
    fetchList()
    fetchStatusCount()
  } finally { submitLoading.value = false }
}

// ========== 删除 ==========
async function handleDelete(record: any) {
  try {
    await deleteException(record.id)
    message.success('删除成功')
    fetchList()
    fetchStatusCount()
  } catch (e) { console.error('删除失败:', e) }
}

// ========== 详情抽屉 ==========
const detailVisible = ref(false)
const detailData = ref<any>(null)

async function handleViewDetail(record: any) {
  detailVisible.value = true
  detailData.value = null
  try {
    const res = await getExceptionDetail(record.id) as any
    detailData.value = res
  } catch (e) { console.error('获取详情失败:', e) }
}

// ========== 派发 ==========
const dispatchVisible = ref(false)
const dispatchFormRef = ref<FormInstance>()
const dispatchLoading = ref(false)
const dispatchTargetId = ref<number | null>(null)

const dispatchForm = reactive({
  assigneeId: undefined as number | undefined,
  dispatchMethod: 0,
  timeoutHours: undefined as number | undefined,
  remark: '',
})

const dispatchRules: Record<string, Rule[]> = {
  assigneeId: [{ required: true, message: '请输入受派人ID', trigger: 'blur' }],
  dispatchMethod: [{ required: true, message: '请选择派发方式', trigger: 'change' }],
}

function handleDispatch(record: any) {
  dispatchTargetId.value = record.id
  dispatchForm.assigneeId = undefined
  dispatchForm.dispatchMethod = 0
  dispatchForm.timeoutHours = undefined
  dispatchForm.remark = ''
  dispatchVisible.value = true
}

async function handleDispatchSubmit() {
  if (!dispatchFormRef.value) return
  try { await dispatchFormRef.value.validate() } catch { return }
  dispatchLoading.value = true
  try {
    await dispatchException(dispatchTargetId.value!, {
      assigneeId: dispatchForm.assigneeId,
      dispatchMethod: dispatchForm.dispatchMethod,
      timeoutHours: dispatchForm.timeoutHours || undefined,
      remark: dispatchForm.remark || undefined,
    })
    message.success('派发成功')
    dispatchVisible.value = false
    fetchList()
    fetchStatusCount()
  } finally { dispatchLoading.value = false }
}

// ========== 关闭 ==========
const closeVisible = ref(false)
const closeLoading = ref(false)
const closeTargetId = ref<number | null>(null)

const closeForm = reactive({ result: '', remark: '' })

function handleClose(record: any) {
  Modal.confirm({
    title: '确认关闭',
    content: '确定要关闭该异常单吗？',
    okText: '确定',
    cancelText: '取消',
    onOk() {
      closeTargetId.value = record.id
      closeForm.result = ''
      closeForm.remark = ''
      closeVisible.value = true
    },
  })
}

async function handleCloseSubmit() {
  closeLoading.value = true
  try {
    await closeException(closeTargetId.value!, {
      result: closeForm.result || undefined,
      remark: closeForm.remark || undefined,
    })
    message.success('关闭成功')
    closeVisible.value = false
    fetchList()
    fetchStatusCount()
  } finally { closeLoading.value = false }
}

// ========== 转派 ==========
const reassignVisible = ref(false)
const reassignFormRef = ref<FormInstance>()
const reassignLoading = ref(false)
const reassignTargetId = ref<number | null>(null)

const reassignForm = reactive({
  newAssigneeId: undefined as number | undefined,
  reason: '',
})

const reassignRules: Record<string, Rule[]> = {
  newAssigneeId: [{ required: true, message: '请输入新受派人ID', trigger: 'blur' }],
}

function handleReassign(record: any) {
  reassignTargetId.value = record.id
  reassignForm.newAssigneeId = undefined
  reassignForm.reason = ''
  reassignVisible.value = true
}

async function handleReassignSubmit() {
  if (!reassignFormRef.value) return
  try { await reassignFormRef.value.validate() } catch { return }
  reassignLoading.value = true
  try {
    await reassignException(reassignTargetId.value!, {
      newAssigneeId: reassignForm.newAssigneeId,
      reason: reassignForm.reason || undefined,
    })
    message.success('转派成功')
    reassignVisible.value = false
    fetchList()
    fetchStatusCount()
  } finally { reassignLoading.value = false }
}

// ========== 初始化 ==========
onMounted(() => {
  fetchList()
  fetchStatusCount()
})
</script>
