<template>
  <div class="page-container">
    <PageHeader title="快递成本方案">
      <template #actions>
        <a-select v-model:value="searchForm.brandCode" placeholder="品牌" allow-clear style="width: 120px"
          :options="brandOptions" @change="handleSearch" />
        <a-select v-model:value="searchForm.status" placeholder="状态" allow-clear style="width: 120px"
          :options="statusFilterOptions" @change="handleSearch" />
        <a-input-search v-model:value="searchForm.keyword" placeholder="搜索方案名称" allow-clear
          style="width: 200px" @search="handleSearch" @pressEnter="handleSearch" />
        <a-button @click="handleReset">重置</a-button>
        <a-button v-if="canCreate" type="primary" @click="openCreateModal">
          <template #icon><PlusOutlined /></template>新建方案
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
        :scroll="{ x: 900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'planName'">
            <a class="plan-name-link" @click="router.push(`/express/cost-plan/edit/${record.id}`)">
              {{ record.planName }}
            </a>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') ?? '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="canEdit" type="link" size="small" @click="router.push(`/express/cost-plan/edit/${record.id}`)">编辑</a-button>
            <a-popconfirm v-if="record.status === 0 && canEdit" title="确定启用此方案？" @confirm="handleActivate(record as CostPlanListItem)">
              <a-button type="link" size="small">启用</a-button>
            </a-popconfirm>
            <a-popconfirm v-if="record.status === 1 && canEdit" title="确定停用此方案？" @confirm="handleDeactivate(record as CostPlanListItem)">
              <a-button type="link" size="small">停用</a-button>
            </a-popconfirm>
            <a-popconfirm v-if="record.status !== 1 && canDelete" title="确定删除此方案？" @confirm="handleDelete(record as CostPlanListItem)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新建方案弹窗 -->
    <a-modal
      v-model:open="createModalVisible"
      title="新建成本方案"
      ok-text="确定"
      cancel-text="取消"
      :confirm-loading="createLoading"
      @ok="handleCreateConfirm"
    >
      <a-form :label-col="{ span: 5 }" style="margin-top: 16px;">
        <a-form-item label="品牌" required>
          <a-select
            v-model:value="createForm.brandCode"
            placeholder="请选择品牌"
            :options="brandOptions"
            show-search
            :filter-option="filterByLabel"
            allow-clear
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="方案名称" required>
          <a-input v-model:value="createForm.planName" placeholder="例如：申通城区2026Q2成本方案" allow-clear />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { usePermission, ExpressPermissions } from '@/utils/permission'
import {
  getCostPlanList,
  activateCostPlan,
  deactivateCostPlan,
  deleteCostPlan,
  createCostPlan,
  getExpBrandOptions,
  type CostPlanListItem,
} from '@/api/express'

const router = useRouter()

// ==================== 按钮级权限 ====================
const { has } = usePermission()
const canCreate = computed(() => has(ExpressPermissions.CostPlanCreate))
const canEdit = computed(() => has(ExpressPermissions.CostPlanEdit))
const canDelete = computed(() => has(ExpressPermissions.CostPlanDelete))

// ==================== 品牌选项 ====================
const brandOptions = ref<{ label: string; value: string }[]>([])

async function fetchBrandOptions() {
  try {
    const res = await getExpBrandOptions()
    brandOptions.value = res.items.map(b => ({ label: b.name, value: b.code }))
  } catch { /* ignore */ }
}

function filterByLabel(input: string, option: any) {
  return (option?.label ?? '').toString().toLowerCase().includes(String(input).toLowerCase())
}

// ==================== 状态映射 ====================
const statusOptions = [
  { value: 0, label: '草稿', color: 'default' },
  { value: 1, label: '启用', color: 'green' },
  { value: 2, label: '停用', color: 'red' },
]

const statusFilterOptions = statusOptions.map(s => ({ label: s.label, value: s.value }))

function getStatusText(s: number) { return statusOptions.find(o => o.value === s)?.label ?? '未知' }
function getStatusColor(s: number) { return statusOptions.find(o => o.value === s)?.color ?? 'default' }

// ==================== 搜索 ====================
const searchForm = reactive({
  brandCode: undefined as string | undefined,
  status: undefined as number | undefined,
  keyword: undefined as string | undefined,
})

// ==================== 表格 ====================
const loading = ref(false)
const tableData = ref<CostPlanListItem[]>([])
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
  { title: '方案名称', dataIndex: 'planName', width: 220, ellipsis: true },
  { title: '品牌', dataIndex: 'brandName', width: 100, align: 'center' as const },
  { title: '成本项数', dataIndex: 'itemCount', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 90, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', width: 160 },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const res = await getCostPlanList({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      brandCode: searchForm.brandCode,
      status: searchForm.status,
      keyword: searchForm.keyword,
    })
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取成本方案列表失败')
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
  searchForm.brandCode = undefined
  searchForm.status = undefined
  searchForm.keyword = undefined
  handleSearch()
}

// ==================== 操作 ====================
async function handleActivate(record: CostPlanListItem) {
  try {
    await activateCostPlan(record.id)
    message.success('启用成功')
    fetchList()
  } catch { /* handled */ }
}

async function handleDeactivate(record: CostPlanListItem) {
  try {
    await deactivateCostPlan(record.id)
    message.success('已停用')
    fetchList()
  } catch { /* handled */ }
}

async function handleDelete(record: CostPlanListItem) {
  try {
    await deleteCostPlan(record.id)
    message.success('删除成功')
    fetchList()
  } catch { /* handled */ }
}

// ==================== 新建弹窗 ====================
const createModalVisible = ref(false)
const createLoading = ref(false)
const createForm = reactive({
  brandCode: undefined as string | undefined,
  planName: undefined as string | undefined,
})

function openCreateModal() {
  createForm.brandCode = undefined
  createForm.planName = undefined
  createModalVisible.value = true
}

async function handleCreateConfirm() {
  if (!createForm.brandCode) {
    message.warning('请选择品牌')
    return
  }
  if (!createForm.planName) {
    message.warning('请输入方案名称')
    return
  }
  createLoading.value = true
  try {
    const res = await createCostPlan({
      brandCode: createForm.brandCode,
      planName: createForm.planName,
    })
    message.success('创建成功')
    createModalVisible.value = false
    const newId = res?.id
    if (newId) {
      router.push(`/express/cost-plan/edit/${newId}`)
    } else {
      fetchList()
    }
  } catch {
    // 后端业务错误已由响应拦截器提示，避免二次泛化 toast
  } finally {
    createLoading.value = false
  }
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchBrandOptions()
  fetchList()
})
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
