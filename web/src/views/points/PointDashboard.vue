<template>
  <div class="points-dashboard">
    <PageHeader title="积分总览" />

    <!-- 双账户卡片：A 分（终身资本）/ B 分（周期清算） -->
    <div class="account-cards">
      <div class="account-card account-card--a">
        <div class="account-card__head">
          <span class="account-card__tag account-card__tag--a">A 分</span>
          <span class="account-card__title">终身资本积分</span>
        </div>
        <div class="account-card__value">{{ account?.fAPoints ?? 0 }}</div>
        <div class="account-card__desc">不参与清算 · 不可消费 · 作为个人财富资本永久保留</div>
      </div>

      <div class="account-card account-card--b">
        <div class="account-card__head">
          <span class="account-card__tag account-card__tag--b">B 分</span>
          <span class="account-card__title">周期清算积分</span>
        </div>
        <div class="account-card__value">{{ account?.fBPoints ?? 0 }}</div>
        <div class="account-card__desc">可兑换福利券 · 将于月初/年初清算转福利券</div>
      </div>
    </div>

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div class="kpi-item" v-for="kpi in kpiList" :key="kpi.label">
        <span class="kpi-dot" :style="{ background: kpi.color }"></span>
        <span class="kpi-label">{{ kpi.label }}</span>
        <span class="kpi-value" :style="{ color: kpi.color }">{{ kpi.value }}</span>
      </div>
    </div>

    <!-- Tab 切换区 -->
    <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs" destroyInactiveTabPane>
      <!-- Tab1: 积分趋势 -->
      <a-tab-pane key="trend" tab="积分趋势">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="14">
              <div class="content-panel">
                <div class="panel-title">近6月积分变化</div>
                <div class="chart-container" ref="trendChartRef"></div>
              </div>
            </a-col>
            <a-col :span="10">
              <div class="content-panel">
                <div class="panel-title">获得 / 使用对比</div>
                <div class="chart-container" ref="barChartRef"></div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <!-- Tab2: 积分明细 -->
      <a-tab-pane key="records" tab="积分明细">
        <div class="tab-content">
          <div class="content-panel">
            <div class="panel-title">最近积分变动</div>
            <a-table
              :columns="recordColumns"
              :data-source="recentRecords"
              :loading="recordsLoading"
              :pagination="false"
              row-key="id"
              size="middle"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'pointValue'">
                  <span :style="{ color: record.pointValue > 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }">
                    {{ record.pointValue > 0 ? '+' : '' }}{{ record.pointValue }}
                  </span>
                </template>
                <template v-if="column.dataIndex === 'type'">
                  <a-tag :color="record.type === 1 ? 'green' : 'red'">
                    {{ record.type === 1 ? '奖分' : '扣分' }}
                  </a-tag>
                </template>
                <template v-if="column.dataIndex === 'createTime'">
                  {{ formatTime(record.createTime) }}
                </template>
              </template>
            </a-table>
          </div>
        </div>
      </a-tab-pane>

      <!-- Tab3: 排行榜 -->
      <a-tab-pane key="ranking" tab="排行榜">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">团队积分排行</div>
                <a-table
                  :columns="rankColumns"
                  :data-source="rankingList"
                  :loading="rankListLoading"
                  :pagination="false"
                  row-key="userId"
                  size="middle"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'rank'">
                      <span :style="{ color: record.rank <= 3 ? 'var(--color-warning)' : undefined, fontWeight: record.rank <= 3 ? 700 : 400 }">
                        {{ record.rank }}
                      </span>
                    </template>
                  </template>
                </a-table>
              </div>
            </a-col>
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">个人奖扣明细</div>
                <div v-if="myRanking" class="ranking-summary">
                  <div class="ranking-summary__item">
                    <span class="ranking-summary__label">我的排名</span>
                    <span class="ranking-summary__value" style="color: var(--color-info)">第 {{ myRanking.rank }} 名</span>
                  </div>
                  <div class="ranking-summary__item">
                    <span class="ranking-summary__label">总积分</span>
                    <span class="ranking-summary__value" style="color: var(--color-info)">{{ myRanking.totalPoints }}</span>
                  </div>
                  <div class="ranking-summary__item">
                    <span class="ranking-summary__label">奖分</span>
                    <span class="ranking-summary__value" style="color: var(--color-success)">+{{ myRanking.awardPoints }}</span>
                  </div>
                  <div class="ranking-summary__item">
                    <span class="ranking-summary__label">扣分</span>
                    <span class="ranking-summary__value" style="color: var(--color-danger)">-{{ myRanking.deductPoints }}</span>
                  </div>
                </div>
                <EmptyState v-else size="small" title="暂无排名数据" />
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import * as echarts from 'echarts'
import PageHeader from '@/components/PageHeader.vue'
import { getMyPointAccount, getMyPointRecords, getMyRanking, getRankings } from '@/api/points'
import type { PointAccountDto, PointRecordListDto, MyRankingDto, TrendItem, RankingListDto } from '@/types/points'

