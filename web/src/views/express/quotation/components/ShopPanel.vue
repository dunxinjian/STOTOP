<template>
  <div class="shop-panel">
    <div class="panel-header">
      <span class="panel-title">关联店铺</span>
      <span class="panel-count" v-if="shopList.length">({{ shopList.length }})</span>
    </div>

    <!-- 加载状态 -->
    <a-spin v-if="listLoading" size="small" style="display: block; text-align: center; padding: 24px 0" />

    <!-- 店铺列表 -->
    <div v-else class="shop-list">
      <div
        v-for="shop in shopList"
        :key="shop.id"
        class="shop-item"
      >
        <span class="shop-name" :title="shop.shopName">{{ shop.shopName }}</span>
        <CloseOutlined class="shop-remove" @click="handleRemove(shop)" />
      </div>
      <div v-if="shopList.length === 0 && quotationId" class="shop-empty">
        暂无关联店铺
      </div>
      <div v-if="!quotationId" class="shop-empty">
        保存后可添加店铺
      </div>
    </div>

    <!-- 添加区域（仅编辑模式） -->
    <div class="panel-footer" v-if="quotationId">
      <a-auto-complete
        v-model:value="selectedShopName"
        placeholder="输入或搜索店铺"
        :options="searchAutoCompleteOptions"
        :filter-option="false"
        :loading="searchLoading"
        allow-clear
        size="small"
        style="width: 100%; margin-bottom: 6px"
        @search="handleSearch"
      />
      <a-button
        type="dashed"
        size="small"
        block
        :loading="adding"
        :disabled="!candidateShopName"
        @click="handleAdd"
      >
        <template #icon><PlusOutlined /></template>
        添加
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, h } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined, CloseOutlined } from '@ant-design/icons-vue'
import {
  getQuotationShops,
  addQuotationShops,
  removeQuotationShop,
  checkQuotationShopConflicts,
  getExpShopList,
  type QuotationShopDto,
  type ShopListItemDto,
  type ShopConflictItem,
} from '@/api/express'

const props = defineProps<{
  quotationId: number
}>()

// ===== 店铺列表 =====
const listLoading = ref(false)
const shopList = ref<QuotationShopDto[]>([])

async function fetchShops() {
  if (!props.quotationId) return
  listLoading.value = true
  try {
    const res = await getQuotationShops(props.quotationId)
    shopList.value = Array.isArray(res) ? res : []
  } catch {
    message.error('加载关联店铺失败')
  } finally {
    listLoading.value = false
  }
}

// ===== 搜索添加 =====
const selectedShopName = ref('')
const searchOptions = ref<ShopListItemDto[]>([])
const searchAutoCompleteOptions = computed(() =>
  searchOptions.value.map(s => ({ value: s.name, label: s.name }))
)
const candidateShopName = computed(() => selectedShopName.value.trim())
const searchLoading = ref(false)
const adding = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | null = null

function handleSearch(value: string) {
  if (searchTimer) clearTimeout(searchTimer)
  if (!value) {
    searchOptions.value = []
    return
  }
  searchLoading.value = true
  searchTimer = setTimeout(async () => {
    try {
      const res = await getExpShopList({ keyword: value, pageIndex: 1, pageSize: 30 })
      const items = Array.isArray(res) ? res : (res.items || [])
      // 过滤已关联的店铺
      const existingNames = new Set(shopList.value.map(s => (s.shopName || '').trim().toLowerCase()))
      searchOptions.value = items.filter((s: ShopListItemDto) => !existingNames.has(s.name.trim().toLowerCase()))
    } catch {
      searchOptions.value = []
    } finally {
      searchLoading.value = false
    }
  }, 300)
}

function isShopAlreadyLinked(shopName: string) {
  const normalized = shopName.trim().toLowerCase()
  return shopList.value.some(s => (s.shopName || '').trim().toLowerCase() === normalized)
}

async function doAddShop(shopName: string) {
  await addQuotationShops(props.quotationId, {
    shopNames: [shopName],
  })
  message.success('添加成功')
  selectedShopName.value = ''
  searchOptions.value = []
  fetchShops()
}

async function handleAdd() {
  const shopName = candidateShopName.value
  if (!shopName || !props.quotationId) return
  if (isShopAlreadyLinked(shopName)) {
    message.warning('该店铺已关联当前报价')
    return
  }
  adding.value = true
  try {
    // 先检查冲突
    const conflicts = await checkQuotationShopConflicts(props.quotationId, [shopName])
    const conflictList = Array.isArray(conflicts) ? conflicts : []
    if (conflictList.length > 0) {
      showConflictConfirm(conflictList, () => doAddShop(shopName))
    } else {
      await doAddShop(shopName)
    }
  } catch {
    message.error('添加失败')
  } finally {
    adding.value = false
  }
}

function showConflictConfirm(conflicts: ShopConflictItem[], onConfirm: () => Promise<void>) {
  Modal.confirm({
    title: '店铺关联冲突提示',
    width: 640,
    content: h('div', { style: 'margin-top:8px' }, [
      h('p', { style: 'margin-bottom:8px;color:var(--text-2)' }, '以下店铺已关联其他报价，继续添加可能导致冲突：'),
      h('table', {
        style: 'width:100%;border-collapse:collapse;font-size:13px',
      }, [
        h('thead', null, [
          h('tr', null, [
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '店铺名'),
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '业务对象类型'),
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '编号'),
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '品牌'),
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '报价名'),
            h('th', { style: 'border:1px solid var(--border);padding:6px 8px;background:var(--bg-muted);text-align:left' }, '生效日期'),
          ]),
        ]),
        h('tbody', null, conflicts.map(c =>
          h('tr', null, [
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.shopName),
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.clientTypeName),
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.clientId),
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.brandCode),
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.quotationName),
            h('td', { style: 'border:1px solid var(--border);padding:6px 8px' }, c.effectiveDate ?? '-'),
          ])
        )),
      ]),
    ]),
    async onOk() {
      await onConfirm()
    },
  })
}

// ===== 移除店铺 =====
function handleRemove(shop: QuotationShopDto) {
  Modal.confirm({
    title: '确认移除',
    content: `确定要移除店铺"${shop.shopName}"吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await removeQuotationShop(props.quotationId, shop.id)
        message.success('移除成功')
        fetchShops()
      } catch {
        message.error('移除失败')
      }
    },
  })
}

// ===== quotationId 变化时重新加载 =====
watch(() => props.quotationId, (val) => {
  if (val) {
    fetchShops()
  } else {
    shopList.value = []
  }
}, { immediate: true })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.shop-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 12px;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 8px;

  .panel-title {
    font-size: $font-size-sm;
    font-weight: 500;
    color: $text-primary;
  }

  .panel-count {
    font-size: $font-size-sm;
    color: $text-secondary;
  }
}

.shop-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.shop-item {
  display: flex;
  align-items: center;
  padding: 4px 8px;
  border-radius: $border-radius-sm;
  transition: $transition-base;

  &:hover {
    background: $color-primary-bg;

    .shop-remove {
      opacity: 1;
    }
  }

  .shop-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: $font-size-sm;
    color: $text-regular;
  }

  .shop-remove {
    flex-shrink: 0;
    font-size: 10px;
    color: $text-secondary;
    cursor: pointer;
    opacity: 0;
    transition: $transition-base;
    padding: 2px;

    &:hover {
      color: $color-danger;
    }
  }
}

.shop-empty {
  padding: 16px 0;
  text-align: center;
  font-size: $font-size-sm;
  color: $text-secondary;
}

.panel-footer {
  margin-top: 8px;
}
</style>
