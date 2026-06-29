<template>
  <div class="page-container">
    <!-- 无可用账套时显示空状态 -->
    <a-empty v-if="!accountSetStore.loading && !accountSetStore.hasAvailableAccountSets" description="暂无可用账套，请联系管理员授权" style="margin-top: 120px">
      <template #image>
        <LockOutlined style="font-size: 48px; color: #999" />
      </template>
    </a-empty>

    <!-- 正常内容 -->
    <template v-else>
    <PageHeader title="凭证管理">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button v-if="has(FinancePermissions.VoucherCreate) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherCreate)" type="primary" class="btn-primary-brand" @click="goToEntry">
          <template #icon><PlusOutlined /></template>新增凭证
        </a-button>
        <a-button @click="handleCopy" :disabled="selectedIds.length !== 1">复制</a-button>
        <a-button @click="handleReverse" :disabled="selectedIds.length !== 1" class="btn-warning">冲销</a-button>
        <a-button v-if="has(FinancePermissions.VoucherAudit) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherAudit)" @click="batchAudit" :disabled="selectedIds.length === 0">审核</a-button>
        <a-button v-if="has(FinancePermissions.VoucherDelete) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherDelete)" :disabled="selectedIds.length === 0" @click="batchDelete">删除</a-button>
        <a-dropdown :trigger="['click']">
          <a-button>
            更多<DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="handleMoreCommand">
              <a-menu-item key="export">导出</a-menu-item>
              <a-menu-item key="import">导入</a-menu-item>
              <a-menu-divider />
              <a-menu-item key="checkGap">断号检查</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <a-button v-if="has(FinancePermissions.VoucherPrint) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherPrint)" @click="handlePrint" :disabled="selectedIds.length === 0">打印</a-button>
      </template>
      <template #toolbar>
        <div class="toolbar-row">
          <!-- 左侧：胶囊式 Tab 切换 -->
          <div class="tab-group">
            <span
              class="tab-item"
              :class="{ active: filterStatus === '' }"
              @click="filterStatus = ''; onStatusChange()"
            >全部 <span class="tab-count">{{ totalCount }}</span></span>
            <span
              class="tab-item"
              :class="{ active: filterStatus === 'pending' }"
              @click="filterStatus = 'pending'; onStatusChange()"
            >待审核 <span class="tab-count">{{ pendingCount }}</span></span>
            <span
              class="tab-item"
              :class="{ active: filterStatus === 'recording' }"
              @click="filterStatus = 'recording'; onStatusChange()"
            >待补录 <span v-if="pendingRecordCount > 0" class="tab-count tab-count-recording">{{ pendingRecordCount }}</span></span>
          </div>

          <!-- 右侧：筛选控件 -->
          <div class="filter-group">
            <!-- 查询模式对应的日期/账期控件 -->
            <template v-if="queryMode === 'period'">
              <a-select v-model:value="selectedPeriodId" placeholder="选择账期" style="width: 160px" @change="handleSearch"
                :options="periodList.map(p => ({ label: p.name, value: p.id }))" />
            </template>
            <template v-else-if="queryMode === 'date'">
              <a-date-picker
                v-model:value="selectedDate"
                placeholder="选择日期"
                style="width: 160px"
                valueFormat="YYYY-MM-DD"
              />
            </template>
            <template v-else-if="queryMode === 'periodRange'">
              <a-select v-model:value="startPeriodId" placeholder="起始账期" style="width: 130px" @change="handleSearch"
                :options="periodList.map(p => ({ label: p.name, value: p.id }))" />
              <span class="range-sep">~</span>
              <a-select v-model:value="endPeriodId" placeholder="结束账期" style="width: 130px" @change="handleSearch"
                :options="periodList.map(p => ({ label: p.name, value: p.id }))" />
            </template>
            <template v-else-if="queryMode === 'dateRange'">
              <a-range-picker
                v-model:value="selectedDateRange"
                style="width: 260px"
                valueFormat="YYYY-MM-DD"
              />
            </template>

            <!-- 查询模式切换 -->
            <a-dropdown :trigger="['click']">
              <a-button class="query-mode-btn">
                <ControlOutlined />
              </a-button>
              <template #overlay>
                <a-menu @click="({ key }: any) => handleQueryModeChange(key)">
                  <a-menu-item key="period" :class="{ 'query-mode-active': queryMode === 'period' }">按账期查询</a-menu-item>
                  <a-menu-item key="date" :class="{ 'query-mode-active': queryMode === 'date' }">按日期查询</a-menu-item>
                  <a-menu-item key="periodRange" :class="{ 'query-mode-active': queryMode === 'periodRange' }">按账期区间查询</a-menu-item>
                  <a-menu-item key="dateRange" :class="{ 'query-mode-active': queryMode === 'dateRange' }">按日期区间查询</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>

            <a-select v-model:value="searchField" style="width: 90px"
              :options="[{ label: '摘要', value: 'summary' }, { label: '科目', value: 'account' }, { label: '凭证号', value: 'voucherNumber' }]" />
            <a-input
              v-model:value="searchKeyword"
              placeholder="搜索凭证"
              style="width: 200px"
              allowClear
              @input="debouncedSearch"
              @pressEnter="handleSearch"
            >
              <template #suffix>
                <SearchOutlined class="search-icon" @click="handleSearch" />
              </template>
            </a-input>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 批量操作提示条 -->
    <div v-if="selectedIds.length > 0" class="batch-bar">
      <span class="batch-info">已选 <strong>{{ selectedIds.length }}</strong> 张凭证</span>
      <div class="batch-actions">
        <a-button size="small" @click="handleCopy" :disabled="selectedIds.length !== 1">复制</a-button>
        <a-button size="small" @click="handleReverse" :disabled="selectedIds.length !== 1">冲销</a-button>
        <a-button v-if="has(FinancePermissions.VoucherAudit) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherAudit)" size="small" type="primary" @click="batchAudit">批量审核</a-button>
        <a-button v-if="has(FinancePermissions.VoucherDelete) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherDelete)" size="small" danger @click="batchDelete">批量删除</a-button>
        <a-button v-if="has(FinancePermissions.VoucherPrint) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.VoucherPrint)" size="small" @click="handlePrint">打印</a-button>
        <a-button size="small" @click="selectedIds = []">取消选择</a-button>
      </div>
    </div>

    <!-- 凭证列表 -->
    <div class="table-card">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        rowKey="uniqueId"
        :loading="loading"
        :bordered="false"
        :rowClassName="getRowClassName"
        :pagination="false"
        :scroll="{ x: 1200, y: 'calc(100% - 50px)' }"
        @change="handleTableChange"
      >
        <template #headerCell="{ column }">
          <template v-if="column.key === 'selection'">
            <a-checkbox
              :checked="isAllSelected"
              :indeterminate="isIndeterminate"
              @change="handleSelectAll"
            />
          </template>
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'selection'">
            <a-checkbox
              :checked="selectedIds.includes(record.voucherId)"
              @change="(e: any) => handleRowSelect(record.voucherId, e.target.checked)"
            />
          </template>
          <template v-if="column.dataIndex === 'date'">
            <span>{{ record.date }}</span>
          </template>
          <template v-if="column.dataIndex === 'voucherNumber'">
            <a
              class="voucher-link"
              @click="editVoucher(record.voucherId)"
            >
              {{ record.voucherWord }}-{{ record.voucherNumber }}
            </a>
            <a-tag v-if="record.remark && record.remark.includes('[待补录]')" color="orange" style="margin-left: 4px;">待补录</a-tag>
          </template>
          <template v-if="column.dataIndex === 'accountName'">
            <span v-if="record.accountCode">{{ record.accountCode }}_{{ record.accountName }}</span>
            <span v-else>{{ record.accountName }}</span>
          </template>
          <template v-if="column.dataIndex === 'debitAmount'">
            <span v-if="record.debitAmount" class="amount" :class="{ 'negative-value': record.debitAmount < 0 }">{{ formatAmount(record.debitAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'creditAmount'">
            <span v-if="record.creditAmount" class="amount" :class="{ 'negative-value': record.creditAmount < 0 }">{{ formatAmount(record.creditAmount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'creator'">
            <span>{{ record.creator }}</span>
          </template>
          <template v-if="column.dataIndex === 'auditor'">
            <span>{{ record.auditor || '' }}</span>
          </template>
          <template v-if="column.key === 'action'">
            <a-button
              v-if="record.remark && record.remark.includes('[待补录]')"
              type="link"
              size="small"
              @click="goToRecord(record.voucherId)"
            >
              去补录
            </a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>

      <!-- 分页 -->
      <div class="pagination-wrapper">
        <a-pagination
          v-model:current="pagination.pageIndex"
          v-model:pageSize="pagination.pageSize"
          :pageSizeOptions="['50', '100', '200', '500']"
          :total="pagination.total"
          showSizeChanger
          @change="handlePageChange"
          @showSizeChange="handleSizeChange"
        />
      </div>
    </div>
    <!-- 断号检查弹窗 -->
    <a-modal v-model:open="gapDialogVisible" title="凭证断号检查" :width="480" :centered="true" class="gap-check-modal">
    <div v-if="gapLoading" class="gap-loading">
      <LoadingOutlined spin /> 正在检查...
    </div>
    <div v-else-if="gapResult">
      <div v-if="gapResult.missingNos && gapResult.missingNos.length === 0" class="gap-success">
        <CheckCircleOutlined />
        <span>当前期间凭证号连续，无断号！（共 {{ gapResult.totalCount }} 张，号段 {{ gapResult.minNo }}-{{ gapResult.maxNo }}）</span>
      </div>
      <div v-else>
        <div class="gap-error">
          <CloseCircleOutlined />
          <span>发现 {{ gapResult.missingNos.length }} 个断号！（共 {{ gapResult.totalCount }} 张凭证）</span>
        </div>
        <a-tag
          v-for="no in gapResult.missingNos"
          :key="no"
          color="error"
          class="gap-tag"
        >缺 {{ no }} 号</a-tag>
      </div>
    </div>
    <template #footer>
      <a-button @click="gapDialogVisible = false">关闭</a-button>
    </template>
    </a-modal>

    <!-- 导入凭证弹窗 -->
    <a-modal
      v-model:open="importModalVisible"
      title="导入凭证"
      @ok="handleImport"
      :confirmLoading="importing"
      :destroyOnClose="true"
    >
      <a-upload-dragger
        v-model:fileList="importFileList"
        :beforeUpload="() => false"
        :maxCount="1"
        accept=".xlsx,.xls"
      >
        <p class="ant-upload-drag-icon"><InboxOutlined /></p>
        <p class="ant-upload-text">点击或拖拽Excel文件到此区域</p>
      </a-upload-dragger>
      <div style="margin-top: 12px;">
        <a @click="downloadImportTemplate" style="color: var(--text-1);">下载导入模板</a>
      </div>
    </a-modal>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, h, onMounted, onActivated, onDeactivated, computed, watch } from 'vue'
import { useDebounceFn } from '@/utils/debounce'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined, SearchOutlined, DownOutlined, ControlOutlined,
  LoadingOutlined, CheckCircleOutlined, CloseCircleOutlined, InboxOutlined, LockOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getVoucherList,
  deleteVoucher,
  getPeriods,
  copyVoucher,
  reverseVoucher,
  batchAuditVouchers,
  checkVoucherGap,
  exportVouchers,
  exportVoucherTemplate,
  importVouchers,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { usePermission, FinancePermissions } from '@/utils/permission'
import { AccountSetPermissions } from '@/constants/accountSetPermissions'
import { downloadBlob } from '@/utils/download'

const router = useRouter()
const accountSetStore = useAccountSetStore()
const { has } = usePermission()

const getCellSpan = (record: any) => {
  const style: Record<string, string> = { verticalAlign: 'middle' }
  // 合并单元格（rowSpan > 1）属于首行DOM，不受 .voucher-last-row td 影响，
  // 需要显式设置与凭证分隔线一致的下边框
  const cls: string[] = []
  if (record.rowSpan > 1) {
    cls.push('voucher-merged-cell')
  }
  return {
    rowSpan: record.rowSpan,
    style,
    class: cls.join(' ') || undefined
  }
}

const columns = [
  { title: '', dataIndex: 'selection', key: 'selection', width: 50, align: 'center' as const, customCell: getCellSpan },
  { title: '日期', dataIndex: 'date', key: 'date', width: 120, align: 'center' as const, sorter: true, customCell: getCellSpan },
  { title: '凭证号', dataIndex: 'voucherNumber', key: 'voucherNumber', width: 100, align: 'center' as const, sorter: true, customCell: getCellSpan },
  { title: '摘要', dataIndex: 'summary', key: 'summary', minWidth: 250, ellipsis: true },
  { title: '科目', dataIndex: 'accountName', key: 'accountName', minWidth: 200, ellipsis: true },
  { title: '借方金额', dataIndex: 'debitAmount', key: 'debitAmount', width: 130, align: 'right' as const },
  { title: '贷方金额', dataIndex: 'creditAmount', key: 'creditAmount', width: 130, align: 'right' as const },
  { title: '制单人', dataIndex: 'creator', key: 'creator', width: 100, align: 'center' as const, customCell: getCellSpan },
  { title: '审核人', dataIndex: 'auditor', key: 'auditor', width: 100, align: 'center' as const, customCell: getCellSpan },
  { title: '操作', key: 'action', width: 80, align: 'center' as const, fixed: 'right' as const, customCell: getCellSpan },
]

// 查询模式
const queryMode = ref<'period' | 'date' | 'periodRange' | 'dateRange'>('period')
const periodList = ref<{ id: number; name: string }[]>([])
const selectedPeriodId = ref<number | undefined>(undefined)
const selectedDate = ref('')
const startPeriodId = ref<number | undefined>(undefined)
const endPeriodId = ref<number | undefined>(undefined)
const selectedDateRange = ref<[string, string] | undefined>(undefined)

// 筛选状态
const filterStatus = ref('')
const searchField = ref('summary')
const searchKeyword = ref('')

// 数据
const loading = ref(false)
const voucherList = ref<any[]>([])
const pendingCount = ref(0)
const pendingRecordCount = ref(0)
const totalCount = ref(0)
const selectedIds = ref<number[]>([])

// 分页
const pagination = ref({
  pageIndex: 1,
  pageSize: 200,
  total: 0
})

// 断号检查
const gapDialogVisible = ref(false)
const gapLoading = ref(false)
const gapResult = ref<any>(null)

// 导入凭证
const importModalVisible = ref(false)
const importFileList = ref<any[]>([])
const importing = ref(false)

// 排序
const sortField = ref('')
const sortOrder = ref('')

// 表格数据（展开后的分录）
const tableData = computed(() => {
  const data: any[] = []
  const sourceList = voucherList.value

  sourceList.forEach(voucher => {
    if (voucher.entries && voucher.entries.length > 0) {
      voucher.entries.forEach((entry: any, index: number) => {
        data.push({
          uniqueId: `${voucher.id}-${entry.id || index}`,
          voucherId: voucher.id,
          voucherWord: voucher.voucherWord,
          voucherNumber: voucher.voucherNo,
          date: formatDate(voucher.date),
          summary: entry.summary,
          accountCode: entry.accountCode,
          accountName: entry.accountName,
          debitAmount: entry.debitAmount,
          creditAmount: entry.creditAmount,
          creator: voucher.creator,
          auditor: voucher.auditor,
          status: voucher.status,
          remark: voucher.remark,
          isFirstRow: index === 0,
          isLastRow: index === voucher.entries.length - 1,
          rowSpan: index === 0 ? voucher.entries.length : 0
        })
      })
    } else {
      data.push({
        uniqueId: `${voucher.id}-0`,
        voucherId: voucher.id,
        voucherWord: voucher.voucherWord,
        voucherNumber: voucher.voucherNo,
        date: formatDate(voucher.date),
        summary: voucher.summary,
        accountName: voucher.accountName,
        debitAmount: voucher.debitAmount,
        creditAmount: voucher.creditAmount,
        creator: voucher.creator,
        auditor: voucher.auditor,
        status: voucher.status,
        remark: voucher.remark,
        isFirstRow: true,
        isLastRow: true,
        rowSpan: 1
      })
    }
  })
  return data
})

// 切换查询模式
function handleQueryModeChange(mode: 'period' | 'date' | 'periodRange' | 'dateRange') {
  queryMode.value = mode
  pagination.value.pageIndex = 1
  loadData()
}

// 格式化日期为 yyyy-MM-dd
function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

// 加载账期列表
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .map((p: any) => ({
        id: p.id,
        name: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
        year: p.year,
        month: p.periodNo
      }))
    if (periodList.value.length > 0) {
      const now = new Date()
      const currentYear = now.getFullYear()
      const currentMonth = now.getMonth() + 1
      let defaultPeriod = (periodList.value as any[]).find(
        (p: any) => p.year === currentYear && p.month === currentMonth
      )
      if (!defaultPeriod) defaultPeriod = periodList.value[0]
      selectedPeriodId.value = defaultPeriod.id
      startPeriodId.value = periodList.value[periodList.value.length - 1].id
      endPeriodId.value = defaultPeriod.id
    }
  } catch (error) {
    console.error('加载账期列表失败', error)
  }
}

// 加载数据
async function loadData() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined,
    }

    if (filterStatus.value === 'recording') {
      params.source = '费用支出'
      params.status = 0
      params.keyword = '[待补录]'
      params.searchField = 'remark'
    } else if (filterStatus.value === 'pending') {
      params.status = 1
    }

    if (searchKeyword.value) {
      params.keyword = searchKeyword.value
      params.searchField = searchField.value
    }

    if (sortField.value) {
      params.sortField = sortField.value
      params.sortOrder = sortOrder.value
    }

    // 根据查询模式添加参数
    switch (queryMode.value) {
      case 'period':
        if (selectedPeriodId.value) {
          params.periodId = selectedPeriodId.value
        }
        break
      case 'date':
        if (selectedDate.value) {
          params.date = selectedDate.value
        }
        break
      case 'periodRange':
        if (startPeriodId.value) {
          params.startPeriodId = startPeriodId.value
        }
        if (endPeriodId.value) {
          params.endPeriodId = endPeriodId.value
        }
        break
      case 'dateRange':
        if (selectedDateRange.value && selectedDateRange.value.length === 2) {
          params.startDate = selectedDateRange.value[0]
          params.endDate = selectedDateRange.value[1]
        }
        break
    }

    const res = await getVoucherList(params) as any
    if (res && res.items) {
      voucherList.value = res.items
      pagination.value.total = res.total || 0
      totalCount.value = res.totalAllCount ?? res.total ?? 0
      pendingCount.value = res.pendingCount ?? 0
      pendingRecordCount.value = res.pendingRecordCount || 0
    } else {
      voucherList.value = []
      pagination.value.total = 0
      totalCount.value = 0
      pendingCount.value = 0
      pendingRecordCount.value = 0
    }
  } catch (error) {
    console.error('加载凭证列表失败', error)
    voucherList.value = []
    pagination.value.total = 0
    totalCount.value = 0
    pendingCount.value = 0
    pendingRecordCount.value = 0
  } finally {
    loading.value = false
  }
}

