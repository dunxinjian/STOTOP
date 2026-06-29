<template>
  <div class="finance-dashboard">
    <PageHeader title="财务首页">
      <template #left>
        <AccountSetSelector style="width: 180px;" />
        <AccountPeriodSelector
          :account-set-id="accountSetStore.getCurrentAccountSetId()"
          @change="handlePeriodChange"
          style="margin-left: 8px;"
        />
      </template>
      <template #actions>
        <a-button size="small" @click="openConfig">
          <SettingOutlined /> 配置
        </a-button>
      </template>
    </PageHeader>

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-info);"></span>
        <span class="kpi-label">账户余额</span>
        <span class="kpi-value" style="color: var(--color-info);">{{ formatWan(bankBalance) }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-success);"></span>
        <span class="kpi-label">应收合计</span>
        <span class="kpi-value" style="color: var(--color-success);">{{ formatWan(receivable) }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-warning);"></span>
        <span class="kpi-label">应付合计</span>
        <span class="kpi-value" style="color: var(--color-warning);">{{ formatWan(payable) }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-info);"></span>
        <span class="kpi-label">本期收入</span>
        <span class="kpi-value" style="color: var(--color-info);">{{ formatWan(totalRevenue) }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--biz-finance);"></span>
        <span class="kpi-label">本期支出</span>
        <span class="kpi-value" style="color: var(--biz-finance);">{{ formatWan(totalExpense) }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" :style="{ background: totalProfit >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }"></span>
        <span class="kpi-label">本期利润</span>
        <span class="kpi-value" :style="{ color: totalProfit >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">{{ formatWan(totalProfit) }}</span>
      </div>
    </div>

    <!-- Tab 内容区 -->
    <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs" @change="handleTabChange">
      <!-- Tab1: 财务概览 -->
      <a-tab-pane key="overview" tab="财务概览">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%;">
            <a-col :span="14" style="height: 100%;">
              <div class="content-panel">
                <div class="panel-title">账户余额分布</div>
                <div ref="barChartRef" class="chart-container"></div>
              </div>
            </a-col>
            <a-col :span="10" style="height: 100%;">
              <div class="content-panel">
                <div class="panel-title">收支趋势</div>
                <div ref="lineChartRef" class="chart-container"></div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <!-- Tab2: 报表入口 -->
      <a-tab-pane key="reports" tab="报表入口">
        <div class="tab-content">
          <div class="content-panel">
            <div class="panel-title">余额表</div>
            <div class="report-grid">
              <div class="report-item" @click="navigateTo('/finance/reports/account-balance')">
                <FileOutlined class="report-icon" />
                <span class="report-name">科目余额表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/reports/auxiliary-balance')">
                <AppstoreOutlined class="report-icon" />
                <span class="report-name">辅助余额表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/reports/asset-balance')">
                <InboxOutlined class="report-icon" />
                <span class="report-name">资产余额表</span>
              </div>
            </div>

            <div class="panel-title" style="margin-top: 24px;">明细表</div>
            <div class="report-grid">
              <div class="report-item" @click="navigateToAccountDetail">
                <FileOutlined class="report-icon" />
                <span class="report-name">科目明细表</span>
              </div>
            </div>

            <div class="panel-title" style="margin-top: 24px;">综合表</div>
            <div class="report-grid">
              <div class="report-item" @click="navigateTo('/finance/reports/profit')">
                <LineChartOutlined class="report-icon" />
                <span class="report-name">利润表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/reports/balance-sheet')">
                <BlockOutlined class="report-icon" />
                <span class="report-name">资产负债表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/reports/cash-flow')">
                <DollarOutlined class="report-icon" />
                <span class="report-name">现金流量表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/reports/tax-payable')">
                <AccountBookOutlined class="report-icon" />
                <span class="report-name">应交税费表</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/amoeba/report')">
                <PieChartOutlined class="report-icon" />
                <span class="report-name">阿米巴损益表</span>
              </div>
            </div>

            <div class="panel-title" style="margin-top: 24px;">预算与资金计划</div>
            <div class="report-grid">
              <div class="report-item" @click="navigateTo('/finance/budget/versions')">
                <ProfileOutlined class="report-icon" />
                <span class="report-name">预算版本</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/budget/expense-mapping')">
                <BranchesOutlined class="report-icon" />
                <span class="report-name">费用预算映射</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/treasury/account-bindings')">
                <BankOutlined class="report-icon" />
                <span class="report-name">资金账户绑定</span>
              </div>
              <div class="report-item" @click="navigateTo('/finance/treasury/rolling-13-weeks')">
                <LineChartOutlined class="report-icon" />
                <span class="report-name">13周资金预测</span>
              </div>
            </div>
          </div>
        </div>
      </a-tab-pane>

      <!-- Tab3: 老板驾驶舱 -->
      <a-tab-pane key="boss" tab="老板驾驶舱">
        <div class="tab-content">
          <a-spin :spinning="bossLoading">
            <!-- 摘要卡片 -->
            <div class="boss-summary">
              <div class="boss-card">
                <div class="boss-card-label">总收入</div>
                <div class="boss-card-value" style="color: var(--color-info);">{{ formatWan(totalRevenue) }} <span class="unit">万</span></div>
              </div>
              <div class="boss-card">
                <div class="boss-card-label">总支出</div>
                <div class="boss-card-value" style="color: var(--biz-finance);">{{ formatWan(totalExpense) }} <span class="unit">万</span></div>
              </div>
              <div class="boss-card">
                <div class="boss-card-label">总利润</div>
                <div class="boss-card-value" :style="{ color: totalProfit >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">{{ formatWan(totalProfit) }} <span class="unit">万</span></div>
              </div>
            </div>
            <!-- 项目收益列表 -->
            <div class="content-panel" style="margin-top: 16px;">
              <div class="panel-title">
                项目收益
                <span class="panel-subtitle" v-if="selectedPeriod">{{ selectedPeriod.year }}.{{ String(selectedPeriod.periodNo).padStart(2, '0') }}</span>
              </div>
              <a-table
                v-if="projectProfits.length > 0"
                :columns="profitColumns"
                :data-source="projectProfits"
                :pagination="false"
                size="small"
                row-key="name"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'value'">
                    <span :style="{ color: record.value >= 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }">
                      {{ (record.value / 10000).toFixed(2) }} 万
                    </span>
                  </template>
                </template>
              </a-table>
              <a-empty v-else description="暂无项目收益数据" style="margin-top: 16px;" />
            </div>
          </a-spin>
        </div>
      </a-tab-pane>
    </a-tabs>

    <!-- 仪表盘配置弹窗 -->
    <a-modal
      v-model:open="configVisible"
      title="仪表盘配置"
      :width="480"
      @ok="handleConfigSave"
      okText="保存"
      cancelText="取消"
    >
      <a-form :label-col="{ span: 8 }" :wrapper-col="{ span: 14 }">
        <a-form-item label="银行存款科目前缀">
          <a-input v-model:value="configForm.bankAccountPrefix" placeholder="如 1002" />
          <div class="form-tip">用于计算账户余额和银行分布图</div>
        </a-form-item>
        <a-form-item label="应收账款科目">
          <a-input v-model:value="configForm.receivableCode" placeholder="如 1122" />
        </a-form-item>
        <a-form-item label="应付账款科目">
          <a-input v-model:value="configForm.payableCode" placeholder="如 2202" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import AccountPeriodSelector from '@/components/AccountPeriodSelector.vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { getAccountBalance, getAuxiliaryBalance, getAccountTree } from '@/api/finance'
import {
  SettingOutlined,
  LineChartOutlined,
  BlockOutlined,
  DollarOutlined,
  AccountBookOutlined,
  PieChartOutlined,
  FileOutlined,
  AppstoreOutlined,
  InboxOutlined,
  ProfileOutlined,
  BranchesOutlined,
  BankOutlined,
} from '@ant-design/icons-vue'

const router = useRouter()
const accountSetStore = useAccountSetStore()

// Tab 状态
const activeTab = ref('overview')

// 图表引用
const barChartRef = ref<HTMLElement>()
const lineChartRef = ref<HTMLElement>()
let barChartInstance: echarts.ECharts | null = null
let lineChartInstance: echarts.ECharts | null = null

// 选中的期间信息
const selectedPeriod = ref<{ periodId: number; year: number; periodNo: number } | null>(null)

// ==================== 仪表盘配置 ====================
interface DashboardConfig {
  bankAccountPrefix: string
  receivableCode: string
  payableCode: string
}

const DEFAULT_CONFIG: DashboardConfig = {
  bankAccountPrefix: '1002',
  receivableCode: '1122',
  payableCode: '2202',
}

function getConfigKey() {
  return `finance_dashboard_config_${accountSetStore.getCurrentAccountSetId()}`
}

function loadConfig(): DashboardConfig {
  try {
    const saved = localStorage.getItem(getConfigKey())
    return saved ? { ...DEFAULT_CONFIG, ...JSON.parse(saved) } : { ...DEFAULT_CONFIG }
  } catch {
    return { ...DEFAULT_CONFIG }
  }
}

function saveConfig(config: DashboardConfig) {
  localStorage.setItem(getConfigKey(), JSON.stringify(config))
}

const configVisible = ref(false)
const configForm = reactive<DashboardConfig>({ ...DEFAULT_CONFIG })

function openConfig() {
  const current = loadConfig()
  Object.assign(configForm, current)
  configVisible.value = true
}

function handleConfigSave() {
  saveConfig({ ...configForm })
  configVisible.value = false
  loadAllData()
}
// ==================== 仪表盘配置结束 ====================

// 仪表盘数据
const dashLoading = ref(false)
const bankBalance = ref(0)
const receivable = ref(0)
const payable = ref(0)
const bankAccounts = ref<{ name: string; value: number }[]>([])

// 老板驾驶舱数据
const bossLoading = ref(false)
const totalRevenue = ref(0)
const totalExpense = ref(0)
const totalProfit = ref(0)
const projectProfits = ref<{ name: string; value: number }[]>([])

// 项目收益表格列
const profitColumns = [
  { title: '项目名称', dataIndex: 'name', key: 'name' },
  { title: '收益金额', dataIndex: 'value', key: 'value', align: 'right' as const },
]

// 格式化为万元
function formatWan(val: number) {
  return (val / 10000).toFixed(2)
}

// 处理期间选择变化
function handlePeriodChange(periodId: number, period: { year: number; periodNo: number }) {
  selectedPeriod.value = { periodId, ...period }
  loadAllData()
}

// Tab 切换处理
function handleTabChange(key: string | number) {
  if (String(key) === 'overview') {
    nextTick(() => {
      initCharts()
    })
  }
}

// 导航到指定路由
function navigateTo(path: string) {
  router.push(path)
}

function findFirstLeafAccount(accounts: any[]): any | null {
  for (const account of accounts || []) {
    const children = account.children || account.Children || []
    if (children.length > 0) {
      const child = findFirstLeafAccount(children)
      if (child) return child
    }

    if (account.isLeaf === true || account.fIsLeaf === 1 || children.length === 0) {
      return account
    }
  }

  return null
}

async function navigateToAccountDetail() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  const accounts = await getAccountTree(undefined, accountSetId)
  const firstAccount = findFirstLeafAccount(accounts)
  const accountId = firstAccount?.id ?? firstAccount?.fid ?? firstAccount?.fID
  if (!accountId) {
    message.warning('当前账套下暂无可查看明细的科目')
    return
  }

  router.push(`/finance/reports/account-detail/${accountId}`)
}

