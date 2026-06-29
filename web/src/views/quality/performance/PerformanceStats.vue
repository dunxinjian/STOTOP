<template>
  <div class="page-container performance-stats">
    <PageHeader title="绩效统计" />

    <!-- 周期选择器 -->
    <div class="period-selector" style="margin-bottom: 16px">
      <a-space>
        <span>周期：</span>
        <a-input
          v-model:value="period"
          placeholder="如: 2026-Q1"
          allow-clear
          style="width: 180px"
          @pressEnter="fetchAllData"
        />
        <a-button type="primary" @click="fetchAllData">
          <template #icon><SearchOutlined /></template>查询
        </a-button>
      </a-space>
    </div>

    <a-spin :spinning="loading">
      <!-- 统计卡片区 -->
      <a-row :gutter="16" class="stat-row">
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="平均分" :value="stats.avgScore" :precision="1" :value-style="{ color: 'var(--color-info)' }" />
          </a-card>
        </a-col>
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="最高分" :value="stats.maxScore" :precision="1" :value-style="{ color: 'var(--color-success)' }" />
          </a-card>
        </a-col>
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="总处理数" :value="stats.totalHandled" :value-style="{ color: 'var(--color-info)' }" />
          </a-card>
        </a-col>
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic
              title="超时率"
              :value="stats.overdueRate"
              suffix="%"
              :precision="1"
              :value-style="{ color: stats.overdueRate > 20 ? 'var(--color-danger)' : 'var(--color-success)' }"
            />
          </a-card>
        </a-col>
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="我的排名" :value-style="{ color: 'var(--color-warning)' }">
              <template #default>
                <span v-if="stats.myRank">第{{ stats.myRank }}名</span>
                <span v-else>-</span>
              </template>
            </a-statistic>
          </a-card>
        </a-col>
        <a-col :span="4">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="参与人数" :value="stats.totalUsers" suffix="人" :value-style="{ color: 'var(--color-info)' }" />
          </a-card>
        </a-col>
      </a-row>

      <!-- 图表区 -->
      <a-row :gutter="16" class="stat-row">
        <a-col :span="24">
          <a-card title="个人绩效趋势" :bordered="false">
            <v-chart v-if="trendData.length" :option="trendOption" style="height: 360px" autoresize />
            <EmptyState v-else title="暂无趋势数据" />
          </a-card>
        </a-col>
      </a-row>

      <!-- 排名表格区 -->
      <a-card title="绩效排名" :bordered="false">
        <a-table
          :columns="rankColumns"
          :data-source="rankingData"
          :pagination="rankPagination"
          row-key="userId"
          size="small"
          @change="handleRankTableChange"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.dataIndex === 'rank'">
              <a-tag v-if="getRankIndex(index) <= 3" :color="rankColor(getRankIndex(index))">
                {{ getRankIndex(index) }}
              </a-tag>
              <span v-else>{{ getRankIndex(index) }}</span>
            </template>
            <template v-if="column.dataIndex === 'score'">
              <a-progress
                :percent="record.score"
                :stroke-color="scoreColor(record.score)"
                size="small"
                :format="(p?: number) => `${p ?? 0}`"
                style="max-width: 200px"
              />
            </template>
          </template>
        </a-table>
      </a-card>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, reactive, onMounted } from 'vue'
import { SearchOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import {
  getPerformanceStats,
  getPerformanceRanking,
  getPerformanceTrend,
} from '@/api/quality'

use([CanvasRenderer, LineChart, BarChart, GridComponent, TooltipComponent, LegendComponent])

const loading = ref(false)
const period = ref('')

// 统计数据
const stats = ref({
  avgScore: 0,
  maxScore: 0,
  totalHandled: 0,
  overdueRate: 0,
  myRank: 0,
  totalUsers: 0,
})

// 趋势数据
const trendData = ref<{ period: string; score: number; handleCount: number }[]>([])

// 排名数据
const rankingData = ref<any[]>([])
const rankPagination = reactive({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50'],
  showTotal: (total: number) => `共 ${total} 条`,
})

// 趋势图配置
const trendOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['评分', '处理数'] },
  grid: { left: 50, right: 50, top: 40, bottom: 30 },
  xAxis: {
    type: 'category',
    data: trendData.value.map((d) => d.period),
  },
  yAxis: [
    { type: 'value', name: '评分', min: 0, max: 100 },
    { type: 'value', name: '处理数', minInterval: 1 },
  ],
  series: [
    {
      name: '评分',
      type: 'line',
      data: trendData.value.map((d) => d.score),
      smooth: true,
      itemStyle: { color: '#3A6FB0' },
      areaStyle: { color: 'rgba(58,111,176,0.1)' },
    },
    {
      name: '处理数',
      type: 'bar',
      yAxisIndex: 1,
      data: trendData.value.map((d) => d.handleCount),
      itemStyle: { color: 'rgba(82,196,26,0.6)' },
      barMaxWidth: 30,
    },
  ],
}))