// 表格变化（排序）
function handleTableChange(_pagination: any, _filters: any, sorter: any) {
  if (sorter.order) {
    sortField.value = sorter.field || ''
    sortOrder.value = sorter.order
  } else {
    sortField.value = ''
    sortOrder.value = ''
  }
  pagination.value.pageIndex = 1
  loadData()
}

// 状态变化
function onStatusChange() {
  pagination.value.pageIndex = 1
  loadData()
}

// 搜索（防抖300ms，避免频繁请求）
function handleSearch() {
  pagination.value.pageIndex = 1
  loadData()
}
const debouncedSearch = useDebounceFn(handleSearch, 300)

// 全选状态
const allVoucherIds = computed(() => {
  const ids = new Set<number>()
  tableData.value.forEach(row => ids.add(row.voucherId))
  return [...ids]
})
const isAllSelected = computed(() => allVoucherIds.value.length > 0 && allVoucherIds.value.every(id => selectedIds.value.includes(id)))
const isIndeterminate = computed(() => selectedIds.value.length > 0 && !isAllSelected.value)

// 全选/取消全选
function handleSelectAll(e: any) {
  if (e.target.checked) {
    selectedIds.value = [...allVoucherIds.value]
  } else {
    selectedIds.value = []
  }
}

// 单行选择（按凭证维度）
function handleRowSelect(voucherId: number, checked: boolean) {
  if (checked) {
    if (!selectedIds.value.includes(voucherId)) {
      selectedIds.value = [...selectedIds.value, voucherId]
    }
  } else {
    selectedIds.value = selectedIds.value.filter(id => id !== voucherId)
  }
}

