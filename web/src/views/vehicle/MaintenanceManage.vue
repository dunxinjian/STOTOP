<template>
  <div class="page-container">
    <PageHeader title="维修记录" description="管理三轮车维修保养信息">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增维修
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="车辆编码" allow-clear style="width: 160px" @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.maintenanceType" size="small" placeholder="维修类型" allow-clear style="width: 120px" :options="maintenanceTypeOptions" />
          <a-select v-model:value="searchForm.maintenanceStatus" size="small" placeholder="维修状态" allow-clear style="width: 120px" :options="maintenanceStatusOptions" />
          <a-range-picker v-model:value="searchForm.dateRange" size="small" style="width: 240px" value-format="YYYY-MM-DD" :placeholder="['开始日期', '结束日期']" />
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
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'maintenanceType'">
            {{ getMaintenanceTypeLabel(record.maintenanceType) }}
          </template>
          <template v-if="column.dataIndex === 'maintenanceItem'">
            <a-tooltip :title="record.maintenanceItem">
              <span class="ellipsis-cell">{{ record.maintenanceItem }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'maintenanceCost'">
            <span class="amount-cell">{{ formatAmount(record.maintenanceCost) }}</span>
          </template>
          <template v-if="column.dataIndex === 'costBearer'">
            <a-tag :color="record.costBearer === 1 ? 'blue' : 'orange'">
              {{ record.costBearer === 1 ? '公司' : '员工' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'completionDate'">
            {{ record.completionDate || '-' }}
          </template>
          <template v-if="column.dataIndex === 'maintenanceStatus'">
            <a-tag :color="record.maintenanceStatus === 1 ? 'orange' : 'green'">
              {{ record.maintenanceStatus === 1 ? '维修中' : '已完成' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <template v-if="record.maintenanceStatus === 1">
              <a-button type="link" size="small" @click="handleComplete(record)">
                <CheckOutlined />完成维修
              </a-button>
            </template>
            <span v-else class="text-gray">-</span>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无维修记录" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增维修弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      title="新增维修记录"
      width="700px"
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
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="车辆" name="vehicleId">
              <a-select
                v-model:value="formData.vehicleId"
                placeholder="请选择车辆"
                show-search
                :filter-option="filterVehicleOption"
                :options="vehicleOptions"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="维修日期" name="maintenanceDate">
              <a-date-picker
                v-model:value="formData.maintenanceDate"
                placeholder="请选择维修日期"
                style="width: 100%"
                value-format="YYYY-MM-DD"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="维修类型">
              <a-select
                v-model:value="formData.maintenanceType"
                placeholder="请选择维修类型"
                :options="maintenanceTypeOptions"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="维修单位">
              <a-input
                v-model:value="formData.maintenanceUnit"
                placeholder="请输入维修单位"
                :maxlength="100"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="维修费用">
              <a-input-number
                v-model:value="formData.maintenanceCost"
                placeholder="请输入维修费用"
                style="width: 100%"
                :min="0"
                :precision="2"
                prefix="¥"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="承担方" name="costBearer">
              <a-select
                v-model:value="formData.costBearer"
                placeholder="请选择承担方"
                :options="costBearerOptions"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="维修项目" name="maintenanceItem">
              <a-textarea
                v-model:value="formData.maintenanceItem"
                :rows="2"
                placeholder="请输入维修项目"
                :maxlength="500"
                show-count
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea
                v-model:value="formData.remark"
                :rows="2"
                placeholder="请输入备注"
                :maxlength="500"
                show-count
              />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 完成维修弹窗 -->
    <a-modal
      v-model:open="completeDialogVisible"
      title="完成维修"
      width="500px"
      :destroy-on-close="true"
      @cancel="completeDialogVisible = false"
    >
      <a-form
        ref="completeFormRef"
        :model="completeFormData"
        :rules="completeFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="完成日期" name="completionDate">
          <a-date-picker
            v-model:value="completeFormData.completionDate"
            placeholder="请选择完成日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="completeFormData.remark"
            :rows="3"
            placeholder="请输入备注"
            :maxlength="500"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="completeDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="completeLoading" @click="handleCompleteSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, CheckOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getMaintenanceList,
  createMaintenance,
  completeMaintenance,
  getVehicleList,
  type VehicleMaintenanceListItemDto,
  type VehicleListItemDto,
} from '@/api/vehicle'

// 维修类型选项
const maintenanceTypeOptions = [
  { label: '日常保养', value: 'Daily' },
  { label: '故障维修', value: 'Fault' },
  { label: '事故维修', value: 'Accident' },
]

// 维修状态选项
const maintenanceStatusOptions = [
  { label: '维修中', value: 1 },
  { label: '已完成', value: 2 },
]

// 承担方选项
const costBearerOptions = [
  { label: '公司承担', value: 1 },
  { label: '员工承担', value: 2 },
]

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '车辆编码', dataIndex: 'vehicleCode', key: 'vehicleCode', width: 100 },
  { title: '维修日期', dataIndex: 'maintenanceDate', key: 'maintenanceDate', width: 110 },
  { title: '维修类型', dataIndex: 'maintenanceType', key: 'maintenanceType', width: 100 },
  { title: '维修项目', dataIndex: 'maintenanceItem', key: 'maintenanceItem', width: 200, ellipsis: true },
  { title: '维修单位', dataIndex: 'maintenanceUnit', key: 'maintenanceUnit', width: 120, ellipsis: true },
  { title: '维修费用', dataIndex: 'maintenanceCost', key: 'maintenanceCost', width: 110, align: 'right' as const },
  { title: '承担方', dataIndex: 'costBearer', key: 'costBearer', width: 80, align: 'center' as const },
  { title: '完成日期', dataIndex: 'completionDate', key: 'completionDate', width: 110 },
  { title: '维修状态', dataIndex: 'maintenanceStatus', key: 'maintenanceStatus', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  maintenanceType: undefined as string | undefined,
  maintenanceStatus: undefined as number | undefined,
  dateRange: undefined as [string, string] | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<VehicleMaintenanceListItemDto[]>([])
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
  fetchMaintenanceList()
}

// 车辆选项
const vehicleOptions = ref<{ label: string; value: number }[]>([])

// 新增弹窗相关
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)

const formData = reactive({
  vehicleId: undefined as number | undefined,
  maintenanceDate: '',
  maintenanceType: undefined as string | undefined,
  maintenanceItem: '',
  maintenanceUnit: '',
  maintenanceCost: undefined as number | undefined,
  costBearer: undefined as number | undefined,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  vehicleId: [{ required: true, message: '请选择车辆', trigger: 'change' }],
  maintenanceDate: [{ required: true, message: '请选择维修日期', trigger: 'change' }],
  maintenanceItem: [{ required: true, message: '请输入维修项目', trigger: 'blur' }],
  costBearer: [{ required: true, message: '请选择承担方', trigger: 'change' }],
}

// 完成维修弹窗相关
const completeDialogVisible = ref(false)
const completeFormRef = ref<FormInstance>()
const completeLoading = ref(false)
const currentMaintenanceId = ref<number | null>(null)

const completeFormData = reactive({
  completionDate: '',
  remark: '',
})

const completeFormRules: Record<string, Rule[]> = {
  completionDate: [{ required: true, message: '请选择完成日期', trigger: 'change' }],
}

// 获取维修列表
async function fetchMaintenanceList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.maintenanceType) params.maintenanceType = searchForm.maintenanceType
    if (searchForm.maintenanceStatus) params.maintenanceStatus = searchForm.maintenanceStatus
    if (searchForm.dateRange && searchForm.dateRange.length === 2) {
      params.startDate = searchForm.dateRange[0]
      params.endDate = searchForm.dateRange[1]
    }
    const res = await getMaintenanceList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取车辆列表
async function fetchVehicleList() {
  try {
    const res = await getVehicleList({
      pageIndex: 1,
      pageSize: 1000,
    })
    vehicleOptions.value = (res?.items || []).map((item: VehicleListItemDto) => ({
      label: item.code,
      value: item.id,
    }))
  } catch (error) {
    console.error('获取车辆列表失败:', error)
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchMaintenanceList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.maintenanceType = undefined
  searchForm.maintenanceStatus = undefined
  searchForm.dateRange = undefined
  pagination.pageIndex = 1
  fetchMaintenanceList()
}

// 重置表单
function resetForm() {
  formData.vehicleId = undefined
  formData.maintenanceDate = new Date().toISOString().split('T')[0]
  formData.maintenanceType = undefined
  formData.maintenanceItem = ''
  formData.maintenanceUnit = ''
  formData.maintenanceCost = undefined
  formData.costBearer = undefined
  formData.remark = ''
}

// 新增
function handleAdd() {
  resetForm()
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
      vehicleId: formData.vehicleId!,
      maintenanceDate: formData.maintenanceDate,
      maintenanceType: formData.maintenanceType || undefined,
      maintenanceItem: formData.maintenanceItem,
      maintenanceUnit: formData.maintenanceUnit || undefined,
      maintenanceCost: formData.maintenanceCost || undefined,
      costBearer: formData.costBearer!,
      remark: formData.remark || undefined,
    }

    await createMaintenance(data)
    message.success('新增成功')
    dialogVisible.value = false
    fetchMaintenanceList()
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    submitLoading.value = false
  }
}

// 完成维修
function handleComplete(row: any) {
  currentMaintenanceId.value = row.id
  completeFormData.completionDate = new Date().toISOString().split('T')[0]
  completeFormData.remark = ''
  completeDialogVisible.value = true
}

// 提交完成维修
async function handleCompleteSubmit() {
  if (!completeFormRef.value || !currentMaintenanceId.value) return
  try {
    await completeFormRef.value.validate()
  } catch {
    return
  }

  completeLoading.value = true
  try {
    await completeMaintenance(currentMaintenanceId.value, {
      completionDate: completeFormData.completionDate,
      remark: completeFormData.remark || undefined,
    })
    message.success('维修已完成')
    completeDialogVisible.value = false
    fetchMaintenanceList()
  } catch (error) {
    console.error('完成维修失败:', error)
    message.error('完成维修失败')
  } finally {
    completeLoading.value = false
  }
}

// 车辆搜索过滤
function filterVehicleOption(input: string, option: any) {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

// 格式化金额
function formatAmount(amount: number | undefined) {
  return `¥${(amount || 0).toFixed(2)}`
}

// 获取维修类型标签
function getMaintenanceTypeLabel(type: string | undefined) {
  if (!type) return '-'
  const map: Record<string, string> = {
    Daily: '日常保养',
    Fault: '故障维修',
    Accident: '事故维修',
  }
  return map[type] || type
}

onMounted(() => {
  fetchMaintenanceList()
  fetchVehicleList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.amount-cell {
  font-family: monospace;
  font-weight: 500;
}

.text-gray {
  color: #999;
}

.ellipsis-cell {
  display: inline-block;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
