<template>
  <div class="page-container">
    <PageHeader title="银行对账">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button v-if="has(FinancePermissions.BankReconciliationImport)" type="primary" @click="showImportModal = true">
          <UploadOutlined />导入银行流水
        </a-button>
        <a-button v-if="has(FinancePermissions.BankReconciliationMatch)" @click="handleAutoMatch" :loading="autoMatchLoading">
          <ThunderboltOutlined />自动匹配
        </a-button>
        <a-button v-if="has(FinancePermissions.BankReconciliationMatch)" @click="handleManualMatch"
          :disabled="!selectedStatement || !selectedVoucher">
          <LinkOutlined />手动匹配
        </a-button>
        <a-button @click="showReportModal = true">
          <FileTextOutlined />对账报告
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <span class="tab-item" :class="{ active: matchFilter === null }" @click="matchFilter = null; handleSearch()">
              全部
            </span>
            <span class="tab-item" :class="{ active: matchFilter === 0 }" @click="matchFilter = 0; handleSearch()">
              未匹配({{ unmatchedCount }})
            </span>
            <span class="tab-item" :class="{ active: matchFilter === 1 }" @click="matchFilter = 1; handleSearch()">
              已匹配({{ matchedCount }})
            </span>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-range-picker v-model:value="dateRange" style="width: 260px" valueFormat="YYYY-MM-DD"
              @change="handleSearch" />
            <a-input v-model:value="bankAccount" placeholder="银行账号" style="width: 160px" allowClear
              @pressEnter="handleSearch" />
            <a-button type="primary" @click="handleSearch">
              <SearchOutlined />查询
            </a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 主体区域：左右分栏 -->
    <div class="reconciliation-body">
      <!-- 左侧：银行流水 -->
      <a-card :bordered="false" class="panel-card" title="银行流水">
        <template #extra>
          <span class="panel-count">共 {{ statementTotal }} 笔</span>
        </template>
        <a-table :columns="statementColumns" :dataSource="statementList" rowKey="id" :loading="statementsLoading"
          bordered size="small" :pagination="false" :scroll="{ x: 780 }"
          :rowClassName="(record: any) => record.id === selectedStatement?.id ? 'selected-row' : ''"
          :customRow="(record: any) => ({ onClick: () => onSelectStatement(record) })">
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'transactionDate'">
              {{ formatDate(record.transactionDate) }}
            </template>
            <template v-if="column.dataIndex === 'debitAmount'">
              <span v-if="record.debitAmount" class="amount">{{ formatAmount(record.debitAmount) }}</span>
            </template>
            <template v-if="column.dataIndex === 'creditAmount'">
              <span v-if="record.creditAmount" class="amount">{{ formatAmount(record.creditAmount) }}</span>
            </template>
            <template v-if="column.dataIndex === 'balance'">
              <span class="amount">{{ formatAmount(record.balance) }}</span>
            </template>
            <template v-if="column.dataIndex === 'matchStatus'">
              <a-tag :color="record.matchStatus === 1 ? 'green' : 'default'">
                {{ record.matchStatus === 1 ? '已匹配' : '未匹配' }}
              </a-tag>
            </template>
          </template>
          <template #emptyText><EmptyState /></template>
        </a-table>
        <div class="pagination-wrapper">
          <a-pagination v-model:current="pagination.pageIndex" v-model:pageSize="pagination.pageSize"
            :pageSizeOptions="['20', '50', '100']" :total="pagination.total" size="small" showSizeChanger
            @change="handlePageChange" @showSizeChange="handleSizeChange" />
        </div>
      </a-card>

      <!-- 右侧：账面凭证分录 -->
      <a-card :bordered="false" class="panel-card" title="账面凭证分录（银行存款科目）">
        <template #extra>
          <span class="panel-count">共 {{ voucherEntries.length }} 笔</span>
        </template>
        <a-table :columns="voucherColumns" :dataSource="voucherEntries" rowKey="entryId" :loading="vouchersLoading"
          bordered size="small" :pagination="false"
          :rowClassName="(record: any) => record.entryId === selectedVoucher?.entryId ? 'selected-row' : ''"
          :customRow="(record: any) => ({ onClick: () => onSelectVoucher(record) })">
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'date'">
              {{ formatDate(record.date) }}
            </template>
            <template v-if="column.dataIndex === 'debitAmount'">
              <span v-if="record.debitAmount" class="amount">{{ formatAmount(record.debitAmount) }}</span>
            </template>
            <template v-if="column.dataIndex === 'creditAmount'">
              <span v-if="record.creditAmount" class="amount">{{ formatAmount(record.creditAmount) }}</span>
            </template>
          </template>
          <template #emptyText><EmptyState /></template>
        </a-table>
      </a-card>
    </div>

    <!-- 底部统计栏 -->
    <a-card :bordered="false" class="stats-bar">
      <div class="stats-content">
        <span>已匹配 <strong>{{ matchedCount }}</strong> 笔</span>
        <a-divider type="vertical" />
        <span>未匹配 <strong>{{ unmatchedCount }}</strong> 笔</span>
        <a-divider type="vertical" />
        <span>银行流水差额 <strong class="amount">{{ formatAmount(statementDiff) }}</strong> 元</span>
      </div>
    </a-card>

    <!-- 导入弹窗 -->
    <a-modal v-model:open="showImportModal" title="导入银行流水" :width="560" :centered="true" @ok="handleImport"
      :confirmLoading="importLoading" okText="开始导入">
      <a-form layout="vertical">
        <a-form-item label="选择文件">
          <a-upload :beforeUpload="beforeUpload" :maxCount="1" :fileList="importFileList"
            @remove="importFileList = []" accept=".xlsx,.xls,.csv">
            <a-button><UploadOutlined />选择Excel文件</a-button>
          </a-upload>
        </a-form-item>
        <a-form-item label="银行账号">
          <a-input v-model:value="importForm.bankAccount" placeholder="请输入银行账号" />
        </a-form-item>
        <a-form-item label="银行名称">
          <a-input v-model:value="importForm.bankName" placeholder="请输入银行名称" />
        </a-form-item>
        <a-divider>列映射配置</a-divider>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="起始数据行">
              <a-input-number v-model:value="importForm.startRow" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="日期列号">
              <a-input-number v-model:value="importForm.dateColumnIndex" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="摘要列号">
              <a-input-number v-model:value="importForm.descriptionColumnIndex" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="借方(收入)列号">
              <a-input-number v-model:value="importForm.debitColumnIndex" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="贷方(支出)列号">
              <a-input-number v-model:value="importForm.creditColumnIndex" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="余额列号">
              <a-input-number v-model:value="importForm.balanceColumnIndex" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="对方户名列号">
              <a-input-number v-model:value="importForm.counterpartyColumnIndex" :min="-1" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="流水号列号">
              <a-input-number v-model:value="importForm.referenceNoColumnIndex" :min="-1" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <div style="color: #909399; font-size: 12px;">列号从0开始，-1表示无此列</div>
      </a-form>
    </a-modal>

    <!-- 对账报告弹窗 -->
    <a-modal v-model:open="showReportModal" title="银行余额调节表" :width="720" :centered="true" :footer="null">
      <div v-if="reportLoading" style="text-align: center; padding: 24px;">
        <LoadingOutlined spin /> 正在生成报告...
      </div>
      <div v-else-if="report" class="report-content">
        <a-form-item label="选择账期" style="margin-bottom: 16px;">
          <a-select v-model:value="reportPeriodId" placeholder="选择账期" style="width: 200px" @change="loadReport"
            :options="periodList.map((p: any) => ({ label: p.name, value: p.id }))" />
        </a-form-item>
        <table class="report-table">
          <thead>
            <tr>
              <th>项目</th>
              <th style="text-align: right;">金额</th>
            </tr>
          </thead>
          <tbody>
            <tr class="section-header"><td colspan="2">银行对账单</td></tr>
            <tr><td>银行流水余额</td><td class="amount-cell">{{ formatAmount(report.bankBalance) }}</td></tr>
            <tr><td>加：企业已收银行未收</td><td class="amount-cell">{{ formatAmount(report.companyReceivedBankNot) }}</td></tr>
            <tr><td>减：企业已付银行未付</td><td class="amount-cell">{{ formatAmount(report.companyPaidBankNot) }}</td></tr>
            <tr class="total-row"><td>调节后银行余额</td><td class="amount-cell">{{ formatAmount(report.adjustedBankBalance) }}</td></tr>
            <tr class="section-header"><td colspan="2">企业账面</td></tr>
            <tr><td>账面余额</td><td class="amount-cell">{{ formatAmount(report.bookBalance) }}</td></tr>
            <tr><td>加：银行已收企业未收</td><td class="amount-cell">{{ formatAmount(report.bankReceivedCompanyNot) }}</td></tr>
            <tr><td>减：银行已付企业未付</td><td class="amount-cell">{{ formatAmount(report.bankPaidCompanyNot) }}</td></tr>
            <tr class="total-row"><td>调节后账面余额</td><td class="amount-cell">{{ formatAmount(report.adjustedBookBalance) }}</td></tr>
          </tbody>
        </table>
        <div v-if="report.adjustedBankBalance === report.adjustedBookBalance" class="balance-match">
          <CheckCircleOutlined style="color: var(--color-success);" /> 调节后余额一致
        </div>
        <div v-else class="balance-mismatch">
          <CloseCircleOutlined style="color: var(--color-danger);" />
          调节后余额不一致，差额：{{ formatAmount(report.adjustedBankBalance - report.adjustedBookBalance) }}
        </div>
      </div>
      <div v-else>
        <a-form-item label="选择账期">
          <a-select v-model:value="reportPeriodId" placeholder="选择账期" style="width: 200px" @change="loadReport"
            :options="periodList.map((p: any) => ({ label: p.name, value: p.id }))" />
        </a-form-item>
        <a-empty description="请选择账期后查看对账报告" />
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'
import {
  UploadOutlined, ThunderboltOutlined, LinkOutlined, FileTextOutlined,
  SearchOutlined, LoadingOutlined,
  CheckCircleOutlined, CloseCircleOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getBankStatements,
  getUnmatchedVouchers,
  importBankStatements,
  autoMatchBankStatements,
  manualMatchBankStatement,
  getReconciliationReport,
  getPeriods,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { usePermission, FinancePermissions } from '@/utils/permission'

const accountSetStore = useAccountSetStore()
const { has } = usePermission()

// 筛选
const matchFilter = ref<number | null>(null)
const dateRange = ref<[string, string] | undefined>(undefined)
const bankAccount = ref('')

// 银行流水
const statementsLoading = ref(false)
const statementList = ref<any[]>([])
const statementTotal = ref(0)
const matchedCount = ref(0)
const unmatchedCount = ref(0)
const selectedStatement = ref<any>(null)

// 凭证分录
const vouchersLoading = ref(false)
const voucherEntries = ref<any[]>([])
const selectedVoucher = ref<any>(null)

// 分页
const pagination = ref({ pageIndex: 1, pageSize: 50, total: 0 })

// 自动匹配
const autoMatchLoading = ref(false)

// 导入
const showImportModal = ref(false)
const importLoading = ref(false)
const importFileList = ref<UploadFile[]>([])
const importForm = ref({
  bankAccount: '',
  bankName: '',
  startRow: 2,
  dateColumnIndex: 0,
  descriptionColumnIndex: 1,
  debitColumnIndex: 2,
  creditColumnIndex: 3,
  balanceColumnIndex: 4,
  counterpartyColumnIndex: 5,
  referenceNoColumnIndex: -1,
})

// 对账报告
const showReportModal = ref(false)
const reportLoading = ref(false)
const report = ref<any>(null)
const reportPeriodId = ref<number | undefined>(undefined)
const periodList = ref<{ id: number; name: string; year: number; month: number }[]>([])

// 统计
const statementDiff = computed(() => {
  const debitSum = statementList.value.reduce((sum, s) => sum + (s.debitAmount || 0), 0)
  const creditSum = statementList.value.reduce((sum, s) => sum + (s.creditAmount || 0), 0)
  return debitSum - creditSum
})

// 表格列定义
const statementColumns = [
  { title: '日期', dataIndex: 'transactionDate', key: 'transactionDate', width: 100, align: 'center' as const },
  { title: '摘要', dataIndex: 'description', key: 'description', ellipsis: true, minWidth: 150 },
  { title: '借方(收入)', dataIndex: 'debitAmount', key: 'debitAmount', width: 110, align: 'right' as const },
  { title: '贷方(支出)', dataIndex: 'creditAmount', key: 'creditAmount', width: 110, align: 'right' as const },
  { title: '余额', dataIndex: 'balance', key: 'balance', width: 110, align: 'right' as const },
  { title: '对方户名', dataIndex: 'counterparty', key: 'counterparty', width: 120, ellipsis: true },
  { title: '状态', dataIndex: 'matchStatus', key: 'matchStatus', width: 80, align: 'center' as const },
]

const voucherColumns = [
  { title: '日期', dataIndex: 'date', key: 'date', width: 100, align: 'center' as const },
  { title: '凭证号', dataIndex: 'voucherNo', key: 'voucherNo', width: 100, align: 'center' as const },
  { title: '摘要', dataIndex: 'summary', key: 'summary', ellipsis: true, minWidth: 150 },
  { title: '借方', dataIndex: 'debitAmount', key: 'debitAmount', width: 110, align: 'right' as const },
  { title: '贷方', dataIndex: 'creditAmount', key: 'creditAmount', width: 110, align: 'right' as const },
]

// 加载账期
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .sort((a: any, b: any) => {
        if (a.year !== b.year) return b.year - a.year
        return b.periodNo - a.periodNo
      })
      .map((p: any) => ({
        id: p.id,
        name: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
        year: p.year,
        month: p.periodNo,
      }))
  } catch (error) {
    console.error('加载账期列表失败', error)
  }
}

