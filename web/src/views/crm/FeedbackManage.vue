<template>
  <div class="page-container">
    <PageHeader title="服务反馈">
      <template #actions>
        <a-button v-if="has(CrmPermissions.FeedbackCreate)" type="primary" @click="handleCreate">
          <template #icon><PlusOutlined /></template>提交反馈
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; width:100%; justify-content:space-between;">
          <div style="display:flex; align-items:center; gap:4px;">
            <span class="tab-item" :class="{ active: filterStatus === '' }" @click="filterStatus = ''; onStatusChange()">全部({{ statusCounts.total }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 0 }" @click="filterStatus = 0; onStatusChange()">待审阅({{ statusCounts.pending }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 1 }" @click="filterStatus = 1; onStatusChange()">已受理({{ statusCounts.accepted }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 2 }" @click="filterStatus = 2; onStatusChange()">改善中({{ statusCounts.improving }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 3 }" @click="filterStatus = 3; onStatusChange()">已落实({{ statusCounts.implemented }})</span>
            <span class="tab-item" :class="{ active: filterStatus === 4 }" @click="filterStatus = 4; onStatusChange()">已驳回({{ statusCounts.rejected }})</span>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-input v-model:value="searchForm.keyword" placeholder="标题关键词" allow-clear size="small" style="width: 160px" @keyup.enter="handleSearch" />
            <a-select v-model:value="searchForm.category" placeholder="分类" allow-clear size="small" style="width: 120px" :options="categoryOptions" />
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
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">{{ getStatusLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
            <a-button v-if="has(CrmPermissions.FeedbackHandle) && record.status < 3" type="link" size="small" @click="handleProcess(record)">处理</a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无反馈数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 提交反馈弹窗 -->
    <a-modal v-model:open="createVisible" title="提交反馈" width="700px" :destroy-on-close="true" @cancel="createVisible = false">
      <a-form ref="createFormRef" :model="createForm" :rules="createRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="标题" name="title">
          <a-input v-model:value="createForm.title" placeholder="请输入反馈标题" :maxlength="200" />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-select v-model:value="createForm.category" placeholder="请选择分类" :options="categoryOptions" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="createForm.description" :rows="4" placeholder="请详细描述问题" :maxlength="2000" show-count />
        </a-form-item>
        <a-form-item label="改善建议">
          <a-textarea v-model:value="createForm.suggestion" :rows="3" placeholder="请输入改善建议（可选）" :maxlength="1000" show-count />
        </a-form-item>
        <a-form-item label="关联客户">
          <a-select v-model:value="createForm.customerId" placeholder="请搜索选择客户（可选）" show-search :filter-option="false" @search="handleCustomerSearch" allow-clear style="width: 100%">
            <a-select-option v-for="c in customerOptions" :key="c.id" :value="c.id">{{ c.shortName }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="关联工单">
          <a-select v-model:value="createForm.orderId" placeholder="请搜索选择工单（可选）" show-search :filter-option="false" @search="handleOrderSearch" allow-clear style="width: 100%">
            <a-select-option v-for="o in orderOptions" :key="o.id" :value="o.id">{{ o.orderNo }} - {{ o.title }}</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="createVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleCreateSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 反馈详情抽屉 -->
    <a-drawer v-model:open="detailVisible" title="反馈详情" :width="600" :destroy-on-close="true">
      <template v-if="currentDetail">
        <a-descriptions :column="2" bordered size="small" style="margin-bottom: 24px">
          <a-descriptions-item label="标题" :span="2">{{ currentDetail.title }}</a-descriptions-item>
          <a-descriptions-item label="分类">{{ getCategoryLabel(currentDetail.category) }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="getStatusColor(currentDetail.status)">{{ getStatusLabel(currentDetail.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="提交人">{{ currentDetail.creatorName }}</a-descriptions-item>
          <a-descriptions-item label="提交时间">{{ currentDetail.createdTime }}</a-descriptions-item>
          <a-descriptions-item label="描述" :span="2">{{ currentDetail.description || '-' }}</a-descriptions-item>
          <a-descriptions-item label="改善建议" :span="2">{{ currentDetail.suggestion || '-' }}</a-descriptions-item>
          <a-descriptions-item label="处理结果" :span="2">{{ currentDetail.handleResult || '-' }}</a-descriptions-item>
        </a-descriptions>

        <!-- 处理区域 -->
        <template v-if="has(CrmPermissions.FeedbackHandle) && currentDetail.status < 3">
          <div class="section-title">处理反馈</div>
          <a-form :label-col="{ style: { width: '100px' } }" style="margin-top: 16px">
            <a-form-item label="处理状态">
              <a-select v-model:value="handleForm.newStatus" placeholder="请选择状态" style="width: 200px">
                <a-select-option :value="1">已受理</a-select-option>
                <a-select-option :value="2">改善中</a-select-option>
                <a-select-option :value="3">已落实</a-select-option>
                <a-select-option :value="4">已驳回</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item label="处理结果">
              <a-textarea v-model:value="handleForm.handleResult" :rows="4" placeholder="请输入处理结果" :maxlength="2000" show-count />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" :loading="handleLoading" @click="handleSubmitProcess">提交处理</a-button>
            </a-form-item>
          </a-form>
        </template>
      </template>
    </a-drawer>
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
  getServiceFeedbackList,
  getServiceFeedbackById,
  createServiceFeedback,
  handleServiceFeedback,
  getCustomerList,
  getServiceOrderList,
  type ServiceFeedbackListItemDto,
  type ServiceFeedbackDto,
  type CustomerListItemDto,
  type ServiceOrderListItemDto,
} from '@/api/crm'

const { has } = usePermission()

// 分类选项
const categoryOptions = [
  { label: '流程问题', value: 1 },
  { label: '时效问题', value: 2 },
  { label: '服务质量', value: 3 },
  { label: '系统问题', value: 4 },
  { label: '其他', value: 5 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '标题', dataIndex: 'title', key: 'title', width: 200, ellipsis: true },
  { title: '分类', dataIndex: 'category', key: 'category', width: 100, align: 'center' as const },
  { title: '提交人', dataIndex: 'creatorName', key: 'creatorName', width: 100 },
  { title: '客户', dataIndex: 'customerName', key: 'customerName', width: 150, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '提交时间', dataIndex: 'createdTime', key: 'createdTime', width: 170 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

// 搜索
const searchForm = reactive({
  keyword: '',
  category: undefined as number | undefined,
})

// 状态过滤
const filterStatus = ref<number | ''>('')

// 状态计数
const statusCounts = reactive({
  total: 0,
  pending: 0,
  accepted: 0,
  improving: 0,
  implemented: 0,
  rejected: 0,
})

// 表格
const loading = ref(false)
const tableData = ref<ServiceFeedbackListItemDto[]>([])
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

// 新建反馈
const createVisible = ref(false)
const createFormRef = ref<FormInstance>()
const submitLoading = ref(false)
const customerOptions = ref<CustomerListItemDto[]>([])
const orderOptions = ref<ServiceOrderListItemDto[]>([])

const createForm = reactive({
  title: '',
  category: undefined as number | undefined,
  description: '',
  suggestion: '',
  customerId: undefined as number | undefined,
  orderId: undefined as number | undefined,
})

const createRules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入反馈标题', trigger: 'blur' }],
  category: [{ required: true, message: '请选择分类', trigger: 'change' }],
  description: [{ required: true, message: '请输入描述', trigger: 'blur' }],
}

// 详情
const detailVisible = ref(false)
const currentDetail = ref<ServiceFeedbackDto | null>(null)

// 处理
const handleLoading = ref(false)
const handleForm = reactive({
  newStatus: undefined as number | undefined,
  handleResult: '',
})

// 辅助方法
function getCategoryLabel(v: number) {
  const map: Record<number, string> = { 1: '服务质量', 2: '时效问题', 3: '费用争议', 4: '建议', 5: '其他' }
  return map[v] || '未知'
}

function getStatusLabel(v: number) {
  const map: Record<number, string> = { 0: '待审阅', 1: '已受理', 2: '改善中', 3: '已落实', 4: '已驳回' }
  return map[v] || '未知'
}

function getStatusColor(v: number) {
  const map: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'warning', 3: 'success', 4: 'error' }
  return map[v] || 'default'
}

// 数据加载
async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.category !== undefined) params.category = searchForm.category
    if (filterStatus.value !== '') params.status = filterStatus.value
    const res = await getServiceFeedbackList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

async function fetchStatusCounts() {
  try {
    // 通过列表数据统计各状态数量
    const res = await getServiceFeedbackList({ pageIndex: 1, pageSize: 1 }) as any
    const total = res?.total || 0
    statusCounts.total = total
    // 简化：使用 total 填充，精确计数需后端支持
    statusCounts.pending = 0
    statusCounts.accepted = 0
    statusCounts.improving = 0
    statusCounts.implemented = 0
    statusCounts.rejected = 0

    // 尝试分别请求各状态的数量
    for (const s of [0, 1, 2, 3, 4]) {
      const r = await getServiceFeedbackList({ pageIndex: 1, pageSize: 1, status: s }) as any
      const cnt = r?.total || 0
      if (s === 0) statusCounts.pending = cnt
      else if (s === 1) statusCounts.accepted = cnt
      else if (s === 2) statusCounts.improving = cnt
      else if (s === 3) statusCounts.implemented = cnt
      else if (s === 4) statusCounts.rejected = cnt
    }
    statusCounts.total = statusCounts.pending + statusCounts.accepted + statusCounts.improving + statusCounts.implemented + statusCounts.rejected
  } catch { /* ignore */ }
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.category = undefined
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

// 工单搜索
async function handleOrderSearch(val: string) {
  if (!val || val.length < 1) return
  try {
    const res = await getServiceOrderList({ keyword: val, pageSize: 20 }) as any
    orderOptions.value = res?.items || res || []
  } catch { /* ignore */ }
}

// 新建
function handleCreate() {
  createForm.title = ''
  createForm.category = undefined
  createForm.description = ''
  createForm.suggestion = ''
  createForm.customerId = undefined
  createForm.orderId = undefined
  createVisible.value = true
}

async function handleCreateSubmit() {
  if (!createFormRef.value) return
  try { await createFormRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    await createServiceFeedback({
      submitterId: 0, // 由后端从 token 获取
      title: createForm.title,
      category: createForm.category!,
      description: createForm.description || undefined,
      suggestion: createForm.suggestion || undefined,
      customerId: createForm.customerId,
      orderId: createForm.orderId,
    })
    message.success('提交成功')
    createVisible.value = false
    fetchList()
    fetchStatusCounts()
  } finally {
    submitLoading.value = false
  }
}

// 查看详情
async function handleView(record: any) {
  try {
    const res = await getServiceFeedbackById(record.id) as any
    currentDetail.value = res
    handleForm.newStatus = undefined
    handleForm.handleResult = ''
    detailVisible.value = true
  } catch {
    message.error('获取反馈详情失败')
  }
}

// 处理入口
function handleProcess(record: any) {
  handleView(record)
}

// 提交处理
async function handleSubmitProcess() {
  if (!handleForm.newStatus) {
    message.warning('请选择处理状态')
    return
  }
  handleLoading.value = true
  try {
    await handleServiceFeedback(currentDetail.value!.id, {
      handlerId: 0, // 由后端从 token 获取
      newStatus: handleForm.newStatus,
      handleResult: handleForm.handleResult || undefined,
    })
    message.success('处理成功')
    detailVisible.value = false
    fetchList()
    fetchStatusCounts()
  } finally {
    handleLoading.value = false
  }
}

onMounted(() => {
  fetchList()
  fetchStatusCounts()
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

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
}
</style>
