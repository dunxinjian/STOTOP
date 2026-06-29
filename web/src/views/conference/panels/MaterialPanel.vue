<template>
  <div class="material-panel">
    <!-- 工具栏 -->
    <div class="toolbar" style="margin-bottom:16px; display:flex; gap:8px; align-items:center">
      <a-button type="primary" @click="showAddModal"><PlusOutlined />登记物品</a-button>
      <a-select v-model:value="filters.category" placeholder="分类筛选" allowClear style="width:120px"
        :options="categoryOptions" @change="loadMaterials" />
      <a-select v-model:value="filters.status" placeholder="状态筛选" allowClear style="width:120px"
        :options="statusOptions" @change="loadMaterials" />
      <a-input-search v-model:value="filters.keyword" placeholder="物品名称" style="width:180px" @search="loadMaterials" />
    </div>

    <!-- 统计栏 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="6"><StatCard title="物品总数" :value="summary.totalCount" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="已到位" :value="summary.receivedCount" :progress="receiveProgress" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="待采购" :value="summary.pendingCount" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="总费用" :value="summary.totalCost" suffix="元" :clickable="false" /></a-col>
    </a-row>

    <!-- 物品表格 -->
    <a-table
      :columns="columns"
      :data-source="materialList"
      :loading="loading"
      row-key="id"
      :pagination="pagination"
      :scroll="{ x: 1200 }"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'category'">
          <a-tag v-if="record.category === '采购'" color="blue">采购</a-tag>
          <a-tag v-else-if="record.category === '租赁'" color="green">租赁</a-tag>
          <a-tag v-else color="default">{{ record.category || '自备' }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'unitPrice'">
          ¥{{ (record.unitPrice ?? 0).toFixed(2) }}
        </template>
        <template v-else-if="column.dataIndex === 'totalPrice'">
          ¥{{ (record.totalPrice ?? 0).toFixed(2) }}
        </template>
        <template v-else-if="column.dataIndex === 'status'">
          <a-badge v-if="record.status === '待采购'" status="default" text="待采购" />
          <a-badge v-else-if="record.status === '已采购'" status="processing" text="已采购" />
          <a-badge v-else-if="record.status === '已到位'" status="success" text="已到位" />
          <a-badge v-else-if="record.status === '已归还'" status="default" text="已归还" />
          <span v-else>{{ record.status }}</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button v-if="record.status === '已采购'" type="link" size="small" @click="handleReceive(record as MaterialDto)">到货确认</a-button>
            <a-button v-if="record.acquisitionMethod === '租赁' && record.status === '已到位'" type="link" size="small" @click="handleReturn(record as MaterialDto)">归还</a-button>
            <a-button type="link" size="small" @click="showEditModal(record as MaterialDto)">编辑</a-button>
            <a-popconfirm title="确定删除此物品？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑 Modal -->
    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? '编辑物品' : '登记物品'"
      @ok="handleSubmit"
      :confirm-loading="submitting"
      :width="600"
    >
      <a-form :model="form" layout="vertical" ref="formRef">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="物品名称" name="name" :rules="[{ required: true, message: '请输入物品名称' }]">
              <a-input v-model:value="form.name" placeholder="请输入" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="分类" name="acquisitionMethod">
              <a-select v-model:value="form.acquisitionMethod" placeholder="请选择" :options="acquisitionMethodOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="数量" name="requiredQuantity">
              <a-input-number v-model:value="form.requiredQuantity" :min="1" style="width:100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="单位" name="unit">
              <a-input v-model:value="form.unit" placeholder="个/套/箱" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="单价" name="unitPrice">
              <a-input-number v-model:value="form.unitPrice" :min="0" prefix="¥" style="width:100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="供应商" name="supplier">
              <a-input v-model:value="form.supplier" placeholder="请输入" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="关联日程" name="scheduleId">
              <a-select v-model:value="form.scheduleId" placeholder="选择日程（可选）" allowClear :options="scheduleOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注" name="remark">
          <a-textarea v-model:value="form.remark" placeholder="请输入备注" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import StatCard from '../components/StatCard.vue'
import {
  getMaterials, createMaterial, updateMaterial, deleteMaterial,
  receiveMaterial, returnMaterial, getSchedules, getMaterialSummary,
} from '@/api/conference'
import type {
  MaterialDto, CreateMaterialRequest, UpdateMaterialRequest,
  MaterialSummaryDto, MaterialReceiveRequest, MaterialReturnRequest,
} from '@/api/conference'
import dayjs from 'dayjs'

const props = defineProps<{ eventId: number; eventData?: any }>()

const loading = ref(false)
const submitting = ref(false)
const materialList = ref<MaterialDto[]>([])
const summary = ref<MaterialSummaryDto>({ totalCount: 0, receivedCount: 0, pendingCount: 0, totalCost: 0, categorySummaries: [] })
const scheduleOptions = ref<{ label: string; value: number }[]>([])

const filters = reactive({ category: undefined as string | undefined, status: undefined as string | undefined, keyword: '' })
const pagination = reactive({ current: 1, pageSize: 10, total: 0, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` })

const categoryOptions = [
  { label: '采购', value: '采购' },
  { label: '租赁', value: '租赁' },
  { label: '自备', value: '自备' },
]
const statusOptions = [
  { label: '待采购', value: '待采购' },
  { label: '已采购', value: '已采购' },
  { label: '已到位', value: '已到位' },
  { label: '已归还', value: '已归还' },
]
const acquisitionMethodOptions = [
  { label: '采购', value: '采购' },
  { label: '租赁', value: '租赁' },
  { label: '自备', value: '自备' },
]

const receiveProgress = computed(() => {
  if (summary.value.totalCount === 0) return 0
  return Math.round((summary.value.receivedCount / summary.value.totalCount) * 100)
})

const columns = [
  { title: '物品名称', dataIndex: 'name', width: 140 },
  { title: '分类', dataIndex: 'category', width: 80 },
  { title: '数量', dataIndex: 'requiredQuantity', width: 70 },
  { title: '单位', dataIndex: 'unit', width: 60 },
  { title: '单价', dataIndex: 'unitPrice', width: 100 },
  { title: '总价', dataIndex: 'totalPrice', width: 100 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '供应商', dataIndex: 'supplier', width: 120 },
  { title: '关联日程', dataIndex: 'scheduleTitle', width: 120 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const },
]

// ---- Modal ----
const modalVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref()
const form = reactive<CreateMaterialRequest>({
  name: '',
  acquisitionMethod: undefined,
  requiredQuantity: 1,
  unit: '个',
  unitPrice: 0,
  supplier: '',
  scheduleId: undefined,
  remark: '',
})

function resetForm() {
  form.name = ''
  form.acquisitionMethod = undefined
  form.requiredQuantity = 1
  form.unit = '个'
  form.unitPrice = 0
  form.supplier = ''
  form.scheduleId = undefined
  form.remark = ''
}

function showAddModal() {
  editingId.value = null
  resetForm()
  modalVisible.value = true
}

function showEditModal(record: MaterialDto) {
  editingId.value = record.id
  form.name = record.name
  form.acquisitionMethod = record.acquisitionMethod
  form.requiredQuantity = record.requiredQuantity
  form.unit = record.unit
  form.unitPrice = record.unitPrice
  form.supplier = record.supplier
  form.scheduleId = record.scheduleId
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
    if (editingId.value) {
      await updateMaterial(editingId.value, { ...form } as UpdateMaterialRequest)
      message.success('物品已更新')
    } else {
      await createMaterial(props.eventId, { ...form })
      message.success('物品已登记')
    }
    modalVisible.value = false
    loadMaterials()
    loadSummary()
  } finally {
    submitting.value = false
  }
}

// ---- Data loading ----
async function loadMaterials() {
  loading.value = true
  try {
    const res: any = await getMaterials(props.eventId, {
      keyword: filters.keyword || undefined,
      category: filters.category,
      status: filters.status,
      acquisitionMethod: filters.category,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    })
    if (Array.isArray(res)) {
      materialList.value = res
      pagination.total = res.length
    } else if (res?.items) {
      materialList.value = res.items
      pagination.total = res.total ?? res.items.length
    } else {
      materialList.value = []
    }
  } finally {
    loading.value = false
  }
}

async function loadSummary() {
  try {
    const res: any = await getMaterialSummary(props.eventId)
    if (res) summary.value = res
  } catch { /* ignore */ }
}

async function loadSchedules() {
  try {
    const res: any = await getSchedules(props.eventId)
    const list = Array.isArray(res) ? res : res?.items ?? []
    scheduleOptions.value = list.map((s: any) => ({ label: s.title, value: s.id }))
  } catch { /* ignore */ }
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadMaterials()
}

// ---- Actions ----
async function handleReceive(record: MaterialDto) {
  const req: MaterialReceiveRequest = {
    receivedQuantity: record.requiredQuantity,
    receivedDate: dayjs().format('YYYY-MM-DD'),
  }
  await receiveMaterial(record.id, req)
  message.success('已确认到货')
  loadMaterials()
  loadSummary()
}

async function handleReturn(record: MaterialDto) {
  const req: MaterialReturnRequest = {
    returnDate: dayjs().format('YYYY-MM-DD'),
  }
  await returnMaterial(record.id, req)
  message.success('已归还')
  loadMaterials()
  loadSummary()
}

async function handleDelete(id: number) {
  await deleteMaterial(id)
  message.success('已删除')
  loadMaterials()
  loadSummary()
}

// ---- Init ----
onMounted(() => {
  loadMaterials()
  loadSummary()
  loadSchedules()
})

watch(() => props.eventId, () => {
  loadMaterials()
  loadSummary()
  loadSchedules()
})
</script>

<style scoped lang="scss">
.material-panel {
  padding: 0;
}
</style>