// 加载银行流水
async function loadStatements() {
  statementsLoading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
    }
    if (matchFilter.value !== null) params.matchStatus = matchFilter.value
    if (dateRange.value && dateRange.value.length === 2) {
      params.startDate = dateRange.value[0]
      params.endDate = dateRange.value[1]
    }
    if (bankAccount.value) params.bankAccount = bankAccount.value

    const res = await getBankStatements(params)
    statementList.value = res.items || []
    pagination.value.total = res.total || 0
    statementTotal.value = res.total || 0
    matchedCount.value = res.matchedCount || 0
    unmatchedCount.value = res.unmatchedCount || 0
  } catch (error) {
    console.error('加载银行流水失败', error)
    statementList.value = []
  } finally {
    statementsLoading.value = false
  }
}

// 加载未匹配凭证分录
async function loadVouchers() {
  if (!dateRange.value || dateRange.value.length !== 2) {
    // 无日期范围时，使用默认近3个月
    const end = new Date()
    const start = new Date()
    start.setMonth(start.getMonth() - 3)
    const startStr = formatDateISO(start)
    const endStr = formatDateISO(end)
    vouchersLoading.value = true
    try {
      const res = await getUnmatchedVouchers({
        startDate: startStr,
        endDate: endStr,
        accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
      })
      voucherEntries.value = res || []
    } catch (error) {
      console.error('加载凭证分录失败', error)
      voucherEntries.value = []
    } finally {
      vouchersLoading.value = false
    }
    return
  }

  vouchersLoading.value = true
  try {
    const res = await getUnmatchedVouchers({
      startDate: dateRange.value[0],
      endDate: dateRange.value[1],
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
    })
    voucherEntries.value = res || []
  } catch (error) {
    console.error('加载凭证分录失败', error)
    voucherEntries.value = []
  } finally {
    vouchersLoading.value = false
  }
}