// ==================== 状态 ====================

const activeTab = ref('trend')

// 账户数据
const account = ref<PointAccountDto | null>(null)

// 趋势数据
const trendData = ref<TrendItem[]>([])

// 排名数据
const myRanking = ref<MyRankingDto | null>(null)

// 排行榜列表
const rankingList = ref<RankingListDto[]>([])
const rankListLoading = ref(false)

// 最近记录
const recentRecords = ref<PointRecordListDto[]>([])
const recordsLoading = ref(false)

// ==================== KPI ====================

const kpiList = computed(() => {
  const a = account.value
  return [
    { label: '总积分', value: a?.totalPoints ?? 0, color: 'var(--color-info)' },
    { label: '可用积分', value: a?.availablePoints ?? 0, color: 'var(--color-success)' },
    { label: '本月获得', value: a?.monthlyAward ?? 0, color: 'var(--color-info)' },
    { label: '本月使用', value: a?.monthlyDeduct ?? 0, color: 'var(--color-warning)' },
    { label: '我的排名', value: myRanking.value?.rank ?? '-', color: 'var(--color-info)' },
  ]
})

// ==================== 表格列 ====================

const recordColumns = [
  { title: '时间', dataIndex: 'createTime', width: 160 },
  { title: '来源', dataIndex: 'sourceName', width: 120 },
  { title: '类型', dataIndex: 'type', width: 80 },
  { title: '规则', dataIndex: 'ruleName', width: 150 },
  { title: '积分', dataIndex: 'pointValue', width: 100 },
  { title: '余额', dataIndex: 'balance', width: 100 },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '操作人', dataIndex: 'operatorName', width: 100 },
]

const rankColumns = [
  { title: '排名', dataIndex: 'rank', width: 60 },
  { title: '姓名', dataIndex: 'userName', width: 100 },
  { title: '部门', dataIndex: 'departmentName', ellipsis: true },
  { title: '总积分', dataIndex: 'totalPoints', width: 80 },
  { title: '奖分', dataIndex: 'awardPoints', width: 80 },
  { title: '扣分', dataIndex: 'deductPoints', width: 80 },
]

// ==================== ECharts ====================

const trendChartRef = ref<HTMLElement>()
const barChartRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null
let barChart: echarts.ECharts | null = null

function initCharts() {
  if (trendChartRef.value && !trendChart) {
    trendChart = echarts.init(trendChartRef.value)
  }
  if (barChartRef.value && !barChart) {
    barChart = echarts.init(barChartRef.value)
  }
  renderTrendChart()
  renderBarChart()
}

function renderTrendChart() {
  if (!trendChart) return
  const periods = trendData.value.map(d => d.period)
  const nets = trendData.value.map(d => d.netPoints)

  trendChart.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: '3%', right: '4%', bottom: '10%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: periods, boundaryGap: false },
    yAxis: { type: 'value' },
    series: [
      {
        name: '净积分',
        type: 'line',
        data: nets,
        smooth: true,
        itemStyle: { color: '#3A6FB0' },
        areaStyle: { color: 'rgba(58,111,176,0.1)' },
        lineStyle: { width: 3 },
      },
    ],
  })
}

