<template>
  <div class="page-container">
    <PageHeader title="奖金管理" description="管理奖金方案与分配明细">
      <template #actions>
        <a-button v-if="has(CrmPermissions.BonusManage)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建方案
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-date-picker v-model:value="searchForm.period" picker="month" placeholder="选择期间" size="small" style="width: 160px" />
          <a-select v-model:value="searchForm.orgId" placeholder="全部组织" allow-clear size="small" style="width: 160px" />
          <a-select v-model:value="searchForm.status" placeholder="全部" allow-clear size="small" style="width: 120px" :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
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
        :scroll="{ x: 900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'totalAmount'">
            ¥{{ record.totalAmount?.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="statusColorMap[record.status] || 'default'">
              {{ statusTextMap[record.status] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="record.status === 0" type="link" size="small" @click="handleEdit(record as BonusPlanDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button type="link" size="small" @click="handleViewDetail(record as BonusPlanDto)">
              <EyeOutlined />查看明细
            </a-button>
            <a-button v-if="record.status === 0" type="link" size="small" @click="handleSubmitApproval(record as BonusPlanDto)">
              <SendOutlined />提交审批
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无奖金方案" />
        </template>
      </a-table>
    </a-card>

    <!-- 新建/编辑方案 Modal -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalType === 'add' ? '新建方案' : '编辑方案'"
      width="600px"
      :destroy-on-close="true"
      @cancel="modalVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="组织" name="orgId">
          <a-select v-model:value="formData.orgId" placeholder="请选择组织" style="width: 100%" />
        </a-form-item>
        <a-form-item label="期间" name="period">
          <a-date-picker v-model:value="formData.period" picker="month" style="width: 100%" />
        </a-form-item>
        <a-form-item label="奖金总额" name="totalAmount">
          <a-input-number v-model:value="formData.totalAmount" :min="0" :precision="2" prefix="¥" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="modalVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 奖金明细 Drawer -->
    <a-drawer v-model:open="detailDrawerVisible" title="奖金明细" width="800" :destroy-on-close="true">
      <template v-if="currentPlan">
        <!-- 方案基本信息 -->
        <a-descriptions title="方案信息" :column="3" bordered size="small">
          <a-descriptions-item label="组织">{{ currentPlan.orgId || '-' }}</a-descriptions-item>
          <a-descriptions-item label="期间">{{ currentPlan.period }}</a-descriptions-item>
          <a-descriptions-item label="奖金总额">¥{{ currentPlan.totalAmount?.toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="statusColorMap[currentPlan.status]">{{ statusTextMap[currentPlan.status] }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="创建人">{{ currentPlan.creatorName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ currentPlan.createdTime }}</a-descriptions-item>
        </a-descriptions>

        <!-- 明细列表 -->
        <div class="detail-section-title">
          奖金明细
          <a-space v-if="currentPlan.status === 0">
            <a-button type="primary" size="small" @click="handleAddDetail">
              <template #icon><PlusOutlined /></template>添加明细
            </a-button>
            <a-button size="small" @click="handleSaveDetails" :loading="saveDetailLoading">
              <template #icon><SaveOutlined /></template>保存明细
            </a-button>
          </a-space>
        </div>
        <a-table
          :columns="detailColumns"
          :data-source="detailData"
          :loading="detailLoading"
          :pagination="false"
          row-key="id"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'employeeId'">
              <template v-if="currentPlan.status === 0 && record._editing">
                <a-select v-model:value="record.employeeId" placeholder="选择员工" size="small" style="width: 100%" show-search :filter-option="false" />
              </template>
              <template v-else>{{ record.employeeName || record.employeeId }}</template>
            </template>
            <template v-if="column.dataIndex === 'bonusType'">
              <template v-if="currentPlan.status === 0 && record._editing">
                <a-select v-model:value="record.bonusType" size="small" style="width: 100%" :options="bonusTypeOptions" />
              </template>
              <template v-else>{{ bonusTypeTextMap[record.bonusType] || '未知' }}</template>
            </template>
            <template v-if="column.dataIndex === 'amount'">
              <template v-if="currentPlan.status === 0 && record._editing">
                <a-input-number v-model:value="record.amount" :min="0" :precision="2" size="small" style="width: 100%" />
              </template>
              <template v-else>¥{{ record.amount?.toFixed(2) }}</template>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-button v-if="currentPlan.status === 0" type="link" size="small" @click="record._editing = !record._editing">
                {{ record._editing ? '完成' : '编辑' }}
              </a-button>
              <a-popconfirm v-if="currentPlan.status === 0" title="确定删除？" @confirm="handleDeleteDetail(record as BonusDetailDto)">
                <a-button type="link" size="small" danger>删除</a-button>
              </a-popconfirm>
            </template>
          </template>
          <template #emptyText>
            <EmptyState description="暂无明细数据" />
          </template>
        </a-table>

        <!-- 提交审批 -->
        <div v-if="currentPlan.status === 0" style="margin-top: 24px; text-align: right">
          <a-button type="primary" @click="handleSubmitApproval(currentPlan)">
            <template #icon><SendOutlined /></template>提交审批
          </a-button>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, EyeOutlined, SendOutlined, SaveOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { CrmPermissions, usePermission } from '@/utils/permission'
import {
  getBonusPlanList, createBonusPlan, updateBonusPlan, updateBonusPlanStatus,
  getBonusPlanById, addBonusDetail, deleteBonusDetail,
  type BonusPlanDto, type BonusDetailDto,
} from '@/api/crm'

const { has } = usePermission()

const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '审批中', value: 1 },
  { label: '已批准', value: 2 },
  { label: '已发放', value: 3 },
]

const statusColorMap: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'cyan' }
const statusTextMap: Record<number, string> = { 0: '草稿', 1: '审批中', 2: '已批准', 3: '已发放' }

const bonusTypeOptions = [
  { label: 'BD绩效', value: 1 },
  { label: '运维绩效', value: 2 },
  { label: '管理奖金', value: 3 },
]
const bonusTypeTextMap: Record<number, string> = { 1: 'BD绩效', 2: '运维绩效', 3: '管理奖金' }

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '组织', dataIndex: 'orgId', width: 120 },
  { title: '期间', dataIndex: 'period', width: 100 },
  { title: '奖金总额', dataIndex: 'totalAmount', width: 140, align: 'right' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 250, align: 'center' as const, fixed: 'right' as const },
]

const detailColumns = [
  { title: '员工姓名', dataIndex: 'employeeId', width: 150 },
  { title: '奖金类型', dataIndex: 'bonusType', width: 130 },
  { title: '金额', dataIndex: 'amount', width: 130, align: 'right' as const },
  { title: '操作', dataIndex: 'action', width: 130, align: 'center' as const },
]

// 搜索
const searchForm = reactive({ period: null as any, orgId: undefined as number | undefined, status: undefined as number | undefined })
const loading = ref(false)
const tableData = ref<BonusPlanDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.period) params.period = searchForm.period.format('YYYY-MM')
    if (searchForm.orgId !== undefined) params.orgId = searchForm.orgId
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getBonusPlanList(params) as any
    tableData.value = res?.items || res || []
    pagination.total = res?.total || 0
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() { searchForm.period = null; searchForm.orgId = undefined; searchForm.status = undefined; handleSearch() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

// 新建/编辑方案
const modalVisible = ref(false)
const modalType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  orgId: undefined as number | undefined,
  period: null as any,
  totalAmount: undefined as number | undefined,
})

