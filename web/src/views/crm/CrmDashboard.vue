<template>
  <div class="crm-dashboard">
    <PageHeader title="CRM 业务看板" description="客户关系管理数据总览" />

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div v-for="(kpi, idx) in kpiList" :key="kpi.key" class="kpi-item">
        <span class="kpi-dot" :style="{ background: kpi.color }"></span>
        <span class="kpi-label">{{ kpi.label }}</span>
        <span class="kpi-value" :style="{ color: kpi.color }">{{ kpi.value }}</span>
      </div>
    </div>

    <!-- Tab 内容区 -->
    <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs" destroyInactiveTabPane>
      <a-tab-pane key="trend" tab="业务趋势">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="14">
              <div class="content-panel">
                <div class="panel-title">毛利趋势（近6月）</div>
                <div class="chart-container">
                  <a-spin :spinning="chartLoading">
                    <VChart ref="profitChartRef" :option="profitChartOption" autoresize style="height: 300px" />
                  </a-spin>
                </div>
              </div>
            </a-col>
            <a-col :span="10">
              <div class="content-panel">
                <div class="panel-title">工单分类占比</div>
                <div class="chart-container">
                  <a-spin :spinning="chartLoading">
                    <VChart ref="pieChartRef" :option="orderPieOption" autoresize style="height: 300px" />
                  </a-spin>
                </div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <a-tab-pane key="rank" tab="业绩排行">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">BD 签约排行 Top 5</div>
                <a-table
                  :columns="bdRankColumns"
                  :data-source="bdRankData"
                  :pagination="false"
                  size="small"
                  row-key="rank"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'rank'">
                      <a-tag :color="record.rank <= 3 ? 'gold' : 'default'">{{ record.rank }}</a-tag>
                    </template>
                    <template v-if="column.dataIndex === 'profit'">
                      {{ formatMoney(record.profit) }}
                    </template>
                  </template>
                </a-table>
              </div>
            </a-col>
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">运维工单排行 Top 5</div>
                <a-table
                  :columns="maintRankColumns"
                  :data-source="maintRankData"
                  :pagination="false"
                  size="small"
                  row-key="rank"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'rank'">
                      <a-tag :color="record.rank <= 3 ? 'gold' : 'default'">{{ record.rank }}</a-tag>
                    </template>
                    <template v-if="column.dataIndex === 'avgDuration'">
                      {{ record.avgDuration }}h
                    </template>
                  </template>
                </a-table>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <a-tab-pane key="todo" tab="待办事项">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">待处理工单</div>
                <a-list :data-source="pendingOrderList" size="small">
                  <template #renderItem="{ item }">
                    <a-list-item>
                      <a-list-item-meta>
                        <template #avatar>
                          <a-avatar :style="{ backgroundColor: 'var(--color-danger)' }" size="small">
                            <template #icon><ToolOutlined /></template>
                          </a-avatar>
                        </template>
                        <template #title>{{ item.title }}</template>
                        <template #description>{{ item.desc }}</template>
                      </a-list-item-meta>
                    </a-list-item>
                  </template>
                  <template #header v-if="pendingOrderList.length === 0">
                    <a-empty description="暂无待处理工单" :image="false" />
                  </template>
                </a-list>
              </div>
            </a-col>
            <a-col :span="12">
              <div class="content-panel">
                <div class="panel-title">客户跟进提醒</div>
                <a-list :data-source="followUpList" size="small">
                  <template #renderItem="{ item }">
                    <a-list-item>
                      <a-list-item-meta>
                        <template #avatar>
                          <a-avatar :style="{ backgroundColor: 'var(--color-info)' }" size="small">
                            <template #icon><UserOutlined /></template>
                          </a-avatar>
                        </template>
                        <template #title>{{ item.title }}</template>
                        <template #description>{{ item.desc }}</template>
                      </a-list-item-meta>
                    </a-list-item>
                  </template>
                  <template #header v-if="followUpList.length === 0">
                    <a-empty description="暂无跟进提醒" :image="false" />
                  </template>
                </a-list>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import {
  UserOutlined,
  ToolOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart, BarChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
} from 'echarts/components'
import {
  getCustomerList,
  getVisitRecordList,
  getServiceOrderList,
  getServiceOrderStatistics,
  getServiceFeedbackList,
  getProfitList,
  getPendingFollowUp,
  getCommissionList,
  getBonusPlanList,
  type ServiceOrderStatisticsDto,
} from '@/api/crm'

