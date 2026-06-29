<template>
  <div class="page-container">
    <PageHeader title="设施管理" description="管理宿舍房间设施">
      <template #right>
        <a-button type="primary" :disabled="!selectedRoomId" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增设施
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-form-item label="楼栋" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.buildingId"
                placeholder="请选择楼栋"
                allow-clear
                style="width: 150px"
                :options="buildingOptions"
                @change="handleBuildingChange"
              />
            </a-form-item>
            <a-form-item label="房间" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.roomId"
                placeholder="请选择房间"
                allow-clear
                style="width: 180px"
                :options="roomOptions"
                :disabled="!searchForm.buildingId"
                @change="handleRoomChange"
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
      <a-alert
        v-if="!selectedRoomId"
        message="请先选择房间"
        description="选择楼栋和房间后，可查看和管理该房间的设施"
        type="info"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        row-key="id"
        bordered
        :scroll="{ x: 700 }"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'facilityName'">
            <a-tooltip :title="record.facilityName">{{ record.facilityName }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'quantity'">
            {{ record.quantity }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该设施吗？"
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
          <EmptyState :description="selectedRoomId ? '该房间暂无设施' : '请先选择房间'" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增设施' : '编辑设施'"
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
        <a-form-item label="设施名称" name="facilityName">
          <a-input
            v-model:value="formData.facilityName"
            placeholder="请输入设施名称"
            :maxlength="100"
          />
        </a-form-item>
        <a-form-item label="数量" name="quantity">
          <a-input-number
            v-model:value="formData.quantity"
            placeholder="请输入数量"
            style="width: 100%"
            :min="1"
            :precision="0"
          />
        </a-form-item>
        <a-form-item v-if="dialogType === 'edit'" label="状态" name="status">
          <a-select
            v-model:value="formData.status"
            placeholder="请选择状态"
            :options="statusOptions"
          />
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
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getFacilityList,
  createFacility,
  updateFacility,
  deleteFacility,
  getAllBuildings,
  getRoomList,
  type FacilityDto,
  type BuildingListItemDto,
  type RoomDto,
} from '@/api/dormitory'

// 设施状态选项（0=正常/1=损坏/2=报废）
const statusOptions = [
  { label: '正常', value: 0 },
  { label: '损坏', value: 1 },
  { label: '报废', value: 2 },
]

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '设施名称', dataIndex: 'facilityName', key: 'facilityName', width: 200 },
  { title: '数量', dataIndex: 'quantity', key: 'quantity', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  buildingId: undefined as number | undefined,
  roomId: undefined as number | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<FacilityDto[]>([])
const selectedRoomId = ref<number | undefined>(undefined)

// 楼栋和房间选项
const buildingOptions = ref<{ label: string; value: number }[]>([])
const roomOptions = ref<{ label: string; value: number }[]>([])

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentFacilityId = ref<number | null>(null)

const formData = reactive({
  facilityName: '',
  quantity: 1,
  status: 0,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  facilityName: [{ required: true, message: '请输入设施名称', trigger: 'blur' }],
  quantity: [{ required: true, message: '请输入数量', trigger: 'blur' }],
  status: [{ required: true, message: '请选择状态', trigger: 'change' }],
}

// 获取设施列表
async function fetchFacilityList() {
  if (!selectedRoomId.value) {
    tableData.value = []
    return
  }
  loading.value = true
  try {
    const res = await getFacilityList(selectedRoomId.value)
    tableData.value = res || []
  } finally {
    loading.value = false
  }
}

// 获取楼栋列表
async function fetchBuildings() {
  try {
    const res = await getAllBuildings()
    buildingOptions.value = (res || []).map((b: BuildingListItemDto) => ({
      label: b.name,
      value: b.id,
    }))
  } catch (error) {
    console.error('获取楼栋列表失败:', error)
  }
}

// 楼栋变化
async function handleBuildingChange(buildingId: any) {
  searchForm.roomId = undefined
  selectedRoomId.value = undefined
  roomOptions.value = []
  tableData.value = []
  if (!buildingId) return
  try {
    const res = await getRoomList(buildingId)
    roomOptions.value = (res || []).map((r: RoomDto) => ({
      label: `${r.roomNumber} (${getRoomTypeText(r.roomType || '')})`,
      value: r.id,
    }))
  } catch (error) {
    console.error('获取房间列表失败:', error)
  }
}

// 房间变化
function handleRoomChange(roomId: any) {
  selectedRoomId.value = roomId
  fetchFacilityList()
}

// 搜索
function handleSearch() {
  fetchFacilityList()
}

// 重置搜索
function handleReset() {
  searchForm.buildingId = undefined
  searchForm.roomId = undefined
  selectedRoomId.value = undefined
  roomOptions.value = []
  tableData.value = []
}

// 重置表单
function resetForm() {
  formData.facilityName = ''
  formData.quantity = 1
  formData.status = 0
  formData.remark = ''
  currentFacilityId.value = null
}

// 新增
function handleAdd() {
  if (!selectedRoomId.value) {
    message.warning('请先选择房间')
    return
  }
  dialogType.value = 'add'
  resetForm()
  dialogVisible.value = true
}

// 编辑
function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentFacilityId.value = row.id
  formData.facilityName = row.facilityName
  formData.quantity = row.quantity
  formData.status = row.status
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

  if (!selectedRoomId.value) {
    message.warning('请先选择房间')
    return
  }

  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      await createFacility(selectedRoomId.value, {
        facilityName: formData.facilityName,
        quantity: formData.quantity,
        remark: formData.remark || undefined,
      })
      message.success('新增成功')
    } else {
      await updateFacility(selectedRoomId.value, currentFacilityId.value!, {
        facilityName: formData.facilityName,
        quantity: formData.quantity,
        remark: formData.remark || undefined,
        status: formData.status,
      })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchFacilityList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  if (!selectedRoomId.value) return
  try {
    await deleteFacility(selectedRoomId.value, row.id)
    message.success('删除成功')
    fetchFacilityList()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

// 房间类型中文映射
const roomTypeMap: Record<string, string> = {
  standard: '标准间',
  single: '单人间',
  suite: '套间',
}

function getRoomTypeText(type: string): string {
  return roomTypeMap[type] || type
}

// 获取状态标签
function getStatusLabel(status: number) {
  const map: Record<number, string> = {
    0: '正常',
    1: '损坏',
    2: '报废',
  }
  return map[status] || '未知'
}

// 获取状态颜色
function getStatusColor(status: number) {
  const map: Record<number, string> = {
    0: 'green',
    1: 'orange',
    2: 'red',
  }
  return map[status] || 'default'
}

onMounted(() => {
  fetchBuildings()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
