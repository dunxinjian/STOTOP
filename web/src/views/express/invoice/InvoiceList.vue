<template>
  <div class="page-container">
    <PageHeader title="账单管理">
      <template #actions>
        <a-button @click="handleAutoGenerate" :loading="autoGenerateLoading">
          <template #icon><ThunderboltOutlined /></template>自动出账
        </a-button>
        <a-button type="primary" @click="generateDialogVisible = true">
          <template #icon><PlusOutlined /></template>生成账单
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; width:100%;">
          <a-tabs v-model:activeKey="activeTab" @change="handleTabChange" style="margin-bottom:0; flex: 1;">
            <a-tab-pane key="all">
              <template #tab>
                全部
                <a-badge :count="tabCounts.all" :overflow-count="9999" :number-style="{ backgroundColor: '#999' }" style="margin-left: 4px" />
              </template>
            </a-tab-pane>
            <a-tab-pane key="0">
              <template #tab>
                草稿
                <a-badge :count="tabCounts.draft" :number-style="{ backgroundColor: '#d9d9d9', color: '#666' }" style="margin-left: 4px" />
              </template>
            </a-tab-pane>
            <a-tab-pane key="1">
              <template #tab>
                已确认
                <a-badge :count="tabCounts.confirmed" :number-style="{ backgroundColor: 'var(--color-info)' }" style="margin-left: 4px" />
              </template>
            </a-tab-pane>
            <a-tab-pane key="2">
              <template #tab>
                已发送
                <a-badge :count="tabCounts.sent" :number-style="{ backgroundColor: 'var(--color-warning)' }" style="margin-left: 4px" />
              </template>
            </a-tab-pane>
            <a-tab-pane key="3">
              <template #tab>
                已收款
                <a-badge :count="tabCounts.paid" :number-style="{ backgroundColor: 'var(--color-success)' }" style="margin-left: 4px" />
              </template>
            </a-tab-pane>
          </a-tabs>
        </div>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px; margin-top: 8px;">
          <a-input-number v-model:value="searchForm.clientId" size="small" placeholder="客户ID" style="width: 120px" />
          <a-input v-model:value="searchForm.brandCode" size="small" placeholder="品牌编码" allow-clear style="width: 120px" />
          <a-select v-model:value="searchForm.clientType" size="small" placeholder="业务对象类型" allow-clear style="width: 120px"
            :options="clientTypeFilterOptions" />
          <a-select v-model:value="searchForm.reviewStatus" size="small" placeholder="审核状态" allow-clear style="width: 120px"
            :options="reviewStatusOptions" />
          <a-range-picker v-model:value="dateRange" size="small" style="width: 240px" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 批量操作栏 -->
    <div v-if="selectedRowKeys.length > 0" class="invoice-batch-bar">
      <span class="batch-count">已选择 <strong>{{ selectedRowKeys.length }}</strong> 项</span>
      <a-space>
        <a-popconfirm title="确认批量确认账单吗？" @confirm="handleBatchConfirm">
          <a-button type="primary" :loading="batchLoading">批量确认</a-button>
        </a-popconfirm>
        <a-popconfirm title="确认批量发送账单吗？" @confirm="handleBatchSend">
          <a-button :loading="batchLoading">批量发送</a-button>
        </a-popconfirm>
        <a-popconfirm title="确认批量审核通过吗？" @confirm="handleBatchReview(true)">
          <a-button type="primary" ghost>批量审核通过</a-button>
        </a-popconfirm>
        <a-popconfirm title="确认批量驳回吗？" @confirm="handleBatchReview(false)">
          <a-button danger ghost>批量驳回</a-button>
        </a-popconfirm>
        <a-button @click="selectedRowKeys = []">取消选择</a-button>
      </a-space>
    </div>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        :row-selection="invoiceRowSelection"
        row-key="id"
        bordered
        :scroll="{ x: 1900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'clientName'">
            <a
              class="cross-module-link"
              @click="router.push({ path: '/crm/customers', query: { keyword: record.clientName } })"
            >{{ record.clientName }}</a>
          </template>
          <template v-if="column.dataIndex === 'period'">
            {{ record.periodStart?.slice(0, 10) }} ~ {{ record.periodEnd?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'totalCharge'">
            ¥{{ (record.totalCharge ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'totalCost'">
            ¥{{ (record.totalCost ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'totalProfit'">
            <span :style="{ color: (record.totalProfit ?? 0) >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">
              ¥{{ (record.totalProfit ?? 0).toFixed(2) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'payableAmount'">
            ¥{{ (record.payableAmount ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'reconciliationStatus'">
            <a-tag :color="getReconciliationStatusColor(record.reconciliationStatus)">
              {{ getReconciliationStatusText(record.reconciliationStatus) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'reviewStatus'">
            <a-tag :color="getReviewStatusColor(record.reviewStatus)">
              {{ getReviewStatusText(record.reviewStatus) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewDetail(record as InvoiceDto)">详情</a-button>
            <a-button v-if="record.status === 0" type="link" size="small" @click="handleConfirm(record as InvoiceDto)">确认</a-button>
            <a-button v-if="record.status === 1" type="link" size="small" @click="handleSend(record as InvoiceDto)">发送</a-button>
            <a-button v-if="record.status === 2" type="link" size="small" @click="openPaymentDialog(record as InvoiceDto)">收款</a-button>
            <a-button v-if="record.reviewStatus === 0" type="link" size="small" @click="openReviewDialog(record as InvoiceDto, true)">审核</a-button>
            <a-button v-if="record.reviewStatus === 1" type="link" size="small" danger @click="openReviewDialog(record as InvoiceDto, false)">反审核</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 生成账单弹窗 -->
    <a-modal v-model:open="generateDialogVisible" title="生成账单" width="500px" :destroy-on-close="true" @cancel="generateDialogVisible = false">
      <a-form ref="generateFormRef" :model="generateForm" :rules="generateRules" :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="客户ID" name="clientId">
          <a-input-number v-model:value="generateForm.clientId" style="width: 100%" placeholder="请输入客户ID" />
        </a-form-item>
        <a-form-item label="品牌编码" name="brandCode">
          <a-input v-model:value="generateForm.brandCode" style="width: 100%" placeholder="请输入品牌编码" />
        </a-form-item>
        <a-form-item label="账期" name="periodRange">
          <a-range-picker v-model:value="generateForm.periodRange" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="generateDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="generateLoading" @click="handleGenerate">生成</a-button>
      </template>
    </a-modal>

    <!-- 收款弹窗 -->
    <a-modal v-model:open="paymentDialogVisible" title="收款" width="400px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="收款金额">
          <a-input-number v-model:value="paymentAmount" :min="0" :precision="2" style="width: 100%" placeholder="请输入收款金额" prefix="¥" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="paymentDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="paymentLoading" @click="handlePayment">确定</a-button>
      </template>
    </a-modal>

    <!-- 审核/反审核弹窗 -->
    <a-modal v-model:open="reviewDialogVisible" :title="isApproveAction ? '审核账单' : '反审核账单'" width="400px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="备注">
          <a-textarea v-model:value="reviewRemark" :rows="3" placeholder="请输入备注（可选）" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="reviewDialogVisible = false">取消</a-button>
        <a-button v-if="isApproveAction" danger @click="handleReview(false)">驳回</a-button>
        <a-button v-if="isApproveAction" type="primary" @click="handleReview(true)">通过</a-button>
        <a-button v-if="!isApproveAction" type="primary" danger :loading="reviewLoading" @click="handleReverseReview">确认反审核</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { TableRowSelection, Key } from 'ant-design-vue/es/table/interface'
import type { Dayjs } from 'dayjs'
import { PlusOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getInvoiceList,
  generateInvoice,
  confirmInvoice,
  sendInvoice,
  receivePayment,
  reviewInvoice,
  reverseReview,
  triggerAutoGenerate,
  type InvoiceDto,
} from '@/api/express'

const router = useRouter()

// 批量操作
const selectedRowKeys = ref<number[]>([])
const batchLoading = ref(false)

const invoiceRowSelection = computed<TableRowSelection<InvoiceDto>>(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: Key[]) => { selectedRowKeys.value = keys.map(Number) },
}))

// Tab 状态
const activeTab = ref<string>('all')
const tabCounts = reactive({ all: 0, draft: 0, confirmed: 0, sent: 0, paid: 0 })

// 搜索
const searchForm = reactive({
  clientId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  clientType: undefined as string | undefined,
  reviewStatus: undefined as number | undefined,
  status: undefined as number | undefined,
})

const clientTypeFilterOptions = [
  { value: 'KH', label: '客户' },
  { value: 'DL', label: '代理' },
  { value: 'WD', label: '网点' },
  { value: 'YW', label: '业务员' },
  { value: 'CB', label: '承包区' },
  { value: 'YZ', label: '驿站' },
]
const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)

const reviewStatusOptions = [
  { label: '待审核', value: 0 },
  { label: '已通过', value: 1 },
  { label: '已驳回', value: 2 },
]
const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '已确认', value: 1 },
  { label: '已发送', value: 2 },
  { label: '已收款', value: 3 },
]

// 表格
const loading = ref(false)
const tableData = ref<InvoiceDto[]>([])
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
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '账单号', dataIndex: 'invoiceNo', width: 150 },
  { title: '报价编号', dataIndex: 'quotationCode', width: 140, ellipsis: true },
  { title: '业务对象类型', dataIndex: 'clientType', width: 110, align: 'center' as const },
  { title: '客户', dataIndex: 'clientName', width: 120, ellipsis: true },
  { title: '品牌', dataIndex: 'brandName', width: 100 },
  { title: '账期', dataIndex: 'period', width: 200 },
  { title: '运单数', dataIndex: 'totalWaybills', width: 80, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, customRender: ({ text }: any) => text?.toFixed(2) ?? '-' },
  { title: '应收金额', dataIndex: 'totalCharge', width: 120, align: 'right' as const },
  { title: '成本', dataIndex: 'totalCost', width: 120, align: 'right' as const },
  { title: '毛利', dataIndex: 'totalProfit', width: 120, align: 'right' as const },
  { title: '应付金额', dataIndex: 'payableAmount', width: 120, align: 'right' as const },
  { title: '对账状态', dataIndex: 'reconciliationStatus', width: 100, align: 'center' as const },
  { title: '审核状态', dataIndex: 'reviewStatus', width: 100, align: 'center' as const },
  { title: '业务状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 260, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      ...searchForm,
    }
    // Tab状态筛选
    if (activeTab.value !== 'all') {
      params.status = Number(activeTab.value)
    }
    if (dateRange.value) {
      params.periodStart = dateRange.value[0].format('YYYY-MM-DD')
      params.periodEnd = dateRange.value[1].format('YYYY-MM-DD')
    }
    const res = await getInvoiceList(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取账单列表失败')
  } finally {
    loading.value = false
  }
}

// TODO: 对接各状态数量统计API，目前在获取列表后用总数近似
async function fetchTabCounts() {
  try {
    // 查询各状态数量
    const baseParams: any = { pageIndex: 1, pageSize: 1 }
    if (searchForm.clientId) baseParams.clientId = searchForm.clientId
    if (searchForm.brandCode) baseParams.brandCode = searchForm.brandCode
    if (searchForm.reviewStatus !== undefined) baseParams.reviewStatus = searchForm.reviewStatus
    if (dateRange.value) {
      baseParams.periodStart = dateRange.value[0].format('YYYY-MM-DD')
      baseParams.periodEnd = dateRange.value[1].format('YYYY-MM-DD')
    }
    const [allRes, draftRes, confirmedRes, sentRes, paidRes] = await Promise.all([
      getInvoiceList({ ...baseParams }).catch(() => ({ total: 0 })),
      getInvoiceList({ ...baseParams, status: 0 }).catch(() => ({ total: 0 })),
      getInvoiceList({ ...baseParams, status: 1 }).catch(() => ({ total: 0 })),
      getInvoiceList({ ...baseParams, status: 2 }).catch(() => ({ total: 0 })),
      getInvoiceList({ ...baseParams, status: 3 }).catch(() => ({ total: 0 })),
    ])
    tabCounts.all = allRes.total
    tabCounts.draft = draftRes.total
    tabCounts.confirmed = confirmedRes.total
    tabCounts.sent = sentRes.total
    tabCounts.paid = paidRes.total
  } catch {
    // 获取数量失败不影响主流程
  }
}

function handleTabChange() {
  // 切换Tab时清空批量选择和搜索中的状态（由Tab控制）
  selectedRowKeys.value = []
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchList()
  fetchTabCounts()
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.clientId = undefined
  searchForm.brandCode = undefined
  searchForm.clientType = undefined
  searchForm.reviewStatus = undefined
  searchForm.status = undefined
  dateRange.value = undefined
  activeTab.value = 'all'
  handleSearch()
}

// 状态映射
function getReviewStatusText(s: number) { return ['待审核', '已通过', '已驳回'][s] ?? '未知' }
function getReviewStatusColor(s: number) { return ['processing', 'success', 'error'][s] ?? 'default' }
function getStatusText(s: number) { return ['草稿', '已确认', '已发送', '已收款'][s] ?? '未知' }
function getStatusColor(s: number) { return ['default', 'blue', 'orange', 'green'][s] ?? 'default' }
function getReconciliationStatusText(s: number) { return ['未对账', '已对账', '有异议', '已解决'][s ?? 0] ?? '未知' }
function getReconciliationStatusColor(s: number) { return ['default', 'success', 'error', 'processing'][s ?? 0] ?? 'default' }

// 详情
function handleViewDetail(row: InvoiceDto) {
  router.push({ name: 'ExpressInvoiceDetail', params: { id: row.id } })
}

// 确认
async function handleConfirm(row: InvoiceDto) {
  try {
    await confirmInvoice(row.id)
    message.success('账单已确认')
    fetchList()
  } catch { /* handled */ }
}

// 发送
async function handleSend(row: InvoiceDto) {
  try {
    await sendInvoice(row.id)
    message.success('账单已发送')
    fetchList()
  } catch { /* handled */ }
}

// 收款
const paymentDialogVisible = ref(false)
const paymentAmount = ref<number>(0)
const paymentLoading = ref(false)
const currentInvoiceId = ref(0)

function openPaymentDialog(row: InvoiceDto) {
  currentInvoiceId.value = row.id
  paymentAmount.value = row.payableAmount ?? 0
  paymentDialogVisible.value = true
}

async function handlePayment() {
  if (!paymentAmount.value || paymentAmount.value <= 0) {
    message.error('请输入有效金额')
    return
  }
  paymentLoading.value = true
  try {
    await receivePayment(currentInvoiceId.value, { amount: paymentAmount.value })
    message.success('收款成功')
    paymentDialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    paymentLoading.value = false
  }
}

// 审核 / 反审核
const reviewDialogVisible = ref(false)
const isApproveAction = ref(true)
const reviewRemark = ref('')
const reviewLoading = ref(false)

function openReviewDialog(row: InvoiceDto, isApprove: boolean) {
  currentInvoiceId.value = row.id
  isApproveAction.value = isApprove
  reviewRemark.value = ''
  reviewDialogVisible.value = true
}

async function handleReview(approved: boolean) {
  reviewLoading.value = true
  try {
    await reviewInvoice(currentInvoiceId.value, { approved, remark: reviewRemark.value || undefined })
    message.success(approved ? '审核通过' : '审核驳回')
    reviewDialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    reviewLoading.value = false
  }
}

async function handleReverseReview() {
  reviewLoading.value = true
  try {
    await reverseReview(currentInvoiceId.value, { remark: reviewRemark.value || undefined })
    message.success('反审核成功')
    reviewDialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    reviewLoading.value = false
  }
}

// 生成账单
const generateDialogVisible = ref(false)
const generateFormRef = ref<FormInstance>()
const generateLoading = ref(false)
const generateForm = reactive({
  clientId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  periodRange: undefined as [Dayjs, Dayjs] | undefined,
})
const generateRules: Record<string, Rule[]> = {
  clientId: [{ required: true, message: '请输入客户ID', trigger: 'blur' }],
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
  periodRange: [{ required: true, message: '请选择账期', trigger: 'change' }],
}

async function handleGenerate() {
  if (!generateFormRef.value) return
  try { await generateFormRef.value.validate() } catch { return }
  generateLoading.value = true
  try {
    await generateInvoice({
      clientId: String(generateForm.clientId),
      brandCode: generateForm.brandCode!,
      periodStart: generateForm.periodRange![0].format('YYYY-MM-DD'),
      periodEnd: generateForm.periodRange![1].format('YYYY-MM-DD'),
    })
    message.success('账单生成成功')
    generateDialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    generateLoading.value = false
  }
}

// 自动出账
const autoGenerateLoading = ref(false)
async function handleAutoGenerate() {
  autoGenerateLoading.value = true
  try {
    await triggerAutoGenerate()
    message.success('自动出账任务已执行')
    fetchList()
  } catch { /* handled */ } finally {
    autoGenerateLoading.value = false
  }
}

async function handleBatchConfirm() {
  batchLoading.value = true
  try {
    const results = await Promise.allSettled(
      selectedRowKeys.value.map(id => confirmInvoice(id))
    )
    const succeeded = results.filter(r => r.status === 'fulfilled').length
    const failed = results.filter(r => r.status === 'rejected').length
    if (succeeded > 0) message.success(`成功确认 ${succeeded} 条账单`)
    if (failed > 0) message.warning(`${failed} 条账单确认失败`)
    selectedRowKeys.value = []
    await fetchList()
    await fetchTabCounts()
  } catch {
    message.error('批量确认失败')
  } finally {
    batchLoading.value = false
  }
}

async function handleBatchSend() {
  batchLoading.value = true
  try {
    const results = await Promise.allSettled(
      selectedRowKeys.value.map(id => sendInvoice(id))
    )
    const succeeded = results.filter(r => r.status === 'fulfilled').length
    const failed = results.filter(r => r.status === 'rejected').length
    if (succeeded > 0) message.success(`成功发送 ${succeeded} 条账单`)
    if (failed > 0) message.warning(`${failed} 条账单发送失败`)
    selectedRowKeys.value = []
    await fetchList()
    await fetchTabCounts()
  } catch {
    message.error('批量发送失败')
  } finally {
    batchLoading.value = false
  }
}

async function handleBatchReview(approved: boolean) {
  batchLoading.value = true
  try {
    const results = await Promise.allSettled(
      selectedRowKeys.value.map(id =>
        reviewInvoice(id, { approved, remark: approved ? '批量审核通过' : '批量驳回' })
      )
    )
    const succeeded = results.filter(r => r.status === 'fulfilled').length
    const failed = results.filter(r => r.status === 'rejected').length
    const action = approved ? '审核通过' : '驳回'
    if (succeeded > 0) message.success(`成功${action} ${succeeded} 条账单`)
    if (failed > 0) message.warning(`${failed} 条账单${action}失败`)
    selectedRowKeys.value = []
    await fetchList()
    await fetchTabCounts()
  } catch {
    message.error('批量操作失败')
  } finally {
    batchLoading.value = false
  }
}

onMounted(() => {
  fetchList()
  fetchTabCounts()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.cross-module-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.invoice-batch-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--color-primary-light);
  border: 1px solid var(--color-primary-border);
  border-radius: 4px;
  margin-bottom: 12px;
}

.batch-count {
  color: var(--color-info);
  font-size: 14px;
}
</style>
