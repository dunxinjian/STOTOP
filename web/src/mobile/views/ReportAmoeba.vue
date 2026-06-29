<template>
  <div class="page-report-amoeba">
    <van-nav-bar title="阿米巴损益" left-arrow @click-left="$router.back()" />

    <!-- 组织切换 -->
    <van-tabs v-model:active="activeOrg" shrink sticky>
      <van-tab v-for="org in orgs" :key="org.id" :title="org.name" :name="org.id" />
    </van-tabs>

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <!-- 接口不可用时明确告知，决不能静默显示全 0 的假数据 -->
      <van-empty
        v-if="!loading && loadFailed"
        image="error"
        description="移动端阿米巴报表服务尚未上线，请使用电脑端「财务 → 阿米巴经营报表」查看"
      />
      <div class="report-body" v-else-if="!loading">
        <!-- 收入合计 -->
        <van-collapse v-model="activeCollapse">
          <van-collapse-item name="revenue" :title="`收入合计: ¥${formatNum(summary.revenue)}`">
            <div class="detail-row" v-for="item in details.revenue" :key="item.name">
              <span class="detail-label">{{ item.name }}</span>
              <span class="detail-value">¥{{ formatNum(item.value) }}</span>
            </div>
          </van-collapse-item>

          <van-collapse-item name="cost" :title="`成本合计: ¥${formatNum(summary.cost)}`">
            <div class="detail-row" v-for="item in details.cost" :key="item.name">
              <span class="detail-label">{{ item.name }}</span>
              <span class="detail-value">¥{{ formatNum(item.value) }}</span>
            </div>
          </van-collapse-item>
        </van-collapse>

        <!-- 汇总指标 -->
        <div class="summary-section">
          <div class="summary-row highlight">
            <span>经营利润</span>
            <span :class="summary.profit >= 0 ? 'text-green' : 'text-red'">
              ¥{{ formatNum(summary.profit) }}
            </span>
          </div>
          <div class="summary-row">
            <span>利润率</span>
            <span :class="summary.profitRate >= 0 ? 'text-green' : 'text-red'">
              {{ summary.profitRate.toFixed(1) }}%
            </span>
          </div>
        </div>
      </div>

      <van-loading v-else class="page-loading" />
    </van-pull-refresh>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import {
  NavBar as VanNavBar,
  Tabs as VanTabs,
  Tab as VanTab,
  PullRefresh as VanPullRefresh,
  Collapse as VanCollapse,
  CollapseItem as VanCollapseItem,
  Loading as VanLoading,
  Empty as VanEmpty,
} from 'vant'
import { get } from '@/api/request'

defineOptions({ name: 'MobileReportAmoeba' })

interface DetailItem {
  name: string
  value: number
}

const orgs = ref([
  { id: 192, name: '城区' },
  { id: 193, name: '新城' },
  { id: 194, name: '沙溪' },
])
const activeOrg = ref(192)
const activeCollapse = ref<string[]>(['revenue'])
const loading = ref(false)
const refreshing = ref(false)
const loadFailed = ref(false)

const summary = ref({
  revenue: 0,
  cost: 0,
  profit: 0,
  profitRate: 0,
})

const details = ref<{ revenue: DetailItem[]; cost: DetailItem[] }>({
  revenue: [],
  cost: [],
})

function formatNum(v: number): string {
  if (Math.abs(v) >= 10000) return (v / 10000).toFixed(2) + '万'
  return v.toLocaleString()
}

async function loadReport() {
  loading.value = true
  loadFailed.value = false
  try {
    const now = new Date()
    const period = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
    const res = await get<any>('/amoeba/pl/report', {
      period,
      orgId: activeOrg.value,
    })
    if (res) {
      summary.value = {
        revenue: res.totalRevenue ?? 0,
        cost: res.totalCost ?? 0,
        profit: res.profit ?? 0,
        profitRate: res.profitRate ?? 0,
      }
      details.value = {
        revenue: res.revenueItems ?? [
          { name: '出港收入', value: res.outboundRevenue ?? 0 },
          { name: '进港收入', value: res.inboundRevenue ?? 0 },
          { name: '代收货款', value: res.codRevenue ?? 0 },
        ],
        cost: res.costItems ?? [
          { name: '运输成本', value: res.transportCost ?? 0 },
          { name: '人工成本', value: res.laborCost ?? 0 },
          { name: '其他成本', value: res.otherCost ?? 0 },
        ],
      }
    }
  } catch (e) {
    // 后端尚无 /amoeba/pl/report 接口（移动端报表 API 待建设），显式进入不可用状态
    console.error('[ReportAmoeba] 加载失败:', e)
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

async function onRefresh() {
  await loadReport()
  refreshing.value = false
}

watch(activeOrg, () => loadReport())
onMounted(() => loadReport())
</script>

<style scoped>
.page-report-amoeba {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 20px;
}
.report-body {
  padding: 12px 16px;
}
.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 14px;
  border-bottom: 1px solid #f5f5f5;
}
.detail-label {
  color: #595959;
}
.detail-value {
  color: #1a1a1a;
  font-weight: 500;
}
.summary-section {
  background: #fff;
  border-radius: 10px;
  margin-top: 12px;
  padding: 16px;
}
.summary-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 14px;
  color: #595959;
}
.summary-row.highlight {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
}
.text-green {
  color: var(--color-success);
}
.text-red {
  color: var(--color-danger);
}
.page-loading {
  display: flex;
  justify-content: center;
  padding: 60px 0;
}
</style>