function renderBarChart() {
  if (!barChart) return
  const periods = trendData.value.map(d => d.period)
  const awards = trendData.value.map(d => d.awardPoints)
  const deducts = trendData.value.map(d => d.deductPoints)

  barChart.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['获得', '使用'], bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '14%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: periods },
    yAxis: { type: 'value' },
    series: [
      {
        name: '获得',
        type: 'bar',
        data: awards,
        itemStyle: { color: '#2BA471', borderRadius: [4, 4, 0, 0] },
      },
      {
        name: '使用',
        type: 'bar',
        data: deducts,
        itemStyle: { color: '#E6A700', borderRadius: [4, 4, 0, 0] },
      },
    ],
  })
}

function handleResize() {
  trendChart?.resize()
  barChart?.resize()
}

// Tab 切换时 resize 图表
watch(activeTab, (val) => {
  if (val === 'trend') {
    nextTick(() => {
      if (!trendChart && trendChartRef.value) {
        trendChart = echarts.init(trendChartRef.value)
      }
      if (!barChart && barChartRef.value) {
        barChart = echarts.init(barChartRef.value)
      }
      renderTrendChart()
      renderBarChart()
      handleResize()
    })
  }
})

// ==================== 工具函数 ====================

function formatTime(t: string) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 16)
}

// ==================== 数据加载 ====================

async function loadAccount() {
  try {
    account.value = await getMyPointAccount()
  } catch {
    message.error('加载积分账户失败')
  }
}

async function loadRecords() {
  recordsLoading.value = true
  try {
    const res = await getMyPointRecords({ pageIndex: 1, pageSize: 10 })
    recentRecords.value = res.items
  } catch {
    message.error('加载积分记录失败')
  } finally {
    recordsLoading.value = false
  }
}

async function loadRanking() {
  try {
    myRanking.value = await getMyRanking({ dimension: 0 })
    // 使用排名趋势数据作为趋势图数据
    if (myRanking.value?.trends?.length) {
      trendData.value = myRanking.value.trends.map(t => ({
        period: t.period,
        awardPoints: t.totalPoints,
        deductPoints: 0,
        netPoints: t.totalPoints,
      }))
    }
  } catch {
    message.error('加载排名数据失败')
  }
}

async function loadRankingList() {
  rankListLoading.value = true
  try {
    const res = await getRankings({ pageIndex: 1, pageSize: 20, dimension: 0 })
    rankingList.value = res.items
  } catch {
    message.error('加载排行榜失败')
  } finally {
    rankListLoading.value = false
  }
}

onMounted(async () => {
  await Promise.all([loadAccount(), loadRecords(), loadRanking(), loadRankingList()])
  // 初始化图表（Tab1 默认激活）
  nextTick(() => {
    initCharts()
  })
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  trendChart?.dispose()
  barChart?.dispose()
})
</script>

<style scoped lang="scss">
.points-dashboard {
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

.account-cards {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 16px;
  flex-shrink: 0;
}
.account-card {
  background: #fff;
  border-radius: 10px;
  padding: 18px 22px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
  border-left: 4px solid #1890ff;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.account-card--a { border-left-color: #722ed1; }
.account-card--b { border-left-color: #fa8c16; }
.account-card__head { display: flex; align-items: center; gap: 10px; }
.account-card__tag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 22px;
  padding: 0 10px;
  border-radius: 11px;
  font-size: 12px;
  color: #fff;
  font-weight: 600;
}
.account-card__tag--a { background: #722ed1; }
.account-card__tag--b { background: #fa8c16; }
.account-card__title { font-size: 14px; color: rgba(0, 0, 0, 0.65); }
.account-card__value { font-size: 30px; font-weight: 700; color: rgba(0, 0, 0, 0.88); line-height: 1.2; }
.account-card__desc { font-size: 12px; color: rgba(0, 0, 0, 0.45); }
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
}

.chart-container {
  height: 100%;
  min-height: 300px;
}

.ranking-summary {
  padding: 12px 0;

  &__item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 16px;
    border-bottom: 1px solid #f5f5f5;

    &:last-child {
      border-bottom: none;
    }
  }

  &__label {
    font-size: 14px;
    color: rgba(0, 0, 0, 0.65);
  }

  &__value {
    font-size: 18px;
    font-weight: 600;
  }
}
</style>
