<template>
  <div class="page-report-cost">
    <van-nav-bar title="成本明细" left-arrow @click-left="$router.back()" />

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <!-- 环形图 -->
      <div class="section-card">
        <div class="section-title">成本结构</div>
        <MiniChart :option="pieOption" height="260px" @chart-click="onPieClick" />
        <div class="total-label">
          合计: ¥{{ formatNum(totalCost) }}
        </div>
      </div>

      <!-- 科目明细列表 -->
      <div class="detail-list" v-if="selectedSubject">
        <div class="list-header">
          <span>{{ selectedSubject.name }} 明细</span>
          <span class="close-btn" @click="selectedSubject = null">收起</span>
        </div>
        <div class="detail-row" v-for="(item, idx) in selectedSubject.items" :key="idx">
          <span class="item-name">{{ item.name }}</span>
          <span class="item-value">¥{{ formatNum(item.value) }}</span>
        </div>
        <div v-if="!selectedSubject.items?.length" class="empty-hint">暂无明细</div>
      </div>

      <!-- 科目列表 -->
      <div class="subject-list">
        <div
          class="subject-row"
          v-for="item in costItems"
          :key="item.name"
          @click="selectSubject(item)"
        >
          <div class="subject-color" :style="{ background: item.color }"></div>
          <span class="subject-name">{{ item.name }}</span>
          <span class="subject-value">¥{{ formatNum(item.value) }}</span>
          <span class="subject-percent">{{ item.percent.toFixed(1) }}%</span>
        </div>
      </div>

      <van-loading v-if="loading" class="page-loading" />
    </van-pull-refresh>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  NavBar as VanNavBar,
  PullRefresh as VanPullRefresh,
  Loading as VanLoading,
} from 'vant'
import type { EChartsOption } from 'echarts'
import MiniChart from '../components/MiniChart.vue'
import { get } from '@/api/request'

defineOptions({ name: 'MobileReportCost' })

// 分类色板：淡雅协调多色（echarts 渲染到 canvas 无法解析 CSS 变量，故用固定 hex）
const COLORS = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A']

interface CostItem {
  name: string
  value: number
  percent: number
  color: string
  items?: { name: string; value: number }[]
}

const loading = ref(false)
const refreshing = ref(false)
const costItems = ref<CostItem[]>([])
const selectedSubject = ref<CostItem | null>(null)

const totalCost = computed(() => costItems.value.reduce((s, i) => s + i.value, 0))

function formatNum(v: number): string {
  if (Math.abs(v) >= 10000) return (v / 10000).toFixed(2) + '万'
  return v.toLocaleString()
}

const pieOption = computed<EChartsOption>(() => ({
  tooltip: {
    trigger: 'item',
    triggerOn: 'click',
    formatter: (p: any) => `${p.name}: ¥${formatNum(p.value)} (${p.percent?.toFixed(1)}%)`,
  },
  series: [{
    type: 'pie',
    radius: ['45%', '70%'],
    center: ['50%', '50%'],
    avoidLabelOverlap: true,
    label: {
      show: true,
      fontSize: 11,
      formatter: '{b}\n{d}%',
    },
    data: costItems.value.map(item => ({
      name: item.name,
      value: item.value,
      itemStyle: { color: item.color },
    })),
  }],
}))

function onPieClick(params: any) {
  const item = costItems.value.find(d => d.name === params.name)
  if (item) selectSubject(item)
}

async function selectSubject(item: CostItem) {
  selectedSubject.value = item
  if (!item.items) {
    try {
      const res = await get<any>('/amoeba/pl/cost-detail', {
        subject: item.name,
        period: getCurrentPeriod(),
      })
      item.items = res?.items ?? []
    } catch (e) {
      item.items = []
    }
  }
}

async function loadData() {
  loading.value = true
  try {
    const res = await get<any>('/amoeba/pl/cost-structure', {
      period: getCurrentPeriod(),
    })
    const items: any[] = res?.items ?? res ?? []
    const total = items.reduce((s: number, i: any) => s + (i.value ?? 0), 0)
    costItems.value = items.map((item: any, idx: number) => ({
      name: item.name,
      value: item.value ?? 0,
      percent: total > 0 ? ((item.value ?? 0) / total * 100) : 0,
      color: COLORS[idx % COLORS.length],
    }))
  } catch (e) {
    console.error('[ReportCost] 加载失败:', e)
  } finally {
    loading.value = false
  }
}

function getCurrentPeriod() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

async function onRefresh() {
  selectedSubject.value = null
  await loadData()
  refreshing.value = false
}

onMounted(() => loadData())
</script>

<style scoped>
.page-report-cost {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 20px;
}
.section-card {
  background: #fff;
  border-radius: 10px;
  margin: 12px 16px;
  padding: 14px;
  position: relative;
}
.section-title {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 8px;
}
.total-label {
  text-align: center;
  font-size: 13px;
  color: #8c8c8c;
  margin-top: 4px;
}
.subject-list {
  background: #fff;
  border-radius: 10px;
  margin: 0 16px 12px;
  padding: 8px 16px;
}
.subject-row {
  display: flex;
  align-items: center;
  padding: 10px 0;
  border-bottom: 1px solid #f5f5f5;
  font-size: 14px;
}
.subject-color {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  margin-right: 8px;
}
.subject-name {
  flex: 1;
  color: #1a1a1a;
}
.subject-value {
  color: #595959;
  margin-right: 8px;
}
.subject-percent {
  color: #8c8c8c;
  font-size: 12px;
  width: 45px;
  text-align: right;
}
.detail-list {
  background: #fff;
  border-radius: 10px;
  margin: 0 16px 12px;
  padding: 12px 16px;
}
.list-header {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 8px;
}
.close-btn {
  color: var(--text-1);
  font-weight: 400;
  font-size: 13px;
}
.close-btn:hover {
  color: var(--color-primary);
}
.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 6px 0;
  font-size: 13px;
  color: #595959;
}
.empty-hint {
  text-align: center;
  color: #bfbfbf;
  padding: 16px 0;
  font-size: 13px;
}
.page-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
