<template>
  <div class="page-container">
    <PageHeader :title="`科目明细账 - ${accountInfo}`">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #actions>
        <a-select
          v-model:value="selectedPeriodId"
          placeholder="选择账期"
          style="width: 160px"
          @change="onPeriodChange"
          :options="periodList.map(p => ({ label: p.label, value: p.id }))"
        />
        <a-button @click="goBack">返回余额表</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
    </PageHeader>

    <!-- 科目信息卡片 -->
    <a-card :bordered="false" class="info-card" v-if="result">
      <div class="account-info">
        <span class="info-item"><strong>科目编码：</strong>{{ result.accountCode }}</span>
        <span class="info-item"><strong>科目名称：</strong>{{ result.accountName }}</span>
        <span class="info-item"><strong>查询期间：</strong>{{ currentPeriodLabel }}</span>
      </div>
    </a-card>

    <!-- 明细数据表 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="result?.items || []"
        :loading="loading"
        :pagination="false"
        rowKey="id"
        bordered
        :rowClassName="getRowClassName"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'voucherNo'">
            <span v-if="record.voucherNo">{{ record.voucherNo }}</span>
            <span v-else class="text-muted">—</span>
          </template>
          <template v-if="column.dataIndex === 'debitAmount'">
            <span :class="{ 'amount-value': record.debitAmount > 0 }">
              {{ record.debitAmount > 0 ? formatAmount(record.debitAmount) : '—' }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'creditAmount'">
            <span :class="{ 'amount-value': record.creditAmount > 0 }">
              {{ record.creditAmount > 0 ? formatAmount(record.creditAmount) : '—' }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'balance'">
            <span class="balance-value">{{ formatAmount(record.balance) }}</span>
          </template>
          <template v-if="column.dataIndex === 'direction'">
            <a-tag :color="record.direction === '借' ? 'blue' : 'red'">
              {{ record.direction }}
            </a-tag>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { DownloadOutlined } from '@ant-design/icons-vue'
import { getAccountDetail, getPeriods } from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const route = useRoute()
const router = useRouter()
const accountSetStore = useAccountSetStore()

const accountId = computed(() => Number(route.params.accountId))
const queryYear = computed(() => Number(route.query.year) || new Date().getFullYear())
const queryPeriodNo = computed(() => Number(route.query.periodNo) || (new Date().getMonth() + 1))
const queryAccountCode = computed(() => String(route.query.accountCode || ''))
const queryAccountName = computed(() => String(route.query.accountName || ''))

const accountInfo = computed(() => {
  if (result.value) return `${result.value.accountCode} ${result.value.accountName}`
  if (queryAccountCode.value) return `${queryAccountCode.value} ${queryAccountName.value}`
  return ''
})

const selectedPeriodId = ref<number | undefined>(undefined)
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])
const currentYear = ref(queryYear.value)
const currentPeriodNo = ref(queryPeriodNo.value)
const loading = ref(false)
const result = ref<any>(null)

const columns = [
  { title: '日期', dataIndex: 'date', key: 'date', width: 160 },
  { title: '凭证号', dataIndex: 'voucherNo', key: 'voucherNo', width: 100, align: 'center' as const },
  { title: '摘要', dataIndex: 'summary', key: 'summary' },
  { title: '借方金额', dataIndex: 'debitAmount', key: 'debitAmount', width: 150, align: 'right' as const },
  { title: '贷方金额', dataIndex: 'creditAmount', key: 'creditAmount', width: 150, align: 'right' as const },
  { title: '余额', dataIndex: 'balance', key: 'balance', width: 150, align: 'right' as const },
  { title: '方向', dataIndex: 'direction', key: 'direction', width: 80, align: 'center' as const },
]

const currentPeriodLabel = computed(() => {
  const p = periodList.value.find(x => x.id === selectedPeriodId.value)
  return p ? p.label : `${currentYear.value}年第${String(currentPeriodNo.value).padStart(2, '0')}期`
})

function onPeriodChange() {
  const p = periodList.value.find(x => x.id === selectedPeriodId.value)
  if (p) {
    currentYear.value = p.year
    currentPeriodNo.value = p.month
    loadData()
  }
}

function goBack() {
  router.push({ name: 'AccountBalanceReport' })
}

function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function getRowClassName(record: any) {
  if (record.isOpeningBalance) return 'opening-row'
  return ''
}

async function loadData() {
  if (!accountId.value) return
  loading.value = true
  try {
    const res = await getAccountDetail({
      accountId: accountId.value,
      year: currentYear.value,
      periodNo: currentPeriodNo.value,
      accountSetId: accountSetStore.currentAccountSetId || undefined
    })
    result.value = res
  } catch (error) {
    console.error('加载科目明细账失败', error)
  } finally {
    loading.value = false
  }
}

async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .sort((a: any, b: any) => {
        if (a.year !== b.year) return b.year - a.year
        return b.periodNo - a.periodNo
      })
      .map((p: any) => ({
        id: p.id,
        label: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
        year: p.year,
        month: p.periodNo
      }))
    const target = periodList.value.find(p => p.year === queryYear.value && p.month === queryPeriodNo.value)
    if (target) {
      selectedPeriodId.value = target.id
    } else if (periodList.value.length > 0) {
      // 无钻取参数时，基于当前系统日期匹配期间
      const now = new Date()
      const curYear = now.getFullYear()
      const curMonth = now.getMonth() + 1
      const matched = periodList.value.find(p => p.year === curYear && p.month === curMonth)
      const defaultPeriod = matched || periodList.value[0]
      selectedPeriodId.value = defaultPeriod.id
      currentYear.value = defaultPeriod.year
      currentPeriodNo.value = defaultPeriod.month
    }
  } catch (e) {
    console.error('加载期间失败', e)
  }
}

function exportToExcel() {
  if (!result.value?.items?.length) { message.warning('暂无数据可导出'); return }
  const headers = ['日期', '凭证号', '摘要', '借方金额', '贷方金额', '余额', '方向']
  const rows = result.value.items.map((row: any) => [
    row.date,
    row.voucherNo || '',
    row.summary || '',
    row.debitAmount || '',
    row.creditAmount || '',
    row.balance || '',
    row.direction || ''
  ])
  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `科目明细账_${result.value.accountCode}_${currentPeriodLabel.value}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

onMounted(async () => {
  await loadPeriods()
  loadData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  selectedPeriodId.value = undefined
  await loadPeriods()
  loadData()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.info-card {
  margin-bottom: $section-gap;

  .account-info {
    display: flex;
    gap: 32px;
    flex-wrap: wrap;

    .info-item {
      font-size: 14px;
      color: #303133;
    }
  }
}

.amount-value {
  color: #303133;
}

.balance-value {
  font-weight: 500;
  color: #303133;
}

.text-muted {
  color: #c0c4cc;
}

:deep(.opening-row) {
  background-color: var(--color-success-light) !important;
  font-weight: bold;
}

:deep(.opening-row:hover) {
  background-color: #e1f3d8 !important;
}
</style>
