<template>
  <div class="page-container">
    <PageHeader title="预付款管理" description="管理客户预付款与运单编号发放">
      <template #actions>
        <a-button v-if="has(CrmPermissions.PrepaymentCreate)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建预付款
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; width:100%; justify-content:space-between;">
          <a-radio-group v-model:value="statusFilter" button-style="solid" size="small" @change="handleSearch">
            <a-radio-button :value="undefined">全部</a-radio-button>
            <a-radio-button :value="0">待付款</a-radio-button>
            <a-radio-button :value="1">部分到账</a-radio-button>
            <a-radio-button :value="2">已到账</a-radio-button>
            <a-radio-button :value="3">已发放</a-radio-button>
            <a-radio-button :value="4">已关闭</a-radio-button>
          </a-radio-group>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-input v-model:value="searchForm.keyword" placeholder="客户名称" allow-clear size="small" style="width: 160px" @keyup.enter="handleSearch" />
            <a-select v-model:value="searchForm.brandCode" placeholder="全部品牌" allow-clear size="small" style="width: 120px" />
            <a-range-picker v-model:value="searchForm.dateRange" size="small" style="width: 220px" />
            <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
            <a-button size="small" @click="handleReset">重置</a-button>
          </div>
        </div>
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
          <template v-if="column.dataIndex === 'prepayAmount'">
            ¥{{ record.prepayAmount?.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'receivedAmount'">
            ¥{{ record.receivedAmount?.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="prepayStatusColorMap[record.status] || 'default'">
              {{ prepayStatusTextMap[record.status] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewDetail(record as PrepaymentDto)">
              <EyeOutlined />查看
            </a-button>
            <a-button
              v-if="record.status === 2 && has(CrmPermissions.PrepaymentAllocate)"
              type="link" size="small"
              @click="handleAllocate(record as PrepaymentDto)"
            >
              <SendOutlined />发放运单编号
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无预付款数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新建预付款 Modal -->
    <a-modal
      v-model:open="modalVisible"
      title="新建预付款"
      width="600px"
      :destroy-on-close="true"
      @cancel="modalVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="客户" name="customerId">
          <a-select v-model:value="formData.customerId" placeholder="请选择客户" show-search :filter-option="false" style="width: 100%" />
        </a-form-item>
        <a-form-item label="品牌" name="brandCode">
          <a-select v-model:value="formData.brandCode" placeholder="请选择品牌" style="width: 100%" />
        </a-form-item>
        <a-form-item label="预付金额" name="prepayAmount">
          <a-input-number v-model:value="formData.prepayAmount" :min="0" :precision="2" prefix="¥" style="width: 100%" />
        </a-form-item>
        <a-form-item label="应发运单数" name="expectedWaybillCount">
          <a-input-number v-model:value="formData.expectedWaybillCount" :min="0" :precision="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="formData.remark" :rows="3" placeholder="请输入备注" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="modalVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 详情 Drawer -->
    <a-drawer v-model:open="detailDrawerVisible" title="预付款详情" width="700" :destroy-on-close="true">
      <template v-if="currentDetail">
        <!-- 基本信息 -->
        <a-descriptions title="基本信息" :column="2" bordered size="small">
          <a-descriptions-item label="客户">{{ currentDetail.customerName || currentDetail.customerId }}</a-descriptions-item>
          <a-descriptions-item label="品牌">{{ currentDetail.brandCode }}</a-descriptions-item>
          <a-descriptions-item label="预付金额">¥{{ currentDetail.prepayAmount?.toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="到账金额">¥{{ currentDetail.receivedAmount?.toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="应发运单数">{{ currentDetail.expectedWaybillCount }}</a-descriptions-item>
          <a-descriptions-item label="已发运单数">{{ currentDetail.allocatedWaybillCount }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="prepayStatusColorMap[currentDetail.status]">{{ prepayStatusTextMap[currentDetail.status] }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="银行流水" v-if="currentDetail.bankTransactionId">
            已匹配 (ID: {{ currentDetail.bankTransactionId }})
          </a-descriptions-item>
          <a-descriptions-item label="备注" :span="2">{{ currentDetail.remark || '-' }}</a-descriptions-item>
        </a-descriptions>

        <!-- 发放历史 -->
        <div class="detail-section-title">
          发放历史
          <a-button
            v-if="currentDetail.status === 2 && has(CrmPermissions.PrepaymentAllocate)"
            type="primary" size="small"
            @click="handleAllocate(currentDetail)"
          >
            发放运单编号
          </a-button>
        </div>
        <a-table
          :columns="allocationColumns"
          :data-source="allocationData"
          :loading="allocationLoading"
          :pagination="false"
          row-key="id"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'range'">
              {{ record.startNo }} - {{ record.endNo }}
            </template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '有效' : '已回收' }}</a-tag>
            </template>
          </template>
          <template #emptyText>
            <EmptyState description="暂无发放记录" />
          </template>
        </a-table>
      </template>
    </a-drawer>

    <!-- 发放运单编号 Modal -->
    <a-modal
      v-model:open="allocateModalVisible"
      title="发放运单编号"
      width="500px"
      :destroy-on-close="true"
      @cancel="allocateModalVisible = false"
    >
      <a-form ref="allocateFormRef" :model="allocateForm" :rules="allocateFormRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="号段池" name="poolId">
          <a-select v-model:value="allocateForm.poolId" placeholder="请选择号段池" style="width: 100%" />
        </a-form-item>
        <a-form-item label="发放数量" name="count">
          <a-input-number v-model:value="allocateForm.count" :min="1" :precision="0" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="allocateModalVisible = false">取消</a-button>
        <a-button type="primary" :loading="allocateSubmitLoading" @click="handleSubmitAllocate">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EyeOutlined, SendOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { CrmPermissions, usePermission } from '@/utils/permission'
import {
  getPrepaymentList, createPrepayment, getPrepaymentById,
  allocateWaybill, getCustomerAllocations,
  type PrepaymentDto, type WaybillAllocationDto,
} from '@/api/crm'

const { has } = usePermission()

const prepayStatusColorMap: Record<number, string> = { 0: 'default', 1: 'warning', 2: 'success', 3: 'cyan', 4: 'error' }
const prepayStatusTextMap: Record<number, string> = { 0: '待付款', 1: '部分到账', 2: '已到账', 3: '已发放', 4: '已关闭' }

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '客户名称', dataIndex: 'customerId', width: 150, ellipsis: true },
  { title: '品牌', dataIndex: 'brandCode', width: 100 },
  { title: '预付金额', dataIndex: 'prepayAmount', width: 120, align: 'right' as const },
  { title: '到账金额', dataIndex: 'receivedAmount', width: 120, align: 'right' as const },
  { title: '应发运单数', dataIndex: 'expectedWaybillCount', width: 100, align: 'right' as const },
  { title: '已发运单数', dataIndex: 'allocatedWaybillCount', width: 100, align: 'right' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

const allocationColumns = [
  { title: '号段池', dataIndex: 'poolId', width: 80 },
  { title: '号段范围', dataIndex: 'range', width: 200 },
  { title: '数量', dataIndex: 'allocatedCount', width: 80, align: 'right' as const },
  { title: '发放日期', dataIndex: 'allocationDate', width: 120 },
  { title: '操作人', dataIndex: 'creatorName', width: 100 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
]

// 搜索
const statusFilter = ref<number | undefined>(undefined)
const searchForm = reactive({ keyword: '', brandCode: undefined as string | undefined, dateRange: null as any })
const loading = ref(false)
const tableData = ref<PrepaymentDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (statusFilter.value !== undefined) params.status = statusFilter.value
    if (searchForm.brandCode !== undefined) params.brandCode = searchForm.brandCode
    if (searchForm.dateRange?.[0]) params.startDate = searchForm.dateRange[0].format('YYYY-MM-DD')
    if (searchForm.dateRange?.[1]) params.endDate = searchForm.dateRange[1].format('YYYY-MM-DD')
    const res = await getPrepaymentList(params) as any
    tableData.value = res?.items || res || []
    pagination.total = res?.total || 0
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() { searchForm.keyword = ''; searchForm.brandCode = undefined; searchForm.dateRange = null; statusFilter.value = undefined; handleSearch() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

// 新建预付款
const modalVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const formData = reactive({
  customerId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  prepayAmount: undefined as number | undefined,
  expectedWaybillCount: undefined as number | undefined,
  remark: '',
})
const formRules: Record<string, Rule[]> = {
  customerId: [{ required: true, message: '请选择客户', trigger: 'change' }],
  brandCode: [{ required: true, message: '请选择品牌', trigger: 'change' }],
  prepayAmount: [{ required: true, message: '请输入预付金额', trigger: 'blur' }],
  expectedWaybillCount: [{ required: true, message: '请输入应发运单数', trigger: 'blur' }],
}

function handleAdd() {
  Object.assign(formData, { customerId: undefined, brandCode: undefined, prepayAmount: undefined, expectedWaybillCount: undefined, remark: '' })
  modalVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    await createPrepayment({
      customerId: formData.customerId!,
      customerAccountId: 0,
      brandCode: formData.brandCode!,
      prepayAmount: formData.prepayAmount!,
      expectedWaybillCount: formData.expectedWaybillCount!,
      remark: formData.remark || undefined,
    })
    message.success('创建成功')
    modalVisible.value = false
    fetchList()
  } finally { submitLoading.value = false }
}

// 详情
const detailDrawerVisible = ref(false)
const currentDetail = ref<(PrepaymentDto & { customerName?: string }) | null>(null)
const allocationData = ref<WaybillAllocationDto[]>([])
const allocationLoading = ref(false)

async function handleViewDetail(record: PrepaymentDto) {
  detailDrawerVisible.value = true
  try {
    const detail = await getPrepaymentById(record.id) as any
    currentDetail.value = detail || record
  } catch { currentDetail.value = record }
  fetchAllocations(record.customerId)
}

async function fetchAllocations(customerId: number) {
  allocationLoading.value = true
  try {
    const res = await getCustomerAllocations(customerId) as any
    allocationData.value = res?.items || res || []
  } finally { allocationLoading.value = false }
}

// 发放运单编号
const allocateModalVisible = ref(false)
const allocateFormRef = ref<FormInstance>()
const allocateSubmitLoading = ref(false)
const currentPrepaymentForAllocate = ref<PrepaymentDto | null>(null)
const allocateForm = reactive({ poolId: undefined as number | undefined, count: undefined as number | undefined })
const allocateFormRules: Record<string, Rule[]> = {
  poolId: [{ required: true, message: '请选择号段池', trigger: 'change' }],
  count: [{ required: true, message: '请输入发放数量', trigger: 'blur' }],
}

function handleAllocate(record: PrepaymentDto) {
  currentPrepaymentForAllocate.value = record
  Object.assign(allocateForm, { poolId: undefined, count: undefined })
  allocateModalVisible.value = true
}

async function handleSubmitAllocate() {
  if (!allocateFormRef.value) return
  try { await allocateFormRef.value.validate() } catch { return }
  allocateSubmitLoading.value = true
  try {
    await allocateWaybill({
      prepaymentId: currentPrepaymentForAllocate.value!.id,
      customerId: currentPrepaymentForAllocate.value!.customerId,
      poolId: allocateForm.poolId!,
      count: allocateForm.count!,
      operatorId: 0,
    })
    message.success('发放成功')
    allocateModalVisible.value = false
    fetchList()
    if (detailDrawerVisible.value && currentDetail.value) {
      handleViewDetail(currentDetail.value)
    }
  } finally { allocateSubmitLoading.value = false }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
.detail-section-title {
  font-size: 15px;
  font-weight: 600;
  margin: 24px 0 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