// 行类名
function getRowClassName(record: any) {
  return record.isLastRow ? 'voucher-last-row' : ''
}

// 跳转到录入页
function goToEntry() {
  router.push('/finance/voucher/entry')
}

// 编辑凭证
function editVoucher(id: number) {
  router.push(`/finance/voucher/entry/${id}`)
}

// 补录操作：跳转到凭证编辑页面
function goToRecord(id: number) {
  router.push(`/finance/voucher/entry/${id}`)
}

// 复制凭证
async function handleCopy() {
  if (selectedIds.value.length !== 1) {
    message.warning('请选择单张凭证进行复制')
    return
  }
  try {
    const res = await copyVoucher(selectedIds.value[0]) as any
    message.success(res?.message || '复制成功')
    loadData()
  } catch (error) {
    message.error('复制失败')
  }
}

// 冲销凭证
function handleReverse() {
  if (selectedIds.value.length !== 1) {
    message.warning('请选择单张凭证进行冲销')
    return
  }
  Modal.confirm({
    title: '冲销确认',
    content: '确定要冲销此凭证？将生成一张红字冲销凭证',
    async onOk() {
      const res = await reverseVoucher(selectedIds.value[0]) as any
      message.success(res?.message || '冲销凭证已生成')
      loadData()
    },
  })
}

