<template>
  <div class="page-container">
    <PageHeader title="车辆分配" description="管理车辆分配与归还">
      <template #actions>
        <a-input-search v-model:value="searchForm.keyword" placeholder="车辆编码" style="width: 200px" @search="handleSearch" allowClear />
        <a-select v-model:value="searchForm.assignmentType" placeholder="分配类型" allow-clear style="width: 120px" :options="assignmentTypeOptions" @change="handleSearch" />
        <a-select v-model:value="searchForm.assignmentStatus" placeholder="分配状态" allow-clear style="width: 120px" :options="assignmentStatusOptions" @change="handleSearch" />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>分配车辆
        </a-button>
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
          <template v-if="column.dataIndex === 'vehicleCode'">
            <a-tooltip :title="record.vehicleCode">{{ record.vehicleCode }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'assignmentType'">
            <a-tag :color="record.assignmentType === 1 ? 'success' : 'blue'">
              {{ record.assignmentType === 1 ? '免费使用' : '租赁' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'startDate'">
            {{ formatDate(record.startDate) }}
          </template>
          <template v-if="column.dataIndex === 'endDate'">
            {{ record.endDate ? formatDate(record.endDate) : '-' }}
          </template>
          <template v-if="column.dataIndex === 'assignmentStatus'">
            <a-tag :color="record.assignmentStatus === 1 ? 'success' : 'default'">
              {{ record.assignmentStatus === 1 ? '使用中' : '已归还' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button
              v-if="record.assignmentStatus === 1"
              type="link"
              size="small"
              @click="handleReturn(record)"
            >
              <RollbackOutlined />归还
            </a-button>
            <span v-else class="text-muted">-</span>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无分配数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 分配车辆弹窗 -->
    <a-modal
      v-model:open="assignDialogVisible"
      title="分配车辆"
      width="500px"
      :destroy-on-close="true"
      @cancel="assignDialogVisible = false"
    >
      <a-form
        ref="assignFormRef"
        :model="assignFormData"
        :rules="assignFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="车辆" name="vehicleId">
          <a-select
            v-model:value="assignFormData.vehicleId"
            placeholder="请选择车辆"
            show-search
            :filter-option="filterVehicleOption"
            :options="availableVehicleOptions"
          />
        </a-form-item>
        <a-form-item label="员工ID" name="employeeId">
          <a-input-number
            v-model:value="assignFormData.employeeId"
            placeholder="请输入员工ID"
            style="width: 100%"
            :min="1"
          />
        </a-form-item>
        <a-form-item label="员工姓名" name="employeeName">
          <a-input
            v-model:value="assignFormData.employeeName"
            placeholder="请输入员工姓名"
            :maxlength="50"
          />
        </a-form-item>
        <a-form-item label="分配类型" name="assignmentType">
          <a-select
            v-model:value="assignFormData.assignmentType"
            placeholder="请选择分配类型"
            :options="assignmentTypeOptions.slice(1)"
          />
        </a-form-item>
        <a-form-item label="开始日期" name="startDate">
          <a-date-picker
            v-model:value="assignFormData.startDate"
            placeholder="请选择开始日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="assignFormData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="assignDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="assignLoading" @click="handleAssignSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 归还弹窗 -->
    <a-modal
      v-model:open="returnDialogVisible"
      title="归还车辆"
      width="400px"
      :destroy-on-close="true"
      @cancel="returnDialogVisible = false"
    >
      <a-form
        ref="returnFormRef"
        :model="returnFormData"
        :rules="returnFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="车辆编码">
          <span>{{ currentRecord?.vehicleCode }}</span>
        </a-form-item>
        <a-form-item label="员工姓名">
          <span>{{ currentRecord?.employeeName }}</span>
        </a-form-item>
        <a-form-item label="结束日期" name="endDate">
          <a-date-picker
            v-model:value="returnFormData.endDate"
            placeholder="请选择结束日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="returnFormData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="returnDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="returnLoading" @click="handleReturnSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, RollbackOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getAssignmentList,
  createAssignment,
  returnVehicle,
  getVehicleList,
  type VehicleAssignmentListItemDto,
  type VehicleListItemDto,
} from '@/api/vehicle'

// 选项配置
const assignmentTypeOptions = [
  { label: '全部', value: '' },
  { label: '免费使用', value: 1 },
  { label: '租赁', value: 2 },
]

const assignmentStatusOptions = [
  { label: '全部', value: '' },
  { label: '使用中', value: 1 },
  { label: '已归还', value: 2 },
]

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '车辆编码', dataIndex: 'vehicleCode', key: 'vehicleCode', width: 120 },
  { title: '员工姓名', dataIndex: 'employeeName', key: 'employeeName', width: 120 },
  { title: '分配类型', dataIndex: 'assignmentType', key: 'assignmentType', width: 100, align: 'center' as const },
  { title: '开始日期', dataIndex: 'startDate', key: 'startDate', width: 120 },
  { title: '结束日期', dataIndex: 'endDate', key: 'endDate', width: 120 },
  { title: '分配状态', dataIndex: 'assignmentStatus', key: 'assignmentStatus', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 100, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  assignmentType: '' as number | string,
  assignmentStatus: '' as number | string,
})

// 表格数据
const loading = ref(false)
const tableData = ref<VehicleAssignmentListItemDto[]>([])
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

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchAssignmentList()
}

// 可用车辆选项
const availableVehicleOptions = ref<{ label: string; value: number }[]>([])

// 分配弹窗
const assignDialogVisible = ref(false)
const assignFormRef = ref<FormInstance>()
const assignLoading = ref(false)

const assignFormData = reactive({
  vehicleId: undefined as number | undefined,
  employeeId: undefined as number | undefined,
  employeeName: '',
  assignmentType: 1,
  startDate: '',
  remark: '',
})

const assignFormRules: Record<string, Rule[]> = {
  vehicleId: [{ required: true, message: '请选择车辆', trigger: 'change' }],
  employeeId: [{ required: true, message: '请输入员工ID', trigger: 'blur' }],
  employeeName: [{ required: true, message: '请输入员工姓名', trigger: 'blur' }],
  assignmentType: [{ required: true, message: '请选择分配类型', trigger: 'change' }],
  startDate: [{ required: true, message: '请选择开始日期', trigger: 'change' }],
}

// 归还弹窗
const returnDialogVisible = ref(false)
const returnFormRef = ref<FormInstance>()
const returnLoading = ref(false)
const currentRecord = ref<VehicleAssignmentListItemDto | null>(null)

const returnFormData = reactive({
  endDate: '',
  remark: '',
})

const returnFormRules: Record<string, Rule[]> = {
  endDate: [{ required: true, message: '请选择结束日期', trigger: 'change' }],
}

// 获取分配列表
async function fetchAssignmentList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.assignmentType !== '' && searchForm.assignmentType !== undefined) {
      params.assignmentType = searchForm.assignmentType
    }
    if (searchForm.assignmentStatus !== '' && searchForm.assignmentStatus !== undefined) {
      params.assignmentStatus = searchForm.assignmentStatus
    }
    const res = await getAssignmentList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取可用车辆
