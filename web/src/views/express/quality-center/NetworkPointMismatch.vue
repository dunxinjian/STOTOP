<template>
  <div :class="{ 'page-container': !embedded }">
    <PageHeader v-if="!embedded" title="网点不一致">
      <template #actions>
        <a-button :disabled="!selectedRowKeys.length" @click="onIgnore">
          批量忽略
        </a-button>
        <span v-if="selectedRowKeys.length" style="color: #999; font-size: 13px;">
          已选 {{ selectedRowKeys.length }} 条
        </span>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input-number v-model:value="query.batchId" size="small" placeholder="批次ID" :min="1" style="width: 150px;" />
          <a-input v-model:value="query.waybillNo" size="small" placeholder="搜索运单编号" allow-clear style="width: 180px;" />
          <a-button type="primary" size="small" @click="onSearch">查询</a-button>
          <a-button size="small" @click="onReset">重置</a-button>
        </div>
      </template>
    </PageHeader>
    <div v-else style="margin-bottom: 12px;">
      <a-button size="small" :disabled="!selectedRowKeys.length" @click="onIgnore">批量忽略</a-button>
      <span v-if="selectedRowKeys.length" style="color: #999; font-size: 13px; margin-left:8px">已选 {{ selectedRowKeys.length }} 条</span>
    </div>

    <a-alert
      message="以下运单的映射网点编号与报价方案配置的网点编号不一致，请确认是否需要调整报价配置。"
      type="warning"
      show-icon
      style="margin-bottom: 16px;"
    />

    <a-table
      :columns="columns"
      :data-source="list"
      :pagination="pagination"
      :loading="loading"
      :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
      row-key="errorId"
      size="middle"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'dispatchStatus'">
          <a-tag v-if="record.dispatchStatus === 'Pending'" color="orange">Pending</a-tag>
          <a-tag v-else-if="record.dispatchStatus === 'Ignored'" color="default">Ignored</a-tag>
          <a-tag v-else-if="record.dispatchStatus === 'Resolved'" color="green">Resolved</a-tag>
          <span v-else>{{ record.dispatchStatus || '-' }}</span>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import PageHeader from '@/components/PageHeader.vue'
import {
  getNetworkPointMismatches,
  ignoreMismatchErrors,
  type NetworkPointMismatchItemDto,
} from '@/api/qualityCenter'

const props = withDefaults(defineProps<{ embedded?: boolean; initialQuery?: Record<string, any> }>(), { embedded: false })

// ==================== 列定义 ====================
const columns = [
  { title: '错误ID', dataIndex: 'errorId', key: 'errorId', width: 90 },
  { title: '批次ID', dataIndex: 'batchId', key: 'batchId', width: 90 },
  { title: '运单编号', dataIndex: 'waybillNo', key: 'waybillNo', width: 160 },
  { title: '映射网点编号', dataIndex: 'mappedNpCode', key: 'mappedNpCode', width: 140 },
  { title: '报价网点编号', dataIndex: 'quotationNpCode', key: 'quotationNpCode', width: 140 },
  { title: '状态', key: 'dispatchStatus', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
]

// ==================== 查询/分页 ====================
const loading = ref(false)
const list = ref<NetworkPointMismatchItemDto[]>([])
const query = reactive({ batchId: undefined as number | undefined, waybillNo: undefined as string | undefined })
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function loadList() {
  loading.value = true
  try {
    const res = (await getNetworkPointMismatches({
      batchId: query.batchId,
      waybillNo: query.waybillNo,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    })) as any
    list.value = res?.items || []
    pagination.total = res?.total || 0
  } catch (e: any) {
    message.error('加载网点不一致记录失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function onSearch(external?: Record<string, any>) {
  if (external) {
    if ('batchId' in external) query.batchId = external.batchId
    if ('waybillNo' in external) query.waybillNo = external.waybillNo
  }
  pagination.current = 1
  loadList()
}

function onReset() {
  query.batchId = undefined
  query.waybillNo = undefined
  pagination.current = 1
  loadList()
}

function onTableChange(pg: any) {
  pagination.current = pg.current
  pagination.pageSize = pg.pageSize
  loadList()
}

// ==================== 行选择 ====================
const selectedRowKeys = ref<number[]>([])

function onSelectChange(keys: Key[]) {
  selectedRowKeys.value = keys.map(Number)
}

// ==================== 批量忽略 ====================
function onIgnore() {
  Modal.confirm({
    title: '批量忽略',
    content: `确定忽略已选的 ${selectedRowKeys.value.length} 条网点不一致记录吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      try {
        const res = (await ignoreMismatchErrors(selectedRowKeys.value)) as any
        message.success(res?.message || `成功忽略 ${res?.affectedCount || 0} 条`)
        selectedRowKeys.value = []
        loadList()
      } catch (e: any) {
        message.error('忽略失败：' + (e?.message || '未知错误'))
      }
    },
  })
}

onMounted(() => {
  loadList()
})

defineExpose({ onSearch, onReset, query })
</script>

<style scoped>
.page-container { padding: 0; }
</style>