// 批量审核
function batchAudit() {
  if (selectedIds.value.length === 0) {
    message.warning('请选择要审核的凭证')
    return
  }
  Modal.confirm({
    title: '提示',
    content: `确定审核选中的 ${selectedIds.value.length} 张凭证？`,
    async onOk() {
      const res = await batchAuditVouchers({
        ids: selectedIds.value,
        auditorId: 0,
        auditorName: ''
      }) as any
      message.success(res?.message || '审核完成')
      loadData()
    },
  })
}

// 批量删除
function batchDelete() {
  if (selectedIds.value.length === 0) {
    message.warning('请选择要删除的凭证')
    return
  }
  Modal.confirm({
    title: '警告',
    content: `确定删除选中的 ${selectedIds.value.length} 张凭证？`,
    okType: 'danger',
    async onOk() {
      let successCount = 0
      for (const id of selectedIds.value) {
        try {
          await deleteVoucher(id)
          successCount++
        } catch (error) {
          console.error(`删除凭证 ${id} 失败`, error)
        }
      }
      message.success(`成功删除 ${successCount} 张凭证`)
      loadData()
    },
  })
}

// 打印
function handlePrint() {
  if (selectedIds.value.length === 0) {
    message.warning('请先选择要打印的凭证')
    return
  }
  const ids = selectedIds.value.join(',')
  router.push(`/finance/voucher/print/${ids}`)
}

