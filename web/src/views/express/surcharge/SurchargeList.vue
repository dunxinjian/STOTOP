<template>
  <div class="page-container">
    <PageHeader title="出港加收管理">
      <template #actions>
        <a-select v-model:value="searchForm.surchargeType" placeholder="加收类型" allow-clear style="width: 140px"
          :options="surchargeTypeOptions" />
        <a-select v-model:value="searchForm.scope" placeholder="作用域" allow-clear style="width: 120px"
          :options="scopeOptions" />
        <a-select v-model:value="searchForm.isActive" placeholder="启用状态" allow-clear style="width: 120px"
          :options="activeOptions" />
        <a-button type="primary" @click="handleSearch">查询</a-button>
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增加收
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'surchargeType'">
            <a-tag :color="surchargeTypeColor(record.surchargeType)">
              {{ surchargeTypeMap[record.surchargeType] ?? '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'scope'">
            <a-tag :color="scopeColorMap[record.scope]">
              {{ scopeMap[record.scope] ?? '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'effectiveDate'">
            {{ record.effectiveDate?.slice(0, 10) ?? '-' }}
          </template>
          <template v-if="column.dataIndex === 'isActive'">
            <a-tag :color="record.isActive ? 'green' : 'default'">
              {{ record.isActive ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as PriceSurchargeListItem)">编辑</a-button>
            <a-popconfirm :title="record.isActive ? '确定停用此加收方案？' : '确定启用此加收方案？'" @confirm="handleToggleActive(record as PriceSurchargeListItem)">
              <a-button type="link" size="small">{{ record.isActive ? '停用' : '启用' }}</a-button>
            </a-popconfirm>
            <a-popconfirm title="确定删除此加收方案？" @confirm="handleDelete(record as PriceSurchargeListItem)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPriceSurcharges,
  deletePriceSurcharge,
  togglePriceSurchargeActive,
  type PriceSurchargeListItem,
} from '@/api/express'

const router = useRouter()

// 加收类型映射
const surchargeTypeMap: Record<string, string> = {
  '1': '电商大促',
  '2': '春节涨价',
  '3': '目的地加收',
  '4': '周期性加收',
  '5': '拦截费',
  '6': '单量加收',
  '7': '其他',
}

const surchargeTypeOptions = Object.entries(surchargeTypeMap).map(([k, v]) => ({ label: v, value: k }))

const scopeMap: Record<number, string> = { 0: '全局', 1: '业务对象级', 2: '报价级' }
const scopeColorMap: Record<number, string> = { 0: 'blue', 1: 'orange', 2: 'purple' }
const scopeOptions = Object.entries(scopeMap).map(([k, v]) => ({ label: v, value: Number(k) }))

const activeOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

function surchargeTypeColor(t: string) {
  const colors: Record<string, string> = { '1': 'red', '2': 'orange', '3': 'blue', '4': 'purple', '5': 'volcano', '6': 'cyan', '7': 'default' }
  return colors[t] ?? 'default'
}

// 搜索
const searchForm = reactive({
  surchargeType: undefined as string | undefined,
  scope: undefined as number | undefined,
  isActive: undefined as number | undefined,
})

// 表格
const loading = ref(false)
const tableData = ref<PriceSurchargeListItem[]>([])
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
  { title: '加收类型', dataIndex: 'surchargeType', width: 120, align: 'center' as const },
  { title: '作用域', dataIndex: 'scope', width: 120, align: 'center' as const },
  { title: '品牌', dataIndex: 'brandCode', width: 80, align: 'center' as const },
  { title: '网点', dataIndex: 'networkPointCode', width: 100 },
  { title: '生效日期', dataIndex: 'effectiveDate', width: 110 },
  { title: '状态', dataIndex: 'isActive', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const res = await getPriceSurcharges({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      surchargeType: searchForm.surchargeType,
      scope: searchForm.scope,
      isActive: searchForm.isActive === undefined ? undefined : searchForm.isActive === 1,
    })
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取加收列表失败')
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
  searchForm.surchargeType = undefined
  searchForm.scope = undefined
  searchForm.isActive = undefined
  handleSearch()
}

function handleAdd() {
  router.push('/express/surcharge/create')
}

function handleEdit(row: PriceSurchargeListItem) {
  router.push(`/express/surcharge/edit/${row.id}`)
}

async function handleToggleActive(row: PriceSurchargeListItem) {
  try {
    await togglePriceSurchargeActive(row.id)
    message.success(row.isActive ? '已停用' : '已启用')
    fetchList()
  } catch { /* handled */ }
}

async function handleDelete(row: PriceSurchargeListItem) {
  try {
    await deletePriceSurcharge(row.id)
    message.success('删除成功')
    fetchList()
  } catch { /* handled */ }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
