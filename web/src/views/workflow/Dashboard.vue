<template>
  <div class="workflow-dashboard">
    <!-- 总览卡片行 -->
    <a-row :gutter="16" class="overview-row">
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="待处理"
            :value="overview?.totalPending ?? 0"
            :value-style="{ color: 'var(--color-warning)' }"
          >
            <template #prefix><ClockCircleOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="处理中"
            :value="overview?.totalInProgress ?? 0"
            :value-style="{ color: 'var(--color-info)' }"
          >
            <template #prefix><SyncOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="今日完成"
            :value="overview?.completedToday ?? 0"
            :value-style="{ color: 'var(--color-success)' }"
          >
            <template #prefix><CheckCircleOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <div class="dual-stat">
            <a-statistic
              title="超时"
              :value="overview?.overdueCount ?? 0"
              :value-style="{ color: 'var(--color-danger)', fontSize: '20px' }"
            />
            <a-divider type="vertical" style="height: 40px" />
            <a-statistic
              title="SLA达标率"
              :value="overview?.slaRate ?? 0"
              suffix="%"
              :value-style="{ color: 'var(--color-info)', fontSize: '20px' }"
            />
          </div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 图表区 -->
    <a-row :gutter="16" class="chart-row">
      <!-- 左侧 2/3 -->
      <a-col :span="16">
        <!-- 趋势折线图 -->
        <a-card title="工作项趋势（近7天）" :bordered="false" class="chart-card">
          <template #extra>
            <a-radio-group v-model:value="trendDays" size="small" @change="onTrendDaysChange">
              <a-radio-button :value="7">7天</a-radio-button>
              <a-radio-button :value="14">14天</a-radio-button>
              <a-radio-button :value="30">30天</a-radio-button>
            </a-radio-group>
          </template>
          <VChart :option="trendChartOption" autoresize style="height: 300px" />
        </a-card>

        <!-- 人效排行表 -->
        <a-card title="人效排行" :bordered="false" class="chart-card" style="margin-top: 16px">
          <a-table
            :columns="assigneeColumns"
            :data-source="assigneeStats"
            :pagination="false"
            size="small"
            row-key="assigneeId"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'rank'">
                <a-tag :color="index < 3 ? 'gold' : 'default'">{{ index + 1 }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'avgProcessHours'">
                {{ record.avgProcessHours }}h
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <!-- 右侧 1/3 -->
      <a-col :span="8">
        <!-- 模块分布饼图 -->
        <a-card title="模块分布" :bordered="false" class="chart-card">
          <VChart :option="modulePieOption" autoresize style="height: 300px" />
        </a-card>

        <!-- 超时列表 -->
        <a-card title="超时工作项" :bordered="false" class="chart-card" style="margin-top: 16px">
          <a-table
            :columns="overdueColumns"
            :data-source="overduePage?.items ?? []"
            :pagination="overduePagination"
            size="small"
            row-key="id"
            @change="onOverdueTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'overdueHours'">
                <span style="color: var(--color-danger); font-weight: 500">{{ record.overdueHours }}h</span>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  ClockCircleOutlined,
  SyncOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
} from 'echarts/components'
import { useDashboard } from './composables/useDashboard'

use([CanvasRenderer, LineChart, PieChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const {
  loading,
  overview,
  moduleGroups,
  assigneeStats,
  trend,
  overduePage,
  loadAll,
  loadTrend,
  loadOverdueItems,
  startAutoRefresh,
} = useDashboard()

const trendDays = ref(7)

// ===== 趋势图 =====
const trendChartOption = computed(() => {
  if (!trend.value) return {}
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['新建', '完成'] },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', data: trend.value.dates, boundaryGap: false },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      {
        name: '新建',
        type: 'line',
        data: trend.value.createdCounts,
        smooth: true,
        itemStyle: { color: '#3A6FB0' },
        areaStyle: { color: 'rgba(58,111,176,0.1)' },
      },
      {
        name: '完成',
        type: 'line',
        data: trend.value.completedCounts,
        smooth: true,
        itemStyle: { color: '#2BA471' },
        areaStyle: { color: 'rgba(82,196,26,0.1)' },
      },
    ],
  }
})

// ===== 模块饼图 =====
const modulePieOption = computed(() => {
  if (!moduleGroups.value.length) return {}
  const data = moduleGroups.value.map(m => ({
    name: m.module,
    value: m.pendingCount + m.inProgressCount + m.completedCount,
  }))
  return {
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', left: 'left', top: 'middle' },
    series: [
      {
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
        data,
      },
    ],
  }
})

// ===== 人效排行表 =====
const assigneeColumns = [
  { title: '#', dataIndex: 'rank', width: 50 },
  { title: '处理人', dataIndex: 'assigneeName', width: 100 },
  { title: '待处理', dataIndex: 'pendingCount', width: 70, sorter: (a: any, b: any) => a.pendingCount - b.pendingCount },
  { title: '处理中', dataIndex: 'inProgressCount', width: 70 },
  { title: '已完成', dataIndex: 'completedCount', width: 70, sorter: (a: any, b: any) => a.completedCount - b.completedCount },
  { title: '平均时长', dataIndex: 'avgProcessHours', width: 90, sorter: (a: any, b: any) => a.avgProcessHours - b.avgProcessHours },
]

// ===== 超时列表 =====
const overdueColumns = [
  { title: '标题', dataIndex: 'title', ellipsis: true },
  { title: '处理人', dataIndex: 'assigneeName', width: 80 },
  { title: '状态', dataIndex: 'status', width: 70 },
  { title: '超时', dataIndex: 'overdueHours', width: 70 },
]

const overduePagination = computed(() => ({
  current: overduePage.value?.page ?? 1,
  pageSize: overduePage.value?.pageSize ?? 20,
  total: overduePage.value?.total ?? 0,
  size: 'small' as const,
  showTotal: (total: number) => `共 ${total} 条`,
}))

function statusText(status: number) {
  const map: Record<number, string> = { 0: '待处理', 1: '处理中', 2: '已完成', 3: '已取消', 4: '已超时' }
  return map[status] ?? '未知'
}

function statusColor(status: number) {
  const map: Record<number, string> = { 0: 'orange', 1: 'blue', 2: 'green', 3: 'default', 4: 'red' }
  return map[status] ?? 'default'
}

function onTrendDaysChange() {
  loadTrend(trendDays.value)
}

function onOverdueTableChange(pagination: any) {
  loadOverdueItems(pagination.current, pagination.pageSize)
}

onMounted(() => {
  loadAll()
  startAutoRefresh()
})
</script>

<style scoped>
.workflow-dashboard {
  padding: 16px;
}

.overview-row {
  margin-bottom: 16px;
}

.stat-card {
  border-radius: 8px;
}

.dual-stat {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.chart-card {
  border-radius: 8px;
}

.chart-row {
  margin-top: 0;
}
</style>