// 更多操作
function handleMoreCommand({ key }: { key: string | number }) {
  switch (key) {
    case 'export':
      handleExport()
      break
    case 'import':
      handleOpenImport()
      break
    case 'checkGap':
      openGapCheck()
      break
  }
}

// 导出凭证
async function handleExport() {
  if (!selectedIds.value || selectedIds.value.length === 0) {
    message.warning('请先选择要导出的凭证')
    return
  }
  const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }
  try {
    const blob = await exportVouchers(selectedIds.value, accountSetId)
    downloadBlob(blob instanceof Blob ? blob : new Blob([blob]), '凭证导出.xlsx')
  } catch {
    message.error('导出失败')
  }
}

// 打开导入弹窗
function handleOpenImport() {
  importModalVisible.value = true
}

// 导入凭证
async function handleImport() {
  if (!importFileList.value.length) {
    message.warning('请选择Excel文件')
    return
  }
  const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }
  importing.value = true
  try {
    const formData = new FormData()
    formData.append('file', importFileList.value[0].originFileObj || importFileList.value[0])
    formData.append('accountSetId', String(accountSetId))

    const res = await importVouchers(formData) as any
    if (res.success && (!res.errors || res.errors.length === 0)) {
      message.success(`成功导入 ${res.importedCount} 张凭证`)
      importModalVisible.value = false
      importFileList.value = []
      await loadData()
    } else {
      Modal.error({
        title: '导入存在错误',
        content: h('div', (res.errors || []).map((e: any) =>
          h('p', `第 ${e.rowNumber} 行: ${e.message}`)
        )),
      })
    }
  } catch {
    message.error('导入失败')
  } finally {
    importing.value = false
  }
}