use([CanvasRenderer, LineChart, PieChart, BarChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

// ==================== Tab 控制 ====================

const activeTab = ref('trend')
const profitChartRef = ref<InstanceType<typeof VChart> | null>(null)
const pieChartRef = ref<InstanceType<typeof VChart> | null>(null)

watch(activeTab, (val) => {
  if (val === 'trend') {
    nextTick(() => {
      profitChartRef.value?.resize()
      pieChartRef.value?.resize()
    })
  }
})

// ==================== 指标数据 ====================

const stats = reactive({
  customerTotal: 0,
  customerMonthNew: 0,
  visitMonthCount: 0,
  pendingOrderCount: 0,
  activeContractCount: 0,
  feedbackMonthCount: 0,
})

const kpiList = computed(() => [
  { key: 'total', label: '客户总数', value: stats.customerTotal, color: 'var(--color-info)' },
  { key: 'new', label: '本月新增', value: stats.customerMonthNew, color: 'var(--color-success)' },
  { key: 'visit', label: '拜访量', value: stats.visitMonthCount, color: 'var(--color-info)' },
  { key: 'order', label: '待处理工单', value: stats.pendingOrderCount, color: 'var(--color-warning)' },
  { key: 'contract', label: '活跃合同', value: stats.activeContractCount, color: 'var(--biz-contract)' },
  { key: 'feedback', label: '反馈量', value: stats.feedbackMonthCount, color: 'var(--color-danger)' },
])

const chartLoading = ref(false)

// ==================== 毛利趋势图数据 ====================

const months = ref<string[]>([])
const profitData = ref<number[]>([])
const profitRateData = ref<number[]>([])

const profitChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['毛利', '毛利率'], bottom: 0 },
  grid: { top: 20, right: 60, bottom: 40, left: 60 },
  xAxis: { type: 'category', data: months.value },
  yAxis: [
    { type: 'value', name: '金额(元)' },
    { type: 'value', name: '毛利率(%)', axisLabel: { formatter: '{value}%' } },
  ],
  series: [
    { name: '毛利', type: 'line', data: profitData.value, smooth: true, itemStyle: { color: '#3A6FB0' } },
    { name: '毛利率', type: 'line', yAxisIndex: 1, data: profitRateData.value, smooth: true, itemStyle: { color: '#2BA471' } },
  ],
}))

// ==================== 工单饼图数据 ====================

const orderCategoryData = ref<{ name: string; value: number }[]>([])

const orderPieOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { bottom: 0, type: 'scroll' },
  series: [
    {
      type: 'pie',
      radius: ['40%', '65%'],
      center: ['50%', '45%'],
      data: orderCategoryData.value,
      emphasis: { itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0,0,0,0.5)' } },
      label: { formatter: '{b}\n{d}%' },
    },
  ],
}))

// ==================== 排行榜数据 ====================

const bdRankColumns = [
  { title: '排名', dataIndex: 'rank', width: 60, align: 'center' as const },
  { title: '姓名', dataIndex: 'name', ellipsis: true },
  { title: '签约客户数', dataIndex: 'count', width: 90, align: 'center' as const },
  { title: '毛利贡献', dataIndex: 'profit', width: 100, align: 'right' as const },
]

const maintRankColumns = [
  { title: '排名', dataIndex: 'rank', width: 60, align: 'center' as const },
  { title: '姓名', dataIndex: 'name', ellipsis: true },
  { title: '处理工单数', dataIndex: 'count', width: 90, align: 'center' as const },
  { title: '平均时长', dataIndex: 'avgDuration', width: 90, align: 'center' as const },
]

