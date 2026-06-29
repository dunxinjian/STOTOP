<template>
  <div class="page-container">
    <PageHeader title="拜访记录">
      <template #actions>
        <a-button v-if="has(CrmPermissions.VisitCreate)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增拜访
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-button :type="pendingOnly ? 'primary' : 'default'" ghost size="small" @click="togglePending">
            <FieldTimeOutlined />待跟进
            <a-badge v-if="pendingCount > 0" :count="pendingCount" :offset="[6, -4]" />
          </a-button>
          <a-input v-model:value="searchForm.customerName" placeholder="客户名称" allow-clear size="small" style="width: 140px" @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.visitorId" placeholder="拜访人" allow-clear show-search :filter-option="filterEmployee" :options="employeeOptions" size="small" style="width: 130px" />
          <a-range-picker v-model:value="searchForm.dateRange" value-format="YYYY-MM-DD" size="small" style="width: 220px" />
          <a-select v-model:value="searchForm.visitMethod" placeholder="拜访方式" allow-clear size="small" style="width: 110px" :options="methodOptions" />
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
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'customerName'">
            {{ record.customerName || '-' }}
          </template>
          <template v-if="column.dataIndex === 'visitorId'">
            {{ getEmployeeName(record.visitorId) }}
          </template>
          <template v-if="column.dataIndex === 'visitMethod'">
            <a-tag :color="methodColorMap[record.visitMethod] || 'default'">
              {{ methodTextMap[record.visitMethod] || '其他' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'content'">
            <a-tooltip :title="record.content">
              <span class="ellipsis-text">{{ record.content || '-' }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'nextFollowUpDate'">
            <span :class="{ 'overdue-text': isOverdue(record.nextFollowUpDate) }">
              {{ record.nextFollowUpDate || '-' }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="has(CrmPermissions.VisitEdit)" type="link" size="small" @click="handleEdit(record as VisitRecordListItemDto)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该拜访记录吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record as VisitRecordListItemDto)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无拜访记录" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增拜访记录' : '编辑拜访记录'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '110px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="客户" name="customerId">
          <a-select
            v-model:value="formData.customerId"
            placeholder="请选择客户"
            show-search
            :filter-option="filterCustomer"
            :options="customerOptions"
          />
        </a-form-item>
        <a-form-item label="拜访人" name="visitorId">
          <a-select
            v-model:value="formData.visitorId"
            placeholder="请选择拜访人"
            show-search
            :filter-option="filterEmployee"
            :options="employeeOptions"
          />
        </a-form-item>
        <a-form-item label="拜访日期" name="visitDate">
          <a-date-picker v-model:value="formData.visitDate" style="width: 100%" value-format="YYYY-MM-DD" />
        </a-form-item>
        <a-form-item label="拜访方式" name="visitMethod">
          <a-select v-model:value="formData.visitMethod" :options="methodOptions" placeholder="请选择" />
        </a-form-item>
        <a-form-item label="拜访内容">
          <a-textarea v-model:value="formData.content" :rows="4" placeholder="请输入拜访内容" :maxlength="2000" show-count />
        </a-form-item>
        <a-form-item label="下次跟进日期">
          <a-date-picker v-model:value="formData.nextFollowUpDate" style="width: 100%" value-format="YYYY-MM-DD" />
        </a-form-item>
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
import dayjs from 'dayjs'
import { PlusOutlined, EditOutlined, DeleteOutlined, FieldTimeOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { usePermission, CrmPermissions } from '@/utils/permission'
import {
  getVisitRecordList,
  getVisitRecordById,
  createVisitRecord,
  updateVisitRecord,
  deleteVisitRecord,
  getCustomerList,
  type VisitRecordListItemDto,
  type CreateVisitRecordRequest,
  type UpdateVisitRecordRequest,
} from '@/api/crm'
import { getEmployeeList, type EmployeeDto } from '@/api/hr'

const { has } = usePermission()

// 拜访方式
const methodOptions = [
  { label: '上门', value: 1 },
  { label: '电话', value: 2 },
  { label: '线上', value: 3 },
  { label: '其他', value: 4 },
]
const methodTextMap: Record<number, string> = { 1: '上门', 2: '电话', 3: '线上', 4: '其他' }
const methodColorMap: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'purple', 4: 'default' }

// 表格列
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '客户名称', dataIndex: 'customerName', key: 'customerName', width: 160, ellipsis: true },
  { title: '拜访人', dataIndex: 'visitorId', key: 'visitorId', width: 100 },
  { title: '拜访日期', dataIndex: 'visitDate', key: 'visitDate', width: 120 },
  { title: '拜访方式', dataIndex: 'visitMethod', key: 'visitMethod', width: 100, align: 'center' as const },
  { title: '内容', dataIndex: 'content', key: 'content', width: 200, ellipsis: true },
  { title: '下次跟进', dataIndex: 'nextFollowUpDate', key: 'nextFollowUpDate', width: 120 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 160, align: 'center' as const, fixed: 'right' as const },
]

// 搜索
const searchForm = reactive({
  customerName: '',
  visitorId: undefined as number | undefined,
  dateRange: undefined as [string, string] | undefined,
  visitMethod: undefined as number | undefined,
})
const pendingOnly = ref(false)
const pendingCount = ref(0)

// 数据
const loading = ref(false)
const tableData = ref<VisitRecordListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const employeeOptions = ref<{ label: string; value: number }[]>([])
const employeeMap = ref<Record<number, string>>({})
const customerOptions = ref<{ label: string; value: number }[]>([])

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
  fetchList()
}

// 弹窗
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  customerId: undefined as number | undefined,
  visitorId: undefined as number | undefined,
  visitDate: '',
  visitMethod: undefined as number | undefined,
  content: '',
  nextFollowUpDate: '',
})

