<template>
  <div class="gift-panel">
    <!-- 顶部操作栏 -->
    <SmartActionBar description="管理宾客礼金收入与回礼">
      <a-button type="primary" @click="showQuickRegister"><GiftOutlined />快速登记</a-button>
      <a-button @click="showBatchRegister"><UnorderedListOutlined />批量登记</a-button>
      <a-button @click="handleExport"><ExportOutlined />导出Excel</a-button>
    </SmartActionBar>

    <!-- 统计卡片 -->
    <a-row :gutter="16" style="margin-bottom:16px">
      <a-col :span="6">
        <StatCard title="总礼金金额" :value="'¥' + summary.totalAmount.toFixed(2)" color="var(--color-danger)" :clickable="false" />
      </a-col>
      <a-col :span="6">
        <StatCard title="红包/现金份数" :value="summary.cashCount + summary.transferCount" suffix="份" :clickable="false" />
      </a-col>
      <a-col :span="6">
        <StatCard title="礼物份数" :value="summary.giftCount" suffix="份" :clickable="false" />
      </a-col>
      <a-col :span="6">
        <StatCard title="待回礼数" :value="summary.pendingReturnCount" suffix="份" color="var(--color-warning)" :clickable="false" />
      </a-col>
    </a-row>

    <!-- 分组统计 -->
    <div v-if="summary.campSummaries?.length" class="camp-summary" style="margin-bottom:16px; display:flex; gap:16px; color:#595959; font-size:14px;">
      <span v-for="cs in summary.campSummaries" :key="cs.camp">
        {{ cs.camp }}：<strong style="color:var(--color-info)">¥{{ cs.totalAmount.toFixed(2) }}</strong>
      </span>
    </div>

    <!-- 列表表格 -->
    <a-table
      :columns="columns"
      :data-source="giftList"
      :loading="loading"
      row-key="id"
      :pagination="pagination"
      @change="handleTableChange"
      :scroll="{ x: 1000 }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'amount'">
          <span style="float:right">¥{{ (record.amount ?? 0).toFixed(2) }}</span>
        </template>
        <template v-else-if="column.dataIndex === 'registrationTime'">
          {{ record.registrationTime ? dayjs(record.registrationTime).format('YYYY-MM-DD HH:mm') : '' }}
        </template>
        <template v-else-if="column.dataIndex === 'isReturned'">
          <a-tag v-if="record.isReturned" color="green">已回礼</a-tag>
          <a-tag v-else color="orange">待回礼</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="showEditModal(record as GiftDto)"><EditOutlined />编辑</a-button>
            <a-button type="link" size="small" @click="showReturnModal(record as GiftDto)" :disabled="record.isReturned"><CheckOutlined />回礼</a-button>
            <a-popconfirm title="确定删除此记录？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger><DeleteOutlined />删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 快速登记 Modal -->
    <a-modal
      v-model:open="quickModalVisible"
      title="快速登记礼金"
      @ok="handleQuickSubmit"
      :confirm-loading="submitting"
      :width="520"
    >
      <a-form :model="quickForm" layout="vertical">
        <a-form-item label="宾客" required>
          <template v-if="!quickManualInput">
            <a-select
              v-model:value="quickForm.attendeeId"
              placeholder="搜索宾客姓名"
              show-search
              allowClear
              :options="attendeeOptions"
              :filter-option="filterAttendeeOption"
            />
            <a style="font-size:12px; margin-top:4px; display:inline-block" @click="switchQuickToManual">找不到？手动输入姓名</a>
          </template>
          <template v-else>
            <a-input
              v-model:value="quickForm.guestName"
              placeholder="输入宾客姓名"
              allowClear
            />
            <a style="font-size:12px; margin-top:4px; display:inline-block" @click="switchQuickToSelect">从列表选择宾客</a>
          </template>
        </a-form-item>
        <a-form-item label="金额" required>
          <a-input-number v-model:value="quickForm.amount" :min="0" prefix="¥" style="width:100%" placeholder="请输入金额" />
        </a-form-item>
        <a-form-item label="登记方式" required>
          <a-radio-group v-model:value="quickForm.registrationMethod" :options="methodOptions" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="quickForm.remark" placeholder="可选备注" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 编辑 Modal -->
    <a-modal
      v-model:open="editModalVisible"
      title="编辑礼金"
      @ok="handleEditSubmit"
      :confirm-loading="submitting"
      :width="520"
    >
      <a-form :model="editForm" layout="vertical">
        <a-form-item label="金额" required>
          <a-input-number v-model:value="editForm.amount" :min="0" prefix="¥" style="width:100%" />
        </a-form-item>
        <a-form-item label="登记方式">
          <a-radio-group v-model:value="editForm.registrationMethod" :options="methodOptions" />
        </a-form-item>
        <a-form-item label="礼物描述">
          <a-input v-model:value="editForm.giftDescription" placeholder="礼物描述（如有）" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="editForm.remark" placeholder="备注" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 批量登记 Modal -->
    <a-modal
      v-model:open="batchModalVisible"
      title="批量登记礼金"
      :width="800"
      @ok="handleBatchSubmit"
      :confirm-loading="batchSubmitting"
    >
      <a-alert message="为多位宾客批量登记礼金，可逐行填写不同金额" type="info" show-icon style="margin-bottom:16px" />
      <div v-for="(row, idx) in batchRows" :key="idx" style="display:flex; gap:8px; margin-bottom:8px; align-items:center">
        <a-select
          v-model:value="row.attendeeId"
          placeholder="选择宾客"
          show-search
          allowClear
          :options="attendeeOptions"
          :filter-option="filterAttendeeOption"
          style="flex:2"
        />
        <a-input-number v-model:value="row.amount" :min="0" prefix="¥" placeholder="金额" style="flex:1" />
        <a-select v-model:value="row.registrationMethod" :options="methodSelectOptions" placeholder="方式" style="flex:1" />
        <a-button type="text" danger @click="removeBatchRow(idx)" :disabled="batchRows.length <= 1"><DeleteOutlined /></a-button>
      </div>
      <a-button type="dashed" block @click="addBatchRow" style="margin-top:8px">+ 添加一行</a-button>
    </a-modal>

    <!-- 回礼 Modal -->
    <a-modal
      v-model:open="returnModalVisible"
      title="标记回礼"
      @ok="handleReturnSubmit"
      :confirm-loading="submitting"
      :width="480"
    >
      <a-form :model="returnForm" layout="vertical">
        <a-form-item label="回礼内容" required>
          <a-input v-model:value="returnForm.returnContent" placeholder="如：喜糖礼盒、伴手礼等" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="returnForm.remark" placeholder="可选备注" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  GiftOutlined, UnorderedListOutlined, ExportOutlined,
  EditOutlined, DeleteOutlined, CheckOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import SmartActionBar from '../components/SmartActionBar.vue'
