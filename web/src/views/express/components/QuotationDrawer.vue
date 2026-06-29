<template>
  <a-drawer
    :open="open"
    :title="drawerTitle"
    :width="900"
    :destroy-on-close="true"
    @close="emit('update:open', false)"
  >
    <!-- 工具栏 -->
    <div style="display: flex; gap: 12px; margin-bottom: 16px; justify-content: flex-end">
      <a-button type="primary" @click="handleAdd">
        <template #icon><PlusOutlined /></template>新增报价
      </a-button>
    </div>

    <!-- 报价列表 -->
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      bordered
      size="small"
      :pagination="false"
      :scroll="{ x: 900 }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'clientType'">
          <a-tag>{{ getClientTypeLabel(record.clientType) }}</a-tag>
        </template>
        <template v-if="column.dataIndex === 'sharedShopEnabled'">
          <a-tag :color="record.sharedShopEnabled ? 'green' : 'default'">
            {{ record.sharedShopEnabled ? '是' : '否' }}
          </a-tag>
        </template>
        <template v-if="column.dataIndex === 'status'">
          <a-tag v-if="record.status === 0" color="blue">草稿</a-tag>
          <a-tag v-else-if="record.status === 1" color="processing">待审批</a-tag>
          <a-tag v-else-if="record.status === 2" color="green">已通过</a-tag>
          <a-tag v-else-if="record.status === 3" color="error">已驳回</a-tag>
          <a-tag v-else-if="record.status === 4" color="warning">已作废</a-tag>
        </template>
        <template v-if="column.dataIndex === 'effectiveDate'">
          {{ record.effectiveDate?.slice(0, 10) }}
        </template>
        <template v-if="column.key === 'action'">
          <a-button type="link" size="small" @click="handleViewShops(record as QuotationListItemDto)">关联店铺</a-button>
          <a-button type="link" size="small" @click="handleQuotationDetail(record as QuotationListItemDto)">报价详情</a-button>
          <a-button type="link" size="small" danger @click="handleDeleteQuotation(record as QuotationListItemDto)">删除</a-button>
        </template>
      </template>
    </a-table>

    <!-- 编辑弹窗 -->
    <QuotationEditModal
      v-model:open="editModalVisible"
      :quotation-id="0"
      @saved="handleSaved"
    />

    <!-- 关联店铺弹窗 -->
    <BatchShopModal
      v-model:open="batchShopModalVisible"
      :quotation-id="currentQuotation.id"
      :quotation-name="currentQuotation.planName"
      :shared-shop-enabled="currentQuotation.sharedShopEnabled"
      @changed="fetchList"
    />
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import {
  getQuotationList,
  deleteQuotation,
  type QuotationListItemDto,
} from '@/api/express'
import QuotationEditModal from './QuotationEditModal.vue'
import BatchShopModal from './BatchShopModal.vue'

const clientTypeMap: Record<string, string> = {
  KH: '客户', DL: '代理', WD: '网点', YW: '业务员', CB: '承包区', YZ: '驿站',
}
function getClientTypeLabel(type: string) { return clientTypeMap[type] ?? type }

const props = defineProps<{
  open: boolean
  quotationId?: number
  clientName?: string
  clientId?: string
  clientType?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const router = useRouter()

const drawerTitle = computed(() =>
  props.clientName ? `${props.clientName} 的报价管理` : '报价管理'
)

// ===== 列定义 =====
const columns = [
  { title: '报价名称', dataIndex: 'planName', width: 160, ellipsis: true },
  { title: '方案编号', dataIndex: 'planCode', width: 140, ellipsis: true },
  { title: '类型', dataIndex: 'clientType', width: 80, align: 'center' as const },
  { title: '对象ID', dataIndex: 'clientId', width: 100, ellipsis: true },
  { title: '共享', dataIndex: 'sharedShopEnabled', width: 70, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', width: 100 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const, align: 'center' as const },
]

// ===== 列表查询 =====
const loading = ref(false)
const dataSource = ref<QuotationListItemDto[]>([])

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: 1, pageSize: 999 }
    if (props.clientId) params.clientId = props.clientId
    if (props.clientType) params.clientType = props.clientType
    const res = await getQuotationList(params)
    dataSource.value = Array.isArray(res.items) ? res.items : []
  } catch {
    message.error('查询报价列表失败')
  } finally {
    loading.value = false
  }
}

// ===== 新增 =====
const editModalVisible = ref(false)

function handleAdd() {
  editModalVisible.value = true
}

function handleSaved() {
  fetchList()
}

// ===== 关联店铺 =====
const batchShopModalVisible = ref(false)
const currentQuotation = reactive({
  id: 0,
  planName: '',
  sharedShopEnabled: false,
})

function handleViewShops(record: QuotationListItemDto) {
  currentQuotation.id = record.id
  currentQuotation.planName = record.planName
  currentQuotation.sharedShopEnabled = record.sharedShopEnabled
  batchShopModalVisible.value = true
}

// ===== 报价详情跳转 =====
function handleQuotationDetail(record: QuotationListItemDto) {
  router.push(`/express/quotation/edit/${record.id}`)
}

// ===== 删除 =====
function handleDeleteQuotation(record: QuotationListItemDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除报价"${record.planName}"吗？此操作不可恢复。`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteQuotation(record.id)
        message.success('删除成功')
        fetchList()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

// ===== 打开时加载数据 =====
watch(() => props.open, (val) => {
  if (val) {
    fetchList()
  }
})
</script>
