<template>
  <div class="shop-query-panel">
    <!-- 搜索区 -->
    <div class="search-area">
      <a-select
        v-model:value="selectedShop"
        show-search
        :filter-option="false"
        :not-found-content="shopFetching ? undefined : null"
        placeholder="输入店铺名称搜索..."
        style="width: 100%"
        allow-clear
        @search="handleShopSearch"
        @change="handleShopChange"
      >
        <template #notFoundContent>
          <a-spin v-if="shopFetching" size="small" />
          <span v-else>无匹配结果</span>
        </template>
        <a-select-option v-for="item in shopOptions" :key="item.name" :value="item.name">
          {{ item.name }}
        </a-select-option>
      </a-select>
    </div>

    <!-- 内容区 -->
    <div class="content-area">
      <!-- 未选择店铺 -->
      <div v-if="!selectedShop" class="empty-state">
        <a-empty description="请在上方搜索并选择一个店铺" />
      </div>

      <!-- 加载中 -->
      <div v-else-if="loading" class="loading-state">
        <a-spin tip="加载中..." />
      </div>

      <!-- 有数据 -->
      <template v-else-if="groups.length > 0">
        <a-collapse v-model:activeKey="activeKeys" :bordered="false">
          <a-collapse-panel v-for="(group, gi) in groups" :key="String(gi)">
            <template #header>
              <div class="panel-header">
                <a-tag :color="getClientTypeColor(group.clientType)" size="small">
                  {{ getClientTypeLabel(group.clientType) }}
                </a-tag>
                <span class="panel-client-name">{{ group.clientName }}</span>
                <a-badge :count="group.quotations.length" :number-style="{ backgroundColor: 'var(--color-info)' }" />
              </div>
            </template>
            <a-table
              :columns="tableColumns"
              :data-source="group.quotations"
              row-key="id"
              size="small"
              :pagination="false"
              bordered
            >
              <template #bodyCell="{ column, record, index }">
                <template v-if="column.dataIndex === 'index'">
                  {{ index + 1 }}
                </template>
                <template v-if="column.dataIndex === 'planName'">
                  <a class="plan-name-link" @click="router.push(`/express/quotation/edit/${record.id}`)">
                    {{ record.planName }}
                  </a>
                </template>
                <template v-if="column.dataIndex === 'status'">
                  <a-tag :color="statusConfig[record.status]?.color ?? 'default'">
                    {{ statusConfig[record.status]?.label ?? '未知' }}
                  </a-tag>
                </template>
              </template>
            </a-table>
          </a-collapse-panel>
        </a-collapse>
      </template>

      <!-- 无数据 -->
      <div v-else class="empty-state">
        <a-empty description="该店铺暂无关联报价" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import {
  getExpShopList,
  getQuotationsByShop,
  type QuotationByShopGroupDto,
  type ShopListItemDto,
} from '@/api/express'

const router = useRouter()

// ==================== 常量 ====================

const clientTypeConfig: Record<string, { label: string; color: string }> = {
  KH: { label: '客户', color: 'blue' },
  DL: { label: '代理', color: 'purple' },
  WD: { label: '网点', color: 'green' },
  CB: { label: '承包区', color: 'orange' },
  YZ: { label: '驿站', color: 'cyan' },
  YW: { label: '业务员', color: 'volcano' },
}

const statusConfig: Record<number, { label: string; color: string }> = {
  0: { label: '草稿', color: 'default' },
  1: { label: '生效', color: 'success' },
  2: { label: '过期', color: 'warning' },
  3: { label: '作废', color: 'error' },
}

function getClientTypeColor(type: string | null): string {
  if (!type) return 'default'
  return clientTypeConfig[type]?.color ?? 'default'
}

function getClientTypeLabel(type: string | null): string {
  if (!type) return '未知'
  return clientTypeConfig[type]?.label ?? type
}

// ==================== 店铺搜索 ====================

const selectedShop = ref<string | undefined>(undefined)
const shopOptions = ref<ShopListItemDto[]>([])
const shopFetching = ref(false)
let shopSearchTimer: ReturnType<typeof setTimeout> | null = null

function handleShopSearch(value: string) {
  if (shopSearchTimer) clearTimeout(shopSearchTimer)
  if (!value) {
    shopOptions.value = []
    return
  }
  shopSearchTimer = setTimeout(async () => {
    shopFetching.value = true
    try {
      const res = await getExpShopList({ keyword: value, pageIndex: 1, pageSize: 50 })
      shopOptions.value = res.items || []
    } catch {
      shopOptions.value = []
    } finally {
      shopFetching.value = false
    }
  }, 300)
}

function handleShopChange(value: SelectValue) {
  const shop = value == null ? undefined : String(value)
  selectedShop.value = shop
  if (shop) {
    fetchGroups(shop)
  } else {
    groups.value = []
    activeKeys.value = []
  }
}

// ==================== 报价数据 ====================

const loading = ref(false)
const groups = ref<QuotationByShopGroupDto[]>([])
const activeKeys = ref<string[]>([])

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '报价名称', dataIndex: 'planName', width: 200, ellipsis: true },
  { title: '方案编号', dataIndex: 'planCode', width: 160, ellipsis: true },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', width: 120, align: 'center' as const },
]

async function fetchGroups(shopName: string) {
  loading.value = true
  try {
    const res = await getQuotationsByShop(shopName)
    groups.value = res
    // 默认全部展开
    activeKeys.value = res.map((_, i) => String(i))
  } catch {
    message.error('获取报价数据失败')
    groups.value = []
    activeKeys.value = []
  } finally {
    loading.value = false
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.shop-query-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 16px;
  background: var(--bg-card);
  border: 1px solid $border-color-lighter;
  overflow: hidden;
}

.search-area {
  margin-bottom: 16px;
  flex-shrink: 0;
}

.content-area {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
}

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 8px;

  .panel-client-name {
    font-weight: 500;
  }
}

.plan-name-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}
</style>