// 加载仪表盘数据
async function loadDashboardData() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  const periodId = selectedPeriod.value?.periodId
  if (!accountSetId || !periodId) return

  dashLoading.value = true
  try {
    const config = loadConfig()
    const balances = (await getAccountBalance({ periodId, accountSetId })) as any[]

    // 1. 账户余额：银行存款科目的末级子科目期末余额合计
    const bankItems = (balances || []).filter(
      (b) => b.accountCode?.startsWith(config.bankAccountPrefix) && b.accountCode !== config.bankAccountPrefix
    )
    const totalBank = bankItems.reduce(
      (sum, b) => sum + ((b.endDebit || 0) - (b.endCredit || 0)),
      0
    )
    bankBalance.value = totalBank

    // 银行账户明细（用于图表）
    bankAccounts.value = bankItems
      .map((b) => ({
        name: b.accountName || b.accountCode,
        value: Number(((b.endDebit || 0) - (b.endCredit || 0)).toFixed(2)),
      }))
      .filter((b) => b.value !== 0)
      .sort((a, b) => b.value - a.value)

    // 2. 应收账款
    const arItem = (balances || []).find((b) => b.accountCode === config.receivableCode)
    receivable.value = arItem
      ? (arItem.endDebit || 0) - (arItem.endCredit || 0)
      : 0

    // 3. 应付账款
    const apItem = (balances || []).find((b) => b.accountCode === config.payableCode)
    payable.value = apItem
      ? (apItem.endCredit || 0) - (apItem.endDebit || 0)
      : 0

    // 更新图表
    if (activeTab.value === 'overview') {
      nextTick(() => updateBarChart())
    }
  } catch (e) {
    console.error('加载仪表盘数据失败', e)
  } finally {
    dashLoading.value = false
  }
}