const formRules: Record<string, Rule[]> = {
  period: [{ required: true, message: '请选择期间', trigger: 'change' }],
  totalAmount: [{ required: true, message: '请输入奖金总额', trigger: 'blur' }],
}

function handleAdd() {
  modalType.value = 'add'
  currentId.value = null
  Object.assign(formData, { orgId: undefined, period: null, totalAmount: undefined })
  modalVisible.value = true
}

function handleEdit(record: BonusPlanDto) {
  modalType.value = 'edit'
  currentId.value = record.id
  Object.assign(formData, { orgId: record.orgId, period: record.period, totalAmount: record.totalAmount })
  modalVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const data = {
      orgId: formData.orgId,
      period: typeof formData.period === 'string' ? formData.period : formData.period?.format('YYYY-MM'),
      totalAmount: formData.totalAmount!,
    }
    if (modalType.value === 'add') {
      await createBonusPlan(data)
      message.success('创建成功')
    } else {
      await updateBonusPlan(currentId.value!, data)
      message.success('更新成功')
    }
    modalVisible.value = false
    fetchList()
  } finally { submitLoading.value = false }
}

// 奖金明细
const detailDrawerVisible = ref(false)
const currentPlan = ref<BonusPlanDto | null>(null)
const detailData = ref<(BonusDetailDto & { _editing?: boolean; employeeName?: string })[]>([])
const detailLoading = ref(false)
const saveDetailLoading = ref(false)

async function handleViewDetail(record: BonusPlanDto) {
  detailDrawerVisible.value = true
  detailLoading.value = true
  try {
    const detail = await getBonusPlanById(record.id) as any
    currentPlan.value = detail || record
    detailData.value = (detail?.details || record.details || []).map((d: BonusDetailDto) => ({ ...d, _editing: false }))
  } catch {
    currentPlan.value = record
    detailData.value = (record.details || []).map(d => ({ ...d, _editing: false }))
  } finally { detailLoading.value = false }
}

function handleAddDetail() {
  detailData.value.push({
    id: -Date.now(),
    planId: currentPlan.value!.id,
    employeeId: 0,
    amount: 0,
    bonusType: 1,
    creatorName: '',
    createdTime: '',
    _editing: true,
  })
}

async function handleSaveDetails() {
  saveDetailLoading.value = true
  try {
    const newItems = detailData.value.filter(d => d.id < 0)
    for (const item of newItems) {
      await addBonusDetail(currentPlan.value!.id, {
        employeeId: item.employeeId,
        amount: item.amount,
        bonusType: item.bonusType,
      })
    }
    message.success('保存成功')
    handleViewDetail(currentPlan.value!)
  } finally { saveDetailLoading.value = false }
}

async function handleDeleteDetail(record: BonusDetailDto) {
  if (record.id < 0) {
    detailData.value = detailData.value.filter(d => d.id !== record.id)
    return
  }
  try {
    await deleteBonusDetail(record.id)
    message.success('删除成功')
    handleViewDetail(currentPlan.value!)
  } catch { /* handled */ }
}

async function handleSubmitApproval(record: BonusPlanDto) {
  try {
    await updateBonusPlanStatus(record.id, 1)
    message.success('已提交审批')
    fetchList()
    if (detailDrawerVisible.value) {
      handleViewDetail(record)
    }
  } catch { /* handled */ }
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