async function fetchAvailableVehicles() {
  try {
    const res = await getVehicleList({
      pageIndex: 1,
      pageSize: 1000,
      vehicleStatus: 1, // 闲置状态
    })
    if (res) {
      availableVehicleOptions.value = (res.items || []).map((v: VehicleListItemDto) => ({
        label: `${v.code}${v.plateNumber ? ` (${v.plateNumber})` : ''}`,
        value: v.id,
      }))
    }
  } catch (error) {
    console.error('获取可用车辆失败:', error)
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchAssignmentList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.assignmentType = ''
  searchForm.assignmentStatus = ''
  pagination.pageIndex = 1
  fetchAssignmentList()
}

// 筛选车辆
function filterVehicleOption(input: string, option: any) {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

// 新增分配
async function handleAdd() {
  resetAssignForm()
  await fetchAvailableVehicles()
  assignDialogVisible.value = true
}

// 重置分配表单
function resetAssignForm() {
  assignFormData.vehicleId = undefined
  assignFormData.employeeId = undefined
  assignFormData.employeeName = ''
  assignFormData.assignmentType = 1
  assignFormData.startDate = ''
  assignFormData.remark = ''
}

// 提交分配
async function handleAssignSubmit() {
  if (!assignFormRef.value) return
  try {
    await assignFormRef.value.validate()
  } catch {
    return
  }

  assignLoading.value = true
  try {
    await createAssignment({
      vehicleId: assignFormData.vehicleId,
      employeeId: assignFormData.employeeId,
      employeeName: assignFormData.employeeName,
      assignmentType: assignFormData.assignmentType,
      startDate: assignFormData.startDate,
      remark: assignFormData.remark || undefined,
    })
    message.success('分配成功')
    assignDialogVisible.value = false
    fetchAssignmentList()
  } catch (error) {
    console.error('分配失败:', error)
    message.error('分配失败')
  } finally {
    assignLoading.value = false
  }
}

// 归还
function handleReturn(row: any) {
  currentRecord.value = row
  returnFormData.endDate = new Date().toISOString().split('T')[0]
  returnFormData.remark = ''
  returnDialogVisible.value = true
}

// 提交归还
async function handleReturnSubmit() {
  if (!returnFormRef.value) return
  try {
    await returnFormRef.value.validate()
  } catch {
    return
  }

  if (!currentRecord.value) return

  returnLoading.value = true
  try {
    await returnVehicle(currentRecord.value.id, {
      endDate: returnFormData.endDate,
      remark: returnFormData.remark || undefined,
    })
    message.success('归还成功')
    returnDialogVisible.value = false
    fetchAssignmentList()
  } catch (error) {
    console.error('归还失败:', error)
    message.error('归还失败')
  } finally {
    returnLoading.value = false
  }
}

onMounted(() => {
  fetchAssignmentList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.text-muted {
  color: #999;
}
</style>
