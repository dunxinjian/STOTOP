<template>
  <div class="page-container">
    <PageHeader title="服务工单">
      <template #actions>
        <a-button v-if="has(CrmPermissions.OrderCreate)" type="primary" @click="handleCreate">
          <template #icon><PlusOutlined /></template>新建工单
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; width:100%; justify-content:space-between;">
          <div style="display:flex; align-items:center; gap:4px;">
            <span class="tab-item" :class="{ active: filterStatus === '' }" @click="filterStatus = ''; onStatusChange()">全部({{ statusCounts.total }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 0 }" @click="filterStatus = 0; onStatusChange()">待接单({{ statusCounts.pending }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 1 }" @click="filterStatus = 1; onStatusChange()">处理中({{ statusCounts.processing }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 2 }" @click="filterStatus = 2; onStatusChange()">待确认({{ statusCounts.waitingConfirm }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 3 }" @click="filterStatus = 3; onStatusChange()">已完成({{ statusCounts.completed }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 4 }" @click="filterStatus = 4; onStatusChange()">已关闭({{ statusCounts.closed }})</span>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-input v-model:value="searchForm.keyword" placeholder="工单号" allow-clear size="small" style="width: 140px" @keyup.enter="handleSearch" />
            <a-input v-model:value="searchForm.customerName" placeholder="客户名称" allow-clear size="small" style="width: 140px" @keyup.enter="handleSearch" />
            <a-select v-model:value="searchForm.category" placeholder="分类" allow-clear size="small" style="width: 100px" :options="categoryOptions" />
            <a-select v-model:value="searchForm.priority" placeholder="优先级" allow-clear size="small" style="width: 100px" :options="priorityOptions" />
            <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
            <a-button size="small" @click="handleReset">重置</a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'title'">
            <a-tooltip :title="record.title">{{ record.title }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'category'">
            <a-tag>{{ getCategoryLabel(record.category) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'priority'">
            <a-tag :color="getPriorityColor(record.priority)">{{ getPriorityLabel(record.priority) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">{{ getStatusLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
            <a-button v-if="record.status === 0" type="link" size="small" @click="handleAccept(record)">接单</a-button>
            <a-popconfirm title="确定关闭该工单吗？" ok-text="确定" cancel-text="取消" @confirm="handleClose(record)">
              <a-button v-if="record.status !== 3 && record.status !== 4" type="link" size="small" danger>关闭</a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无工单数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新建工单弹窗 -->
    <a-modal v-model:open="createVisible" title="新建工单" width="700px" :destroy-on-close="true" @cancel="createVisible = false">
      <a-form ref="createFormRef" :model="createForm" :rules="createRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="客户" name="customerId">
          <a-select v-model:value="createForm.customerId" placeholder="请搜索选择客户" show-search :filter-option="false" @search="handleCustomerSearch" style="width: 100%">
            <a-select-option v-for="c in customerOptions" :key="c.id" :value="c.id">{{ c.shortName }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="标题" name="title">
          <a-input v-model:value="createForm.title" placeholder="请输入工单标题" :maxlength="200" />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-select v-model:value="createForm.category" placeholder="请选择分类" :options="categoryOptions" />
        </a-form-item>
        <a-form-item label="优先级" name="priority">
          <a-select v-model:value="createForm.priority" placeholder="请选择优先级" :options="priorityOptions" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="createForm.description" :rows="4" placeholder="请输入工单描述" :maxlength="2000" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="createVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleCreateSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 工单详情抽屉 -->
    <a-drawer v-model:open="detailVisible" title="工单详情" :width="700" :destroy-on-close="true">
      <template v-if="currentDetail">
        <div class="detail-header">
          <div class="detail-header-row">
            <span class="detail-order-no">{{ currentDetail.orderNo }}</span>
            <a-tag :color="getStatusColor(currentDetail.status)">{{ getStatusLabel(currentDetail.status) }}</a-tag>
            <a-tag :color="getPriorityColor(currentDetail.priority)">{{ getPriorityLabel(currentDetail.priority) }}</a-tag>
          </div>
        </div>

        <a-descriptions :column="2" bordered size="small" style="margin-bottom: 24px">
          <a-descriptions-item label="客户">{{ currentDetail.customerName }}</a-descriptions-item>
          <a-descriptions-item label="标题">{{ currentDetail.title }}</a-descriptions-item>
          <a-descriptions-item label="分类">{{ getCategoryLabel(currentDetail.category) }}</a-descriptions-item>
          <a-descriptions-item label="受理人">{{ currentDetail.updaterName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建人">{{ currentDetail.creatorName }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ currentDetail.createdTime }}</a-descriptions-item>
          <a-descriptions-item label="描述" :span="2">{{ currentDetail.description || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div class="section-title">处理时间线</div>
        <a-timeline v-if="currentDetail.logs && currentDetail.logs.length > 0" style="margin-top: 16px">
          <a-timeline-item v-for="log in currentDetail.logs" :key="log.id" :color="getLogColor(log.operationType)">
            <div class="timeline-item">
              <div class="timeline-header">
                <span class="timeline-operator">{{ log.creatorName }}</span>
                <a-tag size="small">{{ getOperationLabel(log.operationType) }}</a-tag>
                <span class="timeline-time">{{ log.createdTime }}</span>
              </div>
              <div v-if="log.content" class="timeline-content">{{ log.content }}</div>
            </div>
          </a-timeline-item>
        </a-timeline>
        <a-empty v-else description="暂无操作记录" :image="null" />

        <div class="detail-actions">
          <a-button v-if="currentDetail.status === 0" type="primary" @click="handleAccept(currentDetail)">接单</a-button>
          <a-button v-if="currentDetail.status === 1" type="primary" @click="progressVisible = true">更新进展</a-button>
          <a-button v-if="currentDetail.status === 1" @click="transferVisible = true">转派</a-button>
          <a-popconfirm v-if="currentDetail.status !== 3 && currentDetail.status !== 4" title="确定关闭该工单？" @confirm="handleClose(currentDetail)">
            <a-button danger>关闭工单</a-button>
          </a-popconfirm>
        </div>
      </template>
    </a-drawer>

    <!-- 更新进展弹窗 -->
    <a-modal v-model:open="progressVisible" title="更新进展" width="500px" @ok="handleUpdateProgress" :confirm-loading="actionLoading">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px">
        <a-form-item label="内容">
          <a-textarea v-model:value="progressContent" :rows="4" placeholder="请输入处理进展内容" :maxlength="1000" show-count />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 转派弹窗 -->
    <a-modal v-model:open="transferVisible" title="转派工单" width="500px" @ok="handleTransfer" :confirm-loading="actionLoading">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px">
        <a-form-item label="转派给">
          <a-select v-model:value="transferToId" placeholder="请选择受理人" style="width: 100%" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="transferContent" :rows="3" placeholder="请输入转派原因" :maxlength="500" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { usePermission, CrmPermissions } from '@/utils/permission'
import {
  getServiceOrderList,
  getServiceOrderById,
  createServiceOrder,
  executeServiceOrderAction,
  getServiceOrderStatistics,
  getCustomerList,
  type ServiceOrderListItemDto,
  type ServiceOrderDto,
  type ServiceOrderStatisticsDto,
  type CustomerListItemDto,
} from '@/api/crm'

const { has } = usePermission()

// 选项定义
const categoryOptions = [
  { label: '故障', value: 3 },
  { label: '咨询', value: 1 },
  { label: '维护', value: 4 },
  { label: '投诉', value: 2 },
]

const priorityOptions = [
  { label: '紧急', value: 1 },
  { label: '高', value: 2 },
  { label: '中', value: 3 },
  { label: '低', value: 4 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '工单号', dataIndex: 'orderNo', key: 'orderNo', width: 140 },
  { title: '客户名称', dataIndex: 'customerName', key: 'customerName', width: 150, ellipsis: true },
  { title: '标题', dataIndex: 'title', key: 'title', width: 200, ellipsis: true },
  { title: '分类', dataIndex: 'category', key: 'category', width: 90, align: 'center' as const },
  { title: '优先级', dataIndex: 'priority', key: 'priority', width: 90, align: 'center' as const },
  { title: '受理人', dataIndex: 'creatorName', key: 'creatorName', width: 100 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 170 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

// 搜索
const searchForm = reactive({
  keyword: '',
  customerName: '',
  category: undefined as number | undefined,
  priority: undefined as number | undefined,
})

// 状态过滤
const filterStatus = ref<number | ''>('')

// 状态计数
const statusCounts = reactive<ServiceOrderStatisticsDto & { total: number }>({
  total: 0,
  pending: 0,
  processing: 0,
  waitingConfirm: 0,
  completed: 0,
  closed: 0,
})

// 表格
const loading = ref(false)
const tableData = ref<ServiceOrderListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

// 新建工单
const createVisible = ref(false)
const createFormRef = ref<FormInstance>()
const submitLoading = ref(false)
const customerOptions = ref<CustomerListItemDto[]>([])

const createForm = reactive({
  customerId: undefined as number | undefined,
  title: '',
  category: undefined as number | undefined,
  priority: 3 as number,
  description: '',
})

const createRules: Record<string, Rule[]> = {
  customerId: [{ required: true, message: '请选择客户', trigger: 'change' }],
  title: [{ required: true, message: '请输入工单标题', trigger: 'blur' }],
  category: [{ required: true, message: '请选择分类', trigger: 'change' }],
}

// 工单详情
const detailVisible = ref(false)
const currentDetail = ref<ServiceOrderDto | null>(null)

// 更新进展
const progressVisible = ref(false)
const progressContent = ref('')
const actionLoading = ref(false)

// 转派
const transferVisible = ref(false)
const transferToId = ref<number | undefined>(undefined)
const transferContent = ref('')

// 辅助方法
function getCategoryLabel(v: number) {
  const map: Record<number, string> = { 1: '咨询', 2: '投诉', 3: '故障', 4: '需求', 5: '其他' }
  return map[v] || '未知'
}

function getPriorityLabel(v: number) {
  const map: Record<number, string> = { 1: '紧急', 2: '高', 3: '中', 4: '低' }
  return map[v] || '-'
}

function getPriorityColor(v: number) {
  const map: Record<number, string> = { 1: 'red', 2: 'orange', 3: 'blue', 4: 'default' }
  return map[v] || 'default'
}

function getStatusLabel(v: number) {
  const map: Record<number, string> = { 0: '待接单', 1: '处理中', 2: '待确认', 3: '已完成', 4: '已关闭' }
  return map[v] || '未知'
}

function getStatusColor(v: number) {
  const map: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'warning', 3: 'success', 4: 'error' }
  return map[v] || 'default'
}

function getOperationLabel(v: number) {
  const map: Record<number, string> = { 1: '接单', 2: '处理', 3: '转派', 4: '关闭' }
  return map[v] || '操作'
}

function getLogColor(v: number) {
  const map: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'orange', 4: 'red' }
  return map[v] || 'blue'
}

// 数据加载
async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.category !== undefined) params.category = searchForm.category
    if (searchForm.priority !== undefined) params.priority = searchForm.priority
    if (filterStatus.value !== '') params.status = filterStatus.value
    const res = await getServiceOrderList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

async function fetchStatistics() {
  try {
    const res = await getServiceOrderStatistics() as any
    if (res) {
      statusCounts.total = (res.pending || 0) + (res.processing || 0) + (res.waitingConfirm || 0) + (res.completed || 0) + (res.closed || 0)
      statusCounts.pending = res.pending || 0
      statusCounts.processing = res.processing || 0
      statusCounts.waitingConfirm = res.waitingConfirm || 0
      statusCounts.completed = res.completed || 0
      statusCounts.closed = res.closed || 0
    }
  } catch { /* ignore */ }
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.customerName = ''
  searchForm.category = undefined
  searchForm.priority = undefined
  pagination.pageIndex = 1
  fetchList()
}

function onStatusChange() {
  pagination.pageIndex = 1
  fetchList()
}

// 客户搜索
async function handleCustomerSearch(val: string) {
  if (!val || val.length < 1) return
  try {
    const res = await getCustomerList({ keyword: val, pageSize: 20 }) as any
    customerOptions.value = res?.items || res || []
  } catch { /* ignore */ }
}

// 新建
function handleCreate() {
  createForm.customerId = undefined
  createForm.title = ''
  createForm.category = undefined
  createForm.priority = 3
  createForm.description = ''
  createVisible.value = true
}

async function handleCreateSubmit() {
  if (!createFormRef.value) return
  try { await createFormRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    await createServiceOrder({
      customerId: createForm.customerId!,
      title: createForm.title,
      category: createForm.category!,
      priority: createForm.priority,
      description: createForm.description || undefined,
    })
    message.success('创建成功')
    createVisible.value = false
    fetchList()
    fetchStatistics()
  } finally {
    submitLoading.value = false
  }
}

// 查看详情
async function handleView(record: any) {
  try {
    const res = await getServiceOrderById(record.id) as any
    currentDetail.value = res
    detailVisible.value = true
  } catch {
    message.error('获取工单详情失败')
  }
}

// 接单
async function handleAccept(record: any) {
  actionLoading.value = true
  try {
    await executeServiceOrderAction(record.id, { operatorId: 0, operationType: 1, content: '接单处理' })
    message.success('接单成功')
    fetchList()
    fetchStatistics()
    if (detailVisible.value && currentDetail.value) {
      handleView(currentDetail.value)
    }
  } finally {
    actionLoading.value = false
  }
}

// 关闭
async function handleClose(record: any) {
  try {
    await executeServiceOrderAction(record.id, { operatorId: 0, operationType: 4, content: '关闭工单' })
    message.success('工单已关闭')
    fetchList()
    fetchStatistics()
    if (detailVisible.value) detailVisible.value = false
  } catch {
    message.error('关闭失败')
  }
}

// 更新进展
async function handleUpdateProgress() {
  if (!progressContent.value.trim()) {
    message.warning('请输入进展内容')
    return
  }
  actionLoading.value = true
  try {
    await executeServiceOrderAction(currentDetail.value!.id, { operatorId: 0, operationType: 2, content: progressContent.value })
    message.success('进展已更新')
    progressVisible.value = false
    progressContent.value = ''
    handleView(currentDetail.value!)
  } finally {
    actionLoading.value = false
  }
}

// 转派
async function handleTransfer() {
  if (!transferToId.value) {
    message.warning('请选择转派人')
    return
  }
  actionLoading.value = true
  try {
    await executeServiceOrderAction(currentDetail.value!.id, {
      operatorId: 0,
      operationType: 3,
      content: transferContent.value || undefined,
      transferToId: transferToId.value,
    })
    message.success('转派成功')
    transferVisible.value = false
    transferToId.value = undefined
    transferContent.value = ''
    handleView(currentDetail.value!)
    fetchList()
  } finally {
    actionLoading.value = false
  }
}

onMounted(() => {
  fetchList()
  fetchStatistics()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.tab-item {
  padding: 6px 16px;
  font-size: 14px;
  color: #606266;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
  background: transparent;

  &:hover { color: var(--color-primary); }
  &.active { color: var(--text-1); background: var(--bg-card); font-weight: 500; box-shadow: 0 1px 2px rgba(18, 31, 53, 0.08); }
}

.detail-header {
  margin-bottom: 20px;

  .detail-header-row {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .detail-order-no {
    font-size: 18px;
    font-weight: 600;
    color: $text-primary;
  }
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
}

.timeline-item {
  .timeline-header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 4px;
  }

  .timeline-operator {
    font-weight: 500;
    color: $text-primary;
  }

  .timeline-time {
    font-size: 12px;
    color: #909399;
  }

  .timeline-content {
    color: #606266;
    font-size: 13px;
    line-height: 1.6;
  }
}

.detail-actions {
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid $border-color-lighter;
  display: flex;
  gap: 12px;
}
</style>
