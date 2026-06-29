<template>
  <div class="page-container">
    <PageHeader title="日记账">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button :type="showDraft ? 'primary' : 'default'" @click="toggleDraft"><FileOutlined />草稿箱</a-button>
        <a-button @click="handleGenerateVoucher" :disabled="selectedIds.length === 0"><FileDoneOutlined />生成凭证</a-button>
        <a-button @click="handleDelete" :disabled="selectedIds.length === 0"><DeleteOutlined />删除</a-button>
        <a-button type="primary" v-if="has(FinancePermissions.JournalCreate) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.CashJournalEdit)" @click="openCreateDialog"><PlusOutlined />新增</a-button>
        <a-button v-if="has(FinancePermissions.JournalAdjust) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.CashJournalEdit)" @click="openAdjustDialog"><EditOutlined />调整</a-button>
        <a-dropdown>
          <a-button>更多<DownOutlined /></a-button>
          <template #overlay>
            <a-menu @click="({ key }: any) => handleMoreCommand(key)">
              <a-menu-item key="export">导出</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-radio-group v-model:value="activeTab" button-style="solid" size="default" @change="handleTabChange">
              <a-radio-button value="all">全部</a-radio-button>
              <a-radio-button value="cashBank">现金银行日记账</a-radio-button>
              <a-radio-button value="receivablePayable">应收应付日记账</a-radio-button>
            </a-radio-group>
            <template v-if="queryMode === 'period'">
              <a-select v-model:value="selectedPeriodId" placeholder="选择账期" style="width: 160px" @change="handlePeriodChange"
                :options="periodList" :field-names="{ label: 'label', value: 'id' }" />
            </template>
            <template v-else-if="queryMode === 'date'">
              <a-date-picker v-model:value="selectedDate" placeholder="选择日期" style="width: 160px" value-format="YYYY-MM-DD" @change="handleSearch" />
            </template>
            <template v-else-if="queryMode === 'periodRange'">
              <a-select v-model:value="startPeriodId" placeholder="起始账期" style="width: 160px" @change="handleSearch"
                :options="periodList" :field-names="{ label: 'label', value: 'id' }" />
              <span class="range-sep">~</span>
              <a-select v-model:value="endPeriodId" placeholder="结束账期" style="width: 160px" @change="handleSearch"
                :options="periodList" :field-names="{ label: 'label', value: 'id' }" />
            </template>
            <template v-else-if="queryMode === 'dateRange'">
              <a-range-picker v-model:value="selectedDateRange" :placeholder="['开始日期', '结束日期']"
                style="width: 260px" value-format="YYYY-MM-DD" @change="handleSearch" />
            </template>
            <a-dropdown :trigger="['click']">
              <a-button class="query-mode-btn"><CalendarOutlined /></a-button>
              <template #overlay>
                <a-menu @click="({ key }: any) => handleQueryModeChange(key)">
                  <a-menu-item key="period" :class="{ 'is-active-mode': queryMode === 'period' }">按账期</a-menu-item>
                  <a-menu-item key="date" :class="{ 'is-active-mode': queryMode === 'date' }">按日期</a-menu-item>
                  <a-menu-item key="periodRange" :class="{ 'is-active-mode': queryMode === 'periodRange' }">按账期区间</a-menu-item>
                  <a-menu-item key="dateRange" :class="{ 'is-active-mode': queryMode === 'dateRange' }">按日期区间</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
            <a-select v-if="activeTab !== 'all'" v-model:value="selectedAccountCode" placeholder="全部账户"
              style="width: 200px" allowClear @change="handleSearch"
              :options="filteredAccountOptions" :field-names="{ label: 'label', value: 'code' }" />
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select v-model:value="searchField" style="width: 80px"
              :options="[{ label: '摘要', value: 'summary' }]" />
            <a-input v-model:value="searchKeyword" placeholder="搜索" style="width: 180px" allowClear @pressEnter="handleSearch">
              <template #suffix><SearchOutlined class="search-icon" @click="handleSearch" /></template>
            </a-input>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table :columns="tableColumns" :dataSource="tableData" :loading="loading" bordered
        :rowSelection="rowSelection" rowKey="id" :rowClassName="getRowClassName" :pagination="false"
        :scroll="{ y: 'calc(100% - 50px)' }">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'date'">
            <span v-if="record.isInitialBalance" class="initial-label">期初值</span>
            <span v-else>{{ formatDate(record.date) }}</span>
          </template>
          <template v-else-if="column.key === 'accountName'">
            <span v-if="record.accountCode">{{ record.accountCode }}_{{ record.accountName }}</span>
            <span v-else>{{ record.accountName }}</span>
          </template>
          <template v-else-if="column.key === 'debitAmount'">
            <span v-if="record.debitAmount" class="amount">{{ formatAmount(record.debitAmount) }}</span>
          </template>
          <template v-else-if="column.key === 'creditAmount'">
            <span v-if="record.creditAmount" class="amount">{{ formatAmount(record.creditAmount) }}</span>
          </template>
          <template v-else-if="column.key === 'direction'">
            <a-tag v-if="record.direction" :color="record.direction === '应收' ? 'success' : 'warning'">{{ record.direction }}</a-tag>
          </template>
          <template v-else-if="column.key === 'balance'">
            <span class="amount" :class="{ 'negative-value': record.balance < 0 }">{{ formatAmount(record.balance) }}</span>
          </template>
          <template v-else-if="column.key === 'voucherNo'">
            <a-tag v-if="record.voucherStatus === 0" color="default">草稿</a-tag>
            <a-tag v-else-if="record.voucherStatus === 1" color="warning">待审核</a-tag>
            <span v-else-if="record.voucherNo" class="voucher-no">{{ record.voucherNo }}</span>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
      <div class="pagination-wrapper">
        <a-pagination v-model:current="pagination.page" v-model:pageSize="pagination.pageSize"
          :page-size-options="['50', '100', '200']" :total="pagination.total" show-size-changer
          @change="handlePageChange" @showSizeChange="handleSizeChange" />
      </div>
    </a-card>

    <!-- 新增日记账弹窗 -->
    <a-modal v-model:open="createDialogVisible" title="新增日记账" :width="680" centered
      :maskClosable="false" :footer="null" @cancel="resetCreateForm">
      <div class="dialog-tabs">
        <a-tabs v-model:activeKey="createTab" @change="onCreateTabChange">
          <a-tab-pane key="income" tab="收入" />
          <a-tab-pane key="expense" tab="支出" />
          <a-tab-pane key="transfer" tab="收支" />
        </a-tabs>
      </div>
      <!-- 收入/支出表单 -->
      <a-form v-if="createTab !== 'transfer'" :model="incomeForm" :label-col="{ style: { width: '90px' } }" class="dialog-form">
        <a-form-item label="日期">
          <a-date-picker v-model:value="incomeForm.date" placeholder="选择日期" value-format="YYYY-MM-DD" style="width: 100%" />
        </a-form-item>
        <a-form-item label="类别">
          <a-select v-model:value="incomeForm.category" placeholder="请选择类别" style="width: 100%" allowClear
            :options="categoryOptions.map(c => ({ label: c, value: c }))" />
        </a-form-item>
        <a-form-item label="摘要"><a-input v-model:value="incomeForm.summary" placeholder="请输入摘要" /></a-form-item>
        <a-form-item label="金额"><a-input v-model:value="incomeForm.amount" placeholder="请输入金额" type="number" style="width: 100%" /></a-form-item>
        <a-form-item label="账户">
          <div class="account-row">
            <a-select v-model:value="incomeForm.accountId" placeholder="请选择账户" style="flex: 1" allowClear
              :options="cashBankAccounts" :field-names="{ label: 'label', value: 'id' }" />
            <div class="multi-account-toggle">
              <a-switch v-model:checked="incomeForm.multiAccount" />
              <span class="multi-label">多账户</span>
            </div>
          </div>
          <div v-if="incomeForm.multiAccount" class="multi-account-list">
            <a-select v-model:value="incomeForm.multiAccountIds" mode="multiple" placeholder="选择多个账户" style="width: 100%"
              :options="cashBankAccounts" :field-names="{ label: 'label', value: 'id' }" />
          </div>
        </a-form-item>
        <a-form-item label="附件">
          <div class="journal-attachment-area">
            <a-upload :customRequest="handleJournalUpload" :maxCount="9" :multiple="true" :showUploadList="false" :beforeUpload="beforeJournalUpload">
              <a-button size="small" type="primary" ghost><PlusOutlined />选择文件</a-button>
            </a-upload>
            <span class="paste-hint">支持 Ctrl+V 粘贴剪贴板图片</span>
          </div>
          <div v-if="journalPendingFiles.length > 0" class="journal-file-list">
            <div v-for="(f, idx) in journalPendingFiles" :key="idx" class="journal-file-item">
              <FileOutlined /><span class="jf-name" :title="f.name">{{ f.name }}</span>
              <span class="jf-size">{{ formatJournalFileSize(f.size) }}</span>
              <a-button type="link" danger size="small" @click="removeJournalPendingFile(idx)">移除</a-button>
            </div>
          </div>
        </a-form-item>
        <a-form-item><a-button type="link" @click="addInvoice"><PlusOutlined />添加发票</a-button></a-form-item>
      </a-form>
      <!-- 收支表单 -->
      <a-form v-else :model="transferForm" :label-col="{ style: { width: '100px' } }" class="dialog-form">
        <a-form-item label="">
          <a-radio-group v-model:value="transferForm.subType">
            <a-radio value="payment">收付款</a-radio>
            <a-radio value="internal-transfer">内部转账</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="日期">
          <a-date-picker v-model:value="transferForm.date" placeholder="选择日期" value-format="YYYY-MM-DD" style="width: 100%" />
        </a-form-item>
        <a-form-item label="摘要"><a-input v-model:value="transferForm.summary" placeholder="请输入摘要" /></a-form-item>
        <template v-if="transferForm.subType === 'payment'">
          <a-form-item label="核销方向">
            <a-radio-group v-model:value="transferForm.reconcileDirection">
              <a-radio value="receivable">应收核销</a-radio><a-radio value="payable">应付核销</a-radio>
            </a-radio-group>
          </a-form-item>
          <a-form-item label="核销账户">
            <div class="account-row">
              <a-select v-model:value="transferForm.reconcileAccountId" placeholder="请选择核销账户" style="flex: 1" allowClear
                :options="reconcileAccounts" :field-names="{ label: 'label', value: 'id' }" />
              <div class="multi-account-toggle"><a-switch v-model:checked="transferForm.multiAccount" /><span class="multi-label">多账户</span></div>
            </div>
          </a-form-item>
          <a-form-item label="核销金额">
            <a-input-number v-model:value="transferForm.reconcileAmount" :min="0" :precision="2" style="width: 100%" @change="syncTransferAmount" />
          </a-form-item>
          <a-form-item label="金额"><span class="readonly-amount">{{ formatAmount(transferForm.amount) }}</span></a-form-item>
          <a-form-item label="现金银行账户">
            <a-select v-model:value="transferForm.cashBankAccountId" placeholder="请选择账户" style="width: 100%" allowClear
              :options="cashBankAccounts" :field-names="{ label: 'label', value: 'id' }" />
          </a-form-item>
        </template>
        <template v-else>
          <a-form-item label="转出账户">
            <a-select v-model:value="transferForm.accountId" placeholder="请选择转出账户" style="width: 100%" allowClear
              :options="cashBankAccounts" :field-names="{ label: 'label', value: 'id' }" />
          </a-form-item>
          <a-form-item label="转入账户">
            <a-select v-model:value="transferForm.transferToAccountId" placeholder="请选择转入账户" style="width: 100%" allowClear
              :options="cashBankAccounts" :field-names="{ label: 'label', value: 'id' }" />
          </a-form-item>
          <a-form-item label="金额">
            <a-input-number v-model:value="transferForm.amount" :min="0" :precision="2" style="width: 100%" />
          </a-form-item>
        </template>
        <a-form-item label="附件">
          <div class="journal-attachment-area">
            <a-upload :customRequest="handleJournalUpload" :maxCount="9" :multiple="true" :showUploadList="false" :beforeUpload="beforeJournalUpload">
              <a-button size="small" type="primary" ghost><PlusOutlined />选择文件</a-button>
            </a-upload>
            <span class="paste-hint">支持 Ctrl+V 粘贴剪贴板图片</span>
          </div>
          <div v-if="journalPendingFiles.length > 0" class="journal-file-list">
            <div v-for="(f, idx) in journalPendingFiles" :key="idx" class="journal-file-item">
              <FileOutlined /><span class="jf-name" :title="f.name">{{ f.name }}</span>
              <span class="jf-size">{{ formatJournalFileSize(f.size) }}</span>
              <a-button type="link" danger size="small" @click="removeJournalPendingFile(idx)">移除</a-button>
            </div>
          </div>
        </a-form-item>
        <a-form-item><a-button type="link" @click="addInvoice"><PlusOutlined />添加发票</a-button></a-form-item>
      </a-form>
      <div class="dialog-footer" style="margin-top: 16px; border-top: 1px solid #f0f0f0; padding-top: 16px;">
        <a-button @click="handleSaveDraft">存为草稿</a-button>
        <div class="footer-right">
          <a-button @click="handleSave(false)">保存</a-button>
          <a-button type="primary" @click="handleSave(true)">保存并新增</a-button>
        </div>
      </div>
    </a-modal>

    <!-- 调整弹窗 -->
    <a-modal v-model:open="adjustDialogVisible" title="调整账户" :width="520" centered
      :maskClosable="false" :footer="null" @cancel="resetAdjustForm">
      <a-form :model="adjustForm" :label-col="{ style: { width: '100px' } }" class="dialog-form">
        <a-form-item label="日期">
          <a-date-picker v-model:value="adjustForm.date" placeholder="选择日期" value-format="YYYY-MM-DD" style="width: 100%" />
        </a-form-item>
        <a-form-item label="摘要"><a-input v-model:value="adjustForm.summary" placeholder="请输入摘要" /></a-form-item>
        <a-form-item label="账户">
          <div class="account-row">
            <a-select v-model:value="adjustForm.accountId" placeholder="请选择账户" style="flex: 1" allowClear
              :options="allLeafAccounts" :field-names="{ label: 'label', value: 'id' }" />
            <span class="adjust-direction-label">调整方向：贷方</span>
          </div>
        </a-form-item>
        <a-form-item label="调整金额">
          <a-input-number v-model:value="adjustForm.amount" :precision="2" style="width: 100%" />
        </a-form-item>
      </a-form>
      <div class="dialog-footer" style="margin-top: 16px; border-top: 1px solid #f0f0f0; padding-top: 16px;">
        <span></span>
        <div class="footer-right">
          <a-button @click="handleAdjustSave(false)">保存</a-button>
          <a-button type="primary" @click="handleAdjustSave(true)">保存并新增</a-button>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined, SearchOutlined, DownOutlined, CalendarOutlined,
  EditOutlined, DeleteOutlined, FileOutlined, FileDoneOutlined
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getJournalEntries, getCashBankJournal, getReceivablePayableJournal,
  createJournalEntry, adjustJournal, generateJournalVoucher, deleteJournalVoucher,
  getAccountTree, getPeriods, uploadAttachment
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { usePermission, FinancePermissions } from '@/utils/permission'
import { AccountSetPermissions } from '@/constants/accountSetPermissions'