// 下载导入模板
async function downloadImportTemplate() {
  try {
    const blob = await exportVoucherTemplate()
    downloadBlob(blob instanceof Blob ? blob : new Blob([blob]), '凭证导入模板.xlsx')
  } catch {
    message.error('下载模板失败')
  }
}

// 断号检查
async function openGapCheck() {
  const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  const period = periodList.value.find((p: any) => p.id === selectedPeriodId.value) as any
  if (!period) {
    message.warning('请先选择账期')
    return
  }

  gapResult.value = null
  gapDialogVisible.value = true
  gapLoading.value = true
  try {
    const res = await checkVoucherGap({
      accountSetId,
      year: period.year,
      periodNo: period.month
    }) as any
    gapResult.value = res?.data ?? res
  } catch (error) {
    message.error('断号检查失败')
    gapDialogVisible.value = false
  } finally {
    gapLoading.value = false
  }
}

// 分页变化
function handleSizeChange(_current: number, size: number) {
  pagination.value.pageSize = size
  pagination.value.pageIndex = 1
  loadData()
}

function handlePageChange(page: number) {
  pagination.value.pageIndex = page
  loadData()
}

// 格式化金额
function formatAmount(amount: number): string {
  if (!amount) return ''
  return amount.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  })
}

// 监听日期选择变化
watch(selectedDate, (newVal) => {
  if (queryMode.value === 'date') {
    handleSearch()
  }
})

