<template>
  <div class="finance-panel">
    <!-- 工具栏 -->
    <div class="toolbar" style="margin-bottom:16px; display:flex; gap:8px; align-items:center">
      <a-button type="primary" @click="showAddModal"><PlusOutlined />登记收入</a-button>
      <a-button @click="batchModalVisible = true"><UnorderedListOutlined />批量登记</a-button>
      <a-button @click="navigateToGift"><GiftOutlined />快速登记礼金</a-button>
      <a-divider type="vertical" />
      <a-select v-model:value="filters.type" placeholder="类型筛选" allowClear style="width:120px"
        :options="incomeTypeOptions" @change="loadIncomes" />
      <a-input-search v-model:value="filters.keyword" placeholder="姓名/单位" style="width:180px" @search="loadIncomes" />
    </div>

    <!-- 汇总统计区 -->
    <a-card :bordered="false" style="margin-bottom:16px">
      <a-row :gutter="24">
        <a-col :span="6">
          <a-statistic title="收入总额" :value="summary.totalAmount" prefix="¥" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="会费收入" :value="feeAmount" prefix="¥" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="礼金收入" :value="giftAmount" prefix="¥" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="登记人数" :value="summary.totalCount" suffix="人" />
        </a-col>
      </a-row>
    </a-card>

    <!-- 收入表格 -->
    <a-table
      :columns="columns"
      :data-source="incomeList"
      :loading="loading"
      row-key="id"
      :pagination="pagination"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'type'">
          <a-tag v-if="record.type === '会费'" color="blue">会费</a-tag>
          <a-tag v-else-if="record.type === '礼金'" color="gold">礼金</a-tag>
          <a-tag v-else-if="record.type === '赞助'" color="green">赞助</a-tag>
          <a-tag v-else>{{ record.type || '其他' }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'amount'">
          <span style="float:right">¥{{ (record.amount ?? 0).toFixed(2) }}</span>
        </template>
        <template v-else-if="column.dataIndex === 'paymentDate'">
          {{ record.paymentDate ? dayjs(record.paymentDate).format('YYYY-MM-DD HH:mm') : '' }}
        </template>
        <template v-else-if="column.dataIndex === 'remark'">
          <a-typography-text :ellipsis="{ tooltip: true }" :content="record.remark" style="max-width:120px" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="showEditModal(record as IncomeDto)">编辑</a-button>
            <a-popconfirm title="确定删除此记录？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
      <template #summary>
        <a-table-summary fixed>
          <a-table-summary-row>
            <a-table-summary-cell :index="0" :col-span="3">
              <strong>合计</strong>
            </a-table-summary-cell>
            <a-table-summary-cell :index="3">
              <strong style="float:right">¥{{ totalAmountOfPage.toFixed(2) }}</strong>
            </a-table-summary-cell>
            <a-table-summary-cell :index="4" :col-span="5" />
          </a-table-summary-row>
        </a-table-summary>
      </template>
    </a-table>

    <!-- 单条登记 Modal -->
    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? '编辑收入' : '登记收入'"
      @ok="handleSubmit"
      :confirm-loading="submitting"
      :width="560"
    >
      <a-form :model="form" layout="vertical" ref="formRef">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="参会人" name="attendeeId">
              <a-select
                v-model:value="form.attendeeId"
                placeholder="选择参会人"
                show-search
                allowClear
                :options="attendeeOptions"
                :filter-option="filterAttendeeOption"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="类型" name="type" :rules="[{ required: true, message: '请选择类型' }]">
              <a-select v-model:value="form.type" :options="incomeTypeOptions" placeholder="请选择" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="金额" name="amount" :rules="[{ required: true, message: '请输入金额' }]">
              <a-input-number v-model:value="form.amount" :min="0" prefix="¥" style="width:100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="缴费方式" name="paymentMethod">
              <a-select v-model:value="form.paymentMethod" :options="paymentMethodOptions" placeholder="请选择" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="收据号" name="receiptNumber">
              <a-input v-model:value="form.receiptNumber" placeholder="自动生成或手动输入" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="缴费日期" name="paymentDate">
              <a-date-picker v-model:value="form.paymentDate" style="width:100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注" name="remark">
          <a-textarea v-model:value="form.remark" placeholder="请输入备注" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 批量登记 Modal -->
    <a-modal v-model:open="batchModalVisible" title="批量登记" :width="800" @ok="confirmBatch" :confirm-loading="batchSubmitting">
      <a-alert message="快速为多人登记相同类型的收入" type="info" show-icon style="margin-bottom:16px" />
      <a-form layout="vertical">
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="收入类型">
              <a-select v-model:value="batchForm.type" :options="incomeTypeOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="统一金额">
              <a-input-number v-model:value="batchForm.amount" prefix="¥" style="width:100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="缴费方式">
              <a-select v-model:value="batchForm.paymentMethod" :options="paymentMethodOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="选择人员（多选）">
          <a-select
            v-model:value="batchForm.attendeeIds"
            mode="multiple"
            :options="attendeeOptions"
            placeholder="选择参会人员"
            show-search
            :filter-option="filterAttendeeOption"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, UnorderedListOutlined, GiftOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import {
  getIncomes, createIncome, updateIncome, deleteIncome,
  getIncomeSummary, batchRegisterIncomes, getAttendees,
} from '@/api/conference'
import type {
  IncomeDto, CreateIncomeRequest, UpdateIncomeRequest,
  IncomeSummaryDto, BatchRegisterIncomeRequest,
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()
const emit = defineEmits(['navigate'])

function navigateToGift() {
  emit('navigate', 'gift')
}

const loading = ref(false)
const submitting = ref(false)
const batchSubmitting = ref(false)
const incomeList = ref<IncomeDto[]>([])
const summary = ref<IncomeSummaryDto>({ totalAmount: 0, totalCount: 0, typeSummaries: [] })
const attendeeOptions = ref<{ label: string; value: number }[]>([])

const filters = reactive({ type: undefined as string | undefined, keyword: '' })
const pagination = reactive({ current: 1, pageSize: 10, total: 0, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` })

const incomeTypeOptions = [
  { label: '会费', value: '会费' },
  { label: '礼金', value: '礼金' },
  { label: '赞助', value: '赞助' },
  { label: '其他', value: '其他' },
]
const paymentMethodOptions = [
  { label: '现金', value: '现金' },
  { label: '转账', value: '转账' },
  { label: '微信', value: '微信' },
  { label: '支付宝', value: '支付宝' },
]

const feeAmount = computed(() => {
  const s = summary.value.typeSummaries?.find(t => t.type === '会费')
  return s?.amount ?? 0
})
const giftAmount = computed(() => {
  const s = summary.value.typeSummaries?.find(t => t.type === '礼金')
  return s?.amount ?? 0
})
const totalAmountOfPage = computed(() => incomeList.value.reduce((sum, r) => sum + (r.amount ?? 0), 0))

const columns = [
  { title: '参会人', dataIndex: 'attendeeName', width: 100 },
  { title: '单位', dataIndex: 'payerOrganization', width: 120 },
  { title: '类型', dataIndex: 'type', width: 80 },
  { title: '金额', dataIndex: 'amount', width: 110, align: 'right' as const },
  { title: '缴费方式', dataIndex: 'paymentMethod', width: 90 },
  { title: '收据号', dataIndex: 'receiptNumber', width: 120 },
  { title: '登记时间', dataIndex: 'paymentDate', width: 150 },
  { title: '备注', dataIndex: 'remark', width: 120, ellipsis: true },
  { title: '操作', key: 'action', width: 120, fixed: 'right' as const },
]

// ---- Modal ----
const modalVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref()
const form = reactive<Omit<CreateIncomeRequest, 'paymentDate'> & { paymentDate?: string }>({
  attendeeId: undefined,
  type: undefined,
  amount: 0,
  paymentMethod: undefined,
  receiptNumber: '',
  payerName: '',
  payerOrganization: '',
  paymentDate: dayjs().format('YYYY-MM-DD'),
  remark: '',
})

// ---- Batch Modal ----
const batchModalVisible = ref(false)
const batchForm = reactive({
  type: '会费',
  amount: 0,
  paymentMethod: '现金',
  attendeeIds: [] as number[],
})

function resetForm() {
  form.attendeeId = undefined
  form.type = undefined
  form.amount = 0
  form.paymentMethod = undefined
  form.receiptNumber = ''
  form.payerName = ''
  form.payerOrganization = ''
  form.paymentDate = dayjs().format('YYYY-MM-DD')
  form.remark = ''
}

function generateReceiptNo() {
  const ts = Date.now().toString().slice(-6)
  const rand = Math.floor(Math.random() * 1000).toString().padStart(3, '0')
  return `R${ts}${rand}`
}

function showAddModal() {
  editingId.value = null
  resetForm()
  form.receiptNumber = generateReceiptNo()
  modalVisible.value = true
}

function showEditModal(record: IncomeDto) {
  editingId.value = record.id
  form.attendeeId = record.attendeeId
  form.type = record.type
  form.amount = record.amount
  form.paymentMethod = record.paymentMethod
  form.receiptNumber = record.receiptNumber
  form.payerName = record.payerName
  form.payerOrganization = record.payerOrganization
  form.paymentDate = record.paymentDate ? dayjs(record.paymentDate).format('YYYY-MM-DD') : undefined
  form.remark = record.remark
  modalVisible.value = true
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const payload = { ...form, paymentDate: form.paymentDate || dayjs().format('YYYY-MM-DD') }
    if (editingId.value) {
      await updateIncome(editingId.value, payload as UpdateIncomeRequest)
      message.success('收入已更新')
    } else {
      await createIncome(props.eventId, payload)
      message.success('收入已登记')
    }
    modalVisible.value = false
    loadData()
  } finally {
    submitting.value = false
  }
}

async function handleDelete(id: number) {
  await deleteIncome(id)
  message.success('已删除')
  loadData()
}

async function confirmBatch() {
  if (!batchForm.attendeeIds.length) {
    message.warning('请至少选择一名人员')
    return
  }
  batchSubmitting.value = true
  try {
    await batchRegisterIncomes(props.eventId, {
      attendeeIds: batchForm.attendeeIds,
      type: batchForm.type,
      amount: batchForm.amount,
      paymentMethod: batchForm.paymentMethod,
      paymentDate: dayjs().format('YYYY-MM-DD'),
    } as BatchRegisterIncomeRequest)
    message.success(`成功为 ${batchForm.attendeeIds.length} 人登记`)
    batchModalVisible.value = false
    batchForm.attendeeIds = []
    loadData()
  } finally {
    batchSubmitting.value = false
  }
}

function filterAttendeeOption(input: string, option: any) {
  return (option?.label ?? '').toLowerCase().includes(input.toLowerCase())
}

// ---- Data loading ----
async function loadIncomes() {
  loading.value = true
  try {
    const res: any = await getIncomes(props.eventId, {
      keyword: filters.keyword || undefined,
      type: filters.type,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    })
    if (Array.isArray(res)) {
      incomeList.value = res
      pagination.total = res.length
    } else if (res?.items) {
      incomeList.value = res.items
      pagination.total = res.total ?? res.items.length
    } else {
      incomeList.value = []
    }
  } finally {
    loading.value = false
  }
}

async function loadSummary() {
  try {
    const res: any = await getIncomeSummary(props.eventId)
    if (res) summary.value = res
  } catch { /* ignore */ }
}

async function loadAttendees() {
  try {
    const res: any = await getAttendees(props.eventId, {})
    const list = Array.isArray(res) ? res : res?.items ?? []
    attendeeOptions.value = list.map((a: any) => ({ label: `${a.name}${a.organization ? ' - ' + a.organization : ''}`, value: a.id }))
  } catch { /* ignore */ }
}

async function loadData() {
  await Promise.all([loadIncomes(), loadSummary()])
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadIncomes()
}

// ---- Init ----
onMounted(() => {
  loadData()
  loadAttendees()
})

watch(() => props.eventId, () => {
  loadData()
  loadAttendees()
})
</script>

<style scoped lang="scss">
.finance-panel {
  padding: 0;
}
</style>