const accountSetStore = useAccountSetStore()
const { has } = usePermission()

// ======================== 筛选状态 ========================
const activeTab = ref<'all' | 'cashBank' | 'receivablePayable'>('all')
const queryMode = ref<'period' | 'date' | 'periodRange' | 'dateRange'>('period')
const selectedPeriodId = ref<number | undefined>(undefined)
const selectedDate = ref('')
const startPeriodId = ref<number | undefined>(undefined)
const endPeriodId = ref<number | undefined>(undefined)
const selectedDateRange = ref<[string, string] | undefined>(undefined)
const selectedAccountCode = ref('')
const searchField = ref('summary')
const searchKeyword = ref('')
const showDraft = ref(false)
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])

// ======================== 数据 ========================
const loading = ref(false)
const tableData = ref<any[]>([])
const selectedIds = ref<number[]>([])
const selectedRowKeys = ref<number[]>([])
const pagination = ref({ page: 1, pageSize: 100, total: 0 })

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[]) => { selectedRowKeys.value = keys as number[]; selectedIds.value = keys as number[] }
}))

// 表格列
const tableColumns = computed(() => {
  const cols: any[] = [
    { title: '日期', dataIndex: 'date', key: 'date', width: 110, align: 'center' },
    { title: '摘要', dataIndex: 'summary', key: 'summary', ellipsis: true },
    { title: '类别', dataIndex: 'category', key: 'category', width: 120, filters: categoryFilters.value, onFilter: (value: string, record: any) => record.category === value },
    { title: '科目', dataIndex: 'accountName', key: 'accountName', ellipsis: true },
    { title: '借方金额', dataIndex: 'debitAmount', key: 'debitAmount', width: 130, align: 'right' },
    { title: '贷方金额', dataIndex: 'creditAmount', key: 'creditAmount', width: 130, align: 'right' },
  ]
  if (activeTab.value === 'receivablePayable') {
    cols.push({ title: '方向', dataIndex: 'direction', key: 'direction', width: 90, align: 'center' })
  }
  if (activeTab.value !== 'all') {
    cols.push({ title: '余额', dataIndex: 'balance', key: 'balance', width: 130, align: 'right' })
  }
  cols.push({ title: '凭证字号', dataIndex: 'voucherNo', key: 'voucherNo', width: 120, align: 'center' })
  return cols
})