function loadData() {
  selectedStatement.value = null
  selectedVoucher.value = null
  loadStatements()
  loadVouchers()
}

function handleSearch() {
  pagination.value.pageIndex = 1
  loadData()
}

function onSelectStatement(record: any) {
  selectedStatement.value = selectedStatement.value?.id === record.id ? null : record
}

function onSelectVoucher(record: any) {
  selectedVoucher.value = selectedVoucher.value?.entryId === record.entryId ? null : record
}

// 文件上传
function beforeUpload(file: File) {
  importFileList.value = [{ uid: '-1', name: file.name, status: 'done', originFileObj: file } as any]
  return false
}

// 导入
async function handleImport() {
  if (importFileList.value.length === 0) {
    message.warning('请选择要导入的文件')
    return
  }

  importLoading.value = true
  try {
    const formData = new FormData()
    const file = (importFileList.value[0] as any).originFileObj
    formData.append('file', file)
    formData.append('bankAccount', importForm.value.bankAccount || '')
    formData.append('bankName', importForm.value.bankName || '')
    formData.append('startRow', String(importForm.value.startRow))
    formData.append('dateColumnIndex', String(importForm.value.dateColumnIndex))
    formData.append('descriptionColumnIndex', String(importForm.value.descriptionColumnIndex))
    formData.append('debitColumnIndex', String(importForm.value.debitColumnIndex))
    formData.append('creditColumnIndex', String(importForm.value.creditColumnIndex))
    formData.append('balanceColumnIndex', String(importForm.value.balanceColumnIndex))
    formData.append('counterpartyColumnIndex', String(importForm.value.counterpartyColumnIndex))
    formData.append('referenceNoColumnIndex', String(importForm.value.referenceNoColumnIndex))

    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await importBankStatements(formData, accountSetId) as any
    message.success(res?.message || `成功导入 ${res?.importCount || 0} 条银行流水`)
    showImportModal.value = false
    importFileList.value = []
    loadData()
  } catch (error) {
    message.error('导入失败')
  } finally {
    importLoading.value = false
  }
}

