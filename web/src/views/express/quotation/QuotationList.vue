<template>
  <div class="page-container">
    <PageHeader title="快递报价">
      <template #actions>
        <a-select v-model:value="searchForm.status" placeholder="状态" allow-clear style="width: 120px"
          :options="statusFilterOptions" @change="handleSearch" />
        <a-select v-model:value="searchForm.clientType" placeholder="业务对象类型" allow-clear style="width: 140px"
          :options="clientTypeOptions" @change="handleSearch" />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button @click="importModalVisible = true">
          <template #icon><UploadOutlined /></template>导入Excel
        </a-button>
        <a-button type="primary" @click="router.push('/express/quotation/create')">
          <template #icon><PlusOutlined /></template>新建报价
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="dataSource"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'planName'">
            <a class="plan-name-link" @click="router.push(`/express/quotation/edit/${record.id}`)">
              {{ record.planName }}
            </a>
          </template>
          <template v-if="column.dataIndex === 'shopCount'">
            <span :style="{ color: record.shopCount > 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }">
              {{ record.shopCount ?? 0 }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'clientType'">
            <a-tag>{{ getClientTypeLabel(record.clientType) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'sharedShopEnabled'">
            <a-tag :color="record.sharedShopEnabled ? 'green' : 'default'">
              {{ record.sharedShopEnabled ? '已开启' : '未开启' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="router.push(`/express/quotation/edit/${record.id}`)">编辑</a-button>
            <a-button type="link" size="small" @click="handleCopy(record as QuotationListItemDto)">复制</a-button>
            <a-popconfirm title="确定删除此报价？" @confirm="handleDelete(record as QuotationListItemDto)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <ImportQuotationModal v-model:visible="importModalVisible" @success="fetchList" />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined, UploadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import ImportQuotationModal from './ImportQuotationModal.vue'
import {
  getQuotationList,
  copyQuotation,
  deleteQuotation,
  type QuotationListItemDto,
} from '@/api/express'

const router = useRouter()

// 导入弹窗
const importModalVisible = ref(false)

// 业务对象类型选项
const clientTypeOptions = [
  { value: 'KH', label: '客户' },
  { value: 'DL', label: '代理' },
  { value: 'WD', label: '网点' },
  { value: 'YW', label: '业务员' },
  { value: 'CB', label: '承包区' },
  { value: 'YZ', label: '驿站' },
]

function getClientTypeLabel(type: string) {
  return clientTypeOptions.find(o => o.value === type)?.label ?? type
}

// 搜索
const searchForm = reactive({
  status: undefined as number | undefined,
  clientType: undefined as string | undefined,
})

// 与后端 ExpQuotation.FStatus 对齐：0草稿 1生效 2过期 3作废
const statusOptions = [
  { value: 0, label: '草稿', color: 'default' },
  { value: 1, label: '生效', color: 'success' },
  { value: 2, label: '过期', color: 'warning' },
  { value: 3, label: '作废', color: 'error' },
]

const statusFilterOptions = statusOptions.map(s => ({ label: s.label, value: s.value }))

function getStatusText(s: number) { return statusOptions.find(o => o.value === s)?.label ?? '未知' }
function getStatusColor(s: number) { return statusOptions.find(o => o.value === s)?.color ?? 'default' }

// 表格
const loading = ref(false)
const dataSource = ref<QuotationListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '报价名称', dataIndex: 'planName', width: 200, ellipsis: true },
  { title: '店铺数', dataIndex: 'shopCount', width: 80, align: 'center' as const },
  { title: '方案编号', dataIndex: 'planCode', width: 160, ellipsis: true },
  { title: '业务对象类型', dataIndex: 'clientType', width: 120, align: 'center' as const },
  { title: '业务对象ID', dataIndex: 'clientId', width: 120, ellipsis: true },
  { title: '网点编号', dataIndex: 'networkPointCode', width: 120, ellipsis: true },
  { title: '共享店铺', dataIndex: 'sharedShopEnabled', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const res = await getQuotationList({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      ...searchForm,
    })
    dataSource.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取报价列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.status = undefined
  searchForm.clientType = undefined
  handleSearch()
}

// 复制
async function handleCopy(row: QuotationListItemDto) {
  try {
    await copyQuotation(row.id)
    message.success('复制成功')
    fetchList()
  } catch { /* handled */ }
}

// 删除
async function handleDelete(row: QuotationListItemDto) {
  try {
    await deleteQuotation(row.id)
    message.success('删除成功')
    fetchList()
  } catch { /* handled */ }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.plan-name-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}
</style>
