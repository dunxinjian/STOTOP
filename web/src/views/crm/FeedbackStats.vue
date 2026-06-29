<template>
  <div class="page-container">
    <PageHeader title="反馈统计">
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
          <a-statistic title="反馈总数" :value="summary.total" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="落实率" :value="summary.implementRate" suffix="%" :precision="1" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="平均处理周期" :value="summary.avgDays" suffix="天" :precision="1" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic title="高频分类" :value="summary.topCategory" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 图表区域 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="8">
        <a-card :bordered="false" title="按分类分布">
          <VChart :option="categoryChartOption" style="height: 300px" autoresize />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card :bordered="false" title="月度趋势">
          <VChart :option="trendChartOption" style="height: 300px" autoresize />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card :bordered="false" title="按组织对比">
          <VChart :option="orgChartOption" style="height: 300px" autoresize />
        </a-card>
      </a-col>
    </a-row>

    <!-- 高频问题表格 -->
    <a-card :bordered="false" title="高频问题">
      <a-table
        :columns="freqColumns"
        :data-source="freqTableData"
        :pagination="false"
        row-key="category"
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
import { PieChart, BarChart, LineChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getServiceFeedbackList,
  type ServiceFeedbackListItemDto,
} from '@/api/crm'

use([CanvasRenderer, PieChart, BarChart, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

// 筛选
const dateRange = ref<[string, string] | undefined>(undefined)
const orgId = ref<number | undefined>(undefined)
const orgOptions = ref<{ label: string; value: number }[]>([])

// 统计汇总
const summary = reactive({
  total: 0,
  implementRate: 0,
  avgDays: 0,
  topCategory: '-',
})

// 分类数据
const categoryData = ref<{ name: string; value: number }[]>([])

// 趋势数据
const trendData = ref<{ months: string[]; values: number[] }>({ months: [], values: [] })

// 组织对比数据
const orgData = ref<{ names: string[]; values: number[] }>({ names: [], values: [] })

// 高频问题数据
const freqTableData = ref<{ category: string; count: number; implementRate: string }[]>([])

// 分类标签映射
const catLabels: Record<number, string> = { 1: '服务质量', 2: '时效问题', 3: '费用争议', 4: '建议', 5: '其他' }

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

// 趋势折线图
const trendChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: { type: 'category', data: trendData.value.months, boundaryGap: false },
  yAxis: { type: 'value', name: '反馈量' },
  series: [{
    type: 'line',
    data: trendData.value.values,
    smooth: true,
    areaStyle: { opacity: 0.15 },
    itemStyle: { color: '#3A6FB0' },
  }],
}))

// 组织对比柱状图
const orgChartOption = computed(() => ({
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: { type: 'category', data: orgData.value.names, axisLabel: { rotate: 30 } },
  yAxis: { type: 'value', name: '反馈数' },
  series: [{
    type: 'bar',
    data: orgData.value.values,
    itemStyle: { color: '#3A6FB0', borderRadius: [4, 4, 0, 0] },
    barMaxWidth: 40,
  }],
}))

// 高频问题表格列
const freqColumns = [
  { title: '分类', dataIndex: 'category', key: 'category', width: 150 },
  { title: '数量', dataIndex: 'count', key: 'count', width: 100, align: 'center' as const },
  { title: '落实率', dataIndex: 'implementRate', key: 'implementRate', width: 100, align: 'center' as const },
]

// 加载数据
async function loadData() {
  try {
    const res = await getServiceFeedbackList({ pageIndex: 1, pageSize: 9999 }) as any
    const items: ServiceFeedbackListItemDto[] = res?.items || res || []

    summary.total = items.length

    // 按分类聚合
    const catMap: Record<number, { count: number; implemented: number }> = {}
    items.forEach(item => {
      if (!catMap[item.category]) catMap[item.category] = { count: 0, implemented: 0 }
      catMap[item.category].count++
      if (item.status === 3) catMap[item.category].implemented++
    })

    categoryData.value = Object.entries(catMap).map(([k, v]) => ({
      name: catLabels[Number(k)] || '未知',
      value: v.count,
    }))

    // 落实率
    const implemented = items.filter(i => i.status === 3).length
    summary.implementRate = items.length > 0 ? (implemented / items.length * 100) : 0

    // 高频分类
    let topCat = '-'
    let topCount = 0
    Object.entries(catMap).forEach(([k, v]) => {
      if (v.count > topCount) {
        topCount = v.count
        topCat = catLabels[Number(k)] || '未知'
      }
    })
    summary.topCategory = topCat

    // 高频问题表格
    freqTableData.value = Object.entries(catMap)
      .map(([k, v]) => ({
        category: catLabels[Number(k)] || '未知',
        count: v.count,
        implementRate: v.count > 0 ? `${(v.implemented / v.count * 100).toFixed(1)}%` : '0%',
      }))
      .sort((a, b) => b.count - a.count)

    // 模拟月度趋势
    const months = ['1月', '2月', '3月', '4月', '5月', '6月']
    const values = months.map(() => Math.floor(Math.random() * 30 + 5))
    trendData.value = { months, values }

    // 模拟组织对比
    orgData.value = {
      names: ['城区', '新城', '浏河', '沙溪', '陆渡', '南郊'],
      values: [28, 22, 15, 18, 12, 9],
    }

    // 模拟平均处理周期
    summary.avgDays = Math.round(Math.random() * 5 + 2)
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
