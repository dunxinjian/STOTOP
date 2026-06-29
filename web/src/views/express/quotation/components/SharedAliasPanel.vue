<template>
  <div class="shared-alias-panel">
    <div class="panel-toolbar">
      <span class="panel-title">关联店铺 ({{ shopList.length }})</span>
      <a-button type="primary" size="small" @click="showManageModal = true">
        <template #icon><SettingOutlined /></template>
        管理店铺与别名
      </a-button>
    </div>

    <a-table
      :columns="shopColumns"
      :data-source="shopList"
      :loading="loading"
      :pagination="false"
      row-key="id"
      bordered
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'createdTime'">
          {{ record.createdTime?.substring(0, 10) }}
        </template>
      </template>
    </a-table>

    <!-- 别名区域 -->
    <template v-if="sharedShopEnabled">
      <a-divider style="margin: 12px 0 8px" />
      <div style="margin-bottom: 8px; font-weight: 500;">共享别名</div>
      <div v-if="aliasList.length === 0" style="color: var(--text-3); font-size: 12px;">暂无别名</div>
      <div v-else class="alias-tags">
        <a-tag v-for="alias in aliasList" :key="alias.id" :color="alias.isActive ? 'blue' : 'default'">
          {{ alias.alias }}
          <span v-if="!alias.isActive" style="margin-left: 4px; color: var(--text-3)">(已停用)</span>
        </a-tag>
      </div>
    </template>

    <!-- BatchShopModal placeholder -->
    <a-modal
      v-model:open="showManageModal"
      title="管理店铺与别名"
      :footer="null"
      width="600px"
      centered
    >
      <p style="color: var(--text-3);">店铺与别名的批量管理功能，请通过报价列表页的"批量管理"入口操作。</p>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { SettingOutlined } from '@ant-design/icons-vue'
import {
  getQuotationShops,
  getQuotationAliases,
  type QuotationShopDto,
  type QuotationAliasDto,
} from '@/api/express'

const props = defineProps<{
  quotationId: number
  sharedShopEnabled: boolean
}>()

const loading = ref(false)
const shopList = ref<QuotationShopDto[]>([])
const aliasList = ref<QuotationAliasDto[]>([])
const showManageModal = ref(false)

const shopColumns = [
  { title: '店铺名称', dataIndex: 'shopName', ellipsis: true },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '关联时间', dataIndex: 'createdTime', width: 120 },
]

async function loadData() {
  loading.value = true
  try {
    const [shops, aliases] = await Promise.all([
      getQuotationShops(props.quotationId),
      props.sharedShopEnabled ? getQuotationAliases(props.quotationId) : Promise.resolve([]),
    ])
    shopList.value = shops
    aliasList.value = aliases
  } catch {
    message.error('加载关联店铺失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (props.quotationId) {
    loadData()
  }
})
</script>

<style scoped lang="scss">
.shared-alias-panel {
  padding: 4px 0;
}

.panel-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.panel-title {
  font-weight: 500;
  font-size: 14px;
}

.alias-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
</style>
