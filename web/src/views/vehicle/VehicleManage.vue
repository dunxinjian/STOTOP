<template>
  <div class="page-container page-container--flush">
    <PageHeader title="车辆台账" description="管理三轮车基础信息">
      <template #left>
        <StatFilterTabs inline v-model:active="searchForm.vehicleStatus" :tabs="statusTabs" @change="handleSearch" />
      </template>
      <template #right>
        <a-input-search v-model:value="searchForm.keyword" placeholder="编码/车牌号" style="width: 200px" allow-clear size="middle" @search="handleSearch" />
        <a-select v-model:value="searchForm.ownershipType" placeholder="权属类型" allow-clear style="width: 120px" size="middle" :options="ownershipOptions" @change="handleSearch" />
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
        <a-button type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增车辆
        </a-button>
      </template>
    </PageHeader>

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1200 }"
      row-key="id"
      empty-text="暂无车辆数据"
      @change="fetchVehicleList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'code'">
          <a-tooltip :title="record.code">{{ record.code }}</a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'ownershipType'">
          <StatusTag :type="record.ownershipType === 1 ? 'info' : 'default'">
            {{ record.ownershipType === 1 ? '公司' : '个人' }}
          </StatusTag>
        </template>
        <template v-if="column.dataIndex === 'vehicleStatus'">
          <StatusTag :type="getVehicleStatusType(record.vehicleStatus)" dot>
            {{ getVehicleStatusText(record.vehicleStatus) }}
          </StatusTag>
        </template>
        <template v-if="column.dataIndex === 'createdTime'">
          {{ formatDate(record.createdTime) }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" @click="handleEdit(record)">
            <EditOutlined />编辑
          </a-button>
          <a-popconfirm
            title="确定删除该车辆吗？"
            ok-text="确定"
            cancel-text="取消"
            @confirm="handleDelete(record)"
          >
            <a-button type="link" danger>
              <DeleteOutlined />删除
            </a-button>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增车辆' : '编辑车辆'"
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
            <a-form-item label="编码" name="code">
              <a-input
                v-model:value="formData.code"
                placeholder="请输入车辆编码"
                :maxlength="50"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="车牌号">
              <a-input
                v-model:value="formData.plateNumber"
                placeholder="请输入车牌号"
                :maxlength="20"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="品牌">
              <a-input
                v-model:value="formData.brand"
                placeholder="请输入品牌"
                :maxlength="50"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="车架号">
              <a-input
                v-model:value="formData.frameNumber"
                placeholder="请输入车架号"
                :maxlength="100"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="权属类型" name="ownershipType">
              <a-select
                v-model:value="formData.ownershipType"
                placeholder="请选择权属类型"
                :options="ownershipOptions.slice(1)"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item v-if="formData.ownershipType === 2" label="所有人姓名">
              <a-input
                v-model:value="formData.ownerName"
                placeholder="请输入所有人姓名"
                :maxlength="50"
              />
            </a-form-item>
            <a-form-item v-else label="所有人ID">
              <a-input-number
                v-model:value="formData.ownerId"
                placeholder="请输入所有人ID"
                style="width: 100%"
                :min="1"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="购入日期">
              <a-date-picker
                v-model:value="formData.purchaseDate"
                placeholder="请选择购入日期"
                style="width: 100%"
                value-format="YYYY-MM-DD"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="购入价格">
              <a-input-number
                v-model:value="formData.purchasePrice"
                placeholder="请输入购入价格"
                style="width: 100%"
                :min="0"
                :precision="2"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="颜色">
              <a-input
                v-model:value="formData.color"
                placeholder="请输入颜色"
                :maxlength="20"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="GPS设备号">
              <a-input
                v-model:value="formData.gpsDeviceNo"
                placeholder="请输入GPS设备号"
                :maxlength="50"
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
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import StatFilterTabs from '@/components/StatFilterTabs.vue'
import {
  getVehicleList,
  getVehicleDetail,
  createVehicle,
  updateVehicle,
  deleteVehicle,
  getVehicleStatistics,
  type VehicleListItemDto,
  type VehicleStatisticsDto,
} from '@/api/vehicle'

// 选项配置
const ownershipOptions = [
  { label: '全部', value: '' },
  { label: '公司', value: 1 },
  { label: '员工个人', value: 2 },
]

// 表格列配置
const tableColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 100 },
  { title: '车牌号', dataIndex: 'plateNumber', key: 'plateNumber', width: 100 },
  { title: '品牌', dataIndex: 'brand', key: 'brand', width: 100 },
  { title: '权属类型', dataIndex: 'ownershipType', key: 'ownershipType', width: 100, align: 'center' as const },
  { title: '所有人', dataIndex: 'ownerName', key: 'ownerName', width: 100 },
  { title: '车辆状态', dataIndex: 'vehicleStatus', key: 'vehicleStatus', width: 100, align: 'center' as const },
  { title: '颜色', dataIndex: 'color', key: 'color', width: 80 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 120 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  ownershipType: '' as number | string,
  vehicleStatus: '' as number | string,
})

