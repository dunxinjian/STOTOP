<template>
  <div class="page-container">
    <PageHeader title="推荐统计" description="推荐数据统计与分析">
      <template #actions>
        <a-button @click="handleExport">
          <template #icon><DownloadOutlined /></template>导出 Excel
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-range-picker v-model:value="filterDateRange" size="small" style="width: 240px" @change="handleFilter" />
          <a-select v-model:value="filterOrgId" placeholder="选择组织" allow-clear size="small" style="width: 160px" @change="handleFilter" />
        </div>
      </template>
    </PageHeader>

    <!-- 统计卡片 -->
    <a-row :gutter="16" class="stat-cards">
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="推荐总数" :value="stats.totalReferrals" suffix="个" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="达成率" :value="stats.conversionRate" suffix="%" :precision="1" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="返佣总额" :value="stats.totalCommission" prefix="¥" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="已付款额" :value="stats.paidAmount" prefix="¥" :precision="2" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 图表区 -->
    <a-row :gutter="16" class="chart-section">
      <a-col :span="12">
        <a-card title="推荐人 Top 10" :bordered="false">
          <v-chart :option="top10ChartOption" autoresize style="height: 360px" />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="月度推荐趋势" :bordered="false">
          <v-chart :option="trendChartOption" autoresize style="height: 360px" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 明细表格 -->
    <a-card title="推荐明细" :bordered="false" class="detail-section">
      <a-table
        :columns="detailColumns"
        :data-source="detailData"
        :loading="loading"
        :pagination="false"
        row-key="referrerId"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'totalCommission'">
            ¥{{ record.totalCommission?.toFixed(2) }}
          </template>
        </template>
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
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart, LineChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import { getReferralStatistics, type ReferralStatisticsDto } from '@/api/crm'

use([CanvasRenderer, BarChart, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

// 筛选
const filterDateRange = ref(null as any)
const filterOrgId = ref<number | undefined>(undefined)
const loading = ref(false)

// 统计数据
const stats = reactive({
  totalReferrals: 42,
  conversionRate: 68.5,
  totalCommission: 156800,
  paidAmount: 98500,
})

// 明细数据（模拟）
const detailData = ref<ReferralStatisticsDto[]>([
  { referrerId: 1, referrerName: '张三', referralCount: 12, totalCommission: 45600, paidCommission: 32000 },
  { referrerId: 2, referrerName: '李四', referralCount: 8, totalCommission: 32000, paidCommission: 20000 },
  { referrerId: 3, referrerName: '王五', referralCount: 6, totalCommission: 28000, paidCommission: 18000 },
  { referrerId: 4, referrerName: '赵六', referralCount: 5, totalCommission: 21200, paidCommission: 12500 },
  { referrerId: 5, referrerName: '钱七', referralCount: 4, totalCommission: 15000, paidCommission: 8000 },
  { referrerId: 6, referrerName: '孙八', referralCount: 3, totalCommission: 8000, paidCommission: 5000 },
  { referrerId: 7, referrerName: '周九', referralCount: 2, totalCommission: 4000, paidCommission: 2000 },
  { referrerId: 8, referrerName: '吴十', referralCount: 2, totalCommission: 3000, paidCommission: 1000 },
])

const detailColumns = [
  { title: '推荐人', dataIndex: 'referrerName', width: 120 },
  { title: '推荐数', dataIndex: 'referralCount', width: 100, align: 'center' as const },
  { title: '达成数', dataIndex: 'paidCommission', width: 100, align: 'center' as const },
  { title: '返佣金额', dataIndex: 'totalCommission', width: 140, align: 'right' as const },
]

// Top 10 柱状图
const top10ChartOption = computed(() => {
  const names = detailData.value.map(d => d.referrerName || '')
  const counts = detailData.value.map(d => d.referralCount)
  return {
    tooltip: { trigger: 'axis' },
    grid: { left: 80, right: 20, top: 20, bottom: 30 },
    xAxis: { type: 'value' },
    yAxis: { type: 'category', data: names.reverse(), axisLabel: { width: 60, overflow: 'truncate' } },
    series: [{ type: 'bar', data: counts.reverse(), itemStyle: { color: '#3A6FB0' }, barMaxWidth: 30 }],
  }
})

// 月度趋势折线图（模拟）
const trendChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  grid: { left: 50, right: 20, top: 20, bottom: 30 },
  xAxis: { type: 'category', data: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'] },
  yAxis: { type: 'value' },
  series: [{ type: 'line', data: [3, 5, 4, 6, 5, 7, 4, 3, 5, 6, 4, 3], smooth: true, areaStyle: { opacity: 0.15 }, itemStyle: { color: '#3A6FB0' } }],
}))

async function handleFilter() {
  loading.value = true
  try {
    const params: any = {}
    if (filterOrgId.value) params.orgId = filterOrgId.value
    if (filterDateRange.value?.[0]) params.startDate = filterDateRange.value[0].format('YYYY-MM-DD')
    if (filterDateRange.value?.[1]) params.endDate = filterDateRange.value[1].format('YYYY-MM-DD')
    const res = await getReferralStatistics(params.orgId, params.startDate, params.endDate) as any
    if (Array.isArray(res)) detailData.value = res
  } catch { /* use mock data */ }
  finally { loading.value = false }
}

function handleExport() {
  message.info('导出功能开发中')
}

onMounted(() => {
  handleFilter()
})
</script>

<style scoped lang="scss">
.stat-cards {
  margin-top: 16px;
}

.chart-section {
  margin-top: 16px;
}

.detail-section {
  margin-top: 16px;
}
</style>
