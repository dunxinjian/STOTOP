<template>
  <div class="page-container">
    <PageHeader title="保险报表" description="保险业务综合数据分析" />

    <!-- 综合看板 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="5">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="有效保单" :value="overview.ActivePolicies" :value-style="{ color: 'var(--color-info)' }" />
        </a-card>
      </a-col>
      <a-col :span="5">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="即将到期" :value="overview.ExpiringPolicies" :value-style="{ color: 'var(--color-warning)' }" />
        </a-card>
      </a-col>
      <a-col :span="5">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="进行中理赔" :value="overview.PendingClaims" :value-style="{ color: 'var(--color-danger)' }" />
        </a-card>
      </a-col>
      <a-col :span="5">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="待审批理赔" :value="overview.PendingSettlements" :value-style="{ color: 'var(--biz-approval)' }" />
        </a-card>
      </a-col>
      <a-col :span="4">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="基金余额" :value="overview.TotalFundBalance" :precision="2" prefix="¥" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab" @change="onTabChange">
        <!-- 保费汇总 -->
        <a-tab-pane key="premium" tab="保费汇总">
          <div style="margin-bottom: 12px">
            <a-select v-model:value="premiumFilter.businessType" placeholder="业务类型" allow-clear style="width: 120px; margin-right: 8px" :options="businessTypeOptions" @change="fetchPremiumSummary" />
            <a-range-picker v-model:value="premiumFilter.dateRange" style="width: 260px" value-format="YYYY-MM-DD" @change="fetchPremiumSummary" />
          </div>
          <a-table :columns="premiumColumns" :data-source="premiumData" :loading="tabLoading" row-key="BusinessType" bordered :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'TotalPremium'">¥{{ record.TotalPremium.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'AvgPremium'">¥{{ record.AvgPremium.toFixed(2) }}</template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 出险分析 -->
        <a-tab-pane key="claim" tab="出险分析">
          <div style="margin-bottom: 12px">
            <a-select v-model:value="claimFilter.businessType" placeholder="业务类型" allow-clear style="width: 120px; margin-right: 8px" :options="businessTypeOptions" @change="fetchClaimAnalysis" />
            <a-range-picker v-model:value="claimFilter.dateRange" style="width: 260px" value-format="YYYY-MM-DD" @change="fetchClaimAnalysis" />
          </div>
          <a-table :columns="claimColumns" :data-source="claimData" :loading="tabLoading" row-key="AccidentType" bordered :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'TotalLoss'">¥{{ record.TotalLoss.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'ClaimRate'">{{ (record.ClaimRate * 100).toFixed(1) }}%</template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 赔付分析 -->
        <a-tab-pane key="settlement" tab="赔付分析">
          <div style="margin-bottom: 12px">
            <a-select v-model:value="settlementFilter.businessType" placeholder="业务类型" allow-clear style="width: 120px; margin-right: 8px" :options="businessTypeOptions" @change="fetchSettlementAnalysis" />
            <a-range-picker v-model:value="settlementFilter.dateRange" style="width: 260px" value-format="YYYY-MM-DD" @change="fetchSettlementAnalysis" />
          </div>
          <a-table :columns="settlementColumns" :data-source="settlementData" :loading="tabLoading" row-key="SettlementType" bordered :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'TotalAmount'">¥{{ record.TotalAmount.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'AvgCycle'">{{ record.AvgCycle.toFixed(1) }}天</template>
              <template v-if="column.dataIndex === 'SettlementRate'">{{ (record.SettlementRate * 100).toFixed(1) }}%</template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 基金收支 -->
        <a-tab-pane key="fund" tab="基金收支">
          <a-table :columns="fundColumns" :data-source="fundData" :loading="tabLoading" row-key="FundId" bordered :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'OpeningBalance'">¥{{ record.OpeningBalance.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'Contributions'">¥{{ record.Contributions.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'Payouts'">¥{{ record.Payouts.toFixed(2) }}</template>
              <template v-if="column.dataIndex === 'ClosingBalance'">
                <span :style="{ color: record.ClosingBalance >= 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 500 }">
                  ¥{{ record.ClosingBalance.toFixed(2) }}
                </span>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPremiumSummary,
  getClaimAnalysis,
  getSettlementAnalysis,
  getFundBalance,
  getOverview,
  type PremiumSummaryDto,
  type ClaimAnalysisDto,
  type SettlementAnalysisDto,
  type FundBalanceDto,
  type OverviewDto,
} from '@/api/insurance'

const businessTypeOptions = [{ label: '三轮车', value: 1 }, { label: '员工', value: 2 }, { label: '资产', value: 3 }]

const activeTab = ref('premium')
const tabLoading = ref(false)

const overview = ref<OverviewDto>({ ActivePolicies: 0, ExpiringPolicies: 0, PendingClaims: 0, PendingSettlements: 0, TotalFundBalance: 0 })

// 保费汇总
const premiumFilter = reactive({ businessType: undefined as number | undefined, dateRange: undefined as [string, string] | undefined })
const premiumData = ref<PremiumSummaryDto[]>([])
const premiumColumns = [
  { title: '业务类型', dataIndex: 'BusinessType', width: 100 },
  { title: '保险大类', dataIndex: 'InsuranceCategory', width: 100 },
  { title: '保费总额', dataIndex: 'TotalPremium', width: 130, align: 'right' as const },
  { title: '保单数量', dataIndex: 'PolicyCount', width: 100, align: 'center' as const },
  { title: '平均保费', dataIndex: 'AvgPremium', width: 130, align: 'right' as const },
]

// 出险分析
const claimFilter = reactive({ businessType: undefined as number | undefined, dateRange: undefined as [string, string] | undefined })
const claimData = ref<ClaimAnalysisDto[]>([])
const claimColumns = [
  { title: '事故类型', dataIndex: 'AccidentType', width: 100 },
  { title: '月份', dataIndex: 'Month', width: 100 },
  { title: '出险次数', dataIndex: 'ClaimCount', width: 100, align: 'center' as const },
  { title: '总损失', dataIndex: 'TotalLoss', width: 130, align: 'right' as const },
  { title: '出险率', dataIndex: 'ClaimRate', width: 100, align: 'center' as const },
]

// 赔付分析
const settlementFilter = reactive({ businessType: undefined as number | undefined, dateRange: undefined as [string, string] | undefined })
const settlementData = ref<SettlementAnalysisDto[]>([])
const settlementColumns = [
  { title: '理赔类型', dataIndex: 'SettlementType', width: 120 },
  { title: '月份', dataIndex: 'Month', width: 100 },
  { title: '赔付总额', dataIndex: 'TotalAmount', width: 130, align: 'right' as const },
  { title: '平均周期', dataIndex: 'AvgCycle', width: 100, align: 'center' as const },
  { title: '赔付率', dataIndex: 'SettlementRate', width: 100, align: 'center' as const },
]

// 基金收支
const fundData = ref<FundBalanceDto[]>([])
const fundColumns = [
  { title: '基金名称', dataIndex: 'FundName', width: 150 },
  { title: '期初余额', dataIndex: 'OpeningBalance', width: 130, align: 'right' as const },
  { title: '缴费收入', dataIndex: 'Contributions', width: 130, align: 'right' as const },
  { title: '赔付支出', dataIndex: 'Payouts', width: 130, align: 'right' as const },
  { title: '期末余额', dataIndex: 'ClosingBalance', width: 130, align: 'right' as const },
]

async function fetchOverview() {
  try { overview.value = await getOverview() } catch { /* ignore */ }
}

async function fetchPremiumSummary() {
  tabLoading.value = true
  try {
    const params: any = {}
    if (premiumFilter.businessType != null) params.businessType = premiumFilter.businessType
    if (premiumFilter.dateRange) { params.startDate = premiumFilter.dateRange[0]; params.endDate = premiumFilter.dateRange[1] }
    premiumData.value = await getPremiumSummary(params) || []
  } finally { tabLoading.value = false }
}

async function fetchClaimAnalysis() {
  tabLoading.value = true
  try {
    const params: any = {}
    if (claimFilter.businessType != null) params.businessType = claimFilter.businessType
    if (claimFilter.dateRange) { params.startDate = claimFilter.dateRange[0]; params.endDate = claimFilter.dateRange[1] }
    claimData.value = await getClaimAnalysis(params) || []
  } finally { tabLoading.value = false }
}

async function fetchSettlementAnalysis() {
  tabLoading.value = true
  try {
    const params: any = {}
    if (settlementFilter.businessType != null) params.businessType = settlementFilter.businessType
    if (settlementFilter.dateRange) { params.startDate = settlementFilter.dateRange[0]; params.endDate = settlementFilter.dateRange[1] }
    settlementData.value = await getSettlementAnalysis(params) || []
  } finally { tabLoading.value = false }
}

async function fetchFundBalance() {
  tabLoading.value = true
  try { fundData.value = await getFundBalance() || [] } finally { tabLoading.value = false }
}

function onTabChange(activeKey: string | number) {
  const key = String(activeKey)
  if (key === 'premium') fetchPremiumSummary()
  else if (key === 'claim') fetchClaimAnalysis()
  else if (key === 'settlement') fetchSettlementAnalysis()
  else if (key === 'fund') fetchFundBalance()
}

onMounted(() => {
  fetchOverview()
  fetchPremiumSummary()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.stat-card {
  text-align: center;
}
</style>
