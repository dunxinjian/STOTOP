<template>
  <div class="page-history">
    <van-nav-bar title="已处理" left-arrow @click-left="$router.back()" />

    <!-- Tab 筛选 -->
    <van-tabs v-model:active="activeTab" shrink @change="onTabChange">
      <van-tab title="全部" name="all" />
      <van-tab title="我审批的" name="approved" />
      <van-tab title="我发起的" name="initiated" />
    </van-tabs>

    <!-- 时间筛选 -->
    <div class="time-filter">
      <van-dropdown-menu>
        <van-dropdown-item v-model="daysFilter" :options="daysOptions" @change="onDaysChange" />
      </van-dropdown-menu>
    </div>

    <!-- 列表 -->
    <div class="list-wrapper">
      <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
        <van-list
          v-model:loading="loading"
          :finished="finished"
          finished-text="没有更多了"
          @load="loadMore"
        >
          <HistoryCard
            v-for="item in list"
            :key="item.id"
            :item="item"
            @click="goDetail(item.id)"
          />
        </van-list>
      </van-pull-refresh>

      <!-- 空状态 -->
      <van-empty v-if="!loading && list.length === 0" description="暂无已处理记录" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  Tab as VanTab,
  Tabs as VanTabs,
  List as VanList,
  PullRefresh as VanPullRefresh,
  Empty as VanEmpty,
  DropdownMenu as VanDropdownMenu,
  DropdownItem as VanDropdownItem,
} from 'vant'
import HistoryCard from '../components/HistoryCard.vue'
import type { HistoryItem } from '../components/HistoryCard.vue'
import { getHistory } from '@shared/api/cardflow'

defineOptions({ name: 'MobileHistory' })

const router = useRouter()

const activeTab = ref<string>('all')
const daysFilter = ref<number>(0)
const daysOptions = [
  { text: '全部时间', value: 0 },
  { text: '近7天', value: 7 },
  { text: '近30天', value: 30 },
  { text: '近3月', value: 90 },
]

const list = ref<HistoryItem[]>([])
const loading = ref(false)
const finished = ref(false)
const refreshing = ref(false)
const page = ref(1)
const pageSize = 20

async function fetchData() {
  loading.value = true
  try {
    const params: Record<string, unknown> = {
      page: page.value,
      pageSize,
      type: activeTab.value,
    }
    if (daysFilter.value > 0) {
      params.days = daysFilter.value
    }
    const res = await getHistory(params as any)
    const data = (res as any)?.data ?? res
    const items: HistoryItem[] = (data?.items ?? []).map((item: any) => ({
      id: item.id,
      title: item.title || item.cardNumber || '未命名',
      flowName: item.flowName,
      applicant: item.applicant || item.initiatorName || '',
      result: item.result || 'completed',
      completedAt: item.completedAt || item.completedTime || '',
    }))

    if (page.value === 1) {
      list.value = items
    } else {
      list.value.push(...items)
    }

    const total = data?.total ?? 0
    if (list.value.length >= total || items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    console.error('[History] fetch error:', e)
    finished.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

function loadMore() {
  if (!finished.value) {
    page.value++
    fetchData()
  }
}

function onRefresh() {
  page.value = 1
  finished.value = false
  fetchData()
}

function onTabChange() {
  page.value = 1
  list.value = []
  finished.value = false
  fetchData()
}

function onDaysChange() {
  page.value = 1
  list.value = []
  finished.value = false
  fetchData()
}

function goDetail(id: number) {
  router.push(`/m/card/${id}`)
}

onMounted(() => {
  fetchData()
})
</script>

<style scoped lang="scss">
.page-history {
  min-height: 100vh;
  background: #f5f6f7;
  display: flex;
  flex-direction: column;
}

.time-filter {
  :deep(.van-dropdown-menu__bar) {
    background: #f5f6f7;
    box-shadow: none;
    height: 40px;
  }
}

.list-wrapper {
  flex: 1;
  padding: 12px 16px;
  overflow-y: auto;
}
</style>
