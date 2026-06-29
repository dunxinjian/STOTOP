<template>
  <div class="page-container">
    <PageHeader title="费用管理" description="管理宿舍各项费用">
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增费用
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-form-item label="房间" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.roomId"
                placeholder="全部房间"
                allow-clear
                style="width: 150px"
                :options="roomOptions"
              />
            </a-form-item>
            <a-form-item label="费用类型" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.expenseType"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="expenseTypeOptions"
              />
            </a-form-item>
            <a-form-item label="月份" style="margin-bottom:0">
              <a-date-picker
                v-model:value="searchForm.month"
                placeholder="选择月份"
                picker="month"
                style="width: 150px"
                value-format="YYYY-MM"
              />
            </a-form-item>
            <a-form-item label="状态" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.status"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="statusOptions"
              />
            </a-form-item>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-button type="primary" @click="handleSearch">
              <template #icon><SearchOutlined /></template>查询
            </a-button>
            <a-button @click="handleReset">
              <template #icon><ReloadOutlined /></template>重置
            </a-button>
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
        :scroll="{ x: 1100 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'roomInfo'">
            <span>{{ record.buildingName }} - {{ record.roomNumber }}</span>
          </template>
          <template v-if="column.dataIndex === 'expenseType'">
            {{ getExpenseTypeLabel(record.expenseType) }}
          </template>
          <template v-if="column.dataIndex === 'amount'">
            <span class="amount-cell">{{ formatAmount(record.amount) }}</span>
          </template>
          <template v-if="column.dataIndex === 'month'">
            {{ record.month }}
          </template>
          <template v-if="column.dataIndex === 'shareMethod'">
            {{ getShareMethodLabel(record.shareMethod) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewAllocation(record)">
              <PieChartOutlined />分摊
            </a-button>
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该费用记录吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无费用数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增费用' : '编辑费用'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="房间" name="roomId">
          <a-select
            v-model:value="formData.roomId"
            placeholder="请选择房间"
            :options="roomOptions"
          />
        </a-form-item>
        <a-form-item label="费用类型" name="expenseType">
          <a-select
            v-model:value="formData.expenseType"
            placeholder="请选择费用类型"
            :options="expenseTypeOptions"
          />
        </a-form-item>
        <a-form-item label="金额" name="amount">
          <a-input-number
            v-model:value="formData.amount"
            placeholder="请输入金额"
            style="width: 100%"
            :min="0"
            :precision="2"
            prefix="¥"
          />
        </a-form-item>
        <a-form-item label="月份" name="month">
          <a-date-picker
            v-model:value="formData.month"
            placeholder="请选择月份"
            picker="month"
            style="width: 100%"
            value-format="YYYY-MM"
          />
        </a-form-item>
        <a-form-item label="分摊方式" name="shareMethod">
          <a-select
            v-model:value="formData.shareMethod"
            placeholder="请选择分摊方式"
            :options="shareMethodOptions"
          />
        </a-form-item>
        <a-form-item v-if="dialogType === 'edit'" label="缴费状态" name="status">
          <a-select v-model:value="formData.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="formData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 分摊明细弹窗 -->
    <a-modal v-model:open="allocationVisible" title="费用分摊明细" :width="560" :footer="null">
      <a-spin :spinning="allocationLoading">
        <template v-if="allocationData">
          <a-descriptions :column="2" size="small" bordered style="margin-bottom: 16px">
            <a-descriptions-item label="房间">{{ allocationData.roomNumber }}</a-descriptions-item>
            <a-descriptions-item label="分摊方式">{{ getShareMethodLabel(allocationData.shareMethod) }}</a-descriptions-item>
            <a-descriptions-item label="费用总额">{{ formatAmount(allocationData.expenseAmount) }}</a-descriptions-item>
            <a-descriptions-item label="在住人数">{{ allocationData.occupantCount }} 人</a-descriptions-item>
          </a-descriptions>
          <a-table
            :columns="allocationColumns"
            :data-source="allocationData.shares"
            :pagination="false"
            row-key="employeeId"
            size="small"
            bordered
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'amount'">
                <span class="amount-cell">{{ formatAmount(record.amount) }}</span>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="该房间当前无在住人员，暂无可分摊对象" />
            </template>
          </a-table>
          <div class="allocation-total">
            合计分摊：<span class="amount-cell">{{ formatAmount(allocationData.allocatedTotal) }}</span>
          </div>
        </template>
      </a-spin>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined, PieChartOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getExpenseList,
  createExpense,
  updateExpense,
  deleteExpense,
  getAllBuildings,
  getRoomList,
  getExpenseAllocation,
  type ExpenseDto,
  type ExpenseAllocationDto,
  type BuildingListItemDto,
  type RoomDto,
} from '@/api/dormitory'

// 费用类型选项
const expenseTypeOptions = [
  { label: '水费', value: 'Water' },
  { label: '电费', value: 'Electricity' },
  { label: '住宿费', value: 'Accommodation' },
  { label: '其他', value: 'Other' },
]

// 分摊方式选项（均摊=总额按人数等分；固定=每人按金额收取）
const shareMethodOptions = [
  { label: '均摊', value: 'Equal' },
  { label: '固定', value: 'Fixed' },
]

// 状态选项
const statusOptions = [
  { label: '待缴', value: 0 },
  { label: '已缴', value: 1 },
  { label: '减免', value: 2 },
]

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '房间信息', dataIndex: 'roomInfo', key: 'roomInfo', width: 180 },
  { title: '费用类型', dataIndex: 'expenseType', key: 'expenseType', width: 100 },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 120, align: 'right' as const },
  { title: '月份', dataIndex: 'month', key: 'month', width: 100 },
  { title: '分摊方式', dataIndex: 'shareMethod', key: 'shareMethod', width: 100 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  roomId: undefined as number | undefined,
  expenseType: undefined as string | undefined,
  month: '',
  status: undefined as number | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<ExpenseDto[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchExpenseList()
}

// 房间选项
const roomOptions = ref<{ label: string; value: number }[]>([])

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentExpenseId = ref<number | null>(null)

const formData = reactive({
  roomId: undefined as number | undefined,
  expenseType: undefined as string | undefined,
  amount: undefined as number | undefined,
  month: '',
  shareMethod: 'Equal' as string,
  status: 0 as number,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  roomId: [{ required: true, message: '请选择房间', trigger: 'change' }],
  expenseType: [{ required: true, message: '请选择费用类型', trigger: 'change' }],
  amount: [{ required: true, message: '请输入金额', trigger: 'blur' }],
  month: [{ required: true, message: '请选择月份', trigger: 'change' }],
}

// 获取费用列表
async function fetchExpenseList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.roomId) params.roomId = searchForm.roomId
    if (searchForm.expenseType) params.expenseType = searchForm.expenseType
    if (searchForm.month) params.month = searchForm.month
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getExpenseList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取房间列表
async function fetchRooms() {
  try {
    const buildings = await getAllBuildings()
    const allRooms: { label: string; value: number }[] = []
    for (const building of buildings || []) {
      const rooms = await getRoomList(building.id)
      for (const room of rooms || []) {
        allRooms.push({
          label: `${building.name} - ${room.roomNumber}`,
          value: room.id,
        })
      }
    }
    roomOptions.value = allRooms
  } catch (error) {
    console.error('获取房间列表失败:', error)
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchExpenseList()
}

// 重置搜索
function handleReset() {
  searchForm.roomId = undefined
  searchForm.expenseType = undefined
  searchForm.month = ''
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchExpenseList()
}

// 重置表单
function resetForm() {
  formData.roomId = undefined
  formData.expenseType = undefined
  formData.amount = undefined
  formData.month = ''
  formData.shareMethod = 'Equal'
  formData.status = 0
  formData.remark = ''
  currentExpenseId.value = null
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  resetForm()
  dialogVisible.value = true
}

// 编辑
function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentExpenseId.value = row.id
  formData.roomId = row.roomId
  formData.expenseType = row.expenseType
  formData.amount = row.amount
  formData.month = row.month
  formData.shareMethod = row.shareMethod || 'Equal'
  formData.status = row.status ?? 0
  formData.remark = row.remark || ''
  dialogVisible.value = true
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const data = {
      roomId: formData.roomId!,
      expenseType: formData.expenseType!,
      amount: formData.amount!,
      month: formData.month,
      shareMethod: formData.shareMethod,
      remark: formData.remark || undefined,
    }

    if (dialogType.value === 'add') {
      await createExpense(data)
      message.success('新增成功')
    } else {
      await updateExpense(currentExpenseId.value!, { ...data, status: formData.status })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchExpenseList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deleteExpense(row.id)
    message.success('删除成功')
    fetchExpenseList()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

// ===== 费用分摊明细 =====
const allocationVisible = ref(false)
const allocationLoading = ref(false)
const allocationData = ref<ExpenseAllocationDto | null>(null)
const allocationColumns = [
  { title: '员工', dataIndex: 'employeeName', key: 'employeeName' },
  { title: '工号', dataIndex: 'employeeId', key: 'employeeId', width: 100 },
  { title: '应分摊金额', dataIndex: 'amount', key: 'amount', width: 140, align: 'right' as const },
]

async function handleViewAllocation(row: any) {
  allocationVisible.value = true
  allocationLoading.value = true
  allocationData.value = null
  try {
    allocationData.value = await getExpenseAllocation(row.id)
  } catch (e) {
    console.error('获取分摊明细失败:', e)
    message.error('获取分摊明细失败')
  } finally {
    allocationLoading.value = false
  }
}

// 格式化金额
function formatAmount(amount: number) {
  return `¥${(amount || 0).toFixed(2)}`
}

// 获取费用类型标签
function getExpenseTypeLabel(type: string) {
  const map: Record<string, string> = {
    Water: '水费',
    Electricity: '电费',
    Accommodation: '住宿费',
    Other: '其他',
  }
  return map[type] || type
}

// 获取分摊方式标签
function getShareMethodLabel(method?: string) {
  if (!method) return '—'
  const map: Record<string, string> = {
    Equal: '均摊',
    PerPerson: '均摊', // 兼容历史数据
    Fixed: '固定',
  }
  return map[method] || method
}

// 获取状态标签
function getStatusLabel(status: number) {
  const map: Record<number, string> = {
    0: '待缴',
    1: '已缴',
    2: '减免',
  }
  return map[status] || '未知'
}

// 获取状态颜色
function getStatusColor(status: number) {
  const map: Record<number, string> = {
    0: 'orange',
    1: 'green',
    2: 'blue',
  }
  return map[status] || 'default'
}

onMounted(() => {
  fetchExpenseList()
  fetchRooms()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.amount-cell {
  font-family: monospace;
  font-weight: 500;
}

.allocation-total {
  margin-top: 12px;
  text-align: right;
  font-weight: 600;
}
</style>
