<template>
  <div class="page-container">
    <PageHeader title="出险管理" description="登记和跟踪出险记录">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>出险登记
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.businessType" size="small" placeholder="业务类型" style="width: 120px" allowClear :options="businessTypeOptions" />
          <a-select v-model:value="searchForm.accidentType" size="small" placeholder="事故类型" style="width: 120px" allowClear :options="accidentTypeOptions" />
          <a-select v-model:value="searchForm.claimStatus" size="small" placeholder="出险状态" style="width: 120px" allowClear :options="claimStatusOptions" />
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
        :scroll="{ x: 1300 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'BusinessType'">
            <a-tag>{{ businessTypeMap[record.BusinessType] || '未知' }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'ClaimDate'">
            {{ formatDate(record.ClaimDate) }}
          </template>
          <template v-if="column.dataIndex === 'AccidentType'">
            {{ accidentTypeMap[record.AccidentType] || '未知' }}
          </template>
          <template v-if="column.dataIndex === 'EstimatedLoss'">
            {{ record.EstimatedLoss != null ? `¥${record.EstimatedLoss.toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'ActualLoss'">
            {{ record.ActualLoss != null ? `¥${record.ActualLoss.toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'ClaimStatus'">
            <a-tag :color="claimStatusColorMap[record.ClaimStatus] || 'default'">
              {{ claimStatusMap[record.ClaimStatus] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as InsClaimListItemDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button v-if="record.ClaimStatus !== 3" type="link" size="small" @click="handleClose(record as InsClaimListItemDto)">
              结案
            </a-button>
            <a-button type="link" size="small" @click="handleDetail(record as InsClaimListItemDto)">
              详情
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无出险记录" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '出险登记' : '编辑出险'"
      width="750px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '110px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="业务类型" name="BusinessType">
              <a-select v-model:value="formData.BusinessType" placeholder="请选择" :options="businessTypeEditOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="关联对象ID" name="RelatedObjectId">
              <a-input-number v-model:value="formData.RelatedObjectId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="关联对象">
              <a-input v-model:value="formData.RelatedObjectName" placeholder="关联对象名称" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="关联保单ID">
              <a-input-number v-model:value="formData.PolicyId" placeholder="保单ID（可选）" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="出险日期" name="ClaimDate">
              <a-date-picker v-model:value="formData.ClaimDate" placeholder="请选择" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="出险地点">
              <a-input v-model:value="formData.ClaimLocation" placeholder="请输入" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="事故类型" name="AccidentType">
              <a-select v-model:value="formData.AccidentType" placeholder="请选择" :options="accidentTypeEditOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="责任划分">
              <a-select v-model:value="formData.LiabilityDivision" placeholder="请选择" allow-clear :options="liabilityOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="预估损失">
              <a-input-number v-model:value="formData.EstimatedLoss" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="对方信息">
              <a-input v-model:value="formData.CounterpartyInfo" placeholder="请输入" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="当事人ID">
              <a-input-number v-model:value="formData.PartyId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="当事人姓名">
              <a-input v-model:value="formData.PartyName" placeholder="请输入" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="案件编号">
              <a-input v-model:value="formData.CaseNumber" placeholder="请输入" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="事故照片">
              <a-input v-model:value="formData.ClaimImages" placeholder="图片地址（多个用逗号分隔）" :maxlength="2000" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="事故描述">
              <a-textarea v-model:value="formData.AccidentDescription" :rows="2" placeholder="请描述事故经过" :maxlength="1000" show-count />
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

    <!-- 详情弹窗 -->
    <a-modal v-model:open="detailVisible" title="出险详情" width="600px" :footer="null">
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="出险编号">{{ detailData?.ClaimNumber }}</a-descriptions-item>
        <a-descriptions-item label="业务类型">{{ businessTypeMap[detailData?.BusinessType || 0] }}</a-descriptions-item>
        <a-descriptions-item label="关联对象">{{ detailData?.RelatedObjectName }}</a-descriptions-item>
        <a-descriptions-item label="出险日期">{{ formatDate(detailData?.ClaimDate || '') }}</a-descriptions-item>
        <a-descriptions-item label="事故类型">{{ accidentTypeMap[detailData?.AccidentType || 0] }}</a-descriptions-item>
        <a-descriptions-item label="出险地点">{{ detailData?.ClaimLocation || '-' }}</a-descriptions-item>
        <a-descriptions-item label="预估损失">{{ detailData?.EstimatedLoss != null ? `¥${detailData.EstimatedLoss.toFixed(2)}` : '-' }}</a-descriptions-item>
        <a-descriptions-item label="实际损失">{{ detailData?.ActualLoss != null ? `¥${detailData.ActualLoss.toFixed(2)}` : '-' }}</a-descriptions-item>
        <a-descriptions-item label="状态" :span="2">
          <a-tag :color="claimStatusColorMap[detailData?.ClaimStatus || 0]">{{ claimStatusMap[detailData?.ClaimStatus || 0] }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="事故描述" :span="2">{{ detailData?.AccidentDescription || '-' }}</a-descriptions-item>
      </a-descriptions>
    </a-modal>

    <!-- 结案弹窗 -->
    <a-modal v-model:open="closeVisible" title="结案" width="500px" @ok="handleCloseSubmit" :confirm-loading="closeLoading">
      <a-form :label-col="{ style: { width: '80px' } }">
        <a-form-item label="结案备注">
          <a-textarea v-model:value="closeRemark" :rows="3" placeholder="请输入结案备注" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
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
  getClaimList,
  createClaim,
  updateClaim,
  closeClaim,
  type InsClaimListItemDto,
  type InsClaimDto,
} from '@/api/insurance'

const businessTypeMap: Record<number, string> = { 1: '三轮车', 2: '员工', 3: '资产' }
const businessTypeOptions = [{ label: '三轮车', value: 1 }, { label: '员工', value: 2 }, { label: '资产', value: 3 }]
const businessTypeEditOptions = [...businessTypeOptions]
const accidentTypeMap: Record<number, string> = { 1: '碰撞', 2: '自燃', 3: '盗抢', 4: '自然灾害', 5: '其他' }
const accidentTypeOptions = [
  { label: '碰撞', value: 1 }, { label: '自燃', value: 2 }, { label: '盗抢', value: 3 },
  { label: '自然灾害', value: 4 }, { label: '其他', value: 5 },
]
const accidentTypeEditOptions = [...accidentTypeOptions]
const claimStatusMap: Record<number, string> = { 1: '已登记', 2: '处理中', 3: '已结案' }
const claimStatusColorMap: Record<number, string> = { 1: 'blue', 2: 'orange', 3: 'default' }
const claimStatusOptions = [{ label: '已登记', value: 1 }, { label: '处理中', value: 2 }, { label: '已结案', value: 3 }]
const liabilityOptions = [
  { label: '全责', value: 1 }, { label: '主责', value: 2 }, { label: '同责', value: 3 },
  { label: '次责', value: 4 }, { label: '无责', value: 5 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '出险编号', dataIndex: 'ClaimNumber', width: 140 },
  { title: '业务类型', dataIndex: 'BusinessType', width: 90, align: 'center' as const },
  { title: '关联对象', dataIndex: 'RelatedObjectName', width: 120 },
  { title: '出险日期', dataIndex: 'ClaimDate', width: 110 },
  { title: '事故类型', dataIndex: 'AccidentType', width: 90 },
  { title: '预估损失', dataIndex: 'EstimatedLoss', width: 110, align: 'right' as const },
  { title: '实际损失', dataIndex: 'ActualLoss', width: 110, align: 'right' as const },
  { title: '状态', dataIndex: 'ClaimStatus', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({
  businessType: undefined as number | undefined,
  accidentType: undefined as number | undefined,
  claimStatus: undefined as number | undefined,
})
const loading = ref(false)
const tableData = ref<InsClaimListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const detailVisible = ref(false)
const detailData = ref<InsClaimDto | null>(null)

const closeVisible = ref(false)
const closeLoading = ref(false)
const closeRemark = ref('')
const closeTargetId = ref<number | null>(null)

const formData = reactive({
  BusinessType: 1,
  RelatedObjectId: undefined as number | undefined,
  RelatedObjectName: '',
  PolicyId: undefined as number | undefined,
  ClaimDate: '',
  ClaimLocation: '',
  AccidentType: 1,
  AccidentDescription: '',
  CounterpartyInfo: '',
  EstimatedLoss: undefined as number | undefined,
  LiabilityDivision: undefined as number | undefined,
  PartyId: undefined as number | undefined,
  PartyName: '',
  CaseNumber: '',
  ClaimImages: '',
  Remark: '',
})

const formRules: Record<string, Rule[]> = {
  BusinessType: [{ required: true, message: '请选择业务类型', trigger: 'change' }],
  RelatedObjectId: [{ required: true, message: '请输入关联对象ID', trigger: 'blur' }],
  ClaimDate: [{ required: true, message: '请选择出险日期', trigger: 'change' }],
  AccidentType: [{ required: true, message: '请选择事故类型', trigger: 'change' }],
}

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('zh-CN')
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.businessType != null) params.businessType = searchForm.businessType
    if (searchForm.accidentType != null) params.accidentType = searchForm.accidentType
    if (searchForm.claimStatus != null) params.claimStatus = searchForm.claimStatus
    const res = await getClaimList(params)
    if (res) { tableData.value = res.items || []; pagination.total = res.total || 0 }
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() {
  searchForm.businessType = undefined; searchForm.accidentType = undefined; searchForm.claimStatus = undefined
  pagination.pageIndex = 1; fetchList()
}
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

function resetForm() {
  formData.BusinessType = 1; formData.RelatedObjectId = undefined; formData.RelatedObjectName = ''
  formData.PolicyId = undefined; formData.ClaimDate = ''; formData.ClaimLocation = ''
  formData.AccidentType = 1; formData.AccidentDescription = ''; formData.CounterpartyInfo = ''
  formData.EstimatedLoss = undefined; formData.LiabilityDivision = undefined
  formData.PartyId = undefined; formData.PartyName = ''; formData.CaseNumber = ''
  formData.ClaimImages = ''; formData.Remark = ''
}

function handleAdd() { dialogType.value = 'add'; currentId.value = null; resetForm(); dialogVisible.value = true }

function handleEdit(row: InsClaimListItemDto) {
  dialogType.value = 'edit'; currentId.value = row.Id; resetForm()
  formData.BusinessType = row.BusinessType; formData.AccidentType = row.AccidentType
  formData.RelatedObjectName = row.RelatedObjectName || ''
  formData.ClaimDate = row.ClaimDate ? row.ClaimDate.substring(0, 10) : ''
  dialogVisible.value = true
}

function handleDetail(row: InsClaimListItemDto) {
  detailData.value = row as any
  detailVisible.value = true
}

function handleClose(row: InsClaimListItemDto) {
  closeTargetId.value = row.Id; closeRemark.value = ''; closeVisible.value = true
}

async function handleCloseSubmit() {
  if (!closeTargetId.value) return
  closeLoading.value = true
  try {
    await closeClaim(closeTargetId.value, { ClosedRemark: closeRemark.value || undefined })
    message.success('结案成功'); closeVisible.value = false; fetchList()
  } catch { message.error('结案失败') } finally { closeLoading.value = false }
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const data: any = { ...formData }
    if (dialogType.value === 'add') { await createClaim(data); message.success('登记成功') }
    else { await updateClaim(currentId.value!, data); message.success('更新成功') }
    dialogVisible.value = false; fetchList()
  } catch { message.error('提交失败') } finally { submitLoading.value = false }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