// 加载老板驾驶舱数据
async function loadBossData() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  const period = selectedPeriod.value
  if (!accountSetId || !period?.periodId) return

  bossLoading.value = true
  try {
    const balances = (await getAccountBalance({ periodId: period.periodId, accountSetId })) as any[]
    if (balances) {
      // 收入类科目
      const revenueItems = balances.filter(
        (b) => b.accountCode === '5001' || b.accountCode === '5051' || b.accountCode === '5301'
      )
      totalRevenue.value = revenueItems.reduce(
        (sum, b) => sum + ((b.currentCredit || 0) - (b.currentDebit || 0)),
        0
      )

      // 支出类科目
      const expenseItems = balances.filter((b) =>
        ['5401', '5402', '5601', '5602', '5603'].includes(b.accountCode)
      )
      totalExpense.value = expenseItems.reduce(
        (sum, b) => sum + ((b.currentDebit || 0) - (b.currentCredit || 0)),
        0
      )

      totalProfit.value = totalRevenue.value - totalExpense.value
    }

    // 获取项目收益
    try {
      const auxBalances = (await getAuxiliaryBalance({
        periodId: period.periodId,
        type: 'project',
        accountSetId,
      })) as any[]

      if (auxBalances && auxBalances.length > 0) {
        const projectMap = new Map<string, number>()
        for (const item of auxBalances) {
          const projectName = item.auxiliaryName || item.name || '未分类'
          const code = item.accountCode || ''
          let amount = 0
          if (
            code.startsWith('5001') ||
            code.startsWith('5051') ||
            code.startsWith('5301')
          ) {
            amount = (item.currentCredit || 0) - (item.currentDebit || 0)
          } else if (code.startsWith('5401') || code.startsWith('5402')) {
            amount = -((item.currentDebit || 0) - (item.currentCredit || 0))
          }
          projectMap.set(projectName, (projectMap.get(projectName) || 0) + amount)
        }
        projectProfits.value = Array.from(projectMap.entries())
          .map(([name, value]) => ({ name, value: Number(value.toFixed(2)) }))
          .sort((a, b) => b.value - a.value)
      } else {
        projectProfits.value = []
      }
    } catch {
      projectProfits.value = []
    }
  } catch (e) {
    console.error('加载老板驾驶舱数据失败', e)
  } finally {
    bossLoading.value = false
  }
}