// 表格数据
const loading = ref(false)
const tableData = ref<VehicleListItemDto[]>([])
const pagination = ref({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

// 统计数据
const statistics = ref<VehicleStatisticsDto>({
  totalCount: 0,
  statusGroups: [],
  ownershipGroups: [],
})

function getStatusCount(status: number): number {
  const group = statistics.value.statusGroups.find(g => g.status === status)
  return group?.count || 0
}

const statusTabs = computed(() => [
  { key: '', label: '全部', count: statistics.value.totalCount },
  { key: 1, label: '闲置', count: getStatusCount(1), color: 'var(--text-3)' },
  { key: 2, label: '使用中', count: getStatusCount(2), color: 'var(--color-success)' },
  { key: 3, label: '维修中', count: getStatusCount(3), color: 'var(--color-warning)' },
  { key: 4, label: '报废', count: getStatusCount(4), color: 'var(--color-danger)' },
])

function getVehicleStatusType(status: number): 'success' | 'warning' | 'danger' | 'default' {
  const map: Record<number, 'success' | 'warning' | 'danger' | 'default'> = {
    1: 'default',
    2: 'success',
    3: 'warning',
    4: 'danger',
  }
  return map[status] || 'default'
}

function getVehicleStatusText(status: number): string {
  const texts: Record<number, string> = {
    1: '闲置',
    2: '使用中',
    3: '维修中',
    4: '报废',
  }
  return texts[status] || '未知'
}

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentVehicleId = ref<number | null>(null)

const formData = reactive({
  code: '',
  plateNumber: '',
  brand: '',
  frameNumber: '',
  ownershipType: 1,
  ownerId: undefined as number | undefined,
  ownerName: '',
  purchaseDate: '',
  purchasePrice: undefined as number | undefined,
  color: '',
  gpsDeviceNo: '',
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  code: [{ required: true, message: '请输入车辆编码', trigger: 'blur' }],
  ownershipType: [{ required: true, message: '请选择权属类型', trigger: 'change' }],
}

// 获取车辆列表
async function fetchVehicleList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.ownershipType !== '' && searchForm.ownershipType !== undefined) {
      params.ownershipType = searchForm.ownershipType
    }
    if (searchForm.vehicleStatus !== '' && searchForm.vehicleStatus !== undefined) {
      params.vehicleStatus = searchForm.vehicleStatus
    }
    const res = await getVehicleList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.value.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取统计数据
async function fetchStatistics() {
  try {
    const res = await getVehicleStatistics()
    if (res) {
      statistics.value = res
    }
  } catch (error) {
    console.error('获取统计数据失败:', error)
  }
}

// 搜索
function handleSearch() {
  pagination.value.pageIndex = 1
  fetchVehicleList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.ownershipType = ''
  searchForm.vehicleStatus = ''
  pagination.value.pageIndex = 1
  fetchVehicleList()
}

// 重置表单
function resetForm() {
  formData.code = ''
  formData.plateNumber = ''
  formData.brand = ''
  formData.frameNumber = ''
  formData.ownershipType = 1
  formData.ownerId = undefined
  formData.ownerName = ''
  formData.purchaseDate = ''
  formData.purchasePrice = undefined
  formData.color = ''
  formData.gpsDeviceNo = ''
  formData.remark = ''
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  currentVehicleId.value = null
  resetForm()
  dialogVisible.value = true
}

// 编辑
async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentVehicleId.value = row.id
  resetForm()
  dialogVisible.value = true

  try {
    const detail = await getVehicleDetail(row.id)
    if (detail) {
      formData.code = detail.code
      formData.plateNumber = detail.plateNumber || ''
      formData.brand = detail.brand || ''
      formData.frameNumber = detail.frameNumber || ''
      formData.ownershipType = detail.ownershipType
      formData.ownerId = detail.ownerId
      formData.ownerName = detail.ownerName || ''
      formData.purchaseDate = detail.purchaseDate || ''
      formData.purchasePrice = detail.purchasePrice
      formData.color = detail.color || ''
      formData.gpsDeviceNo = detail.gpsDeviceNo || ''
      formData.remark = detail.remark || ''
    }
  } catch (error) {
    console.error('获取车辆详情失败:', error)
    message.error('获取车辆详情失败')
  }
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
    const data: any = {
      code: formData.code,
      plateNumber: formData.plateNumber || undefined,
      brand: formData.brand || undefined,
      frameNumber: formData.frameNumber || undefined,
      ownershipType: formData.ownershipType,
      purchaseDate: formData.purchaseDate || undefined,
      purchasePrice: formData.purchasePrice,
      color: formData.color || undefined,
      gpsDeviceNo: formData.gpsDeviceNo || undefined,
      remark: formData.remark || undefined,
    }
    
    if (formData.ownershipType === 2) {
      data.ownerName = formData.ownerName || undefined
    } else {
      data.ownerId = formData.ownerId
    }

    if (dialogType.value === 'add') {
      await createVehicle(data)
      message.success('新增成功')
    } else {
      await updateVehicle(currentVehicleId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchVehicleList()
    fetchStatistics()
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deleteVehicle(row.id)
    message.success('删除成功')
    fetchVehicleList()
    fetchStatistics()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

onMounted(() => {
  fetchVehicleList()
  fetchStatistics()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
