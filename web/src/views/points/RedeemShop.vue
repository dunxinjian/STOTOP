<template>
  <div class="redeem-shop-page">
    <PageHeader title="积分商城" />

    <!-- 我的积分概览 -->
    <a-card :bordered="false" class="points-overview">
      <a-row :gutter="24">
        <a-col :span="6">
          <a-statistic title="可用积分" :value="account.availablePoints" :value-style="{ color: 'var(--color-info)', fontWeight: 700, fontSize: '28px' }">
            <template #prefix><WalletOutlined /></template>
          </a-statistic>
        </a-col>
        <a-col :span="6">
          <a-statistic title="累计获得" :value="account.totalPoints" :value-style="{ color: 'var(--color-success)' }">
            <template #prefix><RiseOutlined /></template>
          </a-statistic>
        </a-col>
        <a-col :span="6">
          <a-statistic title="已使用" :value="account.usedPoints" :value-style="{ color: 'var(--color-warning)' }">
            <template #prefix><ShoppingOutlined /></template>
          </a-statistic>
        </a-col>
        <a-col :span="6">
          <a-statistic title="本月获得" :value="account.monthlyAward" :value-style="{ color: 'var(--color-info)' }">
            <template #prefix><TrophyOutlined /></template>
          </a-statistic>
        </a-col>
      </a-row>
    </a-card>

    <!-- Tab 切换 -->
    <a-tabs v-model:activeKey="activeTab" style="margin-top: 8px">
      <!-- 商品浏览 -->
      <a-tab-pane key="shop" tab="积分商城">
        <!-- 分类筛选 -->
        <div class="category-filter">
          <a-radio-group v-model:value="selectedCategory" button-style="solid" @change="loadItems">
            <a-radio-button :value="null">全部</a-radio-button>
            <a-radio-button v-for="cat in categoryList" :key="cat.value" :value="cat.value">
              {{ cat.label }}
            </a-radio-button>
          </a-radio-group>
        </div>

        <!-- 商品网格 -->
        <a-spin :spinning="loadingItems">
          <EmptyState v-if="!loadingItems && items.length === 0" title="暂无商品" />
          <a-row v-else :gutter="[16, 16]" class="shop-grid">
            <a-col v-for="item in items" :key="item.id" :xs="24" :sm="12" :md="8" :lg="6">
              <RedeemCard :item="item" :my-points="account.availablePoints" @exchange="openExchangeConfirm" />
            </a-col>
          </a-row>
        </a-spin>

        <!-- 分页 -->
        <div v-if="total > pageSize" class="pagination-wrap">
          <a-pagination
            v-model:current="pageIndex"
            :total="total"
            :page-size="pageSize"
            show-size-changer
            @change="loadItems"
          />
        </div>
      </a-tab-pane>

      <!-- 我的兑换记录 -->
      <a-tab-pane key="records" tab="我的兑换记录">
        <a-table
          :columns="recordColumns"
          :data-source="myRecords"
          :loading="loadingRecords"
          :pagination="recordPagination"
          row-key="id"
          bordered
          @change="handleRecordTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="recordStatusColor(record.status)">{{ recordStatusLabel(record.status) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ formatTime(record.createTime) }}
            </template>
            <template v-if="column.dataIndex === 'issueTime'">
              {{ record.issueTime ? formatTime(record.issueTime) : '-' }}
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <!-- 兑换确认弹窗 -->
    <a-modal
      v-model:open="exchangeModalVisible"
      title="确认兑换"
      :confirm-loading="exchanging"
      @ok="handleExchange"
    >
      <div v-if="exchangeTarget" class="exchange-confirm">
        <p>确定要兑换 <strong>{{ exchangeTarget.name }}</strong> 吗？</p>
        <p>将消耗 <span class="exchange-points">{{ exchangeTarget.requiredPoints }}</span> 积分</p>
        <p>兑换后可用积分：<span class="exchange-remaining">{{ account.availablePoints - exchangeTarget.requiredPoints }}</span></p>
        <a-form-item label="备注">
          <a-input v-model:value="exchangeRemark" placeholder="可选备注" />
        </a-form-item>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  WalletOutlined, RiseOutlined, ShoppingOutlined, TrophyOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import RedeemCard from './components/RedeemCard.vue'
import {
  getRedeemItems, exchangePoints, getMyRedeemRecords, getMyPointAccount,
} from '@/api/points'
import type { RedeemItemListDto, PointAccountDto, RedeemRecordListDto } from '@/types/points'

// ==================== 分类 ====================
const categoryList = [
  { label: '假期福利', value: 0 },
  { label: '生活补贴', value: 1 },
  { label: '学习培训', value: 2 },
  { label: '团建活动', value: 3 },
  { label: '实物奖品', value: 4 },
  { label: '其他', value: 5 },
]

const categoryMap: Record<number, string> = {
  0: '假期福利', 1: '生活补贴', 2: '学习培训',
  3: '团建活动', 4: '实物奖品', 5: '其他',
}

// ==================== 状态 ====================
const activeTab = ref('shop')
const selectedCategory = ref<number | null>(null)