watch(selectedDateRange, (newVal) => {
  if (queryMode.value === 'dateRange') {
    handleSearch()
  }
}, { deep: true })

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, async () => {
  pagination.value.pageIndex = 1
  selectedPeriodId.value = undefined
  startPeriodId.value = undefined
  endPeriodId.value = undefined
  await loadPeriods()
  loadData()
})

// keep-alive 激活：PageHeader 自身会在 onActivated 中重新 register，
// 此处仅作为占位确保组件响应 keep-alive 生命周期。
onActivated(() => {
  // PageHeader 已自行处理工具栏注册，无需额外操作
})

onDeactivated(() => {
  // PageHeader 已自行处理工具栏注销，无需额外操作
})

// 初始化
onMounted(() => {
  loadPeriods().then(() => {
    loadData()
  })
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

// ===== 主操作按钮 — 品牌色 =====
.btn-primary-brand {
  background: $color-primary !important;
  border-color: $color-primary !important;
  color: $bg-card !important;
  font-weight: 500;
  transition: $transition-smooth;

  &:hover {
    background: $color-primary-hover !important;
    border-color: $color-primary-hover !important;
    box-shadow: 0 4px 12px rgba($color-primary, 0.3);
  }
}

// ===== 冲销按钮 =====
.btn-warning {
  color: $color-warning;
  border-color: $color-warning;

  &:hover {
    color: $color-warning;
    border-color: $color-warning;
    opacity: 0.85;
  }
}

// ===== toolbar 行布局 =====
.toolbar-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  gap: $spacing-sm;
  flex-wrap: wrap;
  min-height: $toolbar-height;
}

// ===== 下划线式 Tab =====
.tab-group {
  display: flex;
  align-items: center;
  gap: 0;
}

.tab-item {
  padding: $spacing-sm $spacing-md;
  font-size: $font-size-base;
  color: $text-regular;
  cursor: pointer;
  border: none;
  background: transparent;
  border-radius: 0;
  transition: color 0.2s;
  user-select: none;
  white-space: nowrap;
  position: relative;
  line-height: $line-height-normal;

  .tab-count {
    display: inline-block;
    margin-left: $spacing-2xs;
    font-size: $font-size-sm;
    opacity: 0.8;

    &.tab-count-recording {
      min-width: 16px;
      height: 16px;
      line-height: 16px;
      padding: 0 4px;
      font-size: 11px;
      background: var(--color-danger);
      color: #fff;
      border-radius: 8px;
      text-align: center;
      opacity: 1;
    }
  }

  &:hover {
    color: $color-primary;
  }

  &.active {
    color: var(--text-1);
    font-weight: 600;

    .tab-count {
      opacity: 1;
    }

    &::after {
      content: '';
      position: absolute;
      bottom: 0;
      left: 50%;
      transform: translateX(-50%);
      width: 70%;
      height: 2px;
      background: $color-primary;
      border-radius: 1px;
    }
  }
}

// ===== 筛选控件组 =====
.filter-group {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: $spacing-sm;
}

.range-sep {
  color: $text-secondary;
  padding: 0 $spacing-2xs;
}

.search-icon {
  cursor: pointer;
  color: $text-secondary;
  transition: $transition-fast;

  &:hover {
    color: $color-primary;
  }
}

// ===== 批量操作栏 =====
.batch-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: $spacing-sm $spacing-md;
  margin: 0;
  background: $color-primary-light;
  border-left: 3px solid $color-primary;
  border-radius: 0;
  animation: slideDown 0.2s ease;

  .batch-info {
    font-size: $font-size-base;
    color: $text-primary;

    strong {
      color: $color-primary;
      font-weight: 600;
    }
  }

  .batch-actions {
    display: flex;
    gap: $spacing-sm;
  }
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-4px); }
  to   { opacity: 1; transform: translateY(0); }
}