// 并行加载所有数据
async function loadAllData() {
  await Promise.all([loadDashboardData(), loadBossData()])
}

// 初始化图表
function initCharts() {
  if (barChartRef.value && !barChartInstance) {
    barChartInstance = echarts.init(barChartRef.value)
    updateBarChart()
  }
  if (lineChartRef.value && !lineChartInstance) {
    lineChartInstance = echarts.init(lineChartRef.value)
    updateLineChart()
  }
}

// 更新柱状图
function updateBarChart() {
  if (!barChartInstance) return

  if (bankAccounts.value.length === 0) {
    barChartInstance.setOption({
      grid: { left: '3%', right: '15%', bottom: '3%', top: '3%', containLabel: true },
      xAxis: { type: 'value', show: false },
      yAxis: {
        type: 'category',
        data: ['暂无数据'],
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#999', fontSize: 12 },
      },
      series: [{
        type: 'bar',
        data: [{ value: 0, itemStyle: { color: '#eee' } }],
        barWidth: 16,
        label: { show: true, position: 'right', formatter: '-', color: '#999', fontSize: 12 },
      }],
    })
    return
  }

  barChartInstance.setOption({
    grid: { left: '3%', right: '15%', bottom: '3%', top: '3%', containLabel: true },
    xAxis: { type: 'value', show: false },
    yAxis: {
      type: 'category',
      data: bankAccounts.value.map((b) => b.name),
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: '#666', fontSize: 12 },
    },
    series: [{
      type: 'bar',
      data: bankAccounts.value.map((b) => ({
        value: b.value,
        itemStyle: { color: '#3A6FB0' },
      })),
      barWidth: 16,
      label: { show: true, position: 'right', formatter: '{c}', color: '#666', fontSize: 12 },
    }],
  })
}

