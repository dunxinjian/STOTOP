<template>
  <div class="page-container">
    <PageHeader title="保单管理" description="管理各业务类型保单信息">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增保单
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.businessType" size="small" placeholder="业务类型" style="width: 120px" allowClear :options="businessTypeOptions" />
          <a-select v-model:value="searchForm.insuranceCategory" size="small" placeholder="保险大类" style="width: 120px" allowClear :options="insuranceCategoryOptions" />
          <a-select v-model:value="searchForm.insuranceStatus" size="small" placeholder="保单状态" style="width: 120px" allowClear :options="insuranceStatusOptions" />
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
        row-key="Id"
        bordered
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'BusinessType'">
            <a-tag>{{ businessTypeMap[record.BusinessType] || '未知' }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'InsuranceCategory'">
            <a-tag :color="record.InsuranceCategory === 1 ? 'blue' : 'orange'">
              {{ record.InsuranceCategory === 1 ? '商业保险' : '共保基金' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'Premium'">
            {{ record.Premium != null ? `¥${record.Premium.toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'EffectiveDate'">
            {{ formatDate(record.EffectiveDate) }}
          </template>
          <template v-if="column.dataIndex === 'ExpiryDate'">
            <span :class="{ 'expiry-warning': isExpiringSoon(record.ExpiryDate) }">
              {{ formatDate(record.ExpiryDate) }}
              <a-tag v-if="isExpiringSoon(record.ExpiryDate)" color="red" style="margin-left: 4px">即将到期</a-tag>
            </span>
          </template>
          <template v-if="column.dataIndex === 'InsuranceStatus'">
            <a-tag :color="insuranceStatusColorMap[record.InsuranceStatus] || 'default'">
              {{ insuranceStatusMap[record.InsuranceStatus] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as InsPolicyListItemDto)">
              <EditOutlined />编辑
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无保单数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增保单' : '编辑保单'"
      width="750px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '110px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="业务类型" name="BusinessType">
              <a-select v-model:value="formData.BusinessType" placeholder="请选择" :options="businessTypeEditOptions" :disabled="dialogType === 'edit'" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="关联对象" name="RelatedObjectName">
              <a-input v-model:value="formData.RelatedObjectName" placeholder="关联对象名称" :maxlength="100" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="关联对象ID" name="RelatedObjectId">
              <a-input-number v-model:value="formData.RelatedObjectId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="保险大类" name="InsuranceCategory">
              <a-select v-model:value="formData.InsuranceCategory" placeholder="请选择" :options="insuranceCategoryEditOptions" @change="onCategoryChange" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 商业保险字段 -->
        <template v-if="formData.InsuranceCategory === 1">
          <a-row :gutter="20">
            <a-col :span="12">
              <a-form-item label="保险公司">
                <a-input-number v-model:value="formData.InsuranceCompanyId" placeholder="保险公司ID" style="width: 100%" :min="1" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="保单号">
                <a-input v-model:value="formData.PolicyNumber" placeholder="请输入保单号" :maxlength="50" />
              </a-form-item>
            </a-col>
          </a-row>
          <a-row :gutter="20">
            <a-col :span="12">
              <a-form-item label="保费">
                <a-input-number v-model:value="formData.Premium" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="保额">
                <a-input-number v-model:value="formData.InsuredAmount" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
              </a-form-item>
            </a-col>
          </a-row>
          <a-row :gutter="20">
            <a-col :span="12">
              <a-form-item label="险种">
                <a-input v-model:value="formData.InsuranceType" placeholder="请输入险种" :maxlength="50" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="联系人">
                <a-input v-model:value="formData.ContactPerson" placeholder="请输入" :maxlength="50" />
              </a-form-item>
            </a-col>
          </a-row>
        </template>

        <!-- 共保基金字段 -->
        <template v-if="formData.InsuranceCategory === 2">
          <a-row :gutter="20">
            <a-col :span="12">
              <a-form-item label="共保基金">
                <a-input-number v-model:value="formData.CoInsuranceFundId" placeholder="基金ID" style="width: 100%" :min="1" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="参保编号">
                <a-input v-model:value="formData.ParticipationNumber" placeholder="请输入参保编号" :maxlength="50" />
              </a-form-item>
            </a-col>
          </a-row>
          <a-row :gutter="20">
            <a-col :span="12">
              <a-form-item label="缴费周期">
                <a-select v-model:value="formData.PaymentCycle" placeholder="请选择" :options="paymentCycleOptions" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="每期金额">
                <a-input-number v-model:value="formData.PerPeriodAmount" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
              </a-form-item>
            </a-col>
          </a-row>
        </template>

        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="生效日期" name="EffectiveDate">
              <a-date-picker v-model:value="formData.EffectiveDate" placeholder="请选择" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="到期日期" name="ExpiryDate">
              <a-date-picker v-model:value="formData.ExpiryDate" placeholder="请选择" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea v-model:value="formData.Remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
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
import { PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getPolicyList,
  createPolicy,
  updatePolicy,
  type InsPolicyListItemDto,
} from '@/api/insurance'

const businessTypeMap: Record<number, string> = { 1: '三轮车', 2: '员工', 3: '资产' }
const businessTypeOptions = [
  { label: '三轮车', value: 1 },
  { label: '员工', value: 2 },
  { label: '资产', value: 3 },
]
const businessTypeEditOptions = [...businessTypeOptions]
const insuranceCategoryOptions = [
  { label: '商业保险', value: 1 },
  { label: '共保基金', value: 2 },
]
const insuranceCategoryEditOptions = [...insuranceCategoryOptions]
const insuranceStatusOptions = [
  { label: '有效', value: 1 },
  { label: '已过期', value: 2 },
  { label: '已退保', value: 3 },
]
const insuranceStatusMap: Record<number, string> = { 1: '有效', 2: '已过期', 3: '已退保' }
const insuranceStatusColorMap: Record<number, string> = { 1: 'success', 2: 'default', 3: 'error' }
const paymentCycleOptions = [
  { label: '月', value: 1 },
  { label: '季', value: 2 },
  { label: '年', value: 3 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '业务类型', dataIndex: 'BusinessType', width: 90, align: 'center' as const },
  { title: '关联对象', dataIndex: 'RelatedObjectName', width: 120 },
  { title: '保险大类', dataIndex: 'InsuranceCategory', width: 100, align: 'center' as const },
  { title: '险种', dataIndex: 'InsuranceType', width: 100 },
  { title: '保险公司', dataIndex: 'InsuranceCompanyName', width: 120 },
  { title: '保单号', dataIndex: 'PolicyNumber', width: 130 },
  { title: '保费', dataIndex: 'Premium', width: 100, align: 'right' as const },
  { title: '生效日期', dataIndex: 'EffectiveDate', width: 110 },
  { title: '到期日期', dataIndex: 'ExpiryDate', width: 150 },
  { title: '状态', dataIndex: 'InsuranceStatus', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 100, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({
  businessType: undefined as number | undefined,
  insuranceCategory: undefined as number | undefined,
  insuranceStatus: undefined as number | undefined,
})
const loading = ref(false)
const tableData = ref<InsPolicyListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  BusinessType: 1,
  RelatedObjectId: undefined as number | undefined,
  RelatedObjectName: '',
  InsuranceCategory: 1,
  InsuranceType: '',
  InsuranceCompanyId: undefined as number | undefined,
  PolicyNumber: '',
  Premium: undefined as number | undefined,
  InsuredAmount: undefined as number | undefined,
  ContactPerson: '',
  ContactPhone: '',
  CoInsuranceFundId: undefined as number | undefined,
  ParticipationNumber: '',
  PaymentCycle: undefined as number | undefined,
  PerPeriodAmount: undefined as number | undefined,
  EffectiveDate: '',
  ExpiryDate: '',
  Remark: '',
})

const formRules: Record<string, Rule[]> = {
  BusinessType: [{ required: true, message: '请选择业务类型', trigger: 'change' }],
  RelatedObjectId: [{ required: true, message: '请输入关联对象ID', trigger: 'blur' }],
  InsuranceCategory: [{ required: true, message: '请选择保险大类', trigger: 'change' }],
  EffectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
  ExpiryDate: [{ required: true, message: '请选择到期日期', trigger: 'change' }],
}

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('zh-CN')
}

function isExpiringSoon(dateStr: string): boolean {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  return diff > 0 && diff < 30 * 24 * 60 * 60 * 1000
}

function onCategoryChange() {
  // 切换保险大类时清空对方字段
  if (formData.InsuranceCategory === 1) {
    formData.CoInsuranceFundId = undefined
    formData.ParticipationNumber = ''
    formData.PaymentCycle = undefined
    formData.PerPeriodAmount = undefined
  } else {
    formData.InsuranceCompanyId = undefined
    formData.PolicyNumber = ''
    formData.Premium = undefined
    formData.InsuredAmount = undefined
    formData.InsuranceType = ''
    formData.ContactPerson = ''
  }
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.businessType != null) params.businessType = searchForm.businessType
    if (searchForm.insuranceCategory != null) params.insuranceCategory = searchForm.insuranceCategory
    if (searchForm.insuranceStatus != null) params.insuranceStatus = searchForm.insuranceStatus
    const res = await getPolicyList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.total || 0
    }
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() {
  searchForm.businessType = undefined
  searchForm.insuranceCategory = undefined
  searchForm.insuranceStatus = undefined
  pagination.pageIndex = 1
  fetchList()
}
function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function resetForm() {
  formData.BusinessType = 1
  formData.RelatedObjectId = undefined
  formData.RelatedObjectName = ''
  formData.InsuranceCategory = 1
  formData.InsuranceType = ''
  formData.InsuranceCompanyId = undefined
  formData.PolicyNumber = ''
  formData.Premium = undefined
  formData.InsuredAmount = undefined
  formData.ContactPerson = ''
  formData.ContactPhone = ''
  formData.CoInsuranceFundId = undefined
  formData.ParticipationNumber = ''
  formData.PaymentCycle = undefined
  formData.PerPeriodAmount = undefined
  formData.EffectiveDate = ''
  formData.ExpiryDate = ''
  formData.Remark = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: InsPolicyListItemDto) {
  dialogType.value = 'edit'
  currentId.value = row.Id
  resetForm()
  formData.BusinessType = row.BusinessType
  formData.RelatedObjectName = row.RelatedObjectName || ''
  formData.InsuranceCategory = row.InsuranceCategory
  formData.InsuranceType = row.InsuranceType || ''
  formData.PolicyNumber = row.PolicyNumber || ''
  formData.Premium = row.Premium
  formData.EffectiveDate = row.EffectiveDate ? row.EffectiveDate.substring(0, 10) : ''
  formData.ExpiryDate = row.ExpiryDate ? row.ExpiryDate.substring(0, 10) : ''
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const data: any = { ...formData }
    if (dialogType.value === 'add') {
      await createPolicy(data)
      message.success('新增成功')
    } else {
      await updatePolicy(currentId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch { message.error('提交失败') } finally { submitLoading.value = false }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.expiry-warning {
  color: var(--color-danger);
  font-weight: 500;
}
</style>
