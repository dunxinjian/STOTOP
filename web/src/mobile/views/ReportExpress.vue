<template>
  <div class="page-report-express">
    <van-nav-bar title="快递业务统计" left-arrow @click-left="$router.back()" />

    <!-- 核心指标 -->
    <div class="kpi-strip">
      <div class="kpi-item">
        <div class="kpi-val">{{ stats.avgDailyVolume }}</div>
        <div class="kpi-lbl">日均票量</div>
      </div>
      <div class="kpi-item">
        <div class="kpi-val">{{ stats.avgWeight }}kg</div>
        <div class="kpi-lbl">平均单票重量</div>
      </div>
      <div class="kpi-item">
        <div class="kpi-val">¥{{ stats.avgPrice }}</div>
        <div class="kpi-lbl">平均单价</div>
      </div>
    </div>

    <!-- 品牌分组 -->
    <van-tabs v-model:active="activeBrand" shrink sticky offset-top="46">
      <van-tab v-for="brand in brands" :key="brand.code" :title="brand.name" :name="brand.code" />
    </van-tabs>

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <!-- 品牌 KPI -->
      <div class="brand-kpi" v-if="brandData">
        <div class="brand-kpi-row">
          <span>票量</span>
          <span class="brand-val">{{ brandData.volume?.toLocaleString() ?? '-' }}</span>
        </div>
        <div class="brand-kpi-row">
          <span>收入</span>
          <span class="brand-val">¥{{ formatNum(brandData.revenue ?? 0) }}</span>
        </div>
        <div class="brand-kpi-row">
          <span>成本</span>
          <span class="brand-val">¥{{ formatNum(brandData.cost ?? 0) }}</span>
        </div>
      </div>

      <!-- 票量趋势图 -->
      <div class="section-card">
        <div class="section-title">近30天票量趋势</div>
        <MiniChart :option="lineOption" height="200px" />
      </div>

      <van-loading v-if="loading" class="page-loading" />
    </van-pull-refresh>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  NavBar as VanNavBar,
  Tabs as VanTabs,
  Tab as VanTab,
  PullRefresh as VanPullRefresh,
  Loading as VanLoading,
} from 'vant'
import type { EChartsOption } from 'echarts'
import MiniChart from '../components/MiniChart.vue'
import { get } from '@/api/request'

defineOptions({ name: 'MobileReportExpress' })

interface BrandData {
  volume: number
  revenue: number
  cost: number
  trend: { date: string; value: number }[]
}

const brands = ref([
  { code: 'STO', name: '申通' },
  { code: 'YD', name: '韵达' },
  { code: 'JT', name: '极兔' },
])
const activeBrand = ref('STO')
const loading = ref(false)
const refreshing = ref(false)

const stats = ref({
  avgDailyVolume: 0,
  avgWeight: '0.0',
  avgPrice: '0.0',
})

const brandData = ref<BrandData | null>(null)

function formatNum(v: number): string {
  if (Math.abs(v) >= 10000) return (v / 10000).toFixed(2) + '万'
  return v.toLocaleString()
}

const lineOption = computed<EChartsOption>(() => {
  const trend = brandData.value?.trend ?? []
  return {
    tooltip: {
      trigger: 'axis',
      triggerOn: 'click',
    },
    grid: { left: 50, right: 16, top: 16, bottom: 30 },
    xAxis: {
      type: 'category',
      data: trend.map(p => p.date.slice(5)),
      axisLabel: { fontSize: 10, interval: 6 },
      axisTick: { show: false },
    },
    yAxis: {
      type: 'value',
      axisLabel: { fontSize: 10 },
      splitLine: { lineStyle: { type: 'dashed' } },
    },
    series: [{
      type: 'line',
      data: trend.map(p => p.value),
      smooth: true,
      symbol: 'none',
      lineStyle: { width: 2, color: '#6B4FB0' },
      areaStyle: {
        color: {
          type: 'linear',
          x: 0, y: 0, x2: 0, y2: 1,
          colorStops: [
            { offset: 0, color: 'rgba(107,79,176,0.2)' },
            { offset: 1, color: 'rgba(107,79,176,0.02)' },
          ],
        },
      },
    }],
  }
})

async function loadData() {
  loading.value = true
  try {
    const res = await get<any>('/express/statistics', {
      brand: activeBrand.value,
      days: 30,
    })
    if (res) {
      stats.value = {
        avgDailyVolume: res.avgDailyVolume ?? 0,
        avgWeight: (res.avgWeight ?? 0).toFixed(1),
        avgPrice: (res.avgPrice ?? 0).toFixed(2),
      }
      brandData.value = {
        volume: res.volume ?? 0,
        revenue: res.revenue ?? 0,
        cost: res.cost ?? 0,
        trend: res.trend ?? [],
      }
    }
  } catch (e) {
    console.error('[ReportExpress] 加载失败:', e)
  } finally {
    loading.value = false
  }
}

async function onRefresh() {
  await loadData()
  refreshing.value = false
}

watch(activeBrand, () => loadData())
onMounted(() => loadData())
</script>

<style scoped>
.page-report-express {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 20px;
}
.kpi-strip {
  display: flex;
  background: #fff;
  padding: 16px;
  justify-content: space-around;
  border-bottom: 1px solid #f0f0f0;
}
.kpi-item {
  text-align: center;
}
.kpi-val {
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
}
.kpi-lbl {
  font-size: 11px;
  color: #8c8c8c;
  margin-top: 4px;
}
.brand-kpi {
  background: #fff;
  border-radius: 10px;
  margin: 12px 16px;
  padding: 12px 16px;
}
.brand-kpi-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 14px;
  color: #595959;
  border-bottom: 1px solid #f5f5f5;
}
.brand-val {
  font-weight: 500;
  color: #1a1a1a;
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
.page-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