// 更新折线图（收支趋势 — 基于当前数据的简单展示）
function updateLineChart() {
  if (!lineChartInstance) return

  lineChartInstance.setOption({
    grid: { left: '3%', right: '4%', bottom: '3%', top: '12%', containLabel: true },
    tooltip: { trigger: 'axis' },
    legend: { data: ['收入', '支出'], top: 0, right: 0, textStyle: { fontSize: 12 } },
    xAxis: {
      type: 'category',
      data: selectedPeriod.value
        ? [`${selectedPeriod.value.year}.${String(selectedPeriod.value.periodNo).padStart(2, '0')}`]
        : ['当期'],
      axisLabel: { color: '#666', fontSize: 12 },
    },
    yAxis: {
      type: 'value',
      axisLabel: {
        color: '#666',
        fontSize: 12,
        formatter: (val: number) => (val / 10000).toFixed(0) + '万',
      },
    },
    series: [
      {
        name: '收入',
        type: 'line',
        data: [totalRevenue.value],
        itemStyle: { color: '#2BA471' },
        smooth: true,
      },
      {
        name: '支出',
        type: 'line',
        data: [totalExpense.value],
        itemStyle: { color: '#E5484D' },
        smooth: true,
      },
    ],
  })
}

// 窗口大小改变时重新调整图表大小
function handleResize() {
  barChartInstance?.resize()
  lineChartInstance?.resize()
}

// 监听账套变化
watch(
  () => accountSetStore.currentAccountSetId,
  () => {
    loadAllData()
  }
)

onMounted(() => {
  nextTick(() => initCharts())
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  barChartInstance?.dispose()
  lineChartInstance?.dispose()
})
</script>

<style scoped lang="scss">
.finance-dashboard {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  padding: 0 16px 16px;
  background: var(--bg-page);
}

.kpi-bar {
  display: flex;
  align-items: center;
  gap: 32px;
  padding: 14px 20px;
  background: #fff;
  border-radius: 8px;
  margin-bottom: 16px;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}
.kpi-item {
  display: flex;
  align-items: baseline;
  gap: 6px;
}
.kpi-item + .kpi-item {
  padding-left: 32px;
  border-left: 1px solid #d9d9d9;
}
.kpi-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}
.kpi-label {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
}
.kpi-value {
  font-size: 20px;
  font-weight: 600;
}

.dashboard-tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.dashboard-tabs :deep(.ant-tabs-tab) {
  font-size: 15px;
  padding: 12px 16px;
}
.dashboard-tabs :deep(.ant-tabs-ink-bar) {
  height: 3px;
  border-radius: 2px;
}
.dashboard-tabs :deep(.ant-tabs-content) {
  flex: 1;
  min-height: 0;
}
.dashboard-tabs :deep(.ant-tabs-content-holder) {
  flex: 1;
  display: flex;
  min-height: 0;
}
.dashboard-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
}
.tab-content {
  height: 100%;
}

.content-panel {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
  height: 100%;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.panel-title {
  font-size: 15px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 12px;
  padding-left: 10px;
  border-left: 3px solid var(--color-info);
  display: flex;
  align-items: center;
  gap: 8px;
}
.panel-subtitle {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  font-weight: normal;
}

.chart-container {
  height: 100%;
  min-height: 300px;
}

// 报表入口网格
.report-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}
.report-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 24px 16px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  border: 1px solid #f0f0f0;
}
.report-item:hover {
  background: var(--color-primary-light);
  border-color: var(--color-primary-border);
  box-shadow: 0 2px 8px var(--color-primary-border);
  transform: translateY(-2px);
}
.report-icon {
  font-size: 32px;
  margin-bottom: 8px;
  color: var(--text-2);
}
.report-name {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
}

// 老板驾驶舱
.boss-summary {
  display: flex;
  gap: 16px;
}
.boss-card {
  flex: 1;
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 20px;
  text-align: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.boss-card-label {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  margin-bottom: 8px;
}
.boss-card-value {
  font-size: 24px;
  font-weight: 600;

  .unit {
    font-size: 14px;
    font-weight: normal;
    color: rgba(0, 0, 0, 0.45);
    margin-left: 4px;
  }
}

// 表单提示文字
.form-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}
</style>
