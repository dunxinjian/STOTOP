<template>
  <div class="page-container">
    <PageHeader title="号段池管理" description="管理运单编号段采购与分配">
      <template #actions>
        <a-button v-if="has(CrmPermissions.WaybillPoolManage)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>采购录入
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.brandCode" placeholder="全部品牌" allow-clear size="small" style="width: 140px" />
          <a-select v-model:value="searchForm.status" placeholder="全部" allow-clear size="small" style="width: 120px" :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 统计卡片 -->
    <a-row :gutter="16" class="stat-cards">
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="总号段数" :value="summaryStats.totalPools" suffix="个" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="总量" :value="summaryStats.totalCount" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="已发放" :value="summaryStats.allocatedCount" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="剩余总量" :value="summaryStats.remainingCount" /></a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1300 }"
        :row-class-name="getRowClassName"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'remainingCount'">
            <span :class="{ 'low-stock': isLowStock(record as WaybillPoolDto) }">
              {{ record.remainingCount }}
              <a-tag v-if="isLowStock(record as WaybillPoolDto)" color="error" style="margin-left: 4px">低库存</a-tag>
            </span>
          </template>
          <template v-if="column.dataIndex === 'unitPrice'">
            ¥{{ record.unitPrice?.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="statusColorMap[record.status] || 'default'">
              {{ statusTextMap[record.status] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="record.status === 1" type="link" size="small" @click="handleEdit(record as WaybillPoolDto)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm v-if="record.status === 1" title="确定作废该号段吗？" ok-text="确定" cancel-text="取消" @confirm="handleVoid(record as WaybillPoolDto)">
              <a-button type="link" size="small" danger>
                <StopOutlined />作废
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无号段数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 采购录入 Modal -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalType === 'add' ? '采购录入' : '编辑号段'"
      width="700px"
      :destroy-on-close="true"
      @cancel="modalVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="品牌" name="brandCode">
              <a-select v-model:value="formData.brandCode" placeholder="请选择品牌" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="号段前缀">
              <a-input v-model:value="formData.prefix" placeholder="请输入号段前缀" :maxlength="20" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="起始号" name="startNo">
              <a-input v-model:value="formData.startNo" placeholder="请输入起始号" :maxlength="30" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结束号" name="endNo">
              <a-input v-model:value="formData.endNo" placeholder="请输入结束号" :maxlength="30" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="单价" name="unitPrice">
              <a-input-number v-model:value="formData.unitPrice" :min="0" :precision="2" prefix="¥" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="采购日期">
              <a-date-picker v-model:value="formData.purchaseDate" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="modalVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, StopOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { CrmPermissions, usePermission } from '@/utils/permission'
import {
  getWaybillPoolList, createWaybillPool, deleteWaybillPool,
  type WaybillPoolDto,
} from '@/api/crm'

const { has } = usePermission()

const statusOptions = [
  { label: '可用', value: 1 },
  { label: '已用完', value: 2 },
  { label: '已作废', value: 3 },
]

const statusColorMap: Record<number, string> = { 1: 'success', 2: 'default', 3: 'error' }
const statusTextMap: Record<number, string> = { 1: '可用', 2: '已用完', 3: '已作废' }

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '品牌', dataIndex: 'brandCode', width: 100 },
  { title: '号段前缀', dataIndex: 'prefix', width: 120 },
  { title: '起始号', dataIndex: 'startNo', width: 140 },
  { title: '结束号', dataIndex: 'endNo', width: 140 },
  { title: '总量', dataIndex: 'totalCount', width: 80, align: 'right' as const },
  { title: '已发放', dataIndex: 'allocatedCount', width: 80, align: 'right' as const },
  { title: '剩余', dataIndex: 'remainingCount', width: 120, align: 'right' as const },
  { title: '单价', dataIndex: 'unitPrice', width: 100, align: 'right' as const },
  { title: '采购日期', dataIndex: 'purchaseDate', width: 120 },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

// 搜索
const searchForm = reactive({ brandCode: undefined as string | undefined, status: undefined as number | undefined })
const loading = ref(false)
const tableData = ref<WaybillPoolDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

// 汇总
const summaryStats = computed(() => {
  const data = tableData.value
  return {
    totalPools: pagination.total || data.length,
    totalCount: data.reduce((s, r) => s + (r.totalCount || 0), 0),
    allocatedCount: data.reduce((s, r) => s + (r.allocatedCount || 0), 0),
    remainingCount: data.reduce((s, r) => s + (r.remainingCount || 0), 0),
  }
})

function isLowStock(record: WaybillPoolDto) {
  return record.totalCount > 0 && record.remainingCount < record.totalCount * 0.1
}

function getRowClassName(record: WaybillPoolDto) {
  return isLowStock(record) ? 'low-stock-row' : ''
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.brandCode !== undefined) params.brandCode = searchForm.brandCode
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getWaybillPoolList(params) as any
    tableData.value = res?.items || res || []
    pagination.total = res?.total || 0
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() { searchForm.brandCode = undefined; searchForm.status = undefined; handleSearch() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

// 弹窗
const modalVisible = ref(false)
const modalType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  brandCode: undefined as string | undefined,
  prefix: '',
  startNo: '',
  endNo: '',
  unitPrice: undefined as number | undefined,
  purchaseDate: null as any,
})

const formRules: Record<string, Rule[]> = {
  brandCode: [{ required: true, message: '请选择品牌', trigger: 'change' }],
  startNo: [{ required: true, message: '请输入起始号', trigger: 'blur' }],
  endNo: [{ required: true, message: '请输入结束号', trigger: 'blur' }],
  unitPrice: [{ required: true, message: '请输入单价', trigger: 'blur' }],
}

function handleAdd() {
  modalType.value = 'add'
  currentId.value = null
  Object.assign(formData, { brandCode: undefined, prefix: '', startNo: '', endNo: '', unitPrice: undefined, purchaseDate: null })
  modalVisible.value = true
}

function handleEdit(record: WaybillPoolDto) {
  modalType.value = 'edit'
  currentId.value = record.id
  Object.assign(formData, { brandCode: record.brandCode, prefix: record.prefix || '', startNo: record.startNo, endNo: record.endNo, unitPrice: record.unitPrice, purchaseDate: record.purchaseDate })
  modalVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const startNum = parseInt(formData.startNo.replace(/\D/g, '') || '0')
    const endNum = parseInt(formData.endNo.replace(/\D/g, '') || '0')
    const totalCount = endNum - startNum + 1
    const data = {
      brandCode: formData.brandCode!,
      prefix: formData.prefix || undefined,
      startNo: formData.startNo,
      endNo: formData.endNo,
      totalCount: totalCount > 0 ? totalCount : 0,
      unitPrice: formData.unitPrice!,
      purchaseDate: typeof formData.purchaseDate === 'string' ? formData.purchaseDate : formData.purchaseDate?.format('YYYY-MM-DD'),
    }
    await createWaybillPool(data)
    message.success(modalType.value === 'add' ? '录入成功' : '更新成功')
    modalVisible.value = false
    fetchList()
  } finally { submitLoading.value = false }
}

async function handleVoid(record: WaybillPoolDto) {
  try { await deleteWaybillPool(record.id); message.success('作废成功'); fetchList() } catch { /* handled */ }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
.stat-cards {
  margin-top: 16px;
}

.low-stock {
  color: var(--color-danger);
  font-weight: 600;
}

:deep(.low-stock-row) {
  background-color: var(--color-danger-light) !important;
}
</style>
