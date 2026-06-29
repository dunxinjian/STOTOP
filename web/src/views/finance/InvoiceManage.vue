<template>
  <div class="page-container">
    <PageHeader title="发票管理">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button type="primary" @click="importDialogVisible = true">
          <UploadOutlined />导入发票
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <span class="tab-item" :class="{ active: activeTab === '' }" @click="activeTab = ''; onTabChange()">全部</span>
            <span class="tab-item" :class="{ active: activeTab === '进项' }" @click="activeTab = '进项'; onTabChange()">进项发票</span>
            <span class="tab-item" :class="{ active: activeTab === '销项' }" @click="activeTab = '销项'; onTabChange()">销项发票</span>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-range-picker v-model:value="dateRange" style="width: 260px" valueFormat="YYYY-MM-DD" @change="handleSearch" />
            <a-select v-model:value="filterType" placeholder="发票类型" style="width: 140px" allowClear @change="handleSearch">
              <a-select-option value="增值税专用发票">增值税专用发票</a-select-option>
              <a-select-option value="增值税普通发票">增值税普通发票</a-select-option>
              <a-select-option value="电子发票">电子发票</a-select-option>
            </a-select>
            <a-select v-model:value="filterMatchStatus" placeholder="匹配状态" style="width: 120px" allowClear @change="handleSearch">
              <a-select-option :value="0">未匹配</a-select-option>
              <a-select-option :value="1">已匹配</a-select-option>
            </a-select>
            <a-input v-model:value="searchKeyword" placeholder="搜索发票号/名称" style="width: 200px" allowClear @pressEnter="handleSearch">
              <template #suffix>
                <SearchOutlined class="search-icon" @click="handleSearch" />
              </template>
            </a-input>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 发票列表 -->
    <a-card :bordered="false" class="table-card">
      <a-table :columns="columns" :dataSource="tableData" rowKey="id" :loading="loading" bordered :pagination="false"
        :scroll="{ y: 'calc(100% - 50px)' }">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'invoiceDate'">
            {{ formatDate(record.invoiceDate) }}
          </template>
          <template v-if="column.dataIndex === 'direction'">
            <a-tag :color="record.direction === '进项' ? 'blue' : 'green'">{{ record.direction }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'amount'">
            <span class="amount">{{ formatAmount(record.amount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'taxAmount'">
            <span class="amount">{{ formatAmount(record.taxAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'totalAmount'">
            <span class="amount" style="font-weight: 600;">{{ formatAmount(record.totalAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'taxRate'">
            {{ record.taxRate }}%
          </template>
          <template v-if="column.dataIndex === 'matchStatus'">
            <a-tag :color="record.matchStatus === 1 ? 'success' : 'default'">
              {{ record.matchStatus === 1 ? '已匹配' : '未匹配' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-space>
              <a @click="showDetail(record)">查看</a>
              <a v-if="record.matchStatus !== 1" @click="openMatchDialog(record)">匹配</a>
              <a v-if="record.matchStatus !== 1" @click="openGenerateDialog(record)">生成凭证</a>
            </a-space>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
      <div class="pagination-wrapper">
        <a-pagination v-model:current="pagination.pageIndex" v-model:pageSize="pagination.pageSize"
          :pageSizeOptions="['20', '50', '100', '200']" :total="pagination.total" showSizeChanger
          @change="handlePageChange" @showSizeChange="handleSizeChange" />
      </div>
    </a-card>

    <!-- 税额汇总 -->
    <a-card :bordered="false" title="进销项税额汇总" class="summary-card">
      <div style="margin-bottom: 12px;">
        <a-select v-model:value="summaryYear" style="width: 120px" @change="loadTaxSummary">
          <a-select-option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</a-select-option>
        </a-select>
      </div>
      <a-table :columns="summaryColumns" :dataSource="taxSummaryData" rowKey="month" :loading="summaryLoading"
        bordered :pagination="false" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'month'">{{ record.month }}月</template>
          <template v-if="column.dataIndex === 'inputTaxAmount'">
            <span class="amount">{{ formatAmount(record.inputTaxAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'outputTaxAmount'">
            <span class="amount">{{ formatAmount(record.outputTaxAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'taxPayable'">
            <span class="amount" :class="{ 'negative-value': record.taxPayable < 0 }">{{ formatAmount(record.taxPayable) }}</span>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 导入弹窗 -->
    <a-modal v-model:open="importDialogVisible" title="导入发票" :width="480" :centered="true" @ok="handleImport"
      :confirmLoading="importing">
      <a-upload-dragger v-model:fileList="importFileList" :maxCount="1" :beforeUpload="() => false"
        accept=".xlsx,.xls">
        <p class="ant-upload-drag-icon"><InboxOutlined /></p>
        <p class="ant-upload-text">点击或拖拽Excel文件到此区域</p>
        <p class="ant-upload-hint">支持 .xlsx / .xls 格式，表头需包含：发票类型、发票号码、发票代码、开票日期、销方名称、购方名称、金额、税额、税率、方向</p>
      </a-upload-dragger>
    </a-modal>

    <!-- 详情弹窗 -->
    <a-modal v-model:open="detailDialogVisible" title="发票详情" :width="640" :centered="true" :footer="null">
      <a-descriptions :column="2" bordered size="small" v-if="currentInvoice">
        <a-descriptions-item label="发票类型">{{ currentInvoice.invoiceType }}</a-descriptions-item>
        <a-descriptions-item label="方向">
          <a-tag :color="currentInvoice.direction === '进项' ? 'blue' : 'green'">{{ currentInvoice.direction }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="发票号码">{{ currentInvoice.invoiceNo }}</a-descriptions-item>
        <a-descriptions-item label="发票代码">{{ currentInvoice.invoiceCode || '-' }}</a-descriptions-item>
        <a-descriptions-item label="开票日期">{{ formatDate(currentInvoice.invoiceDate) }}</a-descriptions-item>
        <a-descriptions-item label="匹配状态">
          <a-tag :color="currentInvoice.matchStatus === 1 ? 'success' : 'default'">
            {{ currentInvoice.matchStatus === 1 ? '已匹配' : '未匹配' }}
          </a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="销方名称" :span="2">{{ currentInvoice.sellerName || '-' }}</a-descriptions-item>
        <a-descriptions-item label="销方税号" :span="2">{{ currentInvoice.sellerTaxNo || '-' }}</a-descriptions-item>
        <a-descriptions-item label="购方名称" :span="2">{{ currentInvoice.buyerName || '-' }}</a-descriptions-item>
        <a-descriptions-item label="购方税号" :span="2">{{ currentInvoice.buyerTaxNo || '-' }}</a-descriptions-item>
        <a-descriptions-item label="金额">{{ formatAmount(currentInvoice.amount) }}</a-descriptions-item>
        <a-descriptions-item label="税额">{{ formatAmount(currentInvoice.taxAmount) }}</a-descriptions-item>
        <a-descriptions-item label="价税合计">{{ formatAmount(currentInvoice.totalAmount) }}</a-descriptions-item>
        <a-descriptions-item label="税率">{{ currentInvoice.taxRate }}%</a-descriptions-item>
      </a-descriptions>
    </a-modal>

    <!-- 匹配凭证弹窗 -->
    <a-modal v-model:open="matchDialogVisible" title="匹配凭证" :width="480" :centered="true" @ok="handleMatch"
      :confirmLoading="matching">
      <p style="margin-bottom: 12px;">为发票 <strong>{{ matchInvoice_?.invoiceNo }}</strong> 选择关联的凭证：</p>
      <a-input-number v-model:value="matchVoucherId" placeholder="请输入凭证ID" style="width: 100%;" />
    </a-modal>

    <!-- 生成凭证确认弹窗 -->
    <a-modal v-model:open="generateDialogVisible" title="从发票生成凭证" :width="560" :centered="true"
      @ok="handleGenerate" :confirmLoading="generating">
      <div v-if="generateInvoice_">
        <a-alert type="info" style="margin-bottom: 16px;">
          <template #message>
            将根据发票 <strong>{{ generateInvoice_.invoiceNo }}</strong> 自动生成凭证分录
          </template>
        </a-alert>
        <div style="margin-bottom: 12px;">
          <span style="margin-right: 8px;">凭证字：</span>
          <a-select v-model:value="generateForm.voucherWord" style="width: 100px">
            <a-select-option value="记">记</a-select-option>
            <a-select-option value="收">收</a-select-option>
            <a-select-option value="付">付</a-select-option>
            <a-select-option value="转">转</a-select-option>
          </a-select>
        </div>
        <div style="margin-bottom: 16px;">
          <span style="margin-right: 8px;">会计期间：</span>
          <a-select v-model:value="generateForm.periodId" style="width: 200px" placeholder="选择会计期间"
            :options="periodList.map(p => ({ label: p.name, value: p.id }))" />
        </div>
        <a-card size="small" title="预览分录">
          <template v-if="generateInvoice_.direction === '进项'">
            <p>借：库存商品(1405) — {{ formatAmount(generateInvoice_.amount) }}</p>
            <p>借：应交税费-进项税额(222101) — {{ formatAmount(generateInvoice_.taxAmount) }}</p>
            <p>贷：应付账款(2202) — {{ formatAmount(generateInvoice_.totalAmount) }}</p>
          </template>
          <template v-else>
            <p>借：应收账款(1122) — {{ formatAmount(generateInvoice_.totalAmount) }}</p>
            <p>贷：主营业务收入(5001) — {{ formatAmount(generateInvoice_.amount) }}</p>
            <p>贷：应交税费-销项税额(222102) — {{ formatAmount(generateInvoice_.taxAmount) }}</p>
          </template>
        </a-card>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  UploadOutlined, SearchOutlined, InboxOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getInvoiceList, importInvoices, matchInvoice, generateVoucherFromInvoice,
  getInvoiceTaxSummary, getPeriods,
  type InvoiceDto, type TaxSummaryDto,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import type { UploadFile } from 'ant-design-vue'

const accountSetStore = useAccountSetStore()

// 列表
const columns = [
  { title: '发票号码', dataIndex: 'invoiceNo', key: 'invoiceNo', width: 140, ellipsis: true },
  { title: '发票代码', dataIndex: 'invoiceCode', key: 'invoiceCode', width: 120, ellipsis: true },
  { title: '开票日期', dataIndex: 'invoiceDate', key: 'invoiceDate', width: 110, align: 'center' as const },
  { title: '方向', dataIndex: 'direction', key: 'direction', width: 80, align: 'center' as const },
  { title: '销方/购方', dataIndex: 'counterparty', key: 'counterparty', minWidth: 160, ellipsis: true,
    customRender: ({ record }: { record: InvoiceDto }) => record.direction === '进项' ? record.sellerName : record.buyerName },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 120, align: 'right' as const },
  { title: '税额', dataIndex: 'taxAmount', key: 'taxAmount', width: 110, align: 'right' as const },
  { title: '价税合计', dataIndex: 'totalAmount', key: 'totalAmount', width: 120, align: 'right' as const },
  { title: '税率', dataIndex: 'taxRate', key: 'taxRate', width: 70, align: 'center' as const },
  { title: '匹配状态', dataIndex: 'matchStatus', key: 'matchStatus', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

const summaryColumns = [
  { title: '月份', dataIndex: 'month', key: 'month', width: 80, align: 'center' as const },
  { title: '进项税额', dataIndex: 'inputTaxAmount', key: 'inputTaxAmount', align: 'right' as const },
  { title: '销项税额', dataIndex: 'outputTaxAmount', key: 'outputTaxAmount', align: 'right' as const },
  { title: '应纳税额', dataIndex: 'taxPayable', key: 'taxPayable', align: 'right' as const },
]

// 状态
const loading = ref(false)
const tableData = ref<InvoiceDto[]>([])
const activeTab = ref('')
const dateRange = ref<[string, string] | undefined>(undefined)
const filterType = ref<string | undefined>(undefined)
const filterMatchStatus = ref<number | undefined>(undefined)
const searchKeyword = ref('')
const pagination = ref({ pageIndex: 1, pageSize: 50, total: 0 })

// 导入
const importDialogVisible = ref(false)
const importFileList = ref<UploadFile[]>([])
const importing = ref(false)

// 详情
const detailDialogVisible = ref(false)
const currentInvoice = ref<InvoiceDto | null>(null)

// 匹配
const matchDialogVisible = ref(false)
const matchInvoice_ = ref<InvoiceDto | null>(null)
const matchVoucherId = ref<number | undefined>(undefined)
const matching = ref(false)

// 生成凭证
const generateDialogVisible = ref(false)
const generateInvoice_ = ref<InvoiceDto | null>(null)
const generateForm = ref({ voucherWord: '记', periodId: undefined as number | undefined })
const generating = ref(false)
const periodList = ref<{ id: number; name: string; year: number; month: number }[]>([])

// 税额汇总
const summaryYear = ref(new Date().getFullYear())
const summaryLoading = ref(false)
const taxSummaryData = ref<TaxSummaryDto[]>([])
const yearOptions = computed(() => {
  const current = new Date().getFullYear()
  return [current, current - 1, current - 2]
})

// 加载数据
async function loadData() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
    }
    if (activeTab.value) params.direction = activeTab.value
    if (dateRange.value && dateRange.value.length === 2) {
      params.startDate = dateRange.value[0]
      params.endDate = dateRange.value[1]
    }
    if (filterType.value) params.invoiceType = filterType.value
    if (filterMatchStatus.value !== undefined && filterMatchStatus.value !== null)
      params.matchStatus = filterMatchStatus.value
    if (searchKeyword.value) params.keyword = searchKeyword.value

    const res = await getInvoiceList(params)
    if (res && res.items) {
      tableData.value = res.items
      pagination.value.total = res.total || 0
    } else {
      tableData.value = []
      pagination.value.total = 0
    }
  } catch (error) {
    console.error('加载发票列表失败', error)
    tableData.value = []
    pagination.value.total = 0
  } finally {
    loading.value = false
  }
}

// 加载税额汇总
async function loadTaxSummary() {
  summaryLoading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await getInvoiceTaxSummary({ year: summaryYear.value, accountSetId })
    taxSummaryData.value = res || []
  } catch (error) {
    console.error('加载税额汇总失败', error)
    taxSummaryData.value = []
  } finally {
    summaryLoading.value = false
  }
}

// 加载会计期间
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const res = await getPeriods(accountSetId) as any[]
    const sorted = (res || [])
      .sort((a: any, b: any) => {
        if (a.year !== b.year) return b.year - a.year
        return b.periodNo - a.periodNo
      })
    periodList.value = sorted.map((p: any) => ({
      id: p.id,
      name: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
      year: p.year,
      month: p.periodNo,
    }))
    // 无已选期间时，基于当前系统日期匹配默认期间
    if (!generateForm.value.periodId && periodList.value.length > 0) {
      const now = new Date()
      const currentYear = now.getFullYear()
      const currentMonth = now.getMonth() + 1
      const matched = periodList.value.find((p: any) => p.year === currentYear && p.month === currentMonth)
      generateForm.value.periodId = matched ? matched.id : periodList.value[0].id
    }
  } catch (error) {
    console.error('加载期间失败', error)
  }
}

function onTabChange() {
  pagination.value.pageIndex = 1
  loadData()
}

function handleSearch() {
  pagination.value.pageIndex = 1
  loadData()
}

function handlePageChange(page: number) {
  pagination.value.pageIndex = page
  loadData()
}

function handleSizeChange(_current: number, size: number) {
  pagination.value.pageSize = size
  pagination.value.pageIndex = 1
  loadData()
}

// 导入
async function handleImport() {
  if (importFileList.value.length === 0) {
    message.warning('请选择要导入的文件')
    return
  }
  importing.value = true
  try {
    const formData = new FormData()
    formData.append('file', importFileList.value[0].originFileObj as Blob)
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await importInvoices(formData, accountSetId)
    message.success(`成功导入 ${res?.count || 0} 张发票`)
    importDialogVisible.value = false
    importFileList.value = []
    loadData()
    loadTaxSummary()
  } catch (error) {
    console.error('导入失败', error)
  } finally {
    importing.value = false
  }
}

// 详情
function showDetail(record: any) {
  currentInvoice.value = record
  detailDialogVisible.value = true
}

// 匹配
function openMatchDialog(record: any) {
  matchInvoice_.value = record
  matchVoucherId.value = undefined
  matchDialogVisible.value = true
}

async function handleMatch() {
  if (!matchInvoice_.value || !matchVoucherId.value) {
    message.warning('请输入凭证ID')
    return
  }
  matching.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    await matchInvoice(matchInvoice_.value.id, { voucherId: matchVoucherId.value }, accountSetId)
    message.success('匹配成功')
    matchDialogVisible.value = false
    loadData()
  } catch (error) {
    console.error('匹配失败', error)
  } finally {
    matching.value = false
  }
}

// 生成凭证
function openGenerateDialog(record: any) {
  generateInvoice_.value = record
  generateForm.value = { voucherWord: '记', periodId: undefined }
  generateDialogVisible.value = true
}

async function handleGenerate() {
  if (!generateInvoice_.value) return
  if (!generateForm.value.periodId) {
    message.warning('请选择会计期间')
    return
  }
  generating.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    await generateVoucherFromInvoice(
      generateInvoice_.value.id,
      { periodId: generateForm.value.periodId, voucherWord: generateForm.value.voucherWord },
      accountSetId
    )
    message.success('凭证生成成功')
    generateDialogVisible.value = false
    loadData()
  } catch (error) {
    console.error('生成凭证失败', error)
  } finally {
    generating.value = false
  }
}

// 格式化
function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatAmount(amount: number): string {
  if (amount === undefined || amount === null) return ''
  return amount.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, async () => {
  pagination.value.pageIndex = 1
  await loadPeriods()
  loadData()
  loadTaxSummary()
})

onMounted(() => {
  loadPeriods()
  loadData()
  loadTaxSummary()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.toolbar-left {
  gap: 4px;

  .tab-item {
    padding: 6px 16px;
    font-size: 14px;
    color: #606266;
    cursor: pointer;
    border-radius: 4px;
    transition: all 0.2s;
    background: transparent;

    &:hover {
      color: var(--color-primary);
    }

    &.active {
      color: var(--text-1);
      background: var(--bg-card);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
      font-weight: 500;
    }
  }
}

.toolbar-right {
  flex-wrap: wrap;

  .search-icon {
    cursor: pointer;
    color: #909399;

    &:hover {
      color: var(--color-primary);
    }
  }
}

.amount {
  font-family: 'Courier New', monospace;
  font-weight: 500;
}

.negative-value {
  color: var(--color-danger);
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  padding-top: $section-gap;
}

// 表格容器 - 填充剩余空间
.table-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;

  :deep(.ant-card-body) {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    min-height: 0;
    padding: 16px;
  }

  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  :deep(.ant-table) {
    flex: 1;
    display: flex;
    flex-direction: column;
  }

  :deep(.ant-table-container) {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  :deep(.ant-table-body) {
    flex: 1;
    overflow: auto !important;
  }

  .pagination-wrapper {
    flex-shrink: 0;
    padding-top: 12px;
  }
}

// 税额汇总卡片 - 固定高度
.summary-card {
  flex-shrink: 0;
  margin-top: 16px;
}
</style>