// 排名表格列
const rankColumns = [
  { title: '排名', dataIndex: 'rank', width: 80, align: 'center' as const },
  { title: '用户名', dataIndex: 'userName', width: 140 },
  { title: '评分', dataIndex: 'score', width: 240 },
  { title: '异常处理数', dataIndex: 'exceptionCount', width: 110, align: 'center' as const },
  { title: '已解决数', dataIndex: 'resolvedCount', width: 100, align: 'center' as const },
  { title: '超时数', dataIndex: 'overdueCount', width: 90, align: 'center' as const },
]

function scoreColor(score: number): string {
  if (score >= 90) return 'var(--color-success)'
  if (score >= 70) return 'var(--color-info)'
  if (score >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

function rankColor(rank: number): string {
  if (rank === 1) return 'gold'
  if (rank === 2) return 'blue'
  return 'green'
}

function getRankIndex(index: number): number {
  return (rankPagination.current - 1) * rankPagination.pageSize + index + 1
}

function handleRankTableChange(pag: any) {
  rankPagination.current = pag.current
  rankPagination.pageSize = pag.pageSize
  fetchRanking()
}

async function fetchAllData() {
  loading.value = true
  try {
    const params: any = {}
    if (period.value) params.period = period.value
    await Promise.all([fetchStats(params), fetchRanking(), fetchTrend(params)])
  } finally {
    loading.value = false
  }
}

async function fetchStats(params?: any) {
  try {
    const p = params ?? (period.value ? { period: period.value } : {})
    const res = await getPerformanceStats(p)
    if (res) {
      const d = res
      stats.value = {
        avgScore: d.avgScore ?? 0,
        maxScore: d.maxScore ?? 0,
        totalHandled: d.totalHandled ?? 0,
        overdueRate: d.overdueRate ?? 0,
        myRank: d.myRank ?? 0,
        totalUsers: d.totalUsers ?? 0,
      }
    }
  } catch {
    // ignore
  }
}

async function fetchRanking() {
  try {
    const params: any = {
      pageIndex: rankPagination.current,
      pageSize: rankPagination.pageSize,
    }
    if (period.value) params.period = period.value
    const res = await getPerformanceRanking(params)
    if (res) {
      if (Array.isArray(res)) {
        rankingData.value = res
        rankPagination.total = res.length
      } else {
        rankingData.value = res.items ?? res.list ?? []
        rankPagination.total = res.totalCount ?? res.total ?? 0
      }
    }
  } catch {
    // ignore
  }
}

async function fetchTrend(params?: any) {
  try {
    const p = params ?? (period.value ? { period: period.value } : {})
    const res = await getPerformanceTrend(p)
    trendData.value = (res ?? []).map((d: any) => ({
      period: d.period ?? '',
      score: d.score ?? 0,
      handleCount: d.handleCount ?? 0,
    }))
  } catch {
    // ignore
  }
}

onMounted(() => {
  fetchAllData()
})
</script>

<style scoped lang="scss">
.performance-stats {
  .stat-row {
    margin-bottom: 16px;
  }

  .stat-card {
    border-radius: 8px;
    box-shadow: 0 1px 4px rgba(0, 21, 41, 0.06);
    transition: box-shadow 0.3s ease;

    &:hover {
      box-shadow: 0 4px 12px rgba(0, 21, 41, 0.1);
    }
  }

  .period-selector {
    display: flex;
    align-items: center;
  }
}
</style>