// ===== 凭证链接 =====
.voucher-link {
  color: $color-primary;
  cursor: pointer;
  text-decoration: none;
  font-weight: 500;
  transition: $transition-fast;

  &:hover {
    color: $color-primary-hover;
    text-decoration: underline;
  }
}

// ===== 金额样式 =====
.amount {
  font-family: 'Courier New', monospace;
  font-weight: 500;
  letter-spacing: 0.02em;
}

.negative-value {
  color: $color-danger;
}

// ===== 凭证分隔线 =====
:deep(.voucher-last-row td) {
  border-bottom: 2px solid $border-color !important;
}

// ===== 合并单元格下边框 =====
:deep(.voucher-merged-cell) {
  border-bottom: 2px solid $border-color !important;
}

// ===== 查询模式按钮 =====
.query-mode-btn {
  padding: 0 $spacing-sm;
  min-width: $button-height;
}

:deep(.query-mode-active) {
  background-color: var(--bg-muted) !important;
  color: var(--text-1) !important;
  font-weight: 500;
}

// ===== 断号检查弹窗 =====
.gap-loading {
  text-align: center;
  padding: $spacing-lg;
  color: $text-regular;
}

.gap-success {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  color: $color-success;
  font-size: 15px;
}

.gap-error {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  color: $color-danger;
  font-size: 15px;
  margin-bottom: $spacing-sm + $spacing-xs;
}

.gap-tag {
  margin: $spacing-xs;
}

// ===== 表格卡片容器 =====
.table-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
  margin: 0;
  background: $bg-card;
  border-radius: 0;
  box-shadow: $shadow-card;
  padding: 0;

  // 表头背景
  :deep(.ant-table-thead > tr > th) {
    background: #FAFAFA !important;
    border-bottom: 1px solid $border-color !important;
    font-weight: 600;
    color: $text-primary;
    font-size: $font-size-sm;
    letter-spacing: 0.02em;
  }

  // 移除外边框
  :deep(.ant-table) {
    border: none !important;
  }

  :deep(.ant-table-container) {
    border: none !important;
    border-right: none !important;

    &::before, &::after {
      display: none;
    }
  }

  // 行 hover
  :deep(.ant-table-tbody > tr:hover > td) {
    background: rgba($color-primary, 0.04) !important;
  }

  // 行分割线
  :deep(.ant-table-tbody > tr > td) {
    border-bottom: 1px solid $border-color-lighter !important;
  }

  // flex 布局让表格撑满
  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: $spacing-sm + $spacing-xs $spacing-md 0;
  }

  :deep(.ant-spin-nested-loading) {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  :deep(.ant-spin-container) {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  :deep(.ant-table) {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
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

  // 分页
  .pagination-wrapper {
    flex-shrink: 0;
    display: flex;
    justify-content: flex-end;
    padding: $spacing-sm + $spacing-xs $spacing-md;
    border-top: 1px solid $border-color-lighter;
    margin-top: 0;
  }
}
</style>
