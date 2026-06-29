<template>
  <div class="page-report-profit">
    <van-nav-bar title="毛利分析" left-arrow @click-left="$router.back()" />

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <!-- 接口不可用时明确告知，决不能静默显示空图表 -->
      <van-empty
        v-if="!loading && loadFailed"
        image="error"
        description="移动端毛利分析服务尚未上线，请使用电脑端「财务 → 阿米巴经营报表」查看"
      />
      <template v-else-if="!loading">
        <!-- Top10 柱状图 -->
        <div class="section-card">
          <div class="section-title">客户毛利 Top10</div>
          <MiniChart :option="barOption" height="300px" @chart-click="onBarClick" />
        </div>

        <!-- 展开明细 -->
        <van-collapse v-model="activeCustomer" v-if="selectedDetail">
          <van-collapse-item :name="selectedDetail.name" :title="selectedDetail.name">
            <div class="detail-info">
              <div class="detail-row">
                <span>收入</span>
                <span>¥{{ formatNum(selectedDetail.revenue) }}</span>
              </div>
              <div class="detail-row">
                <span>成本</span>
                <span>¥{{ formatNum(selectedDetail.cost) }}</span>
              </div>
              <div class="detail-row highlight">
                <span>毛利</span>
                <span>¥{{ formatNum(selectedDetail.profit) }}</span>
              </div>
              <div class="detail-row">
                <span>毛利率</span>
                <span>{{ formatRate(selectedDetail.profitRate) }}</span>
              </div>
            </div>
          </van-collapse-item>
        </van-collapse>
      </template>

      <van-loading v-else class="page-loading" />
    </van-pull-refresh>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  NavBar as VanNavBar,
  PullRefresh as VanPullRefresh,
  Collapse as VanCollapse,
  CollapseItem as VanCollapseItem,
  Loading as VanLoading,
  Empty as VanEmpty,
} from 'vant'
import type { EChartsOption } from 'echarts'
import MiniChart from '../components/MiniChart.vue'
import { get } from '@/api/request'

defineOptions({ name: 'MobileReportProfit' })

interface ProfitItem {
  name: string
  revenue: number
  cost: number
  profit: number
  // 应收≤0 时后端返回 null（见 ExpReportDtos.cs / CalcProfitRate），与后端契约一致
  profitRate: number | null
}

const loading = ref(false)
const refreshing = ref(false)
const loadFailed = ref(false)
const profitData = ref<ProfitItem[]>([])
const activeCustomer = ref<string[]>([])
const selectedDetail = ref<ProfitItem | null>(null)

function formatNum(v: number): string {
  if (Math.abs(v) >= 10000) return (v / 10000).toFixed(2) + '万'
  return v.toLocaleString()
}

/** 毛利率显示：应收≤0 时后端返回 null，显示 — 而非误导性的 0.0%（与桌面端 ProfitAnalysis 一致） */
function formatRate(rate: number | null | undefined): string {
  return rate == null ? '—' : `${rate.toFixed(1)}%`
}

const barOption = computed<EChartsOption>(() => {
  const sorted = [...profitData.value].sort((a, b) => a.profit - b.profit).slice(-10)
  return {
    tooltip: {
      trigger: 'axis',
      triggerOn: 'click',
      axisPointer: { type: 'shadow' },
    },
    grid: { left: 80, right: 20, top: 10, bottom: 20 },
    xAxis: {
      type: 'value',
      axisLabel: {
        fontSize: 10,
        formatter: (v: number) => v >= 10000 ? (v / 10000).toFixed(0) + 'w' : String(v),
      },
    },
    yAxis: {
      type: 'category',
      data: sorted.map(d => d.name),
      axisLabel: { fontSize: 11 },
    },
    series: [{
      type: 'bar',
      data: sorted.map(d => ({
        value: d.profit,
        itemStyle: { color: d.profit >= 0 ? '#2BA471' : '#E5484D' },
      })),
      barWidth: 16,
      label: {
        show: true,
        position: 'right',
        fontSize: 10,
        formatter: (p: any) => '¥' + formatNum(p.value),
      },
    }],
  }
})

function onBarClick(params: any) {
  const item = profitData.value.find(d => d.name === params.name)
  if (item) {
    selectedDetail.value = item
    activeCustomer.value = [item.name]
  }
}

async function loadData() {
  loading.value = true
  loadFailed.value = false
  try {
    const res = await get<any>('/amoeba/pl/profit-ranking', {
      top: 10,
      period: getCurrentPeriod(),
    }, { silent: true } as any)
    if (Array.isArray(res)) {
      profitData.value = res
    } else if (res?.items) {
      profitData.value = res.items
    }
  } catch (e) {
    // 后端尚无 /amoeba/pl/profit-ranking 接口（移动端报表 API 待建设），显式进入不可用状态
    console.error('[ReportProfit] 加载失败:', e)
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

function getCurrentPeriod() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

async function onRefresh() {
  await loadData()
  refreshing.value = false
}

onMounted(() => loadData())
</script>

<style scoped>
.page-report-profit {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 20px;
}
.section-card {
  background: #fff;
  border-radius: 10px;
  margin: 12px 16px;
  padding: 14px;
}
.section-title {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 10px;
}
.detail-info {
  padding: 4px 0;
}
.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 14px;
  color: #595959;
  border-bottom: 1px solid #f5f5f5;
}
.detail-row.highlight {
  font-weight: 600;
  color: #1a1a1a;
}
.page-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