const formRules: Record<string, Rule[]> = {
  customerId: [{ required: true, message: '请选择客户', trigger: 'change' }],
  visitorId: [{ required: true, message: '请选择拜访人', trigger: 'change' }],
  visitDate: [{ required: true, message: '请选择拜访日期', trigger: 'change' }],
  visitMethod: [{ required: true, message: '请选择拜访方式', trigger: 'change' }],
}

function getEmployeeName(id?: number) {
  return id ? (employeeMap.value[id] || '-') : '-'
}

function filterEmployee(input: string, option: any) {
  return option.label?.toLowerCase().includes(input.toLowerCase())
}

function filterCustomer(input: string, option: any) {
  return option.label?.toLowerCase().includes(input.toLowerCase())
}

function isOverdue(date?: string) {
  if (!date) return false
  return dayjs(date).isBefore(dayjs(), 'day') || dayjs(date).isSame(dayjs(), 'day')
}

async function fetchEmployees() {
  try {
    const res = await getEmployeeList({ pageSize: 9999 }) as any
    const items: EmployeeDto[] = res?.items || res || []
    employeeOptions.value = items.map((e) => ({ label: e.name, value: e.id }))
    employeeMap.value = Object.fromEntries(items.map((e) => [e.id, e.name]))
  } catch { /* ignore */ }
}

async function fetchCustomers() {
  try {
    const res = await getCustomerList({ pageSize: 9999 }) as any
    const items = res?.items || res || []
    customerOptions.value = items.map((c: any) => ({ label: c.shortName, value: c.id }))
  } catch { /* ignore */ }
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.visitorId !== undefined) params.visitorId = searchForm.visitorId
    if (searchForm.visitMethod !== undefined) params.visitMethod = searchForm.visitMethod
    if (searchForm.dateRange) {
      params.startDate = searchForm.dateRange[0]
      params.endDate = searchForm.dateRange[1]
    }
    const res = await getVisitRecordList(params) as any
    if (res) {
      let items = res?.items || res || []
      // 前端按客户名称过滤
      if (searchForm.customerName) {
        const kw = searchForm.customerName.toLowerCase()
        items = items.filter((r: any) => r.customerName?.toLowerCase().includes(kw))
      }
      // 待跟进过滤
      if (pendingOnly.value) {
        const today = dayjs().format('YYYY-MM-DD')
        items = items.filter((r: any) => r.nextFollowUpDate && r.nextFollowUpDate <= today)
      }
      tableData.value = items
      pagination.total = res?.total || items.length || 0
    }
    // 计算待跟进数
    computePendingCount()
  } finally {
    loading.value = false
  }
}

function computePendingCount() {
  const today = dayjs().format('YYYY-MM-DD')
  pendingCount.value = tableData.value.filter((r) => r.nextFollowUpDate && r.nextFollowUpDate <= today).length
}

function togglePending() {
  pendingOnly.value = !pendingOnly.value
  pagination.pageIndex = 1
  fetchList()
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.customerName = ''
  searchForm.visitorId = undefined
  searchForm.dateRange = undefined
  searchForm.visitMethod = undefined
  pendingOnly.value = false
  pagination.pageIndex = 1
  fetchList()
}

function resetForm() {
  formData.customerId = undefined
  formData.visitorId = undefined
  formData.visitDate = ''
  formData.visitMethod = undefined
  formData.content = ''
  formData.nextFollowUpDate = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: VisitRecordListItemDto) {
  dialogType.value = 'edit'
  currentId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getVisitRecordById(row.id) as any
    if (detail) {
      formData.customerId = detail.customerId
      formData.visitorId = detail.visitorId
      formData.visitDate = detail.visitDate || ''
      formData.visitMethod = detail.visitMethod
      formData.content = detail.content || ''
      formData.nextFollowUpDate = detail.nextFollowUpDate || ''
    }
  } catch (error) {
    console.error('获取拜访记录详情失败:', error)
  }
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      const data: CreateVisitRecordRequest = {
        customerId: formData.customerId!,
        visitorId: formData.visitorId!,
        visitDate: formData.visitDate,
        visitMethod: formData.visitMethod!,
        content: formData.content || undefined,
        nextFollowUpDate: formData.nextFollowUpDate || undefined,
      }
      await createVisitRecord(data)
      message.success('新增成功')
    } else {
      const data: UpdateVisitRecordRequest = {
        visitDate: formData.visitDate,
        visitMethod: formData.visitMethod!,
        content: formData.content || undefined,
        nextFollowUpDate: formData.nextFollowUpDate || undefined,
      }
      await updateVisitRecord(currentId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handleDelete(row: VisitRecordListItemDto) {
  try {
    await deleteVisitRecord(row.id)
    message.success('删除成功')
    fetchList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

onMounted(() => {
  fetchEmployees()
  fetchCustomers()
  fetchList()
})
</script>

<style scoped lang="scss">
.ellipsis-text {
  display: inline-block;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.overdue-text {
  color: var(--color-danger);
  font-weight: 600;
}
</style>
