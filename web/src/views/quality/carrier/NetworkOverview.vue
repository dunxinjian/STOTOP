<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import * as echarts from 'echarts'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import { useThemeStore } from '@/stores/theme'
import { getNetworkKpi, getNetworkTrend, getDomainDistribution, getFeeByDomain, getNetworkOptions } from '@/api/carrierQuality'

const theme = useThemeStore()
const carrier = ref('申通')
const dateRange = ref<[Dayjs, Dayjs]>([dayjs().subtract(29, 'day').startOf('day'), dayjs()])
const networkCode = ref<string | undefined>(undefined)
const networkOptions = ref<{ value: string; label: string }[]>([])

async function loadNetworkOptions() {
  try {
    const list = (await getNetworkOptions({ carrier: carrier.value })) as any[]
    networkOptions.value = (list || []).map((n: any) => ({ value: n.code, label: n.name ? `${n.name}（${n.code}）` : n.code }))
  } catch { /* 下拉为空不影响主查询 */ }
}

const kpi = ref<any>({})
const loading = ref(false)

function getQueryParams() {
  return {
    carrier: carrier.value,
    from: dateRange.value[0].format('YYYY-MM-DD'),
    to: dateRange.value[1].format('YYYY-MM-DD'),
    networkCode: networkCode.value || undefined,
  }
}

function fmtRate(v: number | null | undefined) {
  return v === null || v === undefined ? '—' : (v * 100).toFixed(1) + '%'
}
function fmtNum(v: number | null | undefined) {
  return v === null || v === undefined ? '—' : Number(v).toLocaleString('zh-CN')
}

const trendRef = ref<HTMLElement>()
const domainRef = ref<HTMLElement>()
const feeRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null
let domainChart: echarts.ECharts | null = null
let feeChart: echarts.ECharts | null = null
let ro: ResizeObserver | null = null

function ensureChart(el: HTMLElement | undefined, cur: echarts.ECharts | null): echarts.ECharts | null {
  if (!el) return cur
  if (cur && cur.getDom() !== el) { cur.dispose(); cur = null }
  if (!cur) {
    cur = echarts.init(el)
    if (!ro) ro = new ResizeObserver(() => { trendChart?.resize(); domainChart?.resize(); feeChart?.resize() })
    ro.observe(el)
  }
  return cur
}

async function fetchAll() {
  loading.value = true
  try {
    const p = getQueryParams()
    const [k, trend, domain, fee] = await Promise.all([
      getNetworkKpi(p), getNetworkTrend(p), getDomainDistribution(p), getFeeByDomain(p),
    ])
    kpi.value = k || {}
    await nextTick()
    renderTrend(trend || [])
    renderDomain(domain || [])
    renderFee(fee || [])
  } catch {
    message.error('获取网点总览失败')
  } finally {
    loading.value = false
  }
}

function renderTrend(data: any[]) {
  trendChart = ensureChart(trendRef.value, trendChart)
  trendChart?.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['当天及时签收率', '出仓及时率', '滞留率', '虚签率'] },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', data: data.map(d => d.date) },
    yAxis: { type: 'value', axisLabel: { formatter: (v: number) => (v * 100).toFixed(0) + '%' } },
    series: [
      { name: '当天及时签收率', type: 'line', smooth: true, itemStyle: { color: theme.themeConfig.colorSuccess }, data: data.map(d => d.signRateToday) },
      { name: '出仓及时率', type: 'line', smooth: true, itemStyle: { color: theme.themeConfig.colorInfo }, data: data.map(d => d.outboundOnTimeRate) },
      { name: '滞留率', type: 'line', smooth: true, itemStyle: { color: theme.themeConfig.colorWarning }, data: data.map(d => d.retentionRate) },
      { name: '虚签率', type: 'line', smooth: true, itemStyle: { color: theme.themeConfig.colorError }, data: data.map(d => d.fakeSignRate) },
    ],
  }, true)
}

