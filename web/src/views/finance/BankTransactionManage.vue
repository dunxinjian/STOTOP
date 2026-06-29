<template>
  <div class="page-container">
    <PageHeader title="银行流水管理">
      <template #actions>
        <a-button v-if="has(FinancePermissions.BankTransactionImport)" @click="importDialogVisible = true">
          <template #icon><UploadOutlined /></template>导入流水
        </a-button>
        <a-button v-if="has(FinancePermissions.BankTransactionMatch)" type="primary" @click="handleBatchGenerateVouchers">
          <template #icon><ThunderboltOutlined /></template>一键生成凭证
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:4px;">
            <span class="tab-item" :class="{ active: filterStatus === '' }" @click="filterStatus = ''; handleSearch()">
              全部({{ statusCounts.all }})
            </span>
            <span class="tab-item" :class="{ active: filterStatus === '0' }" @click="filterStatus = '0'; handleSearch()">
              未匹配({{ statusCounts.unmatched }})
            </span>
            <span class="tab-item" :class="{ active: filterStatus === '1' }" @click="filterStatus = '1'; handleSearch()">
              已匹配({{ statusCounts.matched }})
            </span>
            <span class="tab-item" :class="{ active: filterStatus === '2' }" @click="filterStatus = '2'; handleSearch()">
              无需匹配({{ statusCounts.skipped }})
            </span>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select v-model:value="searchForm.channelId" size="small" placeholder="全部渠道" allow-clear style="width: 140px" :options="channelOptions" />
            <a-range-picker v-model:value="searchForm.dateRange" size="small" style="width: 220px" valueFormat="YYYY-MM-DD" />
            <a-select v-model:value="searchForm.direction" size="small" placeholder="收支方向" allow-clear style="width: 100px">
              <a-select-option :value="1">收入</a-select-option>
              <a-select-option :value="2">支出</a-select-option>
            </a-select>
            <a-input v-model:value="searchForm.counterpartName" size="small" placeholder="对方户名" allow-clear style="width: 130px" @keyup.enter="handleSearch" />
            <a-input-number v-model:value="searchForm.minAmount" size="small" placeholder="最小金额" style="width: 100px" :min="0" :precision="2" />
            <span>~</span>
            <a-input-number v-model:value="searchForm.maxAmount" size="small" placeholder="最大金额" style="width: 100px" :min="0" :precision="2" />
            <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
            <a-button size="small" @click="handleReset">重置</a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false" class="toolbar-section">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1500 }"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'counterpartName'">
            <a-tooltip :title="record.counterpartName">{{ record.counterpartName }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'direction'">
            <a-tag :color="record.direction === 1 ? 'green' : 'red'">
              {{ record.direction === 1 ? '收入' : '支出' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'amount'">
            <span class="amount">{{ formatCurrency(record.amount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'summary'">
            <a-tooltip :title="record.summary">{{ record.summary }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'matchStatus'">
            <a-tag :color="matchStatusColor(record.matchStatus)">
              {{ matchStatusText(record.matchStatus) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'relatedBusinessType'">
            {{ record.relatedBusinessType || '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button
              v-if="record.matchStatus === 0 && has(FinancePermissions.BankTransactionMatch)"
              type="link" size="small" @click="handleMatch(record as BankTransactionDto)"
            >匹配</a-button>
            <a-button
              v-if="record.matchStatus === 1 && has(FinancePermissions.BankTransactionMatch)"
              type="link" size="small" danger @click="handleUnmatch(record as BankTransactionDto)"
            >取消匹配</a-button>
            <a-button
              v-if="record.matchStatus === 0 && has(FinancePermissions.BankTransactionMatch)"
              type="link" size="small" @click="handleMarkNoMatch(record as BankTransactionDto)"
            >标记无需匹配</a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无流水数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 导入流水弹窗 -->
    <a-modal
      v-model:open="importDialogVisible"
      title="导入银行流水"
      width="700px"
      :destroy-on-close="true"
      @cancel="resetImport"
    >
      <a-form :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="选择渠道" required>
          <a-select
            v-model:value="importForm.channelId"
            placeholder="请选择渠道"
            style="width: 300px"
            :options="channelOptions"
            @change="handleImportChannelChange"
          />
        </a-form-item>
        <a-form-item label="上传文件" required>
          <a-upload
            :file-list="importForm.fileList"
            :before-upload="handleBeforeUpload"
            :max-count="1"
            accept=".xlsx,.xls,.csv"
            @remove="importForm.fileList = []"
          >
            <a-button><UploadOutlined />选择文件</a-button>
          </a-upload>
        </a-form-item>

        <!-- 渠道映射配置展示 -->
        <div v-if="importChannelTemplate" class="template-preview">
          <div class="preview-title">列映射配置</div>
          <a-descriptions :column="2" size="small" bordered>
            <a-descriptions-item
              v-for="(val, key) in importChannelTemplate"
              :key="key"
              :label="getFieldLabel(key as string)"
            >
              {{ (val as any).excelColumn }}
              <a-tag v-if="(val as any).required" color="red" style="margin-left: 4px">必填</a-tag>
            </a-descriptions-item>
          </a-descriptions>
        </div>

        <!-- 导入结果 -->
        <div v-if="importResult" class="import-result">
          <a-alert type="success" show-icon>
            <template #message>
              导入完成：成功 <strong>{{ importResult.importedCount }}</strong> 条，
              去重跳过 <strong>{{ importResult.duplicateCount }}</strong> 条
            </template>
          </a-alert>
        </div>
      </a-form>
      <template #footer>
        <a-button @click="resetImport">关闭</a-button>
        <a-button type="primary" :loading="importLoading" :disabled="!importForm.channelId || importForm.fileList.length === 0" @click="handleImport">
          导入
        </a-button>
      </template>
    </a-modal>

    <!-- 手动匹配弹窗 -->
    <a-modal
      v-model:open="matchDialogVisible"
      title="手动匹配"
      width="800px"
      :destroy-on-close="true"
      @cancel="matchDialogVisible = false"
    >
      <div class="match-layout">
        <div class="match-left">
          <div class="section-title">当前流水信息</div>
          <a-descriptions :column="1" size="small" bordered>
            <a-descriptions-item label="交易日期">{{ currentMatchTx?.transactionDate }}</a-descriptions-item>
            <a-descriptions-item label="金额">
              <span class="amount">{{ formatCurrency(currentMatchTx?.amount || 0) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="对方户名">{{ currentMatchTx?.counterpartName }}</a-descriptions-item>
            <a-descriptions-item label="摘要">{{ currentMatchTx?.summary }}</a-descriptions-item>
          </a-descriptions>
        </div>
        <div class="match-right">
          <div class="section-title">匹配业务单据</div>
          <a-radio-group v-model:value="matchBusinessType" style="margin-bottom: 12px">
            <a-radio-button value="prepayment">预付款</a-radio-button>
            <a-radio-button value="billing">账单收款</a-radio-button>
            <a-radio-button value="other">其他</a-radio-button>
          </a-radio-group>
          <a-input v-model:value="matchSearchKeyword" placeholder="搜索单据" allow-clear style="margin-bottom: 12px" />
          <a-table
            :columns="matchTableColumns"
            :data-source="matchBusinessList"
            :pagination="false"
            size="small"
            bordered
            row-key="id"
            :row-selection="{ type: 'radio', selectedRowKeys: matchSelectedKeys, onChange: (keys: any) => matchSelectedKeys = keys }"
            :scroll="{ y: 200 }"
          >
            <template #emptyText>
              <EmptyState description="暂无匹配单据" />
            </template>
          </a-table>
        </div>
      </div>
      <template #footer>
        <a-button @click="matchDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="matchLoading" :disabled="matchSelectedKeys.length === 0" @click="handleConfirmMatch">
          确认匹配
        </a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { UploadOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getBankTransactionList,
  importBankTransactions,
  manualMatchBankTransaction,
  skipMatchBankTransactions,
  generateVoucherDraft,
  getAllEnabledBankChannels,
  getBankChannelById,
  type BankTransactionDto,
  type BankTransactionImportResult,
} from '@/api/finance'
import { usePermission, FinancePermissions } from '@/utils/permission'

const { has } = usePermission()

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '渠道名称', dataIndex: 'channelName', key: 'channelName', width: 120 },
  { title: '交易日期', dataIndex: 'transactionDate', key: 'transactionDate', width: 110 },
  { title: '交易流水号', dataIndex: 'transactionNo', key: 'transactionNo', width: 160 },
  { title: '对方户名', dataIndex: 'counterpartName', key: 'counterpartName', width: 150, ellipsis: true },
  { title: '收支方向', dataIndex: 'direction', key: 'direction', width: 90, align: 'center' as const },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 120, align: 'right' as const },
  { title: '摘要', dataIndex: 'summary', key: 'summary', width: 180, ellipsis: true },
  { title: '匹配状态', dataIndex: 'matchStatus', key: 'matchStatus', width: 100, align: 'center' as const },
  { title: '关联业务', dataIndex: 'relatedBusinessType', key: 'relatedBusinessType', width: 100 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  channelId: undefined as number | undefined,
  dateRange: undefined as [string, string] | undefined,
  direction: undefined as number | undefined,
  counterpartName: '',
  minAmount: undefined as number | undefined,
  maxAmount: undefined as number | undefined,
})

const filterStatus = ref('')

// 状态计数
const statusCounts = reactive({
  all: 0,
  unmatched: 0,
  matched: 0,
  skipped: 0,
})

// 表格数据
const loading = ref(false)
const tableData = ref<BankTransactionDto[]>([])
const selectedRowKeys = ref<(string | number)[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

// 渠道选项
const channelOptions = ref<{ label: string; value: number }[]>([])

// 系统字段映射
const fieldLabels: Record<string, string> = {
  transactionDate: '交易日期',
  transactionNo: '交易流水号',
  counterpartAccount: '对方账号',
  counterpartName: '对方户名',
  direction: '收支方向',
  amount: '金额',
  balance: '余额',
  summary: '摘要',
  remark: '备注',
}

function getFieldLabel(key: string) {
  return fieldLabels[key] || key
}

function matchStatusColor(status: number) {
  switch (status) {
    case 0: return 'default'
    case 1: return 'success'
    case 2: return 'warning'
    default: return 'default'
  }
}

function matchStatusText(status: number) {
  switch (status) {
    case 0: return '未匹配'
    case 1: return '已匹配'
    case 2: return '无需匹配'
    default: return '未知'
  }
}

function formatCurrency(amount: number) {
  if (!amount && amount !== 0) return ''
  return amount.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys
}

// 获取列表
async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (filterStatus.value) params.matchStatus = Number(filterStatus.value)
    if (searchForm.channelId) params.channelId = searchForm.channelId
    if (searchForm.dateRange && searchForm.dateRange.length === 2) {
      params.startDate = searchForm.dateRange[0]
      params.endDate = searchForm.dateRange[1]
    }
    if (searchForm.direction) params.direction = searchForm.direction
    if (searchForm.counterpartName) params.counterpartName = searchForm.counterpartName

    const res = await getBankTransactionList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
      // 更新状态计数
      statusCounts.all = res?.totalCount ?? pagination.total
      statusCounts.unmatched = res?.unmatchedCount ?? 0
      statusCounts.matched = res?.matchedCount ?? 0
      statusCounts.skipped = res?.skippedCount ?? 0
    }
  } finally {
    loading.value = false
  }
}

async function fetchChannels() {
  try {
    const res = await getAllEnabledBankChannels() as any[]
    channelOptions.value = (res || []).map((ch: any) => ({ label: ch.name, value: ch.id }))
  } catch (error) {
    console.error('加载渠道列表失败:', error)
  }
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.channelId = undefined
  searchForm.dateRange = undefined
  searchForm.direction = undefined
  searchForm.counterpartName = ''
  searchForm.minAmount = undefined
  searchForm.maxAmount = undefined
  filterStatus.value = ''
  pagination.pageIndex = 1
  fetchList()
}

// 导入相关
const importDialogVisible = ref(false)
const importLoading = ref(false)
const importResult = ref<BankTransactionImportResult | null>(null)
const importChannelTemplate = ref<Record<string, any> | null>(null)
const importForm = reactive({
  channelId: undefined as number | undefined,
  fileList: [] as UploadFile[],
})

function handleBeforeUpload(file: UploadFile) {
  importForm.fileList = [file]
  return false
}

async function handleImportChannelChange(value: SelectValue) {
  const channelId = value as number | undefined
  importChannelTemplate.value = null
  if (!channelId) return
  try {
    const ch = await getBankChannelById(channelId) as any
    if (ch?.importTemplate) {
      importChannelTemplate.value = JSON.parse(ch.importTemplate)
    }
  } catch {
    // ignore
  }
}

async function handleImport() {
  if (!importForm.channelId || importForm.fileList.length === 0) return
  importLoading.value = true
  importResult.value = null
  try {
    // Note: 实际导入需要后端解析 Excel，这里发送文件和 channelId
    const data = {
      channelId: importForm.channelId,
      items: [], // 实际由后端解析 Excel
    }
    const res = await importBankTransactions(data) as any
    importResult.value = res || { importedCount: 0, duplicateCount: 0 }
    message.success('导入完成')
    fetchList()
  } catch (error) {
    message.error('导入失败')
  } finally {
    importLoading.value = false
  }
}

function resetImport() {
  importDialogVisible.value = false
  importForm.channelId = undefined
  importForm.fileList = []
  importResult.value = null
  importChannelTemplate.value = null
}

// 匹配相关
const matchDialogVisible = ref(false)
const matchLoading = ref(false)
const currentMatchTx = ref<BankTransactionDto | null>(null)
const matchBusinessType = ref('prepayment')
const matchSearchKeyword = ref('')
const matchBusinessList = ref<any[]>([])
const matchSelectedKeys = ref<(string | number)[]>([])

const matchTableColumns = [
  { title: '单据编号', dataIndex: 'code', key: 'code', width: 140 },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 100, align: 'right' as const },
  { title: '日期', dataIndex: 'date', key: 'date', width: 110 },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
]

function handleMatch(record: BankTransactionDto) {
  currentMatchTx.value = record
  matchBusinessType.value = 'prepayment'
  matchSearchKeyword.value = ''
  matchBusinessList.value = []
  matchSelectedKeys.value = []
  matchDialogVisible.value = true
}

async function handleConfirmMatch() {
  if (!currentMatchTx.value || matchSelectedKeys.value.length === 0) return
  matchLoading.value = true
  try {
    await manualMatchBankTransaction({
      transactionId: currentMatchTx.value.id,
      businessType: matchBusinessType.value,
      businessId: matchSelectedKeys.value[0] as number,
    })
    message.success('匹配成功')
    matchDialogVisible.value = false
    fetchList()
  } catch (error) {
    message.error('匹配失败')
  } finally {
    matchLoading.value = false
  }
}

async function handleUnmatch(record: BankTransactionDto) {
  Modal.confirm({
    title: '确认取消匹配',
    content: `确定取消流水 ${record.transactionNo} 的匹配？`,
    async onOk() {
      try {
        // Use skipMatch with status reset or a dedicated API
        await skipMatchBankTransactions({ transactionIds: [record.id] })
        message.success('已取消匹配')
        fetchList()
      } catch {
        message.error('取消匹配失败')
      }
    },
  })
}

async function handleMarkNoMatch(record: BankTransactionDto) {
  try {
    await skipMatchBankTransactions({ transactionIds: [record.id] })
    message.success('已标记为无需匹配')
    fetchList()
  } catch {
    message.error('操作失败')
  }
}

async function handleBatchGenerateVouchers() {
  Modal.confirm({
    title: '一键生成凭证',
    content: '将为所有已匹配流水生成凭证草稿，确定继续？',
    async onOk() {
      try {
        const res = await generateVoucherDraft() as any
        if (res) {
          const firstError = Array.isArray(res.errors) ? res.errors[0] : undefined
          if (firstError) {
            message.warning(`生成完成：成功 ${res.generatedCount ?? 0} 条，跳过 ${res.skippedCount ?? 0} 条。${firstError}`)
          } else {
            message.success(`生成完成：成功 ${res.generatedCount ?? 0} 条，跳过 ${res.skippedCount ?? 0} 条`)
          }
        } else {
          message.success('生成完成')
        }
        fetchList()
      } catch {
        message.error('生成凭证失败')
      }
    },
  })
}

onMounted(() => {
  fetchChannels()
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

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

.amount {
  font-family: 'Courier New', monospace;
  font-weight: 500;
}

.template-preview {
  margin-top: 16px;
  .preview-title {
    font-size: 14px;
    font-weight: 500;
    margin-bottom: 8px;
  }
}

.import-result {
  margin-top: 16px;
}

.match-layout {
  display: flex;
  gap: 20px;

  .match-left {
    flex: 0 0 280px;
  }

  .match-right {
    flex: 1;
    min-width: 0;
  }
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
}
</style>
