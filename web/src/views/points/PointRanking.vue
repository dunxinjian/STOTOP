<template>
  <div class="point-ranking-page">
    <PageHeader title="积分排行榜" />

    <a-card :bordered="false" class="section-card">
      <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
        <a-tab-pane key="monthly" tab="月度排行" />
        <a-tab-pane key="quarterly" tab="季度排行" />
        <a-tab-pane key="yearly" tab="年度排行" />
        <a-tab-pane key="department" tab="部门排名" />
      </a-tabs>

      <!-- 我的排名概览 -->
      <div v-if="activeTab !== 'department' && myRanking" class="my-rank-bar">
        <span>我的当前排名：</span>
        <span class="my-rank-bar__rank">第 {{ myRanking.rank }} 名</span>
        <a-divider type="vertical" />
        <span>总积分：<b style="color: var(--color-info)">{{ myRanking.totalPoints }}</b></span>
        <a-divider type="vertical" />
        <span>共 {{ myRanking.totalUsers }} 人参与排名</span>
      </div>

      <!-- 个人排行榜 -->
      <a-spin :spinning="loading" v-if="activeTab !== 'department'">
        <RankingList :rankings="rankings" :myUserId="currentUserId" />
        <div class="pagination-wrap">
          <a-pagination
            v-model:current="pageIndex"
            v-model:pageSize="pageSize"
            :total="total"
            :showTotal="(t: number) => `共 ${t} 条`"
            show-size-changer
            show-quick-jumper
            @change="loadRankings"
          />
        </div>
      </a-spin>

      <!-- 部门排行 -->
      <a-spin :spinning="deptLoading" v-if="activeTab === 'department'">
        <a-table
          :columns="deptColumns"
          :data-source="deptRankings"
          :pagination="false"
          row-key="departmentId"
          size="middle"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'rank'">
              <TrophyOutlined v-if="record.rank <= 3" :style="{ color: medalColors[record.rank - 1], fontSize: '18px' }" />
              <span v-else>{{ record.rank }}</span>
            </template>
            <template v-if="column.dataIndex === 'avgPoints'">
              {{ record.avgPoints.toFixed(1) }}
            </template>
          </template>
        </a-table>
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { TrophyOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import RankingList from './components/RankingList.vue'
import { getRankings, getDepartmentRankings, getMyRanking } from '@/api/points'
import type { RankingListDto, DepartmentRankingDto, MyRankingDto } from '@/types/points'

const activeTab = ref('monthly')
const loading = ref(false)
const deptLoading = ref(false)

const rankings = ref<RankingListDto[]>([])
const deptRankings = ref<DepartmentRankingDto[]>([])
const myRanking = ref<MyRankingDto | null>(null)
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(20)
const currentUserId = ref(0) // 从用户 store 获取

const medalColors = ['#faad14', '#bfbfbf', '#d48806']

const dimensionMap: Record<string, number> = {
  monthly: 0,
  quarterly: 1,
  yearly: 2,
}

const deptColumns = [
  { title: '排名', dataIndex: 'rank', width: 80 },
  { title: '部门名称', dataIndex: 'departmentName', width: 180 },
  { title: '总积分', dataIndex: 'totalPoints', width: 120 },
  { title: '奖分', dataIndex: 'awardPoints', width: 100 },
  { title: '扣分', dataIndex: 'deductPoints', width: 100 },
  { title: '人数', dataIndex: 'memberCount', width: 80 },
  { title: '人均积分', dataIndex: 'avgPoints', width: 120 },
]

function handleTabChange() {
  pageIndex.value = 1
  if (activeTab.value === 'department') {
    loadDeptRankings()
  } else {
    loadRankings()
    loadMyRanking()
  }
}

async function loadRankings() {
  loading.value = true
  try {
    const dimension = dimensionMap[activeTab.value] ?? 0
    const res = await getRankings({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      dimension,
    })
    rankings.value = res.items
    total.value = res.total
  } catch {
    message.error('加载排行榜失败')
  } finally {
    loading.value = false
  }
}

async function loadDeptRankings() {
  deptLoading.value = true
  try {
    deptRankings.value = await getDepartmentRankings({ dimension: 0 })
  } catch {
    message.error('加载部门排名失败')
  } finally {
    deptLoading.value = false
  }
}

async function loadMyRanking() {
  try {
    const dimension = dimensionMap[activeTab.value] ?? 0
    myRanking.value = await getMyRanking({ dimension })
  } catch {
    // ignore
  }
}

onMounted(() => {
  loadRankings()
  loadMyRanking()
})
</script>

<style scoped lang="scss">
.point-ranking-page {
  padding: 0 4px;
}

.section-card {
  border-radius: 8px;
}

.my-rank-bar {
  padding: 12px 16px;
  background: var(--color-info-light);
  border: 1px solid var(--color-info);
  border-radius: 6px;
  margin-bottom: 16px;
  font-size: 14px;

  &__rank {
    font-size: 18px;
    font-weight: 700;
    color: var(--color-info);
  }
}

.pagination-wrap {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
