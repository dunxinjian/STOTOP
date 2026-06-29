<template>
  <div class="page-container">
    <PageHeader title="理赔管理" description="管理理赔申请与审批流程">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增理赔
        </a-button>
        <a-button @click="fetchMyPending" style="margin-left: 8px">待我审批</a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.settlementType" size="small" placeholder="理赔类型" style="width: 140px" allowClear :options="settlementTypeOptions" />
          <a-select v-model:value="searchForm.settlementStatus" size="small" placeholder="理赔状态" style="width: 120px" allowClear :options="settlementStatusOptions" />
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
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'SettlementType'">
            <a-tag :color="record.SettlementType === 1 ? 'blue' : 'orange'">
              {{ settlementTypeMap[record.SettlementType] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'ApplyDate'">
            {{ formatDate(record.ApplyDate) }}
          </template>
          <template v-if="column.dataIndex === 'AssessedAmount'">
            {{ record.AssessedAmount != null ? `¥${record.AssessedAmount.toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'SettlementAmount'">
            {{ record.SettlementAmount != null ? `¥${record.SettlementAmount.toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'SettlementStatus'">
            <a-tag :color="settlementStatusColorMap[record.SettlementStatus] || 'default'">
              {{ settlementStatusMap[record.SettlementStatus] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleDetail(record as InsSettlementListItemDto)">详情</a-button>
            <a-button v-if="record.SettlementStatus === 1" type="link" size="small" @click="handleSubmitApproval(record as InsSettlementListItemDto)">提交审批</a-button>
            <a-button v-if="record.SettlementStatus === 10" type="link" size="small" @click="handleApprove(record as InsSettlementListItemDto)">审批</a-button>
            <a-button v-if="record.SettlementStatus === 20" type="link" size="small" @click="handlePay(record as InsSettlementListItemDto)">拨付</a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无理赔记录" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增弹窗 -->
    <a-modal v-model:open="dialogVisible" title="新增理赔" width="650px" :destroy-on-close="true" @cancel="dialogVisible = false">
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="出险记录ID" name="ClaimId">
              <a-input-number v-model:value="formData.ClaimId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="保单ID" name="PolicyId">
              <a-input-number v-model:value="formData.PolicyId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="理赔类型" name="SettlementType">
              <a-select v-model:value="formData.SettlementType" placeholder="请选择" :options="settlementTypeEditOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="申请日期" name="ApplyDate">
              <a-date-picker v-model:value="formData.ApplyDate" placeholder="请选择" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="定损金额">
              <a-input-number v-model:value="formData.AssessedAmount" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="理赔金额">
              <a-input-number v-model:value="formData.SettlementAmount" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="自付金额">
              <a-input-number v-model:value="formData.SelfPayAmount" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="免赔额">
              <a-input-number v-model:value="formData.Deductible" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea v-model:value="formData.Remark" :rows="2" placeholder="请输入" :maxlength="500" show-count />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleFormSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 详情 + 审批时间线弹窗 -->
    <a-modal v-model:open="detailVisible" title="理赔详情" width="700px" :footer="null">
      <a-descriptions :column="2" bordered size="small" style="margin-bottom: 16px">
        <a-descriptions-item label="理赔编号">{{ detailData?.SettlementNumber }}</a-descriptions-item>
        <a-descriptions-item label="理赔类型">{{ settlementTypeMap[detailData?.SettlementType || 0] }}</a-descriptions-item>
        <a-descriptions-item label="申请日期">{{ formatDate(detailData?.ApplyDate || '') }}</a-descriptions-item>
        <a-descriptions-item label="申请人">{{ detailData?.ApplicantName || '-' }}</a-descriptions-item>
        <a-descriptions-item label="定损金额">{{ detailData?.AssessedAmount != null ? `¥${detailData.AssessedAmount.toFixed(2)}` : '-' }}</a-descriptions-item>
        <a-descriptions-item label="理赔金额">{{ detailData?.SettlementAmount != null ? `¥${detailData.SettlementAmount.toFixed(2)}` : '-' }}</a-descriptions-item>
        <a-descriptions-item label="状态" :span="2">
          <a-tag :color="settlementStatusColorMap[detailData?.SettlementStatus || 0]">{{ settlementStatusMap[detailData?.SettlementStatus || 0] }}</a-tag>
          <span v-if="detailData?.CurrentStepName" style="margin-left: 8px">当前环节: {{ detailData.CurrentStepName }}</span>
        </a-descriptions-item>
      </a-descriptions>

      <a-divider>审批时间线</a-divider>
      <a-timeline v-if="approvalHistory.length > 0">
        <a-timeline-item v-for="item in approvalHistory" :key="item.Id" :color="approvalActionColorMap[item.ApprovalAction] || 'gray'">
          <p><strong>{{ item.StepName }}</strong> - {{ item.ApproverName }}</p>
          <p>
            <a-tag :color="approvalActionColorMap[item.ApprovalAction]">{{ approvalActionMap[item.ApprovalAction] }}</a-tag>
            <span style="margin-left: 8px; color: #999">{{ formatDate(item.ApprovalTime) }}</span>
          </p>
          <p v-if="item.ApprovalComment" style="color: #666">{{ item.ApprovalComment }}</p>
        </a-timeline-item>
      </a-timeline>
      <EmptyState v-else description="暂无审批记录" />
    </a-modal>

    <!-- 审批操作弹窗 -->
    <a-modal v-model:open="approveVisible" title="审批操作" width="500px" @ok="handleApproveSubmit" :confirm-loading="approveLoading">
      <a-form :label-col="{ style: { width: '80px' } }">
        <a-form-item label="审批动作">
          <a-radio-group v-model:value="approveAction">
            <a-radio :value="1">通过</a-radio>
            <a-radio :value="2">驳回</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="审批意见">
          <a-textarea v-model:value="approveComment" :rows="3" placeholder="请输入审批意见" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 支付弹窗 -->
    <a-modal v-model:open="payVisible" title="确认支付" width="500px" @ok="handlePaySubmit" :confirm-loading="payLoading">
      <a-form :label-col="{ style: { width: '80px' } }">
        <a-form-item label="支付凭证">
          <a-input v-model:value="payVoucher" placeholder="请输入支付凭证号" :maxlength="100" />
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
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getSettlementList,
  createSettlement,
  submitSettlement,
  reviewSettlement,
  paySettlement,
  getMyPendingSettlements,
  getApprovalHistory,
  type InsSettlementListItemDto,
  type InsSettlementDto,
  type InsApprovalRecordListItemDto,
} from '@/api/insurance'

const settlementTypeMap: Record<number, string> = { 1: '商业保险理赔', 2: '共保基金理赔' }
const settlementTypeOptions = [{ label: '商业保险理赔', value: 1 }, { label: '共保基金理赔', value: 2 }]
const settlementTypeEditOptions = [...settlementTypeOptions]
const settlementStatusMap: Record<number, string> = { 1: '草稿', 10: '审批中', 20: '已通过', 99: '已驳回', 30: '已拨付' }
const settlementStatusColorMap: Record<number, string> = { 1: 'default', 10: 'processing', 20: 'success', 99: 'error', 30: 'purple' }
const settlementStatusOptions = [
  { label: '草稿', value: 1 }, { label: '审批中', value: 10 }, { label: '已通过', value: 20 },
  { label: '已驳回', value: 99 }, { label: '已拨付', value: 30 },
]
const approvalActionMap: Record<number, string> = { 1: '通过', 2: '驳回', 3: '转办' }
const approvalActionColorMap: Record<number, string> = { 1: 'green', 2: 'red', 3: 'blue' }

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '理赔编号', dataIndex: 'SettlementNumber', width: 150 },
  { title: '理赔类型', dataIndex: 'SettlementType', width: 130, align: 'center' as const },
  { title: '申请日期', dataIndex: 'ApplyDate', width: 110 },
  { title: '申请人', dataIndex: 'ApplicantName', width: 100 },
  { title: '定损金额', dataIndex: 'AssessedAmount', width: 110, align: 'right' as const },
  { title: '理赔金额', dataIndex: 'SettlementAmount', width: 110, align: 'right' as const },
  { title: '状态', dataIndex: 'SettlementStatus', width: 90, align: 'center' as const },
  { title: '出险编号', dataIndex: 'ClaimNumber', width: 140 },
  { title: '操作', dataIndex: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({ settlementType: undefined as number | undefined, settlementStatus: undefined as number | undefined })
const loading = ref(false)
const tableData = ref<InsSettlementListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

// 新增弹窗
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const formData = reactive({
  ClaimId: undefined as number | undefined,
  PolicyId: undefined as number | undefined,
  SettlementType: 1,
  ApplyDate: '',
  AssessedAmount: undefined as number | undefined,
  SettlementAmount: undefined as number | undefined,
  SelfPayAmount: undefined as number | undefined,
  Deductible: undefined as number | undefined,
  Remark: '',
})
const formRules: Record<string, Rule[]> = {
  ClaimId: [{ required: true, message: '请输入出险记录ID', trigger: 'blur' }],
  PolicyId: [{ required: true, message: '请输入保单ID', trigger: 'blur' }],
  SettlementType: [{ required: true, message: '请选择理赔类型', trigger: 'change' }],
  ApplyDate: [{ required: true, message: '请选择申请日期', trigger: 'change' }],
}

// 详情
const detailVisible = ref(false)
const detailData = ref<InsSettlementDto | null>(null)
const approvalHistory = ref<InsApprovalRecordListItemDto[]>([])

// 审批
const approveVisible = ref(false)
const approveLoading = ref(false)
const approveAction = ref(1)
const approveComment = ref('')
const approveTargetId = ref<number | null>(null)

// 支付
const payVisible = ref(false)
const payLoading = ref(false)
const payVoucher = ref('')
const payTargetId = ref<number | null>(null)

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('zh-CN')
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.settlementType != null) params.settlementType = searchForm.settlementType
    if (searchForm.settlementStatus != null) params.settlementStatus = searchForm.settlementStatus
    const res = await getSettlementList(params)
    if (res) { tableData.value = res.items || []; pagination.total = res.total || 0 }
  } finally { loading.value = false }
}

async function fetchMyPending() {
  loading.value = true
  try {
    const res = await getMyPendingSettlements({ pageIndex: pagination.pageIndex, pageSize: pagination.pageSize })
    if (res) { tableData.value = res.items || []; pagination.total = res.total || 0 }
  } finally { loading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() { searchForm.settlementType = undefined; searchForm.settlementStatus = undefined; pagination.pageIndex = 1; fetchList() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

function handleAdd() {
  formData.ClaimId = undefined; formData.PolicyId = undefined; formData.SettlementType = 1
  formData.ApplyDate = ''; formData.AssessedAmount = undefined; formData.SettlementAmount = undefined
  formData.SelfPayAmount = undefined; formData.Deductible = undefined; formData.Remark = ''
  dialogVisible.value = true
}

async function handleFormSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    await createSettlement({ ...formData })
    message.success('新增成功'); dialogVisible.value = false; fetchList()
  } catch { message.error('提交失败') } finally { submitLoading.value = false }
}

async function handleDetail(row: InsSettlementListItemDto) {
  detailData.value = row as any
  detailVisible.value = true
  try {
    approvalHistory.value = await getApprovalHistory(row.Id) || []
  } catch { approvalHistory.value = [] }
}

async function handleSubmitApproval(row: InsSettlementListItemDto) {
  try { await submitSettlement(row.Id); message.success('已提交审批'); fetchList() }
  catch { message.error('提交失败') }
}

function handleApprove(row: InsSettlementListItemDto) {
  approveTargetId.value = row.Id; approveAction.value = 1; approveComment.value = ''; approveVisible.value = true
}

async function handleApproveSubmit() {
  if (!approveTargetId.value) return
  approveLoading.value = true
  try {
    await reviewSettlement(approveTargetId.value, { ApprovalAction: approveAction.value, ApprovalComment: approveComment.value || undefined })
    message.success('审批操作成功'); approveVisible.value = false; fetchList()
  } catch { message.error('审批操作失败') } finally { approveLoading.value = false }
}

function handlePay(row: InsSettlementListItemDto) {
  payTargetId.value = row.Id; payVoucher.value = ''; payVisible.value = true
}

async function handlePaySubmit() {
  if (!payTargetId.value) return
  payLoading.value = true
  try {
    await paySettlement(payTargetId.value, { PaymentVoucher: payVoucher.value || undefined })
    message.success('支付确认成功'); payVisible.value = false; fetchList()
  } catch { message.error('支付确认失败') } finally { payLoading.value = false }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