// 账户
const account = reactive<PointAccountDto>({
  id: 0, orgId: 0, userId: 0, userName: null, accountType: 0,
  totalPoints: 0, usedPoints: 0, availablePoints: 0,
  monthlyAward: 0, monthlyDeduct: 0, yearlyPoints: 0,
  fAPoints: 0, fBPoints: 0, snapshotDate: null, snapshotValue: 0, updateTime: '',
})

// 商品列表
const items = ref<RedeemItemListDto[]>([])
const loadingItems = ref(false)
const pageIndex = ref(1)
const pageSize = ref(12)
const total = ref(0)

// 兑换记录
const myRecords = ref<RedeemRecordListDto[]>([])
const loadingRecords = ref(false)
const recordPageIndex = ref(1)
const recordPageSize = ref(10)
const recordTotal = ref(0)

const recordPagination = computed(() => ({
  current: recordPageIndex.value,
  pageSize: recordPageSize.value,
  total: recordTotal.value,
  showSizeChanger: true,
}))

// 兑换确认
const exchangeModalVisible = ref(false)
const exchangeTarget = ref<RedeemItemListDto | null>(null)
const exchangeRemark = ref('')
const exchanging = ref(false)

// ==================== 表格列 ====================
const recordColumns = [
  { title: '商品名称', dataIndex: 'itemName', width: 180 },
  { title: '消耗积分', dataIndex: 'deductedPoints', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '兑换时间', dataIndex: 'createTime', width: 180 },
  { title: '发放时间', dataIndex: 'issueTime', width: 180 },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
]

// ==================== 方法 ====================
async function loadAccount() {
  try {
    const res = await getMyPointAccount()
    const data: any = (res as any)?.data ?? res
    Object.assign(account, data)
  } catch {
    // 新用户可能没有账户
  }
}

async function loadItems() {
  loadingItems.value = true
  try {
    const res = await getRedeemItems({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      category: selectedCategory.value,
      status: 1, // 上架中
    })
    const data: any = (res as any)?.data ?? res
    items.value = data?.items ?? []
    total.value = data?.total ?? 0
  } catch (e: any) {
    message.error(e?.message || '加载商品失败')
  } finally {
    loadingItems.value = false
  }
}

async function loadMyRecords() {
  loadingRecords.value = true
  try {
    const res = await getMyRedeemRecords({
      pageIndex: recordPageIndex.value,
      pageSize: recordPageSize.value,
    })
    const data: any = (res as any)?.data ?? res
    myRecords.value = data?.items ?? []
    recordTotal.value = data?.total ?? 0
  } catch (e: any) {
    message.error(e?.message || '加载兑换记录失败')
  } finally {
    loadingRecords.value = false
  }
}

function openExchangeConfirm(itemId: number) {
  const item = items.value.find(i => i.id === itemId)
  if (!item) return
  exchangeTarget.value = item
  exchangeRemark.value = ''
  exchangeModalVisible.value = true
}

async function handleExchange() {
  if (!exchangeTarget.value) return
  exchanging.value = true
  try {
    await exchangePoints({
      itemId: exchangeTarget.value.id,
      remark: exchangeRemark.value || undefined,
    })
    message.success('兑换成功！')
    exchangeModalVisible.value = false
    await Promise.all([loadAccount(), loadItems()])
    // 如果在记录tab，也刷新记录
    if (activeTab.value === 'records') {
      await loadMyRecords()
    }
  } catch (e: any) {
    message.error(e?.message || '兑换失败')
  } finally {
    exchanging.value = false
  }
}

function handleRecordTableChange(pagination: any) {
  recordPageIndex.value = pagination.current
  recordPageSize.value = pagination.pageSize
  loadMyRecords()
}

function recordStatusColor(status: number): string {
  switch (status) {
    case 0: return 'processing'
    case 1: return 'success'
    case 2: return 'error'
    default: return 'default'
  }
}

function recordStatusLabel(status: number): string {
  switch (status) {
    case 0: return '待发放'
    case 1: return '已发放'
    case 2: return '已取消'
    default: return '未知'
  }
}

function formatTime(val: string) {
  if (!val) return ''
  return val.replace('T', ' ').substring(0, 19)
}

// ==================== 初始化 ====================
onMounted(async () => {
  await Promise.all([loadAccount(), loadItems()])
})

// 切换tab时加载记录
watch(activeTab, (val) => {
  if (val === 'records' && myRecords.value.length === 0) {
    loadMyRecords()
  }
})
</script>

<style scoped lang="scss">
.redeem-shop-page {
  padding: 20px;
  background: #fff;
  border-radius: 8px;
  min-height: calc(100vh - 120px);
}

.points-overview {
  margin-bottom: 8px;
  border-radius: 8px;
  background: linear-gradient(135deg, #f0f5ff 0%, var(--color-info-light) 100%);
}

.category-filter {
  margin-bottom: 16px;
}

.shop-grid {
  min-height: 200px;
}

.pagination-wrap {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.exchange-confirm {
  p {
    margin-bottom: 8px;
    font-size: 14px;
  }
  .exchange-points {
    font-size: 20px;
    font-weight: 700;
    color: var(--color-danger);
  }
  .exchange-remaining {
    font-size: 16px;
    font-weight: 600;
    color: var(--color-info);
  }
}
</style>