// 科目列表
const cashBankAccounts = ref<{ id: number; code: string; label: string }[]>([])
const receivablePayableAccounts = ref<{ id: number; code: string; label: string }[]>([])
const allLeafAccounts = ref<{ id: number; code: string; label: string }[]>([])

const filteredAccountOptions = computed(() => {
  if (activeTab.value === 'cashBank') return cashBankAccounts.value
  if (activeTab.value === 'receivablePayable') return receivablePayableAccounts.value
  return []
})
const reconcileAccounts = computed(() => receivablePayableAccounts.value)
const categoryFilters = computed(() => {
  const cats = new Set<string>()
  tableData.value.forEach(row => { if (row.category) cats.add(row.category) })
  return Array.from(cats).map(c => ({ text: c, value: c }))
})
const categoryOptions = ['收入', '支出', '转账', '其他']

// ======================== 加载期间 ========================
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.currentAccountSetId || 0
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .sort((a: any, b: any) => { if (a.year !== b.year) return b.year - a.year; return b.periodNo - a.periodNo })
      .map((p: any) => ({ id: p.id, label: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`, year: p.year, month: p.periodNo }))
    if (periodList.value.length > 0) {
      const now = new Date(); const currentYear = now.getFullYear(); const currentMonth = now.getMonth() + 1
      let defaultPeriod = periodList.value.find((p: any) => p.year === currentYear && p.month === currentMonth)
      if (!defaultPeriod) defaultPeriod = periodList.value[0]
      selectedPeriodId.value = defaultPeriod.id
      startPeriodId.value = periodList.value[periodList.value.length - 1].id
      endPeriodId.value = defaultPeriod.id
    }
  } catch (e) { console.error('加载期间失败', e) }
}

// ======================== 加载科目 ========================
async function loadAccounts() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const [, , allRes] = await Promise.all([
      getAccountTree(undefined, accountSetId) as Promise<any[]>,
      getAccountTree(undefined, accountSetId) as Promise<any[]>,
      getAccountTree(undefined, accountSetId) as Promise<any[]>,
    ])
    const flatLeafs = (nodes: any[]): any[] => {
      const result: any[] = []; const walk = (list: any[]) => { for (const node of list) { if (!node.children || node.children.length === 0) { result.push(node) } else { walk(node.children) } } }; walk(nodes); return result
    }
    const allLeafs = flatLeafs(allRes || [])
    cashBankAccounts.value = allLeafs.filter(n => n.code?.startsWith('1001') || n.code?.startsWith('1002')).map(n => ({ id: n.id, code: n.code, label: `${n.code} ${n.name}` }))
    receivablePayableAccounts.value = allLeafs.filter(n => n.code?.startsWith('1122') || n.code?.startsWith('2202')).map(n => ({ id: n.id, code: n.code, label: `${n.code} ${n.name}` }))
    allLeafAccounts.value = allLeafs.map(n => ({ id: n.id, code: n.code, label: `${n.code} ${n.name}` }))
  } catch (err) { console.error('加载科目失败', err) }
}

// ======================== 加载数据 ========================
async function loadData() {
  loading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const params: any = { accountSetId, page: pagination.value.page, pageSize: pagination.value.pageSize, searchField: searchField.value, searchText: searchKeyword.value || undefined, accountCode: selectedAccountCode.value || undefined, status: showDraft.value ? 'draft' : undefined }
    switch (queryMode.value) {
      case 'period': { params.queryMode = 'period'; const p = periodList.value.find(x => x.id === selectedPeriodId.value); if (p) { params.year = p.year; params.month = p.month } break }
      case 'date': params.queryMode = 'date'; if (selectedDate.value) { params.startDate = selectedDate.value; params.endDate = selectedDate.value } break
      case 'periodRange': { params.queryMode = 'period-range'; const sp = periodList.value.find(x => x.id === startPeriodId.value); const ep = periodList.value.find(x => x.id === endPeriodId.value); if (sp) { params.startYear = sp.year; params.startMonth = sp.month } if (ep) { params.endYear = ep.year; params.endMonth = ep.month } break }
      case 'dateRange': params.queryMode = 'date-range'; if (selectedDateRange.value) { params.startDate = selectedDateRange.value[0]; params.endDate = selectedDateRange.value[1] } break
    }
    let res: any
    if (activeTab.value === 'cashBank') res = await getCashBankJournal(params)
    else if (activeTab.value === 'receivablePayable') res = await getReceivablePayableJournal(params)
    else res = await getJournalEntries(params)
    tableData.value = res?.items || res?.data?.items || []
    pagination.value.total = res?.total || res?.data?.total || 0
  } catch (err) { console.error('加载日记账失败', err); tableData.value = [] } finally { loading.value = false }
}

// ======================== 事件处理 ========================
function handleTabChange() { selectedAccountCode.value = ''; pagination.value.page = 1; loadData() }
function handleQueryModeChange(mode: string) { queryMode.value = mode as any; pagination.value.page = 1; loadData() }
function handleSearch() { pagination.value.page = 1; loadData() }
function handlePeriodChange() { handleSearch() }
function toggleDraft() { showDraft.value = !showDraft.value; loadData() }
function getRowClassName(record: any) { return record.isInitialBalance ? 'initial-balance-row' : '' }
function handleSizeChange(_current: number, size: number) { pagination.value.pageSize = size; pagination.value.page = 1; loadData() }
function handlePageChange(page: number) { pagination.value.page = page; loadData() }
function handleMoreCommand(cmd: string) { if (cmd === 'export') exportToExcel() }

function exportToExcel() {
  const data = tableData.value
  if (!data.length) { message.warning('暂无数据可导出'); return }
  const headers = ['日期', '摘要', '类别', '科目', '借方金额', '贷方金额', '余额', '凭证字号']
  const rows = data.map((row: any) => [row.isInitialBalance ? '期初' : formatDate(row.date), row.summary ?? '', row.category ?? '', row.accountCode ? `${row.accountCode}_${row.accountName}` : (row.accountName ?? ''), row.debitAmount ?? '', row.creditAmount ?? '', row.balance ?? '', row.voucherNo ?? ''])
  const csv = [headers, ...rows].map(r => r.map((v: any) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = `日记账_${new Date().toISOString().slice(0, 10)}.csv`; a.click(); URL.revokeObjectURL(url)
  message.success('导出成功')
}

function handleGenerateVoucher() {
  if (selectedIds.value.length === 0) { message.warning('请先选择要生成凭证的记录'); return }
  Modal.confirm({
    title: '生成凭证确认', content: `确定将选中的 ${selectedIds.value.length} 条记录生成凭证？`,
    okText: '确定生成', cancelText: '取消',
    async onOk() {
      try {
        const accountSetId = accountSetStore.getCurrentAccountSetId()
        await generateJournalVoucher({ entryIds: selectedIds.value, accountSetId })
        message.success(`成功生成 ${selectedIds.value.length} 张凭证`)
        selectedIds.value = []; selectedRowKeys.value = []; loadData()
      } catch (err: any) { if (err?.message) message.error(err.message || '生成凭证失败') }
    }
  })
}

function handleDelete() {
  if (selectedIds.value.length === 0) { message.warning('请选择要删除的条目'); return }
  Modal.confirm({
    title: '警告', content: `确定删除选中的 ${selectedIds.value.length} 条记录？`,
    async onOk() {
      const accountSetId = accountSetStore.getCurrentAccountSetId(); let success = 0
      for (const id of selectedIds.value) { try { await deleteJournalVoucher(id, accountSetId); success++ } catch (e) { console.error('删除失败', id, e) } }
      message.success(`成功删除 ${success} 条记录`); loadData()
    }
  })
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''; const d = new Date(dateStr); if (isNaN(d.getTime())) return dateStr
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
function formatAmount(amount: number): string {
  if (amount === undefined || amount === null) return '0.00'
  return Number(amount).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// ======================== 新增弹窗 ========================
const createDialogVisible = ref(false)
const createTab = ref<'income' | 'expense' | 'transfer'>('income')
const defaultIncomeForm = () => ({ date: new Date().toISOString().split('T')[0], category: '', summary: '', amount: '', accountId: undefined as number | undefined, multiAccount: false, multiAccountIds: [] as number[], attachmentCount: 0 })
const defaultTransferForm = () => ({ date: new Date().toISOString().split('T')[0], summary: '', subType: 'payment' as 'payment' | 'internal-transfer', reconcileDirection: 'receivable' as 'receivable' | 'payable', reconcileAccountId: undefined as number | undefined, reconcileAmount: 0, amount: 0, cashBankAccountId: undefined as number | undefined, accountId: undefined as number | undefined, transferToAccountId: undefined as number | undefined, multiAccount: false, attachmentCount: 0 })
const incomeForm = ref(defaultIncomeForm())
const transferForm = ref(defaultTransferForm())

function openCreateDialog() { createDialogVisible.value = true; createTab.value = 'income' }
function resetCreateForm() { incomeForm.value = defaultIncomeForm(); transferForm.value = defaultTransferForm(); resetJournalAttachments() }
function onCreateTabChange(tab: string | number) { if (tab === 'expense') incomeForm.value.amount = '0' }
function addInvoice() { message.info('添加发票功能开发中') }
function syncTransferAmount() { transferForm.value.amount = transferForm.value.reconcileAmount }
async function handleSaveDraft() { await submitCreate(true) }
async function handleSave(andNew: boolean) { await submitCreate(false); if (andNew) { resetCreateForm() } else { createDialogVisible.value = false } }

async function submitCreate(saveAsDraft: boolean) {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  try {
    let data: any
    if (createTab.value !== 'transfer') {
      data = { type: createTab.value, date: incomeForm.value.date, category: incomeForm.value.category, summary: incomeForm.value.summary, amount: Number(incomeForm.value.amount) || 0, accountId: incomeForm.value.accountId, multiAccountIds: incomeForm.value.multiAccount ? incomeForm.value.multiAccountIds : undefined, attachmentCount: incomeForm.value.attachmentCount, accountSetId, saveAsDraft }
    } else {
      data = { type: 'transfer', date: transferForm.value.date, summary: transferForm.value.summary, subType: transferForm.value.subType, reconcileDirection: transferForm.value.reconcileDirection, reconcileAccountId: transferForm.value.reconcileAccountId, reconcileAmount: transferForm.value.reconcileAmount, amount: transferForm.value.amount, cashBankAccountId: transferForm.value.cashBankAccountId, accountId: transferForm.value.accountId, transferToAccountId: transferForm.value.transferToAccountId, attachmentCount: transferForm.value.attachmentCount, accountSetId, saveAsDraft }
    }
    const res: any = await createJournalEntry(data)
    message.success(saveAsDraft ? '已存为草稿' : '保存成功')
    const voucherId = typeof res === 'number' ? res : res?.id || res
    if (voucherId && journalPendingFiles.value.length > 0) await uploadJournalAttachments('journal', voucherId, accountSetId || 0)
    if (createTab.value !== 'transfer') incomeForm.value.attachmentCount = 0; else transferForm.value.attachmentCount = 0
    loadData()
  } catch (err: any) { message.error(err?.message || '保存失败') }
}

// ======================== 调整弹窗 ========================
const adjustDialogVisible = ref(false)
const defaultAdjustForm = () => ({ date: new Date().toISOString().split('T')[0], summary: '', accountId: undefined as number | undefined, direction: 'credit', amount: 0 })
const adjustForm = ref(defaultAdjustForm())
function openAdjustDialog() { adjustDialogVisible.value = true }
function resetAdjustForm() { adjustForm.value = defaultAdjustForm() }

async function handleAdjustSave(andNew: boolean) {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  try {
    await adjustJournal({ date: adjustForm.value.date, summary: adjustForm.value.summary, accountId: adjustForm.value.accountId, direction: adjustForm.value.direction, amount: adjustForm.value.amount, accountSetId, saveAsDraft: false })
    message.success('调整成功'); loadData()
    if (andNew) resetAdjustForm(); else adjustDialogVisible.value = false
  } catch (err: any) { message.error(err?.message || '调整失败') }
}

// ======================== 监听 ========================
watch(() => accountSetStore.currentAccountSetId, async () => {
  pagination.value.page = 1; selectedPeriodId.value = undefined; startPeriodId.value = undefined; endPeriodId.value = undefined
  await loadPeriods(); loadAccounts(); loadData()
})
onMounted(() => { loadPeriods(); loadAccounts(); loadData(); document.addEventListener('paste', handlePasteInDialog) })
onUnmounted(() => { document.removeEventListener('paste', handlePasteInDialog) })

// ======================== 附件上传 ========================
interface JournalPendingFile { file: File; name: string; size: number }
const journalPendingFiles = ref<JournalPendingFile[]>([])

function handleJournalUpload(options: any) { addJournalPendingFile(options.file as File) }
function addJournalPendingFile(file: File) {
  if (journalPendingFiles.value.length >= 9) { message.warning('最多上传 9 个附件'); return }
  journalPendingFiles.value.push({ file, name: file.name, size: file.size })
}
function removeJournalPendingFile(idx: number) { journalPendingFiles.value.splice(idx, 1) }
function beforeJournalUpload(file: File) { if (file.size > 50 * 1024 * 1024) { message.error('文件大小不能超过 50MB'); return false } return true }

function handlePasteInDialog(e: ClipboardEvent) {
  if (!createDialogVisible.value) return
  const items = e.clipboardData?.items; if (!items) return
  for (const item of Array.from(items)) {
    if (item.kind === 'file' && item.type.startsWith('image/')) {
      const file = item.getAsFile()
      if (file) { addJournalPendingFile(new File([file], `clipboard_${Date.now()}.png`, { type: file.type })); message.success('已添加剪贴板图片') }
    }
  }
}

async function uploadJournalAttachments(businessType: string, businessId: number, accountSetId: number) {
  for (const pf of journalPendingFiles.value) {
    const formData = new FormData(); formData.append('file', pf.file); formData.append('accountSetId', String(accountSetId)); formData.append('businessType', businessType); formData.append('businessId', String(businessId))
    try { await uploadAttachment(formData) } catch (e) { console.error('附件上传失败', pf.name, e) }
  }
  journalPendingFiles.value = []
}
function resetJournalAttachments() { journalPendingFiles.value = [] }
function formatJournalFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'; if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'; return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;
.filter-bar {
  display: flex; justify-content: space-between; align-items: center; flex-wrap: nowrap; gap: 8px;
  .filter-left { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }
  .filter-right { display: flex; align-items: center; gap: 8px; .search-icon { cursor: pointer; color: #909399; &:hover { color: var(--color-primary); } } }
}
.query-mode-btn { padding: 8px; min-width: 32px; }
.range-sep { color: #909399; margin: 0 2px; }
.is-active-mode { background-color: var(--bg-muted) !important; color: var(--text-1) !important; font-weight: 500; }
.amount { font-family: 'Courier New', monospace; font-weight: 500; }
.negative-value { color: var(--color-danger); }
.initial-label { font-size: 12px; color: #909399; background: #f4f4f5; padding: 2px 6px; border-radius: 3px; }
:deep(.initial-balance-row) { background-color: #f9f9f9 !important; font-style: italic; color: #909399; }
.voucher-no { font-size: 13px; color: var(--text-1); }
.pagination-wrapper { display: flex; justify-content: flex-end; padding-top: $section-gap; }

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
.dialog-tabs { border-bottom: 1px solid #e4e7ed; margin-bottom: 16px; }
.dialog-form { padding: 8px 0; }
.account-row { display: flex; align-items: center; gap: 12px; width: 100%; }
.multi-account-toggle { display: flex; align-items: center; gap: 6px; flex-shrink: 0; .multi-label { font-size: 13px; color: #606266; white-space: nowrap; } }
.multi-account-list { margin-top: 8px; width: 100%; }
.adjust-direction-label { font-size: 13px; color: #909399; white-space: nowrap; flex-shrink: 0; }
.readonly-amount { font-size: 15px; font-weight: 600; color: #303133; font-family: 'Courier New', monospace; }
.dialog-footer { display: flex; justify-content: space-between; align-items: center; .footer-right { display: flex; gap: 8px; } }
.journal-attachment-area { display: flex; align-items: center; gap: 10px; margin-bottom: 6px; .paste-hint { font-size: 12px; color: #909399; } }
.journal-file-list {
  display: flex; flex-direction: column; gap: 4px; width: 100%;
  .journal-file-item {
    display: flex; align-items: center; gap: 6px; padding: 4px 8px; background: #f5f7fa; border: 1px solid #e4e7ed; border-radius: 4px;
    .jf-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; color: #333; }
    .jf-size { font-size: 12px; color: #999; flex-shrink: 0; }
  }
}
</style>