const bdRankData = ref<any[]>([])
const maintRankData = ref<any[]>([])

// ==================== 待办列表 ====================

interface TodoItem {
  key: string
  title: string
  desc: string
}

const pendingOrderList = ref<TodoItem[]>([])
const followUpList = ref<TodoItem[]>([])

function formatMoney(val: number): string {
  if (!val) return '¥0'
  return '¥' + val.toLocaleString('zh-CN', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

// ==================== 数据加载 ====================

function getMonthStr(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  return `${y}-${m}`
}

function getFirstDayOfMonth(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`
}

async function fetchStats() {
  try {
    const monthStart = getFirstDayOfMonth()

    const [customerRes, visitRes, feedbackRes, orderStats] = await Promise.allSettled([
      getCustomerList({ pageIndex: 1, pageSize: 1 }),
      getVisitRecordList({ pageIndex: 1, pageSize: 1, startDate: monthStart }),
      getServiceFeedbackList({ pageIndex: 1, pageSize: 1 }),
      getServiceOrderStatistics(),
    ])

    if (customerRes.status === 'fulfilled' && customerRes.value) {
      const r = customerRes.value as any
      stats.customerTotal = r.total ?? r.totalCount ?? 0
    }
    if (visitRes.status === 'fulfilled' && visitRes.value) {
      const r = visitRes.value as any
      stats.visitMonthCount = r.total ?? r.totalCount ?? 0
    }
    if (feedbackRes.status === 'fulfilled' && feedbackRes.value) {
      const r = feedbackRes.value as any
      stats.feedbackMonthCount = r.total ?? r.totalCount ?? 0
    }
    if (orderStats.status === 'fulfilled' && orderStats.value) {
      const r = orderStats.value as ServiceOrderStatisticsDto
      stats.pendingOrderCount = r.pending ?? 0
    }

    // 本月新增客户 - 暂用模拟数据
    stats.customerMonthNew = 12
    // 活跃合同数 - 暂用模拟数据
    stats.activeContractCount = 38
  } catch (e) {
    console.error('获取统计数据失败:', e)
  }
}

async function fetchProfitTrend() {
  chartLoading.value = true
  try {
    const now = new Date()
    const monthLabels: string[] = []
    const periods: string[] = []
    for (let i = 5; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
      monthLabels.push(`${d.getMonth() + 1}月`)
      periods.push(getMonthStr(d))
    }
    months.value = monthLabels

    // 尝试从 API 获取毛利数据
    try {
      const res = await getProfitList({ pageIndex: 1, pageSize: 200, startDate: periods[0], endDate: periods[5] }) as any
      const items = res?.items || res || []
      if (items.length > 0) {
        const profitMap: Record<string, { profit: number; rate: number }> = {}
        items.forEach((item: any) => {
          const p = item.period?.substring(0, 7)
          if (p) {
            if (!profitMap[p]) profitMap[p] = { profit: 0, rate: 0 }
            profitMap[p].profit += item.profit || 0
            profitMap[p].rate = item.profitRate || profitMap[p].rate
          }
        })
        profitData.value = periods.map(p => profitMap[p]?.profit || 0)
        profitRateData.value = periods.map(p => profitMap[p]?.rate || 0)
        return
      }
    } catch {
      // API 不可用时使用模拟数据
    }

    // 模拟数据
    profitData.value = [82000, 95000, 78000, 110000, 125000, 118000]
    profitRateData.value = [12.5, 14.2, 11.8, 16.3, 18.1, 17.5]
  } finally {
    chartLoading.value = false
  }
}

async function fetchOrderStats() {
  try {
    const res = await getServiceOrderList({ pageIndex: 1, pageSize: 500 }) as any
    const items = res?.items || res || []
    if (items.length > 0) {
      const categoryMap: Record<number, number> = {}
      items.forEach((item: any) => {
        categoryMap[item.category] = (categoryMap[item.category] || 0) + 1
      })
      const categoryNames: Record<number, string> = { 1: '咨询', 2: '投诉', 3: '故障', 4: '需求', 5: '其他' }
      orderCategoryData.value = Object.entries(categoryMap).map(([k, v]) => ({
        name: categoryNames[Number(k)] || `类型${k}`,
        value: v,
      }))
      return
    }
  } catch {
    // fallback
  }
  // 模拟数据
  orderCategoryData.value = [
    { name: '故障', value: 35 },
    { name: '咨询', value: 28 },
    { name: '维护', value: 20 },
    { name: '投诉', value: 12 },
    { name: '其他', value: 5 },
  ]
}

async function fetchRankings() {
  // 暂用模拟数据（后端暂未提供专门的排行 API）
  bdRankData.value = [
    { rank: 1, name: '张伟', count: 18, profit: 256000 },
    { rank: 2, name: '李娜', count: 15, profit: 218000 },
    { rank: 3, name: '王强', count: 12, profit: 185000 },
    { rank: 4, name: '赵敏', count: 9, profit: 142000 },
    { rank: 5, name: '刘洋', count: 7, profit: 98000 },
  ]
  maintRankData.value = [
    { rank: 1, name: '陈工', count: 42, avgDuration: 2.1 },
    { rank: 2, name: '周工', count: 38, avgDuration: 2.5 },
    { rank: 3, name: '吴工', count: 31, avgDuration: 3.2 },
    { rank: 4, name: '孙工', count: 25, avgDuration: 2.8 },
    { rank: 5, name: '郑工', count: 20, avgDuration: 3.5 },
  ]
}

async function fetchTodoList() {
  // 待处理工单
  try {
    const orderRes = await getServiceOrderList({ pageIndex: 1, pageSize: 10, status: 0 }) as any
    const orderItems = orderRes?.items || orderRes || []
    if (Array.isArray(orderItems) && orderItems.length > 0) {
      pendingOrderList.value = orderItems.slice(0, 5).map((item: any, idx: number) => ({
        key: `order-${idx}`,
        title: item.title || `工单 #${item.id}`,
        desc: item.description || '待处理',
      }))
    }
  } catch {
    // ignore
  }

  // 如果没有真实数据，展示模拟
  if (pendingOrderList.value.length === 0) {
    pendingOrderList.value = [
      { key: 'o1', title: '网络故障 - 城区分部', desc: '2小时前提交，等待接单' },
      { key: 'o2', title: '系统升级申请 - 沙溪', desc: '今日提交，待审核' },
      { key: 'o3', title: '打印机维修 - 浏河站', desc: '昨日提交，待派单' },
    ]
  }

  // 客户跟进提醒
  try {
    const followUpRes = await getPendingFollowUp() as any
    const followUpItems = followUpRes?.items || followUpRes || []
    if (Array.isArray(followUpItems) && followUpItems.length > 0) {
      followUpList.value = followUpItems.slice(0, 5).map((item: any, idx: number) => ({
        key: `fu-${idx}`,
        title: item.customerName || item.name || `客户 #${item.id}`,
        desc: item.nextFollowDate ? `跟进日期: ${item.nextFollowDate}` : '待跟进',
      }))
    }
  } catch {
    // ignore
  }

  // 如果没有真实数据，展示模拟
  if (followUpList.value.length === 0) {
    followUpList.value = [
      { key: 'f1', title: '太仓精密机械有限公司', desc: '跟进日期: 2026-05-06' },
      { key: 'f2', title: '苏州电子科技', desc: '跟进日期: 2026-05-07' },
      { key: 'f3', title: '昆山物流集团', desc: '跟进日期: 2026-05-08' },
    ]
  }
}

onMounted(async () => {
  await Promise.allSettled([
    fetchStats(),
    fetchProfitTrend(),
    fetchOrderStats(),
    fetchRankings(),
    fetchTodoList(),
  ])
})
</script>

<style scoped lang="scss">
.crm-dashboard {
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
}

.chart-container {
  height: 100%;
  min-height: 300px;
}
</style>