// 自动匹配
async function handleAutoMatch() {
  autoMatchLoading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await autoMatchBankStatements(accountSetId) as any
    message.success(res?.message || `自动匹配成功 ${res?.matchCount || 0} 笔`)
    loadData()
  } catch (error) {
    message.error('自动匹配失败')
  } finally {
    autoMatchLoading.value = false
  }
}

// 手动匹配
function handleManualMatch() {
  if (!selectedStatement.value || !selectedVoucher.value) {
    message.warning('请在左侧选择一条银行流水，在右侧选择一条凭证分录')
    return
  }

  if (selectedStatement.value.matchStatus === 1) {
    message.warning('该银行流水已匹配')
    return
  }

  Modal.confirm({
    title: '手动匹配确认',
    content: `确定将银行流水「${selectedStatement.value.description || ''}」与凭证「${selectedVoucher.value.voucherNo}」进行匹配？`,
    async onOk() {
      try {
        const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
        await manualMatchBankStatement({
          bankStatementId: selectedStatement.value.id,
          voucherId: selectedVoucher.value.voucherId,
          voucherEntryId: selectedVoucher.value.entryId,
        }, accountSetId)
        message.success('匹配成功')
        loadData()
      } catch (error) {
        message.error('匹配失败')
      }
    },
  })
}

