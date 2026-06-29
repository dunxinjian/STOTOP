<template>
  <a-modal
    :open="open"
    :title="`${quotationName} - 关联店铺`"
    :width="700"
    :destroy-on-close="true"
    :footer="null"
    centered
    :body-style="{ padding: 0, display: 'flex', flexDirection: 'column' }"
    @cancel="emit('update:open', false)"
  >
    <!-- 添加店铺区域（冻结顶部） -->
    <div class="batch-shop-toolbar">
      <a-auto-complete
        v-model:value="addShopName"
        placeholder="输入或搜索店铺"
        :options="shopAutoCompleteOptions"
        :filter-option="false"
        :loading="shopSearchLoading"
        allow-clear
        style="flex: 1"
        @search="handleShopSearch"
      />
      <a-button type="primary" :loading="adding" :disabled="!candidateShopName" @click="handleAddShop">
        添加
      </a-button>
    </div>

    <!-- 可滚动内容区 -->
    <div class="batch-shop-scroll">

    <!-- 店铺列表 -->
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      bordered
      size="small"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'createdTime'">
          {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
        </template>
        <template v-if="column.key === 'action'">
          <a-button type="link" size="small" danger @click="handleRemove(record as QuotationShopDto)">移除</a-button>
        </template>
      </template>
    </a-table>

    <!-- 共享别名管理（仅当开启共享店铺时显示） -->
    <template v-if="sharedShopEnabled">
      <a-divider>共享别名管理</a-divider>
      <a-table
        :columns="aliasColumns"
        :data-source="aliasDataSource"
        :loading="aliasLoading"
        row-key="id"
        bordered
        size="small"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" danger @click="handleRemoveAlias(record as QuotationAliasDto)">删除</a-button>
          </template>
        </template>
      </a-table>
      <div style="display: flex; gap: 8px; margin-top: 12px; align-items: center">
        <a-input v-model:value="newAlias" placeholder="输入别名" style="flex: 1" />
        <a-button type="primary" :loading="aliasAdding" :disabled="!newAlias" @click="handleAddAlias">
          添加别名
        </a-button>
      </div>
    </template>
    </div><!-- /batch-shop-scroll -->
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref, watch, h } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  getQuotationShops,
  addQuotationShops,
  removeQuotationShop,
  checkQuotationShopConflicts,
  getQuotationAliases,
  addQuotationAlias,
  removeQuotationAlias,
  getExpShopList,
  type QuotationShopDto,
  type QuotationAliasDto,
  type ShopListItemDto,
  type ShopConflictItem,
} from '@/api/express'

const props = defineProps<{
  open: boolean
  quotationId: number
  quotationName: string
  sharedShopEnabled?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'changed'): void
}>()

// ===== 店铺列定义 =====
const columns = [
  { title: '店铺名称', dataIndex: 'shopName', width: 180 },
  { title: '备注', dataIndex: 'remark', width: 120, ellipsis: true },
  { title: '创建时间', dataIndex: 'createdTime', width: 150 },
  { title: '操作', key: 'action', width: 80, align: 'center' as const },
]

// ===== 店铺列表数据 =====
const loading = ref(false)
const dataSource = ref<QuotationShopDto[]>([])

async function fetchShops() {
  if (!props.quotationId) return
  loading.value = true
  try {
    const res = await getQuotationShops(props.quotationId)
    dataSource.value = Array.isArray(res) ? res : []
  } catch {
    message.error('查询店铺列表失败')
  } finally {
    loading.value = false
  }
}

// ===== 移除店铺 =====
function handleRemove(record: QuotationShopDto) {
  Modal.confirm({
    title: '确认移除',
    content: `确定要移除店铺"${record.shopName}"吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await removeQuotationShop(props.quotationId, record.id)
        message.success('移除成功')
        fetchShops()
        emit('changed')
      } catch {
        message.error('移除失败')
      }
    },
  })
}

// ===== 添加店铺 =====
const addShopName = ref('')
const adding = ref(false)
const shopOptions = ref<ShopListItemDto[]>([])
const shopAutoCompleteOptions = computed(() =>
  shopOptions.value.map(s => ({ value: s.name, label: s.name }))
)
const candidateShopName = computed(() => addShopName.value.trim())
const shopSearchLoading = ref(false)
let shopSearchTimer: any = null

function handleShopSearch(value: string) {
  if (shopSearchTimer) clearTimeout(shopSearchTimer)
  if (!value) {
    shopOptions.value = []
    return
  }
  shopSearchLoading.value = true
  shopSearchTimer = setTimeout(async () => {
    try {
      const res = await getExpShopList({ keyword: value, pageIndex: 1, pageSize: 50 })
      const items = Array.isArray(res) ? res : (res.items || [])
      const existingNames = new Set(dataSource.value.map(s => (s.shopName || '').trim().toLowerCase()))
      shopOptions.value = items.filter((s: ShopListItemDto) => !existingNames.has(s.name.trim().toLowerCase()))
    } catch {
      shopOptions.value = []
    } finally {
      shopSearchLoading.value = false
    }
  }, 300)
}

function isShopAlreadyLinked(shopName: string) {
  const normalized = shopName.trim().toLowerCase()
  return dataSource.value.some(s => (s.shopName || '').trim().toLowerCase() === normalized)
}

async function doAddShop(shopName: string) {
  await addQuotationShops(props.quotationId, {
    shopNames: [shopName],
  })
  message.success('添加成功')
  addShopName.value = ''
  shopOptions.value = []
  fetchShops()
  emit('changed')
}

async function handleAddShop() {
  const shopName = candidateShopName.value
  if (!shopName) return
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

// ===== 别名管理 =====
const aliasColumns = [
  { title: '别名', dataIndex: 'alias', width: 200 },
  { title: '创建时间', dataIndex: 'createdTime', width: 150 },
  { title: '操作', key: 'action', width: 80, align: 'center' as const },
]

const aliasLoading = ref(false)
const aliasDataSource = ref<QuotationAliasDto[]>([])
const newAlias = ref('')
const aliasAdding = ref(false)

async function fetchAliases() {
  if (!props.quotationId || !props.sharedShopEnabled) return
  aliasLoading.value = true
  try {
    const res = await getQuotationAliases(props.quotationId)
    aliasDataSource.value = Array.isArray(res) ? res : []
  } catch {
    message.error('查询别名列表失败')
  } finally {
    aliasLoading.value = false
  }
}

function handleRemoveAlias(record: QuotationAliasDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除别名"${record.alias}"吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await removeQuotationAlias(record.id)
        message.success('删除成功')
        fetchAliases()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

async function handleAddAlias() {
  if (!newAlias.value) return
  aliasAdding.value = true
  try {
    await addQuotationAlias(props.quotationId, { alias: newAlias.value })
    message.success('添加别名成功')
    newAlias.value = ''
    fetchAliases()
  } catch {
    message.error('添加别名失败')
  } finally {
    aliasAdding.value = false
  }
}

// ===== 打开时加载 =====
watch(() => props.open, (val) => {
  if (val) {
    addShopName.value = ''
    shopOptions.value = []
    newAlias.value = ''
    fetchShops()
    if (props.sharedShopEnabled) {
      fetchAliases()
    }
  }
})
</script>

<style scoped>
.batch-shop-toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  padding: 16px 24px 12px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--border);
}

.batch-shop-scroll {
  flex: 1;
  overflow-y: auto;
  max-height: 60vh;
  padding: 12px 24px 16px;
}
</style>