function renderDomain(data: any[]) {
  domainChart = ensureChart(domainRef.value, domainChart)
  domainChart?.setOption({
    tooltip: { trigger: 'item' },
    series: [{ type: 'pie', radius: ['40%', '70%'], data: data.map(d => ({ name: d.domain, value: d.count })) }],
  }, true)
}

function renderFee(data: any[]) {
  feeChart = ensureChart(feeRef.value, feeChart)
  feeChart?.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', data: data.map(d => d.domain) },
    yAxis: { type: 'value' },
    series: [{ type: 'bar', itemStyle: { color: theme.themeConfig.colorPrimary }, data: data.map(d => d.fee) }],
  }, true)
}

function handleSearch() { fetchAll() }

onMounted(() => { loadNetworkOptions(); fetchAll() })
onBeforeUnmount(() => { ro?.disconnect(); trendChart?.dispose(); domainChart?.dispose(); feeChart?.dispose() })
</script>

<template>
  <div class="page">
    <PageHeader title="网点总览">
      <template #left><span class="view-title">网点总览</span></template>
      <template #actions>
        <div style="display:flex;align-items:center;gap:8px;">
          <a-select v-model:value="carrier" size="middle" style="width:100px" :options="[{ value: '申通', label: '申通' }]" />
          <a-select
            v-model:value="networkCode" size="middle" style="width:220px"
            placeholder="全部网点" allow-clear show-search option-filter-prop="label"
            :options="networkOptions" />
          <a-range-picker v-model:value="dateRange" size="middle" style="width:260px" />
          <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        </div>
      </template>
    </PageHeader>

    <div class="kpi-strip">
      <div class="kpi-item"><span class="kpi-label">当天及时签收率</span><span class="kpi-value">{{ fmtRate(kpi.signRateToday) }}</span><span class="kpi-sub">48h {{ fmtRate(kpi.signRate48h) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">出仓及时率</span><span class="kpi-value">{{ fmtRate(kpi.outboundOnTimeRate) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">滞留率</span><span class="kpi-value">{{ fmtRate(kpi.retentionRate) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">积压倍数</span><span class="kpi-value">{{ fmtNum(kpi.backlogMultiple) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">遗失率(ppm)</span><span class="kpi-value">{{ fmtNum(kpi.lossRatePpm) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">虚签率</span><span class="kpi-value">{{ fmtRate(kpi.fakeSignRate) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">考核金额合计</span><span class="kpi-value">¥{{ fmtNum(kpi.totalAssessFee) }}</span></div>
      <div class="kpi-divider" />
      <div class="kpi-item"><span class="kpi-label">问题件数</span><span class="kpi-value">{{ fmtNum(kpi.problemEventCount) }}</span></div>
    </div>

    <a-row :gutter="12">
      <a-col :span="24"><a-card title="近30天多指标趋势" size="small"><div ref="trendRef" style="height:320px" /></a-card></a-col>
    </a-row>
    <a-row :gutter="12" style="margin-top:12px;">
      <a-col :span="12"><a-card title="质量域问题分布" size="small"><div ref="domainRef" style="height:300px" /></a-card></a-col>
      <a-col :span="12"><a-card title="考核金额按域构成" size="small"><div ref="feeRef" style="height:300px" /></a-card></a-col>
    </a-row>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
.page { padding: 12px; }
.view-title { font-weight: 600; }
.kpi-strip {
  display: flex; align-items: center; flex-wrap: wrap; gap: 0; row-gap: 8px;
  padding: 12px 20px; background: var(--bg-card); border: 1px solid var(--border);
  border-radius: 8px; margin-bottom: 12px;
}
.kpi-item { display: flex; flex-direction: column; align-items: center; padding: 0 24px; min-width: 100px; }
.kpi-label { font-size: 12px; color: var(--text-3); line-height: 18px; white-space: nowrap; }
.kpi-value { font-size: 20px; font-weight: 600; color: var(--text-1); line-height: 28px; font-variant-numeric: tabular-nums; }
.kpi-sub { font-size: 12px; color: var(--text-3); }
.kpi-divider { width: 1px; height: 32px; background: var(--border); flex-shrink: 0; }
</style>