// 对账报告
async function loadReport() {
  if (!reportPeriodId.value) return
  reportLoading.value = true
  try {
    const res = await getReconciliationReport({
      periodId: reportPeriodId.value,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
    })
    report.value = res
  } catch (error) {
    message.error('生成对账报告失败')
    report.value = null
  } finally {
    reportLoading.value = false
  }
}

// 分页
function handlePageChange(page: number) {
  pagination.value.pageIndex = page
  loadStatements()
}

function handleSizeChange(_current: number, size: number) {
  pagination.value.pageSize = size
  pagination.value.pageIndex = 1
  loadStatements()
}

// 工具函数
function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatDateISO(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function formatAmount(amount: number): string {
  if (amount === null || amount === undefined) return ''
  return amount.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, async () => {
  pagination.value.pageIndex = 1
  await loadPeriods()
  loadData()
})

// 监听报告弹窗
watch(showReportModal, (val) => {
  if (val && !reportPeriodId.value && periodList.value.length > 0) {
    const now = new Date()
    const currentYear = now.getFullYear()
    const currentMonth = now.getMonth() + 1
    const matched = periodList.value.find((p: any) => p.year === currentYear && p.month === currentMonth)
    reportPeriodId.value = matched ? matched.id : periodList.value[0].id
    loadReport()
  }
})

onMounted(() => {
  loadPeriods().then(() => loadData())
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

    &:hover { color: var(--color-primary); }
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
}

.reconciliation-body {
  display: flex;
  gap: $section-gap;
  flex: 1;
  overflow: hidden;
  min-height: 0;

  .panel-card {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;

    :deep(.ant-card-body) {
      flex: 1;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      padding: 12px;
      min-height: 0;
    }

    :deep(.ant-table-wrapper) {
      flex: 1;
      overflow: hidden;
      min-height: 0;
    }

    :deep(.ant-table) {
      height: 100%;
    }

    :deep(.ant-table-container) {
      height: 100%;
      display: flex;
      flex-direction: column;
    }

    :deep(.ant-table-body) {
      flex: 1;
      overflow-y: auto !important;
    }
  }
}

.panel-count {
  color: #909399;
  font-size: 13px;
}

.stats-bar {
  flex-shrink: 0;

  .stats-content {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 14px;

    strong {
      color: var(--color-info);
    }
  }
}

.amount {
  font-family: 'Courier New', monospace;
  font-weight: 500;
}

:deep(.selected-row) {
  background-color: var(--bg-muted) !important;

  td {
    background-color: var(--bg-muted) !important;
  }
}

.pagination-wrapper {
  flex-shrink: 0;
  display: flex;
  justify-content: flex-end;
  padding-top: $section-gap;
}

// 对账报告
.report-content {
  .report-table {
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 16px;

    th, td {
      padding: 8px 12px;
      border: 1px solid #e8e8e8;
      font-size: 14px;
    }

    th {
      background: #fafafa;
      font-weight: 500;
    }

    .section-header td {
      background: #f0f5ff;
      font-weight: 500;
      color: var(--color-info);
    }

    .total-row td {
      background: var(--color-warning-light);
      font-weight: 600;
    }

    .amount-cell {
      text-align: right;
      font-family: 'Courier New', monospace;
      font-weight: 500;
    }
  }

  .balance-match {
    text-align: center;
    padding: 12px;
    font-size: 15px;
    color: var(--color-success);
    font-weight: 500;
  }

  .balance-mismatch {
    text-align: center;
    padding: 12px;
    font-size: 15px;
    color: var(--color-danger);
    font-weight: 500;
  }
}
</style>