import StatCard from '../components/StatCard.vue'
import {
  getGifts, createGift, updateGift, deleteGift,
  getGiftSummary, batchRegisterGifts, exportGifts, getAttendees,
} from '@/api/conference'
import type {
  GiftDto, CreateGiftRequest, GiftSummaryDto, AttendeeListItemDto,
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ---- State ----
const loading = ref(false)
const submitting = ref(false)
const batchSubmitting = ref(false)
const giftList = ref<GiftDto[]>([])
const summary = ref<GiftSummaryDto>({
  totalAmount: 0, totalCount: 0, cashCount: 0, transferCount: 0,
  giftCount: 0, returnedCount: 0, pendingReturnCount: 0, campSummaries: [],
})
const attendeeOptions = ref<{ label: string; value: number }[]>([])
const pagination = reactive({
  current: 1, pageSize: 10, total: 0,
  showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条`,
})

const methodOptions = [
  { label: '现金', value: '现金' },
  { label: '转账', value: '转账' },
  { label: '红包', value: '红包' },
  { label: '礼物', value: '礼物' },
]
const methodSelectOptions = methodOptions.map(o => ({ label: o.label, value: o.value }))

const columns = [
  { title: '宾客姓名', dataIndex: 'attendeeName', width: 100 },
  { title: '阵营', dataIndex: 'camp', width: 80 },
  { title: '宾客类型', dataIndex: 'guestType', width: 90 },
  { title: '金额', dataIndex: 'amount', width: 110, align: 'right' as const },
  { title: '登记方式', dataIndex: 'registrationMethod', width: 90 },
  { title: '登记时间', dataIndex: 'registrationTime', width: 150 },
  { title: '回礼状态', dataIndex: 'isReturned', width: 90 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const },
]

// ---- Quick Register ----
const quickModalVisible = ref(false)
const quickManualInput = ref(false)
const quickForm = reactive<CreateGiftRequest & { remark?: string; guestName?: string }>({
  attendeeId: undefined as any,
  guestName: '',
  amount: 0,
  registrationMethod: '现金',
  remark: '',
})

function switchQuickToManual() {
  quickManualInput.value = true
  quickForm.attendeeId = undefined as any
}

function switchQuickToSelect() {
  quickManualInput.value = false
  quickForm.guestName = ''
}

function showQuickRegister() {
  quickForm.attendeeId = undefined as any
  quickForm.guestName = ''
  quickForm.amount = 0
  quickForm.registrationMethod = '现金'
  quickForm.remark = ''
  quickManualInput.value = false
  quickModalVisible.value = true
}

async function handleQuickSubmit() {
  const hasAttendee = quickForm.attendeeId
  const hasGuestName = quickForm.guestName && quickForm.guestName.trim()
  if (!hasAttendee && !hasGuestName) { message.warning('请选择宾客或输入宾客姓名'); return }
  if (!quickForm.amount || quickForm.amount <= 0) { message.warning('请输入有效金额'); return }
  submitting.value = true
  try {
    const payload: any = {
      amount: quickForm.amount,
      registrationMethod: quickForm.registrationMethod,
      remark: quickForm.remark,
    }
    if (hasAttendee) {
      payload.attendeeId = quickForm.attendeeId
    } else {
      payload.guestName = quickForm.guestName!.trim()
    }
    await createGift(props.eventId, payload)
    message.success('礼金登记成功')
    quickModalVisible.value = false
    loadData()
  } finally {
    submitting.value = false
  }
}

// ---- Edit ----
const editModalVisible = ref(false)
const editingId = ref<number | null>(null)
const editForm = reactive({
  amount: 0,
  registrationMethod: '现金',
  giftDescription: '',
  remark: '',
})

function showEditModal(record: GiftDto) {
  editingId.value = record.id
  editForm.amount = record.amount
  editForm.registrationMethod = record.registrationMethod
  editForm.giftDescription = record.giftDescription || ''
  editForm.remark = record.remark || ''
  editModalVisible.value = true
}

async function handleEditSubmit() {
  if (!editingId.value) return
  submitting.value = true
  try {
    await updateGift(editingId.value, { ...editForm })
    message.success('已更新')
    editModalVisible.value = false
    loadData()
  } finally {
    submitting.value = false
  }
}

// ---- Batch Register ----
const batchModalVisible = ref(false)
interface BatchRow { attendeeId: number | undefined; amount: number; registrationMethod: string }
const batchRows = ref<BatchRow[]>([{ attendeeId: undefined, amount: 0, registrationMethod: '现金' }])

function showBatchRegister() {
  batchRows.value = [{ attendeeId: undefined, amount: 0, registrationMethod: '现金' }]
  batchModalVisible.value = true
}

function addBatchRow() {
  batchRows.value.push({ attendeeId: undefined, amount: 0, registrationMethod: '现金' })
}

function removeBatchRow(idx: number) {
  batchRows.value.splice(idx, 1)
}

async function handleBatchSubmit() {
  const validRows = batchRows.value.filter(r => r.attendeeId && r.amount > 0)
  if (!validRows.length) { message.warning('请至少填写一行有效数据'); return }
  batchSubmitting.value = true
  try {
    await batchRegisterGifts(props.eventId, {
      items: validRows.map(r => ({
        attendeeId: r.attendeeId!,
        amount: r.amount,
        registrationMethod: r.registrationMethod,
      })),
    })
    message.success(`成功登记 ${validRows.length} 条礼金`)
    batchModalVisible.value = false
    loadData()
  } finally {
    batchSubmitting.value = false
  }
}

// ---- Return Gift ----
const returnModalVisible = ref(false)
const returnGiftId = ref<number | null>(null)
const returnForm = reactive({ returnContent: '', remark: '' })

function showReturnModal(record: GiftDto) {
  returnGiftId.value = record.id
  returnForm.returnContent = ''
  returnForm.remark = ''
  returnModalVisible.value = true
}

async function handleReturnSubmit() {
  if (!returnGiftId.value) return
  if (!returnForm.returnContent) { message.warning('请填写回礼内容'); return }
  submitting.value = true
  try {
    await updateGift(returnGiftId.value, {
      isReturned: true,
      returnContent: returnForm.returnContent,
      remark: returnForm.remark || undefined,
    })
    message.success('已标记回礼')
    returnModalVisible.value = false
    loadData()
  } finally {
    submitting.value = false
  }
}

// ---- Delete ----
async function handleDelete(id: number) {
  await deleteGift(id)
  message.success('已删除')
  loadData()
}

// ---- Export ----
async function handleExport() {
  try {
    const res = await exportGifts(props.eventId) as any
    const blob = res instanceof Blob ? res : new Blob([res])
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `礼金记录_${dayjs().format('YYYYMMDD')}.xlsx`
    a.click()
    URL.revokeObjectURL(url)
    message.success('导出成功')
  } catch {
    message.error('导出失败')
  }
}

// ---- Helpers ----
function filterAttendeeOption(input: string, option: any) {
  return (option?.label ?? '').toLowerCase().includes(input.toLowerCase())
}

// ---- Data Loading ----
async function loadGifts() {
  loading.value = true
  try {
    const res: any = await getGifts(props.eventId)
    if (Array.isArray(res)) {
      giftList.value = res
      pagination.total = res.length
    } else if (res?.items) {
      giftList.value = res.items
      pagination.total = res.total ?? res.items.length
    } else {
      giftList.value = []
    }
  } finally {
    loading.value = false
  }
}

async function loadSummary() {
  try {
    const res: any = await getGiftSummary(props.eventId)
    if (res) summary.value = res
  } catch { /* ignore */ }
}

async function loadAttendees() {
  try {
    const res: any = await getAttendees(props.eventId, { pageSize: 9999 })
    const list = Array.isArray(res) ? res : res?.items ?? []
    const options: { label: string; value: number }[] = []
    list.forEach((a: any) => {
      // 添加主宾客
      options.push({
        label: `${a.name}${a.organization ? ' - ' + a.organization : ''}`,
        value: a.id,
      })
      // 添加随行人员
      if (a.companions && a.companions.length > 0) {
        a.companions.forEach((c: any) => {
          options.push({
            label: `${c.name}（${a.name}的${c.relation || '随行'}）`,
            value: c.id,
          })
        })
      }
    })
    attendeeOptions.value = options
  } catch { /* ignore */ }
}

async function loadData() {
  await Promise.all([loadGifts(), loadSummary()])
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadGifts()
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
.gift-panel {
  padding: 0;
}

.camp-summary {
  background: #fafafa;
  border-radius: 6px;
  padding: 10px 16px;
}
</style>
