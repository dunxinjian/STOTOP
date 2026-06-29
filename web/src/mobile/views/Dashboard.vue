<template>
  <div class="page-dashboard">
    <van-nav-bar title="经营看板">
      <template #right>
        <span class="period-btn" @click="showPeriodPicker = true">
          {{ currentPeriod }} ▾
        </span>
      </template>
    </van-nav-bar>

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <!-- KPI 区 -->
      <div class="kpi-grid">
        <KpiCard label="票量" :value="kpi.volume.value" :change="kpi.volume.change" suffix="票" />
        <KpiCard label="收入" :value="kpi.revenue.value" :change="kpi.revenue.change" prefix="¥" />
        <KpiCard label="成本" :value="kpi.cost.value" :change="kpi.cost.change" prefix="¥" />
        <KpiCard label="利润" :value="kpi.profit.value" :change="kpi.profit.change" prefix="¥" />
      </div>

      <!-- 趋势图 -->
      <div class="section-card">
        <div class="section-title">近30天收入趋势</div>
        <MiniChart :option="trendOption" height="220px" />
      </div>

      <!-- 快捷入口 -->
      <div class="shortcut-grid">
        <div class="shortcut-item" @click="$router.push('/m/report/amoeba')">
          <van-icon name="bar-chart-o" size="24" color="var(--color-info)" />
          <span>阿米巴损益</span>
        </div>
        <div class="shortcut-item" @click="$router.push('/m/report/profit')">
          <van-icon name="gold-coin-o" size="24" color="var(--color-success)" />
          <span>毛利分析</span>
        </div>
        <div class="shortcut-item" @click="$router.push('/m/report/cost')">
          <van-icon name="balance-list-o" size="24" color="var(--color-warning)" />
          <span>成本明细</span>
        </div>
        <div class="shortcut-item" @click="$router.push('/m/report/express')">
          <van-icon name="logistics" size="24" color="var(--biz-waybill)" />
          <span>快递统计</span>
        </div>
      </div>

      <!-- 缓存时间 -->
      <div class="cache-hint" v-if="kpi.cachedAt">
        数据更新于 {{ formatTime(kpi.cachedAt) }}
      </div>
    </van-pull-refresh>

    <!-- 期间选择器 -->
    <van-popup v-model:show="showPeriodPicker" position="bottom" round>
      <van-date-picker
        v-model="pickerValue"
        title="选择期间"
        :columns-type="['year', 'month']"
        :min-date="new Date(2024, 0, 1)"
        :max-date="new Date()"
        @confirm="onPeriodConfirm"
        @cancel="showPeriodPicker = false"
      />
    </van-popup>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  NavBar as VanNavBar,
  PullRefresh as VanPullRefresh,
  Icon as VanIcon,
  Popup as VanPopup,
  DatePicker as VanDatePicker,
} from 'vant'
import type { EChartsOption } from 'echarts'
import KpiCard from '../components/KpiCard.vue'
import MiniChart from '../components/MiniChart.vue'
import { getDashboardKpi, getDashboardTrend } from '@/shared/api/mobile'
import type { KpiData, TrendPoint } from '@/shared/types'

defineOptions({ name: 'MobileDashboard' })

const CACHE_KEY = 'mobile_dashboard_cache'
const CACHE_TTL = 5 * 60 * 1000 // 5 minutes

const now = new Date()
const currentPeriod = ref(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
const pickerValue = ref([String(now.getFullYear()), String(now.getMonth() + 1).padStart(2, '0')])
const showPeriodPicker = ref(false)
const refreshing = ref(false)
const orgId = ref(0)

const kpi = ref<KpiData>({
  volume: { value: 0, change: 0 },
  revenue: { value: 0, change: 0 },
  cost: { value: 0, change: 0 },
  profit: { value: 0, change: 0 },
  cachedAt: '',
})

const trendPoints = ref<TrendPoint[]>([])

const trendOption = computed<EChartsOption>(() => ({
  tooltip: {
    trigger: 'axis',
    triggerOn: 'click',
    formatter: (params: any) => {
      const p = Array.isArray(params) ? params[0] : params
      return `${p.axisValue}<br/>收入: ¥${p.value?.toLocaleString()}`
    },
  },
  grid: { left: 50, right: 16, top: 20, bottom: 30 },
  xAxis: {
    type: 'category',
    data: trendPoints.value.map(p => p.date.slice(5)),
    axisLabel: { fontSize: 10, interval: 6 },
    axisTick: { show: false },
  },
  yAxis: {
    type: 'value',
    axisLabel: {
      fontSize: 10,
      formatter: (v: number) => v >= 10000 ? (v / 10000).toFixed(0) + 'w' : String(v),
    },
    splitLine: { lineStyle: { type: 'dashed' } },
  },
  series: [{
    type: 'line',
    data: trendPoints.value.map(p => p.value),
    smooth: true,
    symbol: 'none',
    lineStyle: { width: 2.5, color: '#3A6FB0' },
    areaStyle: {
      color: {
        type: 'linear',
        x: 0, y: 0, x2: 0, y2: 1,
        colorStops: [
          { offset: 0, color: 'rgba(58,111,176,0.25)' },
          { offset: 1, color: 'rgba(58,111,176,0.02)' },
        ],
      },
    },
  }],
}))

function getCached(): { kpi: KpiData; trend: TrendPoint[]; period: string; ts: number } | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    if (!raw) return null
    const data = JSON.parse(raw)
    if (Date.now() - data.ts > CACHE_TTL) return null
    if (data.period !== currentPeriod.value) return null
    return data
  } catch { return null }
}

function setCache(kpiData: KpiData, trend: TrendPoint[]) {
  localStorage.setItem(CACHE_KEY, JSON.stringify({
    kpi: kpiData,
    trend,
    period: currentPeriod.value,
    ts: Date.now(),
  }))
}

async function loadData(force = false) {
  if (!force) {
    const cached = getCached()
    if (cached) {
      kpi.value = cached.kpi
      trendPoints.value = cached.trend
      return
    }
  }
  try {
    const [kpiRes, trendRes] = await Promise.all([
      getDashboardKpi(currentPeriod.value, orgId.value),
      getDashboardTrend(30, 'revenue', orgId.value),
    ])
    kpi.value = kpiRes as any
    trendPoints.value = (trendRes as any).points ?? []
    setCache(kpi.value, trendPoints.value)
  } catch (e) {
    console.error('[Dashboard] 加载失败:', e)
  }
}

async function onRefresh() {
  await loadData(true)
  refreshing.value = false
}

function onPeriodConfirm({ selectedValues }: any) {
  currentPeriod.value = `${selectedValues[0]}-${selectedValues[1]}`
  showPeriodPicker.value = false
  loadData(true)
}

function formatTime(iso: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`
}

onMounted(() => loadData())
</script>

<style scoped>
.page-dashboard {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 70px;
}
.period-btn {
  font-size: 14px;
  color: var(--color-primary);
}
.kpi-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
  padding: 12px 16px;
}
.section-card {
  background: #fff;
  border-radius: 10px;
  margin: 0 16px 12px;
  padding: 14px;
}
.section-title {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 10px;
}
.shortcut-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  background: #fff;
  border-radius: 10px;
  margin: 0 16px 12px;
  padding: 16px 8px;
}
.shortcut-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #595959;
}
.cache-hint {
  text-align: center;
  font-size: 11px;
  color: #bfbfbf;
  padding: 8px 0 20px;
}
</style>
