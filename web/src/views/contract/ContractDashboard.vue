<template>
  <div class="contract-dashboard">
    <PageHeader title="合同看板">
      <template #left>
        <a-radio-group v-model:value="activeTab" button-style="solid" size="default">
          <a-radio-button v-for="opt in tabOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</a-radio-button>
        </a-radio-group>
      </template>
      <template #right>
        <a-button type="primary" size="middle" @click="router.push('/contract/list?action=create')">
          <template #icon><PlusOutlined /></template>新建合同
        </a-button>
      </template>
    </PageHeader>

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div v-for="(kpi, idx) in kpiItems" :key="kpi.label" class="kpi-item">
        <span class="kpi-dot" :style="{ background: kpi.color }"></span>
        <span class="kpi-label">{{ kpi.label }}</span>
        <span class="kpi-value" :style="{ color: kpi.color }">{{ kpi.value }}</span>
      </div>
    </div>

    <!-- 视图内容区(视图切换在首行段控件) -->
    <div class="dashboard-content">
      <div v-show="activeTab === 'overview'" class="tab-content">
        <a-row :gutter="16" style="height: 100%">
          <a-col :span="12" style="height: 100%">
            <div class="content-panel">
              <div class="panel-title">合同类型分布</div>
              <div class="chart-container" ref="pieChartRef"></div>
            </div>
          </a-col>
          <a-col :span="12" style="height: 100%">
            <div class="content-panel">
              <div class="panel-title">合同状态分布</div>
              <div class="chart-container" ref="barChartRef"></div>
            </div>
          </a-col>
        </a-row>
      </div>

      <div v-show="activeTab === 'warning'" class="tab-content">
        <a-row :gutter="16" style="height: 100%">
          <a-col :span="12" style="height: 100%">
            <div class="content-panel">
              <div class="panel-title">30天内到期合同</div>
              <a-table
                :columns="warningColumns"
                :data-source="warningList"
                :loading="loading"
                :pagination="false"
                row-key="id"
                :bordered="false"
                size="small"
                :scroll="{ y: 400 }"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'remainDays'">
                    <StatusTag :type="record.remainDays <= 7 ? 'danger' : 'warning'">
                      {{ record.remainDays }}天
                    </StatusTag>
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
                    <a-button type="link" size="small" @click="handleRenew(record)">续签</a-button>
                  </template>
                </template>
                <template #emptyText>
                  <EmptyState description="暂无到期预警" />
                </template>
              </a-table>
            </div>
          </a-col>
          <a-col :span="12" style="height: 100%">
            <div class="content-panel">
              <div class="panel-title">续签待办</div>
              <a-table
                :columns="renewColumns"
                :data-source="renewList"
                :loading="loading"
                :pagination="false"
                row-key="id"
                :bordered="false"
                size="small"
                :scroll="{ y: 400 }"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'status'">
                    <StatusTag :type="statusTagType(record.status)">
                      {{ contractStatusText(record.status) }}
                    </StatusTag>
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
                  </template>
                </template>
                <template #emptyText>
                  <EmptyState description="暂无续签待办" />
                </template>
              </a-table>
            </div>
          </a-col>
        </a-row>
      </div>

      <div v-show="activeTab === 'actions'" class="tab-content">
        <div class="content-panel" style="max-width: 600px">
          <div class="panel-title">快捷操作</div>
          <div class="quick-actions-grid">
            <div
              v-for="action in quickActions"
              :key="action.label"
              class="quick-action-item"
              @click="action.handler"
            >
              <div class="quick-action-icon">
                <component :is="action.icon" />
              </div>
              <span>{{ action.label }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart, BarChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import { PlusOutlined, SyncOutlined, ExportOutlined, AuditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getContractList,
  type ContractListItemDto,
} from '@/api/contract'

echarts.use([CanvasRenderer, PieChart, BarChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const router = useRouter()

// 状态映射
const statusMap: Record<number, string> = { 0: '草稿', 1: '审批中', 2: '待签署', 3: '已生效', 4: '已到期', 5: '已终止' }

function contractStatusText(status: number) {
  return statusMap[status] || '未知'
}

function statusTagType(s: number): 'success' | 'warning' | 'danger' | 'info' | 'default' {
  return (['default', 'info', 'warning', 'success', 'danger', 'default'] as const)[s] || 'default'
}

// 统计数据
const stats = reactive({
  total: 0,
  active: 0,
  expiringSoon: 0,
  expired: 0,
  pendingApproval: 0,
  monthNew: 0,
})

const kpiItems = computed(() => [
  { label: '合同总数', value: stats.total, color: 'var(--color-info)' },
  { label: '活跃', value: stats.active, color: 'var(--color-success)' },
  { label: '即将到期', value: stats.expiringSoon, color: 'var(--color-warning)' },
  { label: '待审批', value: stats.pendingApproval, color: 'var(--biz-approval)' },
  { label: '本月新增', value: stats.monthNew, color: 'var(--color-info)' },
  { label: '过期', value: stats.expired, color: 'var(--color-danger)' },
])

// 视图切换(段控件,置于首行工具栏)
const activeTab = ref('overview')
const tabOptions = [
  { label: '数据概览', value: 'overview' },
  { label: '到期预警', value: 'warning' },
  { label: '快捷操作', value: 'actions' },
]

// 图表
const pieChartRef = ref<HTMLElement>()
const barChartRef = ref<HTMLElement>()
let pieChart: echarts.ECharts | null = null
let barChart: echarts.ECharts | null = null

const typeDistribution = ref<{ name: string; value: number }[]>([])
const statusDistribution = ref<{ name: string; value: number }[]>([])

function initCharts() {
  if (activeTab.value !== 'overview') return
  nextTick(() => {
    if (pieChartRef.value && !pieChart) {
      pieChart = echarts.init(pieChartRef.value)
    }
    if (barChartRef.value && !barChart) {
      barChart = echarts.init(barChartRef.value)
    }
    updateCharts()
  })
}

function updateCharts() {
  if (pieChart) {
    pieChart.setOption({
      color: ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A'],
      tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
      legend: { bottom: 0 },
      series: [{
        type: 'pie',
        radius: ['40%', '65%'],
        data: typeDistribution.value,
        emphasis: { itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0, 0, 0, 0.5)' } },
      }],
    })
  }
  if (barChart) {
    barChart.setOption({
      tooltip: { trigger: 'axis' },
      grid: { left: 40, right: 20, bottom: 30, top: 20 },
      xAxis: { type: 'category', data: statusDistribution.value.map(i => i.name) },
      yAxis: { type: 'value', minInterval: 1 },
      series: [{
        type: 'bar',
        data: statusDistribution.value.map(i => i.value),
        itemStyle: {
          color: (params: any) => {
            // 状态语义色(0草稿中性/1审批info/2待签warning/3生效success/4到期danger/5终止中性);ECharts 不解析 var()，用 theme.ts 真 hex
            const colors = ['#8A9099', '#5B7290', '#D49A2E', '#3E9E6E', '#D6584E', '#8A9099']
            return colors[params.dataIndex] || '#8A9099'
          },
        },
        barWidth: 40,
      }],
    })
  }
}

function handleResize() {
  pieChart?.resize()
  barChart?.resize()
}

// watch activeTab 切换时 resize 图表
watch(activeTab, (key) => {
  if (key === 'overview') {
    nextTick(() => {
      if (!pieChart && pieChartRef.value) {
        pieChart = echarts.init(pieChartRef.value)
      }
      if (!barChart && barChartRef.value) {
        barChart = echarts.init(barChartRef.value)
      }
      updateCharts()
      handleResize()
    })
  }
})

// 到期预警列表
const warningColumns = [
  { title: '合同号', dataIndex: 'contractNo', key: 'contractNo', width: 140 },
  { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '到期日', dataIndex: 'endDate', key: 'endDate', width: 110 },
  { title: '剩余天数', dataIndex: 'remainDays', key: 'remainDays', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const },
]

const renewColumns = [
  { title: '合同号', dataIndex: 'contractNo', key: 'contractNo', width: 140 },
  { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 150 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const },
]

const warningList = ref<any[]>([])
const renewList = ref<ContractListItemDto[]>([])
const loading = ref(false)

// 快捷操作
const quickActions = [
  { label: '新建合同', icon: PlusOutlined, handler: () => router.push('/contract/list?action=create') },
  { label: '续签合同', icon: SyncOutlined, handler: () => router.push('/contract/list?action=renew') },
  { label: '导出报表', icon: ExportOutlined, handler: () => router.push('/contract/list?action=export') },
  { label: '合同审批', icon: AuditOutlined, handler: () => router.push('/contract/list?status=1') },
]

function handleView(record: any) {
  router.push({ path: '/contract/list', query: { keyword: record.contractNo } })
}

function handleRenew(record: any) {
  router.push({ path: '/contract/list', query: { keyword: record.contractNo } })
}

async function fetchDashboardData() {
  loading.value = true
  try {
    const res = await getContractList({ pageIndex: 1, pageSize: 9999 }) as any
    const allContracts: ContractListItemDto[] = res?.items || res || []

    const now = new Date()
    const thirtyDaysLater = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000)
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1)

    stats.total = allContracts.length
    stats.active = allContracts.filter(c => c.status === 3).length
    stats.expired = allContracts.filter(c => c.status === 4).length
    stats.pendingApproval = allContracts.filter(c => c.status === 1).length
    stats.monthNew = allContracts.filter(c => new Date(c.createdTime) >= monthStart).length

    // 即将到期：已生效且结束日期在30天内
    const expiringContracts = allContracts.filter(c => {
      if (c.status !== 3 || !c.endDate) return false
      const end = new Date(c.endDate)
      return end >= now && end <= thirtyDaysLater
    })
    stats.expiringSoon = expiringContracts.length

    // 到期预警列表
    warningList.value = expiringContracts.map(c => {
      const remainDays = Math.ceil((new Date(c.endDate!).getTime() - now.getTime()) / (24 * 60 * 60 * 1000))
      return { ...c, remainDays }
    }).sort((a, b) => a.remainDays - b.remainDays)

    // 按类型分布
    const typeCount: Record<string, number> = {}
    allContracts.forEach(c => {
      const name = c.typeName || '未分类'
      typeCount[name] = (typeCount[name] || 0) + 1
    })
    typeDistribution.value = Object.entries(typeCount).map(([name, value]) => ({ name, value }))

    // 按状态分布
    const statusCount: Record<number, number> = { 0: 0, 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 }
    allContracts.forEach(c => {
      if (statusCount[c.status] !== undefined) statusCount[c.status]++
    })
    statusDistribution.value = Object.entries(statusMap).map(([key, name]) => ({
      name,
      value: statusCount[Number(key)] || 0,
    }))

    // 续签待办：合同性质为续签(1)且状态为草稿(0)或审批中(1)
    renewList.value = allContracts.filter(c => c.contractNature === 1 && (c.status === 0 || c.status === 1))

    // 初始化图表
    initCharts()
  } catch (error) {
    console.error('获取看板数据失败:', error)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchDashboardData()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  pieChart?.dispose()
  barChart?.dispose()
  pieChart = null
  barChart = null
})
</script>

<style scoped lang="scss">
.contract-dashboard {
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
  background: var(--bg-card);
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
  border-left: 1px solid var(--border);
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
  color: var(--text-3);
}
.kpi-value {
  font-size: 20px;
  font-weight: 600;
}

.dashboard-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.tab-content {
  flex: 1;
  min-height: 0;
}

.content-panel {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 16px;
  height: 100%;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.panel-title {
  font-size: 15px;
  font-weight: 500;
  color: var(--text-1);
  margin-bottom: 12px;
  padding-left: 10px;
  border-left: 3px solid var(--color-info);
}

.chart-container {
  height: 100%;
  min-height: 300px;
}

.quick-actions-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}
.quick-action-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 20px 8px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  border: 1px solid var(--border);
}
.quick-action-item:hover {
  background: var(--color-primary-light);
  border-color: var(--color-primary-border);
  box-shadow: 0 2px 8px var(--color-primary-border);
  transform: translateY(-2px);
}
.quick-action-icon {
  font-size: 24px;
  margin-bottom: 8px;
  color: var(--text-2);
  background: var(--bg-muted);
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
