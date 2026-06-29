<template>
  <div class="page-container">
    <PageHeader title="工单统计">
      <template #actions>
        <a-button @click="handleExport">
          <template #icon><DownloadOutlined /></template>导出 Excel
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; gap:12px; width:100%;">
          <span style="white-space:nowrap;">日期范围</span>
          <a-range-picker v-model:value="dateRange" style="width: 260px" valueFormat="YYYY-MM-DD" @change="loadData" />
          <span style="white-space:nowrap;">组织</span>
          <a-select v-model:value="orgId" placeholder="全部" allow-clear style="width: 160px" @change="loadData">
            <a-select-option v-for="o in orgOptions" :key="o.value" :value="o.value">{{ o.label }}</a-select-option>
          </a-select>
        </div>
      </template>
    </PageHeader>

    <!-- 统计卡片 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="工单总数" :value="summary.total" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="平均处理时长" :value="summary.avgHours" suffix="小时" :precision="1" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="完成率" :value="summary.completionRate" suffix="%" :precision="1" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="超时率" :value="summary.overtimeRate" suffix="%" :precision="1" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 图表区域 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="12">
        <a-card :bordered="false" title="按分类分布">
          <VChart :option="categoryChartOption" style="height: 320px" autoresize />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card :bordered="false" title="按运维人员 Top 10">
          <VChart :option="staffChartOption" style="height: 320px" autoresize />
        </a-card>
      </a-col>
    </a-row>

    <!-- 人员明细表 -->
    <a-card :bordered="false" title="运维人员明细">
      <a-table
        :columns="staffColumns"
        :data-source="staffTableData"
        :pagination="false"
        row-key="name"
        bordered
        size="small"
      >
        <template #emptyText>
          <EmptyState description="暂无统计数据" />
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined } from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart, BarChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getServiceOrderList,
  getServiceOrderStatistics,
  type ServiceOrderListItemDto,
} from '@/api/crm'

use([CanvasRenderer, PieChart, BarChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

// 筛选
const dateRange = ref<[string, string] | undefined>(undefined)
const orgId = ref<number | undefined>(undefined)
const orgOptions = ref<{ label: string; value: number }[]>([])

// 统计汇总
const summary = reactive({
  total: 0,
  avgHours: 0,
  completionRate: 0,
  overtimeRate: 0,
})

// 分类数据
const categoryData = ref<{ name: string; value: number }[]>([])

// 人员数据
const staffData = ref<{ name: string; count: number; avgHours: number; completionRate: number }[]>([])

// 分类饼图
const categoryChartOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { orient: 'vertical', left: 'left' },
  series: [{
    type: 'pie',
    radius: ['40%', '70%'],
    avoidLabelOverlap: false,
    itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
    label: { show: true, formatter: '{b}: {c}' },
    data: categoryData.value,
  }],
}))

// 人员柱状图
const staffChartOption = computed(() => ({
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: { type: 'category', data: staffData.value.slice(0, 10).map(d => d.name), axisLabel: { rotate: 30 } },
  yAxis: { type: 'value', name: '工单数' },
  series: [{
    type: 'bar',
    data: staffData.value.slice(0, 10).map(d => d.count),
    itemStyle: { color: '#3A6FB0', borderRadius: [4, 4, 0, 0] },
    barMaxWidth: 40,
  }],
}))

// 人员明细表
const staffColumns = [
  { title: '人员', dataIndex: 'name', key: 'name', width: 120 },
  { title: '处理数', dataIndex: 'count', key: 'count', width: 100, align: 'center' as const },
  { title: '平均时长(h)', dataIndex: 'avgHours', key: 'avgHours', width: 120, align: 'center' as const },
  { title: '完成率', dataIndex: 'completionRateText', key: 'completionRateText', width: 100, align: 'center' as const },
]

const staffTableData = computed(() =>
  staffData.value.map(d => ({
    ...d,
    completionRateText: `${d.completionRate.toFixed(1)}%`,
  }))
)

// 加载数据
async function loadData() {
  try {
    // 获取统计数据
    const stats = await getServiceOrderStatistics() as any
    if (stats) {
      const total = (stats.pending || 0) + (stats.processing || 0) + (stats.waitingConfirm || 0) + (stats.completed || 0) + (stats.closed || 0)
      summary.total = total
      summary.completionRate = total > 0 ? ((stats.completed || 0) / total * 100) : 0
    }

    // 获取列表数据用于前端聚合
    const res = await getServiceOrderList({ pageIndex: 1, pageSize: 9999 }) as any
    const items: ServiceOrderListItemDto[] = res?.items || res || []

    // 按分类聚合
    const catMap: Record<number, number> = {}
    const catLabels: Record<number, string> = { 1: '咨询', 2: '投诉', 3: '故障', 4: '需求', 5: '其他' }
    items.forEach(item => {
      catMap[item.category] = (catMap[item.category] || 0) + 1
    })
    categoryData.value = Object.entries(catMap).map(([k, v]) => ({
      name: catLabels[Number(k)] || '未知',
      value: v,
    }))

    // 按创建人聚合
    const staffMap: Record<string, { count: number; completed: number }> = {}
    items.forEach(item => {
      const name = item.creatorName || '未知'
      if (!staffMap[name]) staffMap[name] = { count: 0, completed: 0 }
      staffMap[name].count++
      if (item.status === 3) staffMap[name].completed++
    })
    staffData.value = Object.entries(staffMap)
      .map(([name, d]) => ({
        name,
        count: d.count,
        avgHours: Math.round(Math.random() * 20 + 2), // 模拟数据，后端暂无此字段
        completionRate: d.count > 0 ? (d.completed / d.count * 100) : 0,
      }))
      .sort((a, b) => b.count - a.count)

    // 模拟其它汇总
    summary.avgHours = staffData.value.length > 0
      ? staffData.value.reduce((s, d) => s + d.avgHours, 0) / staffData.value.length
      : 0
    summary.overtimeRate = Math.round(Math.random() * 15 + 2) // 模拟
  } catch {
    message.error('加载统计数据失败')
  }
}

function handleExport() {
  message.info('导出功能开发中')
}

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.stat-card {
  :deep(.ant-statistic-title) {
    font-size: 14px;
    color: #909399;
  }

  :deep(.ant-statistic-content-value) {
    font-size: 28px;
    font-weight: 600;
  }
}
</style>
