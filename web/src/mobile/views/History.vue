<template>
  <div class="page-history">
    <van-nav-bar title="已处理" left-arrow @click-left="$router.back()" />

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
  List as VanList,
  PullRefresh as VanPullRefresh,
  Empty as VanEmpty,
} from 'vant'
import HistoryCard from '../components/HistoryCard.vue'
import type { HistoryItem } from '../components/HistoryCard.vue'
import { getHistory } from '@shared/api/cardflow'

defineOptions({ name: 'MobileHistory' })

const router = useRouter()

const list = ref<HistoryItem[]>([])
const loading = ref(false)
const finished = ref(false)
const refreshing = ref(false)
const page = ref(1)
const pageSize = 20

async function fetchData() {
  loading.value = true
  try {
    const res = await getHistory({ page: page.value, pageSize })
    // 待办 DTO 无完成时间字段，用待办生成时间近似展示
    const items: HistoryItem[] = (res.items ?? []).map(item => ({
      id: item.cardId,
      title: item.title || item.cardNumber || '未命名',
      flowName: item.flowName,
      applicant: item.initiatorName || '',
      result: 'completed' as const,
      completedAt: item.createdTime || '',
    }))

    if (page.value === 1) {
      list.value = items
    } else {
      list.value.push(...items)
    }

    const total = res.total ?? 0
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

.list-wrapper {
  flex: 1;
  padding: 12px 16px;
  overflow-y: auto;
}
</style>
